#!/usr/bin/env python3
"""Move this repository's official Habbo AIR header ids to another AIR build.

Header ids are reshuffled by Habbo on every AIR build, and the opaque class names are renumbered,
so neither carries over on its own. This tool maps every header constant through its AIR identity:

    our id -> same id in the identity map (old build -> new build, matched by class body)
           -> header id of the paired class in the target build (habbo-intelligence index)

The identity map is generated data and is never committed. By default it is read from
$HABBO_IDENTITY or ~/dev/habbo-intelligence/identity/<from>__<to>.json, which the habbo-intelligence
update step writes before it removes the old build from the index. If that file is missing and both builds
are still indexed, the map is computed with `habbo-pair diff <from> <to> --json --full`.

Ids the current build does not register (custom ranges, 65000+ placeholders, -1 unsupported) are
left alone, but the run fails if one of them is an official id of the target build. A constant whose
class is unmatched or in an unresolved tie of the identity map also fails the run.

Inputs
  --index DIR     habbo-intelligence index (default $HABBO_INDEX or ~/dev/habbo-intelligence/index).
                  Only the target build must be indexed (messages.jsonl and manifest.json).
  --identity DIR  folder of identity maps (default $HABBO_IDENTITY or ~/dev/habbo-intelligence/identity)
  --map FILE      identity map JSON, used instead of the folder lookup: {"schema": 1, "fromBuild", "toBuild",
                  "pairs": [{"direction", "fromHeader", "fromClass", "toHeader", "toClass", ...}],
                  "unresolved": [{"direction", "fromCandidates": [[id, class], ...], ...}]}.
                  Directions are the AIR client's: incoming = server to client.
  --to BUILD      target build (default: the map's toBuild, else the newest WIN63 build in the index).
  --from BUILD    current build (default: read from the repository, see TARGETS).

  --check         exit 1 if the repository is not on --to (no writes)
  --dry-run       print what would change

Usage (repository root):
  python3 tools/protocol/sync_air_headers.py
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

DIRECTIONS = ('incoming', 'outgoing')


def read(path: Path) -> str:
    with path.open(encoding='utf-8', newline='') as file:  # keep CRLF / LF exactly as committed
        return file.read()


def write(path: Path, text: str):
    with path.open('w', encoding='utf-8', newline='') as file:
        file.write(text)


# ---------------------------------------------------------------- index and identity

class Build:
    def __init__(self, index: Path, build: str):
        root = index / build
        if not (root / 'messages.jsonl').is_file():
            sys.exit(f'{build}: not in the habbo-intelligence index at {index}; index it first')
        self.id = build
        self.manifest = json.loads((root / 'manifest.json').read_text())
        self.by_header: dict[tuple[str, int], dict] = {}
        self.by_class: dict[tuple[str, str], dict] = {}
        for line in (root / 'messages.jsonl').open():
            row = json.loads(line)
            key = (row['direction'], row['header'])
            if key in self.by_header:
                sys.exit(f'{build}: duplicate {key}')
            self.by_header[key] = row
            self.by_class[(row['direction'], row['class_name'])] = row


def newest_build(index: Path) -> str:
    builds = sorted(p.name for p in index.glob('WIN63-*') if (p / 'messages.jsonl').is_file())
    if not builds:
        sys.exit(f'no WIN63 build in the habbo-intelligence index at {index}')
    return builds[-1]


def compute_identity(index: Path, current: str, target: str) -> dict:
    """The identity map from `habbo-pair diff`, for when both builds are still indexed."""
    tool = shutil.which('habbo-pair') or str(Path('~/dev/habbo-intelligence/pair/target/release/habbo-pair').expanduser())
    result = subprocess.run([tool, '--index', str(index), 'diff', current, target, '--json', '--full'],
                            capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f'no identity map {current}__{target}.json and habbo-pair diff failed:\n{result.stderr.strip()}')
    diff = json.loads(result.stdout)
    if diff['fuzzy']:
        sys.exit(f'habbo-pair diff {current} {target} has {len(diff["fuzzy"])} fuzzy matches; '
                 f'review them and write the identity map by hand')
    return {
        'schema': 1, 'fromBuild': current, 'toBuild': target,
        'source': 'habbo-pair diff',
        'pairs': [{'direction': p['dir'], 'fromHeader': p['old_header'], 'fromClass': p['old_class'],
                   'toHeader': p['new_header'], 'toClass': p['new_class'], 'evidence': p['how']}
                  for p in diff['pairs']],
        'unresolved': [],
    }


def load_identity(args, current: str, target: str) -> tuple[dict, bytes]:
    """The identity map for current -> target and its bytes (for the manifest audit hash)."""
    path = args.map or args.identity / f'{current}__{target}.json'
    if path.is_file():
        raw = path.read_bytes()
        identity = json.loads(raw)
    elif args.map:
        sys.exit(f'{path}: not found')
    else:
        identity = compute_identity(args.index, current, target)
        raw = (json.dumps(identity, indent=1, sort_keys=True) + '\n').encode()
    if identity['fromBuild'] != current or identity['toBuild'] != target:
        sys.exit(f'identity map goes {identity["fromBuild"]} -> {identity["toBuild"]}, '
                 f'need {current} -> {target}')
    return identity, raw


class Resolver:
    """Maps (direction, id in the current build) to the id in the target build."""

    def __init__(self, index: Path, current: str, identity: dict, target: str):
        self.current_id = current
        self.target = Build(index, target)
        self.pairs = {}
        self.by_header = {}
        for p in identity['pairs']:
            t = self.target.by_class.get((p['direction'], p['toClass']))
            if t is None or t['header'] != p['toHeader']:
                sys.exit(f'identity map pair {p} does not match the {target} index')
            self.pairs[(p['direction'], p['fromClass'])] = p
            self.by_header[(p['direction'], p['fromHeader'])] = p
        self.unresolved = {(u['direction'], header): u for u in identity.get('unresolved', [])
                           for header, _ in u['fromCandidates']}

    def resolve(self, direction: str, header: int, symbol: str, foreign: bool = False) -> int | None:
        """New id, or None for a non-official id. Raises on an unmatched official id, and on a custom id
        that is official in the target build unless it is another program's id (foreign)."""
        key = (direction, header)
        if key in self.unresolved:
            raise LookupError(f'{symbol} = {header} is in an unresolved tie: {self.unresolved[key]}')
        pair = self.by_header.get(key)
        if pair is None:
            hit = None if foreign else self.target.by_header.get(key)
            if hit is not None:
                raise LookupError(f'{symbol} = {header} has no identity from {self.current_id} but is '
                                  f'{direction} {hit["class_name"]} in {self.target.id}')
            return None
        return pair['toHeader']

    def pair(self, direction: str, header: int) -> dict | None:
        return self.by_header.get((direction, header))


# ---------------------------------------------------------------- constant files

class ConstantFile:
    """A source file of `NAME = <int>;` header constants, rewritten in place. A trailing `//` comment that
    names the constant's own opaque AIR class gets the target build's class name."""

    COMMENT = r'(?P<comment>[ \t]*//[^\r\n]*)?'

    def __init__(self, path: Path, pattern: str, direction: str):
        self.path, self.direction = path, direction
        self.pattern = re.compile(pattern + self.COMMENT, re.M)
        self.text = read(path)

    def constants(self) -> list[tuple[str, int]]:
        return [(m.group('name'), int(m.group('id'))) for m in self.pattern.finditer(self.text)]

    def remap(self, resolver: Resolver, errors: list, stats: dict):
        def sub(m: re.Match) -> str:
            name, old = m.group('name'), int(m.group('id'))
            try:
                new = resolver.resolve(self.direction, old, f'{self.path.name}:{name}')
            except LookupError as error:
                errors.append(str(error))
                return m.group(0)
            if new is None:
                stats['custom'] += 1
                return m.group(0)
            stats['changed' if new != old else 'kept'] += 1
            comment = m.group('comment') or ''
            pair = resolver.pair(self.direction, old)
            if comment and re.fullmatch(r'class_\d+', pair['fromClass']):
                comment = re.sub(rf'\b{pair["fromClass"]}\b', pair['toClass'], comment)
            start = m.start()
            return (m.group(0)[:m.start('id') - start] + str(new)
                    + m.group(0)[m.end('id') - start:(m.start('comment') if m.group('comment') else m.end()) - start]
                    + comment)

        self.text = self.pattern.sub(sub, self.text)


def replace_build(path: Path, old: str, new: str, pattern: str) -> tuple[str, str]:
    text = read(path)
    updated, count = re.subn(pattern.format(build=re.escape(old)), lambda m: m.group(0).replace(old, new), text)
    if count == 0:
        sys.exit(f'{path}: build marker {old} not found')
    return text, updated


# ---------------------------------------------------------------- repository targets

def plusemu(root: Path):
    headers = [
        ConstantFile(root / 'Communication/Packets/Incoming/ClientPacketHeader.cs',
                     r'public const uint (?P<name>\w+) = (?P<id>\d+);', 'outgoing'),
        ConstantFile(root / 'Communication/Packets/Outgoing/ServerPacketHeader.cs',
                     r'public const uint (?P<name>\w+) = (?P<id>\d+);', 'incoming'),
    ]
    cache = root / 'Communication/Revisions/RevisionsCache.cs'
    current = re.search(r'Name = "(WIN63-[\w-]+)"', read(cache)).group(1)

    def finish(resolver: Resolver, writes: dict, identity_raw: bytes):
        writes[cache] = replace_build(cache, current, resolver.target.id, r'Name = "{build}"')
        ranges = root / 'Communication/Packets/CustomPacketRanges.cs'
        writes[ranges] = replace_build(ranges, current, resolver.target.id, r'not official in {build}')

    return current, headers, finish


def renderer(root: Path):
    messages = root / 'packages/communication/src/messages'
    headers = [
        ConstantFile(messages / 'incoming/IncomingHeader.ts',
                     r'public static (?P<name>\w+)\s*=\s*(?P<id>\d+);', 'incoming'),
        ConstantFile(messages / 'outgoing/OutgoingHeader.ts',
                     r'public static (?P<name>\w+)\s*=\s*(?P<id>\d+);', 'outgoing'),
    ]
    hello = messages / 'outgoing/handshake/ClientHelloMessageComposer.ts'
    current = re.search(r"BUILD = '(WIN63-[\w-]+)'", read(hello)).group(1)
    manifest_path = root / 'protocol/packet-field-contracts.json'

    def finish(resolver: Resolver, writes: dict, identity_raw: bytes):
        writes[hello] = replace_build(hello, current, resolver.target.id, r"BUILD = '{build}'")
        before = read(manifest_path)
        manifest = json.loads(before)
        side = {'server_to_client': 'incoming', 'client_to_server': 'outgoing'}

        def move(entry: dict, context: str):
            # Java-side entries are the companion emulator's ids; their custom ids are not ours to police.
            new = resolver.resolve(side[entry['direction']], entry['header'], context, entry.get('side') == 'java')
            if new is not None:
                entry['header'] = new

        errors = []
        for group in ('contracts', 'exemptions', 'unpaired'):
            for entry in manifest[group]:
                try:
                    move(entry, f'{group}:{entry.get("name") or entry.get("symbol")}')
                except LookupError as error:
                    errors.append(str(error))
        for entry in manifest['registry'].get('aliases', []):
            try:
                move(entry, f'aliases:{entry["canonical"]}')
            except LookupError as error:
                errors.append(str(error))
        if errors:
            sys.exit('packet-field-contracts.json:\n  ' + '\n  '.join(errors))
        manifest['nativeAir'] = native_air(manifest['nativeAir'], resolver, identity_raw)
        writes[manifest_path] = (before, format_manifest(before, manifest))

    return current, headers, finish


def format_manifest(before: str, manifest: dict) -> str:
    """The manifest with its new header values written over the old text, so the hand-kept layout stays."""
    cut = before.index('\n  "nativeAir": {')
    values = []

    def walk(node):
        if isinstance(node, dict):
            for key, value in node.items():
                if key == 'header':
                    values.append(value)
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk({key: value for key, value in manifest.items() if key != 'nativeAir'})
    found = list(re.finditer(r'"header"(\s*):(\s*)(-?\d+)', before[:cut]))
    if len(found) != len(values):
        sys.exit(f'packet-field-contracts.json: {len(found)} header fields in the text, {len(values)} parsed')
    head, last = [], 0
    for match, value in zip(found, values):
        head.append(before[last:match.start(3)] + str(value))
        last = match.end(3)
    head.append(before[last:cut])
    native = manifest['nativeAir']
    meta = json.dumps({key: value for key, value in native.items() if key != 'packets'}, indent=2,
                      ensure_ascii=False).replace('\n', '\n  ')[:-1].rstrip()
    packets = ',\n'.join('      ' + json.dumps(packet, ensure_ascii=False, separators=(',', ':'))
                         for packet in native['packets'])
    return ''.join(head) + '\n  "nativeAir": ' + meta + '\n  ,\n    "packets": [\n' + packets + '\n    ]\n  }\n}\n'


def native_air(old: dict, resolver: Resolver, identity_raw: bytes) -> dict:
    """The AIR message identities of the target build, carrying names and bindings by identity."""
    side = {'incoming': 'server_to_client', 'outgoing': 'client_to_server'}
    carried = {}
    for packet in old['packets']:
        direction = 'incoming' if packet['direction'] == 'server_to_client' else 'outgoing'
        pair = resolver.pair(direction, packet['header'])
        if pair is not None:
            carried[(direction, pair['toClass'])] = packet
    bundle = Path(resolver.target.manifest['bundle_path'])
    packets = []
    for (direction, header), row in sorted(resolver.target.by_header.items(),
                                           key=lambda item: (item[0][0] != 'incoming', item[0][1])):
        path = row['class_paths'][0]
        source = bundle / path
        if not source.is_file():
            sys.exit(f'{source}: missing; nativeAir needs the decompiled bundle of {resolver.target.id}')
        previous = carried.get((direction, row['class_name']), {})
        packets.append({
            'direction': side[direction], 'header': header,
            'sourceClass': row['class_name'], 'sourcePath': path,
            'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
            'mapLine': row['line'],
            'reportedName': previous.get('reportedName'), 'namingSource': previous.get('namingSource'),
            'canonicalName': previous.get('canonicalName'),
            'canonicalStatus': previous.get('canonicalStatus', 'unknown'),
            'localBindings': previous.get('localBindings', {'csharp': [], 'typescript': []}),
        })
    manifest = resolver.target.manifest
    return {
        'build': resolver.target.id,
        'swfSha256': manifest['assets_swf_sha256'],
        'messageMapSha256': manifest['message_map_sha256'],
        'auditSha256': hashlib.sha256(identity_raw).hexdigest(),
        'namingScope': old['namingScope'],
        'semanticScope': old['semanticScope'],
        'packets': packets,
    }


# ---------------------------------------------------------------- main

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--index', type=Path,
                        default=Path(os.environ.get('HABBO_INDEX', '~/dev/habbo-intelligence/index')).expanduser())
    parser.add_argument('--identity', type=Path,
                        default=Path(os.environ.get('HABBO_IDENTITY', '~/dev/habbo-intelligence/identity')).expanduser())
    parser.add_argument('--map', type=Path)
    parser.add_argument('--to')
    parser.add_argument('--from', dest='current')
    parser.add_argument('--root', type=Path, default=Path.cwd())
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()

    root = args.root.resolve()
    target = args.to or (json.loads(args.map.read_text())['toBuild'] if args.map else newest_build(args.index))
    if (root / 'Communication/Revisions/RevisionsCache.cs').is_file():
        current, headers, finish = plusemu(root)
    elif (root / 'packages/communication/src/messages/incoming/IncomingHeader.ts').is_file():
        current, headers, finish = renderer(root)
    else:
        sys.exit(f'{root}: neither PlusEMU nor Volt-Renderer')
    current = args.current or current

    if current == target:
        print(f'already on {target}')
        return 0
    if args.check:
        print(f'on {current}, not {target}')
        return 1

    identity, identity_raw = load_identity(args, current, target)
    resolver = Resolver(args.index, current, identity, target)
    errors, stats = [], {'changed': 0, 'kept': 0, 'custom': 0}
    for file in headers:
        file.remap(resolver, errors, stats)
    if errors:
        print('cannot map:\n  ' + '\n  '.join(errors), file=sys.stderr)
        return 2
    writes = {file.path: (read(file.path), file.text) for file in headers}
    finish(resolver, writes, identity_raw)
    for path, (before, after) in writes.items():
        if before != after and not args.dry_run:
            write(path, after)
        print(f'{"would write" if args.dry_run else "wrote"} {path.relative_to(root)}'
              if before != after else f'unchanged {path.relative_to(root)}')
    print(f'{current} -> {target}: {stats["changed"]} ids changed, {stats["kept"]} kept, '
          f'{stats["custom"]} custom left alone')
    return 0


if __name__ == '__main__':
    sys.exit(main())
