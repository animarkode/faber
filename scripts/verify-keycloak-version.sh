#!/usr/bin/env bash

# Fails when the Keycloak version differs between development (AppHost), the auth tests
# (Testcontainers fixture), the production image (infra/keycloak/Dockerfile) and its
# operations doc. The faber login theme forks two keycloak.v2 templates, so production must
# run the exact version the tests ran. Nothing bumps these automatically: dependabot does not
# watch Docker images here, so a forgotten copy would otherwise drift silently.

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cd "$repo_root"

apphost="src/api/Aspire/Faber.AppHost/Program.cs"
fixture="tests/Integration/Modules/Common/Faber.Testing.Shared/Containers/FaberKeycloak.cs"
dockerfile="infra/keycloak/Dockerfile"
operations_doc="docs/operations/keycloak.md"

# Prints the first capture of a sed -E pattern in a file, or nothing.
extract() {
  sed -nE "s/$2/\\1/p" "$1" | head -n 1
}

declare -a sources=(
  "$apphost|$(extract "$apphost" '.*const string keycloakTag = "([^"]+)";.*')"
  "$fixture|$(extract "$fixture" '.*"quay\.io\/keycloak\/keycloak:([^"]+)".*')"
  "$dockerfile|$(extract "$dockerfile" '^ARG KEYCLOAK_VERSION=([^[:space:]]+).*')"
  "$operations_doc|$(extract "$operations_doc" '.*`quay\.io\/keycloak\/keycloak:([^`]+)`.*')"
)

has_errors=false
expected=""

for source in "${sources[@]}"; do
  path="${source%%|*}"
  version="${source#*|}"

  if [[ -z "$version" ]]; then
    echo "error: no Keycloak version found in $path" >&2
    has_errors=true
    continue
  fi

  if [[ -z "$expected" ]]; then
    expected="$version"
  elif [[ "$version" != "$expected" ]]; then
    echo "error: $path pins Keycloak $version, but $apphost pins $expected" >&2
    has_errors=true
  fi
done

if [[ "$has_errors" == true ]]; then
  exit 1
fi

echo "Keycloak version verified: $expected in the AppHost, the test fixture, the Dockerfile and the operations doc."
