# Keycloak with Faber's own flow steps (see providers/).
# The Keycloak version is set only here: the steps are compiled against this image's own jars.
FROM quay.io/keycloak/keycloak:26.2.5 AS keycloak

# The Keycloak image ships a JRE only, so the steps are compiled in a JDK image.
FROM eclipse-temurin:21-jdk AS providers
COPY --from=keycloak /opt/keycloak/lib/lib/main /keycloak-lib
COPY providers /providers
RUN javac --release 21 -cp "/keycloak-lib/*" -d /classes $(find /providers/src -name '*.java') \
 && cp -r /providers/META-INF /classes/ \
 && jar --create --file /faber-keycloak-providers.jar -C /classes .

FROM keycloak
COPY --from=providers /faber-keycloak-providers.jar /opt/keycloak/providers/
