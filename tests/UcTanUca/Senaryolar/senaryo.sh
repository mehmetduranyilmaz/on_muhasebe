#!/usr/bin/env bash
# Yonetim modülü uçtan uca senaryosu. Kullanım: senaryo.sh <api-adresi>
API="${1:-${OM_API:-http://localhost:5128}}/api/yonetim"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

# istek METOT YOL [TOKEN] [JAR] [GOVDE] -> $KOD, $GOVDE
istek() {
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$API$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(-b "$4" -c "$4")
  [ -n "$5" ] && args+=(-d "$5")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"[^\"]*\"" | head -1 | cut -d'"' -f4; }
kontrol() { # beklenen-kod aciklama [govdede-aranan]
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -q "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> $GOVDE"; KALAN=$((KALAN+1))
  fi
}

U=$(date +%s)
ALI="ali$U@ornek.com"; VELI="veli$U@ornek.com"; AYSE="ayse$U@ornek.com"

echo "--- Kayit ve giris"
istek POST /kayit "" "" '{"firmaUnvani":"A Ticaret","vergiNo":"1234567890","ad":"Ali","soyad":"Yilmaz","eposta":"'"$ALI"'","sifre":"Sifre1234"}'
kontrol 202 "Kayit alindi, oturum acilmadi (e-posta dogrulanacak)" "$ALI"
istek POST /giris "" "" '{"eposta":"'"$ALI"'","sifre":"Sifre1234"}'
kontrol 401 "Dogrulanmadan giris yok" eposta_dogrulanmadi
istek POST /eposta-dogrula "" "$DIR/ali.jar" '{"kod":"uydurma"}'
kontrol 422 "Uydurma dogrulama kodu reddedildi" dogrulama_kodu_gecersiz
istek POST /eposta-dogrula "" "$DIR/ali.jar" '{"kod":"'"$(mailpit_kodu "$ALI" eposta-dogrula)"'"}'
kontrol 200 "E-postadaki baglantiyla dogrulandi: firma + yonetici + deneme lisansi, oturum acildi" accessToken
ALI_TOKEN=$(alan accessToken)
grep -q om_yenile "$DIR/ali.jar" && echo "GECTI  Refresh token HttpOnly cerezde" && GECEN=$((GECEN+1)) || { echo "KALDI  Cerez yok"; KALAN=$((KALAN+1)); }

istek GET /ben "$ALI_TOKEN"
kontrol 200 "Ben: aktif firma A, deneme lisansi tum moduller" '"moduller":\["cari","stok","fatura","kasabanka"\]'
FIRMA_A=$(echo "$GOVDE" | grep -o '"aktifFirma":{"id":"[^"]*"' | cut -d'"' -f6)

istek POST /kayit "" "" '{"firmaUnvani":"X","ad":"A","soyad":"B","eposta":"'"$ALI"'","sifre":"Baska1234"}'
kontrol 202 "Ayni e-postayla ikinci kayit: yanit hesabin varligini ele vermez"
istek POST /giris "" "" '{"eposta":"'"$ALI"'","sifre":"Baska1234"}'
kontrol 401 "Ikinci kayit mevcut hesabin sifresini degistirmedi" giris_basarisiz
sleep 1
curl -s "$MAILPIT/search?query=to:$ALI" | grep -q "zaten hesab" \
  && { echo "GECTI  Hesap sahibine 'zaten hesabiniz var' e-postasi gitti"; GECEN=$((GECEN+1)); } \
  || { echo "KALDI  'zaten hesabiniz var' e-postasi yok"; KALAN=$((KALAN+1)); }

istek POST /kayit "" "" '{"firmaUnvani":"","ad":"A","soyad":"B","eposta":"bozuk","sifre":"kisa"}'
kontrol 400 "Dogrulama hatalari Turkce" "bo"

istek GET /ben ""
kontrol 401 "Tokensiz istek reddedildi"

echo "--- Refresh token rotasyonu"
cp "$DIR/ali.jar" "$DIR/ali_eski.jar"
istek POST /yenile "" "$DIR/ali.jar"
kontrol 200 "Yenileme yeni access token verdi" accessToken
istek POST /yenile "" "$DIR/ali_eski.jar"
kontrol 401 "Eski token tolerans icinde: reddedildi ama aile iptal edilmedi" oturum_gecersiz
istek POST /yenile "" "$DIR/ali.jar"
kontrol 200 "Guncel token hala gecerli"
cp "$DIR/ali.jar" "$DIR/ali_ara.jar"
istek POST /yenile "" "$DIR/ali.jar"
echo "      (31 sn bekleniyor: yeniden kullanim toleransi asilacak)"; sleep 31
istek POST /yenile "" "$DIR/ali_ara.jar"
kontrol 401 "Iptal edilmis token tekrar kullanildi" oturum_gecersiz
istek POST /yenile "" "$DIR/ali.jar"
kontrol 401 "Calinti suphesi: kullanicinin tum oturumlari kapatildi" oturum_gecersiz

istek POST /giris "" "$DIR/ali.jar" '{"eposta":"'"$ALI"'","sifre":"Sifre1234"}'
kontrol 200 "Tekrar giris" accessToken
ALI_TOKEN=$(alan accessToken)

echo "--- Sistem yoneticisi ve coklu firma"
istek POST /giris "" "$DIR/sys.jar" "$SISTEM_GIRIS"
kontrol 200 "Sistem yoneticisi girisi (firmasiz)" accessToken
SYS_TOKEN=$(alan accessToken)

istek POST /sistem/firmalar "$ALI_TOKEN" "" '{"firmaUnvani":"B","yoneticiEposta":"x@y.com","paketKodu":"basic","lisansBitisTarihi":"2027-12-31"}'
kontrol 403 "Normal kullanici sistem endpoint'ine erisemez"

istek POST /sistem/firmalar "$SYS_TOKEN" "" '{"firmaUnvani":"B Insaat","vergiNo":"9876543217","yoneticiEposta":"'"$ALI"'","paketKodu":"basic","lisansBitisTarihi":"2027-12-31"}'
kontrol 201 "Sistem yoneticisi firma B'yi acti, Ali'ye yonetici daveti gitti" firmaId
FIRMA_B=$(alan firmaId)

istek GET /ben "$ALI_TOKEN"
if echo "$GOVDE" | grep -q "B Insaat"; then KOD=999; fi
kontrol 200 "Davet kabul edilmeden Ali firma B'ye bagli degil"

DAVET_KODU=$(mailpit_kodu "$ALI" davet)
istek POST /davet-bilgi "" "" '{"kod":"'"$DAVET_KODU"'"}'
kontrol 200 "Davet bilgisi: firma B, yonetici, mevcut hesap" '"firmaUnvani":"B Insaat".*"rol":"Yonetici","hesapVarMi":true'
istek POST /davet-kabul "" "" '{"kod":"'"$DAVET_KODU"'","sifre":"Yanlis123"}'
kontrol 401 "Yanlis sifreyle davet kabul edilmez" giris_basarisiz
istek POST /davet-kabul "" "" '{"kod":"'"$DAVET_KODU"'","sifre":"Sifre1234"}'
kontrol 200 "Ali daveti kendi sifresiyle kabul etti" accessToken
istek POST /davet-kabul "" "" '{"kod":"'"$DAVET_KODU"'","sifre":"Sifre1234"}'
kontrol 422 "Kabul edilen davet ikinci kez kullanilamaz" davet_gecersiz

istek GET /ben "$ALI_TOKEN"
kontrol 200 "Ali iki firmayi goruyor" "B Insaat"

istek POST /firma-degistir "$ALI_TOKEN" "$DIR/ali.jar" '{"firmaId":"'"$FIRMA_B"'"}'
kontrol 200 "Firma B'ye gecis" accessToken
ALI_B_TOKEN=$(alan accessToken)
istek GET /ben "$ALI_B_TOKEN"
kontrol 200 "Aktif firma B, basic lisans" '"aktifFirma":{"id":"'"$FIRMA_B"'"'

istek POST /firma-degistir "$ALI_TOKEN" "" '{"firmaId":"'"$(cat /proc/sys/kernel/random/uuid 2>/dev/null || echo 00000000-0000-0000-0000-000000000001)"'"}'
kontrol 403 "Uyesi olmadigi firmaya gecis reddedildi" firma_uyesi_degil

echo "--- Kullanici yonetimi ve firma izolasyonu"
davet_kabul "$ALI_TOKEN" '{"ad":"Veli","soyad":"Kaya","eposta":"'"$VELI"'","sifre":"Sifre1234","rol":"Kullanici"}'
kontrol 200 "Veli davetle firma A'ya katildi (kendi sifresini belirledi)" accessToken

istek POST /kullanicilar "$ALI_TOKEN" "" '{"eposta":"'"$AYSE"'","rol":"Kullanici"}'
kontrol 201 "Ayse'ye davet gitti" '"eposta":"'"$AYSE"'","rol":"Kullanici"'
if echo "$GOVDE" | grep -q '"ad"'; then KOD=999; fi
kontrol 201 "Davet yaniti kisisel bilgi (ad/soyad) icermez"
istek GET /kullanicilar/davetler "$ALI_TOKEN"
kontrol 200 "Bekleyen davetlerde Ayse var" "$AYSE"

istek POST /kullanicilar "$ALI_TOKEN" "" '{"eposta":"'"$VELI"'","rol":"Patron"}'
kontrol 400 "Gecersiz rol reddedildi"
istek POST /kullanicilar "$ALI_TOKEN" "" '{"eposta":"'"$VELI"'","rol":"Kullanici"}'
kontrol 409 "Zaten uye olan kisiye davet gitmez" zaten_firma_uyesi

istek GET /kullanicilar "$ALI_TOKEN"
kontrol 200 "Firma A listesi Veli'yi iceriyor" "$VELI"
istek GET /kullanicilar "$ALI_B_TOKEN"
if echo "$GOVDE" | grep -q "$VELI"; then KOD=999; fi
kontrol 200 "Firma B listesi Veli'yi ICERMIYOR (izolasyon)"

istek POST /giris "" "$DIR/veli.jar" '{"eposta":"'"$VELI"'","sifre":"Sifre1234"}'
VELI_TOKEN=$(alan accessToken)
istek GET /kullanicilar "$VELI_TOKEN"
kontrol 403 "Kullanici rolu kullanici yonetemez"

istek GET /kullanicilar "$ALI_TOKEN"; VELI_ID=$(echo "$GOVDE" | grep -o "\"kullaniciId\":\"[^\"]*\",\"ad\":\"Veli\"" | cut -d\" -f4)
istek POST "/kullanicilar/$VELI_ID/pasiflestir" "$ALI_TOKEN"
kontrol 204 "Veli pasiflestirildi"
istek POST /yenile "" "$DIR/veli.jar"
kontrol 401 "Pasif Veli'nin oturumu yenilenemiyor"

ALI_ID=$(echo "$ALI_TOKEN" | cut -d. -f2 | tr '_-' '/+' | base64 -d 2>/dev/null | grep -o '"sub":"[^"]*"' | cut -d'"' -f4)
istek POST "/kullanicilar/$ALI_ID/pasiflestir" "$ALI_TOKEN"
kontrol 422 "Yonetici kendini pasiflestiremez" kendini_pasiflestiremez

echo "--- Lisans"
istek PUT "/sistem/firmalar/$FIRMA_A/lisans" "$SYS_TOKEN" "" '{"paketKodu":"basic","bitisTarihi":"2020-01-01","ekModuller":[]}'
kontrol 204 "Firma A lisansi gecmis tarihli yapildi"
istek GET /ben "$ALI_TOKEN"
kontrol 200 "Onbellek temizlendi: lisans salt okunur" '"saltOkunur":true'

istek PUT "/sistem/firmalar/$FIRMA_A/lisans" "$SYS_TOKEN" "" '{"paketKodu":"basic","bitisTarihi":"2027-12-31","ekModuller":["muhasebe"]}'
kontrol 400 "Gecersiz ek modul reddedildi"

echo "--- Cikis ve hesap kilidi"
istek POST /cikis "" "$DIR/ali.jar"
kontrol 204 "Cikis"
istek POST /yenile "" "$DIR/ali.jar"
kontrol 401 "Cikistan sonra yenileme reddedildi"

# Kimlik uç noktaları IP başına dakikada 10 istekle sınırlı; testin ilk yarısı 6 istek kullanır.
# Kilitleme bölümü (7 istek) yeni bir dakikalık pencerede çalışsın diye beklenir.
echo "  (hız sınırı penceresi için 61 sn bekleniyor)"; sleep 61
for i in 1 2 3 4 5; do istek POST /giris "" "" '{"eposta":"'"$ALI"'","sifre":"YanlisSifre9"}'; done
kontrol 401 "5. hatali giris" giris_basarisiz
istek POST /giris "" "" '{"eposta":"'"$ALI"'","sifre":"Sifre1234"}'
kontrol 401 "Dogru sifreyle bile hesap kilitli" hesap_kilitli

istek POST /giris "" "" '{"eposta":"yok'"$U"'@ornek.com","sifre":"Sifre1234"}'
kontrol 401 "Olmayan e-posta ayni mesaji aliyor" giris_basarisiz

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
