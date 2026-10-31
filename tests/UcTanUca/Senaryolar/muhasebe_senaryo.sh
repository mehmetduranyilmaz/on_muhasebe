#!/usr/bin/env bash
# Muhasebe aktarımı (mali müşavir köprüsü) uçtan uca senaryosu. Kullanım: muhasebe_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_muhasebe"; rm -rf "$DIR"; mkdir -p "$DIR"
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
M=/api/muhasebe
U=$(date +%s)
kayit_ol '{"firmaUnvani":"Muhasebe Test AS","ad":"Mehmet","soyad":"Yon","eposta":"muhasebe'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Muhasebe AS","ad":"D","soyad":"F","eposta":"muhasebediger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Musteri Bir","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C1=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Tedarikci Bir","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C2=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Merkez Kasa","tur":"Kasa","paraBirimi":"TRY"}'

echo "--- Hesap plani"
istek GET $M/hesap-plani "$T"
kontrol 200 "Varsayilan Tek Duzen Hesap Plani" '"ayirac":"\.","cariAltGrup":"01".*"rol":"Alicilar","ad":"[^"]*","kod":"120","varsayilanKod":"120"'
istek PUT $M/hesap-plani "$T" '{"ayirac":".","cariAltGrup":"01","kodlar":{"Alicilar":"120.10","HesaplananKdv":"391"}}'
kontrol 200 "Alicilar ana hesabi degisti" '"rol":"Alicilar","ad":"[^"]*","kod":"120.10"'
istek PUT $M/hesap-plani "$T" '{"ayirac":"/","cariAltGrup":"01","kodlar":{}}'
kontrol 400 "Gecersiz ayirac reddedilir" 'Ayıraç'
istek PUT $M/hesap-plani "$T" '{"ayirac":".","cariAltGrup":"01","kodlar":{"Saticilar":"32;0"}}'
kontrol 400 "Gecersiz hesap kodu reddedilir" 'Hesap kodu'

echo "--- Kart eslestirme"
istek GET /api/kasabanka/kategoriler "$T"
istek GET $M/eslemeler/cari "$T"
kontrol 200 "Musteri otomatik 120.10.01.x, tedarikci 320.01.x" '"ad":"Musteri Bir","tur":"Musteri","otomatikKod":"120\.10\.01\.[0-9]+".*"ad":"Tedarikci Bir","tur":"Tedarikci","otomatikKod":"320\.01\.[0-9]+"'
istek PUT "$M/eslemeler/cari/$C1" "$T" '{"kod":"120.99.001"}'
kontrol 204 "Cariye ozel kod verildi"
istek GET $M/eslemeler/cari "$T"
kontrol 200 "Ozel kod kullaniliyor" '"ad":"Musteri Bir".*"ozelKod":"120\.99\.001","kod":"120\.99\.001"'
istek PUT "$M/eslemeler/cari/$C1" "$T" '{"kod":""}'
istek GET $M/eslemeler/cari "$T"
kontrol 200 "Bos kod otomatige dondu" '"ad":"Musteri Bir".*"ozelKod":null,"kod":"120\.10\.01'
istek GET $M/eslemeler/kasahesabi "$T"
kontrol 200 "Kasa hesabi 100.x" '"ad":"Merkez Kasa","tur":"Kasa","otomatikKod":"100\.[0-9]+"'
istek GET $M/eslemeler/kategori "$T"
kontrol 200 "Kategoriler gelir 649, gider 770" '"tur":"Gelir","otomatikKod":"649".*"tur":"Gider","otomatikKod":"770"'
istek PUT "$M/eslemeler/cari/$C1" "$T2" '{"kod":"120.01.999"}'
kontrol 404 "Baska firmanin carisine kod verilemez" kart_bulunamadi
istek GET $M/eslemeler/uydurma "$T"
kontrol 400 "Gecersiz kart turu" kart_turu

echo "--- Mali musavir rolu"
istek GET $Y/roller "$T"
kontrol 200 "Hazir mali musavir rolu isaretli" '"ad":"Salt Okur \(Mali M[^"]*\)".*"maliMusavirMi":true'
MM=$(echo "$GOVDE" | tr -d '\n' | sed 's/},{/}\n{/g' | grep '"maliMusavirMi":true' | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
istek PUT "$Y/roller/$MM" "$T" '{"ad":"Salt Okur (Mali Musavir)","yetkiler":["cari.hesap.goruntule","fatura.belge.ekle"],"maliMusavirMi":true}'
kontrol 400 "Mali musavir rolune islem yetkisi verilemez" mali_musavir_yetkisi
istek POST $Y/roller "$T" '{"ad":"Disaridan Muhasebeci","yetkiler":["cari.hesap.goruntule","muhasebe.aktarim.goruntule","muhasebe.aktarim.ekle"],"maliMusavirMi":true}'
kontrol 201 "Goruntuleme ve aktarim yetkili mali musavir rolu acildi" '"maliMusavirMi":true'

MALI="muhasebeci$U@ornek.com"
davet_kabul "$T" '{"ad":"Mali","soyad":"Musavir","eposta":"'"$MALI"'","sifre":"Sifre1234","rol":"Disaridan"}'
kontrol 200 "Mali musavir davetle katildi" accessToken; MT=$(alan accessToken)
istek GET $M/eslemeler/cari "$MT"
kontrol 403 "Plan yetkisi olmayan mali musavir eslesmeleri goremez" yetki_yok
istek POST /api/cari/hesaplar "$MT" '{"unvan":"Yasak","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'
kontrol 403 "Mali musavir kayit acamaz" yetki_yok
istek GET /api/yonetim/abonelik "$T"
kontrol 200 "Mali musavir kullanici sayisina girmez" '"kullaniciSayisi":1,'

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
