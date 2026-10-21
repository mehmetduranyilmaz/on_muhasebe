#!/usr/bin/env bash
# Cari ve Döviz modülleri uçtan uca senaryosu. Kullanım: cari_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_cari"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(-d "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -q -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:400}"; KALAN=$((KALAN+1))
  fi
}

U=$(date +%s)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Cari Test A.S.","vergiNo":"1234567890","ad":"Ayse","soyad":"Demir","eposta":"cari'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
istek GET /api/yonetim/ben "$T"; FIRMA=$(echo "$GOVDE" | grep -o '"aktifFirma":{"id":"[^"]*"' | cut -d'"' -f6)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Baska Firma","ad":"B","soyad":"F","eposta":"baska'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/yonetim/giris "" "$SISTEM_GIRIS"
SYS=$(alan accessToken)

echo "--- Doviz (gercek TCMB)"
istek GET "/api/doviz/islem-kuru?paraBirimi=USD&tarih=2026-10-20" "$T"
kontrol 200 "USD islem kuru: 20.10 islemi icin 19.10 bulteni" '"kurTarihi":"2026-10-19"'
istek GET "/api/doviz/islem-kuru?paraBirimi=EUR&tarih=2026-10-19" "$T"
kontrol 200 "Pazartesi islemi icin Cuma (16.10) bulteni" '"kurTarihi":"2026-10-16"'
istek GET "/api/doviz/islem-kuru?paraBirimi=JPY&tarih=2026-10-20" "$T"
kontrol 200 "JPY kuru 1 birime cevrilmis (TL'den kucuk)" '"dovizAlis":0\.'
istek GET "/api/doviz/islem-kuru?paraBirimi=TRY&tarih=2026-10-20" "$T"
kontrol 200 "TRY kuru 1" '"dovizAlis":1'
istek GET "/api/doviz/islem-kuru?paraBirimi=XYZ&tarih=2026-10-20" "$T"
kontrol 400 "Gecersiz para birimi" gecersiz_para_birimi
istek GET "/api/doviz/islem-kuru?paraBirimi=USD&tarih=2026-10-20" ""
kontrol 401 "Kur servisi oturum ister"

echo "--- Cari kartlari"
istek POST /api/cari/hesaplar "$T" '{"unvan":"Yerli Musteri Ltd","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","vadeGun":30,"riskLimiti":500}'
kontrol 201 "TL cari olustu, kod otomatik C00001" '"kod":"C00001"'
TL_CARI=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Global Supplier GmbH","tur":"Tedarikci","kisiTipi":"Tuzel","vergiNo":"9876543217","paraBirimi":"USD"}'
kontrol 201 "USD cari olustu, kod C00002" '"kod":"C00002"'
USD_CARI=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"kod":"OZEL-1","unvan":"Ozel Kodlu","tur":"MusteriVeTedarikci","kisiTipi":"Sahis","vergiNo":"10000000146","paraBirimi":"TRY"}'
kontrol 201 "Elle kod ve gecerli TCKN" '"kod":"OZEL-1"'
OZEL_CARI=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"kod":"OZEL-1","unvan":"Ayni Kod","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'
kontrol 409 "Ayni kod reddedildi" kod_kullaniliyor
istek POST /api/cari/hesaplar "$T" '{"unvan":"Hatali VKN","tur":"Musteri","kisiTipi":"Tuzel","vergiNo":"9876543210","paraBirimi":"TRY"}'
kontrol 400 "Kontrol hanesi yanlis VKN reddedildi" "vergi kimlik"
istek POST /api/cari/hesaplar "$T" '{"unvan":"Sahis VKN","tur":"Musteri","kisiTipi":"Sahis","vergiNo":"1234567890","paraBirimi":"TRY"}'
kontrol 400 "Sahis caride VKN (10 hane) reddedildi" "T.C. kimlik"
istek POST /api/cari/hesaplar "$T" '{"unvan":"X","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"XYZ"}'
kontrol 400 "Gecersiz para birimi reddedildi"

echo "--- Hareketler ve bakiye"
istek POST "/api/cari/hesaplar/$TL_CARI/hareketler" "$T" '{"tur":"Acilis","yon":"Borc","tarih":"2026-01-01","tutar":1000}'
kontrol 201 "TL acilis bakiyesi 1000 borc" '"borcTl":1000'
istek POST "/api/cari/hesaplar/$TL_CARI/hareketler" "$T" '{"tur":"Acilis","yon":"Borc","tarih":"2026-01-01","tutar":5}'
kontrol 422 "Ikinci acilis reddedildi" acilis_zaten_var
istek POST "/api/cari/hesaplar/$TL_CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Alacak","tarih":"2026-10-10","tutar":300,"belgeNo":"D-1","aciklama":"Iade"}'
kontrol 201 "TL alacak dekontu 300"
DEKONT=$(alan id)
istek POST "/api/cari/hesaplar/$TL_CARI/hareketler" "$T" '{"tur":"SatisFaturasi","yon":"Borc","tarih":"2026-10-10","tutar":300}'
kontrol 400 "Fatura turu elle girilemez"
istek GET "/api/cari/hesaplar/$TL_CARI" "$T"
kontrol 200 "TL cari bakiyesi 700" '"bakiye":700'

istek POST "/api/cari/hesaplar/$USD_CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Alacak","tarih":"2026-09-01","tutar":100}'
kontrol 400 "Dovizli caride kur zorunlu" kur_gerekli
istek POST "/api/cari/hesaplar/$USD_CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Alacak","tarih":"2026-09-01","tutar":100,"kur":40}'
kontrol 201 "USD alacak 100 @40 -> 4000 TL" '"alacakTl":4000'
istek POST "/api/cari/hesaplar/$USD_CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Borc","tarih":"2026-10-15","tutar":30,"kur":45.123456789}'
kontrol 201 "USD borc 30, kur 6 haneye yuvarlandi, TL 1353,70" '"borcTl":1353.7'
istek GET "/api/cari/hesaplar/$USD_CARI" "$T"
kontrol 200 "USD cari bakiyesi -70 USD (alacakli)" '"bakiye":-70'

echo "--- Ekstre"
istek GET "/api/cari/hesaplar/$TL_CARI/ekstre?baslangic=2026-10-01&bitis=2026-10-31" "$T"
kontrol 200 "Ekstre: devir 1000, son bakiye 700" '"devirBakiye":1000.*"sonBakiye":700'
istek GET "/api/cari/hesaplar/$USD_CARI/ekstre?baslangic=2026-01-01&bitis=2026-12-31" "$T"
kontrol 200 "Dovizli ekstre: TL bakiye -2646,30" '"sonBakiyeTl":-2646.3'
istek GET "/api/cari/hesaplar/$TL_CARI/ekstre?baslangic=2026-12-01&bitis=2026-01-01" "$T"
kontrol 400 "Ters tarih araligi reddedildi" tarih_araligi

echo "--- Iptal (ters kayit)"
istek POST "/api/cari/hareketler/$DEKONT/iptal" "$T"
kontrol 200 "Dekont iptal edildi" '"iptalKaydiMi":true'
istek POST "/api/cari/hareketler/$DEKONT/iptal" "$T"
kontrol 422 "Ikinci iptal reddedildi" zaten_iptal
istek GET "/api/cari/hesaplar/$TL_CARI" "$T"
kontrol 200 "Iptal sonrasi bakiye tekrar 1000 (risk limiti 500 asildi)" '"bakiye":1000'
istek GET "/api/cari/hesaplar/$TL_CARI/hareketler" "$T"
kontrol 200 "Hareket listesi orijinal + ters kaydi gosteriyor" '"toplamKayit":3'

echo "--- Guncelleme, eszamanlilik, silme"
istek GET "/api/cari/hesaplar/$OZEL_CARI" "$T"; SURUM=$(alan surum)
istek PUT "/api/cari/hesaplar/$OZEL_CARI" "$T" '{"kod":"OZEL-1","unvan":"Ozel Kodlu Guncel","tur":"Musteri","kisiTipi":"Sahis","paraBirimi":"TRY","surum":'"$SURUM"'}'
kontrol 200 "Guncel surumle guncelleme" "Ozel Kodlu Guncel"
istek PUT "/api/cari/hesaplar/$OZEL_CARI" "$T" '{"kod":"OZEL-1","unvan":"Eski Ekrandan","tur":"Musteri","kisiTipi":"Sahis","paraBirimi":"TRY","surum":'"$SURUM"'}'
kontrol 409 "Eski surumle guncelleme reddedildi (baskasi degistirmis)"
istek GET "/api/cari/hesaplar/$USD_CARI" "$T"; S2=$(alan surum)
istek PUT "/api/cari/hesaplar/$USD_CARI" "$T" '{"unvan":"Global Supplier GmbH","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"EUR","surum":'"$S2"'}'
kontrol 422 "Hareketli carinin para birimi degismez" para_birimi_degistirilemez
istek DELETE "/api/cari/hesaplar/$USD_CARI" "$T"
kontrol 422 "Hareketli cari silinemez" hareketli_cari_silinemez
istek DELETE "/api/cari/hesaplar/$OZEL_CARI" "$T"
kontrol 204 "Hareketsiz cari silindi"
istek GET "/api/cari/hesaplar/$OZEL_CARI" "$T"
kontrol 404 "Silinen cari gorunmuyor"
istek POST /api/cari/hesaplar "$T" '{"kod":"OZEL-1","unvan":"Yeniden","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'
kontrol 409 "Silinen carinin kodu tekrar kullanilamaz" kod_kullaniliyor
istek POST "/api/cari/hesaplar/$TL_CARI/pasiflestir" "$T"
istek POST "/api/cari/hesaplar/$TL_CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Borc","tarih":"2026-10-20","tutar":10}'
kontrol 422 "Pasif cariye hareket girilemez" pasif_cari
istek POST "/api/cari/hesaplar/$TL_CARI/aktiflestir" "$T"

echo "--- Gruplar"
istek POST /api/cari/gruplar "$T" '{"ad":"Bayiler"}'
kontrol 201 "Grup olustu"; GRUP=$(alan id)
istek POST /api/cari/gruplar "$T" '{"ad":"Bayiler"}'
kontrol 409 "Ayni grup adi reddedildi"
istek GET "/api/cari/hesaplar/$TL_CARI" "$T"; S3=$(alan surum)
istek PUT "/api/cari/hesaplar/$TL_CARI" "$T" '{"unvan":"Yerli Musteri Ltd","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","vadeGun":30,"riskLimiti":500,"grupId":"'"$GRUP"'","surum":'"$S3"'}'
kontrol 200 "Cari gruba atandi" '"grupAdi":"Bayiler"'
istek DELETE "/api/cari/gruplar/$GRUP" "$T"
kontrol 422 "Kullanilan grup silinemez" grup_kullaniliyor

echo "--- Liste ve bakiye listesi"
istek GET "/api/cari/hesaplar?arama=global" "$T"
kontrol 200 "Arama unvanla buluyor" '"toplamKayit":1'
istek GET "/api/cari/hesaplar?tur=Musteri" "$T"
kontrol 200 "Musteri filtresi" "Yerli Musteri"
istek GET "/api/cari/hesaplar" "$T"
kontrol 200 "Risk asimi listede isaretli" '"riskAsildiMi":true'
istek GET "/api/cari/raporlar/bakiye-listesi?tarih=2026-10-20" "$T"
kontrol 200 "Bakiye listesi: USD toplaminda guncel TL ve kur farki var" '"paraBirimi":"USD","borc":30.00,"alacak":100.00,"bakiye":-70.00,"bakiyeTl":-2646.30,"guncelTl":-[0-9.]*,"kurFarki":-[0-9]'
istek GET "/api/cari/raporlar/bakiye-listesi?durum=Borclu" "$T"
kontrol 200 "Sadece borclular" '"toplamKayit":1'
istek GET "/api/cari/raporlar/bakiye-listesi?tarih=2026-06-30" "$T"
kontrol 200 "Gecmis tarihli bakiye: Eylul hareketleri haric" '"paraBirimi":"TRY","borc":1000'

echo "--- Izolasyon ve lisans"
istek GET "/api/cari/hesaplar/$TL_CARI" "$T2"
kontrol 404 "Baska firma cariyi goremiyor"
istek GET "/api/cari/hesaplar" "$T2"
kontrol 200 "Baska firmanin listesi bos" '"toplamKayit":0'
istek PUT "/api/yonetim/sistem/firmalar/$FIRMA/lisans" "$SYS" '{"paketKodu":"basic","bitisTarihi":"2020-01-01","ekModuller":[]}'
istek POST "/api/cari/hesaplar/$TL_CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Borc","tarih":"2026-10-20","tutar":10}'
kontrol 403 "Suresi dolan lisansta hareket girilemez" lisans_suresi_doldu
istek GET "/api/cari/hesaplar/$TL_CARI/ekstre" "$T"
kontrol 200 "Suresi dolan lisansta ekstre okunabiliyor"
istek PUT "/api/yonetim/sistem/firmalar/$FIRMA/lisans" "$SYS" '{"paketKodu":"basic","bitisTarihi":"2027-12-31","ekModuller":[]}'

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
