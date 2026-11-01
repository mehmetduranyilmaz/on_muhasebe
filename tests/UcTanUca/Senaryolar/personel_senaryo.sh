#!/usr/bin/env bash
# Personel modulu uctan uca senaryosu. Kullanim: personel_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_personel"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
P=/api/personel
Y=/api/yonetim

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
bakiye() { istek GET "/api/kasabanka/hesaplar/$1" "$T"; kontrol 200 "$2" "\"bakiye\":$3(\.0+)?,"; }
satir_id() { echo "$GOVDE" | sed 's/},{/}\n{/g' | grep "\"adSoyad\":\"$1\"" | grep -o '"id":"[^"]*' | tail -1 | cut -d'"' -f4; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Personel Test AS","ad":"Pelin","soyad":"Patron","eposta":"personel'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Personel AS","ad":"Dila","soyad":"Fer","eposta":"personeldiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Merkez Kasa","tur":"Kasa","paraBirimi":"TRY"}'; KS=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Ana Banka","tur":"Banka","paraBirimi":"TRY"}'; BN=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Sirket Karti","tur":"KrediKarti","paraBirimi":"TRY","limit":1000}'; KK=$(alan id)
istek POST /api/kasabanka/islemler/acilis "$T" '{"hesapId":"'"$BN"'","tarih":"2026-01-01","tutar":100000}'
istek POST /api/kasabanka/islemler/acilis "$T" '{"hesapId":"'"$KS"'","tarih":"2026-01-01","tutar":10000}'

echo "--- Kart"
istek POST $P/kartlar "$T" '{"ad":"Ayse","soyad":"Kaya","tcKimlik":"12345678901","iseGiris":"2026-01-01"}'
kontrol 400 "Gecersiz TC reddedildi" tc_gecersiz
istek POST $P/kartlar "$T" '{"ad":"Ayse","soyad":"Kaya","iban":"TR00","iseGiris":"2026-01-01"}'
kontrol 400 "Gecersiz IBAN reddedildi" iban_gecersiz
istek POST $P/kartlar "$T" '{"ad":"","soyad":"Kaya","iseGiris":"2026-01-01"}'
kontrol 400 "Ad zorunlu"
istek POST $P/kartlar "$T" '{"ad":"Ayse","soyad":"Kaya","tcKimlik":"10000000146","iban":"TR33 0006 1005 1978 6457 8413 26","iseGiris":"2026-01-01","gorev":"Muhasebeci","netMaas":20000}'
kontrol 201 "Personel acildi" '"kod":"PR[0-9]+".*"tcKimlik":"10000000146".*"netMaas":20000'; A=$(alan id)
istek POST $P/kartlar "$T" '{"ad":"Ayni","soyad":"Tc","tcKimlik":"10000000146","iseGiris":"2026-01-01"}'
kontrol 409 "Ayni TC ikinci kez acilmaz" tc_kullaniliyor
istek POST $P/kartlar "$T" '{"ad":"Bora","soyad":"Demir","iseGiris":"2026-03-15","netMaas":15000}'; B=$(alan id)
istek POST $P/kartlar "$T" '{"ad":"Cem","soyad":"Sonra","iseGiris":"2026-09-01","netMaas":9000}'; C=$(alan id)
istek GET "$P/kartlar?arama=Kaya" "$T"
kontrol 200 "Arama" '"adSoyad":"Ayse Kaya".*"toplamKayit":1'
if echo "$GOVDE" | grep -q 'netMaas\|tcKimlik'; then KOD=sizinti; fi
kontrol 200 "Listede maas ve TC yok"

echo "--- Maas donemi"
istek POST $P/donemler "$T" '{"yil":2026,"ay":13}'
kontrol 400 "Gecersiz ay"
istek POST $P/donemler "$T" '{"yil":2020,"ay":1}'
kontrol 422 "Calisan yoksa donem acilmaz" donem_personel_yok
istek POST $P/avanslar "$T" '{"personelId":"'"$A"'","tarih":"2026-02-10","tutar":3000,"taksitSayisi":3,"aciklama":"Avans"}'
kontrol 400 "Avansta hesap zorunlu" hesap_gerekli
istek POST $P/avanslar "$T" '{"personelId":"'"$A"'","tarih":"2026-02-10","tutar":3000,"taksitSayisi":3,"hesapId":"'"$KK"'"}'
kontrol 422 "Kredi kartindan avans verilmez" hesap_uygun_degil
istek POST $P/avanslar "$T" '{"personelId":"'"$A"'","tarih":"2026-02-10","tutar":3000,"taksitSayisi":3,"hesapId":"'"$KS"'","aciklama":"Avans"}'
kontrol 201 "3 taksitli avans verildi" '"tutar":3000,"taksitSayisi":3,"kalan":3000'; AV=$(alan id)
bakiye "$KS" "Kasadan avans cikti" 7000
istek POST $P/donemler "$T" '{"yil":2026,"ay":3}'
kontrol 201 "Mart donemi acildi: yalniz calisanlar, avans taksiti dustu" '"durum":"Taslak"'; D=$(alan id)
SA=$(satir_id 'Ayse Kaya'); SB=$(satir_id 'Bora Demir')
KOD=200; if echo "$GOVDE" | grep -q 'Cem Sonra'; then KOD=gelecek; fi
kontrol 200 "Sonra giren personel donemde yok"
if ! echo "$GOVDE" | grep -q '"netMaas":20000.*"avansKesintisi":1000.*"acikAvans":3000.*"odenecek":19000'; then KOD=hesap; fi
kontrol 200 "Ayse: 20000 - 1000 avans taksiti = 19000"
istek POST $P/donemler "$T" '{"yil":2026,"ay":3}'
kontrol 409 "Ayni ay ikinci kez acilmaz" donem_var
istek PUT "$P/donemler/$D/satirlar/$SB" "$T" '{"netMaas":15000,"prim":1000,"kesinti":500,"avansKesintisi":0,"not":"Prim"}'
kontrol 200 "Bora: prim ve kesinti" '"prim":1000.*"kesinti":500.*"odenecek":15500'
istek PUT "$P/donemler/$D/satirlar/$SB" "$T" '{"netMaas":100,"prim":0,"kesinti":500,"avansKesintisi":0}'
kontrol 400 "Eksi odenecek olmaz" eksi_odenecek
istek PUT "$P/donemler/$D/satirlar/$SA" "$T" '{"netMaas":20000,"prim":0,"kesinti":0,"avansKesintisi":9000}'
kontrol 400 "Avans kesintisi acik avansi gecemez" avans_fazla
istek POST "$P/donemler/$D/ode" "$T" '{"tarih":"2026-03-31","hesapId":"'"$BN"'"}'
kontrol 422 "Onaysiz donem odenmez" donem_onayli_degil
istek POST "$P/donemler/$D/onayla" "$T" '{}'
kontrol 200 "Donem onaylandi" '"durum":"Onayli"'
istek GET "$P/avanslar?personelId=$A" "$T"
kontrol 200 "Avansin kalani dustu" '"kalan":2000'
istek PUT "$P/donemler/$D/satirlar/$SB" "$T" '{"netMaas":15000,"prim":0,"kesinti":0,"avansKesintisi":0}'
kontrol 422 "Onayli donem degismez" donem_taslak_degil
istek POST "$P/avanslar/$AV/iptal" "$T" '{}'
kontrol 422 "Dusulmus avans iptal edilemez" avans_mahsuplu
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/banka.xlsx" -w "%{http_code}" "$KOK$P/donemler/$D/banka-dosyasi" -H "Authorization: Bearer $T"); GOVDE=""
kontrol 200 "Banka odeme dosyasi indi"
if [ "$(head -c 2 "$DIR/banka.xlsx")" != "PK" ]; then KOD=bozuk; fi
kontrol 200 "Dosya xlsx"

echo "--- Odeme"
istek POST "$P/donemler/$D/ode" "$T" '{"tarih":"2026-03-31"}'
kontrol 400 "Odemede hesap zorunlu" hesap_gerekli
istek POST "$P/donemler/$D/ode" "$T" '{"tarih":"2026-03-31","hesapId":"'"$BN"'","satirIdleri":["'"$SA"'"],"tutar":50000}'
kontrol 400 "Kalandan fazla odenmez" tutar_fazla
istek POST "$P/donemler/$D/ode" "$T" '{"tarih":"2026-03-31","hesapId":"'"$BN"'","tutar":100}'
kontrol 400 "Toplu odemede kismi tutar olmaz" tek_satir
istek POST "$P/donemler/$D/ode" "$T" '{"tarih":"2026-03-31","hesapId":"'"$BN"'","satirIdleri":["'"$SA"'"],"tutar":4000}'
kontrol 200 "Kismi odeme" '"odenen":4000.*"kalan":15000'
bakiye "$BN" "Bankadan 4000 cikti" 96000
istek POST "$P/donemler/$D/geri-al" "$T" '{}'
kontrol 422 "Odemeli donem geri alinmaz" donem_odemeli
istek POST "$P/donemler/$D/ode" "$T" '{"tarih":"2026-03-31","hesapId":"'"$BN"'"}'
kontrol 200 "Kalan tum satirlar odendi" '"kalan":0'
bakiye "$BN" "Banka: 100000 - 19000 - 15500" 65500
istek POST "$P/donemler/$D/ode" "$T" '{"tarih":"2026-03-31","hesapId":"'"$BN"'"}'
kontrol 422 "Odenecek kalmadi" odenecek_yok
istek GET "$P/kartlar/$A/hesap" "$T"
kontrol 200 "Personel hesabi: kalan 2 taksit avans borcu" '"islem":"Avans".*"islem":"Maa.*tahakkuku".*"bakiye":-2000(.0+)?}$'

echo "--- Masraf"
istek POST $P/masraflar "$T" '{"personelId":"'"$A"'","tarih":"2026-03-20","kategori":"Yemek","tutar":0,"aciklama":"x"}'
kontrol 400 "Masraf tutari zorunlu"
istek POST $P/masraflar "$T" '{"personelId":"'"$A"'","tarih":"2026-03-20","kategori":"Yemek","tutar":450,"aciklama":"Musteri yemegi"}'
kontrol 201 "Masraf beyan edildi" '"durum":"Beyan"'; MR=$(alan id)
istek POST "$P/masraflar/$MR/onayla" "$T" '{}'
kontrol 422 "Beyan eden kendi girdigini onaylayamaz" kendi_masrafi

echo "--- Roller ve kendi masrafi"
istek POST $Y/roller "$T" '{"ad":"Calisan","yetkiler":["personel.kendi.goruntule","personel.kendi.ekle"]}'
istek POST $Y/roller "$T" '{"ad":"Onaylayan","yetkiler":["personel.masraf.goruntule","personel.masraf.onayla","personel.kart.goruntule"]}'
davet_kabul "$T" '{"ad":"Can","soyad":"Calisan","eposta":"pcalisan'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Calisan"}'; TC=$(alan accessToken)
davet_kabul "$T" '{"ad":"Onur","soyad":"Onay","eposta":"ponay'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Onaylayan"}'; TO=$(alan accessToken)
istek GET $P/benim "$TC"
kontrol 200 "Bagli degilse bos" '"personelId":null'
istek POST $P/benim/masraflar "$TC" '{"tarih":"2026-03-21","kategori":"Yol","tutar":120,"aciklama":"Taksi"}'
kontrol 422 "Bagli olmayan masraf giremez" bagli_personel_yok
istek GET $P/kartlar/kullanicilar "$T"
UC=$(echo "$GOVDE" | sed 's/},{/}\n{/g' | grep 'Can Calisan' | grep -o '"id":"[^"]*' | cut -d'"' -f4)
istek PUT "$P/kartlar/$B" "$T" '{"ad":"Bora","soyad":"Demir","iseGiris":"2026-03-15","netMaas":15000,"kullaniciId":"'"$UC"'","surum":'"$(istek GET "$P/kartlar/$B" "$T"; alan surum)"'}'
kontrol 200 "Kullanici karta baglandi" '"kullaniciId":"'"$UC"'"'
istek POST $P/benim/masraflar "$TC" '{"tarih":"2026-03-21","kategori":"Yol","tutar":120,"aciklama":"Taksi"}'
kontrol 201 "Calisan kendi masrafini girdi" '"adSoyad":"Bora Demir".*"durum":"Beyan"'; MK=$(alan id)
istek GET $P/benim "$TC"
kontrol 200 "Calisan kendi masrafini gorur" '"adSoyad":"Bora Demir".*"Taksi"'
if echo "$GOVDE" | grep -q 'Musteri yemegi'; then KOD=sizinti; fi
kontrol 200 "Baskasinin masrafi gorunmez"
istek GET $P/masraflar "$TC"
kontrol 403 "Calisan tum masraflari goremez" yetki_yok
istek GET "$P/kartlar/$A" "$TC"
kontrol 403 "Calisan kartlari goremez" yetki_yok
istek POST "$P/masraflar/$MK/onayla" "$TC" '{}'
kontrol 403 "Calisan onaylayamaz" yetki_yok
istek POST $Y/roller "$T" '{"ad":"Calisan Dosyali","yetkiler":["personel.kendi.goruntule","personel.kendi.ekle","dosya.ek.goruntule","dosya.ek.ekle"]}'
davet_kabul "$T" '{"ad":"Deniz","soyad":"Fisli","eposta":"pfisli'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Calisan Dosyali"}'; TF=$(alan accessToken)
istek GET $P/kartlar/kullanicilar "$T"
UF=$(echo "$GOVDE" | sed 's/},{/}\n{/g' | grep 'Deniz Fisli' | grep -o '"id":"[^"]*' | cut -d'"' -f4)
istek POST $P/kartlar "$T" '{"ad":"Deniz","soyad":"Fisli","iseGiris":"2026-01-01","kullaniciId":"'"$UF"'"}'
istek POST $P/benim/masraflar "$TF" '{"tarih":"2026-03-22","kategori":"Yemek","tutar":80,"aciklama":"Fisli masraf"}'; MF=$(alan id)
printf '%%PDF-1.4 fis' > "$DIR/fis.pdf"
dosya_yukle() { KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/dosya/kayit/personel-masrafi/$1" -H "Authorization: Bearer $2" -F "dosya=@$DIR/fis.pdf"); GOVDE=$(cat "$DIR/govde"); }
dosya_yukle "$MF" "$TF"
kontrol 201 "Calisan kendi masrafina fis ekledi" '"ad":"fis.pdf"'
dosya_yukle "$MK" "$TF"
kontrol 403 "Calisan baskasinin masrafina fis ekleyemez" yetki_yok
istek GET "/api/dosya/kayit/personel-masrafi/$MF" "$TF"
kontrol 200 "Calisan kendi fisini gorur" 'fis.pdf'
istek GET "/api/dosya/kayit/personel-masrafi/$MF" "$T"
kontrol 200 "Yonetici ayni fisi gorur" 'fis.pdf'
istek GET "/api/dosya/kayit/personel-masrafi/$MK" "$TF"
kontrol 403 "Calisan baskasinin fisini goremez" yetki_yok
istek POST "$P/masraflar/$MK/reddet" "$TO" '{"not":" "}'
kontrol 400 "Red gerekce ister" red_gerekcesi
istek POST "$P/masraflar/$MK/reddet" "$TO" '{"not":"Fis eksik"}'
kontrol 200 "Reddedildi" '"durum":"Reddedildi".*"not":"Fis eksik"'
S=$(istek GET $P/benim "$TC"; alan surum)
istek PUT "$P/benim/masraflar/$MK" "$TC" '{"tarih":"2026-03-21","kategori":"Yol","tutar":150,"aciklama":"Taksi fisli","surum":'"$S"'}'
kontrol 200 "Calisan duzeltti, yeniden onayda" '"tutar":150.*"durum":"Beyan"'
istek POST "$P/masraflar/$MK/onayla" "$TO" '{}'
kontrol 200 "Onaylandi" '"durum":"Onaylandi","kararVeren":"Onur Onay"'
istek POST "$P/masraflar/$MR/onayla" "$TO" '{}'
kontrol 200 "Patronun girdigini baskasi onayladi"
istek POST "$P/masraflar/$MK/ode" "$T" '{"tarih":"2026-03-25","hesapId":"'"$KS"'"}'
kontrol 200 "Masraf odendi" '"durum":"Odendi"'
bakiye "$KS" "Kasa: 7000 - 150" 6850

echo "--- Maas gizliligi"
istek GET "$P/kartlar/$A" "$TO"
kontrol 200 "Maas yetkisi yoksa TC maskeli, maas bos" '"tcKimlik":"100\*+46".*"netMaas":null'
istek GET "$P/donemler" "$TO"
kontrol 403 "Maas yetkisi yoksa donem goremez" yetki_yok
istek GET "$P/kartlar/$A/hesap" "$TO"
kontrol 403 "Maas yetkisi yoksa hesap goremez" yetki_yok

echo "--- Muhasebe fisi"
istek GET "/api/muhasebe/fisler?baslangic=2026-02-01&bitis=2026-03-31" "$T"
kontrol 200 "Fisler uretildi"
fisi_var() { if echo "$GOVDE" | tr -d '\n' | grep -qE "$1"; then KOD=200; else KOD=yok; fi; kontrol 200 "$2"; }
G="$GOVDE"
fisi_var '"hesapKodu":"196' "Avans ve mahsup 196 hesabina"
GOVDE="$G"; fisi_var '"hesapKodu":"335' "Odeme, mahsup ve masraf 335 hesabina"
GOVDE="$G"; fisi_var '"hesapKodu":"770' "Masraf gideri 770 hesabina"
GOVDE="$G"; KOD=200
if echo "$GOVDE" | tr -d '\n' | grep -qE 'tahakkuku'; then KOD=cift; fi
kontrol 200 "Maas tahakkuku fise girmedi: gider cift yazilmaz"

echo "--- Iptaller"
istek POST "$P/odemeler/00000000-0000-7000-8000-000000000001/iptal" "$T" '{}'
kontrol 404 "Olmayan odeme" odeme_bulunamadi
istek POST $P/kartlar "$T" '{"ad":"Iptal","soyad":"Kisi","iseGiris":"2026-01-01","netMaas":1000}'; IK=$(alan id)
istek POST $P/avanslar "$T" '{"personelId":"'"$IK"'","tarih":"2026-06-01","tutar":500,"taksitSayisi":1,"hesapId":"'"$KS"'"}'; IA=$(alan id)
bakiye "$KS" "Avansla kasa 500 azaldi" 6350
istek POST "$P/avanslar/$IA/iptal" "$T" '{}'
kontrol 204 "Avans iptal edildi"
bakiye "$KS" "Avans iptaliyle kasa geri geldi" 6850
istek POST $P/donemler "$T" '{"yil":2026,"ay":6}'; D6=$(alan id); S6=$(satir_id 'Iptal Kisi')
istek POST "$P/donemler/$D6/onayla" "$T" '{}'
istek POST "$P/donemler/$D6/ode" "$T" '{"tarih":"2026-06-30","hesapId":"'"$BN"'","satirIdleri":["'"$S6"'"]}'
kontrol 200 "Haziran odendi" '"adSoyad":"Iptal Kisi"[^}]*"odenen":1000'
bakiye "$BN" "Banka 1000 azaldi" 64500
istek GET "/api/kasabanka/hesaplar/$BN/ekstre?baslangic=2026-06-01&bitis=2026-06-30" "$T"
kontrol 200 "Kasa hareketinde personel odemesi" 'PersonelOdeme'
istek GET "$P/kartlar/$IK/odemeler" "$T"
kontrol 200 "Kisinin odemeleri listelenir" '"tur":"Maas".*"tutar":1000'
ODM=$(alan id)
istek POST "$P/odemeler/$ODM/iptal" "$T" '{}'
kontrol 204 "Odeme iptal edildi"
bakiye "$BN" "Iptalle banka geri geldi" 65500
istek GET "$P/donemler/$D6" "$T"
kontrol 200 "Satirin odenen tutari geri dondu" '"adSoyad":"Iptal Kisi"[^}]*"odenen":0'
istek POST "$P/odemeler/$ODM/iptal" "$T" '{}'
kontrol 422 "Ikinci iptal olmaz" odeme_iptal
istek POST "$P/donemler/$D6/geri-al" "$T" '{}'
kontrol 200 "Odemesi kalmayan donem geri alindi" '"durum":"Taslak"'

echo "--- Zimmet ve isten cikis"
istek POST $P/zimmetler "$T" '{"personelId":"'"$A"'","ad":"Telefon","seriNo":"S1","verilisTarihi":"2026-02-01","bedel":25000}'
kontrol 201 "Zimmet verildi" '"durum":"Zimmette"'; Z=$(alan id)
istek POST "$P/zimmetler/$Z/iade" "$T" '{"tarih":"2026-01-01","kayipMi":false}'
kontrol 400 "Iade verilisten once olamaz" iade_tarihi
istek DELETE "$P/kartlar/$A" "$T"
kontrol 422 "Hareketli personel silinmez" personel_hareketli
istek POST "$P/kartlar/$A/isten-cikis" "$T" '{"tarih":"2025-01-01"}'
kontrol 400 "Cikis giristen once olamaz" cikis_tarihi
istek POST "$P/kartlar/$A/isten-cikis" "$T" '{"tarih":"2026-04-30"}'
kontrol 200 "Cikis yapildi, zimmet uyarisi" '"istenCikis":"2026-04-30".*"acikZimmet":1'
istek POST $P/avanslar "$T" '{"personelId":"'"$A"'","tarih":"2026-05-01","tutar":100,"taksitSayisi":1,"hesapId":"'"$KS"'"}'
kontrol 422 "Cikan personele avans verilmez" personel_cikti
istek POST "$P/zimmetler/$Z/iade" "$T" '{"tarih":"2026-04-30","kayipMi":false,"not":"Saglam"}'
kontrol 200 "Zimmet iade alindi" '"durum":"IadeEdildi"'
istek DELETE "$P/kartlar/$C" "$T"
kontrol 204 "Hareketsiz personel silindi"

echo "--- Izolasyon"
istek GET "$P/kartlar/$A" "$T2"
kontrol 404 "Baska firma karti goremez" personel_bulunamadi
istek GET "$P/donemler/$D" "$T2"
kontrol 404 "Baska firma donemi goremez" donem_bulunamadi
istek POST "$P/masraflar/$MR/onayla" "$T2" '{}'
kontrol 404 "Baska firma masraf onaylayamaz" masraf_bulunamadi
istek POST $P/avanslar "$T2" '{"personelId":"'"$A"'","tarih":"2026-03-01","tutar":10,"taksitSayisi":1}'
kontrol 404 "Baska firma personeline avans verilmez" personel_bulunamadi
istek GET "$P/kartlar" "$T2"
kontrol 200 "Baska firma listesi bos" '"toplamKayit":0'

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
