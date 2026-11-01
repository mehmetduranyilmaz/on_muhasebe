#!/usr/bin/env bash
# Arac Takibi modulu uctan uca senaryosu. Kullanim: arac_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_arac"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim
A=/api/arac

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
arac() { echo '{"plaka":"'"$1"'","marka":"Ford","model":"Transit","modelYili":2022,"tur":"'"${2:-HafifTicari}"'","yakit":"Dizel","mulkiyet":"OzMal","durum":"Aktif","guncelKm":10000'"${3:+,$3}"'}'; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Arac Test AS","ad":"Can","soyad":"Yon","eposta":"arac'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Arac AS","ad":"D","soyad":"F","eposta":"aracdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)

echo "--- Arac karti"
istek POST $A/araclar "$T" "$(arac 'ABC')"
kontrol 400 "Gecersiz plaka reddedilir" plaka_gecersiz
istek POST $A/araclar "$T" "$(arac '34 abc 123' HafifTicari '"surucu":"Ali Veli"')"
kontrol 201 "Arac eklendi, plaka normallesti" '"plaka":"34ABC123"'; ARAC=$(alan id); SURUM=$(alan surum)
istek POST $A/araclar "$T" "$(arac '34ABC123')"
kontrol 409 "Ayni plaka ikinci kez eklenmez" plaka_kayitli
istek POST $A/araclar "$T" "$(arac 'TESCIL-77' IsMakinesi)"
kontrol 201 "Is makinesinde tescil no" '"plaka":"TESCIL77"'; MAKINE=$(alan id)
istek POST $A/araclar "$T" "$(arac '34XYZ999' Binek '"masrafMerkeziId":"00000000-0000-7000-8000-000000000001"')"
kontrol 400 "Olmayan masraf merkezi reddedilir" masraf_merkezi
istek POST $A/araclar "$T" "$(arac '34XYZ999' Binek '"demirbasId":"00000000-0000-7000-8000-000000000001"')"
kontrol 400 "Olmayan demirbas reddedilir" demirbas_gecersiz
istek PUT "$A/araclar/$ARAC" "$T" "$(arac '34ABC123' HafifTicari '"surucu":"Ayse Kaya","surum":'"$SURUM")"
kontrol 200 "Guncellendi" '"surucu":"Ayse Kaya"'
istek PUT "$A/araclar/$ARAC" "$T" "$(arac '34ABC123' HafifTicari '"surum":'"$SURUM")"
kontrol 409 "Eski surumle guncelleme reddedilir"
istek GET "$A/araclar?arama=34%20abc" "$T"
kontrol 200 "Bosluklu plakayla arama" '"plaka":"34ABC123"'

echo "--- Yukumlulukler"
istek POST "$A/araclar/$ARAC/yukumlulukler" "$T" '{"tur":"TrafikSigortasi","bitisTarihi":"'"$(gun 10)"'","hatirlatmaGun":0}'
kontrol 400 "Hatirlatma gunu en az 1" Hat
istek POST "$A/araclar/$ARAC/yukumlulukler" "$T" '{"tur":"Diger","bitisTarihi":"'"$(gun 10)"'","hatirlatmaGun":30}'
kontrol 400 "Diger turunde ad gerekli" 'ad gerekli'
istek POST "$A/araclar/$ARAC/yukumlulukler" "$T" '{"tur":"TrafikSigortasi","baslangicTarihi":"2026-10-10","bitisTarihi":"'"$(gun 10)"'","belgeNo":"T-1","sirket":"Sigorta AS","tutar":5000,"hatirlatmaGun":30}'
kontrol 201 "Trafik sigortasi yaklasiyor" '"kalanGun":10,"durum":"Yaklasiyor"'; TRAFIK=$(alan id)
istek POST "$A/araclar/$ARAC/yukumlulukler" "$T" '{"tur":"Muayene","bitisTarihi":"'"$(gun -3)"'","hatirlatmaGun":30}'
kontrol 201 "Muayene suresi dolmus" '"durum":"SuresiDoldu"'
istek POST "$A/araclar/$ARAC/yukumlulukler" "$T" '{"tur":"Kasko","bitisTarihi":"'"$(gun 300)"'","hatirlatmaGun":30}'
kontrol 201 "Kasko gecerli" '"durum":"Gecerli"'
istek GET $A/araclar "$T"
kontrol 200 "Listede en yakin yukumluluk" '"plaka":"34ABC123".*"enYakinYukumluluk":"Muayene".*"enYakinDurum":"SuresiDoldu"'
istek GET $A/yaklasanlar "$T"
kontrol 200 "Yaklasanlar panelinde muayene ve trafik" '"tur":"Muayene".*"tur":"TrafikSigortasi"'
if echo "$GOVDE" | grep -q '"tur":"Kasko"'; then KOD=kasko-var; fi
kontrol 200 "Gecerli kasko yaklasanlarda yok"
istek POST "$A/yukumlulukler/$TRAFIK/yenile" "$T" '{"tur":"TrafikSigortasi","bitisTarihi":"'"$(gun 5)"'","hatirlatmaGun":30}'
kontrol 422 "Yeni bitis eskisinden sonra olmali" yenileme_tarihi
istek POST "$A/yukumlulukler/$TRAFIK/yenile" "$T" '{"tur":"TrafikSigortasi","baslangicTarihi":"'"$(gun 10)"'","bitisTarihi":"'"$(gun 375)"'","belgeNo":"T-2","tutar":6000,"hatirlatmaGun":30}'
kontrol 201 "Yenilendi, yeni kayit gecerli" '"belgeNo":"T-2".*"yenilendiMi":false.*"durum":"Gecerli"'
istek POST "$A/yukumlulukler/$TRAFIK/yenile" "$T" '{"tur":"TrafikSigortasi","bitisTarihi":"'"$(gun 400)"'","hatirlatmaGun":30}'
kontrol 422 "Eski kayit ikinci kez yenilenmez" zaten_yenilendi
istek GET "$A/araclar/$ARAC/yukumlulukler" "$T"
kontrol 200 "Eski kayit gecmiste" '"belgeNo":"T-1".*"yenilendiMi":true'

echo "--- MTV"
istek POST "$A/araclar/$ARAC/mtv" "$T" '{"yil":2026,"taksit":3,"tutar":100}'
kontrol 400 "Taksit 1 ya da 2" Taksit
istek POST "$A/araclar/$ARAC/mtv" "$T" '{"yil":2026,"taksit":2,"tutar":3000}'
kontrol 201 "MTV 2. taksit, son odeme Temmuz sonu" '"sonOdemeTarihi":"2026-07-31","odemeTarihi":null'; MTV=$(alan id)
istek POST "$A/araclar/$ARAC/mtv" "$T" '{"yil":2026,"taksit":2,"tutar":3000}'
kontrol 409 "Ayni taksit ikinci kez eklenmez" mtv_kayitli
istek POST "$A/mtv/$MTV/ode" "$T" '{"odemeTarihi":"2026-07-20"}'
kontrol 200 "MTV odendi" '"odemeTarihi":"2026-07-20"'

echo "--- Masraf ve hasar"
istek POST "$A/araclar/$ARAC/masraflar" "$T" '{"tur":"Yakit","tarih":"2026-10-01","tutar":0}'
kontrol 400 "Tutar sifir olamaz"
istek POST "$A/araclar/$ARAC/masraflar" "$T" '{"tur":"Yakit","tarih":"2026-10-01","tutar":1000,"km":10000,"litre":50,"faturaId":"00000000-0000-7000-8000-000000000001"}'
kontrol 400 "Olmayan fatura baglanamaz" fatura_gecersiz
istek POST "$A/araclar/$ARAC/masraflar" "$T" '{"tur":"Yakit","tarih":"2026-10-01","tutar":1000,"km":10000,"litre":50}'
kontrol 201 "Ilk dolum"
istek POST "$A/araclar/$ARAC/masraflar" "$T" '{"tur":"Yakit","tarih":"2026-10-10","tutar":600,"km":10500,"litre":30}'
kontrol 201 "Ikinci dolum"
istek POST "$A/araclar/$ARAC/masraflar" "$T" '{"tur":"Yakit","tarih":"2026-10-20","tutar":800,"km":11000,"litre":40}'
kontrol 201 "Ucuncu dolum"; YAKIT3=$(alan id)
istek POST "$A/araclar/$ARAC/masraflar" "$T" '{"tur":"Otopark","tarih":"2026-10-21","tutar":100,"litre":5}'
kontrol 201 "Yakit disinda litre tutulmaz" '"litre":null'
istek GET "$A/araclar/$ARAC" "$T"
kontrol 200 "Masraftaki km aracin sayacini ilerletir" '"guncelKm":11000'
istek POST "$A/araclar/$ARAC/hasarlar" "$T" '{"tarih":"2026-10-15","kusur":"Kusursuz","aciklama":"Park halinde carpma","onarimTutari":5000,"sigortaOdemesi":4000,"dosyaNo":"H-1"}'
kontrol 201 "Hasar kaydi" '"kusur":"Kusursuz"'; HASAR=$(alan id)
# Satis faturasi olmadan kayit yapilabilir; muhasebe fislerine dusmez
istek GET "/api/muhasebe/fisler?baslangic=2026-10-01&bitis=2026-10-31" "$T"
kontrol 200 "Arac masrafi muhasebe fisi uretmez" '"fisler":\[\]'

echo "--- Maliyet raporu"
istek GET "$A/raporlar/maliyet?baslangic=2026-10-01&bitis=2026-10-31&aracId=$ARAC" "$T"
kontrol 200 "Masraf, yakit, sigorta, hasar net, toplam" '"masraf":100(\.0+)?,"yakit":2400(\.0+)?,"sigorta":5000(\.0+)?,"mtv":0(\.0+)?,"hasarNet":1000(\.0+)?,"toplam":8500(\.0+)?'
kontrol 200 "Km, km basina maliyet, tuketim" '"km":1000,"kmBasinaMaliyet":8\.5(0+)?,"tuketim":7(\.0+)?[,}]'
istek GET "$A/raporlar/maliyet?baslangic=2026-07-01&bitis=2026-07-31&aracId=$ARAC" "$T"
kontrol 200 "MTV odeme tarihine gore doneme girer" '"mtv":3000(\.0+)?,'
istek GET "$A/raporlar/maliyet?baslangic=2026-10-31&bitis=2026-10-01" "$T"
kontrol 400 "Ters donem reddedilir" donem

echo "--- Dosya"
printf '%%PDF-1.4 police' > "$DIR/police.pdf"
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/dosya/kayit/arac-yukumluluk/$TRAFIK" -H "Authorization: Bearer $T" -F "dosya=@$DIR/police.pdf"); GOVDE=$(cat "$DIR/govde")
kontrol 201 "Yukumluluge police eklendi" '"ad":"police.pdf"'
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/dosya/kayit/arac/$ARAC" -H "Authorization: Bearer $T" -F "dosya=@$DIR/police.pdf"); GOVDE=$(cat "$DIR/govde")
kontrol 201 "Araca ruhsat eklendi"

echo "--- Izolasyon ve yetki"
istek GET "$A/araclar/$ARAC" "$T2"
kontrol 404 "Baska firma araci goremez" arac_bulunamadi
istek GET "$A/araclar/$ARAC/masraflar" "$T2"
kontrol 404 "Baska firma masraflari goremez" arac_bulunamadi
istek DELETE "$A/masraflar/$YAKIT3" "$T2"
kontrol 404 "Baska firma masrafi silemez" arac_kaydi_bulunamadi
istek POST "$A/mtv/$MTV/odeme-geri-al" "$T2" '{}'
kontrol 404 "Baska firma MTV'ye dokunamaz" arac_kaydi_bulunamadi
istek POST $A/araclar "$T2" "$(arac '34ABC123')"
kontrol 201 "Plaka firma icinde tektir, baska firma ayni plakayi girebilir"
istek POST $Y/roller "$T" '{"ad":"Arac Goren","yetkiler":["arac.kart.goruntule","arac.islem.goruntule"]}'
davet_kabul "$T" '{"ad":"Goren","soyad":"Kisi","eposta":"aracgoren'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Arac Goren"}'
GT=$(alan accessToken)
istek GET "$A/araclar/$ARAC/masraflar" "$GT"
kontrol 200 "Goruntuleme yetkilisi listeler"
istek POST "$A/araclar/$ARAC/masraflar" "$GT" '{"tur":"Otopark","tarih":"2026-10-21","tutar":10}'
kontrol 403 "Ekleme yetkisi yoksa masraf girilemez" yetki_yok
istek GET "$A/raporlar/maliyet?baslangic=2026-10-01&bitis=2026-10-31" "$GT"
kontrol 403 "Rapor yetkisi yoksa maliyet goremez" yetki_yok

echo "--- Silme"
istek DELETE "$A/hasarlar/$HASAR" "$T"
kontrol 204 "Hasar silindi"
istek POST "$A/mtv/$MTV/odeme-geri-al" "$T" '{}'
kontrol 200 "MTV odemesi geri alindi" '"odemeTarihi":null'
istek DELETE "$A/araclar/$MAKINE" "$T"
kontrol 204 "Arac silindi"
istek GET "$A/araclar/$MAKINE" "$T"
kontrol 404 "Silinen arac bulunamaz" arac_bulunamadi
istek POST $A/araclar "$T" "$(arac 'TESCIL77' IsMakinesi)"
kontrol 201 "Silinen aracin plakasi yeniden kullanilir"

istek DELETE "$A/araclar/$ARAC" "$T"
kontrol 204 "Kayitlari olan arac da silinir"
istek GET "$A/araclar/$ARAC/masraflar" "$T"
kontrol 404 "Silinen aracin kayitlarina ulasilmaz" arac_bulunamadi

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
