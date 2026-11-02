#!/usr/bin/env bash
# Node makinede kurulu değil; npm komutlarını node:22 kapsayıcısında çalıştırır.
# Kullanım: bash scripts/npm.sh install | test | run build ...
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || cd "$(dirname "$0")/.." && pwd)"
MSYS_NO_PATHCONV=1 docker run --rm -v "$ROOT/web:/app" -w /app node:22 npm "$@"
