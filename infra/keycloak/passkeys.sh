#!/usr/bin/env bash
# Turns passkeys on in the Keycloak realm, after keycloak-config-cli has imported faber.json.
#
# Why it exists: keycloak-config-cli's realm model (Keycloak 26.1) predates the passkey fields, so it drops
# them on every import and they cannot live in faber.json. This step therefore runs after every
# keycloak-config run, in the stack, in production and in the tests. kcadm's update sends only the
# fields named here; the rest of the realm is left as imported.
#
# Delete this script (and its AppHost resource) once keycloak-config-cli understands these fields, and
# put them in faber.json instead.
#
# Environment: KEYCLOAK_URL, KEYCLOAK_USER, KEYCLOAK_PASSWORD, and optionally KEYCLOAK_REALM (default faber).
# Credentials come from the environment, never from arguments, so they stay out of process listings.
set -euo pipefail

: "${KEYCLOAK_URL:?KEYCLOAK_URL is required}"
: "${KEYCLOAK_USER:?KEYCLOAK_USER is required}"
: "${KEYCLOAK_PASSWORD:?KEYCLOAK_PASSWORD is required}"
realm="${KEYCLOAK_REALM:-faber}"

# The image runs as a non-root user, so keep kcadm's session file somewhere writable.
kcadm=(/opt/keycloak/bin/kcadm.sh)
config=/tmp/kcadm.config

"${kcadm[@]}" config credentials --config "$config" \
  --server "$KEYCLOAK_URL" --realm master \
  --user "$KEYCLOAK_USER" --password "$KEYCLOAK_PASSWORD"

"${kcadm[@]}" update "realms/$realm" --config "$config" \
  -s webAuthnPolicyPasswordlessPasskeysEnabled=true \
  -s webAuthnPolicyPasswordlessMediation=conditional
