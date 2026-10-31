# Keycloak with the faber login theme baked in. Build context: infra/keycloak.
#
# This tag must match keycloakTag in src/api/Aspire/Faber.AppHost/Program.cs and
# FaberKeycloak.Image in the auth test fixture, so dev, tests and production run
# the same Keycloak.
ARG KEYCLOAK_VERSION=26.8.1

FROM quay.io/keycloak/keycloak:${KEYCLOAK_VERSION} AS builder

# Build-time options only: they are fixed into the optimized image by `kc.sh build`,
# so run it with `start --optimized`. Runtime options (KC_HOSTNAME, KC_DB_URL,
# credentials, KC_HTTP_ENABLED, KC_PROXY_HEADERS) are set when the container starts.
ENV KC_DB=postgres
ENV KC_HEALTH_ENABLED=true

COPY themes/faber /opt/keycloak/themes/faber

RUN /opt/keycloak/bin/kc.sh build

FROM quay.io/keycloak/keycloak:${KEYCLOAK_VERSION}

COPY --from=builder /opt/keycloak /opt/keycloak

# The theme cache stays on, the default in production mode (`start`); only
# `start-dev` turns it off. Theme edits need a new image.
ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
