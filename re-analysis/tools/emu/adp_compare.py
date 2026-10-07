"""ADP-1 structure-first measurement. No alignment, tolerance or pass threshold.

Input JSON: schema=1, sample_rate=22320, channels=1, format=s16le,
case (the identical event/state/RTPC/timing input object), start_sample,
chunks=[{start_sample, pcm_hex}]. Each captured chunk must be 744 bytes.
Positions are absolute on the common case clock, never rebased to first sound.
Reference provenance is recorded by the renderer; this tool cannot authenticate it.
"""
import argparse
import json
import math
import struct
from pathlib import Path


def decode(bundle):
    for key, value in [('schema', 1), ('sample_rate', 22320),
                       ('channels', 1), ('format', 's16le')]:
        if bundle.get(key) != value:
            raise ValueError(f'{key}: expected {value!r}')
    if not isinstance(bundle.get('case'), dict):
        raise ValueError('case must contain the common render inputs')
    cursor = bundle['start_sample']
    if type(cursor) is not int:
        raise ValueError('start_sample must be an integer')
    samples, bounds = [], []
    for chunk in bundle['chunks']:
        if chunk['start_sample'] != cursor:
            raise ValueError('gap, overlap or reordered chunk')
        data = bytes.fromhex(chunk['pcm_hex'])
        if len(data) != 744:
            raise ValueError(f'chunk length {len(data)} != 744')
        bounds.append((cursor, cursor + 372))
        samples.extend(struct.unpack('<372h', data))
        cursor += 372
    return samples, bounds, cursor


def regions(samples, start):
    """Maximal exact-zero/nonzero regions. No amplitude threshold is invented."""
    out = []
    for i, value in enumerate(samples):
        silent = value == 0
        if out and out[-1]['silent'] == silent:
            out[-1]['end_sample'] = start + i + 1
        else:
            out.append(dict(start_sample=start+i, end_sample=start+i+1, silent=silent))
    return out


def compare(reference, candidate):
    a, ab, ae = decode(reference)
    b, bb, be = decode(candidate)
    checks = dict(inputs=reference['case'] == candidate['case'],
                  frame_count=len(a) == len(b), chunk_boundaries=ab == bb,
                  start=reference['start_sample'] == candidate['start_sample'],
                  end=ae == be,
                  silence_regions=regions(a, reference['start_sample']) ==
                                  regions(b, candidate['start_sample']))
    result = dict(schema=1, structure=checks, structure_pass=all(checks.values()),
                  timing_offset_samples=candidate['start_sample']-reference['start_sample'],
                  reference_provenance=reference.get('provenance'),
                  candidate_provenance=candidate.get('provenance'), pcm=None)
    if not result['structure_pass']:
        return result
    # Empty streams have no measured PCM, not a zero-error measurement.
    if not a:
        result['pcm_reason'] = 'empty stream'
        return result
    differences = [y-x for x, y in zip(a, b)]
    error_energy = sum(d*d for d in differences)
    signal_energy = sum(x*x for x in a)
    result['pcm'] = dict(sample_count=len(a), signal_energy=signal_energy,
                        error_energy=error_energy,
                        rms_error=math.sqrt(error_energy/len(a)),
                        max_absolute_error=max(abs(d) for d in differences),
                        snr_db=10*math.log10(signal_energy/error_energy)
                        if signal_energy and error_energy else None,
                        snr_reason='zero error' if not error_energy else
                                   'zero reference energy' if not signal_energy else None)
    return result


def aggregate(results):
    eligible = [r['pcm'] for r in results if r['structure_pass'] and r['pcm']]
    n = sum(r['sample_count'] for r in eligible)
    signal = sum(r['signal_energy'] for r in eligible)
    error = sum(r['error_energy'] for r in eligible)
    return dict(case_count=len(results), structure_failures=sum(not r['structure_pass'] for r in results),
                measured_case_count=len(eligible), sample_count=n,
                rms_error=math.sqrt(error/n) if n else None,
                max_absolute_error=max((r['max_absolute_error'] for r in eligible), default=None),
                snr_db=10*math.log10(signal/error) if signal and error else None,
                signal_energy=signal, error_energy=error)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('candidate', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = compare(json.loads(args.reference.read_text()), json.loads(args.candidate.read_text()))
    report = dict(schema=1, cases=[result], overall=aggregate([result]))
    args.output.write_text(json.dumps(report, indent=2, allow_nan=False)+'\n')
    return 0 if result['structure_pass'] else 2


if __name__ == '__main__':
    raise SystemExit(main())
