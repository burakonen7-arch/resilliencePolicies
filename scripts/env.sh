#!/usr/bin/env bash
# Source this file from the repository root.
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if [ -x /workspace/.dotnet/dotnet ]; then
  export PATH="/workspace/.dotnet:$PATH"
fi
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:5080}"
export ConnectionStrings__Products="${ConnectionStrings__Products:-Host=127.0.0.1;Port=5432;Database=products;Username=postgres}"
