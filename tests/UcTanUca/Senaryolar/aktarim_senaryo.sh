#!/usr/bin/env bash
# Excel'den toplu aktarim uctan uca senaryosu. Kullanim: aktarim_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_aktarim"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim

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
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:600}"; KALAN=$((KALAN+1))
  fi
}

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Aktarim Test AS","ad":"Asli","soyad":"Yon","eposta":"aktarim'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Aktarim AS","ad":"D","soyad":"F","eposta":"aktarimdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
C=/api/cari/aktarim
S=/api/stok/aktarim
TARIH="acilisTarihi=2026-01-01"

echo "--- Sablon"
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/sablon.xlsx" -w "%{http_code}" "$KOK$C/sablon" -H "Authorization: Bearer $T"); GOVDE=$(head -c 2 "$DIR/sablon.xlsx")
kontrol 200 "Cari sablonu indirildi" '^PK'
yukle "$C/onizleme" "$T" "$DIR/sablon.xlsx"
kontrol 200 "Bos sablon okunur, satir yok" '"toplam":0'

echo "--- Cari onizleme"
yukle "$C/onizleme" "$T" "$DOS/banka.csv"
kontrol 400 "Excel olmayan dosya reddedilir" dosya_okunamadi
yukle "$C/onizleme?$TARIH" "$T" "$DOS/stok_aktarim.xlsx"
kontrol 400 "Zorunlu sutunu olmayan dosya reddedilir" 'sutun_bulunamadi'
yukle "$C/onizleme?$TARIH" "$T" "$DOS/cari_aktarim.xlsx"
kontrol 200 "Ust bilgi satiri ve farkli basliklar tanindi" '"kaydedildiMi":false,"toplam":6,"eklenecek":3,"mukerrer":1,"hatali":2'
kontrol 200 "Taninmayan sutun bildirildi" '"taninmayanSutunlar":\["Bilinmeyen Sutun"\]'
kontrol 200 "Gecersiz vergi no satir hatasi" '"ad":"Hatali Cari","durum":"Hatali","mesajlar":\["[^"]*vergi kimlik'
kontrol 200 "Dovizli bakiyede kur eksigi satir hatasi" '"ad":"Dolar Cari","durum":"Hatali","mesajlar":\["[^"]*Kur'
kontrol 200 "Dosya icinde ayni unvan mukerrer" '"ad":"AKTARIM M[^"]*","durum":"Mukerrer"'
istek GET "/api/cari/hesaplar" "$T"
kontrol 200 "Onizleme kayit yapmaz" '"toplamKayit":0'
yukle "$C/onizleme" "$T" "$DOS/cari_aktarim.xlsx"
kontrol 200 "Acilis tarihi yoksa bakiyeli satir hatali" '"ad":"Aktarim Musteri AS","durum":"Hatali","mesajlar":\["[^"]*tarihi'

echo "--- Cari kayit"
yukle "$C?$TARIH" "$T" "$DOS/cari_aktarim.xlsx"
kontrol 200 "Hatasiz satirlar aktarildi" '"kaydedildiMi":true,"toplam":6,"eklenecek":3'
istek GET "/api/cari/hesaplar?arama=Aktarim%20Musteri" "$T"
kontrol 200 "Otomatik kod ve borc bakiyesi" '"kod":"C[0-9]+","unvan":"Aktarim Musteri AS".*"bakiye[^:]*":1500\.50?'
istek GET "/api/cari/hesaplar?arama=Aktarim%20Tedarikci" "$T"
kontrol 200 "Metin hucresindeki virgullu tutar, alacak bakiyesi" '"unvan":"Aktarim Tedarikci Ltd","tur":"Tedarikci","grupAdi":"Bayi".*"bakiye[^:]*":-750\.25'
istek GET "/api/cari/hesaplar?arama=Euro" "$T"
kontrol 200 "Dovizli cari kurla TL karsiligi" '"paraBirimi":"EUR".*"bakiye[^:]*":200.*:8000'
istek GET /api/cari/gruplar "$T"
kontrol 200 "Grup bir kez acildi" '"ad":"Bayi"'
if [ "$(echo "$GOVDE" | grep -o '"ad":"Bayi"' | wc -l)" != "1" ]; then KOD=cift-grup; fi
kontrol 200 "Bayi grubu tektir"
yukle "$C?$TARIH" "$T" "$DOS/cari_aktarim.xlsx"
kontrol 200 "Ayni dosya yeniden yuklenince tekrar eklenmez" '"eklenecek":0,"mukerrer":4,"hatali":2'
istek GET /api/cari/hesaplar "$T2"
kontrol 200 "Baska firma aktarilan carileri gormez" '"toplamKayit":0'

echo "--- Stok"
yukle "$S/onizleme?$TARIH" "$T" "$DOS/stok_aktarim.xlsx"
kontrol 200 "Stok onizleme sayilari" '"toplam":8,"eklenecek":3,"mukerrer":1,"hatali":4'
kontrol 200 "Adet biriminde kesirli miktar hatasi" '"ad":"Yarim Kalem","durum":"Hatali"'
kontrol 200 "Maliyetsiz acilis hatasi" '"ad":"Maliyetsiz","durum":"Hatali","mesajlar":\["[^"]*Maliyet'
kontrol 200 "Olmayan depo hatasi" '"ad":"Yok Depo","durum":"Hatali","mesajlar":\[[^]]*depo'
kontrol 200 "KDV bos hatasi" '"ad":"Oransiz","durum":"Hatali","mesajlar":\["[^"]*KDV'
yukle "$S?$TARIH" "$T" "$DOS/stok_aktarim.xlsx"
kontrol 200 "Stok kartlari aktarildi" '"kaydedildiMi":true,"toplam":8,"eklenecek":3'
istek GET "/api/stok/kartlar?arama=Aktarim%20Kalem" "$T"
kontrol 200 "Otomatik kod, barkod ve acilis miktari" '"kod":"S[0-9]+","ad":"Aktarim Kalem".*"barkod":"8690000000017".*"miktar":100'
istek GET "/api/stok/kartlar?arama=Aktarim%20Un" "$T"
kontrol 200 "Kilo birimi ve yuzde bicimli KDV, kesirli miktar" '"temelBirim":"KG".*"miktar":12\.5'
istek GET "/api/stok/kartlar?arama=Aktarim%20Hizmeti" "$T"
kontrol 200 "Hizmet karti" '"tur":"Hizmet","temelBirim":"SAAT"'
yukle "$S?$TARIH" "$T" "$DOS/stok_aktarim.xlsx"
kontrol 200 "Stok dosyasi yeniden yuklenince tekrar eklenmez" '"eklenecek":0,"mukerrer":4'

echo "--- Yetki"
istek POST $Y/roller "$T" '{"ad":"Yalniz Kart","yetkiler":["cari.hesap.goruntule","cari.hesap.ekle"]}'
kontrol 201 "Yalniz kart yetkili rol acildi"
davet_kabul "$T" '{"ad":"Kart","soyad":"Yetkili","eposta":"kart'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Yalniz"}'
KT=$(alan accessToken)
yukle "$C/onizleme?$TARIH" "$KT" "$DOS/cari_aktarim.xlsx"
kontrol 200 "Hareket yetkisi yoksa bakiyeli yeni satir hatali" '"ad":"Dolar Cari","durum":"Hatali","mesajlar":\[[^]]*yetkiniz'
yukle "$S/onizleme?$TARIH" "$KT" "$DOS/stok_aktarim.xlsx"
kontrol 403 "Stok yetkisi olmayan stok aktaramaz" yetki_yok

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
