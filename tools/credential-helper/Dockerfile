# syntax=docker/dockerfile:1
# =============================================================================
# Sidecar de credenciales temporales para el bucket de adjuntos (feature 011,
# US5, research R3): el binario OFICIAL de IAM Roles Anywhere,
# `aws_signing_helper`, en modo `serve`. Expone en 127.0.0.1:9911 un endpoint
# que imita al IMDSv2 de EC2; la API lo usa con
# AWS_EC2_METADATA_SERVICE_ENDPOINT=http://127.0.0.1:9911, por la cadena estandar
# del SDK, sin tocar su codigo. Las credenciales duran una hora y el sidecar las
# renueva cinco minutos antes de que venzan.
#
# El certificado y la llave del ambiente se montan SOLO en este contenedor
# (Secret erp-adjuntos-certificado): el proceso de la API nunca los ve.
#
# VERSION y SHA256 son los que publica AWS en
# https://docs.aws.amazon.com/rolesanywhere/latest/userguide/credential-helper.html
# (1.8.5, 2026-08-24). Si la huella no coincide, el build falla: nunca se
# empaqueta un binario sin verificar. Para actualizar, cambiar los dos juntos.
# =============================================================================
FROM debian:bookworm-slim AS descarga
ARG VERSION=1.8.5
ARG SHA256=beec9ed1c492d93db809890f16713e3556353294b823c2184ad4e891f1b2b54d
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL --proto '=https' --tlsv1.2 -o /aws_signing_helper \
        "https://rolesanywhere.amazonaws.com/releases/${VERSION}/X86_64/Linux/Amzn2023/aws_signing_helper" \
 && echo "${SHA256}  /aws_signing_helper" | sha256sum -c - \
 && chmod 0755 /aws_signing_helper \
 # Que corra aqui garantiza que sus bibliotecas (glibc) estan en la imagen final.
 && /aws_signing_helper version

# glibc, certificados raiz y nada mas: sin shell ni gestor de paquetes. Usuario sin privilegios.
FROM gcr.io/distroless/base-debian12:nonroot
COPY --from=descarga /aws_signing_helper /usr/local/bin/aws_signing_helper
USER nonroot
ENTRYPOINT ["/usr/local/bin/aws_signing_helper"]
