#!/usr/bin/env bash
# Bildirimler: üretim, tekilleştirme, tercih, e-posta (Mailpit) uçtan uca senaryosu. Kullanım: bildirim_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_bildirim"; rm -rf "$DIR"; mkdir -p "$DIR"
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
B=/api/bildirim
CS=/api/kasabanka/cek-senet
gun() { date -d "$1 days" +%F; }
sayi() { echo "$GOVDE" | grep -o "$1" | wc -l | tr -d ' '; }

U=$(date +%s)
AYLA="bildirimayla$U@ornek.com"
istek POST /api/yonetim/giris "" "$SISTEM_GIRIS"; SYS=$(alan accessToken)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Bildirim Test AS","ad":"Ayla","soyad":"Yonetici","eposta":"'"$AYLA"'","sifre":"Sifre1234"}'; T=$(alan accessToken)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Diger","ad":"D","soyad":"F","eposta":"bildirimdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'; T2=$(alan accessToken)
istek POST /api/yonetim/kullanicilar "$T" '{"ad":"Kaan","soyad":"Kullanici","eposta":"bildirimkaan'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Kullanici"}'
istek POST /api/yonetim/giris "" '{"eposta":"bildirimkaan'"$U"'@ornek.com","sifre":"Sifre1234"}'; TK=$(alan accessToken)

echo "--- SMTP (sistem)"
istek PUT $B/sistem/eposta "$T" '{"aktifMi":true,"sunucu":"'"$SMTP_SUNUCU"'","port":'"$SMTP_PORT"',"guvenlik":"Yok","gonderenAdres":"bildirim@onmuhasebe.local","sifreKayitliMi":false}'
kontrol 403 "Firma yoneticisi SMTP ayarlayamaz"
istek PUT $B/sistem/eposta "$SYS" '{"aktifMi":true,"sunucu":"","port":0,"guvenlik":"Yok","gonderenAdres":"x","sifreKayitliMi":false}'
kontrol 400 "Eksik SMTP ayari reddedildi" 'Sunucu.*Port.*GonderenAdres'
istek PUT $B/sistem/eposta "$SYS" '{"aktifMi":true,"sunucu":"'"$SMTP_SUNUCU"'","port":'"$SMTP_PORT"',"guvenlik":"Yok","kullaniciAdi":"posta","sifre":"Cok Gizli 1","gonderenAdres":"bildirim@onmuhasebe.local","gonderenAd":"On Muhasebe","sifreKayitliMi":false}'
kontrol 200 "SMTP kaydedildi, sifre donmuyor" '"kullaniciAdi":"posta","sifre":null,"sifreKayitliMi":true'
SAKLI=$(sql "select sifre_sifreli from bildirim.eposta_ayari limit 1")
if [ -z "$SAKLI" ] || echo "$SAKLI" | grep -q "Gizli"; then KOD=999; fi
kontrol 200 "Sifre veritabaninda sifreli"
istek PUT $B/sistem/eposta "$SYS" '{"aktifMi":true,"sunucu":"'"$SMTP_SUNUCU"'","port":'"$SMTP_PORT"',"guvenlik":"Yok","kullaniciAdi":null,"gonderenAdres":"bildirim@onmuhasebe.local","gonderenAd":"On Muhasebe","sifreKayitliMi":false}'
kontrol 200 "Kimliksiz SMTP (Mailpit)" '"kullaniciAdi":null,"sifre":null,"sifreKayitliMi":false'
curl -s -X DELETE $MAILPIT/messages > /dev/null
istek POST $B/sistem/eposta/test "$SYS" '{"alici":"test'"$U"'@ornek.com"}'
kontrol 204 "Test e-postasi gonderildi"
sleep 1
GOVDE=$(curl -s "$MAILPIT/search?query=to:test$U@ornek.com"); KOD=200
kontrol 200 "Test e-postasi Mailpit'e ulasti" '"Subject":"Ön Muhasebe test e-postası"'

echo "--- Veri"
istek PUT $B/tercihler "$T" '{"bildirimAlir":true,"kapaliTurler":[],"eposta":"Aninda"}'
kontrol 200 "Ayla e-postayi aninda istedi" '"eposta":"Aninda"'
istek POST /api/cari/hesaplar "$T" '{"unvan":"Riskli Musteri","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","riskLimiti":100}'; CARI=$(alan id)
istek POST "/api/cari/hesaplar/$CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Borc","tarih":"'"$(gun -10)"'","tutar":500}'
istek POST /api/cari/hesaplar "$T" '{"unvan":"Cekli Musteri","tur":"MusteriVeTedarikci","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; CARI2=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Is Bankasi","tur":"Banka","paraBirimi":"TRY"}'; BANKA=$(alan id)
cek() { echo '{"tur":"Cek","seriNo":"'"$1"'","vadeTarihi":"'"$2"'","tutar":'"$3"',"paraBirimi":"TRY","borclu":{"ad":"Kesideci","kimlikNo":"9876543217"},"bankaAdi":"Akbank"}'; }
istek POST $CS/giris "$T" '{"cariHesapId":"'"$CARI2"'","tarih":"'"$(gun -10)"'","riskOnaylandi":true,"evraklar":['"$(cek 1001 "$(gun 2)" 1000),$(cek 1002 "$(gun 0)" 2000),$(cek 1003 "$(gun -5)" 3000),$(cek 1004 "$(gun 20)" 4000)"']}'
kontrol 201 "Dort alinan cek (yaklasan, bugun, gecikmis, uzak)"
istek POST $CS/karneler "$T" '{"tur":"Cek","kendiHesapId":"'"$BANKA"'","baslangicSeri":"K1","yaprakSayisi":2,"tarih":"'"$(gun -10)"'"}'; KARNE=$(alan id)
istek POST $CS/cikis "$T" '{"cariHesapId":"'"$CARI2"'","tarih":"'"$(gun 0)"'","evraklar":[{"tur":"Cek","vadeTarihi":"'"$(gun 1)"'","tutar":700,"kendiHesapId":"'"$BANKA"'","karneId":"'"$KARNE"'"}]}'
kontrol 201 "Karneden verilen cek (yarin odenecek, karnede 1 yaprak kaldi)"

echo "--- Uretim"
istek POST $B/sistem/calistir "$T"
kontrol 403 "Firma yoneticisi elle calistiramaz"
istek POST $B/sistem/calistir "$SYS"
kontrol 200 "Elle calistirildi" '"uretilen":[1-9]'
sleep 1
istek GET "$B?boyut=50" "$T"
kontrol 200 "Yaklasan alinan cek" '"tur":"AlinanEvrakVadesi","baslik":"Çek vadesine 2 gün kaldı"'
kontrol 200 "Bugun vadeli cek (uyari)" '"baslik":"Çek bugün vadeli: tahsil zamanı","metin":"[^"]*1002[^"]*","baglanti":"cek-senet/[^"]*","onem":"Uyari"'
kontrol 200 "Gecikmis cek (kritik)" '"tur":"GecikmisEvrak","baslik":"Vadesi geçen çek portföyde: 5 gün".*"onem":"Kritik"'
kontrol 200 "Verilen cek odemesi" '"tur":"VerilenEvrakVadesi","baslik":"Verilen çek ödemesine 1 gün kaldı"'
kontrol 200 "Risk limiti" '"tur":"RiskLimiti","baslik":"Risk limiti aşıldı: Riskli Musteri","metin":"Bakiye 500,00 TRY, limit 100,00 TRY."'
kontrol 200 "Karne azaldi" '"tur":"KarneAzaldi","baslik":"Çek karnesi azaldı: 1 yaprak"'
if echo "$GOVDE" | grep -q "1004"; then KOD=999; fi
kontrol 200 "20 gun sonraki cek henuz hatirlatilmadi"
N1=$(echo "$GOVDE" | grep -o '"toplamKayit":[0-9]*' | cut -d: -f2)
istek GET $B/sayac "$T"
kontrol 200 "Okunmamis sayisi" "\"okunmamis\":$N1"

istek POST $B/sistem/calistir "$SYS"
istek GET "$B?boyut=50" "$T"
kontrol 200 "Ikinci calisma ayni bildirimi tekrar uretmedi" "\"toplamKayit\":$N1"

echo "--- Roller ve tercih"
istek GET $B "$TK"
kontrol 200 "Kullanici rolu varsayilan olarak almaz" '"toplamKayit":0'
istek GET $B/tercihler "$TK"
kontrol 200 "Kullanicinin varsayilan tercihi" '"bildirimAlir":false,"kapaliTurler":\[\],"eposta":"GunlukOzet"'
istek PUT $B/tercihler "$TK" '{"bildirimAlir":true,"kapaliTurler":["RiskLimiti"],"eposta":"Kapali"}'
kontrol 200 "Kullanici bildirim almayi acti, riski kapatti"
istek POST $B/sistem/calistir "$SYS"
istek GET "$B?boyut=50" "$TK"
kontrol 200 "Kullaniciya vade bildirimleri geldi" '"tur":"AlinanEvrakVadesi"'
if echo "$GOVDE" | grep -q '"tur":"RiskLimiti"'; then KOD=999; fi
kontrol 200 "Kapattigi tur gelmedi"
istek PUT $B/tercihler "$TK" '{"bildirimAlir":true,"kapaliTurler":["Bilinmeyen"],"eposta":"Kapali"}'
kontrol 400 "Gecersiz tur reddedildi"

echo "--- Okundu"
ILK=$(curl -s "$KOK$B?boyut=1" -H "Authorization: Bearer $T" | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)
istek POST "$B/$ILK/okundu" "$TK"
kontrol 404 "Baskasinin bildirimi okundu yapilamaz"
istek POST "$B/$ILK/okundu" "$T"
kontrol 204 "Okundu"
istek GET $B/sayac "$T"
kontrol 200 "Sayac bir azaldi" "\"okunmamis\":$((N1-1))"
istek GET "$B?sadeceOkunmamis=true&boyut=50" "$T"
kontrol 200 "Okunmamis filtresi" "\"toplamKayit\":$((N1-1))"
istek POST $B/tumu-okundu "$T"
kontrol 200 "Tumu okundu" "\"okunmamis\":$((N1-1))"
istek GET $B/sayac "$T"
kontrol 200 "Sayac sifir" '"okunmamis":0'

echo "--- E-posta"
GOVDE=$(curl -s "$MAILPIT/search?query=to:$AYLA"); KOD=200
kontrol 200 "Ayla'ya aninda e-posta gitti" '"Subject":"[0-9]+ yeni bildirim"'
GOVDE=$(curl -s "$MAILPIT/search?query=to:bildirimkaan$U@ornek.com"); KOD=200
kontrol 200 "E-postayi kapatan kullaniciya gitmedi" '"messages_count":0|"total":0'

echo "--- Firma ayari ve izolasyon"
istek PUT $B/ayarlar "$TK" '{"vadeOncesiGun":5,"kapaliTurler":[]}'
kontrol 403 "Kullanici firma ayarini degistiremez"
istek PUT $B/ayarlar "$T" '{"vadeOncesiGun":99,"kapaliTurler":[]}'
kontrol 400 "Vade gunu 0-60"
istek PUT $B/ayarlar "$T" '{"vadeOncesiGun":30,"kapaliTurler":["KarneAzaldi"]}'
kontrol 200 "Firma ayari kaydedildi" '"vadeOncesiGun":30,"kapaliTurler":\["KarneAzaldi"\]'
istek POST $B/sistem/calistir "$SYS"
istek GET "$B?boyut=50" "$T"
kontrol 200 "30 gune cikinca uzak cek de hatirlatildi" '"metin":"[^"]*1004'
istek GET $B "$T2"
kontrol 200 "Baska firma bu bildirimleri gormez" '"toplamKayit":0'

echo "--- Odeme onayi aninda bildirim"
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" '{"paketKodu":"basic","aySayisi":12,"tutar":5000,"odemeTarihi":"'"$(gun 0)"'","gonderen":"Bildirim Test AS"}'
OB=$(alan id)
istek POST "/api/yonetim/sistem/odeme-bildirimleri/$OB/onayla" "$SYS" '{}'
kontrol 200 "Odeme onaylandi"
istek GET "$B?boyut=5" "$T"
kontrol 200 "Calistirmayi beklemeden 'Odemeniz onaylandi' geldi" '"tur":"OdemeBildirimiSonucu","baslik":"Ödemeniz onaylandı"'
istek GET "$B?boyut=50" "$TK"
if echo "$GOVDE" | grep -q "OdemeBildirimiSonucu"; then KOD=999; fi
kontrol 200 "Odeme sonucu kullanici rolune gitmedi"

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
