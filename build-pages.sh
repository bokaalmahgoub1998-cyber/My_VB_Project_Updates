#!/usr/bin/env bash
set -euo pipefail
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
dotnet publish Mahgoub_Store/Mahgoub_Store.csproj -c Release -o cf-dist
mkdir -p functions
cp -a Mahgoub_Store/functions/. functions/
