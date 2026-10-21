#!/usr/bin/env bash
# Çek karekodu çözümleme ve karekodlu giriş uçtan uca senaryosu. Kullanım: karekod_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_karekod"; rm -rf "$DIR"; mkdir -p "$DIR"
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
CS=/api/kasabanka/cek-senet
KK='0064|1234|0012345|7654321|9876543217'
giris() { # TOKEN CARI KAREKOD SERI
  istek POST "$CS/giris" "$1" '{"cariHesapId":"'"$2"'","tarih":"2026-10-21","riskOnaylandi":true,"evraklar":[{"tur":"Cek","seriNo":"'"$4"'","vadeTarihi":"2026-12-30","tutar":4500,"paraBirimi":"TRY","borclu":{"ad":"Karekod Kesidecisi","kimlikNo":"9876543217"},"bankaAdi":"Is Bankasi","sube":"1234","hesapNo":"0012345","karekod":"'"$3"'"}]}'
}

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Karekod Test","ad":"K","soyad":"T","eposta":"karekod'"$U"'@ornek.com","sifre":"Sifre1234"}'; T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger","ad":"D","soyad":"F","eposta":"karekoddiger'"$U"'@ornek.com","sifre":"Sifre1234"}'; T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Musteri","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C=$(alan id)
istek POST /api/cari/hesaplar "$T2" '{"unvan":"Musteri2","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C2=$(alan id)

istek POST "$CS/karekod-coz" "$T" '{"metin":"  '"$KK"' "}'
kontrol 200 "Karekod cozuldu: seri, banka, sube, hesap, VKN" '"ham":"0064\|1234\|0012345\|7654321\|9876543217","seriNo":"7654321","bankaKodu":"0064","bankaAdi":"Türkiye İş Bankası","subeKodu":"1234","hesapNo":"0012345","iban":null,"kimlikNo":"9876543217","borcluId":null,"borcluAdi":null,"zatenKayitli":false'
istek POST "$CS/karekod-coz" "$T" '{"metin":""}'
kontrol 400 "Bos karekod reddedildi"
istek POST "$CS/karekod-coz" "$T" '{"metin":"ABCDEFG"}'
kontrol 200 "Taninmayan icerik: alanlar bos, ham saklanir" '"ham":"ABCDEFG","seriNo":null,"bankaKodu":null'

giris "$T" "$C" "$KK" "7654321"
kontrol 201 "Karekodlu cek portfoye alindi"
istek POST "$CS/karekod-coz" "$T" '{"metin":"'"$KK"'"}'
kontrol 200 "Ayni karekod: zaten kayitli, kesideci karti bulundu" '"borcluId":"[^"]+","borcluAdi":"Karekod Kesidecisi","zatenKayitli":true'
giris "$T" "$C" "$KK" "9999999"
kontrol 409 "Ayni karekod (farkli seri yazilsa da) ikinci kez alinamaz" evrak_zaten_kayitli
istek POST "$CS/karekod-coz" "$T2" '{"metin":"'"$KK"'"}'
kontrol 200 "Baska firma icin kayitli degil, kesideci karti gorunmez" '"borcluId":null,"borcluAdi":null,"zatenKayitli":false'
giris "$T2" "$C2" "$KK" "7654321"
kontrol 201 "Baska firma ayni ceki kendi portfoyune alabilir"

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
