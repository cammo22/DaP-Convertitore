#!/bin/sh
# Impacchetta l'app di Linux in un AppImage: un file solo, si scarica e si apre.
# Vuole gli attrezzi già scaricati (scripts/prendi-ffmpeg-linux.sh e prendi-7zip-linux.sh) e ui/dist.
set -eu
radice=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
cd "$radice"
appdir="$radice/AppDir"
rm -rf "$appdir"
mkdir -p "$appdir/usr/bin"
dotnet publish src/DaPConvertitore.Linux/DaPConvertitore.Linux.csproj -c Release -r linux-x64 --self-contained -o "$appdir/usr/bin" -nologo
cat > "$appdir/AppRun" << 'EOF'
#!/bin/sh
HERE=$(dirname "$(readlink -f "$0")")
export PATH="$HERE/usr/bin/motori/ffmpeg-linux:$HERE/usr/bin/motori/7zip-linux:$PATH"
exec "$HERE/usr/bin/DaPConvertitore" "$@"
EOF
chmod +x "$appdir/AppRun"
cp "$radice/risorse/icona-256.png" "$appdir/dap-convertitore.png"
cat > "$appdir/dap-convertitore.desktop" << 'EOF'
[Desktop Entry]
Type=Application
Name=DaP Convertitore
Exec=DaPConvertitore %F
Icon=dap-convertitore
Categories=AudioVideo;Utility;
Terminal=false
EOF
attrezzo="$radice/motori/appimagetool"
if [ ! -x "$attrezzo" ]; then
  mkdir -p "$radice/motori"
  curl -fsSL -o "$attrezzo" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
  chmod +x "$attrezzo"
fi
mkdir -p "$radice/Releases"
chmod -R a+rX "$appdir"
ARCH=x86_64 "$attrezzo" --appimage-extract-and-run "$appdir" "$radice/Releases/DaPConvertitore-linux.AppImage"
chmod +x "$radice/Releases/DaPConvertitore-linux.AppImage"
echo "AppImage pronto: $radice/Releases/DaPConvertitore-linux.AppImage"
