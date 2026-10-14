#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cd "$repo_root"

for required_command in diff jq; do
  command -v "$required_command" >/dev/null 2>&1 || {
    echo "error: required command is unavailable: $required_command" >&2
    exit 1
  }
done

reference_export="infra/keycloak/realm-export.json"
test_exports=(
  "tests/Integration/Faber.Api.Tests/realm-export.json"
  "tests/Integration/Modules/Auth/Faber.Modules.Auth.Application.Tests/realm-export.json"
  "tests/Integration/Modules/Resumes/Faber.Modules.Resumes.Application.Tests/realm-export.json"
)

# Brokered sign-in must be configured identically in every realm; ids, secrets and
# redirect URIs legitimately differ between the development and test realms.
brokering_filter='{
  identityProviders,
  identityProviderMappers,
  brokerFlows: [.authenticationFlows[] | select(.alias == "silent first broker login") | del(.id)],
  verifyEmail,
  idpEmailVerifiedProfile: [.components["org.keycloak.userprofile.UserProfileProvider"][]?.config["kc.user.profile.config"][0] | fromjson | .attributes[] | select(.name == "idp_email_verified")],
  verifyProfile: [.requiredActions[] | select(.alias == "VERIFY_PROFILE") | .enabled],
  apiClientFlows: [.clients[] | select(.clientId == "faber-api") | {standardFlowEnabled, directAccessGrantsEnabled}]
}'

has_errors=false

for export in "$reference_export" "${test_exports[@]}"; do
  [[ -f "$export" ]] || {
    echo "error: realm export is missing: $export" >&2
    exit 1
  }

  if ! jq -e '[.identityProviders[].config | .clientId, .clientSecret] | all(test("^<[A-Z-]+>$"))' "$export" >/dev/null; then
    echo "error: $export has an identity provider credential that is not a <PLACEHOLDER>" >&2
    has_errors=true
  fi
done

for export in "${test_exports[@]}"; do
  if ! diff <(jq -S "$brokering_filter" "$reference_export") <(jq -S "$brokering_filter" "$export") >&2; then
    echo "error: brokered sign-in configuration in $export differs from $reference_export" >&2
    has_errors=true
  fi
done

if [[ "$has_errors" == true ]]; then
  exit 1
fi

echo "Realm exports are in sync."
