#!/usr/bin/env bash
# Mükerrer kayıt koruması (Idempotency-Key) senaryosu. Kullanım: tekrar_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_tekrar"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

istek() { # METOT YOL TOKEN GOVDE [ANAHTAR]
  local args=(-s -D "$DIR/basliklar" -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
  [ -n "$5" ] && args+=(-H "Idempotency-Key: $5")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde"); BASLIK=$(cat "$DIR/basliklar")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -qE -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:400}"; KALAN=$((KALAN+1))
  fi
}
dogru() { if eval "$1"; then echo "GECTI  $2"; GECEN=$((GECEN+1)); else echo "KALDI  $2"; KALAN=$((KALAN+1)); fi; }
bakiye() { istek GET "/api/kasabanka/hesaplar/$1" "$T"; kontrol 200 "$2" "\"bakiye\":$3(\.0+)?,"; }
tahsilat() { echo '{"hesapId":"'"$KASA"'","cariHesapId":"'"$C1"'","tarih":"2026-10-11","tutar":'"$1"'}'; }

U=$(date +%s)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Tekrar Test AS","ad":"T","soyad":"K","eposta":"tekrar'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"tekrardiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Musteri A","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C1=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Kasa","tur":"Kasa","paraBirimi":"TRY"}'; KASA=$(alan id)
K="tahsilat-$U-1"

echo "--- Ayni anahtarla tekrar gonderim"
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 100)" "$K"
kontrol 201 "Ilk gonderim islendi"; ILK=$(alan islemId)
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 100)" "$K"
kontrol 201 "Tekrar gonderim ayni cevabi dondu" "\"islemId\":\"$ILK\""
dogru 'echo "$BASLIK" | grep -qi "^Idempotent-Replayed: true"' "Cevap tekrar oldugunu basliginda belirtiyor"
bakiye "$KASA" "Kasaya yalnizca bir kez 100 girdi" 100
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 150)" "$K"
kontrol 422 "Ayni anahtar farkli icerikle reddedildi" tekrar_anahtari_uyusmuyor
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 100)" "ab"
kontrol 400 "Gecersiz anahtar reddedildi" tekrar_anahtari_gecersiz

echo "--- Dogrulama hatasindan sonra ayni anahtarla duzeltme"
K2="tahsilat-$U-2"
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 0)" "$K2"
kontrol 400 "Hatali istek dogrulamaya takildi"
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 40)" "$K2"
kontrol 201 "Duzeltilmis istek ayni anahtarla kaydedildi (hata cevaplari saklanmaz)"
bakiye "$KASA" "Kasa 140" 140

echo "--- Esszamanli iki istek"
K3="tahsilat-$U-3"
for i in 1 2; do
  curl -s -o "$DIR/es$i" -w "%{http_code}" -X POST "$KOK/api/kasabanka/islemler/tahsilat" -H "Content-Type: application/json" \
    -H "Authorization: Bearer $T" -H "Idempotency-Key: $K3" --data-binary "$(tahsilat 25)" > "$DIR/kod$i" &
done
wait
KODLAR="$(cat "$DIR/kod1") $(cat "$DIR/kod2")"
dogru '[[ "$KODLAR" =~ 201 ]]' "En az biri islendi ($KODLAR)"
bakiye "$KASA" "Esszamanli iki istekten yalnizca biri kayda gecti (165)" 165

echo "--- Anahtarsiz istek ve kapsam"
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 5)"
istek POST /api/kasabanka/islemler/tahsilat "$T" "$(tahsilat 5)"
bakiye "$KASA" "Anahtarsiz iki istek iki kayit (bilincli ayni tutar engellenmez)" 175
istek POST /api/cari/hesaplar "$T2" '{"unvan":"Baska","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}' "$K"
kontrol 201 "Anahtar firma ve kullaniciya ozel: baska firmada ayni anahtar serbest"

echo "--- Diger modullerde"
FATURA='{"tur":"Satis","cariHesapId":"'"$C1"'","tarih":"2026-10-10","genelIskontoTuru":"Yuzde","genelIskonto":0,"satirlar":[{"ad":"Hizmet","birim":"ADET","miktar":1,"birimFiyat":100,"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0}]}'
istek POST /api/fatura/faturalar "$T" "$FATURA" "fatura-$U"; F1=$(echo "$GOVDE" | grep -o '"numara":"[^"]*')
istek POST /api/fatura/faturalar "$T" "$FATURA" "fatura-$U"; F2=$(echo "$GOVDE" | grep -o '"numara":"[^"]*')
istek GET "/api/fatura/faturalar" "$T"
kontrol 200 "Ayni anahtarla iki fatura gonderimi tek fatura" '"toplamKayit":1'
dogru '[ "$F1" = "$F2" ]' "Tekrar cevabinda ayni fatura numarasi ($F1)"
CEK='{"cariHesapId":"'"$C1"'","tarih":"2026-10-12","riskOnaylandi":false,"evraklar":[{"tur":"Senet","seriNo":"S1","vadeTarihi":"2026-12-01","tutar":100,"paraBirimi":"TRY","borclu":{"ad":"Borclu"}}]}'
istek POST /api/kasabanka/cek-senet/giris "$T" "$CEK" "bordro-$U"
istek POST /api/kasabanka/cek-senet/giris "$T" "$CEK" "bordro-$U"
istek GET "/api/kasabanka/cek-senet/evraklar" "$T"
kontrol 200 "Ayni anahtarla iki bordro gonderimi tek evrak" '"toplamKayit":1'
istek GET "/api/kasabanka/hesaplar/$KASA" "$T" "" "okuma-$U"
kontrol 200 "Okuma isteginde anahtar yok sayilir"

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
