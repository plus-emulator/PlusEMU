"""Wrap code names such as @state in backticks, so GitHub does not mention a user with that name."""
import re
import sys

MENTION = re.compile(r"(?<![\w`/])(?<!by )@([A-Za-z_][\w.]*)")
NEW_CONTRIBUTOR = re.compile(r"\* @\S+ made their first contribution")


def escape_line(line):
    if NEW_CONTRIBUTOR.match(line):
        return line
    parts = line.split("`")
    parts[::2] = [MENTION.sub(r"`@\1`", part) for part in parts[::2]]
    return "`".join(parts)


sys.stdout.write("\n".join(escape_line(line) for line in sys.stdin.read().split("\n")))
