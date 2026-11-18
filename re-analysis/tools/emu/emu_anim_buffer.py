"""ANIM-BUFFER-OVERFLOW-1: run the shipped engine at the recorded tag-13 byte gate.

This is a native accounting/completion regression, NOT a firmware playback oracle.
No hardware bundle is written or committed. The recorded counter inputs below are
from 20261118-075139-LLM-SESSION/frames.jsonl at 230847.44..230883.74 ms.
Run with the dependencies used by emu_common.py (lief, capstone, unicorn).
"""
import hashlib
import json
import argparse
import base64
import struct
from pathlib import Path
from emu_common import Emu, BASE, UC_HOOK_CODE, UC_ARM_REG_PC
from so import ELF, RAW
from unicorn.arm_const import UC_ARM_REG_R0, UC_ARM_REG_R5


def verify_recorded_inputs(bundle):
    """Recompute the counter prefix; deduplicate reliable resends across sequence wrap.

    Only the outbound keyframe range 8E..9B is counted. End (9A) is
    also an audio frame; Start (9B) is not. No firmware allocator is inferred.
    """
    path = Path(bundle) / 'frames.jsonl'
    high = [None, None]
    seen = [set(), set()]
    streamed = frames = played = played_frames = 0
    for line_number, line in enumerate(path.open(encoding='utf-8'), 1):
        row = json.loads(line)
        at = row['atMs']
        if at > 230864:
            break
        raw = base64.b64decode(row['rawBase64'])
        if len(raw) < 14 or raw[:7] != b'COZ\x03RE\x01':
            continue
        direction = int(row['outbound'])
        kind, sequence = raw[7], struct.unpack_from('<H', raw, 8)[0]
        messages = []
        if kind in (7, 8, 9):
            offset = 14
            while offset < len(raw):
                kind, size = struct.unpack_from('<BH', raw, offset)
                offset += 3
                seq = sequence if kind in (1, 2, 3, 4, 6) else 0
                messages.append((kind, seq, raw[offset:offset + size]))
                if seq:
                    sequence = sequence % 65534 + 1
                offset += size
        else:
            messages.append((kind, sequence, raw[14:]))
        for kind, seq, payload in messages:
            if seq:
                previous = high[direction]
                absolute = seq if previous is None else previous + ((seq - previous + 32767) % 65534 - 32767)
                if absolute in seen[direction]:
                    continue
                seen[direction].add(absolute)
                high[direction] = absolute if previous is None else max(previous, absolute)
            # Outbound animation keyframes in this capture are single data
            # messages. Do not silently omit an unexpected fragmented send.
            assert not (direction and kind == 6), ('multipart outbound', line_number)
            if kind not in (4, 5) or not payload:
                continue
            tag = payload[0]
            if direction and 0x8E <= tag <= 0x9B:
                if at > 230860 and tag == 0x8E:
                    assert (streamed, frames, played, played_frames, len(payload)) == (479685, 5407, 472279, 5395, 745)
                    with path.open('rb') as source:
                        digest = hashlib.file_digest(source, 'sha256').hexdigest()
                    return dict(frame_line=line_number, at_ms=at, frames_sha256=digest)
                streamed += len(payload)
                frames += tag in (0x8E, 0x8F, 0x9A)
            elif not direction and tag == 0xF1:
                _, played, played_frames = struct.unpack_from('<Iii', payload, 1)
    raise AssertionError('recorded tag-13 packet not found')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', help='Optional private session directory; verify the recorded counter inputs')
    parser.add_argument('--byte-budget', type=int, default=8192,
                        help='Operator-policy constant substitution only; original native value is 8192')
    args = parser.parse_args()
    if not 1 <= args.byte_budget <= 30000:
        parser.error('--byte-budget must be 1..30000')
    capture = verify_recorded_inputs(args.bundle) if args.bundle else None
    e = Emu()
    if args.byte_budget != 8192:
        # Policy experiment, not recovered native configuration. Original
        # 0x0057C706 is ADD.W r5,r0,#8192 without S. Substitute that literal
        # addition only, leave flags from the preceding CMP and all following
        # native branches/clamps untouched. 7370 is not a Thumb modified immediate.
        def policy_add(uc, address, size, data):
            uc.reg_write(UC_ARM_REG_R5, (uc.reg_read(UC_ARM_REG_R0) + args.byte_budget) & 0xffffffff)
            uc.reg_write(UC_ARM_REG_PC, 0x0057C70B)
        e.uc.hook_add(UC_HOOK_CODE, policy_add, begin=0x0057C706, end=0x0057C706)
    e.hook(0x4A4540, lambda emu: 0)  # C9 negative-budget warning only, no state output

    def forward(plt, fragment):
        matches = [s for s in ELF.dynamic_symbols if s.value and s.name.endswith(fragment)]
        assert len(matches) == 1, (fragment, matches)
        target = matches[0].value

        def go(uc, address, size, data):
            uc.reg_write(UC_ARM_REG_PC, target)
        e.uc.hook_add(UC_HOOK_CODE, go, begin=plt, end=plt)

    forward(0x4ADC00, 'RobotInterface13EngineToRobot4SizeEv')
    names = ['11AudioSample', '12AudioSilence', '13RecordHeading',
             '21TurnToRecordedHeading', '9HeadAngle', '10LiftHeight', '5Event',
             '9FaceImage', '14BackpackLights', '10BodyMotion',
             '14EndOfAnimation', '16StartOfAnimation']
    for i, name in enumerate(names):
        forward(0x4C8030 + i * 12, 'AnimKeyFrame' + name + '4SizeEv')

    # Only transport success and destruction are substituted. Size, byte/frame
    # gates, allowance decrements, list removal and counter increments run natively.
    e.hook(0x4A40CC, lambda emu: 0)
    e.hook(0x4A5200, lambda emu: 0)
    stream, robot, node, message = [BASE + i * 0x1000 for i in range(4)]
    sent = []
    e.hook(0x4A5368, lambda emu: sent.append(emu.reg(1)) or 0)
    sizes = {}
    for tag, expected in [(0x8E, 745), (0x8F, 1), (0x93, 4), (0x94, 4),
                          (0x97, 56), (0x98, 11), (0x99, 5), (0x9A, 1), (0x9B, 2)]:
        e.w8(message, tag)
        if tag == 0x97:
            # FaceImage's member is vector<byte> + uint16 wire length.
            e.w32(message + 4, BASE + 0x8000)
            e.w32(message + 8, BASE + 0x8000 + expected - 3)
        actual = e.call(0x7ABB91, message)
        assert actual == expected, (hex(tag), actual, expected)
        sizes[f'{tag:02x}'] = actual

    # Prefix sums of unique reliable CLAD sends through 230829.69 ms; the
    # AnimationState at 230847.44 ms reports bytes=472279, frames=5395.
    e.w32(robot + 0x238, 472279)
    e.w32(robot + 0x23C, 479685)
    e.w32(robot + 0x240, 5395)
    e.w32(robot + 0x244, 5407)
    e.call(0x57C6F1, stream, robot)
    before = [e.r32(stream + 0x98), e.r32(stream + 0x9C)]
    expected_bytes = min(max(472279 - 479685 + args.byte_budget, 0), 30000)
    assert before == [expected_bytes, 2], before

    e.w8(message, 0x8E)
    e.w32(stream + 0x8C, node)
    e.w32(stream + 0x90, node)
    e.w32(stream + 0x94, 1)
    e.w32(node, stream + 0x8C)
    e.w32(node + 4, stream + 0x8C)
    e.w32(node + 8, message)
    result = e.call(0x57BF61, stream, robot)
    after = [e.r32(stream + 0x98), e.r32(stream + 0x9C),
             e.r32(robot + 0x23C), e.r32(robot + 0x244)]
    admitted = expected_bytes >= 745
    expected_after = [expected_bytes - 745, 1, 480430, 5408] if admitted else [expected_bytes, 2, 479685, 5407]
    assert result == 0 and len(sent) == int(admitted) and after == expected_after, (result, sent, after)

    # Actual EndAnimationHandler and CheckIfDone: CA already seen, matching CB,
    # one loop. There is no endSent/HasFramesLeft/buffer-full predicate here.
    action, holder, ended = BASE + 0x5000, BASE + 0x6000, BASE + 0x7000
    e.w32(holder + 4, action)
    e.w8(action + 0x88, 1)
    e.w8(action + 0x8B, 13)
    e.w32(action + 0x84, 1)
    e.w8(ended, 13)
    e.hook(0x4AAF0C, lambda emu: ended)  # typed union accessor only
    e.hook(0x4A505C, lambda emu: 0)      # log only
    e.call(0x54541D, holder, BASE + 0x9000)
    assert e.r8(action + 0x89) == 1
    completion = e.call(0x5441C1, action)
    assert completion == 0, completion
    print(json.dumps(dict(binary_sha256=hashlib.sha256(RAW).hexdigest(), capture=capture,
                          sizes=sizes, byte_budget_constant=args.byte_budget,
                          constant_override_site=None if args.byte_budget == 8192 else "0x0057C706 ADD.W literal substitution; remainder native",
                          budget_before=before, drain_result=result,
                          packet_admitted=admitted, after=after,
                          early_matching_end_action_result=completion,
                          firmware_playback='MISSING: firmware buffer allocation and flush semantics; not emulated'),
                     indent=2))


if __name__ == '__main__':
    main()

