#!/usr/bin/env bash
# Veritabanını bir yedekten geri yükler: mevcut veriler yedekteki hâliyle DEĞİŞTİRİLİR.
# Kullanım: deploy/geri-yukle.sh deploy/yedekler/onmuhasebe-YYYYMMDD-HHMMSS.dump --evet
set -euo pipefail
DIZIN="$(cd "$(dirname "$0")" && pwd)"
DOSYA="${1:?Yedek dosyası gerekli}"
[ -f "$DOSYA" ] || { echo "Dosya yok: $DOSYA"; exit 1; }
[ "${2:-}" = "--evet" ] || { echo "Mevcut veriler silinip yedekteki hâline dönecek. Onaylıyorsanız sona --evet ekleyin."; exit 1; }
C() { docker compose -f "$DIZIN/docker-compose.yml" --env-file "$DIZIN/.env" "$@"; }

# Bozuk ya da yanlış dosya, sistem durdurulmadan önce yakalanır.
C exec -T postgres pg_restore --list < "$DOSYA" > /dev/null || { echo "Geçerli bir yedek dosyası değil: $DOSYA"; exit 1; }

echo "Geri yüklemeden önce mevcut durumun yedeği alınıyor..."
C run --rm yedek simdi
C stop api
C exec -T postgres pg_restore -U onmuhasebe -d onmuhasebe --clean --if-exists --single-transaction < "$DOSYA"
# Yedek eski sürümden ise yeni migration'lar ve satır güvenliği yeniden uygulanır.
C run --rm migrator
C start api
echo "Geri yükleme tamamlandı: $DOSYA"
