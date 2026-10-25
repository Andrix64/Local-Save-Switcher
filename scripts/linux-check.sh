#!/usr/bin/env bash
# Prove su Linux senza passare da GitHub: copia il commit attuale su una
# macchina Linux con Docker raggiungibile via SSH (con chiave, senza password)
# e lì, solo in container temporanei:
#   1. esegue i test;
#   2. crea i pacchetti con scripts/release.sh (come il job linux della CI);
#   3. installa ogni pacchetto in una distribuzione pulita e avvia "lss --version".
#
# Uso (da Git Bash su Windows o da Linux):
#   bash scripts/linux-check.sh utente@host [--tests-only]
#
# Sulla macchina usa solo ~/lss-ci (sorgenti), ~/lss-out (pacchetti),
# ~/.lss-nuget e il volume Docker lss-nuget-root (cache di NuGet). L'utente
# deve poter usare docker; root non serve.
set -euo pipefail

if [ $# -lt 1 ]; then
    echo "Uso: bash scripts/linux-check.sh utente@host [--tests-only]" >&2
    exit 2
fi

target="$1"
tests_only=0
[ "${2:-}" = "--tests-only" ] && tests_only=1

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

# Stesse versioni della CI (.github/workflows/build.yml).
sdk_image="mcr.microsoft.com/dotnet/sdk:10.0"
nfpm_version="2.47.0"

ssh_run() {
    ssh -o BatchMode=yes "$target" "$@"
}

if [ -n "$(git status --porcelain)" ]; then
    echo "Attenzione: ci sono modifiche non committate, che NON vengono copiate (solo HEAD)." >&2
fi

echo "== Copia di $(git rev-parse --short HEAD) su $target"
git archive --format=tar HEAD | ssh_run 'rm -rf ~/lss-ci && mkdir -p ~/lss-ci ~/.lss-nuget && tar -x -C ~/lss-ci'

echo "== Test"
# Root dentro il container per installare libfontconfig1 (la dipendenza del
# pacchetto, che SkiaSharp usa anche nei test delle icone); poi i file in ~/lss-ci
# tornano al proprietario giusto.
ssh_run "docker run --rm -e OWNER=\"\$(id -u):\$(id -g)\" \
    -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e NUGET_PACKAGES=/nuget \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
    -v ~/lss-ci:/src -v ~/.lss-nuget:/nuget -w /src $sdk_image bash -c '
set -e
trap \"chown -R \$OWNER /src\" EXIT
apt-get update -qq >/dev/null && apt-get install -y -qq libfontconfig1 >/dev/null
dotnet test tests/Lss.Tests -c Release
'"

[ "$tests_only" -eq 1 ] && exit 0

# Root dentro il container (per installare nfpm e file), ma su una copia dei
# sorgenti: a ~/lss-out arrivano solo i pacchetti, con il proprietario giusto.
echo "== Pacchetti"
ssh_run "rm -rf ~/lss-out && mkdir -p ~/lss-out && docker run --rm \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 -e OWNER=\"\$(id -u):\$(id -g)\" \
    -v ~/lss-ci:/src:ro -v ~/lss-out:/out -v lss-nuget-root:/root/.nuget/packages $sdk_image \
    bash -c '
set -e
trap \"chown -R \$OWNER /out\" EXIT
apt-get update -qq >/dev/null && apt-get install -y -qq file >/dev/null
cp -r /src /work && cd /work
base=https://github.com/goreleaser/nfpm/releases/download/v$nfpm_version
deb=nfpm_${nfpm_version}_amd64.deb
curl -fsSL -o /tmp/\$deb \$base/\$deb
curl -fsSL -o /tmp/checksums.txt \$base/checksums.txt
(cd /tmp && grep \" \$deb\$\" checksums.txt | sha256sum -c -)
dpkg -i /tmp/\$deb >/dev/null
bash scripts/release.sh --skip-tests --allow-dirty
cp dist/*.tar.gz dist/*.deb dist/*.rpm dist/*.AppImage dist/*.sha256 /out/
'"

echo "== Installazione nelle distribuzioni"
ssh_run 'cd ~/lss-out
failed=0
check() {
    name=$1; shift
    if out=$(docker run --rm -v ~/lss-out:/dist:ro "$@" 2>&1) && echo "$out" | tail -1 | grep -q "^LSS "; then
        echo "OK      $name: $(echo "$out" | tail -1)"
    else
        echo "ERRORE  $name"; echo "$out" | tail -15; failed=1
    fi
}
check "deb Ubuntu 24.04" ubuntu:24.04 bash -c "apt-get update -qq >/dev/null && apt-get install -y -qq /dist/lss_*_amd64.deb >/dev/null && lss --version"
check "deb Ubuntu 22.04" ubuntu:22.04 bash -c "apt-get update -qq >/dev/null && apt-get install -y -qq /dist/lss_*_amd64.deb >/dev/null && lss --version"
check "deb Debian 13" debian:13 bash -c "apt-get update -qq >/dev/null && apt-get install -y -qq /dist/lss_*_amd64.deb >/dev/null && lss --version"
check "rpm Fedora 42" fedora:42 bash -c "dnf install -y -q /dist/lss-*.x86_64.rpm >/dev/null && lss --version"
check "AppImage Ubuntu 24.04" ubuntu:24.04 bash -c "apt-get update -qq >/dev/null && apt-get install -y -qq libicu74 >/dev/null && /dist/LSS-*-x86_64.AppImage --appimage-extract-and-run --version"
check "tar.gz Arch" archlinux:latest bash -c "pacman -Sy --noconfirm --needed icu >/dev/null && useradd -m u && su u -c \"cd /tmp && tar -xzf /dist/LSS-*-linux-x64.tar.gz && cd LSS-*-linux-x64 && ./install.sh >/dev/null && ~/.local/bin/lss --version\""
exit $failed'

echo "== Tutto a posto. Pacchetti in $target:~/lss-out"
