#!/usr/bin/env bash
# Crea i pacchetti Linux di rilascio in dist/ con la loro impronta SHA-256.
# È l'equivalente Linux di scripts/release.ps1 (che fa lo zip per Windows).
#
# Uso: bash scripts/release.sh [--skip-tests] [--allow-dirty] [--only FORMATI]
#   --skip-tests   non esegue i test (solo per prove veloci)
#   --allow-dirty  crea i pacchetti anche con modifiche non committate
#   --only FORMATI crea solo i formati indicati, separati da virgola:
#                  targz, deb, rpm, appimage (default: tutti).
#                  Esempio: --only appimage
#
# La versione si legge da <Version> in src/Lss.App/Lss.App.csproj.
set -euo pipefail

skip_tests=0
allow_dirty=0
only="targz,deb,rpm,appimage"
while [ $# -gt 0 ]; do
    case "$1" in
        --skip-tests) skip_tests=1 ;;
        --allow-dirty) allow_dirty=1 ;;
        --only)
            if [ $# -lt 2 ]; then
                echo "--only richiede un elenco di formati (targz, deb, rpm, appimage)." >&2
                exit 2
            fi
            only="$2"
            shift
            ;;
        --only=*) only="${1#--only=}" ;;
        *) echo "Opzione sconosciuta: $1" >&2; exit 2 ;;
    esac
    shift
done

for format in ${only//,/ }; do
    case "$format" in
        targz|deb|rpm|appimage) ;;
        *) echo "Formato sconosciuto: $format (validi: targz, deb, rpm, appimage)." >&2; exit 2 ;;
    esac
done

# Vero se il formato è tra quelli richiesti con --only.
wants() {
    case ",$only," in
        *",$1,"*) return 0 ;;
        *) return 1 ;;
    esac
}

root="$(cd "$(dirname "$0")/.." && pwd)"
# nfpm legge i percorsi di nfpm.yaml dalla cartella corrente.
cd "$root"

# deb e rpm si creano con nfpm: meglio saperlo prima di pubblicare.
if { wants deb || wants rpm; } && ! command -v nfpm >/dev/null 2>&1; then
    echo "nfpm non trovato (https://nfpm.goreleaser.com): serve per creare deb e rpm." >&2
    exit 1
fi

# appimagetool usa il comando file (manca per esempio nell'immagine Docker dell'SDK .NET).
if wants appimage && ! command -v file >/dev/null 2>&1; then
    echo "Comando file non trovato: serve ad appimagetool (Debian/Ubuntu: apt install file)." >&2
    exit 1
fi

project="src/Lss.App/Lss.App.csproj"
tests="tests/Lss.Tests"
publish_dir="bin/publish/linux-x64"
dist="dist"

# appimagetool: versione fissa e impronta SHA-256, controllata dopo il download.
appimagetool_version="1.9.1"
appimagetool_sha256="ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0"

# Un pacchetto deve corrispondere a un commit preciso.
if [ "$allow_dirty" -eq 0 ] && [ -n "$(git status --porcelain)" ]; then
    echo "Ci sono modifiche non committate. Fai il commit o usa --allow-dirty." >&2
    exit 1
fi

version="$(dotnet msbuild "$project" -getProperty:Version | tr -d '[:space:]')"
if [ -z "$version" ]; then
    echo "Versione non trovata nel .csproj." >&2
    exit 1
fi

name="LSS-$version-linux-x64"
echo "Rilascio $name"

if [ "$skip_tests" -eq 0 ]; then
    echo "== Test"
    dotnet test "$tests" -c Release --nologo
fi

echo "== Pubblicazione"
rm -rf "$publish_dir"
dotnet publish "$project" -p:PublishProfile=linux-x64 --nologo
if [ ! -f "$publish_dir/LSS" ]; then
    echo "Exe non trovato: $publish_dir/LSS" >&2
    exit 1
fi

mkdir -p "$dist"
# File creati in dist/ (si aggiungono sotto, uno per formato richiesto).
artifacts=()

if wants targz; then
# Contenuto del tar.gz: l'exe, l'installer, il file desktop, i metadati e le icone.
stage="$dist/$name"
rm -rf "$stage"
mkdir -p "$stage"
cp "$publish_dir/LSS" "$stage/"
for file in README.md CHANGELOG.md LICENSE; do
    cp "$file" "$stage/"
done
cp packaging/linux/io.github.andrix64.LocalSaveSwitcher.desktop "$stage/"
cp packaging/linux/io.github.andrix64.LocalSaveSwitcher.metainfo.xml "$stage/"
cp -r packaging/linux/icons "$stage/icons"

# Installa per l'utente che lo esegue, senza root (in ~/.local).
cat > "$stage/install.sh" <<'EOF'
#!/bin/sh
# Installa LSS per l'utente che lo esegue, senza root.
# Uso: ./install.sh              installa in ~/.local
#      ./install.sh --uninstall  rimuove il programma (i dati restano)
set -eu

app_id=io.github.andrix64.LocalSaveSwitcher
here="$(cd "$(dirname "$0")" && pwd)"
lib="$HOME/.local/lib/lss"
bin="$HOME/.local/bin"
apps="$HOME/.local/share/applications"
icons="$HOME/.local/share/icons/hicolor"

if [ "${1:-}" = "--uninstall" ]; then
    rm -rf "$lib"
    rm -f "$bin/lss" "$apps/$app_id.desktop"
    for dir in "$icons"/*/apps; do
        rm -f "$dir/$app_id.png"
    done
    echo "LSS rimosso. Le partite e i salvataggi in ~/.local/share/LSS sono rimasti."
    exit 0
fi

mkdir -p "$lib" "$bin" "$apps"
cp "$here/LSS" "$lib/LSS"
chmod +x "$lib/LSS"
ln -sf "$lib/LSS" "$bin/lss"
cp "$here/$app_id.desktop" "$apps/"
for size in 16x16 24x24 32x32 48x48 64x64 256x256; do
    mkdir -p "$icons/$size/apps"
    cp "$here/icons/$size/$app_id.png" "$icons/$size/apps/"
done

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$apps" || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t "$icons" 2>/dev/null || true
fi

echo "LSS installato:"
echo "  programma: $lib/LSS"
echo "  comando:   $bin/lss"
echo "  menu:      $apps/$app_id.desktop"
case ":$PATH:" in
    *":$bin:"*) ;;
    *) echo "Aggiungi $bin al PATH per avviare LSS con il comando lss." ;;
esac
EOF
chmod +x "$stage/LSS" "$stage/install.sh"

tar -C "$dist" -czf "$dist/$name.tar.gz" "$name"
rm -rf "$stage"
artifacts+=("$name.tar.gz")
fi

if wants deb; then
    echo "== deb"
    VERSION="$version" nfpm package --config packaging/linux/nfpm.yaml --packager deb \
        --target "$dist/lss_${version}_amd64.deb"
    artifacts+=("lss_${version}_amd64.deb")
fi

if wants rpm; then
    echo "== rpm"
    VERSION="$version" nfpm package --config packaging/linux/nfpm.yaml --packager rpm \
        --target "$dist/lss-${version}-1.x86_64.rpm"
    artifacts+=("lss-${version}-1.x86_64.rpm")
fi

if wants appimage; then
# AppImage: un solo file eseguibile su quasi ogni distribuzione.
tool="$dist/tools/appimagetool-$appimagetool_version-x86_64.AppImage"
if [ ! -f "$tool" ]; then
    mkdir -p "$dist/tools"
    curl -fL -o "$tool" "https://github.com/AppImage/appimagetool/releases/download/$appimagetool_version/appimagetool-x86_64.AppImage"
fi
if ! echo "$appimagetool_sha256  $tool" | sha256sum -c --status; then
    rm -f "$tool"
    echo "Impronta SHA-256 di appimagetool non valida." >&2
    exit 1
fi
chmod +x "$tool"

echo "== AppImage"
appdir="$dist/LSS.AppDir"
rm -rf "$appdir"
mkdir -p "$appdir/usr/lib/lss" "$appdir/usr/share/applications" "$appdir/usr/share/metainfo"
cp "$publish_dir/LSS" "$appdir/usr/lib/lss/LSS"
cp packaging/linux/io.github.andrix64.LocalSaveSwitcher.desktop "$appdir/usr/share/applications/"
cp packaging/linux/io.github.andrix64.LocalSaveSwitcher.metainfo.xml "$appdir/usr/share/metainfo/"
for size in 16x16 24x24 32x32 48x48 64x64 256x256; do
    mkdir -p "$appdir/usr/share/icons/hicolor/$size/apps"
    cp "packaging/linux/icons/$size/io.github.andrix64.LocalSaveSwitcher.png" "$appdir/usr/share/icons/hicolor/$size/apps/"
done
# Alla radice: il file desktop e l'icona che appimagetool cerca per nome.
cp packaging/linux/io.github.andrix64.LocalSaveSwitcher.desktop "$appdir/"
cp packaging/linux/icons/256x256/io.github.andrix64.LocalSaveSwitcher.png "$appdir/"
cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/lib/lss/LSS" "$@"
EOF
chmod +x "$appdir/AppRun" "$appdir/usr/lib/lss/LSS"

appimage="LSS-$version-x86_64.AppImage"
ARCH=x86_64 "$tool" --appimage-extract-and-run "$appdir" "$dist/$appimage"
rm -rf "$appdir"
artifacts+=("$appimage")
fi

echo "== SHA-256"
for file in "${artifacts[@]}"; do
    (cd "$dist" && sha256sum "$file" > "$file.sha256")
done

echo ""
echo "Creati in $dist/:"
for file in "${artifacts[@]}"; do
    echo "  $file ($(du -h "$dist/$file" | cut -f1))"
done
