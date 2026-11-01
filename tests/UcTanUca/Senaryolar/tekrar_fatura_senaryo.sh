#!/usr/bin/env bash
# Tekrarlayan fatura uctan uca senaryosu. Kullanim: tekrar_fatura_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_tekrar_fatura"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim
P=/api/fatura/tekrar-planlari

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
SATIR='{"ad":"Kira","birim":"ADET","miktar":1,"birimFiyat":1000,"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0}'
plan() { # AD CARI BASLANGIC GUN [EK_ALANLAR]
  echo '{"ad":"'"$1"'","cariHesapId":"'"$2"'","siklik":"Aylik","gun":'"$4"',"baslangic":"'"$3"'","vadeGun":10,"aciklama":"{ay} {yil} kira bedeli","otomatikKesinlestir":false,"stopajOrani":0,"satirlar":['"$SATIR"']'"${5:+,$5}"'}'
}
# Gecmis tarihler: bugune gore hesaplanir ki senaryo her gun ayni calissin.
ay_once() { date -d "$(date +%Y-%m-01) -$1 month" +%Y-%m-01; }

IKI_AY=$(ay_once 2); BU_AY=$(date +%Y-%m-01)

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Tekrar Test AS","ad":"Tuna","soyad":"Kira","eposta":"tekrar'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Tekrar AS","ad":"D","soyad":"F","eposta":"tekrardiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Kiraci Ltd","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C=$(alan id)

echo "--- Dogrulama"
istek POST $P "$T" "$(plan '' "$C" "$IKI_AY" 1)"
kontrol 400 "Plan adi zorunlu"
istek POST $P "$T" "$(plan 'Kotu gun' "$C" "$IKI_AY" 32)"
kontrol 400 "Ayin gunu 1-31"
istek POST $P "$T" '{"ad":"Satirsiz","cariHesapId":"'"$C"'","siklik":"Aylik","gun":1,"baslangic":"'"$IKI_AY"'","otomatikKesinlestir":false,"stopajOrani":0,"satirlar":[]}'
kontrol 400 "En az bir satir"
istek POST $P "$T" "$(plan 'Olmayan cari' 00000000-0000-7000-8000-000000000001 "$IKI_AY" 1)"
if [ "$KOD" = 201 ]; then KOD=kabul; fi
kontrol "$KOD" "Olmayan cari kaydedilmedi"
istek POST $P "$T" "$(plan 'Ters bitis' "$C" "$IKI_AY" 1 '"bitis":"2000-01-01"')"
kontrol 400 "Bitis baslangictan once olamaz" tekrar_bitis
istek POST $P "$T" "$(plan 'Eksik artis' "$C" "$IKI_AY" 1 '"artisOrani":10')"
kontrol 400 "Artis orani ayla birlikte" tekrar_artis

echo "--- Taslak plan: kacan donemler"
istek POST $P "$T" "$(plan 'Depo kirasi' "$C" "$IKI_AY" 1)"
kontrol 201 "Plan acildi" '"kod":"TP[0-9]+".*"sonrakiTarih":"'"$IKI_AY"'"'; K=$(alan id)
istek POST "$P/$K/uret" "$T" '{}'
kontrol 200 "Kacan uc donem kesildi" '"uretilen":3,"hata":null'
UC=$(echo "$GOVDE" | grep -o '"donem":"[^"]*' | wc -l)
if [ "$UC" != 3 ]; then KOD=eksik; fi
kontrol 200 "Gecmiste uc kayit"
F1=$(echo "$GOVDE" | grep -o '"faturaId":"[^"]*' | head -1 | cut -d'"' -f4)
istek GET "/api/fatura/faturalar/$F1" "$T"
kontrol 200 "Fatura taslak, satis, donem tarihli, aciklamada ay" '"tur":"Satis".*"durum":"Taslak".*"tarih":"'"$BU_AY"'"'
if ! echo "$GOVDE" | grep -q 'kira bedeli'; then KOD=aciklamasiz; fi
kontrol 200 "Aciklama sablondan"
istek GET "/api/cari/hesaplar/$C" "$T"
kontrol 200 "Taslak cariyi etkilemedi" '"bakiye":0(\.0+)?[,}]'
istek POST "$P/$K/uret" "$T" '{}'
kontrol 422 "Tarihi gelmeyen donem kesilmez" tekrar_tarih_gelmedi
istek GET "/api/bildirim?boyut=20" "$T"
kontrol 200 "Onay bekliyor bildirimi" 'Onay bekliyor: Depo kirasi'
istek POST "/api/fatura/faturalar/$F1/onayla" "$T" '{}'
kontrol 200 "Taslak onaylandi"
istek GET "/api/cari/hesaplar/$C" "$T"
kontrol 200 "Onaylaninca cari borclandi" '"bakiye":1200(\.0+)?[,}]'

echo "--- Otomatik kesinlestirme"
istek POST $P "$T" '{"ad":"Yazilim aboneligi","cariHesapId":"'"$C"'","siklik":"Aylik","gun":1,"baslangic":"'"$BU_AY"'","vadeGun":0,"otomatikKesinlestir":true,"stopajOrani":0,"satirlar":['"$SATIR"']}'
kontrol 201 "Otomatik plan acildi" '"otomatikKesinlestir":true'; O=$(alan id)
istek POST "$P/$O/uret" "$T" '{}'
kontrol 200 "Bir donem kesildi" '"uretilen":1'
F2=$(echo "$GOVDE" | grep -o '"faturaId":"[^"]*' | head -1 | cut -d'"' -f4)
istek GET "/api/fatura/faturalar/$F2" "$T"
kontrol 200 "Fatura kesinlesti, numara aldi" '"durum":"Onayli","numara":"[A-Z0-9]+'
istek GET "/api/cari/hesaplar/$C" "$T"
kontrol 200 "Cari kesinlesen faturayla borclandi" '"bakiye":2400(\.0+)?[,}]'

echo "--- Yillik artis"
GECEN_YIL=$(date -d "-1 year" +%Y)
istek POST $P "$T" '{"ad":"Artisli kira","cariHesapId":"'"$C"'","siklik":"Yillik","gun":1,"baslangic":"'"$GECEN_YIL"'-01-01","otomatikKesinlestir":false,"stopajOrani":0,"artisOrani":25,"artisAyi":1,"satirlar":['"$SATIR"']}'
kontrol 201 "Artisli plan"; A=$(alan id)
istek POST "$P/$A/uret" "$T" '{}'
kontrol 200 "Iki yil kesildi, ikincisinde artis" '"uretilen":2.*"birimFiyat":1250.*"uygulananArtis":25'
FA=$(echo "$GOVDE" | grep -o '"faturaId":"[^"]*' | head -1 | cut -d'"' -f4)
istek GET "/api/fatura/faturalar/$FA" "$T"
kontrol 200 "Yeni fatura artisli fiyatla" '"birimFiyat":1250'

echo "--- Hata ve duraklatma"
istek POST /api/cari/hesaplar "$T" '{"unvan":"Pasif Olacak","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; PC=$(alan id)
istek POST $P "$T" "$(plan 'Duracak plan' "$PC" "$BU_AY" 1)"; D=$(alan id)
istek POST "$P/$D/duraklat" "$T" '{}'
kontrol 200 "Plan duraklatildi" '"durum":"Duraklatildi"'
istek POST "$P/$D/uret" "$T" '{}'
kontrol 422 "Duraklatilan plan uretmez" tekrar_pasif
istek POST "$P/$D/devam" "$T" '{}'
kontrol 200 "Plan devam etti" '"durum":"Aktif"'
istek DELETE "/api/cari/hesaplar/$PC" "$T"
kontrol 204 "Planin carisi silindi"
istek POST "$P/$D/uret" "$T" '{}'
kontrol 200 "Hatali donem durdu, sira ilerlemedi" '"uretilen":0,"hata":"[^"]+".*"sonrakiTarih":"'"$BU_AY"'".*"sonHata":"[^"]+'
istek GET "$P/$D" "$T"
kontrol 200 "Hata planda duruyor" '"sonHata":"[^"]+'
istek GET "/api/bildirim?boyut=30" "$T"
kontrol 200 "Hata bildirimi kritik" 'Fatura kesilemedi: Duracak plan'
istek GET "$P?durum=Duraklatildi" "$T"
kontrol 200 "Durum filtresi" '"toplamKayit":0'
istek GET "$P?arama=Depo" "$T"
kontrol 200 "Arama ve cari unvani" '"cariUnvan":"Kiraci Ltd".*"toplamKayit":1'

echo "--- Duzenleme, silme"
S=$(istek GET "$P/$K" "$T"; alan surum)
istek PUT "$P/$K" "$T" "$(plan 'Depo kirasi yeni' "$C" "$IKI_AY" 15 '"surum":'"$S")"
kontrol 200 "Plan degisti, kesilmis donem yeniden uretilmez" '"ad":"Depo kirasi yeni".*"gun":15'
if [ "$(alan sonrakiTarih)" \< "$BU_AY" ]; then KOD=geri; fi
kontrol 200 "Sira kesilen donemlerden sonra"
istek PUT "$P/$K" "$T" "$(plan 'Eski surum' "$C" "$IKI_AY" 15 '"surum":1')"
if [ "$KOD" = 200 ]; then KOD=eski_kabul; fi
kontrol "$KOD" "Eski surumle degistirilemez"
istek DELETE "$P/$K" "$T"
kontrol 204 "Plan silindi"
istek GET "/api/fatura/faturalar/$F1" "$T"
kontrol 200 "Plan silinince kesilmis fatura duruyor" '"durum":"Onayli"'
istek GET "$P/$K" "$T"
kontrol 404 "Silinen plan bulunamaz" tekrar_bulunamadi

echo "--- Yetki"
istek POST $Y/roller "$T" '{"ad":"Tekrar Goren","yetkiler":["fatura.tekrar.goruntule"]}'
istek POST $Y/roller "$T" '{"ad":"Tekrar Yazan","yetkiler":["fatura.tekrar.goruntule","fatura.tekrar.ekle"]}'
davet_kabul "$T" '{"ad":"Goren","soyad":"Kisi","eposta":"tekrargoren'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Tekrar Goren"}'; TG=$(alan accessToken)
davet_kabul "$T" '{"ad":"Yazan","soyad":"Kisi","eposta":"tekraryazan'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Tekrar Yazan"}'; TY=$(alan accessToken)
istek GET "$P/$O" "$TG"
kontrol 200 "Goren plani gorur"
istek POST $P "$TG" "$(plan 'Yetkisiz' "$C" "$BU_AY" 1)"
kontrol 403 "Goren plan acamaz" yetki_yok
istek POST "$P" "$TY" "$(plan 'Fatura yetkisiz' "$C" "$BU_AY" 1)"
kontrol 403 "Fatura kesme yetkisi olmadan plan acilamaz" yetki_yok
istek POST "$P/$O/uret" "$TY" '{}'
kontrol 403 "Fatura kesme yetkisi olmadan uretilemez" yetki_yok

echo "--- Izolasyon"
istek GET "$P/$O" "$T2"
kontrol 404 "Baska firma plani goremez" tekrar_bulunamadi
istek POST "$P/$O/uret" "$T2" '{}'
kontrol 404 "Baska firma uretemez" tekrar_bulunamadi
istek GET "$P" "$T2"
kontrol 200 "Baska firma listesi bos" '"toplamKayit":0'
istek POST $P "$T2" "$(plan 'Baskasinin carisi' "$C" "$BU_AY" 1)"
if [ "$KOD" = 201 ]; then KOD=kabul; fi
kontrol "$KOD" "Baska firmanin carisiyle plan acilmaz"

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
