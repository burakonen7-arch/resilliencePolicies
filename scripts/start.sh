#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
# Local development database only; no external credentials are required.
docker compose up -d --wait postgres
dotnet src/Product.Api/bin/Debug/net10.0/Product.Api.dll --initialize-db
exec dotnet src/Product.Api/bin/Debug/net10.0/Product.Api.dll
