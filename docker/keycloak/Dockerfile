FROM quay.io/keycloak/keycloak:latest

# Bake the ToTen realm so Keycloak imports it on first start without any runtime
# volume mounts or Admin API calls. The --import-realm flag in CMD triggers the import.
COPY src/ToTen.AppHost/realms/ToTen-realm.json /opt/keycloak/data/import/ToTen-realm.json

# Pre-build Keycloak's optimized server image to reduce startup time in ACA.
RUN /opt/keycloak/bin/kc.sh build \
    --db=postgres \
    --http-enabled=true

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--import-realm", "--optimized"]
