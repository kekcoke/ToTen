FROM alpine:3.20 AS realm-prep
RUN apk add --no-cache jq
COPY src/ToTen.AppHost/realms/ToTen-realm.json /tmp/realm.json
# The realm export ships a seeded demo/demo user for local dev convenience -
# strip it here so the credential never lands in the production image.
RUN jq 'del(.users[] | select(.username == "demo"))' /tmp/realm.json > /tmp/realm.prod.json

FROM quay.io/keycloak/keycloak:latest

# Bake the ToTen realm so Keycloak imports it on first start without any runtime
# volume mounts or Admin API calls. The --import-realm flag in CMD triggers the import.
COPY --from=realm-prep /tmp/realm.prod.json /opt/keycloak/data/import/ToTen-realm.json

# Pre-build Keycloak's optimized server image to reduce startup time in ACA.
RUN /opt/keycloak/bin/kc.sh build \
    --db=postgres \
    --http-enabled=true

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--import-realm", "--optimized"]
