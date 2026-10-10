#!/usr/bin/env bash
# Smoke-tests a built faber-web image: the PDF.js worker must be in the image
# and served as JavaScript, and a missing static file must be a 404 rather
# than the SPA fallback.
#
#   scripts/smoke-test-image.sh <image>
set -euo pipefail

image="${1:?usage: smoke-test-image.sh <image>}"
port="${SMOKE_TEST_PORT:-18080}"
base="http://127.0.0.1:${port}"

container="$(docker run --detach --rm --publish "127.0.0.1:${port}:80" "$image")"
trap 'docker stop "$container" >/dev/null' EXIT

for _ in $(seq 1 30); do
  curl --silent --fail --output /dev/null "${base}/healthz" && break
  sleep 1
done

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

# Prints "<status> <content-type>" for a path.
probe() {
  curl --silent --output /dev/null --write-out '%{http_code} %{content_type}' "${base}$1"
}

worker="$(docker exec "$container" sh -c 'cd /usr/share/nginx/html && ls media/pdf.worker.min-*.mjs' 2>/dev/null || true)"
[ -n "$worker" ] || fail "no PDF.js worker under media/ in the image"
[ "$(echo "$worker" | wc -l)" -eq 1 ] || fail "expected exactly one PDF.js worker, got: $worker"

docker exec "$container" sh -c "grep -qF '$worker' /usr/share/nginx/html/*.js" \
  || fail "no chunk references ${worker}"

result="$(probe "/${worker}")"
[[ "$result" == "200 "*javascript* ]] || fail "/${worker} -> ${result}, want 200 javascript"
echo "ok: /${worker} -> ${result}"

for missing in /pdfjs-dist/build/pdf.worker.mjs /missing.js /missing.css /missing.js.map; do
  result="$(probe "$missing")"
  [[ "$result" == "404 "* ]] || fail "${missing} -> ${result}, want 404"
  echo "ok: ${missing} -> ${result}"
done

result="$(probe /resumes/some-deep-link)"
[[ "$result" == "200 text/html"* ]] || fail "SPA fallback -> ${result}, want 200 text/html"
echo "ok: SPA fallback -> ${result}"
