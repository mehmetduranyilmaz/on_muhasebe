#!/usr/bin/env bash
# Ek zorunlu alanlar uçtan uca senaryosu. Kullanım: zorunlu_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_zorunlu"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
ZA=/api/yonetim/zorunlu-alanlar

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
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:500}"; KALAN=$((KALAN+1))
  fi
}
cari() { echo '{"unvan":"'"$1"'","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"'"${2:+,$2}"'}'; }

U=$(date +%s)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Zorunlu Test AS","ad":"Zeki","soyad":"Ak","eposta":"zorunlu'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"zorunludiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" "$(cari "Eski Musteri")"; ESKI=$(alan id); ESKI_SURUM=$(alan surum)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Kasa","tur":"Kasa","paraBirimi":"TRY"}'; KASA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Banka","tur":"Banka","paraBirimi":"TRY"}'; BANKA=$(alan id)

echo "--- Tanimlar ve kayit"
istek GET $ZA "$T"
kontrol 200 "Lisansli modullerin tanimlari geldi, secim bos" '"kod":"cari.kart.telefon","modul":"cari","form":"Cari kartı","ad":"Telefon".*"kod":"kasabanka.bordro.senetKefil".*"secili":\[\]'
istek PUT $ZA "$T" '{"secili":["cari.kart.unvan"]}'
kontrol 400 "Sistem alani ya da bilinmeyen kod zorunlu yapilamaz" zorunlu_alan_gecersiz
istek PUT $ZA "$T" '{"secili":["cari.kart.telefon","cari.kart.grup","stok.kart.barkod","fatura.belge.vadeTarihi","kasabanka.islem.belgeNo","kasabanka.bordro.aciklama","kasabanka.bordro.senetKesideYeri"]}'
kontrol 200 "Zorunlu alanlar kaydedildi" '"secili":\["cari.kart.telefon","cari.kart.grup","stok.kart.barkod","fatura.belge.vadeTarihi","kasabanka.islem.belgeNo","kasabanka.bordro.aciklama","kasabanka.bordro.senetKesideYeri"\]'
istek GET $ZA "$T2"
kontrol 200 "Baska firmanin secimi etkilenmedi" '"secili":\[\]'

echo "--- Cari"
istek POST /api/cari/hesaplar "$T" "$(cari "Yeni Musteri")"
kontrol 400 "Telefon ve grup bos cari kaydedilmez" 'Telefon zorunludur \(firma ayarı\)\..*Grup zorunludur'
istek POST /api/cari/gruplar "$T" '{"ad":"Toptan"}'; GRUP=$(alan id)
istek POST /api/cari/hesaplar "$T" "$(cari "Yeni Musteri" '"telefon":"0212 555 00 00","grupId":"'"$GRUP"'"')"
kontrol 201 "Zorunlu alanlar dolu cari kaydedildi"; C1=$(alan id)
istek PUT "/api/cari/hesaplar/$ESKI" "$T" '{"unvan":"Eski Musteri 2","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","surum":'"$ESKI_SURUM"'}'
kontrol 400 "Kural oncesi acilan kayit duzenlenirken eksik alan istendi" 'Telefon zorunludur'
istek PUT "/api/cari/hesaplar/$ESKI" "$T" '{"unvan":"Eski Musteri 2","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","telefon":"0216 111","grupId":"'"$GRUP"'","surum":'"$ESKI_SURUM"'}'
kontrol 200 "Eksik alan tamamlaninca eski kayit guncellendi"
istek POST /api/cari/hesaplar "$T2" "$(cari "Baska Firma Carisi")"
kontrol 201 "Kuralsiz firmada ayni istek kabul edildi"

echo "--- Stok ve fatura"
istek POST /api/stok/kartlar "$T" '{"ad":"Kalem","tur":"Urun","temelBirim":"ADET","kdvOrani":20,"paraBirimi":"TRY"}'
kontrol 400 "Barkodsuz stok karti kaydedilmez" 'Barkod zorunludur'
istek POST /api/stok/kartlar "$T" '{"ad":"Kalem","tur":"Urun","temelBirim":"ADET","kdvOrani":20,"paraBirimi":"TRY","barkod":"8690000000011"}'
kontrol 201 "Barkodlu stok karti kaydedildi"
FATURA='{"tur":"Satis","cariHesapId":"'"$C1"'","tarih":"2026-10-10","genelIskontoTuru":"Yuzde","genelIskonto":0,"satirlar":[{"ad":"Hizmet","birim":"ADET","miktar":1,"birimFiyat":100,"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0}]'
istek POST /api/fatura/faturalar "$T" "$FATURA}"
kontrol 400 "Vadesiz fatura kaydedilmez" 'Vade tarihi zorunludur'
istek POST /api/fatura/faturalar "$T" "$FATURA"',"vadeTarihi":"2026-11-10"}'
kontrol 201 "Vadeli fatura kaydedildi"

echo "--- Kasa/banka ve cek/senet"
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$KASA"'","cariHesapId":"'"$C1"'","tarih":"2026-10-11","tutar":50}'
kontrol 400 "Belge nosuz tahsilat kaydedilmez" 'Belge no \(tahsilat, ödeme, gelir/gider\) zorunludur'
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$KASA"'","cariHesapId":"'"$C1"'","tarih":"2026-10-11","tutar":50,"belgeNo":"MAK-1"}'
kontrol 201 "Belge nolu tahsilat kaydedildi"
istek POST /api/kasabanka/islemler/virman "$T" '{"kaynakHesapId":"'"$KASA"'","hedefHesapId":"'"$BANKA"'","tarih":"2026-10-12","tutar":10}'
kontrol 201 "Belge no virmanda istenmez (aciklama secilmedi)"
SENET='{"tur":"Senet","seriNo":"S1","vadeTarihi":"2026-12-01","tutar":100,"paraBirimi":"TRY","borclu":{"ad":"Borclu Kisi"}}'
istek POST /api/kasabanka/cek-senet/giris "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-12","riskOnaylandi":false,"evraklar":['"$SENET"']}'
kontrol 400 "Bordro aciklamasi ve senet keside yeri istendi" 'Bordro açıklaması zorunludur.*Senette keşide yeri zorunludur'
istek POST /api/kasabanka/cek-senet/giris "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-12","aciklama":"Ekim","riskOnaylandi":false,"evraklar":[{"tur":"Senet","seriNo":"S1","vadeTarihi":"2026-12-01","tutar":100,"paraBirimi":"TRY","borclu":{"ad":"Borclu Kisi"},"kesideYeri":"Istanbul"}]}'
kontrol 201 "Eksiksiz bordro kaydedildi"

echo "--- Kural kaldirma"
istek PUT $ZA "$T" '{"secili":[]}'
kontrol 200 "Tum zorunluluklar kaldirildi" '"secili":\[\]'
istek POST /api/cari/hesaplar "$T" "$(cari "Serbest Musteri")"
kontrol 201 "Onbellek temizlendi: kural hemen kalkti"

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
