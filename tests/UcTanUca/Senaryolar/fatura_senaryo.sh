#!/usr/bin/env bash
# Fatura modülü uçtan uca senaryosu. Kullanım: fatura_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_fatura"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -q -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:500}"; KALAN=$((KALAN+1))
  fi
}
satir() { # KART BIRIM MIKTAR FIYAT KDV [DAHIL] [ISK1]
  local k=null; [ -n "$1" ] && k="\"$1\""
  echo '{"stokKartiId":'"$k"',"birim":"'"$2"'","miktar":'"$3"',"birimFiyat":'"$4"',"kdvOrani":'"$5"',"kdvDahilMi":'"${6:-false}"',"iskonto1":'"${7:-0}"',"iskonto2":0,"iskonto3":0}'
}
fatura() { # TUR CARI SATIRLAR [EK_ALANLAR]
  local c=null; [ -n "$2" ] && c="\"$2\""
  echo '{"tur":"'"$1"'","cariHesapId":'"$c"',"tarih":"2026-10-15","genelIskontoTuru":"Yuzde","genelIskonto":0,"satirlar":['"$3"']'"${4:+,$4}"'}'
}

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Fatura Test AS","ad":"Ece","soyad":"Kar","eposta":"fatura'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"faturadiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)

echo "--- Hazirlik"
istek POST /api/cari/hesaplar "$T" '{"unvan":"Yerli Musteri Ltd","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","adres":"Ataturk Cad. 1","il":"Ankara"}'
TL_CARI=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Global Supplier GmbH","tur":"Tedarikci","kisiTipi":"Tuzel","vergiNo":"9876543217","paraBirimi":"USD"}'
USD_CARI=$(alan id)
istek POST /api/stok/kartlar "$T" '{"ad":"Su 0.5 L","tur":"Urun","temelBirim":"ADET","kdvOrani":10,"alisFiyati":10,"satisFiyati":15,"perakendeFiyati":16.5,"paraBirimi":"TRY","ekBirimler":[{"birimKodu":"KOLI","katsayi":12}]}'
kontrol 201 "Stok kartinda perakende fiyati tutuluyor" '"perakendeFiyati":16.5'
SU=$(alan id)
istek POST /api/stok/kartlar "$T" '{"ad":"Nakliye","tur":"Hizmet","temelBirim":"SAAT","kdvOrani":20,"paraBirimi":"TRY"}'
HIZMET=$(alan id)
istek GET /api/fatura/ayarlar "$T"
kontrol 200 "Varsayilan ayar: dogrudan kayit, TPT/PRK/IAD, perakendede KDV dahil" '"kayitAkisi":"DogrudanKayit","toptanSeri":"TPT","perakendeSeri":"PRK","iadeSeri":"IAD","toptanKdvDahil":false,"perakendeKdvDahil":true'

echo "--- Alis faturasi (dovizli)"
istek POST /api/fatura/faturalar "$T" "$(fatura Alis "$USD_CARI" "$(satir "$SU" KOLI 10 100 10 false 10)" '"numara":"abc2026000000001","kur":40')"
kontrol 201 "USD alis: 1000 - %10 = 900 + KDV 90 = 990 USD, TL 39600, onayli" '"durum":"Onayli","numara":"ABC2026000000001".*"genelToplam":990.*"genelToplamTl":39600'
ALIS=$(alan id)
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Alis stoga 120 adet girdi, maliyet adet basi 300 TL (iskontolu, kurla)" '"miktar":120.*"birimMaliyet":300'
istek GET "/api/cari/hesaplar/$USD_CARI" "$T"
kontrol 200 "Tedarikci 990 USD alacakli" '"bakiye":-990'
istek POST /api/fatura/faturalar "$T" "$(fatura Alis "$USD_CARI" "$(satir "$SU" KOLI 1 100 10)" '"numara":"ABC2026000000001","kur":40')"
kontrol 409 "Ayni tedarikciden ayni numarali ikinci fatura reddedildi" numara_kullaniliyor
istek POST /api/fatura/faturalar "$T" "$(fatura Alis "$USD_CARI" "$(satir "$SU" KOLI 1 100 10)" '"kur":40')"
kontrol 400 "Numarasiz gelen fatura reddedildi" numara_gerekli
istek POST /api/fatura/faturalar "$T" "$(fatura Alis "$USD_CARI" "$(satir "$SU" KOLI 1 100 10)" '"numara":"TCMB-1"')"
kontrol 201 "Kur girilmeyen dovizli faturada TCMB kuru kullanildi" '"paraBirimi":"USD","kur":[1-9][0-9]'
TCMB_ALIS=$(alan id)
istek POST "/api/fatura/faturalar/$TCMB_ALIS/iptal" "$T"

echo "--- Satis faturalari"
SATIRLAR="$(satir "$SU" KOLI 5 5000 10),$(satir "$HIZMET" SAAT 1 1000 20)"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$SATIRLAR" '"satisSekli":"Toptan","genelIskontoTuru":"Yuzde","genelIskonto":10,"vadeTarihi":"2026-11-14"')"
kontrol 201 "Toptan satis: %10 genel iskonto dagitildi, matrah 23400, KDV 2430, toplam 25830, TPT2026000000001" '"numara":"TPT2026000000001".*"matrah":23400.*"kdvToplami":2430.*"genelToplam":25830'
SATIS1=$(alan id); SURUM1=$(echo "$GOVDE" | grep -o '"surum":[0-9]*' | tail -1 | cut -d: -f2)
kontrol 201 "KDV dagilimi %10 ve %20 ayri" '"kdvDagilimi":\[{"oran":10,"matrah":22500,"kdvTutari":2250},{"oran":20,"matrah":900,"kdvTutari":180}\]'
kontrol 201 "Cari bilgileri faturaya kopyalandi" '"cariUnvan":"Yerli Musteri Ltd".*"il":"Ankara".*"adres":"Ataturk Cad. 1"'
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10)")"
kontrol 201 "Ikinci satis TPT2026000000002" '"numara":"TPT2026000000002"'
SATIS2=$(alan id)
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "" "$(satir "$SU" ADET 1 450 10 true)" '"satisSekli":"Perakende"')"
kontrol 201 "Perakende nihai tuketici: 450 KDV dahil -> matrah 409,09 + KDV 40,91 = 450, PRK serisi" '"numara":"PRK2026000000001".*"cariHesapId":null.*"cariUnvan":"Nihai Tüketici","vergiNo":"11111111111".*"matrah":409.09.*"kdvToplami":40.91.*"genelToplam":450'
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "" "$(satir "$SU" ADET 1 450 10 true)" '"satisSekli":"Perakende","nihaiTuketici":{"adSoyad":"Ali Veli","tckn":"10000000146"}')"
kontrol 201 "Nihai tuketici adi ve TCKN girilebildi" '"cariUnvan":"Ali Veli","vergiNo":"10000000146"'
PERAKENDE2=$(alan id)
istek POST "/api/fatura/faturalar/$PERAKENDE2/iptal" "$T"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "" "$(satir "$SU" ADET 1 450 10)" '"satisSekli":"Toptan"')"
kontrol 400 "Toptan satista cari zorunlu" cari_gerekli
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "" "$(satir "$SU" ADET 1 450 10)" '"satisSekli":"Perakende","nihaiTuketici":{"tckn":"12345678901"}')"
kontrol 400 "Gecersiz TCKN reddedildi" "kimlik"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "" ADET 999999999 999999999999 20)")"
kontrol 400 "Miktar x fiyat tasan satir reddedildi (500 degil)" "Tutar"
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Satislardan sonra stok 120 - 60 - 1 - 1 = 58 (iptal edilen perakende geri girdi)" '"miktar":58'
istek GET "/api/cari/hesaplar/$TL_CARI" "$T"
kontrol 200 "Musteri bakiyesi 25830 + 550 = 26380" '"bakiye":26380'

echo "--- Eksi stok engeli ve transaction"
istek PUT /api/stok/ayarlar "$T" '{"maliyetYontemi":"AgirlikliOrtalama","eksiStokDavranisi":"Engelle"}'
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1000 500 10)")"
kontrol 422 "Engelle ayarinda yetersiz stoklu fatura reddedildi (urun adiyla)" "Su 0.5 L: Depoda yeterli stok yok"
istek GET "/api/fatura/faturalar?tur=Satis" "$T"
kontrol 200 "Reddedilen fatura kaydedilmedi (toplam 4 satis faturasi)" '"toplamKayit":4'
istek GET "/api/cari/hesaplar/$TL_CARI" "$T"
kontrol 200 "Reddedilen faturanin cari hareketi de geri alindi (bakiye 26380)" '"bakiye":26380'
istek PUT /api/stok/ayarlar "$T" '{"maliyetYontemi":"AgirlikliOrtalama","eksiStokDavranisi":"Uyar"}'
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10)")"
kontrol 201 "Geri alinan numara bosa dusmedi: sonraki fatura TPT2026000000003" '"numara":"TPT2026000000003"'
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 100 500 10)")"
kontrol 201 "Uyar ayarinda eksi stok uyarisi dondu" '"uyari":"Su 0.5 L: Dikkat'
EKSI=$(echo "$GOVDE" | grep -o '"fatura":{"id":"[^"]*' | cut -d'"' -f6)
istek POST "/api/fatura/faturalar/$EKSI/iptal" "$T"
kontrol 200 "Eksiye dusuren fatura iptal edildi" '"durum":"Iptal"'

echo "--- Iadeler"
istek POST /api/fatura/faturalar "$T" "$(fatura SatistanIade "$TL_CARI" "$(satir "$SU" KOLI 1 5000 10)" '"numara":"MUS-001"')"
kontrol 201 "Satistan iade: musteri alacaklandi, numara karsi taraftan" '"tur":"SatistanIade".*"numara":"MUS-001"'
istek POST /api/fatura/faturalar "$T" "$(fatura AlistanIade "$USD_CARI" "$(satir "$SU" KOLI 1 100 10)" '"kur":40')"
kontrol 201 "Alistan iade IAD serisinden numara aldi" '"numara":"IAD2026000000001"'
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Iadelerden sonra stok 57 + 12 - 12 = 57, maliyet 300 korundu" '"miktar":57.*"birimMaliyet":300'
istek GET "/api/cari/hesaplar/$USD_CARI" "$T"
kontrol 200 "Tedarikci bakiyesi -990 + 110 = -880 USD" '"bakiye":-880'
istek GET "/api/stok/kartlar/$SU/hareketler" "$T"
kontrol 200 "Stok hareketlerinde fatura baglantisi ve iade turleri var" '"tur":"AlistanIade".*"kaynakBelgeId":"'

echo "--- Duzenleme ve iptal (dogrudan kayit)"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10)" '"tarih":"2026-10-01"')"
kontrol 422 "Ayni seride daha eski tarihli fatura kesilemez (numara tarih sirasi)" tarih_sirasi
SATIRLAR="$(satir "$SU" KOLI 4 5000 10),$(satir "$HIZMET" SAAT 1 1000 20)"
istek PUT "/api/fatura/faturalar/$SATIS1" "$T" "$(fatura Satis "$TL_CARI" "$SATIRLAR" '"tarih":"2026-10-20","surum":'"$SURUM1")"
kontrol 422 "Duzenlemede tarih sonraki numaralarin onune gecemez" "sonraki numaral"
istek PUT "/api/fatura/faturalar/$SATIS1" "$T" "$(fatura Satis "$TL_CARI" "$SATIRLAR" '"tarih":"2027-01-05","surum":'"$SURUM1")"
kontrol 422 "Numaralanmis faturanin yili degistirilemez" numara_yili
istek PUT "/api/fatura/faturalar/$SATIS1" "$T" "$(fatura Satis "$TL_CARI" "$SATIRLAR" '"genelIskontoTuru":"Yuzde","genelIskonto":10,"surum":'"$SURUM1")"
kontrol 200 "Onayli fatura duzenlendi: numara korundu, toplam 20880" '"numara":"TPT2026000000001".*"genelToplam":20880'
istek PUT "/api/fatura/faturalar/$SATIS1" "$T" "$(fatura Satis "$TL_CARI" "$SATIRLAR" '"surum":'"$SURUM1")"
kontrol 409 "Eski surumle duzenleme reddedildi"
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Duzenlemede stok yenilendi: 57 + 60 - 48 = 69" '"miktar":69'
istek GET "/api/cari/hesaplar/$TL_CARI" "$T"
kontrol 200 "Musteri bakiyesi 26380 + 550 - 5500 - 25830 + 20880 = 16480" '"bakiye":16480'
istek POST "/api/fatura/faturalar/$SATIS2/iptal" "$T"
kontrol 200 "Satis iptal edildi, numara korundu" '"durum":"Iptal","numara":"TPT2026000000002"'
istek POST "/api/fatura/faturalar/$SATIS2/iptal" "$T"
kontrol 422 "Iptal edilmis fatura tekrar iptal edilemez" fatura_iptal
istek GET "/api/cari/hesaplar/$TL_CARI" "$T"
kontrol 200 "Iptal sonrasi bakiye 15930" '"bakiye":15930'
istek GET "/api/cari/hesaplar/$TL_CARI/hareketler" "$T"
kontrol 200 "Cari hareketlerinde fatura baglantisi var" '"kaynakBelgeId":"'
HAREKET=$(alan id)
istek POST "/api/cari/hareketler/$HAREKET/iptal" "$T"
kontrol 422 "Fatura hareketi cariden elle iptal edilemez" kaynak_belgeden_iptal

echo "--- Taslak -> onay akisi"
istek PUT /api/fatura/ayarlar "$T" '{"kayitAkisi":"TaslakOnay","toptanSeri":"tpt","perakendeSeri":"PRK","iadeSeri":"IAD","toptanKdvDahil":false,"perakendeKdvDahil":true,"varsayilanVadeGunu":30}'
kontrol 200 "Taslak akisina gecildi" '"kayitAkisi":"TaslakOnay","toptanSeri":"TPT"'
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10)")"
kontrol 201 "Taslak olustu, numarasiz" '"durum":"Taslak","numara":null'
TASLAK=$(alan id); TSURUM=$(echo "$GOVDE" | grep -o '"surum":[0-9]*' | tail -1 | cut -d: -f2)
istek PUT "/api/fatura/faturalar/$TASLAK" "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 2 500 10)" '"surum":'"$TSURUM")"
kontrol 200 "Taslak duzenlendi" '"durum":"Taslak".*"genelToplam":1100'
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Taslak stogu etkilemedi (iptalden sonra 70)" '"miktar":70'
istek POST "/api/fatura/faturalar/$TASLAK/onayla" "$T"
kontrol 200 "Onaylanan taslak numara aldi" '"durum":"Onayli","numara":"TPT2026000000005"'
istek POST "/api/fatura/faturalar/$TASLAK/onayla" "$T"
kontrol 422 "Ikinci onay reddedildi" zaten_onayli
istek PUT "/api/fatura/faturalar/$TASLAK" "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 3 500 10)")"
kontrol 422 "Taslak akisinda onayli fatura duzenlenemez" onayli_duzenlenemez
istek DELETE "/api/fatura/faturalar/$TASLAK" "$T"
kontrol 422 "Onayli fatura silinemez" yalnizca_taslak_silinir
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10)")"
TASLAK2=$(alan id)
istek DELETE "/api/fatura/faturalar/$TASLAK2" "$T"
kontrol 204 "Taslak silindi"
istek GET "/api/fatura/faturalar/$TASLAK2" "$T"
kontrol 404 "Silinen taslak bulunamaz"
istek PUT /api/fatura/ayarlar "$T" '{"kayitAkisi":"DogrudanKayit","toptanSeri":"TOPTAN","perakendeSeri":"PRK","iadeSeri":"IAD","toptanKdvDahil":false,"perakendeKdvDahil":true,"varsayilanVadeGunu":0}'
kontrol 400 "3 karakterden uzun seri reddedildi"
istek PUT /api/fatura/ayarlar "$T" '{"kayitAkisi":"DogrudanKayit","toptanSeri":"TPT","perakendeSeri":"PRK","iadeSeri":"IAD","toptanKdvDahil":false,"perakendeKdvDahil":true,"varsayilanVadeGunu":0}'

echo "--- Eszamanli numaralandirma"
for i in 1 2 3 4 5; do
  curl -s -X POST "$KOK/api/fatura/faturalar" -H "Content-Type: application/json" -H "Authorization: Bearer $T" \
    --data-binary "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10)")" -o "$DIR/paralel$i" &
done
wait
NUMARALAR=$(cat "$DIR"/paralel* | grep -o '"numara":"TPT[0-9]*"' | sort -u | tr '\n' ' ')
KOD=200; GOVDE="$NUMARALAR"
kontrol 200 "5 eszamanli fatura 6..10 sirasini cakismadan aldi" 'TPT2026000000006.*TPT2026000000007.*TPT2026000000008.*TPT2026000000009.*TPT2026000000010'

echo "--- Dogrulama"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10 false 150)")"
kontrol 400 "Yuzde 100'u asan iskonto reddedildi"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 18)")"
kontrol 400 "Gecersiz KDV orani reddedildi"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "")"
kontrol 400 "Satirsiz fatura reddedildi"
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" KG 1 500 10)")"
kontrol 400 "Kartta tanimsiz birim reddedildi" birim_tanimsiz
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "" ADET 1 500 10)")"
kontrol 400 "Adsiz serbest satir reddedildi" satir_adi_gerekli
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" '{"ad":"Danismanlik","birim":"SAAT","miktar":2,"birimFiyat":750,"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0}')"
kontrol 201 "Stok kartsiz serbest satirla fatura kesildi (stok hareketi yok)" '"genelToplam":1800.*"stokluMu":false'
istek POST /api/fatura/faturalar "$T" "$(fatura Satis "$TL_CARI" "$(satir "$SU" ADET 1 500 10)" '"genelIskontoTuru":"Tutar","genelIskonto":501')"
kontrol 400 "Net toplami asan tutar iskontosu reddedildi" genel_iskonto_fazla

echo "--- Rapor, firma bilgileri ve izolasyon"
istek GET /api/fatura/raporlar/ozet "$T"
# Test faturaları Ekim 2026 tarihli; "bu ay" sunucunun bugününe göre hesaplandığından Ekim dışında tutar son aylar grafiğinde aranır.
kontrol 200 "Ozet raporu: Ekim satisi (KDV haric, iade dusulmus) 18900+409,09+500+1000+2500+1500-5000" '"buAySatis":19809.09\|"yil":2026,"ay":10,"satis":19809.09'
istek GET /api/yonetim/firma "$T"
kontrol 200 "Firma bilgileri okunuyor" '"unvan":"Fatura Test AS"'
istek PUT /api/yonetim/firma "$T" '{"unvan":"Fatura Test AS","vergiNo":"9876543217","vergiDairesi":"Kadikoy","il":"Istanbul","ilce":"Kadikoy","adres":"Moda Cad. 5","telefon":"0216 000 00 00","eposta":"info@fatura.test"}'
kontrol 200 "Firma adres/telefon bilgileri kaydedildi" '"adres":"Moda Cad. 5","telefon":"0216 000 00 00"'
istek PUT /api/yonetim/firma "$T" '{"unvan":"","vergiNo":"123"}'
kontrol 400 "Bos unvan ve hatali VKN reddedildi"
istek GET "/api/fatura/faturalar/$SATIS1" "$T2"
kontrol 404 "Baska firma faturayi goremez"
istek POST /api/fatura/faturalar "$T2" "$(fatura Satis "$TL_CARI" '{"ad":"X","birim":"ADET","miktar":1,"birimFiyat":1,"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0}')"
kontrol 404 "Baska firmanin carisine fatura kesilemez" cari_bulunamadi
istek GET /api/fatura/faturalar "$T2"
kontrol 200 "Diger firmanin fatura listesi bos" '"toplamKayit":0'

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
