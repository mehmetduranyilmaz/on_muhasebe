#!/usr/bin/env bash
# Tek komutla kurulum/güncelleme. İlk çalıştırmada deploy/.env gizli değerler rastgele üretilerek oluşturulur.
# Kullanım: deploy/kur.sh <alan-adı> <yönetici-e-postası>     (ör. deploy/kur.sh muhasebe.firma.com ben@firma.com)
#           deploy/kur.sh                                     (.env varsa: yeni sürümü derleyip yeniden başlatır)
set -euo pipefail
DIZIN="$(cd "$(dirname "$0")" && pwd)"
ENV="$DIZIN/.env"

if [ ! -f "$ENV" ]; then
  ALAN="${1:?Alan adı gerekli (yerel deneme için: localhost)}"
  EPOSTA="${2:?Sistem yöneticisi e-postası gerekli}"
  rastgele() { openssl rand -hex "$1"; }
  SIFRE="$(rastgele 12)"
  umask 077
  cat > "$ENV" <<AYAR
# Canlı ortam ayarları. Bu dosya gizlidir: repoya eklenmez, yedeği güvenli bir yerde saklanmalıdır
# (BILDIRIM_SIFRELEME_ANAHTARI kaybolursa kayıtlı SMTP şifresi yeniden girilmelidir).
ALAN_ADI=$ALAN
ACME_EPOSTA=$EPOSTA
POSTGRES_SIFRESI=$(rastgele 24)
UYGULAMA_DB_SIFRESI=$(rastgele 24)
JWT_ANAHTARI=$(openssl rand -base64 48 | tr -d '\n')
BILDIRIM_SIFRELEME_ANAHTARI=$(openssl rand -base64 32 | tr -d '\n')
SISTEM_YONETICISI_EPOSTA=$EPOSTA
SISTEM_YONETICISI_SIFRESI=$SIFRE
AYAR
  echo "Ayarlar oluşturuldu: $ENV"
  # Şifre ekrana basılmaz (terminal geçmişine düşmesin); .env içindeki SISTEM_YONETICISI_SIFRESI ile ilk girişi yapıp değiştirin.
  echo "Sistem yöneticisi: $EPOSTA (ilk şifre: $ENV içinde SISTEM_YONETICISI_SIFRESI; girişten sonra menüden değiştirin)"
fi

docker compose -f "$DIZIN/docker-compose.yml" --env-file "$ENV" up -d --build --remove-orphans
docker compose -f "$DIZIN/docker-compose.yml" --env-file "$ENV" ps
