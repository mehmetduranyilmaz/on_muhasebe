#!/usr/bin/env bash
# Satin Alma modulu uctan uca senaryosu. Kullanim: satinalma_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_satinalma"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim
S=/api/satinalma

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
talep() { echo '{"tarih":"2026-10-15","gerekce":"'"$1"'","kalemler":[{"ad":"Kalem","birim":"ADET","miktar":1,"tahminiBirimFiyat":'"$2"',"kdvOrani":20}]}'; }
rolu_bul() { istek GET $Y/roller "$T"; echo "$GOVDE" | tr -d '\n' | sed 's/},{/}\n{/g' | grep "\"ad\":\"$1\"" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4; }
kisi_bul() { istek GET $S/onay-kurali "$T"; echo "$GOVDE" | tr -d '\n' | sed 's/},{/}\n{/g' | grep "\"ad\":\"$1\"" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Satinalma Test AS","ad":"Sena","soyad":"Patron","eposta":"satinalma'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Satinalma AS","ad":"D","soyad":"F","eposta":"satinalmadiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)

echo "--- Tek kullanicida kilitlenme yok"
istek POST $S/talepler "$T" "$(talep 'Ilk talep' 100)"
kontrol 201 "Talep taslak acildi" '"kod":"TL[0-9]+".*"durum":"Taslak","toplam":100'; ILK=$(alan id)
istek POST "$S/talepler/$ILK/gonder" "$T" '{}'
kontrol 422 "Baska onaylayan yoksa onaya gonderilmez" onaylayan_yok

echo "--- Roller ve kullanicilar"
istek POST $Y/roller "$T" '{"ad":"Talep Eden","yetkiler":["satinalma.talep.goruntule","satinalma.talep.ekle","satinalma.talep.duzenle"]}'
kontrol 201 "Talep eden rolu"
istek POST $Y/roller "$T" '{"ad":"Mudur","yetkiler":["satinalma.talep.goruntule","satinalma.onay.goruntule","satinalma.onay.onayla"]}'
kontrol 201 "Onaylayan mudur rolu"
istek POST $Y/roller "$T" '{"ad":"Onaysiz","yetkiler":["satinalma.talep.goruntule"]}'
kontrol 201 "Onay yetkisi olmayan rol"
davet_kabul "$T" '{"ad":"Tuna","soyad":"Talep","eposta":"sattalep'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Talep Eden"}'; TT=$(alan accessToken)
davet_kabul "$T" '{"ad":"Mert","soyad":"Mudur","eposta":"satmudur'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Mudur"}'; TM=$(alan accessToken)
davet_kabul "$T" '{"ad":"Ozan","soyad":"Onaysiz","eposta":"satonaysiz'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Onaysiz"}'; TO=$(alan accessToken)
MUDUR=$(rolu_bul Mudur); ONAYSIZ=$(rolu_bul Onaysiz); PATRON=$(kisi_bul 'Sena Patron')

echo "--- Onay kurali"
istek PUT $S/onay-kurali "$T" '{"basamaklar":[{"enAzTutar":0}]}'
kontrol 400 "Rol ve kisi birlikte bos olamaz" basamak_gecersiz
istek PUT $S/onay-kurali "$T" '{"basamaklar":[{"enAzTutar":0,"rolId":"'"$MUDUR"'","kullaniciId":"'"$PATRON"'"}]}'
kontrol 400 "Rol ve kisi birlikte verilemez" basamak_gecersiz
istek PUT $S/onay-kurali "$T" '{"basamaklar":[{"enAzTutar":0,"rolId":"00000000-0000-7000-8000-000000000001"}]}'
kontrol 400 "Olmayan rol reddedilir" rol_gecersiz
istek PUT $S/onay-kurali "$T" '{"basamaklar":[{"enAzTutar":10000,"kullaniciId":"'"$PATRON"'"},{"enAzTutar":0,"rolId":"'"$MUDUR"'"}]}'
kontrol 200 "Iki basamak, tutara gore siralandi" '"sira":1,"enAzTutar":0.*"ad":"Mudur".*"sira":2,"enAzTutar":10000.*"ad":"Sena Patron"'
istek PUT $S/onay-kurali "$TM" '{"basamaklar":[]}'
kontrol 403 "Kural yetkisi yoksa degistiremez" yetki_yok

echo "--- Kucuk tutar: tek basamak"
istek POST $S/talepler "$TT" "$(talep 'Kirtasiye' 500)"
kontrol 201 "Talep eden talep acti"; K=$(alan id)
istek POST "$S/talepler/$K/onayla" "$TM" '{}'
kontrol 422 "Taslak onaylanamaz" talep_onayda_degil
istek POST "$S/talepler/$K/gonder" "$TT" '{}'
kontrol 200 "Onaya gonderildi, tek basamak" '"durum":"Onayda".*"sira":1,"onaylayan":"Mudur","durum":"Bekliyor"'
istek PUT "$S/talepler/$K" "$TT" "$(talep 'Degisti' 1)"
kontrol 422 "Onaydaki talep degistirilemez" talep_degistirilemez
istek GET $S/talepler/bekleyenler "$TM"
kontrol 200 "Mudurun bekleyenlerinde" 'Kirtasiye'
istek GET "/api/bildirim?boyut=20" "$TM"
kontrol 200 "Mudure onay bildirimi gitti" 'Onay.*bekliyor: TL'
istek GET "/api/bildirim?boyut=20" "$TO"
if echo "$GOVDE" | grep -q 'bekliyor: TL'; then KOD=gitti; fi
kontrol 200 "Ilgisiz kullaniciya bildirim gitmedi"
istek GET "$S/talepler/$K" "$TM"
kontrol 200 "Mudur onaylayabilir gorur" '"onaylayabilirMi":true'
istek POST "$S/talepler/$K/onayla" "$TT" '{}'
kontrol 403 "Onay yetkisi olmayan talep eden onaylayamaz" yetki_yok
istek POST "$S/talepler/$K/reddet" "$TM" '{"not":" "}'
kontrol 400 "Red gerekce ister" red_gerekcesi
istek POST "$S/talepler/$K/onayla" "$TM" '{"not":"Uygun"}'
kontrol 200 "Onaylandi, karar veren kayitli" '"durum":"Onaylandi".*"durum":"Onaylandi","kararVeren":"Mert Mudur".*"not":"Uygun"'
istek GET "/api/bildirim?boyut=20" "$TT"
kontrol 200 "Talep edene sonuc bildirimi" 'Talebiniz onayland'

echo "--- Buyuk tutar: iki basamak ve red"
istek POST $S/talepler "$TT" "$(talep 'Forklift' 50000)"; B=$(alan id)
istek POST "$S/talepler/$B/gonder" "$TT" '{}'
kontrol 200 "Iki basamak olustu" '"sira":1,"onaylayan":"Mudur".*"sira":2,"onaylayan":"Sena Patron"'
istek POST "$S/talepler/$B/onayla" "$TO" '{}'
kontrol 403 "Rol uymayan ve yetkisiz onaylayamaz" yetki_yok
istek POST "$S/talepler/$B/onayla" "$TM" '{}'
kontrol 200 "Birinci basamak onaylandi, talep hala onayda" '"durum":"Onayda"'
istek POST "$S/talepler/$B/onayla" "$TM" '{}'
kontrol 403 "Sira gecince mudur onaylayamaz" onay_sirasi
istek POST "$S/talepler/$B/reddet" "$T" '{"not":"Butce yok"}'
kontrol 200 "Patron reddetti" '"durum":"Reddedildi".*"not":"Butce yok"'
istek GET "/api/bildirim?boyut=20" "$TT"
kontrol 200 "Talep edene red bildirimi" 'Talebiniz reddedildi'
istek PUT "$S/talepler/$B" "$TT" '{"tarih":"2026-10-15","gerekce":"Forklift kiralik","kalemler":[{"ad":"Kira","birim":"ADET","miktar":1,"tahminiBirimFiyat":5000,"kdvOrani":20}],"surum":'"$(istek GET "$S/talepler/$B" "$TT"; alan surum)"'}'
kontrol 200 "Reddedilen duzeltildi" '"toplam":5000'
istek POST "$S/talepler/$B/gonder" "$TT" '{}'
kontrol 200 "Yeniden gonderildi: yeni tutarla tek basamak, eski kararlar duruyor" '"gonderimNo":1,"sira":1.*"gonderimNo":2,"sira":1,"onaylayan":"Mudur","durum":"Bekliyor"'

echo "--- Kendi talebi"
istek POST $S/talepler "$T" "$(talep 'Patron talebi' 50000)"; P=$(alan id)
istek POST "$S/talepler/$P/gonder" "$T" '{}'
kontrol 422 "Patron kendi basamagina tek basina kalamaz" onaylayan_yok
istek PUT $S/onay-kurali "$T" '{"basamaklar":[{"enAzTutar":0,"rolId":"'"$MUDUR"'"}]}'
istek POST "$S/talepler/$P/gonder" "$T" '{}'
kontrol 200 "Mudur basamagiyla gonderildi"
istek PUT $S/onay-kurali "$T" '{"basamaklar":[{"enAzTutar":0,"rolId":"'"$MUDUR"'"},{"enAzTutar":0,"rolId":"'"$ONAYSIZ"'"}]}'
istek POST $S/talepler "$TT" "$(talep 'Onaysiz rol' 10)"; OR=$(alan id)
istek POST "$S/talepler/$OR/gonder" "$TT" '{}'
kontrol 200 "Yonetici yedek onaylayan sayilir, onay yetkisiz rol kilitlemez"
istek POST "$S/talepler/$P/onayla" "$T" '{}'
kontrol 422 "Yonetici olsa da kendi talebini onaylayamaz" kendi_talebi
istek POST "$S/talepler/$P/onayla" "$TM" '{}'
kontrol 200 "Baskasi onayladi" '"durum":"Onaylandi"'
istek PUT $S/onay-kurali "$T" '{"basamaklar":[{"enAzTutar":0,"rolId":"'"$MUDUR"'"}]}'

echo "--- Siparise donusum"
istek POST /api/cari/hesaplar "$T" '{"unvan":"Tedarikci Bir","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C=$(alan id)
istek POST "$S/talepler/$B/siparise-donustur" "$T" '{"tedarikciId":"'"$C"'"}'
kontrol 422 "Onaylanmamis talep donusturulmez" talep_onaylanmadi
istek POST "$S/talepler/$K/siparise-donustur" "$TT" '{"tedarikciId":"'"$C"'"}'
kontrol 403 "Siparis yetkisi olmayan donusturemez" yetki_yok
istek POST "$S/talepler/$K/siparise-donustur" "$T" '{"tedarikciId":"00000000-0000-7000-8000-000000000001"}'
if [ "$KOD" = 200 ]; then KOD=acildi; fi
kontrol "$KOD" "Olmayan tedarikciyle siparis acilmaz"
istek GET "$S/talepler/$K" "$T"
kontrol 200 "Basarisiz donusumde talep onayli kaldi" '"durum":"Onaylandi"'
istek POST "$S/talepler/$K/siparise-donustur" "$T" '{"tedarikciId":"'"$C"'","terminTarihi":"2026-12-01"}'
kontrol 200 "Taslak siparis acildi, talebe baglandi" '"durum":"SipariseDonustu".*"siparisId":"'; SIP=$(alan siparisId)
istek GET "/api/fatura/teklif-siparis/$SIP" "$T"
kontrol 200 "Siparis taslak, tedarikci ve satirlar geldi" '"tur":"SatinalmaSiparisi".*"durum":"Taslak"'
istek POST "$S/talepler/$K/siparise-donustur" "$T" '{"tedarikciId":"'"$C"'"}'
kontrol 422 "Ikinci kez donusturulmez" talep_onaylanmadi
istek GET "/api/muhasebe/fisler?baslangic=2026-10-01&bitis=2026-12-31" "$T"
kontrol 200 "Talep ve taslak siparis muhasebe fisi uretmez" '"fisler":\[\]'
istek POST "$S/talepler/$K/iptal" "$T" '{}'
kontrol 422 "Siparise donusmus talep iptal edilmez" talep_iptal_edilemez

echo "--- Iptal, silme, dosya"
printf '%%PDF-1.4 teklif' > "$DIR/teklif.pdf"
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/dosya/kayit/satinalma-talebi/$B" -H "Authorization: Bearer $T" -F "dosya=@$DIR/teklif.pdf"); GOVDE=$(cat "$DIR/govde")
kontrol 201 "Talebe teklif dosyasi eklendi" '"ad":"teklif.pdf"'
istek POST "$S/talepler/$B/iptal" "$T" '{}'
kontrol 200 "Onaydaki talep iptal edildi" '"durum":"Iptal"'
istek DELETE "$S/talepler/$B" "$T"
kontrol 204 "Iptal edilen talep silindi"
istek DELETE "$S/talepler/$K" "$T"
kontrol 422 "Siparise donusmus talep silinmez" talep_degistirilemez

echo "--- Izolasyon"
istek GET "$S/talepler/$K" "$T2"
kontrol 404 "Baska firma talebi goremez" talep_bulunamadi
istek POST "$S/talepler/$P/onayla" "$T2" '{}'
kontrol 404 "Baska firma onaylayamaz" talep_bulunamadi
istek GET "$S/talepler" "$T2"
kontrol 200 "Baska firma listesi bos" '"toplamKayit":0'

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
