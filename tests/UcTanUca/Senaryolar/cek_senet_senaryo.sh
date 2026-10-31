#!/usr/bin/env bash
# Çek/senet uçtan uca senaryosu. Kullanım: cek_senet_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_cek"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
CS=/api/kasabanka/cek-senet

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
evraklar() { echo "$GOVDE" | grep -o '"evrakIdleri":\[[^]]*' | grep -o '[0-9a-f-]\{36\}'; }
nesne_id() { echo "$GOVDE" | sed 's/},{/}\n{/g' | grep -- "$1" | head -1 | grep -o '"id":"[^"]*' | head -1 | sed -E 's/.*:"//'; }
bakiye() { istek GET "/api/kasabanka/hesaplar/$1" "$T"; kontrol 200 "$2" "\"bakiye\":$3(\.0+)?,"; }
cari_bakiye() { istek GET "/api/cari/hesaplar/$1" "$T"; kontrol 200 "$2" "\"bakiye\":$3(\.0+)?,"; }
durum() { istek GET "$CS/evraklar/$1" "$T"; kontrol 200 "$2" "\"durum\":\"$3\""; }
islem() { # ISLEM EVRAK_IDLERI(json) [EK]
  istek POST "$CS/islemler" "$T" '{"islem":"'"$1"'","tarih":"'"${TARIH:-2026-10-20}"'","evrakIdleri":'"$2${3:+,$3}"'}'
}
cek() { # SERI TUTAR VADE BORCLU(json) [HESAPNO] [EK]
  echo '{"tur":"Cek","seriNo":"'"$1"'","vadeTarihi":"'"$3"'","tutar":'"$2"',"paraBirimi":"TRY","borclu":'"$4"',"bankaAdi":"Ziraat","sube":"Merkez","hesapNo":"'"${5:-555}"'"'"${6:+,$6}"'}'
}

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Cek Test AS","ad":"Cem","soyad":"Ak","eposta":"cek'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"cekdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Musteri A","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C1=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Tedarikci B","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C2=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Merkez Kasa","tur":"Kasa","paraBirimi":"TRY"}'; KASA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Ziraat TL","tur":"Banka","paraBirimi":"TRY","hesapNo":"7001"}'; BANKA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Garanti USD","tur":"Banka","paraBirimi":"USD"}'; BANKA_USD=$(alan id)
istek POST /api/kasabanka/islemler/acilis "$T" '{"hesapId":"'"$BANKA"'","tarih":"2026-10-01","tutar":50000}'
istek POST /api/fatura/faturalar "$T" '{"tur":"Satis","cariHesapId":"'"$C1"'","tarih":"2026-10-10","genelIskontoTuru":"Yuzde","genelIskonto":0,"satirlar":[{"ad":"Hizmet","birim":"ADET","miktar":1,"birimFiyat":1000,"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0}]}'
F1=$(echo "$GOVDE" | grep -o '"fatura":{"id":"[^"]*' | sed -E 's/.*:"//')
AHMET='{"ad":"Ahmet Yilmaz","kimlikNo":"10000000146"}'

echo "--- Ayarlar ve dogrulamalar"
istek GET "$CS/ayarlar" "$T"
kontrol 200 "Varsayilan ayarlar: cekte kimlik zorunlu, uyar ve onay iste" '"kesideciKimlikZorunlu":true,"senetBorcluKimlikZorunlu":false,"riskDavranisi":"UyarVeOnayIste"'
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-15","riskOnaylandi":false,"evraklar":['"$(cek 999 100 2026-11-01 '{"ad":"Kimliksiz Kisi"}')"']}'
kontrol 400 "Kesideci kimlik no olmadan cek girilemez" kimlik_no_gerekli
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-15","riskOnaylandi":false,"evraklar":['"$(cek 999 100 2026-11-01 '{"ad":"X","kimlikNo":"10000000147"}')"']}'
kontrol 400 "Kontrol hanesi yanlis kimlik no reddedildi" "VKN/TCKN"
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-15","riskOnaylandi":false,"evraklar":[]}'
kontrol 400 "Bos bordro reddedildi"
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-15","riskOnaylandi":false,"evraklar":[{"tur":"Cek","seriNo":"1","vadeTarihi":"2026-11-01","tutar":100,"paraBirimi":"TRY","borclu":'"$AHMET"'}]}'
kontrol 400 "Cekte banka adi zorunlu" "bankas"

echo "--- Alinan evrak girisi"
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-15","aciklama":"Ekim tahsilati","riskOnaylandi":false,"evraklar":['"$(cek 1001 10000 2026-11-15 "$AHMET")"',{"tur":"Senet","seriNo":"S-1","vadeTarihi":"2026-12-01","tutar":2000,"paraBirimi":"TRY","borclu":{"ad":"Mehmet Kaya"},"kefilAdi":"Ali Veli"}]}'
kontrol 201 "Giris bordrosu BR00001: bir cek bir senet" '"bordroNo":"BR00001"'
read -r AC1 AS1 <<< "$(evraklar | tr '\n' ' ')"
BR1=$(alan bordroId)
durum "$AC1" "Cek AC00001 portfoyde" Portfoyde
istek GET "$CS/evraklar/$AS1" "$T"
kontrol 200 "Senet AS00001, kimliksiz borclu kabul edildi" '"portfoyNo":"AS00001".*"borcluAdi":"Mehmet Kaya"'
cari_bakiye "$C1" "Musteri: 1.200 fatura - 12.000 evrak = -10.800" -10800
istek GET "/api/fatura/faturalar/$F1" "$T"
kontrol 200 "Evrak girisi faturayi FIFO ile kapatti" '"kalan":0(.0+)?[,}]'
istek GET "/api/cari/hesaplar/$C1/hareketler" "$T"
kontrol 200 "Cari ekstrede cek/senet girisi belge no ve vadeyle" '"tur":"CekSenetGirisi".*"belgeNo":"AC00001/1001"'
istek GET "$CS/borclular?arama=ahmet" "$T"
kontrol 200 "Kesideci karti otomatik acildi" '"ad":"Ahmet Yilmaz","kimlikNo":"10000000146".*"evrakSayisi":1,"karsiliksizSayisi":0'
AHMET_ID=$(nesne_id 'Ahmet')
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-15","riskOnaylandi":false,"evraklar":['"$(cek 1001 777 2026-11-15 "$AHMET")"']}'
kontrol 409 "Ayni hesaptan ayni seri cek ikinci kez girilemez" evrak_zaten_kayitli
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-10-16","riskOnaylandi":false,"evraklar":['"$(cek 1002 5000 2026-11-20 '{"borcluId":"'"$AHMET_ID"'"}')"']}'
kontrol 201 "Kayitli kesideci secilerek ikinci cek" '"bordroNo":"BR00002"'
AC2=$(evraklar)
istek GET "$CS/borclular/$AHMET_ID" "$T"
kontrol 200 "Cek hesabi bir kez hatirlandi" '"hesaplar":\[\{[^]]*"hesapNo":"555"\}\]'
cari_bakiye "$C1" "Musteri -15.800" -15800

echo "--- Tahsile verme ve tahsil"
islem TahsileVerme '["'"$AC1"'"]' '"hesapId":"'"$KASA"'"'
kontrol 422 "Kasaya tahsile verilemez" evrak_hesap_turu
islem TahsileVerme '["'"$AC1"'"]'
kontrol 400 "Tahsile vermede banka zorunlu" evrak_hesap_gerekli
islem TahsileVerme '["'"$AC1"'"]' '"hesapId":"'"$BANKA"'"'
kontrol 201 "Cek bankaya tahsile verildi" '"bordroNo":"BR00003"'
istek GET "$CS/evraklar?acik=true" "$T"
kontrol 200 "Portfoy listesinde konum banka adi" '"portfoyNo":"AC00001".*"durum":"Tahsilde".*"konumAdi":"Ziraat TL"'
islem Ciro '["'"$AC1"'"]' '"cariHesapId":"'"$C2"'"'
kontrol 422 "Tahsildeki cek ciro edilemez" "Tahsilde"
TARIH=2026-11-15 islem Tahsil '["'"$AC1"'"]'
kontrol 201 "Vadede tahsil edildi"
durum "$AC1" "Cek tahsil edildi" TahsilEdildi
bakiye "$BANKA" "Banka 50.000 + 10.000 tahsil" 60000
cari_bakiye "$C1" "Tahsil cariyi tekrar etkilemedi" -15800
istek GET "/api/kasabanka/hesaplar/$BANKA/hareketler" "$T"
TAHSIL_ISLEM=$(echo "$GOVDE" | sed 's/},{/}\n{/g' | grep CekSenetTahsil | grep -o '"islemId":"[^"]*' | sed -E 's/.*:"//')
istek POST "/api/kasabanka/islemler/$TAHSIL_ISLEM/iptal" "$T"
kontrol 422 "Tahsil hareketi kasa ekranindan iptal edilemez" kaynak_belgeden_iptal

echo "--- Ciro ve cirodan karsiliksiz"
islem Ciro '["'"$AC2"'"]' '"cariHesapId":"'"$C1"'"'
kontrol 422 "Evrak getiren cariye ciro edilemez" evrak_ayni_cari
islem Ciro '["'"$AC2"'"]'
kontrol 400 "Ciroda cari zorunlu" evrak_cari_gerekli
islem Ciro '["'"$AC2"'"]' '"cariHesapId":"'"$C2"'"'
kontrol 201 "Cek tedarikciye ciro edildi"
cari_bakiye "$C2" "Tedarikci ciro ile borclandi (+5.000)" 5000
TARIH=2026-11-21 islem CirodanKarsiliksiz '["'"$AC2"'"]'
kontrol 201 "Ciro edilen cek karsiliksiz dondu"
cari_bakiye "$C2" "Tedarikcinin borcu geri alindi" 0
cari_bakiye "$C1" "Musteri yeniden borclandi: -10.800" -10800
istek GET "$CS/evraklar/$AC2" "$T"
kontrol 200 "Evrak karsiliksiz, takipte; ciro bilgisi temizlendi" '"durum":"Karsiliksiz".*"ciroCariId":null.*"karsiliksizTarihi":"2026-11-21"'

echo "--- Kara liste (risk)"
istek GET "$CS/risk?kimlikNo=10000000146" "$T"
kontrol 200 "Kesideci riskli: karsiliksiz gecmisi" '"riskli":true,"mesaj":"Ahmet Yilmaz \(10000000146\): 21.11.2026 tarihli 5.000,00 TRY'
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-11-22","riskOnaylandi":false,"evraklar":['"$(cek 1003 3000 2026-12-20 "$AHMET")"']}'
kontrol 422 "Riskli kesideciden evrak onaysiz kaydedilmez" 'Yine de kabul.*risk_onayi_gerekli'
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-11-22","riskOnaylandi":true,"evraklar":['"$(cek 1003 3000 2026-12-20 "$AHMET")"']}'
kontrol 201 "Kullanici onayiyla kabul edildi"
AC3=$(evraklar)
istek GET "$CS/evraklar/$AC3" "$T"
kontrol 200 "Risk onayi evrakta saklandi" '"riskOnayTarihi":"20[0-9-]+T'
istek POST /api/cari/hesaplar "$T" '{"unvan":"Musteri C","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C3=$(alan id)
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C3"'","tarih":"2026-11-22","riskOnaylandi":true,"evraklar":['"$(cek 1004 700 2027-02-20 "$AHMET" 556)"',{"tur":"Senet","seriNo":"S-9","vadeTarihi":"2027-02-21","tutar":300,"paraBirimi":"TRY","borclu":{"ad":"Temiz Kisi"}}]}'
read -r KR1 KR2 <<< "$(evraklar | tr '\n' ' ')"
istek GET "$CS/evraklar/$KR2" "$T"
kontrol 200 "Ayni bordrodaki risksiz borclunun evrakina risk onayi islenmedi" '"riskOnayTarihi":null'
istek GET "$CS/evraklar/$KR1" "$T"
kontrol 200 "Riskli kesidecinin evrakina risk onayi islendi" '"riskOnayTarihi":"20[0-9-]+T'
istek POST "$CS/evraklar/$KR1/geri-al" "$T"; istek POST "$CS/evraklar/$KR2/geri-al" "$T"
istek PUT "$CS/borclular/$AHMET_ID/risk" "$T" '{"karaListede":false,"uyariKapali":true,"riskNotu":"Borcunu kapatti"}'
kontrol 204 "Kesideci icin uyari kapatildi"
istek GET "$CS/risk?borcluId=$AHMET_ID" "$T"
kontrol 200 "Uyarisi kapali kesideci riskli degil" '"riskli":false'
istek POST "$CS/borclular" "$T" '{"ad":"Riskli Ltd","kimlikNo":"1234567890"}'
kontrol 201 "Elle kesideci karti eklendi"; RISKLI=$(alan id)
istek POST "$CS/borclular" "$T" '{"ad":"Kopya","kimlikNo":"1234567890"}'
kontrol 409 "Ayni kimlik no ikinci kartta kullanilamaz" kimlik_no_kullaniliyor
istek PUT "$CS/borclular/$RISKLI/risk" "$T" '{"karaListede":true,"uyariKapali":false,"riskNotu":"Icra takibi var"}'
istek GET "$CS/risk?kimlikNo=1234567890" "$T"
kontrol 200 "Elle kara listeye alinan riskli" '"riskli":true.*kara listede: Icra takibi var'
istek PUT "$CS/ayarlar" "$T" '{"kesideciKimlikZorunlu":true,"senetBorcluKimlikZorunlu":false,"riskDavranisi":"Engelle"}'
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-11-22","riskOnaylandi":true,"evraklar":['"$(cek 77 100 2026-12-20 '{"ad":"Riskli Ltd","kimlikNo":"1234567890"}' 888)"']}'
kontrol 422 "Engelle ayarinda onayla bile kabul edilmez" riskli_borclu
istek PUT "$CS/ayarlar" "$T" '{"kesideciKimlikZorunlu":false,"senetBorcluKimlikZorunlu":false,"riskDavranisi":"UyarVeOnayIste"}'
kontrol 200 "Ayarlar: cekte kimlik istege bagli"
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-11-22","riskOnaylandi":false,"evraklar":['"$(cek 2001 1000 2026-12-25 '{"ad":"Kimliksiz Kesideci"}' 999)"']}'
kontrol 201 "Ayar kapatilinca kimliksiz cek kabul edildi"
AC4=$(evraklar)
cari_bakiye "$C1" "Musteri: -10.800 - 3.000 - 1.000 = -14.800" -14800

echo "--- Karsiliksizdan tahsil, hukuki takip, iade"
TARIH=2026-11-23 islem KarsiliksizdanTahsil '["'"$AC2"'"]' '"hesapId":"'"$KASA"'","tutar":6000'
kontrol 400 "Kalandan fazla tahsil reddedildi" evrak_tahsil_tutari
TARIH=2026-11-23 islem KarsiliksizdanTahsil '["'"$AC2"'","'"$AC3"'"]' '"hesapId":"'"$KASA"'","tutar":100'
kontrol 400 "Kismi tutar tek evrakla girilir" evrak_kismi_tahsil
TARIH=2026-11-23 islem KarsiliksizdanTahsil '["'"$AC2"'"]' '"hesapId":"'"$KASA"'","tutar":2000'
kontrol 201 "Karsiliksiz cekten 2.000 kismi tahsil"
istek GET "$CS/evraklar/$AC2" "$T"
kontrol 200 "Evrak takipte kaldi, 2.000 tahsil edildi" '"durum":"Karsiliksiz".*"tahsilEdilen":2000'
bakiye "$KASA" "Kasaya 2.000 girdi" 2000
cari_bakiye "$C1" "Musteri alacaklandi: -16.800" -16800
TARIH=2026-11-24 islem HukukiTakip '["'"$AC2"'"]'
kontrol 201 "Hukuki takibe verildi"
TARIH=2026-11-25 islem KarsiliksizIade '["'"$AC2"'"]'
kontrol 201 "Karsiliksiz evrak musteriye iade edildi"
cari_bakiye "$C1" "Karsiliksiz iadesi cariyi etkilemedi" -16800
islem Iade '["'"$AS1"'"]'
kontrol 201 "Senet vadesinden once portfoyden iade"
cari_bakiye "$C1" "Portfoyden iade musteriyi borclandirdi: -14.800" -14800

echo "--- Teminat ve geri alma"
TARIH=2026-11-23 islem TeminataVerme '["'"$AC3"'"]' '"hesapId":"'"$BANKA"'"'
kontrol 201 "Teminata verildi"
TARIH=2026-11-24 islem TeminattanAlma '["'"$AC3"'"]'
kontrol 201 "Teminattan geri alindi"
istek POST "$CS/evraklar/$AC3/geri-al" "$T"
kontrol 204 "Son hareket geri alindi"
durum "$AC3" "Evrak yeniden teminatta" Teminatta
istek POST "$CS/evraklar/$AC3/geri-al" "$T"
istek POST "$CS/evraklar/$AC3/geri-al" "$T"
kontrol 204 "Giris hareketi geri alindi"
istek GET "$CS/evraklar/$AC3" "$T"
kontrol 200 "Evrak iptal, gecmis korunuyor" '"durum":"Iptal".*"uygunIslemler":\[\],"geriAlinabilirMi":false.*"geriAlindi":true'
cari_bakiye "$C1" "Giris iptali musteri alacagini geri aldi: -11.800" -11800
istek POST "$CS/evraklar/$AC3/geri-al" "$T"
kontrol 422 "Iptal evrakta geri alinacak hareket yok" evrak_geri_alinamaz
istek POST "$CS/evraklar/$AC1/geri-al" "$T"
kontrol 204 "Tahsil geri alindi"
durum "$AC1" "Cek yeniden tahsilde" Tahsilde
bakiye "$BANKA" "Banka tahsil oncesine dondu" 50000
TARIH=2026-11-15 islem Tahsil '["'"$AC1"'"]'
kontrol 201 "Tekrar tahsil edildi"

echo "--- Verilen evrak"
istek POST "$CS/cikis" "$T" '{"cariHesapId":"'"$C2"'","tarih":"2026-10-20","evraklar":[{"tur":"Cek","seriNo":"5001","vadeTarihi":"2026-11-20","tutar":3000}]}'
kontrol 400 "Verilen cekte banka hesabi zorunlu" "banka hesab"
istek POST "$CS/cikis" "$T" '{"cariHesapId":"'"$C2"'","tarih":"2026-10-20","evraklar":[{"tur":"Cek","seriNo":"5001","vadeTarihi":"2026-11-20","tutar":3000,"kendiHesapId":"'"$KASA"'"}]}'
kontrol 400 "Verilen cek kasaya baglanamaz" verilen_cek_hesabi
istek POST "$CS/cikis" "$T" '{"cariHesapId":"'"$C2"'","tarih":"2026-10-20","evraklar":[{"tur":"Cek","seriNo":"5001","vadeTarihi":"2026-11-20","tutar":3000,"kendiHesapId":"'"$BANKA"'"},{"tur":"Senet","seriNo":"VS-1","vadeTarihi":"2026-12-10","tutar":1500,"paraBirimi":"TRY"},{"tur":"Cek","seriNo":"5002","vadeTarihi":"2026-12-15","tutar":1000,"kendiHesapId":"'"$BANKA"'"}]}'
kontrol 201 "Cikis bordrosu: iki cek bir senet"
read -r VC1 VS1 VC2 <<< "$(evraklar | tr '\n' ' ')"
cari_bakiye "$C2" "Tedarikci 5.500 borclandi" 5500
istek GET "$CS/evraklar/$VC1" "$T"
kontrol 200 "Verilen cek VC00001, banka hesabina bagli" '"portfoyNo":"VC00001".*"durum":"Verildi".*"kendiHesapAdi":"Ziraat TL"'
istek POST "$CS/cikis" "$T" '{"cariHesapId":"'"$C2"'","tarih":"2026-10-20","evraklar":[{"tur":"Cek","seriNo":"5001","vadeTarihi":"2026-11-20","tutar":10,"kendiHesapId":"'"$BANKA"'"}]}'
kontrol 409 "Ayni hesaptan ayni seri cek tekrar verilemez" evrak_zaten_kayitli
TARIH=2026-11-20 islem Odeme '["'"$VC1"'"]'
kontrol 201 "Verilen cek vadede odendi (bagli bankadan)"
bakiye "$BANKA" "Banka 60.000 - 3.000" 57000
TARIH=2026-12-10 islem Odeme '["'"$VS1"'"]'
kontrol 400 "Senet odemesinde hesap secilmeli" evrak_hesap_gerekli
TARIH=2026-12-10 islem Odeme '["'"$VS1"'"]' '"hesapId":"'"$KASA"'"'
kontrol 201 "Senet kasadan odendi"
bakiye "$KASA" "Kasa 2.000 - 1.500" 500
cari_bakiye "$C2" "Odemeler cariyi tekrar etkilemedi" 5500
TARIH=2026-12-15 islem VerilenKarsiliksiz '["'"$VC2"'"]'
kontrol 201 "Kendi cekimiz karsiliksiz cikti"
cari_bakiye "$C2" "Tedarikcinin alacagi geri dogdu: 4.500" 4500
TARIH=2026-12-16 islem KarsiliksizGeriAlma '["'"$VC2"'"]'
kontrol 201 "Karsiliksiz cekimiz geri alindi"
durum "$VC2" "Iade alindi" IadeAlindi

echo "--- Doviz"
istek POST "$CS/giris" "$T" '{"cariHesapId":"'"$C1"'","tarih":"2026-11-22","riskOnaylandi":false,"evraklar":[{"tur":"Senet","seriNo":"USD-1","vadeTarihi":"2026-12-30","tutar":100,"paraBirimi":"USD","kur":42,"borclu":{"ad":"Mehmet Kaya"}}]}'
kontrol 422 "TL cariden dovizli evrak icin farkli doviz izni gerekir" farkli_doviz
TARIH=2026-11-26 islem TahsileVerme '["'"$AC4"'"]' '"hesapId":"'"$BANKA_USD"'"'
kontrol 422 "TL cek dovizli bankaya tahsile verilemez" evrak_hesap_dovizi

echo "--- Raporlar"
istek GET "$CS/bordrolar/$BR1" "$T"
kontrol 200 "Bordro detayi iki evrakla" '"bordroNo":"BR00001","islem":"Giris".*"cariUnvan":"Musteri A".*"AC00001".*"AS00001"'
istek GET "$CS/cari-riski/$C1" "$T"
kontrol 200 "Musteri cek riski: portfoyde 1.000" '"paraBirimi":"TRY","portfoyde":1000(\.0+)?,"ciroRiski":0(\.0+)?,"karsiliksiz":0'
istek GET "$CS/raporlar/vade-takvimi?baslangic=2026-10-01&bitis=2026-12-31&gruplama=Ay" "$T"
kontrol 200 "Vade takviminde aralik ayi: alinan 1.000" '"baslangic":"2026-12-01","bitis":"2026-12-31","alinanTl":1000(\.0+)?,"verilenTl":0'
istek GET "$CS/raporlar/ozet" "$T"
kontrol 200 "Ozet: portfoy 1.000 TL" '"portfoyTl":1000(\.0+)?,"portfoyAdet":1'
istek GET "$CS/evraklar?yon=Alinan&durum=Iptal" "$T"
kontrol 200 "Iptal evraklar yalniz acikca istenince listelenir" '"portfoyNo":"AC00003"'
istek GET "$CS/evraklar?arama=1002" "$T"
kontrol 200 "Seri no ile arama" '"toplamKayit":1'

echo "--- Kesideci karti ve yetki"
istek DELETE "$CS/borclular/$AHMET_ID" "$T"
kontrol 422 "Evraki olan kesideci silinemez" borclu_evrakli
istek DELETE "$CS/borclular/$RISKLI" "$T"
kontrol 204 "Evraksiz kesideci silindi"
istek GET "$CS/evraklar/$AC1" "$T2"
kontrol 404 "Baska firma evraki goremez"
istek POST "$CS/islemler" "$T2" '{"islem":"Iade","tarih":"2026-10-20","evrakIdleri":["'"$AC4"'"]}'
kontrol 404 "Baska firma evrakina islem yapamaz"
istek GET "$CS/borclular" "$T2"
kontrol 200 "Baska firma kesidecileri gormez" '"toplamKayit":0'

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
