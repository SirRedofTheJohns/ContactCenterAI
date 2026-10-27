#!/bin/sh
# Loads the generated service secrets and starts the resort demo in container mode.
set -eu
set -a
. /config/demo.env
set +a
exec dotnet /app/ContactCenterAI.Api.dll --demo-local
