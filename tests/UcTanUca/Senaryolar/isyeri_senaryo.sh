#!/usr/bin/env bash
# Çoklu işyeri uçtan uca senaryosu. Kullanım: isyeri_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_isyeri"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=("${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -qE -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:500}"; KALAN=$((KALAN+1))
  fi
}

U=$(date +%s)
U=$(date +%s)
kayit_ol '{"firmaUnvani":"Isyeri Test AS","ad":"Ilker","soyad":"Yon","eposta":"isyeri'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Isyeri AS","ad":"D","soyad":"F","eposta":"isyeridiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)

echo "--- Merkez ve isyeri kartlari"
istek GET $Y/isyerleri "$T"
kontrol 200 "Yeni firmaya merkez isyeri acildi" '^\[\{"id":"[^"]*","kod":"MRK","ad":"Merkez","merkezMi":true,"aktifMi":true'
MERKEZ=$(alan id)
istek POST $Y/isyerleri "$T" '{"kod":"ank","ad":"Ankara Sube","il":"Ankara","ilce":"Cankaya","sgkSicilNo":"12345678901234567890123456"}'
kontrol 201 "Sube eklendi, kod buyuk harfe cevrildi" '"kod":"ANK","ad":"Ankara Sube","merkezMi":false'
ANK=$(alan id)
istek POST $Y/isyerleri "$T" '{"kod":"ANK","ad":"Ikinci"}'
kontrol 409 "Ayni kodla ikinci isyeri acilmaz" isyeri_kodu_kayitli
istek POST $Y/isyerleri "$T" '{"kod":"IZM","ad":"Izmir","sgkSicilNo":"123"}'
kontrol 400 "SGK sicil no 26 hane olmali" '26 haneli'
istek POST "$Y/isyerleri/$MERKEZ/pasiflestir" "$T"
kontrol 422 "Merkez kapatilamaz" merkez_kapatilamaz
istek POST "$Y/isyerleri/$ANK/merkez-yap" "$T"
kontrol 200 "Ankara merkez yapildi" '"merkezMi":true'
istek GET $Y/isyerleri "$T"
kontrol 200 "Tek merkez var, eski merkez siradan isyeri oldu" '^\[\{"id":"'"$ANK"'".*"kod":"MRK","ad":"Merkez","merkezMi":false'
istek POST "$Y/isyerleri/$MERKEZ/pasiflestir" "$T"
kontrol 200 "Eski merkez artik kapatilabilir" '"aktifMi":false'
istek POST "$Y/isyerleri/$MERKEZ/merkez-yap" "$T"
kontrol 422 "Kapali isyeri merkez yapilamaz" isyeri_pasif
istek POST "$Y/isyerleri/$MERKEZ/aktiflestir" "$T"
kontrol 200 "Isyeri yeniden acildi" '"aktifMi":true'
istek GET $Y/isyerleri "$T2"
if echo "$GOVDE" | grep -q "$ANK"; then KOD=999; fi
kontrol 200 "Baska firma bu isyerlerini gormez"
istek PUT "$Y/isyerleri/$ANK" "$T2" '{"kod":"X","ad":"X"}'
kontrol 404 "Baska firmanin isyeri guncellenemez" isyeri_bulunamadi

echo "--- Kullaniciya isyeri kisiti"
KEMAL="isyerikemal$U@ornek.com"
davet_kabul "$T" '{"ad":"Kemal","soyad":"Sube","eposta":"'"$KEMAL"'","sifre":"Sifre1234","rol":"Kullanici"}'
K=$(alan accessToken)
istek GET $Y/kullanicilar "$T"
KEMAL_ID=$(echo "$GOVDE" | tr -d '\n' | sed 's/},{/}\n{/g' | grep "$KEMAL" | grep -o '"kullaniciId":"[^"]*"' | cut -d'"' -f4)
ROL=$(rol_id "$T" Kullanici)
istek PUT "$Y/kullanicilar/$KEMAL_ID/yetkiler" "$T" '{"rolId":"'"$ROL"'","isyerleri":["'"$MERKEZ"'","'"$(cat /proc/sys/kernel/random/uuid 2>/dev/null || echo 00000000-0000-0000-0000-000000000009)"'"]}'
kontrol 404 "Firmada olmayan isyeri atanamaz" isyeri_bulunamadi
istek PUT "$Y/kullanicilar/$KEMAL_ID/yetkiler" "$T" '{"rolId":"'"$ROL"'","isyerleri":["'"$MERKEZ"'"],"varsayilanIsyeriId":"'"$MERKEZ"'"}'
kontrol 204 "Kemal yalniz eski merkeze kisitlandi"
istek GET $Y/kullanicilar "$T"
kontrol 200 "Kullanici listesinde isyeri kisiti gorunuyor" '"isyerleri":\["'"$MERKEZ"'"\],"varsayilanIsyeriId":"'"$MERKEZ"'"'
istek GET $Y/isyerleri "$K"
if echo "$GOVDE" | grep -q "$ANK"; then KOD=999; fi
kontrol 200 "Kisitli kullanici yalniz girebildigi isyerini gorur" '"kod":"MRK"'
istek POST $Y/isyerleri "$K" '{"kod":"YET","ad":"Yetkisiz"}'
kontrol 403 "Firma ayari yetkisi olmayan isyeri acamaz" yetki_yok

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
