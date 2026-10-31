#!/usr/bin/env bash
# Abonelik: paketler, kullanıcı limiti, havale bildirimi ve onayı uçtan uca senaryosu. Kullanım: abonelik_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_abonelik"; rm -rf "$DIR"; mkdir -p "$DIR"
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
PK=/api/yonetim/sistem/paketler
paket() { # (KODU degiskeni) AD AYLIK YILLIK LIMIT [TUM] [SATISTA]
  echo '{"kod":"'"$KODU"'","ad":"'"$1"'","aciklama":"'"$ACIKLAMA"'","tumModullerMi":'"${5:-false}"',"moduller":["cari","stok","fatura","kasabanka"],"kullaniciLimiti":'"$4"',"aylikUcret":'"$2"',"yillikUcret":'"$3"',"satistaMi":'"${6:-true}"',"sira":'"$SIRA"'}'
}
bildir() { echo '{"paketKodu":"'"$1"'","aySayisi":'"$2"',"tutar":'"$3"',"odemeTarihi":"2026-10-20","gonderen":"Abone Ltd","aciklama":"EFT dekont 123"}'; }

U=$(date +%s)
istek POST /api/yonetim/giris "" "$SISTEM_GIRIS"; SYS=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Abone Ltd '"$U"'","ad":"Ayla","soyad":"Bal","eposta":"abone'"$U"'@ornek.com","sifre":"Sifre1234"}'; T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Abone","ad":"D","soyad":"A","eposta":"abonediger'"$U"'@ornek.com","sifre":"Sifre1234"}'; T2=$(alan accessToken)
davet_kabul "$T" '{"ad":"Kaan","soyad":"Kul","eposta":"abonekul'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Kullanici"}'
istek POST /api/yonetim/giris "" '{"eposta":"abonekul'"$U"'@ornek.com","sifre":"Sifre1234"}'; TK=$(alan accessToken)

echo "--- Paketler (sistem)"
istek GET $PK "$SYS"
kontrol 200 "Temel/Profesyonel/Kurumsal tanimli, deneme satista degil" '"kod":"deneme","ad":"Deneme".*"satistaMi":false.*"kod":"basic","ad":"Temel".*"kod":"profesyonel".*"kod":"kurumsal"'
KODU=basic; SIRA=1; ACIKLAMA="Temel test"; istek PUT $PK/basic "$SYS" "$(paket Temel 500 5000 2)"
kontrol 200 "Temel ucretleri girildi" '"aylikUcret":500,"yillikUcret":5000'
KODU=profesyonel; SIRA=2; ACIKLAMA="Profesyonel test"; istek PUT $PK/profesyonel "$SYS" "$(paket Profesyonel 900 null 5)"
kontrol 200 "Profesyonel yalniz aylik" '"aylikUcret":900,"yillikUcret":null'
KODU=kurumsal; SIRA=3; ACIKLAMA="Kurumsal test"; istek PUT $PK/kurumsal "$SYS" "$(paket Kurumsal null null null true)"
kontrol 200 "Kurumsal ucretsiz (teklif usulu)" '"tumModullerMi":true,"moduller":\[\],"aciklama":"Kurumsal test","kullaniciLimiti":null,"aylikUcret":null'
KODU=basic; SIRA=1; ACIKLAMA="Temel test"; istek PUT $PK/basic "$SYS" "$(paket Temel -1 5000 0)"
kontrol 400 "Eksi ucret ve sifir limit reddedildi"
istek POST $PK "$SYS" '{"kod":"Buyuk Paket","ad":"X","tumModullerMi":false,"moduller":["uydurma"],"satistaMi":false,"sira":9}'
kontrol 400 "Gecersiz kod ve modul reddedildi" 'küçük harf.*Geçersiz modül kodu'
istek POST $PK "$SYS" '{"kod":"basic","ad":"X","tumModullerMi":true,"moduller":[],"satistaMi":false,"sira":9}'
kontrol 409 "Ayni kodla ikinci paket olmaz" paket_kodu_kayitli
istek GET $PK "$T"
kontrol 403 "Firma yoneticisi paket tanimlayamaz/goremez"

echo "--- Odeme bilgileri"
istek PUT /api/yonetim/sistem/odeme-bilgileri "$SYS" '{"alici":"Ornek Yazilim","banka":"Ziraat","iban":"TR00 0000","not":null}'
kontrol 400 "Gecersiz IBAN reddedildi" IBAN
istek PUT /api/yonetim/sistem/odeme-bilgileri "$SYS" '{"alici":null,"banka":null,"iban":null,"not":null}'
kontrol 200 "Odeme hesabi bosaltildi"
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" "$(bildir basic 12 5000)"
kontrol 422 "Odeme hesabi yokken bildirim alinmaz" odeme_bilgisi_yok
istek PUT /api/yonetim/sistem/odeme-bilgileri "$SYS" '{"alici":"Ornek Yazilim AS","banka":"Ziraat Bankasi","iban":"tr33 0006 1005 1978 6457 8413 26","not":"Aciklamaya firma unvaninizi yazin."}'
kontrol 200 "IBAN bosluksuz ve buyuk harfle kaydedildi" '"iban":"TR330006100519786457841326"'

echo "--- Abonelik ekrani (firma)"
istek GET /api/yonetim/abonelik "$T"
kontrol 200 "Deneme paketi, 14 gun, 2 kullanici, sinirsiz" '"paketKodu":"deneme","paketAdi":"Deneme".*"kalanGun":14,"saltOkunur":false,"kullaniciSayisi":2,"kullaniciLimiti":null'
kontrol 200 "Satistaki paketler ucretleriyle" '"paketler":\[\{"kod":"basic","ad":"Temel".*"aylikUcret":500(.00)?,"yillikUcret":5000(.00)?[,}].*"kod":"kurumsal"'
kontrol 200 "IBAN gorunuyor" '"odemeBilgileri":\{"alici":"Ornek Yazilim AS","banka":"Ziraat Bankasi","iban":"TR330006100519786457841326"'
if echo "$GOVDE" | grep -q '"kod":"deneme"'; then KOD=999; fi
kontrol 200 "Deneme paketi satis listesinde yok"
istek GET /api/yonetim/abonelik "$TK"
kontrol 403 "Kullanici rolu abonelik ekranini goremez"

echo "--- Bildirim"
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" "$(bildir deneme 1 10)"
kontrol 422 "Satista olmayan pakete bildirim yok" paket_satista_degil
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" "$(bildir profesyonel 12 9000)"
kontrol 422 "Ucreti olmayan sureye bildirim yok" sure_satilmiyor
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" "$(bildir basic 6 3000)"
kontrol 400 "Sure 1 ya da 12 ay" 'aylık \(1\) ya da yıllık \(12\)'
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" '{"paketKodu":"basic","aySayisi":12,"tutar":5000,"odemeTarihi":"2099-01-01","gonderen":"X"}'
kontrol 400 "Ileri tarihli odeme yok" 'ileri bir tarih'
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" "$(bildir basic 12 5000)"
kontrol 201 "Yillik Temel bildirimi alindi" '"paketKodu":"basic","paketAdi":"Temel","aySayisi":12,"tutar":5000(.00)?,"paketUcreti":5000(.00)?.*"durum":"Bekliyor"'
B1=$(alan id)
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" "$(bildir basic 12 5000)"
kontrol 409 "Onay beklerken ikinci bildirim yok" bekleyen_bildirim_var
istek GET /api/yonetim/abonelik "$T2"
kontrol 200 "Diger firma bu bildirimi gormez" '"bildirimler":\[\]'

echo "--- Onay / ret (sistem)"
istek GET "/api/yonetim/sistem/odeme-bildirimleri?durum=Bekliyor" "$SYS"
kontrol 200 "Bekleyenlerde firma unvaniyla" "\"id\":\"$B1\",\"firmaId\":\"[^\"]*\",\"firmaUnvani\":\"Abone Ltd $U\""
istek GET "/api/yonetim/sistem/odeme-bildirimleri" "$T"
kontrol 403 "Firma yoneticisi tum bildirimleri goremez"
istek POST "/api/yonetim/sistem/odeme-bildirimleri/$B1/reddet" "$SYS" '{"not":""}'
kontrol 400 "Ret nedeni zorunlu"
istek POST "/api/yonetim/sistem/odeme-bildirimleri/$B1/reddet" "$SYS" '{"not":"Hesabimiza ulasmadi"}'
kontrol 200 "Reddedildi" '"durum":"Reddedildi","sonucNotu":"Hesabimiza ulasmadi"'
istek POST "/api/yonetim/sistem/odeme-bildirimleri/$B1/onayla" "$SYS" '{}'
kontrol 422 "Sonuclanan bildirim tekrar islenmez" bildirim_sonuclanmis
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T" "$(bildir profesyonel 1 900)"
kontrol 201 "Ret sonrasi yeni bildirim"; B2=$(alan id)
istek POST "/api/yonetim/sistem/odeme-bildirimleri/$B2/onayla" "$SYS" '{"not":"Tesekkurler"}'
kontrol 200 "Onaylandi, yeni bitis yazildi" '"durum":"Onaylandi","sonucNotu":"Tesekkurler".*"yeniBitisTarihi":"20'
istek GET /api/yonetim/abonelik "$T"
kontrol 200 "Firma Profesyonele gecti, kalan sure denemenin ustune eklendi" '"paketKodu":"profesyonel","paketAdi":"Profesyonel".*"kalanGun":4[2-5],"saltOkunur":false,"kullaniciSayisi":2,"kullaniciLimiti":5'
kontrol 200 "Bildirim gecmisi firmada" '"durum":"Onaylandi".*"durum":"Reddedildi"'

echo "--- Kullanici limiti"
KODU=profesyonel; SIRA=2; ACIKLAMA="Profesyonel test"; istek PUT $PK/profesyonel "$SYS" "$(paket Profesyonel 900 null 2)"
istek POST /api/yonetim/kullanicilar "$T" '{"ad":"Ucuncu","soyad":"K","eposta":"abone3'"$U"'@ornek.com","sifre":"Sifre1234","rolId":"'"$(rol_id "$T" Kullanici)"'"}'
kontrol 422 "Limit (2) dolu: ucuncu kullanici eklenemez" 'Paketinizin kullanıcı limiti \(2\) dolu.*"kod":"kullanici_limiti"'
istek GET /api/yonetim/kullanicilar "$T"
KUL=$(echo "$GOVDE" | grep -o '"kullaniciId":"[^"]*","ad":"Kaan"' | cut -d'"' -f4)
istek POST "/api/yonetim/kullanicilar/$KUL/pasiflestir" "$T"
istek POST /api/yonetim/kullanicilar "$T" '{"ad":"Ucuncu","soyad":"K","eposta":"abone3'"$U"'@ornek.com","sifre":"Sifre1234","rolId":"'"$(rol_id "$T" Kullanici)"'"}'
kontrol 201 "Bir kullanici pasiflesince yer acildi"
KODU=profesyonel; SIRA=2; ACIKLAMA="Profesyonel test"; istek PUT $PK/profesyonel "$SYS" "$(paket Profesyonel 900 null 5)"

echo "--- Suresi dolmus firma"
FIRMA=$(echo "$T2" | cut -d. -f2 | tr '_-' '/+' | base64 -d 2>/dev/null | grep -o '"firma_id":"[^"]*' | cut -d'"' -f4)
istek PUT "/api/yonetim/sistem/firmalar/$FIRMA/lisans" "$SYS" '{"paketKodu":"basic","bitisTarihi":"2020-01-01","ekModuller":[]}'
istek POST /api/cari/hesaplar "$T2" '{"unvan":"X","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'
kontrol 403 "Suresi dolan firma kayit giremez" lisans_suresi_doldu
istek POST /api/yonetim/kullanicilar "$T2" '{"eposta":"saltokunur'"$U"'@ornek.com","rolId":"'"$(rol_id "$T2" Kullanici)"'"}'
kontrol 403 "Suresi dolan firma kullanici davet edemez" lisans_suresi_doldu
istek GET /api/yonetim/abonelik "$T2"
kontrol 200 "Abonelik ekrani salt okunur durumu gosterir" '"paketKodu":"basic".*"kalanGun":0,"saltOkunur":true'
istek POST /api/yonetim/abonelik/odeme-bildirimleri "$T2" "$(bildir basic 1 500)"
kontrol 201 "Suresi dolan firma bildirim gonderebilir"; B3=$(alan id)
istek POST "/api/yonetim/sistem/odeme-bildirimleri/$B3/onayla" "$SYS"
kontrol 200 "Govdesiz onay da olur" '"durum":"Onaylandi"'
istek GET /api/yonetim/abonelik "$T2"
kontrol 200 "Bugunden itibaren 1 ay acildi" '"kalanGun":(2[89]|3[01]),"saltOkunur":false'
istek POST /api/cari/hesaplar "$T2" '{"unvan":"X","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'
kontrol 201 "Onay sonrasi yazma acildi (lisans onbellegi temizlendi)"

# Paket tanımları testin değiştirdiği açıklamalardan başlangıçtakilere döner (tarayıcı testleri bu metinleri gösterir).
sql "UPDATE yonetim.paket SET aciklama = 'Cari, stok, fatura ve kasa/banka; küçük işletmeler için.', sira = 1 WHERE kod = 'basic';
UPDATE yonetim.paket SET aciklama = 'Temel pakete ek olarak daha fazla kullanıcı.', sira = 2 WHERE kod = 'profesyonel';
UPDATE yonetim.paket SET aciklama = 'Tüm modüller, sınırsız kullanıcı.', sira = 3 WHERE kod = 'kurumsal';" > /dev/null

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
