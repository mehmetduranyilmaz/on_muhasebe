#!/usr/bin/env bash
# Kasa/Banka modülü ve cari kapama uçtan uca senaryosu. Kullanım: kasabanka_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_kasabanka"; rm -rf "$DIR"; mkdir -p "$DIR"
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
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:500}"; KALAN=$((KALAN+1))
  fi
}
# JSON dizisinde verilen metni içeren nesnenin "id" alanı.
nesne_id() { echo "$GOVDE" | sed 's/},{/}\n{/g' | grep -- "$1" | head -1 | grep -o '"hareketId":"[^"]*\|"id":"[^"]*' | head -1 | sed -E 's/.*:"//'; }
bakiye() { istek GET "/api/kasabanka/hesaplar/$1" "$T"; kontrol 200 "$2" "\"bakiye\":$3(\.0+)?,"; }
cari_bakiye() { istek GET "/api/cari/hesaplar/$1" "$T"; kontrol 200 "$2" "\"bakiye\":$3(\.0+)?,"; }
fatura_kalan() { istek GET "/api/fatura/faturalar/$1" "$T"; kontrol 200 "$2" "\"kalan\":$3(\.0+)?}"; }
satis() { # CARI TUTAR [EK]
  local c=null; [ -n "$1" ] && c="\"$1\""
  istek POST /api/fatura/faturalar "$T" '{"tur":"Satis","cariHesapId":'"$c"',"tarih":"2026-10-10","genelIskontoTuru":"Yuzde","genelIskonto":0,"satirlar":[{"ad":"Hizmet","birim":"ADET","miktar":1,"birimFiyat":'"$2"',"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0}]'"${3:+,$3}"'}'
}

U=$(date +%s)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Kasa Test AS","ad":"Can","soyad":"Er","eposta":"kasa'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"kasadiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Musteri A","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C1=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Supplier USD","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"USD"}'; C2=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Ev Sahibi","tur":"Tedarikci","kisiTipi":"Sahis","paraBirimi":"TRY"}'; C3=$(alan id)

echo "--- Hesaplar ve kategoriler"
istek GET /api/kasabanka/kategoriler "$T"
kontrol 200 "Ilk listelemede baslangic kategorileri acildi" '"ad":"Kira","tur":"Gider"'
KIRA=$(nesne_id '"ad":"Kira","tur":"Gider"'); ELEKTRIK=$(nesne_id '"ad":"Elektrik'); KOMISYON=$(nesne_id 'POS komisyonu'); FAIZ=$(nesne_id '"ad":"Faiz geliri"')
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Merkez Kasa","tur":"Kasa","paraBirimi":"TRY"}'
kontrol 201 "Kasa hesabi KS00001" '"kod":"KS00001"'; KASA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Ziraat TL","tur":"Banka","paraBirimi":"TRY","bankaAdi":"Ziraat","iban":"TR33 0006 1005 1978 6457 8413 26"}'
kontrol 201 "Banka hesabi BN00001, IBAN bosluksuz kaydedildi" '"kod":"BN00001".*"iban":"TR330006100519786457841326"'; BANKA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Hatali","tur":"Banka","paraBirimi":"TRY","iban":"TR330006100519786457841327"}'
kontrol 400 "Kontrol hanesi yanlis IBAN reddedildi" "IBAN"
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Garanti USD","tur":"Banka","paraBirimi":"USD"}'; BANKA_USD=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Sirket Karti","tur":"KrediKarti","paraBirimi":"TRY","limit":50000}'
kontrol 201 "Kredi karti KK00001, limitli" '"kod":"KK00001".*"limit":50000'; KART=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Magaza POS","tur":"Pos","paraBirimi":"TRY"}'
kontrol 201 "POS hesabi PS00001" '"kod":"PS00001"'; POS=$(alan id)

echo "--- Acilis"
istek POST /api/kasabanka/islemler/acilis "$T" '{"hesapId":"'"$KASA"'","tarih":"2026-10-01","tutar":10000}'
kontrol 201 "Kasa acilisi 10.000" '"giris":10000'
istek POST /api/kasabanka/islemler/acilis "$T" '{"hesapId":"'"$KASA"'","tarih":"2026-10-01","tutar":5}'
kontrol 422 "Ikinci acilis reddedildi" acilis_zaten_var
istek POST /api/kasabanka/islemler/acilis "$T" '{"hesapId":"'"$BANKA"'","tarih":"2026-10-01","tutar":20000}'

echo "--- Tahsilat ve FIFO kapama"
satis "$C1" 1000; F1=$(alan id)
fatura_kalan "$F1" "Yeni faturanin kalani 1.200" 1200
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$KASA"'","cariHesapId":"'"$C1"'","tarih":"2026-10-11","tutar":500,"belgeNo":"MAK-1"}'
kontrol 201 "Kasaya 500 tahsilat, aciklamada cari unvani" '"tur":"Tahsilat".*"giris":500.*"cariUnvan":"Musteri A"'
T1=$(alan islemId)
fatura_kalan "$F1" "Tahsilat faturayi kismen kapatti: kalan 700" 700
cari_bakiye "$C1" "Cari bakiye 700" 700
bakiye "$KASA" "Kasa 10.500" 10500
satis "$C1" 300; F2=$(alan id)
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$KASA"'","cariHesapId":"'"$C1"'","tarih":"2026-10-12","tutar":1000}'
T2ISLEM=$(alan islemId)
fatura_kalan "$F1" "FIFO: ikinci tahsilat once eski faturayi kapatti (kalan 0)" 0
fatura_kalan "$F2" "FIFO: kalan 300 yeni faturaya, kalan 60" 60
istek GET "/api/cari/hesaplar/$C1/acik-kalemler" "$T"
kontrol 200 "Acik kalemler: yalnizca 60 TL'lik fatura" '"kalan":60'
F2H=$(nesne_id '"tur":"SatisFaturasi"')

echo "--- Elle kapama"
istek GET "/api/cari/hesaplar/$C1/hareketler" "$T"
T2H=$(echo "$GOVDE" | sed 's/},{/}\n{/g' | grep -- "\"kaynakBelgeId\":\"$T2ISLEM\"" | grep -o '"id":"[^"]*' | head -1 | sed -E 's/.*:"//')
istek GET "/api/cari/hareketler/$T2H/kapamalar" "$T"
kontrol 200 "Ikinci tahsilat iki faturayi kapatmis (700 + 300)" '"kalan":0.00,"elleKapamaMi":false,"kapamalar":\[.*"tutar":(300|700).00.*"tutar":(300|700).00'
istek PUT "/api/cari/hareketler/$T2H/kapamalar" "$T" '{"kapamalar":[{"karsiHareketId":"'"$F2H"'","tutar":360}]}'
kontrol 200 "Elle: tahsilat yalnizca ikinci faturayi kapatsin" '"kalan":640.00,"elleKapamaMi":true'
fatura_kalan "$F2" "Elle kapamada ikinci fatura kapandi" 0
fatura_kalan "$F1" "Elle kapamada ilk fatura yeniden acildi (700)" 700
istek PUT "/api/cari/hareketler/$T2H/kapamalar" "$T" '{"kapamalar":[{"karsiHareketId":"'"$F2H"'","tutar":5000}]}'
kontrol 400 "Acik tutardan fazla elle kapama reddedildi" kapama_gecersiz
istek DELETE "/api/cari/hareketler/$T2H/kapamalar" "$T"
kontrol 200 "Otomatige donuldu" '"elleKapamaMi":false'
fatura_kalan "$F1" "Otomatikte yine FIFO: ilk fatura kapali" 0

echo "--- Doviz kurali (cari bazinda)"
istek POST /api/kasabanka/islemler/odeme "$T" '{"hesapId":"'"$BANKA"'","cariHesapId":"'"$C2"'","tarih":"2026-10-12","tutar":4000,"cariKuru":40}'
kontrol 422 "Varsayilan: USD cariye TL hesaptan odeme reddedildi" farkli_doviz
istek GET "/api/cari/hesaplar/$C2" "$T"; SURUM=$(echo "$GOVDE" | grep -o '"surum":[0-9]*' | cut -d: -f2)
istek PUT "/api/cari/hesaplar/$C2" "$T" '{"unvan":"Supplier USD","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"USD","farkliDovizdeIslem":true,"surum":'"$SURUM"'}'
kontrol 200 "Cari kartinda farkli dovize izin verildi" '"farkliDovizdeIslem":true'
istek POST /api/kasabanka/islemler/odeme "$T" '{"hesapId":"'"$BANKA"'","cariHesapId":"'"$C2"'","tarih":"2026-10-12","tutar":4000,"cariKuru":40}'
kontrol 201 "4.000 TL odeme, cariye 100 USD olarak islendi" '"cikis":4000'
cari_bakiye "$C2" "USD cari 100 USD borclandi" 100
istek POST /api/kasabanka/islemler/odeme "$T" '{"hesapId":"'"$BANKA_USD"'","cariHesapId":"'"$C2"'","tarih":"2026-10-12","tutar":10,"kur":40}'
kontrol 201 "Ayni dovizde (USD hesap) odeme"

echo "--- Virman"
istek POST /api/kasabanka/islemler/virman "$T" '{"kaynakHesapId":"'"$KASA"'","hedefHesapId":"'"$BANKA"'","tarih":"2026-10-13","tutar":2000}'
kontrol 201 "Kasadan bankaya 2.000 virman (iki bacak)" '"tur":"VirmanCikis".*"tur":"VirmanGiris"'
bakiye "$KASA" "Kasa 11.500 - 2.000 = 9.500" 9500
istek POST /api/kasabanka/islemler/virman "$T" '{"kaynakHesapId":"'"$BANKA"'","hedefHesapId":"'"$BANKA_USD"'","tarih":"2026-10-13","tutar":4100,"hedefKur":41}'
kontrol 201 "TL bankadan USD bankaya 4.100 TL -> 100 USD" '"giris":100'
bakiye "$BANKA_USD" "USD banka: -10 + 100 = 90" 90
istek POST /api/kasabanka/islemler/virman "$T" '{"kaynakHesapId":"'"$KASA"'","hedefHesapId":"'"$KASA"'","tarih":"2026-10-13","tutar":1}'
kontrol 400 "Ayni hesaba virman reddedildi"

echo "--- Gelir / gider"
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gider","bicim":"Odendi","kategoriId":"'"$KIRA"'","hesapId":"'"$BANKA"'","tarih":"2026-10-14","tutar":1500}'
kontrol 201 "Carisiz kira gideri bankadan" '"tur":"Gider".*"cikis":1500.*"kategoriAdi":"Kira"'
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gider","bicim":"Odendi","kategoriId":"'"$ELEKTRIK"'","hesapId":"'"$KASA"'","cariHesapId":"'"$C3"'","tarih":"2026-10-14","tutar":800}'
kontrol 201 "Carili odenmis gider (kasadan)" '"cikis":800'
cari_bakiye "$C3" "Odendi: cari bakiyesi degismedi (0)" 0
istek GET "/api/cari/hesaplar/$C3/hareketler" "$T"
kontrol 200 "Cari ekstresinde gider ve odeme satirlari" '"tur":"Odeme".*"tur":"Gider"'
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gider","bicim":"Tahakkuk","kategoriId":"'"$KIRA"'","cariHesapId":"'"$C3"'","tarih":"2026-10-15","vadeTarihi":"2026-10-30","tutar":5000}'
kontrol 201 "Kira tahakkuku: kasa hareketi yok" '"hareketler":\[\]'
cari_bakiye "$C3" "Tahakkuk: cariye 5.000 borcumuz var" -5000
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gider","bicim":"Tahakkuk","kategoriId":"'"$KIRA"'","tarih":"2026-10-15","tutar":5000}'
kontrol 400 "Carisiz tahakkuk reddedildi" bicim_gecersiz
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gelir","bicim":"Odendi","kategoriId":"'"$KIRA"'","hesapId":"'"$KASA"'","tarih":"2026-10-15","tutar":10}'
kontrol 400 "Gider kategorisiyle gelir kaydi reddedildi" kategori_turu
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gelir","bicim":"Odendi","kategoriId":"'"$FAIZ"'","hesapId":"'"$BANKA"'","tarih":"2026-10-15","tutar":250}'
kontrol 201 "Faiz geliri bankaya" '"giris":250'
istek GET "/api/kasabanka/raporlar/gelir-gider?baslangic=2026-10-01&bitis=2026-10-31" "$T"
kontrol 200 "Gelir/gider raporu: kira 6.500 (odenen + tahakkuk), faiz 250" '"ad":"Kira","tutarTl":6500.00.*"toplamGelirTl":250.00,"toplamGiderTl":7300.00'

echo "--- Kredi karti ve POS"
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gider","bicim":"Odendi","kategoriId":"'"$ELEKTRIK"'","hesapId":"'"$KART"'","tarih":"2026-10-16","tutar":300}'
bakiye "$KART" "Kartla harcama: kart bakiyesi -300 (borc)" -300
istek GET /api/kasabanka/raporlar/ozet "$T"
kontrol 200 "Ozette kredi karti borcu 300" '"krediKartiBorcuTl":300'
istek POST /api/kasabanka/islemler/virman "$T" '{"kaynakHesapId":"'"$BANKA"'","hedefHesapId":"'"$KART"'","tarih":"2026-10-17","tutar":300}'
bakiye "$KART" "Kart ekstresi bankadan odendi: 0" 0
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$POS"'","cariHesapId":"'"$C1"'","tarih":"2026-10-17","tutar":1000}'
kontrol 201 "POS ile kartli tahsilat"
istek POST /api/kasabanka/islemler/virman "$T" '{"kaynakHesapId":"'"$POS"'","hedefHesapId":"'"$BANKA"'","tarih":"2026-10-18","tutar":980}'
istek POST /api/kasabanka/islemler/gelir-gider "$T" '{"tur":"Gider","bicim":"Odendi","kategoriId":"'"$KOMISYON"'","hesapId":"'"$POS"'","tarih":"2026-10-18","tutar":20}'
bakiye "$POS" "POS: 1.000 - 980 bankaya - 20 komisyon = 0" 0
bakiye "$BANKA" "Banka: 20.000+2.000-4.000-4.100-1.500+250-300+980 = 13.330" 13330
cari_bakiye "$C1" "Musteri avansli: 1.560 - 2.500 = -940" -940

echo "--- Pesin fatura"
satis "$C1" 1000 '"pesinHesapId":"'"$KASA"'"'
kontrol 201 "Pesin satis: fatura kapali (kalan 0)" '"pesinHesapId":"'"$KASA"'","kalan":0'
FP=$(echo "$GOVDE" | grep -o '"fatura":{"id":"[^"]*' | cut -d'"' -f6)
bakiye "$KASA" "Kasa 9.500 - 800 + 1.200 = 9.900" 9900
cari_bakiye "$C1" "Pesin: cari bakiyesi degismedi" -940
istek GET "/api/cari/hesaplar/$C1/acik-kalemler" "$T"
kontrol 200 "Pesin tahsilat kendi faturasini kapatti, eski avans (940) dokunulmadan acik" '"yon":"Alacak".*"kalan":940'
istek GET "/api/kasabanka/hesaplar/$KASA/hareketler" "$T"
PESIN_ISLEM=$(echo "$GOVDE" | sed 's/},{/}\n{/g' | grep -- "\"kaynakBelgeId\":\"$FP\"" | grep -o '"islemId":"[^"]*' | head -1 | sed -E 's/.*:"//')
istek POST "/api/kasabanka/islemler/$PESIN_ISLEM/iptal" "$T"
kontrol 422 "Faturaya bagli tahsilat kasadan iptal edilemez" kaynak_belgeden_iptal
istek POST "/api/fatura/faturalar/$FP/iptal" "$T"
kontrol 200 "Pesin fatura iptal edildi" '"durum":"Iptal"'
bakiye "$KASA" "Fatura iptaliyle tahsilat da geri alindi: 8.700" 8700
satis "$C1" 100 '"pesinHesapId":"'"$BANKA_USD"'"'
kontrol 422 "Fatura dovizinden farkli hesapla pesin reddedildi" pesin_doviz
satis "$C1" 100 '"pesinHesapId":"'"$KART"'"'
kontrol 422 "Kredi kartina pesin tahsilat reddedildi" pesin_hesap_turu
istek POST /api/fatura/faturalar "$T" '{"tur":"Satis","satisSekli":"Perakende","cariHesapId":null,"tarih":"2026-10-10","genelIskontoTuru":"Yuzde","genelIskonto":0,"pesinHesapId":"'"$KASA"'","satirlar":[{"ad":"Urun","birim":"ADET","miktar":1,"birimFiyat":500,"kdvOrani":20,"kdvDahilMi":true,"iskonto1":0,"iskonto2":0,"iskonto3":0}]}'
kontrol 201 "Nihai tuketiciye pesin perakende satis"
bakiye "$KASA" "Nihai tuketici parasi dogrudan kasaya: 9.200" 9200

echo "--- Islem iptali ve kapama cozme"
istek POST "/api/kasabanka/islemler/$T1/iptal" "$T"
kontrol 200 "Ilk tahsilat iptal edildi (ters kayit)" '"iptalKaydiMi":true'
istek POST "/api/kasabanka/islemler/$T1/iptal" "$T"
kontrol 422 "Ikinci iptal reddedildi" zaten_iptal
bakiye "$KASA" "Kasa 8.700" 8700
cari_bakiye "$C1" "Cari -440" -440
fatura_kalan "$F1" "Acilan 500, POS avansindan otomatik kapandi (kalan 0)" 0
istek GET "/api/cari/raporlar/vade-analizi?tarih=2026-11-30&alacaklar=false" "$T"
kontrol 200 "Vade analizi: vadesi 30.10 olan kira borcu 30.11 itibariyla 31-60 gun diliminde" '"unvan":"Ev Sahibi","paraBirimi":"TRY","dilimler":{"vadesiGelmemis":0[.0]*,"gun0Ile30":0[.0]*,"gun31Ile60":5000'

echo "--- Ekstre, ozet, silme, izolasyon"
istek GET "/api/kasabanka/hesaplar/$KASA/ekstre?baslangic=2026-10-11&bitis=2026-10-31" "$T"
kontrol 200 "Kasa ekstresi: devir 10.000 + 10.10 pesin satis 500 = 10.500, son bakiye 8.700 (ilk tahsilat iptal edildi)" '"devirBakiye":10500.00.*"sonBakiye":8700.00'
istek GET /api/kasabanka/raporlar/ozet "$T"
kontrol 200 "Ozet: toplam nakit = kasa+banka+POS (+USD TL karsiligi)" '"toplamNakitTl":2'
istek DELETE "/api/kasabanka/hesaplar/$KASA" "$T"
kontrol 422 "Hareketli hesap silinemez" hareketli_hesap_silinemez
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Bos Kasa","tur":"Kasa","paraBirimi":"EUR"}'; BOS=$(alan id)
istek DELETE "/api/kasabanka/hesaplar/$BOS" "$T"
kontrol 204 "Hareketsiz hesap silindi"
istek POST "/api/kasabanka/hesaplar/$BANKA_USD/pasiflestir" "$T"
istek POST /api/kasabanka/islemler/odeme "$T" '{"hesapId":"'"$BANKA_USD"'","cariHesapId":"'"$C2"'","tarih":"2026-10-20","tutar":1,"kur":40}'
kontrol 422 "Pasif hesaptan islem yapilamaz" pasif_hesap
istek GET "/api/kasabanka/hesaplar/$KASA" "$T2"
kontrol 404 "Baska firma hesabi goremez"
istek POST /api/kasabanka/islemler/tahsilat "$T2" '{"hesapId":"'"$KASA"'","cariHesapId":"'"$C1"'","tarih":"2026-10-20","tutar":1}'
kontrol 404 "Baska firmanin hesabina tahsilat yapilamaz"

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
