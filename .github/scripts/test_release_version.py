"""Exercise version selection with GitHub release records and a local git history."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest


class ReleaseVersionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.repo = Path(self.tmp.name)
        self.script = Path(__file__).with_name("release-version.py").resolve()
        self.git("init", "--quiet")
        self.git("config", "user.name", "Release Test")
        self.git("config", "user.email", "release-test@example.invalid")
        self.git("commit", "--quiet", "--allow-empty", "-m", "Initial release")
        self.previous = self.git("rev-parse", "HEAD")
        self.git("commit", "--quiet", "--allow-empty", "-m", "Next release")
        self.current = self.git("rev-parse", "HEAD")

    def git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.repo, text=True).strip()

    def release(self, tag, *, commit=None, draft=False, prerelease=False):
        return {"tag_name": tag, "target_commitish": commit or self.previous,
                "draft": draft, "prerelease": prerelease}

    def select(self, pages, bump="patch", commit=None):
        return subprocess.run(
            ["python3", str(self.script), bump, commit or self.current],
            input=json.dumps(pages), cwd=self.repo, text=True, capture_output=True)

    def assert_version(self, pages, expected, **kwargs):
        result = self.select(pages, **kwargs)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.strip(), expected)

    def test_first_release_starts_at_one(self):
        for bump in ("patch", "minor", "major"):
            with self.subTest(bump=bump):
                self.assert_version([[]], "v1.0.0", bump=bump)

    def test_date_tags_and_prereleases_do_not_set_stable_version(self):
        releases = [self.release("v2026.10.19-975399e"),
                    self.release("v9.0.0-rc.1", prerelease=True),
                    self.release("v8.0.0", prerelease=True)]
        self.assert_version([releases], "v1.0.0")

    def test_patch_minor_and_major_bumps(self):
        for bump, expected in (("patch", "v1.2.4"), ("minor", "v1.3.0"),
                               ("major", "v2.0.0")):
            with self.subTest(bump=bump):
                self.assert_version([[self.release("v1.2.3")]], expected, bump=bump)

    def test_highest_numeric_version_across_all_pages_wins(self):
        self.assert_version([[self.release("v1.9.9"), self.release("v2026.10.19-975399e")],
                             [self.release("v1.10.2"), self.release("v1.2.3")]], "v1.10.3")

    def test_retry_reuses_published_version_without_bumping(self):
        self.assert_version([[self.release("v1.2.3", commit=self.current)]],
                            "v1.2.3", bump="major")

    def test_retry_reuses_draft_version(self):
        self.assert_version([[self.release("v1.3.0", commit=self.current, draft=True),
                              self.release("v1.2.3")]], "v1.3.0")

    def test_retry_of_older_published_release_keeps_its_version(self):
        self.assert_version([[self.release("v1.3.0", commit=self.current),
                              self.release("v1.2.3")]], "v1.2.3", commit=self.previous)

    def test_unfinished_release_for_another_commit_blocks_new_release(self):
        result = self.select([[self.release("v1.3.0", draft=True)]])
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("draft", result.stderr.lower())
        self.assertIn("v1.3.0", result.stderr)
        self.assertEqual(result.stdout, "")

    def test_older_unpublished_source_cannot_replace_newer_release(self):
        result = self.select([[self.release("v1.2.3", commit=self.current)]],
                             commit=self.previous)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("v1.2.3", result.stderr)
        self.assertEqual(result.stdout, "")

    def test_noncanonical_tags_are_ignored(self):
        self.assert_version([[self.release("v01.2.3"), self.release("v1.02.3"),
                              self.release("v1.2.03"), self.release("1.2.3")]], "v1.0.0")

    def test_invalid_bump_is_rejected(self):
        result = self.select([[]], bump="$(touch invalid)")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")
        self.assertFalse((self.repo / "invalid").exists())

    def test_malformed_release_data_does_not_reset_version(self):
        result = self.select({"message": "API rate limit exceeded"})
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")


if __name__ == "__main__":
    unittest.main()
