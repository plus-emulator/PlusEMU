#!/usr/bin/env python3
"""Check that this server and Volt-Renderer use the same header ids.

Both tables must be on the same AIR build (RevisionsCache Name and ClientHelloMessageComposer.BUILD).
For that build the check fails when
  - an id is neither official in the build (habbo-intelligence index) nor custom: inside a
    CustomPacketRanges range, 65000 or above, or a renderer-only id that no AIR build registers,
  - an id is official only in --previous (a constant the header sync missed),
  - a packet that the renderer's protocol/packet-field-contracts.json nativeAir block binds to both a
    C# and a TypeScript constant has a different id on either side,
  - a CustomPacketRanges.ReservedClientPackets entry has a different id than its renderer constant.
It then prints how many official messages both sides use and which ones only one side uses.

Usage (repository root):
  python3 tools/protocol/compare_client_headers.py --renderer ../Volt-Renderer \
      [--previous WIN63-202610121709-785561319] [--index ~/dev/habbo-intelligence/index] [--list]
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path

CS = r'public const uint (\w+) = (\d+);'
TS = r'public static (\w+)\s*=\s*(-?\d+);'
SIDE = {'server_to_client': 'incoming', 'client_to_server': 'outgoing'}


def constants(path: Path, pattern: str) -> dict[str, int]:
    return {m.group(1): int(m.group(2)) for m in re.finditer(pattern, path.read_text())}


def official(index: Path, build: str) -> dict[tuple[str, int], str]:
    path = index / build / 'messages.jsonl'
    if not path.is_file():
        sys.exit(f'{build}: not in the habbo-intelligence index at {index}')
    rows = (json.loads(line) for line in path.open())
    return {(row['direction'], row['header']): row['class_name'] for row in rows}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--renderer', type=Path, required=True)
    parser.add_argument('--root', type=Path, default=Path.cwd())
    parser.add_argument('--index', type=Path,
                        default=Path(os.environ.get('HABBO_INDEX', '~/dev/habbo-intelligence/index')).expanduser())
    parser.add_argument('--previous', help='build the tables were on before the last sync')
    parser.add_argument('--list', action='store_true', help='print the one-sided official messages')
    args = parser.parse_args()
    root, renderer = args.root.resolve(), args.renderer.resolve()
    messages = renderer / 'packages/communication/src/messages'

    emu_build = re.search(r'Name = "([\w-]+)"', (root / 'Communication/Revisions/RevisionsCache.cs').read_text()).group(1)
    renderer_build = re.search(r"BUILD = '([\w-]+)'",
                               (messages / 'outgoing/handshake/ClientHelloMessageComposer.ts').read_text()).group(1)
    if emu_build != renderer_build:
        print(f'server is on {emu_build}, renderer on {renderer_build}')
        return 1
    build = emu_build
    current = official(args.index, build)
    previous = official(args.index, args.previous) if args.previous else {}
    any_build = set()
    for entry in args.index.iterdir():
        if entry.name.startswith('WIN63-') and (entry / 'messages.jsonl').is_file():
            any_build |= set(official(args.index, entry.name))

    # AIR directions: incoming = server to client.
    emu = {'outgoing': constants(root / 'Communication/Packets/Incoming/ClientPacketHeader.cs', CS),
           'incoming': constants(root / 'Communication/Packets/Outgoing/ServerPacketHeader.cs', CS)}
    ts = {'incoming': constants(messages / 'incoming/IncomingHeader.ts', TS),
          'outgoing': constants(messages / 'outgoing/OutgoingHeader.ts', TS)}
    ranges_text = (root / 'Communication/Packets/CustomPacketRanges.cs').read_text()
    ranges = [('outgoing' if d == 'ClientToServer' else 'incoming', int(s), int(e)) for d, s, e in
              re.findall(r'PacketRangeDirection\.(\w+), (\d+), (\d+)', ranges_text)]
    reserved = re.findall(r'new\("(\w+)", (\d+), "', ranges_text)

    errors = []

    def check(side: str, table: dict, direction: str):
        for name, header in table.items():
            if header < 0 or (direction, header) in current:
                continue
            if (direction, header) in previous:
                errors.append(f'{side} {direction} {name} = {header} is a {args.previous} id, not one of {build}')
            elif side == 'server' and not (header >= 65000 or any(
                    d == direction and s <= header <= e for d, s, e in ranges)):
                errors.append(f'server {direction} {name} = {header} is not official in {build} and outside the custom ranges')
            elif side == 'renderer' and (direction, header) in any_build and not (header >= 65000 or any(
                    d == direction and s <= header <= e for d, s, e in ranges)):
                errors.append(f'renderer {direction} {name} = {header} is an AIR id of another build, not of {build}')

    for direction in ('incoming', 'outgoing'):
        check('server', emu[direction], direction)
        check('renderer', ts[direction], direction)

    manifest = json.loads((renderer / 'protocol/packet-field-contracts.json').read_text())
    native = manifest.get('nativeAir', {})
    if native.get('build') != build:
        errors.append(f'renderer nativeAir is {native.get("build")}, not {build}')
    else:
        for packet in native['packets']:
            direction, header = SIDE[packet['direction']], packet['header']
            bound = [('server', name, emu[direction].get(name)) for name in packet['localBindings']['csharp']] + \
                    [('renderer', name, ts[direction].get(name)) for name in packet['localBindings']['typescript']]
            for side, name, value in bound:
                if value is not None and value != header:
                    errors.append(f'{side} {direction} {name} = {value}, but nativeAir binds it to {header} '
                                  f'({packet["sourceClass"]})')

    for name, header in reserved:
        if ts['outgoing'].get(name) != int(header):
            errors.append(f'reserved client packet {name} = {header}, renderer has {ts["outgoing"].get(name)}')

    both = only_emu = only_ts = 0
    one_sided = []
    for direction in ('incoming', 'outgoing'):
        emu_ids = {v: n for n, v in emu[direction].items() if (direction, v) in current}
        ts_ids = {v: n for n, v in ts[direction].items() if (direction, v) in current}
        both += len(emu_ids.keys() & ts_ids.keys())
        for header in sorted(emu_ids.keys() - ts_ids.keys()):
            only_emu += 1
            one_sided.append(f'server only   {direction} {header} {emu_ids[header]} ({current[(direction, header)]})')
        for header in sorted(ts_ids.keys() - emu_ids.keys()):
            only_ts += 1
            one_sided.append(f'renderer only {direction} {header} {ts_ids[header]} ({current[(direction, header)]})')

    print(f'{build}: {both} official messages on both sides, {only_emu} server only, {only_ts} renderer only')
    if args.list:
        print('\n'.join(one_sided))
    if errors:
        print('\n'.join(errors), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
