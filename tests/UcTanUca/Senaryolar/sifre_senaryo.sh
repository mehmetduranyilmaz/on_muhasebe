#!/usr/bin/env bash
# Şifre değiştirme ve "şifremi unuttum" (Mailpit'ten bağlantı) uçtan uca senaryosu. Kullanım: sifre_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_sifre"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim

istek() { # METOT YOL [TOKEN] [JAR] [GOVDE]
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(-b "$4" -c "$4")
  [ -n "$5" ] && args+=(--data-binary "$5")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"[^\"]*\"" | head -1 | cut -d'"' -f4; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -qE -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:400}"; KALAN=$((KALAN+1))
  fi
}
giris() { echo '{"eposta":"'"$1"'","sifre":"'"$2"'"}'; }
# Giriş/kayıt/şifre uçları IP başına dakikada 10 istekle sınırlı; sınır yükseltilmemişse pencerenin dolması beklenir.
pencere() { [ "${OM_HIZ_BEKLE:-65}" -gt 0 ] && { echo "  (hız sınırı penceresi için 61 sn bekleniyor)"; sleep 61; }; return 0; }
# Mailpit'te alıcıya giden son e-postadaki sıfırlama kodu ve e-posta sayısı.
posta_sayisi() { curl -s "$MAILPIT/search?query=to:$1" | grep -o '"messages_count":[0-9]*' | cut -d: -f2; }
son_kod() {
  local id; id=$(curl -s "$MAILPIT/search?query=to:$1" | grep -o '"ID":"[^"]*"' | head -1 | cut -d'"' -f4)
  curl -s "$MAILPIT/message/$id" | grep -o 'sifre-sifirla?kod=[A-Za-z0-9_-]*' | head -1 | cut -d= -f2
}

U=$(date +%s)
EPOSTA="sifre$U@ornek.com"

echo "--- Şifre değiştirme"
istek POST $Y/kayit "" "$DIR/cihaz1.jar" '{"firmaUnvani":"Sifre Ltd","ad":"Seda","soyad":"Sifre","eposta":"'"$EPOSTA"'","sifre":"Sifre1234"}'
kontrol 201 "Kayit" accessToken; T=$(alan accessToken)
istek POST $Y/giris "" "$DIR/cihaz2.jar" "$(giris "$EPOSTA" Sifre1234)"
kontrol 200 "Ikinci cihazda giris" accessToken
istek POST $Y/giris "" "" "$SISTEM_GIRIS"; SYS=$(alan accessToken)
istek PUT /api/bildirim/sistem/eposta "$SYS" "" '{"aktifMi":true,"sunucu":"'"$SMTP_SUNUCU"'","port":'"$SMTP_PORT"',"guvenlik":"Yok","gonderenAdres":"bildirim@onmuhasebe.local","gonderenAd":"On Muhasebe","sifreKayitliMi":false}'
kontrol 200 "SMTP (Mailpit) ayarlandi"

istek POST $Y/sifre-degistir "" "" '{"mevcutSifre":"Sifre1234","yeniSifre":"Yeni12345"}'
kontrol 401 "Oturumsuz sifre degistirilemez"
istek POST $Y/sifre-degistir "$T" "$DIR/cihaz1.jar" '{"mevcutSifre":"Yanlis123","yeniSifre":"Yeni12345"}'
kontrol 400 "Yanlis mevcut sifre reddedildi" mevcut_sifre_hatali
istek POST $Y/sifre-degistir "$T" "$DIR/cihaz1.jar" '{"mevcutSifre":"Sifre1234","yeniSifre":"kisa"}'
kontrol 400 "Zayif yeni sifre reddedildi"
istek POST $Y/sifre-degistir "$T" "$DIR/cihaz1.jar" '{"mevcutSifre":"Sifre1234","yeniSifre":"Sifre1234"}'
kontrol 400 "Yeni sifre eskisiyle ayni olamaz"
istek POST $Y/sifre-degistir "$T" "$DIR/cihaz1.jar" '{"mevcutSifre":"Sifre1234","yeniSifre":"Yeni12345"}'
kontrol 200 "Sifre degisti, bu cihaza yeni token" accessToken

istek POST $Y/yenile "" "$DIR/cihaz2.jar"
kontrol 401 "Diger cihazin oturumu kapandi"
istek POST $Y/yenile "" "$DIR/cihaz1.jar"
kontrol 200 "Bu cihazin oturumu suruyor" accessToken
pencere
istek POST $Y/giris "" "" "$(giris "$EPOSTA" Sifre1234)"
kontrol 401 "Eski sifreyle giris yok"
istek POST $Y/giris "" "" "$(giris "$EPOSTA" Yeni12345)"
kontrol 200 "Yeni sifreyle giris" accessToken

echo "--- Sifremi unuttum"
istek POST $Y/sifre-sifirlama-istegi "" "" '{"eposta":"yok'"$U"'@ornek.com"}'
kontrol 202 "Kayitsiz e-posta: ayni yanit (hesap varligi sizmaz)"
istek POST $Y/sifre-sifirlama-istegi "" "" '{"eposta":"bozuk"}'
kontrol 400 "Gecersiz e-posta reddedildi"
istek POST $Y/sifre-sifirlama-istegi "" "" '{"eposta":"'"${EPOSTA^^}"'"}'
kontrol 202 "Kayitli e-posta (buyuk harfle) icin istek alindi"
sleep 1
KODU=$(son_kod "$EPOSTA")
[ ${#KODU} -ge 40 ] && { echo "GECTI  E-postada sifirlama baglantisi var"; GECEN=$((GECEN+1)); } || { echo "KALDI  E-postada baglanti yok"; KALAN=$((KALAN+1)); }
ID=$(curl -s "$MAILPIT/search?query=to:$EPOSTA" | grep -o '"ID":"[^"]*"' | head -1 | cut -d'"' -f4)
curl -s "$MAILPIT/message/$ID" | grep -q 'http://localhost:5167/sifre-sifirla?kod=' \
  && { echo "GECTI  Baglanti yapilandirilan uygulama adresinde"; GECEN=$((GECEN+1)); } || { echo "KALDI  Baglanti adresi yanlis"; KALAN=$((KALAN+1)); }
istek POST $Y/sifre-sifirlama-istegi "" "" '{"eposta":"'"$EPOSTA"'"}'
kontrol 202 "Hemen ikinci istek de 202"
sleep 1
[ "$(posta_sayisi "$EPOSTA")" = 1 ] && { echo "GECTI  2 dk dolmadan ikinci e-posta gitmedi"; GECEN=$((GECEN+1)); } || { echo "KALDI  Ikinci e-posta gitti"; KALAN=$((KALAN+1)); }

istek POST $Y/sifre-sifirla "" "" '{"kod":"uydurma-kod","yeniSifre":"Sifirla123"}'
kontrol 422 "Uydurma kod reddedildi" sifirlama_kodu_gecersiz
istek POST $Y/sifre-sifirla "" "" '{"kod":"'"$KODU"'","yeniSifre":"kisa"}'
kontrol 400 "Sifirlamada zayif sifre reddedildi"
pencere
istek POST $Y/sifre-sifirla "" "" '{"kod":"'"$KODU"'","yeniSifre":"Sifirla123"}'
kontrol 204 "Sifre sifirlandi"
istek POST $Y/sifre-sifirla "" "" '{"kod":"'"$KODU"'","yeniSifre":"Baska1234"}'
kontrol 422 "Ayni kod ikinci kez kullanilamaz" sifirlama_kodu_gecersiz
istek POST $Y/yenile "" "$DIR/cihaz1.jar"
kontrol 401 "Sifirlama tum oturumlari kapatti"
istek POST $Y/giris "" "" "$(giris "$EPOSTA" Yeni12345)"
kontrol 401 "Onceki sifre artik gecersiz"
istek POST $Y/giris "" "" "$(giris "$EPOSTA" Sifirla123)"
kontrol 200 "Sifirlanan sifreyle giris" accessToken

echo "--- Kilitli hesap sifirlamayla acilir"
pencere
KILIT="kilit$U@ornek.com"
istek POST $Y/kayit "" "" '{"firmaUnvani":"Kilit Ltd","ad":"Kaan","soyad":"Kilit","eposta":"'"$KILIT"'","sifre":"Sifre1234"}'
for i in 1 2 3 4 5; do istek POST $Y/giris "" "" "$(giris "$KILIT" Yanlis123)"; done
istek POST $Y/giris "" "" "$(giris "$KILIT" Sifre1234)"
kontrol 401 "5 hatali giristen sonra hesap kilitli" hesap_kilitli
istek POST $Y/sifre-sifirlama-istegi "" "" '{"eposta":"'"$KILIT"'"}'
sleep 1
istek POST $Y/sifre-sifirla "" "" '{"kod":"'"$(son_kod "$KILIT")"'","yeniSifre":"Acildi123"}'
kontrol 204 "Kilitli hesapta sifirlama"
istek POST $Y/giris "" "" "$(giris "$KILIT" Acildi123)"
kontrol 200 "Sifirlama kilidi kaldirdi" accessToken

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
