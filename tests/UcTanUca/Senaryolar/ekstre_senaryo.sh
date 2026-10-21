#!/usr/bin/env bash
# Banka/kart/POS ekstresi içe aktarma uçtan uca senaryosu. Kullanım: ekstre_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_ekstre"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(--data-binary "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
yukle() { # HESAP DOSYA [TOKEN]
  KOD=$(curl -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/kasabanka/hesaplar/$1/ekstre-aktarimlari" \
    -H "Authorization: Bearer ${3:-$T}" ${2:+-F "dosya=@$DOS/$2"}); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -qE -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:600}"; KALAN=$((KALAN+1))
  fi
}
nesne_id() { echo "$GOVDE" | sed 's/},{/}\n{/g' | grep -- "$1" | head -1 | grep -o '"id":"[^"]*' | head -1 | sed -E 's/.*:"//'; }
# Aktarım detayında açıklaması verilen kalıpla başlayan satırın id'si ve o satırın JSON'u.
satir_id() { echo "$GOVDE" | grep -oE "\"id\":\"[^\"]*\",\"sira\":[0-9]+,\"tarih\":\"[^\"]*\",\"aciklama\":\"$1" | head -1 | sed -E 's/"id":"([^"]*)".*/\1/'; }
satir() { echo "$GOVDE" | sed 's/},{/}\n{/g' | grep -E "\"aciklama\":\"$1" | head -1; }
satir_kontrol() { # KALIP ACIKLAMA BEKLENEN
  local s; s=$(satir "$1")
  if echo "$s" | grep -qE -- "$3"; then echo "GECTI  $2"; GECEN=$((GECEN+1)); else echo "KALDI  $2 (beklenen '$3') -> ${s:0:500}"; KALAN=$((KALAN+1)); fi
}
duzenle() { # AKTARIM SATIR TUR [CARI] [KATEGORI] [KARSI] [KELIME]
  local c=null k=null h=null w=null
  [ -n "$4" ] && c="\"$4\""; [ -n "$5" ] && k="\"$5\""; [ -n "$6" ] && h="\"$6\""; [ -n "$7" ] && w="\"$7\""
  istek PUT "/api/kasabanka/ekstre-aktarimlari/$1/satirlar/$2" "$T" \
    '{"islemTuru":"'"$3"'","cariHesapId":'"$c"',"kategoriId":'"$k"',"karsiHesapId":'"$h"',"eslesmeyiKaldir":false,"hatirlanacakKelime":'"$w"'}'
}
bakiye() { istek GET "/api/kasabanka/hesaplar/$1" "$T"; kontrol 200 "$2" "\"bakiye\":$3(\.0+)?,"; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Ekstre Test AS","ad":"Ece","soyad":"Ak","eposta":"ekstre'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"ekstrediger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Anadolu Gida Ltd. Sti.","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; ANADOLU=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Beta Ticaret","tur":"Musteri","kisiTipi":"Tuzel","vergiNo":"1234567890","paraBirimi":"TRY"}'; BETA=$(alan id)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Musteri A","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; C1=$(alan id)
istek GET /api/kasabanka/kategoriler "$T"
ELEKTRIK=$(nesne_id '"ad":"Elektrik'); KOMISYON=$(nesne_id 'POS komisyonu'); YAKIT=$(nesne_id 'Yak'); DIGER=$(nesne_id '"ad":"Di[^"]*giderler')
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Merkez Kasa","tur":"Kasa","paraBirimi":"TRY"}'; KASA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Is Bankasi","tur":"Banka","paraBirimi":"TRY","iban":"TR33 0006 1005 1978 6457 8413 26"}'; BANKA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Vakif TL","tur":"Banka","paraBirimi":"TRY"}'; BANKA2=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Sirket Karti","tur":"KrediKarti","paraBirimi":"TRY","hesapNo":"5400 **** 4821","limit":50000}'; KART=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Magaza POS","tur":"Pos","paraBirimi":"TRY"}'; POS=$(alan id)
istek POST /api/kasabanka/islemler/acilis "$T" '{"hesapId":"'"$BANKA"'","tarih":"2026-10-01","tutar":10000}'
# Ekstreden önce elle girilmiş tahsilat: ekstredeki 12.10 tarihli 2.000 TL'lik satırla eşleşmeli.
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$BANKA"'","cariHesapId":"'"$C1"'","tarih":"2026-10-11","tutar":2000}'
ELLE=$(alan hareketId)

echo "--- Yukleme dogrulamalari"
yukle "$KASA" banka.csv
kontrol 422 "Kasa hesabina ekstre yuklenemez" kasaya_ekstre_yok
yukle "$BANKA" bozuk.xlsx
kontrol 400 "Bozuk Excel anlasilir hatayla reddedildi" dosya_okunamadi
yukle "$BANKA" buyuk.csv
kontrol 400 "5 MB ustu dosya reddedildi" dosya_cok_buyuk
yukle "$BANKA" ""
kontrol 400 "Dosyasiz istek reddedildi"
yukle "$BANKA" banka.csv "$T2"
kontrol 404 "Baska firma hesabina ekstre yukleyemez"

echo "--- CSV (Windows-1254): kolon esleme"
yukle "$BANKA" banka.csv
kontrol 201 "CSV yuklendi, esleme bekliyor" '"bicim":"Csv","durum":"EslemeBekliyor"'
A1=$(alan id)
kontrol 201 "Turkce karakterler Windows-1254'ten dogru cozuldu" '"Tarih","Açıklama","Borç","Alacak","Bakiye"'
kontrol 201 "Banka basligi atlanip kolonlar tahmin edildi" '"esleme":\{"baslikSatiri":2,"tarihKolonu":0,"aciklamaKolonu":1,"tutarKolonu":null,"alacakKolonu":3,"borcKolonu":2,"bakiyeKolonu":4'
kontrol 201 "Eslemeden once satir yok" '"satirlar":\[\]'
istek POST "/api/kasabanka/ekstre-aktarimlari/$A1/aktar" "$T"
kontrol 422 "Esleme yapilmadan aktarilamaz" aktarim_hazir_degil
istek PUT "/api/kasabanka/ekstre-aktarimlari/$A1/esleme" "$T" '{"esleme":{"baslikSatiri":2,"tarihKolonu":0,"aciklamaKolonu":1,"tutarKolonu":null,"alacakKolonu":3,"borcKolonu":null,"bakiyeKolonu":null,"referansKolonu":null,"tutarTersMi":false},"sablonuKaydet":false}'
kontrol 400 "Tutar ya da borc+alacak kolonu zorunlu" "Tutar kolonu"
istek PUT "/api/kasabanka/ekstre-aktarimlari/$A1/esleme" "$T" '{"esleme":{"baslikSatiri":2,"tarihKolonu":0,"aciklamaKolonu":1,"tutarKolonu":2,"alacakKolonu":null,"borcKolonu":null,"bakiyeKolonu":-1,"referansKolonu":500,"tutarTersMi":false},"sablonuKaydet":true}'
kontrol 400 "Gecersiz kolon numarasi (negatif/sinir disi) sablona kaydedilmez" "BakiyeKolonu|ReferansKolonu"
istek PUT "/api/kasabanka/ekstre-aktarimlari/$A1/esleme" "$T" '{"esleme":{"baslikSatiri":2,"tarihKolonu":1,"aciklamaKolonu":0,"tutarKolonu":4,"alacakKolonu":null,"borcKolonu":null,"bakiyeKolonu":null,"referansKolonu":null,"tutarTersMi":false},"sablonuKaydet":false}'
kontrol 400 "Satir vermeyen esleme reddedildi" esleme_satir_vermedi
istek PUT "/api/kasabanka/ekstre-aktarimlari/$A1/esleme" "$T" '{"esleme":{"baslikSatiri":2,"tarihKolonu":0,"aciklamaKolonu":1,"tutarKolonu":null,"alacakKolonu":3,"borcKolonu":2,"bakiyeKolonu":4,"referansKolonu":null,"tutarTersMi":false},"sablonuKaydet":true}'
kontrol 200 "Esleme uygulandi: onay bekliyor, donem 05-12.10" '"durum":"OnayBekliyor".*"baslangic":"2026-10-05","bitis":"2026-10-12","bakiyeUyarisi":null'
kontrol 200 "Toplam satiri atlandi: 7 hareket" '"sira":7,'
kontrol 200 "Borc kolonu negatif tutar" '"aciklama":"ENERJ[^"]*ELEKTR[^"]*","tutar":-800'

echo "--- Oneriler ve mutabakat"
satir_kontrol "EFT GELEN ANADOLU" "Unvandan cari onerildi (tahsilat)" '"tutar":1500(\.0+)?,.*"islemTuru":"Tahsilat","cariHesapId":"'"$ANADOLU"'","cariUnvan":"Anadolu Gida Ltd. Sti.".*"oneriKaynagi":"Unvan"'
satir_kontrol "HAVALE VKN" "VKN'den cari onerildi" '"cariHesapId":"'"$BETA"'".*"oneriKaynagi":"VKN/TCKN"'
satir_kontrol "KREDI KARTI ODEMESI" "Kart odemesi kendi kart hesabina virman onerildi" '"islemTuru":"Virman".*"karsiHesapId":"'"$KART"'".*"oneriKaynagi":"Kendi hesab'
satir_kontrol "GELEN HAVALE MUSTERI" "Elle girilen tahsilat 1 gun farkla eslesti (zaten kayitli)" '"durum":"ZatenKayitli".*"eslesenHareketId":"[0-9a-f-]{36}","eslesenHareketTarihi":"2026-10-11"'
satir_kontrol "ENERJ[^\"]*A EK" "Kurali olmayan cikis: varsayilan gider" '"islemTuru":"Gider".*"kategoriId":null.*"oneriKaynagi":"Varsay'

echo "--- Satir duzenleme ve kural ogrenme"
S_ELEKTRIK=$(satir_id "ENERJ[^\"]*ELEKTR"); S_EK=$(satir_id "ENERJ[^\"]*A EK"); S_MASRAF=$(satir_id "HESAP ")
duzenle "$A1" "$S_ELEKTRIK" Gider "" "$ELEKTRIK" "" "AB"
kontrol 400 "Kisa hatirlanacak kelime reddedildi" "en az 3"
duzenle "$A1" "$S_ELEKTRIK" Gider "" "$ELEKTRIK" "" "enerjisa"
kontrol 200 "Satir elle duzenlendi" '"kategoriId":"'"$ELEKTRIK"'".*"elleDuzenlendi":true'
satir_kontrol "ENERJ[^\"]*A EK" "Ogrenilen kural ayni aktarimdaki diger satira uygulandi" '"kategoriId":"'"$ELEKTRIK"'".*"oneriKaynagi":"Kural".*"elleDuzenlendi":false'
istek GET /api/kasabanka/ekstre-kurallari "$T"
kontrol 200 "Kural kaydedildi (normalize, cikis yonu)" '"anahtarKelime":"ENERJISA","yon":"Cikis","islemTuru":"Gider".*"kategoriAdi":"Elektrik'
istek GET "/api/kasabanka/ekstre-aktarimlari/$A1" "$T2"
kontrol 404 "Baska firma aktarimi goremez"

echo "--- Aktarma (satir bazinda)"
istek POST "/api/kasabanka/ekstre-aktarimlari/$A1/aktar" "$T"
kontrol 200 "5 aktarildi, 1 zaten kayitli, 1 hatali" '"aktarilan":5,"zatenKayitli":1,"atlanan":0,"hatali":1'
kontrol 200 "Hatali satir varken aktarim acik kalir" '"durum":"OnayBekliyor"'
satir_kontrol "HESAP " "Kategorisiz gider satiri hatali, sebebi yazili" '"durum":"Hatali".*"hata":"Gelir/gider i[^"]*kategori'
satir_kontrol "EFT GELEN ANADOLU" "Aktarilan satira islem baglandi" '"durum":"Aktarildi".*"islemId":"[0-9a-f-]{36}"'
bakiye "$BANKA" "Banka bakiyesi: 10.000 + 2.000 elle + 1.500 - 920 - 3.000 + 900 = 10.480" 10480
bakiye "$KART" "Kart hesabina 3.000 virman girdi" 3000
istek GET "/api/cari/hesaplar/$ANADOLU" "$T"
kontrol 200 "Anadolu cari 1.500 alacakli" '"bakiye":-1500(\.0+)?,'
istek GET "/api/kasabanka/hesaplar/$BANKA/hareketler" "$T"
kontrol 200 "Elle girilen tahsilat tekrar yazilmadi (tek 2.000 giris)" '"giris":2000'
[ "$(echo "$GOVDE" | grep -o '"giris":2000' | wc -l)" = "1" ] && { echo "GECTI  2.000 TL'lik giris bir kez"; GECEN=$((GECEN+1)); } || { echo "KALDI  2.000 TL'lik giris tekrarlandi"; KALAN=$((KALAN+1)); }
istek DELETE "/api/kasabanka/ekstre-aktarimlari/$A1" "$T"
kontrol 422 "Satiri aktarilmis aktarim silinemez" aktarim_silinemez
istek GET "/api/kasabanka/ekstre-aktarimlari/$A1" "$T"
S_ANADOLU=$(satir_id "EFT GELEN ANADOLU")
duzenle "$A1" "$S_ANADOLU" Atla
kontrol 422 "Aktarilmis satir degistirilemez" satir_degistirilemez
duzenle "$A1" "$S_MASRAF" Tahsilat "$C1"
kontrol 200 "Hatali satir duzenlenebilir"
istek POST "/api/kasabanka/ekstre-aktarimlari/$A1/aktar" "$T"
kontrol 200 "Cikis satirina tahsilat secimi yon hatasi verdi" '"aktarilan":0,.*"hatali":1.*"hata":"[^"]*yle uyu'
duzenle "$A1" "$S_MASRAF" Gider "" "$DIGER"
istek POST "/api/kasabanka/ekstre-aktarimlari/$A1/aktar" "$T"
kontrol 200 "Duzeltilen satir aktarildi, aktarim tamamlandi" '"aktarilan":1,"zatenKayitli":1,"atlanan":0,"hatali":0.*"durum":"Tamamlandi"'
bakiye "$BANKA" "Banka bakiyesi ekstre kapanisiyla ayni: 10.470" 10470
istek POST "/api/kasabanka/ekstre-aktarimlari/$A1/aktar" "$T"
kontrol 422 "Tamamlanan aktarim tekrar aktarilamaz" aktarim_hazir_degil
istek GET "/api/kasabanka/raporlar/gelir-gider?baslangic=2026-10-01&bitis=2026-10-31" "$T"
kontrol 200 "Ekstreden gelen elektrik giderleri rapora yansidi (920)" '"Elektrik[^}]*920(\.0+)?[,}]'

echo "--- Tekrar yukleme ve sablon"
yukle "$BANKA" banka.csv
kontrol 201 "Kayitli sablonla esleme ekrani atlandi" '"durum":"OnayBekliyor"'
A2=$(alan id)
[ "$(echo "$GOVDE" | grep -o '"durum":"DahaOnceAktarildi"' | wc -l)" = "7" ] && { echo "GECTI  Ayni dosyanin 7 satiri da 'daha once aktarildi'"; GECEN=$((GECEN+1)); } || { echo "KALDI  Tekrar yuklemede satirlar isaretlenmedi -> ${GOVDE:0:400}"; KALAN=$((KALAN+1)); }
istek DELETE "/api/kasabanka/ekstre-aktarimlari/$A2" "$T"
kontrol 204 "Aktarilmamis tekrar yukleme silindi"
istek GET "/api/kasabanka/hesaplar/$BANKA/ekstre-aktarimlari" "$T"
kontrol 200 "Aktarim gecmisi: 1 kayit, 7/7" '^\[\{"id":"'"$A1"'".*"satirSayisi":7,"aktarilanSayisi":7\}\]$'

echo "--- Kredi karti CSV (UTF-8, ters isaret)"
yukle "$KART" kart.csv
kontrol 201 "Kart ekstresi esleme bekliyor, tek tutar kolonu tahmin edildi" '"durum":"EslemeBekliyor".*"tutarKolonu":2'
A3=$(alan id)
istek PUT "/api/kasabanka/ekstre-aktarimlari/$A3/esleme" "$T" '{"esleme":{"baslikSatiri":0,"tarihKolonu":0,"aciklamaKolonu":1,"tutarKolonu":2,"alacakKolonu":null,"borcKolonu":null,"bakiyeKolonu":null,"referansKolonu":null,"tutarTersMi":true},"sablonuKaydet":false}'
kontrol 200 "Ingilizce tutar ve ters isaret: harcama -1.250,40" '"aciklama":"M[^"]*GROS MARKET","tutar":-1250.4'
S_SHELL=$(satir_id "SHELL"); S_MARKET=$(satir_id "M[^\"]*GROS")
duzenle "$A3" "$S_SHELL" Gider "" "$YAKIT"
duzenle "$A3" "$S_MARKET" Atla
kontrol 200 "Satir atlanacak olarak isaretlendi" '"islemTuru":"Atla"'
istek POST "/api/kasabanka/ekstre-aktarimlari/$A3/aktar" "$T"
kontrol 200 "1 aktarildi 1 atlandi, tamamlandi" '"aktarilan":1,"zatenKayitli":0,"atlanan":1,"hatali":0.*"durum":"Tamamlandi"'
bakiye "$KART" "Kart: 3.000 - 900" 2100

echo "--- POS Excel"
yukle "$POS" pos.xlsx
kontrol 201 "xlsx okundu, kolonlar tahmin edildi" '"bicim":"Excel","durum":"EslemeBekliyor".*"tutarKolonu":2,"alacakKolonu":null,"borcKolonu":null,"bakiyeKolonu":3'
A4=$(alan id)
kontrol 201 "Excel sayi hucreleri metne cevrildi" '"16.10.2026","POS SATIS BATCH 101","2400.50"'
istek PUT "/api/kasabanka/ekstre-aktarimlari/$A4/esleme" "$T" '{"esleme":{"baslikSatiri":0,"tarihKolonu":0,"aciklamaKolonu":1,"tutarKolonu":2,"alacakKolonu":null,"borcKolonu":null,"bakiyeKolonu":3,"referansKolonu":null,"tutarTersMi":false},"sablonuKaydet":true}'
kontrol 200 "3 satir, bakiye tutarli" '"bakiyeUyarisi":null.*"tutar":-48.01'
S_KOM=$(satir_id "POS KOMISYON")
duzenle "$A4" "$S_KOM" Gider "" "$KOMISYON"
istek POST "/api/kasabanka/ekstre-aktarimlari/$A4/aktar" "$T"
kontrol 200 "Carisiz tahsilat satirlari hatali, komisyon aktarildi" '"aktarilan":1,"zatenKayitli":0,"atlanan":0,"hatali":2.*"hata":"Tahsilat/'
S_B1=$(satir_id "POS SATIS BATCH 101")
duzenle "$A4" "$S_B1" Tahsilat "$C1" "" "" "POS SATIS"
kontrol 200 "Kural ogrenildi; hatali satira otomatik uygulanmaz (yalniz bekleyenlere)" '"aciklama":"POS SATIS BATCH 102".*"cariHesapId":null'
S_B2=$(satir_id "POS SATIS BATCH 102")
duzenle "$A4" "$S_B2" Tahsilat "$C1"
istek POST "/api/kasabanka/ekstre-aktarimlari/$A4/aktar" "$T"
kontrol 200 "Kalan 2 satir aktarildi" '"aktarilan":2,.*"hatali":0.*"durum":"Tamamlandi"'
bakiye "$POS" "POS bakiyesi ekstreyle ayni: 3.352,49" 3352.49

echo "--- MT940"
yukle "$BANKA2" ekstre.sta
kontrol 201 "MT940 dogrudan cozuldu (esleme yok)" '"bicim":"Mt940","durum":"OnayBekliyor".*"bakiyeUyarisi":null'
A5=$(alan id)
satir_kontrol "ENERJISA ELEKTRIK OTOMATIK" "Ogrenilen kural yeni ekstrede uygulandi" '"tutar":-450(\.0+)?,.*"islemTuru":"Gider".*"kategoriId":"'"$ELEKTRIK"'".*"oneriKaynagi":"Kural"'
satir_kontrol "EFT GELEN ANADOLU" "MT940 referansi ve cari onerisi" '"referans":"REF778".*"cariHesapId":"'"$ANADOLU"'"'
istek PUT "/api/kasabanka/ekstre-aktarimlari/$A5/esleme" "$T" '{"esleme":{"baslikSatiri":0,"tarihKolonu":0,"aciklamaKolonu":1,"tutarKolonu":2,"alacakKolonu":null,"borcKolonu":null,"bakiyeKolonu":null,"referansKolonu":null,"tutarTersMi":false},"sablonuKaydet":false}'
kontrol 422 "MT940 aktariminda esleme degistirilemez" esleme_degistirilemez
istek POST "/api/kasabanka/ekstre-aktarimlari/$A5/aktar" "$T"
kontrol 200 "MT940 satirlari aktarildi" '"aktarilan":2,.*"durum":"Tamamlandi"'
bakiye "$BANKA2" "Vakif bakiyesi: 1.200 - 450" 750
istek GET /api/kasabanka/ekstre-kurallari "$T"
kontrol 200 "Kural kullanim sayisi arttı (2 otomatik uygulama)" '"anahtarKelime":"ENERJISA"[^}]*"kullanimSayisi":2'

echo "--- Kural yonetimi"
istek POST /api/kasabanka/ekstre-kurallari "$T" '{"anahtarKelime":"shell","yon":"Cikis","islemTuru":"Gider","kategoriId":"'"$YAKIT"'"}'
kontrol 201 "Kural elle eklendi"; K1=$(alan id)
istek POST /api/kasabanka/ekstre-kurallari "$T" '{"anahtarKelime":"SHELL","yon":"Cikis","islemTuru":"Gider","kategoriId":"'"$YAKIT"'"}'
kontrol 409 "Ayni kelime+yon ikinci kez eklenemez"
istek POST /api/kasabanka/ekstre-kurallari "$T" '{"anahtarKelime":"ab","yon":"Cikis","islemTuru":"Gider"}'
kontrol 400 "Kisa anahtar kelime reddedildi"
istek PUT "/api/kasabanka/ekstre-kurallari/$K1" "$T" '{"anahtarKelime":"shell petrol","yon":"Herhangi","islemTuru":"Gider","kategoriId":"'"$YAKIT"'"}'
kontrol 204 "Kural guncellendi"
istek GET /api/kasabanka/ekstre-kurallari "$T"
kontrol 200 "Guncel kural listede" '"anahtarKelime":"SHELL PETROL","yon":"Herhangi"'
istek GET /api/kasabanka/ekstre-kurallari "$T2"
kontrol 200 "Baska firma kurallari gormez" '^\[\]$'
istek DELETE "/api/kasabanka/ekstre-kurallari/$K1" "$T2"
kontrol 404 "Baska firma kurali silemez"
istek DELETE "/api/kasabanka/ekstre-kurallari/$K1" "$T"
kontrol 204 "Kural silindi"
istek DELETE "/api/kasabanka/ekstre-kurallari/$K1" "$T"
kontrol 404 "Silinen kural bulunamaz"

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
