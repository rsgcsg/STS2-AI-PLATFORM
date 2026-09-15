import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

from sts2_platform_evidence.collection_tool import CollectionTool, CollectionFailure, canonical, digest
from sts2_platform_evidence.transfer import _inventory


class CollectionSetupTests(unittest.TestCase):
    def fixture(self, root):
        entry = "setup/apps/game-mod/collection-setup.mjs"
        for name, content in {entry: "fixture", "sts2-human-annotator.dll": "fixture", "platform-bom.json": "{}",
                              "game-mod/build-provenance.json": "{}"}.items():
            destination = root / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_text(content)
        identity = {"worktree": "clean", "source_revision": "c" * 40,
                    "entrypoint": "sts2-human-annotator.dll", "collection_setup_entrypoint": entry,
                    "supported_recording_schema": "sts2.human-annotator/recording-manifest-2",
                    "files": [{"path": p, "bytes": n, "sha256": h} for p, n, h in _inventory(root)]}
        release_id = digest(identity)
        (root / "collection-tool.json").write_bytes(canonical({"schema": "sts2.evidence/collection-tool-1",
                                                               "release_id": release_id, "identity": identity}))
        return CollectionTool(root, release_id)

    def test_fixed_owner_commands_pin_provenance_and_keep_status_distinct_from_binding(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tool = self.fixture(root)
            response = {"schema": "sts2.platform/collection-setup-1", "status": "configured", "bound": False}
            with patch("sts2_platform_evidence.collection_tool.subprocess.run",
                       return_value=SimpleNamespace(returncode=0, stdout=json.dumps(response))) as run:
                self.assertEqual(tool.setup_status(recordings_root=root / "records", game_directory=root / "game"), response)
                self.assertIn("status", run.call_args.args[0])
                self.assertIn(str(root / "game-mod/build-provenance.json"), run.call_args.args[0])
                self.assertEqual(tool.bind_recording_root(recordings_root=root / "records"), response)
                self.assertIn("bind", run.call_args.args[0])
                with self.assertRaises(CollectionFailure):
                    tool.setup_status(recordings_root=root / "records", mod_provenance=root.parent / "untrusted.json")
                self.assertEqual(run.call_count, 2)

    def test_setup_reverifies_all_owner_bytes_before_each_execution(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tool = self.fixture(root)
            (root / "game-mod/build-provenance.json").write_text("changed")
            with patch("sts2_platform_evidence.collection_tool.subprocess.run") as run:
                with self.assertRaisesRegex(ValueError, "bytes differ"):
                    tool.setup_status(recordings_root=root / "records")
                run.assert_not_called()
