"""Comparator tests use hand-specified integer streams, not C# output as an oracle."""
import copy
import struct
import unittest
from adp_compare import compare, aggregate


def bundle(values, start=100):
    return dict(schema=1, sample_rate=22320, channels=1, format='s16le',
                case=dict(event=1, state={}, rtpcs={}, timing=[]), start_sample=start,
                chunks=[dict(start_sample=start, pcm_hex=struct.pack('<372h', *values).hex())])


class ComparatorTests(unittest.TestCase):
    def test_hand_calculated_error(self):
        # 372 samples: error=2 throughout, signal=10 throughout.
        result = compare(bundle([10]*372), bundle([12]*372))
        self.assertTrue(result['structure_pass'])
        self.assertEqual(2, result['pcm']['rms_error'])
        self.assertEqual(2, result['pcm']['max_absolute_error'])
        self.assertAlmostEqual(13.979400086720375, result['pcm']['snr_db'])
        self.assertEqual(1488, aggregate([result])['error_energy'])

    def test_timing_mismatch_never_aligned_or_measured(self):
        result = compare(bundle([1]*372), bundle([1]*372, start=101))
        self.assertFalse(result['structure_pass'])
        self.assertEqual(1, result['timing_offset_samples'])
        self.assertIsNone(result['pcm'])

    def test_silence_mismatch_blocks_metrics(self):
        result = compare(bundle([0]+[1]*371), bundle([1]*372))
        self.assertFalse(result['structure']['silence_regions'])
        self.assertIsNone(result['pcm'])

    def test_chunk_size_and_overlap_are_rejected(self):
        bad = bundle([0]*372)
        bad['chunks'][0]['pcm_hex'] = '0000'
        with self.assertRaises(ValueError):
            compare(bad, bad)
        bad = bundle([0]*372)
        bad['chunks'].append(copy.deepcopy(bad['chunks'][0]))
        with self.assertRaises(ValueError):
            compare(bad, bad)

    def test_case_input_mismatch_blocks_metrics(self):
        a, b = bundle([1]*372), bundle([1]*372)
        b['case']['rtpcs'] = {'123': '3F800000'}
        result = compare(a, b)
        self.assertFalse(result['structure']['inputs'])
        self.assertIsNone(result['pcm'])

    def test_no_nan_or_fake_zero_error_for_empty(self):
        a = bundle([0]*372)
        a['chunks'] = []
        result = compare(a, a)
        self.assertTrue(result['structure_pass'])
        self.assertIsNone(result['pcm'])
        self.assertIsNone(aggregate([result])['rms_error'])


if __name__ == '__main__':
    unittest.main()
