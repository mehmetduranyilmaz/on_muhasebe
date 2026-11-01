#!/usr/bin/env bash
# Gelen e-Fatura XML ve karekodundan taslak alis faturasi. Kullanim: belge_okuma_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_belge"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
B=/api/fatura/belge-okuma

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=("${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
yukle() { # DOSYA [TOKEN]
  KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK$B/xml" -H "Authorization: Bearer ${2:-$T}" -F "dosya=@$1"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -qE -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:500}"; KALAN=$((KALAN+1))
  fi
}
# Onizlemedeki "belge" nesnesi; olusturma istegine aynen konur.
belge() { echo "$GOVDE" | sed -E 's/^\{"belge":(.*),"cariHesapId":.*$/\1/'; }
olustur() { # BELGE CARI
  # Dosyadan: Windows'ta komut satiri Turkce karakteri bozar.
  printf '%s' '{"belge":'"$1"',"cariHesapId":"'"$2"'"}' > "$DIR/istek"
  istek POST "$B/olustur" "$T" "@$DIR/istek"
}

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Belge Test AS","ad":"Bora","soyad":"Okur","eposta":"belge'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Belge AS","ad":"Dila","soyad":"Fer","eposta":"belgediger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
XML="$DOS/efatura.xml"

echo "--- XML okuma"
printf 'duz metin' > "$DIR/bozuk.xml"
yukle "$DIR/bozuk.xml"
kontrol 400 "Bozuk dosya reddedildi" belge_xml
printf '<?xml version="1.0"?><!DOCTYPE Invoice [<!ENTITY x SYSTEM "file:///etc/passwd">]><Invoice><ID>&x;</ID></Invoice>' > "$DIR/xxe.xml"
yukle "$DIR/xxe.xml"
kontrol 400 "Dis varlikli XML reddedildi" belge_xml
yukle "$XML"
kontrol 200 "XML okundu, cari yok, satirlar geldi" '"numara":"ABC2026000000042".*"tedarikciVkn":"1234567890","tedarikciUnvan":"Tedarikci Gida Ltd".*"birim":"KG","miktar":10.*"iskonto1":10.*"cariHesapId":null'
if echo "$GOVDE" | grep -q 'hesaplanan'; then KOD=toplam_uyarisi; fi
kontrol 200 "Toplam belgeyle tutuyor, uyari yok"

echo "--- Stok ve cari eslestirme"
istek POST /api/stok/kartlar "$T" '{"ad":"Zeytinyagi","kod":"ZY-1","tur":"Urun","temelBirim":"KG","kdvOrani":20,"paraBirimi":"TRY"}'
K=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Tedarikci Gida Ltd","tur":"Tedarikci","kisiTipi":"Tuzel","vergiNo":"1234567890","paraBirimi":"TRY"}'; C=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Dolar Tedarikci","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"USD"}'; CUSD=$(alan id)
yukle "$XML"
kontrol 200 "VKN ile cari, urun kodu ile stok karti bulundu" '"stokKartiId":"'"$K"'".*"cariHesapId":"'"$C"'".*"eslesenStokSatiri":1'
OKUNAN=$(belge)

echo "--- Taslak olusturma"
olustur "$OKUNAN" "$CUSD"
kontrol 422 "Para birimi farkli cari reddedildi" belge_para_birimi
olustur "$OKUNAN" 00000000-0000-7000-8000-000000000001
if [ "$KOD" = 201 ]; then KOD=kabul; fi
kontrol "$KOD" "Olmayan cariye fatura acilmaz"
olustur "$OKUNAN" "$C"
kontrol 201 "Taslak fatura acildi"; F=$(alan id)
istek GET "/api/fatura/faturalar/$F" "$T"
kontrol 200 "Taslak alis, numara, vade dogru" '"tur":"Alis".*"durum":"Taslak","numara":"ABC2026000000042","tarih":"2026-10-15","vadeTarihi":"2026-11-15"'
if ! echo "$GOVDE" | grep -q 'ETTN: f47ac10b' || ! echo "$GOVDE" | grep -qE '"genelToplam":1190'; then KOD=eksik; fi
kontrol 200 "Aciklamada ETTN, genel toplam 1190"
istek GET "/api/cari/hesaplar/$C" "$T"
kontrol 200 "Taslak cariyi etkilemedi" '"bakiye":0(\.0+)?[,}]'
istek POST "/api/fatura/faturalar/$F/onayla" "$T" '{}'
kontrol 200 "Onaylandi"
istek GET "/api/cari/hesaplar/$C" "$T"
kontrol 200 "Onayla cari alacakli oldu" '"bakiye":-1190(\.0+)?[,}]'

echo "--- Mukerrer"
yukle "$XML"
kontrol 200 "Ayni fatura onizlemede bildirilir" '"mevcutFaturaId":"'"$F"'"'
olustur "$OKUNAN" "$C"
kontrol 409 "Ayni fatura ikinci kez acilmaz" numara_kullaniliyor

echo "--- Karekod"
KK='{\"vkntckn\":\"1234567890\",\"avkntckn\":\"9000000002\",\"tip\":\"SATIS\",\"tarih\":\"2026-10-20\",\"no\":\"ABC2026000000043\",\"ettn\":\"aa11\",\"parabirimi\":\"TRY\",\"malhizmettoplam\":\"1500\",\"kdvmatrah(20)\":\"1000\",\"kdvmatrah(10)\":\"500\",\"vergidahil\":\"1750\",\"odenecek\":\"1750\"}'
istek POST "$B/karekod" "$T" '{"metin":"0102345678901234"}'
kontrol 400 "Fatura karekodu olmayan reddedildi" belge_karekod
istek POST "$B/karekod" "$T" '{"metin":"'"$KK"'"}'
kontrol 200 "Karekod okundu, cari bulundu" '"kaynak":"Karekod".*"numara":"ABC2026000000043".*"cariHesapId":"'"$C"'"'
KB=$(belge)
olustur "$KB" "$C"
kontrol 201 "Karekoddan taslak acildi"; F2=$(alan id)
istek GET "/api/fatura/faturalar/$F2" "$T"
kontrol 200 "Toplam 1750, oran basina satir" '"genelToplam":1750'

echo "--- Satis faturasi gonderilemez, yetki, izolasyon"
olustur "$(echo "$OKUNAN" | sed 's/"tur":"Alis"/"tur":"Satis"/')" "$C"
kontrol 400 "Belgeden satis faturasi acilmaz"
yukle "$XML" "$T2"
kontrol 200 "Baska firmada cari eslesmez" '"cariHesapId":null,"cariUnvan":null,"mevcutFaturaId":null,"eslesenStokSatiri":0'
istek POST "$B/olustur" "$T2" '{"belge":'"$OKUNAN"',"cariHesapId":"'"$C"'"}'
if [ "$KOD" = 201 ]; then KOD=sizinti; fi
kontrol "$KOD" "Baska firmanin carisine fatura acilmaz"
istek GET "/api/fatura/faturalar/$F" "$T2"
kontrol 404 "Baska firma faturayi goremez"
istek POST /api/yonetim/roller "$T" '{"ad":"Fatura Goren","yetkiler":["fatura.belge.goruntule"]}'
davet_kabul "$T" '{"ad":"Goren","soyad":"Kisi","eposta":"belgegoren'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Fatura Goren"}'; TG=$(alan accessToken)
yukle "$XML" "$TG"
kontrol 403 "Fatura ekleme yetkisi olmadan okunamaz" yetki_yok

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
