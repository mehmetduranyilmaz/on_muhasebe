#!/usr/bin/env bash
# Evrak Takibi modulu uctan uca senaryosu. Kullanim: evraktakip_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_evraktakip"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim
E=/api/evrak-takibi

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
gun() { date -u -d "$1 days" +%Y-%m-%d; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Evrak Test AS","ad":"Ece","soyad":"Yon","eposta":"evraktakip'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Evrak AS","ad":"D","soyad":"F","eposta":"evrakdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)

echo "--- Kayit"
istek POST $E "$T" '{"ad":"","tur":"Ruhsat","bitisTarihi":"'"$(gun 100)"'","hatirlatmaGun":30}'
kontrol 400 "Adsiz evrak reddedilir"
istek POST $E "$T" '{"ad":"X","tur":"Ruhsat","bitisTarihi":"'"$(gun 100)"'","hatirlatmaGun":0}'
kontrol 400 "Hatirlatma gunu en az 1" Hat
istek POST $E "$T" '{"ad":"X","tur":"Ruhsat","baslangicTarihi":"'"$(gun 10)"'","bitisTarihi":"'"$(gun 5)"'","hatirlatmaGun":30}'
kontrol 400 "Bitis baslangictan once olamaz" bitis_tarihi
istek POST $E "$T" '{"ad":"X","tur":"Ruhsat","bitisTarihi":"'"$(gun 5)"'","hatirlatmaGun":30,"sorumluKullaniciId":"00000000-0000-7000-8000-000000000001"}'
kontrol 400 "Firmada olmayan sorumlu reddedilir" sorumlu_gecersiz
istek GET $E/sorumlular "$T"
kontrol 200 "Sorumlu listesi" '"adSoyad":"Ece Yon"'; SORUMLU=$(alan id)
istek POST $E "$T" '{"ad":"Isyeri Ruhsati","tur":"Ruhsat","belgeNo":"R-1","kurum":"Belediye","bitisTarihi":"'"$(gun 200)"'","hatirlatmaGun":30,"sorumluKullaniciId":"'"$SORUMLU"'"}'
kontrol 201 "Gecerli evrak" '"durum":"Gecerli"'; GECERLI=$(alan id); SURUM=$(alan surum)
istek POST $E "$T" '{"ad":"Trafik Sigortasi","tur":"Sigorta","belgeNo":"P-1","bitisTarihi":"'"$(gun 20)"'","hatirlatmaGun":30}'
kontrol 201 "Suresi yaklasan evrak" '"durum":"Yaklasiyor"'; YAKLASAN=$(alan id)
istek POST $E "$T" '{"ad":"Eski Sozlesme","tur":"Sozlesme","bitisTarihi":"'"$(gun -5)"'","hatirlatmaGun":30}'
kontrol 201 "Suresi dolan evrak" '"durum":"SuresiDoldu"'; DOLAN=$(alan id)

echo "--- Liste"
istek GET $E "$T"
kontrol 200 "Bitise gore sirali, kalan gun ve sorumlu" '"ad":"Eski Sozlesme".*"kalanGun":-5.*"ad":"Trafik Sigortasi".*"ad":"Isyeri Ruhsati".*"sorumluAdi":"Ece Yon"'
istek GET "$E?durum=Yaklasiyor" "$T"
kontrol 200 "Yaklasan filtresi" '"toplamKayit":1'
istek GET "$E?durum=SuresiDoldu" "$T"
kontrol 200 "Dolan filtresi" '"ad":"Eski Sozlesme"'
istek GET "$E?arama=belediye" "$T"
kontrol 200 "Kurumla arama" '"toplamKayit":1'
istek GET $E/sayilar "$T"
kontrol 200 "Durum sayilari" '"gecerli":1,"yaklasiyor":1,"suresiDoldu":1,"pasif":0'

echo "--- Guncelleme ve yenileme"
istek PUT "$E/$GECERLI" "$T" '{"ad":"Isyeri Acma Ruhsati","tur":"Ruhsat","belgeNo":"R-1","kurum":"Belediye","bitisTarihi":"'"$(gun 200)"'","hatirlatmaGun":45,"surum":'"$SURUM"'}'
kontrol 200 "Guncellendi" '"ad":"Isyeri Acma Ruhsati".*"hatirlatmaGun":45'
istek PUT "$E/$GECERLI" "$T" '{"ad":"Eski Surum","tur":"Ruhsat","bitisTarihi":"'"$(gun 200)"'","hatirlatmaGun":45,"surum":'"$SURUM"'}'
kontrol 409 "Eski surumle guncelleme reddedilir"
istek POST "$E/$YAKLASAN/yenile" "$T" '{"yeniBitisTarihi":"'"$(gun 10)"'"}'
kontrol 422 "Yeni bitis eskisinden sonra olmali" yenileme_tarihi
istek POST "$E/$YAKLASAN/yenile" "$T" '{"yeniBitisTarihi":"'"$(gun 385)"'","yeniBelgeNo":"P-2","not":"Police yenilendi"}'
kontrol 200 "Yenilendi, durum gecerli, belge no degisti" '"belgeNo":"P-2".*"durum":"Gecerli".*"yenilemeler":\[\{"eskiBitisTarihi":"'"$(gun 20)"'","yeniBitisTarihi":"'"$(gun 385)"'","eskiBelgeNo":"P-1","not":"Police yenilendi","yapan":"Ece Yon"'

echo "--- Pasif ve silme"
istek POST "$E/$DOLAN/pasiflestir" "$T" '{}'
kontrol 200 "Pasiflestirildi" '"durum":"Pasif"'
istek POST "$E/$DOLAN/yenile" "$T" '{"yeniBitisTarihi":"'"$(gun 400)"'"}'
kontrol 422 "Pasif evrak yenilenemez" pasif_evrak
istek GET $E "$T"
if echo "$GOVDE" | grep -q "Eski Sozlesme"; then KOD=listede; fi
kontrol 200 "Pasif, aktif listesinde yok"
istek GET "$E?durum=Pasif" "$T"
kontrol 200 "Pasif filtresinde gorunur" "Eski Sozlesme"
istek POST "$E/$DOLAN/aktiflestir" "$T" '{}'
kontrol 200 "Aktiflestirildi" '"durum":"SuresiDoldu"'

echo "--- Dosya eki"
printf '%%PDF-1.4 ruhsat' > "$DIR/ruhsat.pdf"
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/dosya/kayit/evrak-takibi/$GECERLI" -H "Authorization: Bearer $T" -F "dosya=@$DIR/ruhsat.pdf"); GOVDE=$(cat "$DIR/govde")
kontrol 201 "Evraga dosya eklendi" '"ad":"ruhsat.pdf"'
istek GET "/api/dosya/kayit/evrak-takibi/$GECERLI" "$T"
kontrol 200 "Evrakin dosyalari listelenir" ruhsat.pdf

echo "--- Izolasyon ve yetki"
istek GET "$E/$GECERLI" "$T2"
kontrol 404 "Baska firma evragi goremez" takip_evraki_bulunamadi
istek DELETE "$E/$GECERLI" "$T2"
kontrol 404 "Baska firma evragi silemez" takip_evraki_bulunamadi
istek GET /api/dosya/kayit/evrak-takibi/$GECERLI "$T2"
kontrol 404 "Baska firma dosyalara erisemez" kayit_bulunamadi
istek POST $Y/roller "$T" '{"ad":"Evrak Goren","yetkiler":["evraktakip.kayit.goruntule"]}'
davet_kabul "$T" '{"ad":"Goren","soyad":"Kisi","eposta":"evrakgoren'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Evrak Goren"}'
GT=$(alan accessToken)
istek GET $E "$GT"
kontrol 200 "Goruntuleme yetkilisi listeler"
istek POST $E "$GT" '{"ad":"Yasak","tur":"Ruhsat","bitisTarihi":"'"$(gun 100)"'","hatirlatmaGun":30}'
kontrol 403 "Ekleme yetkisi yoksa eklenemez" yetki_yok

echo "--- Hatirlatma"
# Sorumlu, bildirim tercihi kapali olsa da (yonetici olmayanda varsayilan kapali) hatirlatmayi alir.
istek GET $E/sorumlular "$T"
GS=$(echo "$GOVDE" | tr -d '\n' | sed 's/},{/}\n{/g' | grep '"adSoyad":"Goren Kisi"' | grep -o '"id":"[^"]*"' | cut -d'"' -f4)
istek POST $E "$T" '{"ad":"Yaklasan Sertifika","tur":"Sertifika","bitisTarihi":"'"$(gun 5)"'","hatirlatmaGun":30,"sorumluKullaniciId":"'"$GS"'"}'
kontrol 201 "Sorumlusu olan yaklasan evrak"
istek POST /api/yonetim/giris "" "$SISTEM_GIRIS"; SYS=$(alan accessToken)
if [ -n "$SYS" ]; then
  istek POST /api/bildirim/sistem/calistir "$SYS" '{}'
  kontrol 200 "Bildirimler uretildi"
  istek GET "/api/bildirim?boyut=50" "$T"
  kontrol 200 "Yonetici suresi dolani gorur" 'doldu: Eski Sozlesme'
  istek GET "/api/bildirim?boyut=50" "$GT"
  kontrol 200 "Sorumlu kendi evragini tercihten bagimsiz alir" 'gün kaldı: Yaklasan Sertifika'
else
  echo "ATLANDI  bildirim uretimi: sistem yoneticisi girisi yapilamadi"
fi

echo "--- Silme"
istek DELETE "$E/$DOLAN" "$T"
kontrol 204 "Evrak silindi"
istek GET "$E/$DOLAN" "$T"
kontrol 404 "Silinen evrak bulunamaz" takip_evraki_bulunamadi

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
