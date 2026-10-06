#!/usr/bin/env python3
"""DRAFT: operator-run M1-033/M1-043 evidence collection. Never commits."""
import argparse
import datetime as dt
import hashlib
import json
import platform
import shutil
import socket
import struct
import subprocess
import sys
import time
from pathlib import Path

TEST_ID = 'M1-PACKED-ZERO'
PREFIX = b'COZ\x03RE\x01'
RECORDS = ('M1-033', 'M1-043')
IMU = b'\x4a' + struct.pack('<I', 1)  # read-only IMURequest.LengthMs, catalog


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def decode(raw):
    if len(raw) < 14 or raw[:7] != PREFIX:
        raise ValueError('not a reliable frame')
    typ, lo, hi, ack = struct.unpack_from('<BHHH', raw, 7)
    body = raw[14:]
    subs = []
    if typ in (7, 8, 9):
        while body:
            if len(body) < 3:
                raise ValueError('short subheader')
            kind, size = struct.unpack_from('<BH', body)
            body = body[3:]
            if len(body) < size:
                raise ValueError('short subpayload')
            subs.append((kind, body[:size]))
            body = body[size:]
    else:
        subs.append((typ, body))
    return typ, lo, hi, ack, subs


class Bundle:
    def __init__(self, root, args):
        self.start = time.monotonic()
        parent = root / 're-analysis/acceptance/hardware'
        self.path = parent / (dt.datetime.now().strftime('%Y%m%d-%H%M%S-') + TEST_ID)
        self.path.mkdir()  # fail on collision; never overwrite evidence
        self.events = (self.path / 'events.jsonl').open('w', encoding='utf-8')
        self.frames = (self.path / 'frames.jsonl').open('w', encoding='utf-8')
        script = Path(__file__).resolve()
        shutil.copy2(script, self.path / script.name)
        manifest = json.loads((root / 're-analysis/fidelity_manifest.json').read_text('utf-8'))
        records = {r['id']: r for r in manifest['records'] if r['id'] in RECORDS}
        if set(records) != set(RECORDS):
            raise ValueError('missing fidelity record')
        self.write('records.json', records)
        self.env = dict(test=TEST_ID, draft=True, scriptSha256=sha(script),
                        startUtc=dt.datetime.now(dt.timezone.utc).isoformat(),
                        python=sys.version, os=platform.platform(), arguments=vars(args))
        for name, cmd in [('gitHead', ['git', 'rev-parse', 'HEAD']),
                          ('gitStatus', ['git', 'status', '--porcelain'])]:
            run = subprocess.run(cmd, cwd=root, capture_output=True, text=True)
            self.env[name] = dict(exitCode=run.returncode, stdout=run.stdout, stderr=run.stderr)
        self.write('env.json', self.env)

    def write(self, name, value):
        (self.path / name).write_text(json.dumps(value, indent=2) + '\n', 'utf-8')

    def log(self, event, **fields):
        self.events.write(json.dumps(dict(tMs=(time.monotonic()-self.start)*1000,
                                         event=event, **fields)) + '\n')
        self.events.flush()

    def capture(self, direction, raw, phase, endpoint):
        self.frames.write(json.dumps(dict(tMs=(time.monotonic()-self.start)*1000,
                             utc=dt.datetime.now(dt.timezone.utc).isoformat(),
                             dir=direction, phase=phase, endpoint=endpoint,
                             length=len(raw), hex=raw.hex())) + '\n')
        self.frames.flush()


class Peer:
    def __init__(self, bundle, ip, phase):
        self.b = bundle
        self.phase = phase
        self.remote = (ip, 5551)
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.bind(('', 0))
        self.sock.settimeout(.01)
        self.ack = 0
        self.seen = set()
        self.echoes = set()
        self.responses = []
        self.last_send = 0
        self.ping_count = 0
        self.next_out = 2
        self.b.log('socket', phase=phase, local=self.sock.getsockname(), remote=self.remote)

    def send(self, typ, subs, lo=0, hi=0):
        # Harness spacing; no background production transport shares this socket.
        delay = .003 - (time.monotonic()-self.last_send)
        if delay > 0:
            time.sleep(delay)
        body = b''.join(struct.pack('<BH', k, len(v))+v for k, v in subs) if typ in (7,8,9) else subs[0][1]
        raw = PREFIX + struct.pack('<BHHH', typ, lo, hi, self.ack) + body
        count = self.sock.sendto(raw, self.remote)
        self.last_send = time.monotonic()
        self.b.capture('out', raw, self.phase, self.remote)
        if count != len(raw):
            raise RuntimeError('short send')

    def ping(self):
        self.ping_count += 1
        stamp = (time.monotonic()-self.b.start)*1000
        return struct.pack('<dIIB', stamp, self.ping_count, 0, 0)

    def poll(self, seconds, connect=False):
        end = time.monotonic()+seconds
        while time.monotonic() < end:
            if time.monotonic()-self.last_send > (.1 if connect else .0333):
                if connect:
                    self.send(1, [(1,b'')], 1, 1)
                else:
                    self.send(11, [(11,self.ping())])
            try:
                raw, src = self.sock.recvfrom(65535)
            except socket.timeout:
                continue
            self.b.capture('in', raw, self.phase, src)
            if src != self.remote:
                self.b.log('foreign-source', phase=self.phase, source=src)
                continue
            try:
                typ, lo, hi, ack, subs = decode(raw)
            except ValueError as exc:
                self.b.log('decode-error', phase=self.phase, error=str(exc))
                continue
            seq = lo
            for kind, payload in subs:
                reliable = lo != 0 and kind not in (5,7,8,9,10,11)
                if reliable:
                    self.seen.add(seq)
                    seq = 1 if seq == 65534 else seq+1
                if kind == 11 and len(payload) == 17:
                    self.echoes.add(payload[:8].hex())  # do not require isReply; firmware varies
                self.responses.append(dict(type=kind, ack=ack, payload=payload.hex()))
            while (1 if self.ack == 65534 else self.ack+1) in self.seen:
                self.ack = 1 if self.ack == 65534 else self.ack+1
            if connect and any(r['type']==2 and r['ack']==1 for r in self.responses):
                return True
        return not connect

    def close(self):
        try:
            self.send(3, [(3,b'')], self.next_out, self.next_out)
        finally:
            self.sock.close()


def packed_trial(b, ip, kind, packed):
    phase = f'type-{kind}-' + ('packed' if packed else 'single-control')
    peer = Peer(b, ip, phase)
    try:
        peer.send(1, [(1,b'')], 1, 1)
        if not peer.poll(5, connect=True):
            return dict(phase=phase, observed=False, reason='connection response absent')
        peer.poll(.25)
        # New connection per trial. These are the only uses of reliable ids 2/3.
        peer.responses.clear()
        tokens = []
        if kind == 7:
            subs, lo, hi = [(4,IMU), (4,IMU)], 2, 3
        elif kind == 8:
            subs, lo, hi = [(11,peer.ping()), (11,peer.ping())], 0, 0
        else:
            subs, lo, hi = [(4,IMU), (11,peer.ping()), (4,IMU)], 2, 3
        tokens = [v[:8].hex() for k,v in subs if k==11]
        if packed:
            peer.next_out = 4 if lo else 2
            peer.send(kind, subs, lo, hi)
        else:
            seq = lo
            for k,v in subs:
                s = seq if k==4 else 0
                peer.send(k, [(k,v)], s, s)
                if k==4:
                    seq += 1
                    peer.next_out = seq
        peer.poll(2)
        acked = any(r['ack']==3 for r in peer.responses) if lo else None
        echoed = all(t in peer.echoes for t in tokens) if tokens else None
        return dict(phase=phase, observed=(acked is not False and echoed is not False),
                    finalAckObserved=acked, allProbeTimestampsEchoed=echoed,
                    tokens=tokens, responses=peer.responses)
    finally:
        peer.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode', choices=['packed','zero','both'], required=True)
    parser.add_argument('--robot-ip', default='172.31.1.1')
    parser.add_argument('--firmware-label', required=True, help='operator-reported robot/phone/app versions; raw firmware response remains primary')
    parser.add_argument('--phone-ip')
    parser.add_argument('--phone-port', type=int)
    parser.add_argument('--phone-trace', help='completed Android native trace to archive, not auto-adjudicate')
    args = parser.parse_args()
    if args.mode in ('zero','both') and (not args.phone_ip or not args.phone_port):
        parser.error('zero mode requires the actual engine-bound phone UDP endpoint')
    root = Path(__file__).resolve().parents[4]
    b = Bundle(root, args)
    results = []
    error = None
    try:
        if args.mode in ('packed','both'):
            for kind in (7,8,9):
                control = packed_trial(b,args.robot_ip,kind,False)
                time.sleep(.5)
                trial = packed_trial(b,args.robot_ip,kind,True)
                results.append(dict(id=f'packed-{kind}', records=['M1-033'],
                    verdict='OBSERVED_ACCEPTANCE' if control['observed'] and trial['observed'] else 'INCONCLUSIVE',
                    control=control, trial=trial))
                time.sleep(.5)
        if args.mode in ('zero','both'):
            target = (args.phone_ip,args.phone_port)
            with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as sock:
                sock.bind(('',0))
                b.log('zero-injection', local=sock.getsockname(), remote=target)
                count = sock.sendto(b'',target)
                b.capture('out',b'','phone-zero',target)
                b.log('zero-send-return', count=count)
                time.sleep(2)  # allow independently running trace collector to flush
            results.append(dict(id='phone-zero', records=['M1-043'], verdict='INCONCLUSIVE',
                reason='send is not proof of recvmsg(0), stale errno, warning, reopen or drain termination; manager must correlate Android trace'))
        if args.phone_trace:
            source=Path(args.phone_trace)
            shutil.copy2(source,b.path/'phone-native-trace.txt')
            b.env['phoneTraceSha256']=sha(source)
    except (Exception, KeyboardInterrupt) as exc:
        error = repr(exc)
        b.log('aborted', error=error)
    finally:
        b.env['endUtc']=dt.datetime.now(dt.timezone.utc).isoformat()
        b.write('env.json',b.env)
        b.write('result.json',dict(test=TEST_ID,draft=True,records=list(RECORDS),
                   overall='ERROR' if error else 'REVIEW_REQUIRED',error=error,checks=results,
                   uncertainties=['M1-043 phone-thread errno requires correlated Android trace',
                     'Acceptance is limited to these payloads, ordering and firmware; absence of replies does not establish rejection',
                     'Reliable ack establishes transport consumption, not complete application handling of IMU requests']))
        b.events.close()
        b.frames.close()
        for source in (Path(__file__).with_suffix('.md'),):
            if source.exists(): shutil.copy2(source,b.path/'README.md')
        b.write('hashes.json',{f.name:sha(f) for f in b.path.iterdir() if f.is_file()})
    print(b.path)
    return 1 if error else 0


if __name__ == '__main__':
    sys.exit(main())
