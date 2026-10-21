#!/usr/bin/env bash
# Hesap ele geçirmeye karşı: e-posta doğrulama, davetle katılma, başkasının e-postasıyla açılmış hesap.
# Kullanım: davet_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_davet"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
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
giris() { istek POST $Y/giris "" '{"eposta":"'"$1"'","sifre":"'"$2"'"}'; }
pencere() { [ "${OM_HIZ_BEKLE:-65}" -gt 0 ] && { echo "  (hız sınırı penceresi için 61 sn bekleniyor)"; sleep 61; }; return 0; }

U=$(date +%s)
KURBAN="kurban$U@ornek.com"; YONETICI="davetyon$U@ornek.com"; MEVCUT="mevcut$U@ornek.com"

echo "--- Saldirgan kurbanin e-postasiyla onceden kayit acar"
istek POST $Y/kayit "" '{"firmaUnvani":"Saldirgan Ltd","ad":"Sahte","soyad":"Ad","eposta":"'"$KURBAN"'","sifre":"Saldirgan1"}'
kontrol 202 "Kayit alindi (dogrulama bekliyor)"
giris "$KURBAN" Saldirgan1
kontrol 401 "Saldirgan dogrulanmamis hesapla giris yapamaz" eposta_dogrulanmadi
giris "$KURBAN" Yanlis1234
kontrol 401 "Yanlis sifrede dogrulama durumu ele verilmez" giris_basarisiz

echo "--- Firma yoneticisi kurbani davet eder"
kayit_ol '{"firmaUnvani":"Hedef Firma","ad":"Yasar","soyad":"Yon","eposta":"'"$YONETICI"'","sifre":"Sifre1234"}'
kontrol 200 "Hedef firma yoneticisi kayit + dogrulama" accessToken; T=$(alan accessToken)
istek POST $Y/kullanicilar "$T" '{"eposta":"'"$KURBAN"'","rol":"Yonetici"}'
kontrol 201 "Kurbana davet gitti"
istek GET $Y/kullanicilar "$T"
if echo "$GOVDE" | grep -q "$KURBAN"; then KOD=999; fi
kontrol 200 "Davet kabul edilmeden kimse firmaya uye olmaz"

echo "--- Saldirgan davet bilgisine erisemez, kurban bağlantiyla hesabi devralir"
istek POST $Y/davet-kabul "" '{"kod":"tahmin-edilen-kod","sifre":"Saldirgan1"}'
kontrol 422 "Kod olmadan davet kabul edilemez" davet_gecersiz
pencere
DKOD=$(mailpit_kodu "$KURBAN" davet)
istek POST $Y/davet-bilgi "" '{"kod":"'"$DKOD"'"}'
kontrol 200 "Dogrulanmamis hesap 'hesap yok' sayilir: kurban kendi hesabini kurar" '"firmaUnvani":"Hedef Firma".*"hesapVarMi":false'
istek POST $Y/davet-kabul "" '{"kod":"'"$DKOD"'","ad":"Gercek","soyad":"Kurban","sifre":"kisa"}'
kontrol 400 "Yeni hesapta zayif sifre reddedildi"
istek POST $Y/davet-kabul "" '{"kod":"'"$DKOD"'","sifre":"Gercek1234"}'
kontrol 400 "Yeni hesapta ad/soyad zorunlu"
istek POST $Y/davet-kabul "" '{"kod":"'"$DKOD"'","ad":"Gercek","soyad":"Kurban","sifre":"Gercek1234"}'
kontrol 200 "Kurban daveti kabul etti, hesap onun oldu" accessToken
giris "$KURBAN" Saldirgan1
kontrol 401 "Saldirganin sifresi artik gecersiz" giris_basarisiz
giris "$KURBAN" Gercek1234
kontrol 200 "Kurban kendi sifresiyle giriyor" accessToken; TK=$(alan accessToken)
istek GET $Y/ben "$TK"
kontrol 200 "Kurban hedef firmada, adi kendi verdigi ad" '"ad":"Gercek","soyad":"Kurban".*Hedef Firma'

echo "--- Var olan (dogrulanmis) hesap davetle katilir"
pencere
kayit_ol '{"firmaUnvani":"Mevcut Ltd","ad":"Mehmet","soyad":"Var","eposta":"'"$MEVCUT"'","sifre":"Mevcut1234"}'
kontrol 200 "Mevcut kullanici kendi firmasini acti" accessToken; TM=$(alan accessToken)
istek POST $Y/kullanicilar "$T" '{"eposta":"'"$MEVCUT"'","rol":"Kullanici"}'
kontrol 201 "Mevcut kullaniciya davet"
if echo "$GOVDE" | grep -q "Mehmet"; then KOD=999; fi
kontrol 201 "Davet yaniti baska firmanin kullanicisinin adini sizdirmaz"
DKOD=$(mailpit_kodu "$MEVCUT" davet)
istek POST $Y/davet-bilgi "" '{"kod":"'"$DKOD"'"}'
kontrol 200 "Mevcut hesap: yalnizca sifre istenir" '"hesapVarMi":true'
istek POST $Y/davet-kabul "" '{"kod":"'"$DKOD"'","ad":"Baska","soyad":"Ad","sifre":"Yanlis1234"}'
kontrol 401 "Mevcut hesapta yanlis sifreyle kabul yok" giris_basarisiz
istek POST $Y/davet-kabul "" '{"kod":"'"$DKOD"'","sifre":"Mevcut1234"}'
kontrol 200 "Mevcut hesap sifresiyle katildi" accessToken; TM2=$(alan accessToken)
istek GET $Y/ben "$TM2"
kontrol 200 "Aktif firma davet edilen firma, ad degismedi, iki firma" '"ad":"Mehmet".*"aktifFirma":\{"id":"[^"]*","unvan":"Hedef Firma".*Mevcut Ltd'

echo "--- Davet yonetimi"
pencere
istek POST $Y/kullanicilar "$T" '{"eposta":"iptal'"$U"'@ornek.com","rol":"Kullanici"}'
DAVET=$(alan id)
IKOD=$(mailpit_kodu "iptal$U@ornek.com" davet)
istek POST "$Y/kullanicilar/davetler/$DAVET/yeniden-gonder" "$T"
kontrol 422 "Iki dakika dolmadan yeniden gonderilmez" davet_cok_sik
istek DELETE "$Y/kullanicilar/davetler/$DAVET" "$TM2"
kontrol 403 "Kullanici rolu daveti iptal edemez"
kayit_ol '{"firmaUnvani":"Ucuncu Ltd","ad":"U","soyad":"F","eposta":"ucuncu'"$U"'@ornek.com","sifre":"Sifre1234"}'; T3=$(alan accessToken)
istek DELETE "$Y/kullanicilar/davetler/$DAVET" "$T3"
kontrol 404 "Baska firmanin yoneticisi daveti iptal edemez" davet_bulunamadi
istek DELETE "$Y/kullanicilar/davetler/$DAVET" "$T"
kontrol 204 "Davet iptal edildi"
istek POST $Y/davet-bilgi "" '{"kod":"'"$IKOD"'"}'
kontrol 422 "Iptal edilen davetin baglantisi gecersiz" davet_gecersiz

echo "--- Sistem yoneticisi bir firmaya davetle eklenemez"
istek POST $Y/kullanicilar "$T" '{"eposta":"'"$OM_SISTEM_EPOSTA"'","rol":"Yonetici"}'
SKOD=$(mailpit_kodu "$OM_SISTEM_EPOSTA" davet)
istek POST $Y/davet-bilgi "" '{"kod":"'"$SKOD"'"}'
kontrol 422 "Sistem yoneticisine giden davet kabul edilemez" davet_gecersiz

echo "--- Dogrulama baglantisini yeniden isteme"
pencere
YENI="yeni$U@ornek.com"
istek POST $Y/kayit "" '{"firmaUnvani":"Yeni Ltd","ad":"Y","soyad":"N","eposta":"'"$YENI"'","sifre":"Sifre1234"}'
istek POST $Y/dogrulama-tekrar "" '{"eposta":"'"$YENI"'"}'
kontrol 202 "Tekrar istegi alindi"
sleep 1
[ "$(curl -s "$MAILPIT/search?query=to:$YENI" | grep -o '"messages_count":[0-9]*' | cut -d: -f2)" = 1 ] \
  && { echo "GECTI  Iki dakika dolmadan ikinci dogrulama e-postasi gitmedi"; GECEN=$((GECEN+1)); } \
  || { echo "KALDI  Ikinci dogrulama e-postasi gitti"; KALAN=$((KALAN+1)); }
istek POST $Y/dogrulama-tekrar "" '{"eposta":"kimseyok'"$U"'@ornek.com"}'
kontrol 202 "Olmayan hesap icin de ayni yanit"
istek POST $Y/sifre-sifirlama-istegi "" '{"eposta":"'"$YENI"'"}'
SFKOD=$(mailpit_kodu "$YENI" sifre-sifirla)
istek POST $Y/sifre-sifirla "" '{"kod":"'"$SFKOD"'","yeniSifre":"Sifirla123"}'
kontrol 204 "Dogrulanmamis hesapta sifre sifirlama (e-posta sahipligi kanitlandi)"
giris "$YENI" Sifirla123
kontrol 200 "Sifirlama hesabi dogrulanmis yapti" accessToken

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
