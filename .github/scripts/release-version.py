import json
import re
import subprocess
import sys


def release_commit(release):
    tag = release["tag_name"]
    result = subprocess.run(
        ["git", "rev-parse", "--verify", "--quiet", f"refs/tags/{tag}^{{commit}}"],
        text=True, capture_output=True)
    if result.returncode == 0:
        return result.stdout.strip()
    target = release["target_commitish"]
    if not re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", target):
        raise ValueError(f"Cannot resolve {tag}. Fetch its tag before selecting a release.")
    return target


def select_version(pages, bump, commit):
    if bump not in ("patch", "minor", "major"):
        raise ValueError("Choose patch, minor, or major.")
    if not re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", commit):
        raise ValueError("The release commit must be a full commit SHA.")
    subprocess.run(["git", "cat-file", "-e", f"{commit}^{{commit}}"], check=True)
    if not isinstance(pages, list) or any(not isinstance(page, list) for page in pages):
        raise ValueError("Expected an array of GitHub release pages.")

    stable = []
    for page in pages:
        for release in page:
            if (not isinstance(release, dict)
                    or not isinstance(release.get("tag_name"), str)
                    or not isinstance(release.get("target_commitish"), str)
                    or not release["target_commitish"]
                    or type(release.get("draft")) is not bool
                    or type(release.get("prerelease")) is not bool):
                raise ValueError("Invalid GitHub release record.")
            version = re.fullmatch(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)",
                                   release["tag_name"])
            if version and not release["prerelease"]:
                stable.append((tuple(map(int, version.groups())), release))

    stable.sort(key=lambda entry: entry[0], reverse=True)
    for _, release in stable:
        if release_commit(release) == commit:
            return release["tag_name"]
    for _, release in stable:
        if release["draft"]:
            raise ValueError(f"Unfinished draft {release['tag_name']} targets another commit. "
                             "Rerun its original workflow to finish it before releasing new changes.")
    if not stable:
        return "v1.0.0"

    version, latest = stable[0]
    ancestor = subprocess.run(
        ["git", "merge-base", "--is-ancestor", release_commit(latest), commit])
    if ancestor.returncode != 0:
        raise ValueError(f"Source commit must descend from {latest['tag_name']}. "
                         "Run the release workflow on the current master branch.")

    major, minor, patch = version
    if bump == "major":
        return f"v{major + 1}.0.0"
    if bump == "minor":
        return f"v{major}.{minor + 1}.0"
    return f"v{major}.{minor}.{patch + 1}"


if __name__ == "__main__":
    try:
        if len(sys.argv) != 3:
            raise ValueError("Usage: release-version.py BUMP COMMIT")
        print(select_version(json.load(sys.stdin), sys.argv[1], sys.argv[2]))
    except (ValueError, subprocess.CalledProcessError) as error:
        sys.exit(str(error))
