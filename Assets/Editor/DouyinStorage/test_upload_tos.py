import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
import upload_tos as uploader


class Missing(Exception):
    status_code = 404


class FakeClient:
    def __init__(self, fail=None):
        self.objects, self.writes, self.fail = {}, [], fail

    def head_object(self, bucket, key):
        if key not in self.objects:
            raise Missing()
        return self.objects[key]

    def put_object_from_file(self, bucket, key, file_path, **kwargs):
        if self.fail and key.endswith(self.fail):
            raise RuntimeError("simulated upload failure")
        self.writes.append(key)
        self.objects[key] = SimpleNamespace(content_length=Path(file_path).stat().st_size,
                                            meta=kwargs["meta"], etag="different")


class UploadTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        files = []
        for name in ("catalog_1.hash", "one_abcd.bundle", "catalog_1.json"):
            path = self.root / name
            path.write_text(name)
            files.append({"path": str(path), "sha256": uploader.digest(path)})
        self.manifest = self.root / "manifest.json"
        self.manifest.write_text(json.dumps({"root": str(self.root), "files": files}))

    def test_order_and_idempotence(self):
        client = FakeClient()
        entries = uploader.plan(self.manifest)
        uploader.publish(client, entries, Missing, lambda *_: None)
        self.assertEqual([Path(k).suffix for k in client.writes], [".bundle", ".json", ".hash"])
        uploader.publish(client, entries, Missing, lambda *_: None)
        self.assertEqual(len(client.writes), 3)

    def test_bundle_failure_does_not_publish_catalog(self):
        client = FakeClient(fail=".bundle")
        with self.assertRaises(RuntimeError):
            uploader.publish(client, uploader.plan(self.manifest), Missing, lambda *_: None)
        self.assertEqual(client.writes, [])

    def test_public_verification_failure_stops_catalog(self):
        client = FakeClient()
        def fail(*_):
            raise RuntimeError("403")
        with self.assertRaises(RuntimeError):
            uploader.publish(client, uploader.plan(self.manifest), Missing, fail)
        self.assertEqual(len(client.writes), 1)
        self.assertTrue(client.writes[0].endswith(".bundle"))

    def test_catalog_failure_never_publishes_hash(self):
        client = FakeClient(fail=".json")
        with self.assertRaises(RuntimeError):
            uploader.publish(client, uploader.plan(self.manifest), Missing, lambda *_: None)
        self.assertEqual(len(client.writes), 1)

    def test_changed_file_rejected(self):
        (self.root / "one_abcd.bundle").write_text("changed")
        with self.assertRaises(ValueError):
            uploader.plan(self.manifest)

    def test_path_escape_rejected(self):
        data = json.loads(self.manifest.read_text())
        data["files"][0]["path"] = str(self.root.parent / "outside.hash")
        self.manifest.write_text(json.dumps(data))
        with self.assertRaises(ValueError):
            uploader.plan(self.manifest)

    def test_existing_different_bundle_not_overwritten(self):
        client = FakeClient()
        key = uploader.PREFIX + "one_abcd.bundle"
        client.objects[key] = SimpleNamespace(content_length=1, meta={}, etag="different")
        with self.assertRaises(RuntimeError):
            uploader.publish(client, uploader.plan(self.manifest), Missing, lambda *_: None)
        self.assertEqual(client.writes, [])

    def test_unregistered_old_file_is_not_uploaded(self):
        (self.root / "old.bundle").write_text("stale")
        self.assertNotIn("old.bundle", [p.name for p, _ in uploader.plan(self.manifest)])


if __name__ == "__main__":
    unittest.main()
