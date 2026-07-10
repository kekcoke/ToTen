FROM alpine:3.20 AS realm-prep
ARG KEYCLOAK_WEB_BFF_CLIENT_SECRET
ARG KEYCLOAK_WEB_BFF_REDIRECT_URI
RUN apk add --no-cache jq
COPY src/ToTen.AppHost/realms/ToTen-realm.json /tmp/realm.json
# The realm export ships a seeded demo/demo user for local dev convenience -
# strip it here so the credential never lands in the production image.
# The ToTen-web-bff client also ships a fixed dev-only secret and a localhost
# redirect URI (matching AppHost.cs's local Aspire parameter and the dev realm) -
# replace both with the real deployed values at image build time. Redirect URI is
# blank until the API's real FQDN is known post-first-deploy (see
# terraform/variables.tf's keycloak_web_bff_redirect_uri) - fall back to the dev
# value so an early/bootstrap image build still produces valid JSON.
RUN jq \
    --arg secret "${KEYCLOAK_WEB_BFF_CLIENT_SECRET:-dev-web-bff-secret-change-me-9f8e7d6c5b4a}" \
    --arg redirect "${KEYCLOAK_WEB_BFF_REDIRECT_URI:-http://localhost:5082/auth/callback}" \
    '(.clients[] | select(.clientId == "ToTen-web-bff") | .secret) = $secret | (.clients[] | select(.clientId == "ToTen-web-bff") | .redirectUris) = [$redirect] | (.clients[] | select(.clientId == "ToTen-web-bff") | .webOrigins) = [($redirect | sub("/auth/callback$"; ""))] | del(.users[] | select(.username == "demo"))' \
    /tmp/realm.json > /tmp/realm.prod.json

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
