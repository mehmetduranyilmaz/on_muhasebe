#!/usr/bin/env bash
# Vade analizi (kısmi kapama, dilimler, sayfa, arama) uçtan uca senaryosu. Kullanım: vade_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_vade"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -qE -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:600}"; KALAN=$((KALAN+1))
  fi
}
satir() { echo '{"stokKartiId":null,"birim":"ADET","miktar":'"$1"',"birimFiyat":'"$2"',"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0,"ad":"'"${3:-Hizmet}"'"}'; }

U=$(date +%s)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Vade Test AS","ad":"V","soyad":"T","eposta":"vade'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Gecikmeli Musteri","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C1=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Dovizli Musteri","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"USD"}'; C2=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Kapali Musteri","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C3=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Tedarikci Ltd","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C4=$(alan id)
h() { istek POST "/api/cari/hesaplar/$1/hareketler" "$T" "$2"; [ "$KOD" = 201 ] || echo "HAREKET HATASI $KOD $GOVDE"; }
h $C1 '{"tur":"Dekont","yon":"Borc","tarih":"2026-08-01","vadeTarihi":"2026-09-01","tutar":1000}'
h $C1 '{"tur":"Dekont","yon":"Borc","tarih":"2026-10-01","vadeTarihi":"2026-11-15","tutar":500}'
h $C1 '{"tur":"Dekont","yon":"Alacak","tarih":"2026-10-05","tutar":400}'
h $C2 '{"tur":"Dekont","yon":"Borc","tarih":"2026-05-01","tutar":200,"kur":40}'
h $C3 '{"tur":"Dekont","yon":"Borc","tarih":"2026-07-01","tutar":300}'
h $C3 '{"tur":"Dekont","yon":"Alacak","tarih":"2026-07-10","tutar":300}'
h $C4 '{"tur":"Dekont","yon":"Alacak","tarih":"2026-10-10","vadeTarihi":"2026-10-11","tutar":750}'
D=$(curl -s -X POST "$KOK/api/cari/hesaplar/$C1/hareketler" -H "Content-Type: application/json" -H "Authorization: Bearer $T" -d '{"tur":"Dekont","yon":"Borc","tarih":"2026-06-01","tutar":999}' | grep -o '"id":"[^"]*' | cut -d'"' -f4)
istek POST "/api/cari/hareketler/$D/iptal" "$T"

istek GET "/api/cari/raporlar/vade-analizi?tarih=2026-10-21" "$T"
kontrol 200 "Kismi kapanan kalem: 1000-400=600 31-60 gun, gelecek vadeli 500 vadesi gelmemis, en eski 50 gun" '"unvan":"Gecikmeli Musteri","paraBirimi":"TRY","dilimler":\{"vadesiGelmemis":500[.0]*,"gun0Ile30":0[.0]*,"gun31Ile60":600[.0]*,"gun61Ile90":0[.0]*,"gun90Ustu":0[.0]*,"toplam":1100[.0]*,"vadesiGecen":600[.0]*\},"enUzunGecikmeGun":50'
kontrol 200 "Dovizli cari kendi para biriminde, 90+ gun" '"unvan":"Dovizli Musteri","paraBirimi":"USD","dilimler":\{"vadesiGelmemis":0[.0]*,"gun0Ile30":0[.0]*,"gun31Ile60":0[.0]*,"gun61Ile90":0[.0]*,"gun90Ustu":200'
kontrol 200 "TL toplami yalniz TL cariler (dovizli haric)" '"tlToplami":\{"vadesiGelmemis":500[.0]*,"gun0Ile30":0[.0]*,"gun31Ile60":600[.0]*,"gun61Ile90":0[.0]*,"gun90Ustu":0[.0]*'
if echo "$GOVDE" | grep -q "Kapali Musteri\|Tedarikci Ltd\|999"; then KOD=999; fi
kontrol 200 "Tamamen kapanan, karsi yondeki ve iptal edilen kalemler listede yok"
kontrol 200 "Siralama: vadesi gecen buyuk once (USD 200 < TL 600)" '"Gecikmeli Musteri".*"Dovizli Musteri"'
kontrol 200 "Sayfa bilgisi" '"sayfa":1,"boyut":20,"toplamKayit":2'
istek GET "/api/cari/raporlar/vade-analizi?tarih=2026-10-21&alacaklar=false" "$T"
kontrol 200 "Borclarimiz: tedarikciye 750, 10 gun gecikmis" '"unvan":"Tedarikci Ltd".*"gun0Ile30":750[.0]*.*"enUzunGecikmeGun":10'
istek GET "/api/cari/raporlar/vade-analizi?tarih=2026-10-21&arama=doviz" "$T"
kontrol 200 "Arama yalniz eslesen cari" '"toplamKayit":1'
if echo "$GOVDE" | grep -q "Gecikmeli"; then KOD=999; fi
kontrol 200 "Arama disi cari gelmedi"
istek GET "/api/cari/raporlar/vade-analizi?tarih=2026-10-21&boyut=1&sayfa=2" "$T"
kontrol 200 "Ikinci sayfada ikinci cari" '"kayitlar":\[\{[^]]*"Dovizli Musteri".*"sayfa":2,"boyut":1,"toplamKayit":2'

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
