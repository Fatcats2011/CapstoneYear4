#!/usr/bin/env bash
# Writes a Unity .meta file with a fresh GUID. Usage: newmeta.sh <asset-path> [folder]
set -euo pipefail
guid=$(head -c16 /dev/urandom | od -An -tx1 | tr -d ' \n')
if [ "${2:-}" = "folder" ]; then
  printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "$1.meta"
else
  printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid" > "$1.meta"
fi
echo "$1.meta ($guid)"
