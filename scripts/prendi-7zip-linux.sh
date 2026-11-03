#!/bin/sh
# 7-Zip per Linux (il programma ufficiale, 7zz). Finisce in motori/7zip-linux.
set -eu
radice=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
dest="$radice/motori/7zip-linux"
if [ -x "$dest/7zz" ]; then
  echo "7-Zip c'è già in $dest"
  exit 0
fi
url="https://www.7-zip.org/a/7z2501-linux-x64.tar.xz"
lavoro=$(mktemp -d)
trap 'rm -rf "$lavoro"' EXIT
echo "Scarico $url"
curl -fsSL -o "$lavoro/7z.tar.xz" "$url"
tar -xJf "$lavoro/7z.tar.xz" -C "$lavoro"
mkdir -p "$dest"
cp "$lavoro/7zz" "$dest/7zz"
chmod +x "$dest/7zz"
echo "7-Zip pronto in $dest"
