"""Exercise release publication without contacting GitHub."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


MOCK_GH = r'''#!/usr/bin/env python3
import json
import os
from pathlib import Path
import sys

state_path = Path(os.environ["RELEASE_TEST_STATE"])
state = json.loads(state_path.read_text())
command = sys.argv[2]
state["calls"].append(sys.argv[1:])
if command == os.environ.get("RELEASE_TEST_FAIL"):
    state_path.write_text(json.dumps(state))
    sys.exit(1)
if command == "view":
    state_path.write_text(json.dumps(state))
    if state["release"] == "absent":
        sys.exit(1)
    print("true" if state["release"] == "draft" else "false")
elif command == "create":
    assert "--draft" in sys.argv
    state["release"] = "draft"
elif command == "upload":
    assert state["release"] == "draft"
    assert "--clobber" in sys.argv
elif command == "edit":
    assert state["release"] == "draft"
    assert "--draft=false" in sys.argv
    state["release"] = "published"
else:
    raise AssertionError(sys.argv)
state_path.write_text(json.dumps(state))
'''


class PublishReleaseTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        gh = root / "gh"
        gh.write_text(MOCK_GH)
        gh.chmod(0o755)
        self.state = root / "state.json"
        self.env = dict(os.environ, PATH=f"{root}:{os.environ['PATH']}",
                        RELEASE_TEST_STATE=str(self.state))
        self.script = Path(__file__).with_name("publish-release.sh")
        self.seed("absent")

    def seed(self, release):
        self.state.write_text(json.dumps({"release": release, "calls": []}))

    def run_release(self, fail=""):
        result = subprocess.run(
            ["bash", str(self.script), "v1-abcdef0", "Release title", "abcdef012345",
             "asset with spaces.zip", "SHA256SUMS"],
            env=dict(self.env, RELEASE_TEST_FAIL=fail), capture_output=True, text=True)
        self.assertEqual(result.returncode, 1 if fail else 0, result.stderr)
        return json.loads(self.state.read_text())

    def commands(self, state):
        return [call[1] for call in state["calls"]]

    def test_new_release_uploads_before_publication(self):
        state = self.run_release()
        self.assertEqual(self.commands(state), ["view", "create", "upload", "edit"])
        create = state["calls"][1]
        self.assertEqual(create[create.index("--target") + 1], "abcdef012345")
        self.assertIn("asset with spaces.zip", state["calls"][2])
        self.assertEqual(state["release"], "published")

    def test_published_release_is_unchanged(self):
        self.seed("published")
        self.assertEqual(self.commands(self.run_release()), ["view"])

    def test_existing_draft_is_resumed(self):
        self.seed("draft")
        self.assertEqual(self.commands(self.run_release()), ["view", "upload", "edit"])

    def test_upload_failure_stays_private_and_rerun_recovers(self):
        state = self.run_release("upload")
        self.assertEqual(state["release"], "draft")
        self.assertNotIn("edit", self.commands(state))
        state = self.run_release()
        self.assertEqual(state["release"], "published")
        self.assertEqual(self.commands(state).count("create"), 1)

    def test_creation_failure_never_uploads_or_publishes(self):
        state = self.run_release("create")
        self.assertEqual(self.commands(state), ["view", "create"])
        self.assertEqual(state["release"], "absent")

    def test_publication_failure_can_be_retried(self):
        self.assertEqual(self.run_release("edit")["release"], "draft")
        self.assertEqual(self.run_release()["release"], "published")


if __name__ == "__main__":
    unittest.main()
