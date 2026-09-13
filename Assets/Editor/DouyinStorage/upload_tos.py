"""Editor-only publisher. Credentials come from environment, never command arguments."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import urllib.request

BUCKET = "tt572c2191c956e13607-env-sqc4xosogq"
ENDPOINT = "tos-cn-beijing.volces.com"
PREFIX = "addressables/WebGL/"
PUBLIC_ROOT = f"https://{BUCKET}.{ENDPOINT}/"


def digest(path):
    h = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def plan(manifest_path):
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8-sig"))
    root = Path(manifest["root"]).resolve()
    entries = []
    seen = set()
    for item in manifest["files"]:
        path = Path(item["path"]).resolve()
        if path.parent != root or not path.is_file():
            raise ValueError("Only files directly inside the build output are supported")
        if path.name in seen or not all(c.isascii() and (c.isalnum() or c in "_.-") for c in path.name):
            raise ValueError("Duplicate or unsupported object name")
        seen.add(path.name)
        if path.suffix not in (".bundle", ".json", ".hash", ".bin"):
            raise ValueError("Unexpected build artifact")
        if path.suffix != ".bundle" and not path.name.startswith("catalog_"):
            raise ValueError("Only remote bundles and catalogs may be published")
        if digest(path) != item["sha256"]:
            raise ValueError("Build output changed; rebuild before publishing")
        entries.append((path, item["sha256"]))
    catalogs = [p for p, _ in entries if p.suffix in (".json", ".bin")]
    hashes = [p for p, _ in entries if p.suffix == ".hash"]
    if len(catalogs) != 1 or len(hashes) != 1 or catalogs[0].stem != hashes[0].stem:
        raise ValueError("Exactly one matching remote catalog/hash pair is required")
    if not any(p.suffix == ".bundle" for p, _ in entries):
        raise ValueError("No remote bundles in this build")
    return sorted(entries, key=lambda e: (0 if e[0].suffix == ".bundle" else 2 if e[0].suffix == ".hash" else 1, e[0].name))


def public_verify(path, sha):
    # Anonymous access verifies that the existing public-directory policy is effective.
    url = PUBLIC_ROOT + PREFIX + path.name
    with urllib.request.urlopen(urllib.request.Request(url, method="HEAD"), timeout=30) as r:
        if r.status != 200 or int(r.headers.get("Content-Length", -1)) != path.stat().st_size:
            raise RuntimeError("Public download size verification failed")
    if path.suffix != ".bundle":
        with urllib.request.urlopen(url, timeout=30) as r:
            if hashlib.sha256(r.read()).hexdigest() != sha:
                raise RuntimeError("Public catalog content verification failed")


def publish(client, entries, missing_error, verify=public_verify):
    for path, sha in entries:
        key = PREFIX + path.name
        # Check again immediately before each upload; do not publish a changed build.
        if digest(path) != sha:
            raise RuntimeError("Build file changed during upload")
        remote = None
        try:
            remote = client.head_object(BUCKET, key)
        except missing_error as e:
            if e.status_code != 404:
                raise
        same = remote is not None and remote.content_length == path.stat().st_size and remote.meta.get("sha256") == sha
        if not same:
            # Bundle names must be immutable so older installed players keep working.
            if remote is not None and path.suffix == ".bundle":
                with open(path, "rb") as stream:
                    md5 = hashlib.md5(stream.read()).hexdigest()
                if remote.etag.strip('"') == md5 and remote.content_length == path.stat().st_size:
                    same = True
                else:
                    raise RuntimeError("Existing bundle differs; use Append Hash bundle naming: " + path.name)
            if not same:
                client.put_object_from_file(
                    BUCKET, key, str(path), meta={"sha256": sha},
                    content_type="application/json" if path.suffix == ".json" else "application/octet-stream",
                    cache_control="public,max-age=31536000,immutable" if path.suffix == ".bundle" else "no-cache",
                    forbid_overwrite=True if path.suffix == ".bundle" else False)
                remote = client.head_object(BUCKET, key)
                if remote.content_length != path.stat().st_size or remote.meta.get("sha256") != sha:
                    raise RuntimeError("Uploaded object verification failed")
        verify(path, sha)
        print(("SKIP " if same else "OK ") + key, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if args.dry_run:
        for p, _ in plan(args.manifest):
            print(PREFIX + p.name)
        return
    import tos
    ak = os.environ.get("DOUYIN_TOS_AK", "")
    sk = os.environ.get("DOUYIN_TOS_SK", "")
    if not ak or not sk:
        raise ValueError("Configure DOUYIN_TOS_AK and DOUYIN_TOS_SK in the Unity upload settings")
    if args.check:
        print("Python SDK and credentials configured (network permission not yet verified)")
        return
    entries = plan(args.manifest)
    client = tos.TosClientV2(ak, sk, ENDPOINT, "cn-beijing", connection_time=10,
                             socket_timeout=60, max_retry_count=2)
    try:
        publish(client, entries, tos.exceptions.TosServerError)
    finally:
        client.close()
    print("UPLOAD COMPLETE", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        # SDK exception strings can include request signatures; do not print them.
        if isinstance(e, (ValueError, RuntimeError, ModuleNotFoundError)):
            print("ERROR: " + str(e), file=sys.stderr)
        else:
            print("ERROR: " + type(e).__name__ + " status=" + str(getattr(e, "status_code", "unknown")), file=sys.stderr)
        sys.exit(1)
