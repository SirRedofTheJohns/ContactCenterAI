#!/bin/sh
# One-time, idempotent demo configuration: random per-installation service secrets in the demo-config volume.
# Later runs keep the existing file, so accounts, sessions and data survive restarts.
# The synthetic user password (123456Aa!) is a public fixture and is not stored here.
set -eu
file=/config/demo.env
if [ -s "$file" ]; then echo "Demo configuration exists; secrets kept."; exit 0; fi
secret() { head -c 32 /dev/urandom | base64 | tr -d '=+/\n'; }
tmp=$(mktemp /config/.demo.env.XXXXXX)
{
  echo "CCAI_BFF_CLIENT_SECRET=Ccai$(secret)"
  echo "CCAI_KEYCLOAK_ADMIN_PASSWORD=Ccai$(secret)"
  echo "CCAI_SOURCE_SERVICE_KEY=$(secret)$(secret)"
} > "$tmp"
# Readable by the Keycloak container (different uid); the volume is mounted only into this demo's containers.
chmod 0644 "$tmp"
mv "$tmp" "$file"
echo "Demo configuration created with random service secrets."
