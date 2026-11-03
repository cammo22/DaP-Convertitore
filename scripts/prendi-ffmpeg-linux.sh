#!/bin/sh
# FFmpeg per Linux (build GPL di BtbN, la stessa famiglia di quello di Windows).
# Finisce in motori/ffmpeg-linux, che git non vede. Se c'è già ffmpeg nel PATH, l'app lo usa lo stesso.
set -eu
radice=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
dest="$radice/motori/ffmpeg-linux"
if [ -x "$dest/ffmpeg" ] && [ -x "$dest/ffprobe" ]; then
  echo "FFmpeg c'è già in $dest"
  exit 0
fi
nome="ffmpeg-n9.0-latest-linux64-gpl-9.0"
url="https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/$nome.tar.xz"
lavoro=$(mktemp -d)
trap 'rm -rf "$lavoro"' EXIT
echo "Scarico $url"
curl -fsSL -o "$lavoro/ffmpeg.tar.xz" "$url"
tar -xJf "$lavoro/ffmpeg.tar.xz" -C "$lavoro"
mkdir -p "$dest"
cp "$lavoro/$nome/bin/ffmpeg" "$lavoro/$nome/bin/ffprobe" "$dest/"
chmod +x "$dest/ffmpeg" "$dest/ffprobe"
echo "FFmpeg pronto in $dest"
