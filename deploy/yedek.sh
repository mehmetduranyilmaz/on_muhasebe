#!/bin/sh
# Yedek kapsayıcısında çalışır. Argümansız: her gün YEDEK_SAATI_UTC saatinde (varsayılan 0 = 03.00 TSİ) yedek alır,
# YEDEK_GUN günden eskileri siler. "simdi" argümanıyla tek yedek alıp çıkar.
set -eu
# Yedekte tüm firmaların verisi var: dosyalar ve klasör yalnızca sahibince okunabilir.
umask 077
chmod 700 /yedekler
YEDEK_SAATI_UTC="${YEDEK_SAATI_UTC:-0}"
YEDEK_GUN="${YEDEK_GUN:-14}"

al() {
  dosya="/yedekler/onmuhasebe-$(date -u +%Y%m%d-%H%M%S).dump"
  # Önce geçici ada yazılır: yarım kalan yedek tam yedek sanılmasın.
  pg_dump -Fc -f "$dosya.yarim"
  mv "$dosya.yarim" "$dosya"
  find /yedekler -name 'onmuhasebe-*.dump' -mtime +"$((YEDEK_GUN - 1))" -delete
  echo "Yedek alındı: $dosya ($(du -h "$dosya" | cut -f1))"
}

if [ "${1:-}" = "simdi" ]; then
  al
  exit 0
fi

while true; do
  simdi=$(date -u +%s)
  hedef=$(date -u -d "$(date -u +%Y-%m-%d) $(printf %02d "$YEDEK_SAATI_UTC"):00:00" +%s)
  [ "$hedef" -gt "$simdi" ] || hedef=$((hedef + 86400))
  sleep $((hedef - simdi))
  al || echo "YEDEK BAŞARISIZ $(date -u)" >&2
done
