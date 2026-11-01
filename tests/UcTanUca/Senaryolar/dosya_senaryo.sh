#!/usr/bin/env bash
# Dosya eki uctan uca senaryosu. Kullanim: dosya_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_dosya"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim
D=/api/dosya

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=("${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
yukle() { # YOL TOKEN DOSYA
  KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK$1" -H "Authorization: Bearer $2" -F "dosya=@$3"); GOVDE=$(cat "$DIR/govde")
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
kayit_ol '{"firmaUnvani":"Dosya Test AS","ad":"Deniz","soyad":"Yon","eposta":"dosya'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Dosya AS","ad":"D","soyad":"F","eposta":"dosyadiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Dosyali Cari","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C1=$(alan id)
K="$D/kayit/cari/$C1"

printf '%%PDF-1.4 sozlesme icerigi' > "$DIR/sozlesme.pdf"
printf '\x89PNG\r\n\x1a\n gorsel' > "$DIR/gorsel.png"
printf '<?xml version="1.0"?><Invoice/>' > "$DIR/efatura.xml"
printf 'MZ calisabilir dosya' > "$DIR/kotu.pdf"
printf 'MZ calisabilir dosya' > "$DIR/kotu.exe"
printf '<html><script>alert(1)</script></html>' > "$DIR/kotu.html"
: > "$DIR/bos.pdf"
{ printf '%%PDF-1.4'; head -c $((20*1024*1024+512*1024)) /dev/zero; } > "$DIR/buyuk.pdf"

echo "--- Yukleme"
yukle "$K" "$T" "$DIR/sozlesme.pdf"
kontrol 201 "PDF yuklendi, adi korunur" '"ad":"sozlesme.pdf".*"yukleyen":"[^"]+"'; D1=$(alan id)
yukle "$K" "$T" "$DIR/gorsel.png"
kontrol 201 "PNG yuklendi"
yukle "$K" "$T" "$DIR/efatura.xml"
kontrol 201 "XML yuklendi"
yukle "$K" "$T" "$DIR/sozlesme.pdf"
kontrol 409 "Ayni dosya ayni kayda ikinci kez eklenmez" dosya_zaten_var
yukle "$K" "$T" "$DIR/kotu.pdf"
kontrol 400 "Uzantisi PDF, icerigi baska dosya reddedilir" dosya_turu
yukle "$K" "$T" "$DIR/kotu.exe"
kontrol 400 "exe reddedilir" dosya_turu
yukle "$K" "$T" "$DIR/kotu.html"
kontrol 400 "html reddedilir" dosya_turu
yukle "$K" "$T" "$DIR/bos.pdf"
kontrol 400 "Bos dosya reddedilir" dosya_bos
yukle "$K" "$T" "$DIR/buyuk.pdf"
kontrol 400 "20 MB uzeri reddedilir" dosya_cok_buyuk
yukle "$D/kayit/uydurma/$C1" "$T" "$DIR/sozlesme.pdf"
kontrol 404 "Tanimsiz kayit turu" kayit_bulunamadi
yukle "$D/kayit/cari/00000000-0000-7000-8000-000000000001" "$T" "$DIR/sozlesme.pdf"
kontrol 404 "Olmayan kayda dosya eklenmez" kayit_bulunamadi

echo "--- Listeleme ve indirme"
istek GET "$K" "$T"
kontrol 200 "Kaydin dosyalari listelenir" '"ad":"efatura.xml".*"ad":"gorsel.png".*"ad":"sozlesme.pdf"'
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/inen" -D "$DIR/baslik" -w "%{http_code}" "$KOK$D/$D1/indir" -H "Authorization: Bearer $T"); GOVDE=$(cat "$DIR/baslik")
kontrol 200 "Indirme ek olarak verilir" 'attachment; filename=sozlesme.pdf'
kontrol 200 "Tur tahmini kapali" 'nosniff'
if cmp -s "$DIR/inen" "$DIR/sozlesme.pdf"; then GOVDE=ayni; else GOVDE=farkli; fi
kontrol 200 "Inen dosya yuklenenle ayni" ayni
istek GET $D/kullanim "$T"
kontrol 200 "Kullanim ve deneme kotasi" '"kotaBayt":524288000'
KULLANIM=$(alan kullanilanBayt)

echo "--- Izolasyon"
istek GET "$K" "$T2"
kontrol 404 "Baska firma kaydin dosyalarini goremez" kayit_bulunamadi
yukle "$K" "$T2" "$DIR/sozlesme.pdf"
kontrol 404 "Baska firma kayda dosya ekleyemez" kayit_bulunamadi
KOD=$(curl "${CURL_EK[@]}" -s -o /dev/null -w "%{http_code}" "$KOK$D/$D1/indir" -H "Authorization: Bearer $T2"); GOVDE=""
kontrol 404 "Baska firma dosyayi indiremez"
istek DELETE "$D/$D1" "$T2"
kontrol 404 "Baska firma dosyayi silemez" dosya_bulunamadi

echo "--- Silme ve geri alma"
istek DELETE "$D/$D1" "$T"
kontrol 204 "Dosya cop kutusuna gitti"
istek GET "$K" "$T"
if echo "$GOVDE" | grep -q sozlesme; then KOD=listede; fi
kontrol 200 "Silinen dosya listede yok"
istek GET "$K?silinenler=true" "$T"
kontrol 200 "Cop kutusunda gorunur" '"ad":"sozlesme.pdf".*"silinmeTarihi":"'
istek GET $D/kullanim "$T"
if [ "$(alan kullanilanBayt)" -ge "$KULLANIM" ]; then KOD=dusmedi; fi
kontrol 200 "Silinen dosya kotadan duser"
KOD=$(curl "${CURL_EK[@]}" -s -o /dev/null -w "%{http_code}" "$KOK$D/$D1/indir" -H "Authorization: Bearer $T"); GOVDE=""
kontrol 404 "Silinen dosya indirilemez"
istek POST "$D/$D1/geri-al" "$T"
kontrol 200 "Geri alindi" '"silinmeTarihi":null'
istek GET "$K" "$T"
kontrol 200 "Geri alinan listede" sozlesme

echo "--- Yetki"
istek POST $Y/roller "$T" '{"ad":"Goren","yetkiler":["cari.hesap.goruntule","dosya.ek.goruntule"]}'
davet_kabul "$T" '{"ad":"Goren","soyad":"Kisi","eposta":"goren'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Goren"}'
GT=$(alan accessToken)
istek GET "$K" "$GT"
kontrol 200 "Goruntuleme yetkilisi listeler" sozlesme
yukle "$K" "$GT" "$DIR/kotu.pdf"
kontrol 403 "Ekleme yetkisi yoksa yukleyemez" yetki_yok
istek DELETE "$D/$D1" "$GT"
kontrol 403 "Silme yetkisi yoksa silemez" yetki_yok
istek POST $Y/roller "$T" '{"ad":"Yalniz Dosya","yetkiler":["dosya.ek.goruntule","dosya.ek.ekle"]}'
davet_kabul "$T" '{"ad":"Dosya","soyad":"Kisi","eposta":"yalnizdosya'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Yalniz"}'
YT=$(alan accessToken)
istek GET "$K" "$YT"
kontrol 403 "Kaydi gorme yetkisi olmayan dosyalarini goremez" yetki_yok
KOD=$(curl "${CURL_EK[@]}" -s -o /dev/null -w "%{http_code}" "$KOK$D/$D1/indir" -H "Authorization: Bearer $YT"); GOVDE=""
kontrol 404 "Kaydi gorme yetkisi olmayan indiremez"

echo "--- Depolama kotasi"
# Lisans bilgisi firma basina onbellekte: yeni firma degisen kotayla baslar.
sql "UPDATE yonetim.paket SET depolama_kotasi_mb = 1 WHERE kod = 'deneme'" > /dev/null
trap "sql \"UPDATE yonetim.paket SET depolama_kotasi_mb = 500 WHERE kod = 'deneme'\" > /dev/null" EXIT
kayit_ol '{"firmaUnvani":"Kota Test AS","ad":"K","soyad":"T","eposta":"dosyakota'"$U"'@ornek.com","sifre":"Sifre1234"}'
KT=$(alan accessToken)
istek POST /api/cari/hesaplar "$KT" '{"unvan":"Kota Cari","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; KC=$(alan id)
{ printf '%%PDF-1.4'; head -c $((700*1024)) /dev/zero; } > "$DIR/k1.pdf"
{ printf '%%PDF-1.5'; head -c $((700*1024)) /dev/zero; } > "$DIR/k2.pdf"
yukle "$D/kayit/cari/$KC" "$KT" "$DIR/k1.pdf"
kontrol 201 "Kota icindeki dosya yuklendi"; KD=$(alan id)
yukle "$D/kayit/cari/$KC" "$KT" "$DIR/k2.pdf"
kontrol 422 "Kotayi asan dosya reddedilir" depolama_kotasi
istek DELETE "$D/$KD" "$KT"
yukle "$D/kayit/cari/$KC" "$KT" "$DIR/k2.pdf"
kontrol 201 "Silince yer acilir"
istek POST "$D/$KD/geri-al" "$KT"
kontrol 422 "Kota doluysa geri alinamaz" depolama_kotasi

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
