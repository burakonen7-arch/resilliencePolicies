#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
source scripts/env.sh
if ! command -v dotnet >/dev/null; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/products-dotnet-install.sh
  bash /tmp/products-dotnet-install.sh --version 10.0.402 --install-dir /workspace/.dotnet
  export PATH="/workspace/.dotnet:$PATH"
fi
dotnet restore --locked-mode
dotnet build --no-restore
