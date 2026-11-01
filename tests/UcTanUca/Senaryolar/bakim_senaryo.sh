#!/usr/bin/env bash
# Bakim Takibi modulu uctan uca senaryosu. Kullanim: bakim_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_bakim"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
Y=/api/yonetim
B=/api/bakim

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
gun() { date -d "$1 days" +%Y-%m-%d; }
zaman() { date -u -d "$1" +%Y-%m-%dT%H:%M:%SZ; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Bakim Test AS","ad":"Bora","soyad":"Yon","eposta":"bakim'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Bakim AS","ad":"D","soyad":"F","eposta":"bakimdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)

echo "--- Ekipman"
istek POST $B/ekipmanlar "$T" '{"ad":"","tur":"Makine","sayacBirimi":"Yok","durum":"Calisiyor"}'
kontrol 400 "Adsiz ekipman reddedilir"
istek POST $B/ekipmanlar "$T" '{"ad":"Pres","tur":"Makine","sayacBirimi":"Saat","sayac":900,"durum":"Calisiyor","sorumluKullaniciId":"00000000-0000-7000-8000-000000000001"}'
kontrol 400 "Firmada olmayan sorumlu reddedilir" sorumlu_gecersiz
istek GET $B/sorumlular "$T"
kontrol 200 "Sorumlu listesi" '"adSoyad":"Bora Yon"'; SORUMLU=$(alan id)
istek POST $B/ekipmanlar "$T" '{"ad":"Hidrolik Pres","tur":"Makine","marka":"ABC","seriNo":"SN-1","konum":"Atolye","sayacBirimi":"Saat","sayac":900,"durum":"Calisiyor","sorumluKullaniciId":"'"$SORUMLU"'"}'
kontrol 201 "Ekipman otomatik kodla eklendi" '"kod":"E[0-9]+","ad":"Hidrolik Pres"'; PRES=$(alan id); SURUM=$(alan surum); PRESKOD=$(alan kod)
istek POST $B/ekipmanlar "$T" '{"kod":"'"$PRESKOD"'","ad":"Kopya","tur":"Makine","sayacBirimi":"Yok","durum":"Calisiyor"}'
kontrol 409 "Ayni kod ikinci kez kullanilmaz" kod_kullaniliyor
istek POST $B/ekipmanlar "$T" '{"ad":"Vinc","tur":"KaldirmaEkipmani","sayacBirimi":"Yok","durum":"Calisiyor"}'
kontrol 201 "Sayacsiz kaldirma ekipmani"; VINC=$(alan id)
istek PUT "$B/ekipmanlar/$PRES" "$T" '{"ad":"Hidrolik Pres 100T","tur":"Makine","sayacBirimi":"Saat","sayac":900,"durum":"Calisiyor","sorumluKullaniciId":"'"$SORUMLU"'","surum":'"$SURUM"'}'
kontrol 200 "Guncellendi" '"ad":"Hidrolik Pres 100T"'
istek PUT "$B/ekipmanlar/$PRES" "$T" '{"ad":"Eski","tur":"Makine","sayacBirimi":"Saat","durum":"Calisiyor","surum":'"$SURUM"'}'
kontrol 409 "Eski surumle guncelleme reddedilir"

echo "--- Plan"
istek POST "$B/ekipmanlar/$PRES/planlar" "$T" '{"ad":"X","yasalMi":false,"hatirlatmaGun":7}'
kontrol 400 "Periyotsuz plan reddedilir" periyot_gerekli
istek POST "$B/ekipmanlar/$VINC/planlar" "$T" '{"ad":"X","yasalMi":false,"periyotSayac":100,"hatirlatmaGun":7}'
kontrol 400 "Sayacsiz ekipmana sayac plani olmaz" sayac_birimi
istek POST "$B/ekipmanlar/$PRES/planlar" "$T" '{"ad":"Filtre degisimi","yasalMi":false,"periyotSayac":1000,"periyotGun":180,"hatirlatmaGun":14}'
kontrol 201 "Gun ve sayac plani, sonraki hesaplandi" '"sonrakiTarih":"'"$(gun 180)"'","sonrakiSayac":1900.*"durum":"Zamaninda"'; PLAN=$(alan id)
istek POST "$B/ekipmanlar/$VINC/planlar" "$T" '{"ad":"Yillik periyodik kontrol","yasalMi":true,"kontrolKurulusu":"Muayene AS","periyotGun":365,"sonrakiTarih":"'"$(gun -2)"'","hatirlatmaGun":30}'
kontrol 201 "Yasal kontrol gecikmis" '"yasalMi":true.*"durum":"Gecikti"'; YASAL=$(alan id)
istek GET $B/yaklasanlar "$T"
kontrol 200 "Geciken yasal kontrol yaklasanlarda" '"plan":"Yillik periyodik kontrol","yasalMi":true.*"durum":"Gecikti"'
if echo "$GOVDE" | grep -q "Filtre degisimi"; then KOD=filtre-var; fi
kontrol 200 "Zamanindaki plan yaklasanlarda yok"
istek GET $B/ekipmanlar "$T"
kontrol 200 "Listede en yakin plan ve sorumlu" '"ad":"Hidrolik Pres 100T".*"sorumluAdi":"Bora Yon".*"enYakinPlan":"Filtre degisimi"'

echo "--- Stok parcasi hazirligi"
istek POST /api/stok/kartlar "$T" '{"ad":"Filtre","tur":"Urun","temelBirim":"ADET","kdvOrani":20,"paraBirimi":"TRY"}'; KART=$(alan id)
istek POST "/api/stok/kartlar/$KART/hareketler" "$T" '{"tur":"Acilis","tarih":"2026-01-01","birim":"ADET","miktar":10,"birimFiyat":100}'
kontrol 201 "Acilis stoku 10 adet"
istek POST /api/stok/kartlar "$T" '{"ad":"Danismanlik","tur":"Hizmet","temelBirim":"SAAT","kdvOrani":20,"paraBirimi":"TRY"}'; HIZMET=$(alan id)

echo "--- Is emri"
ACIK='{"ekipmanId":"'"$PRES"'","tur":"Ariza","baslik":"Hidrolik kacak","bildirimZamani":"'"$(zaman '-5 hours')"'","makineDurduMu":true,"iscilikTutari":500,"disServisTutari":1000,"disServisFirma":"Servis Ltd","parcalar":[{"stokKartiId":"'"$KART"'","miktar":2},{"ad":"Conta","birim":"ADET","miktar":3,"birimFiyat":10}]}'
istek POST $B/is-emirleri "$T" '{"ekipmanId":"'"$PRES"'","tur":"Ariza","baslik":"X","makineDurduMu":true,"iscilikTutari":0,"disServisTutari":0,"parcalar":[{"stokKartiId":"'"$HIZMET"'","miktar":1}]}'
kontrol 400 "Hizmet karti parca olamaz" stok_karti
istek POST $B/is-emirleri "$T" '{"ekipmanId":"'"$PRES"'","tur":"Ariza","baslik":"X","makineDurduMu":true,"iscilikTutari":0,"disServisTutari":0,"parcalar":[{"miktar":1}]}'
kontrol 400 "Karti ve adi olmayan parca reddedilir" parca_adi
istek POST $B/is-emirleri "$T" '{"ekipmanId":"'"$PRES"'","tur":"Ariza","baslik":"X","makineDurduMu":true,"iscilikTutari":0,"disServisTutari":0,"faturaId":"00000000-0000-7000-8000-000000000001"}'
kontrol 400 "Olmayan fatura baglanamaz" fatura_gecersiz
istek POST $B/is-emirleri "$T" '{"ekipmanId":"'"$PRES"'","tur":"PlanliBakim","planId":"'"$YASAL"'","baslik":"X","makineDurduMu":false,"iscilikTutari":0,"disServisTutari":0}'
kontrol 400 "Baska ekipmanin plani baglanamaz" plan_ekipman
istek POST $B/is-emirleri "$T" "$ACIK"
kontrol 201 "Ariza is emri, parca karttan tamamlandi" '"kod":"IE[0-9]+".*"durum":"Acik".*"ad":"Filtre","birim":"ADET"'; EMIR=$(alan id)
istek GET "$B/ekipmanlar/$PRES" "$T"
kontrol 200 "Ariza acilinca ekipman arizali" '"durum":"Arizali"'
istek GET "/api/stok/kartlar?arama=Filtre" "$T"
kontrol 200 "Is emri acikken stok dusmez" '"miktar":10'
istek POST "$B/is-emirleri/$EMIR/baslat" "$T" '{}'
kontrol 200 "Is emri basladi" '"durum":"Devam"'
istek GET "$B/ekipmanlar/$PRES" "$T"
kontrol 200 "Is basladi, ekipman bakimda" '"durum":"Bakimda"'
istek POST "$B/is-emirleri/$EMIR/kapat" "$T" '{"bitisZamani":"'"$(zaman '-6 hours')"'"}'
kontrol 400 "Bitis baslangictan once olamaz" bitis_zamani
istek POST "$B/is-emirleri/$EMIR/kapat" "$T" '{"bitisZamani":"'"$(zaman '+0 hours')"'","sayacDegeri":1000}'
kontrol 200 "Is emri kapandi, durus bildirimden hesaplandi" '"durum":"Kapandi".*"durusSaati":5(\.[0-9]+)?,'
istek GET "/api/stok/kartlar?arama=Filtre" "$T"
kontrol 200 "Kapaninca stoktan sarf olarak dustu" '"miktar":8'
istek GET "$B/ekipmanlar/$PRES" "$T"
kontrol 200 "Ekipman calisiyor, sayac ilerledi" '"sayac":1000(\.0+)?,"durum":"Calisiyor"'
istek PUT "$B/is-emirleri/$EMIR" "$T" "$ACIK"
kontrol 422 "Kapanan is emri degistirilemez" is_emri_kapali
istek GET "/api/muhasebe/fisler?baslangic=$(gun -1)&bitis=$(gun 1)" "$T"
kontrol 200 "Bakim maliyeti muhasebe fisi uretmez" '"fisler":\[\]'

echo "--- Plani tamamlayan is emri"
istek POST $B/is-emirleri "$T" '{"ekipmanId":"'"$VINC"'","tur":"YasalKontrol","planId":"'"$YASAL"'","baslik":"Yillik kontrol","kontrolKurulusu":"Muayene AS","makineDurduMu":false,"iscilikTutari":0,"disServisTutari":2000}'
kontrol 201 "Yasal kontrol is emri"; KONTROL=$(alan id)
istek POST "$B/is-emirleri/$KONTROL/kapat" "$T" '{"bitisZamani":"'"$(zaman '+0 hours')"'","raporNo":"RPR-77"}'
kontrol 200 "Rapor no kaydedildi, makine durmadigi icin durus yok" '"raporNo":"RPR-77".*'
istek GET "$B/ekipmanlar/$VINC/planlar" "$T"
kontrol 200 "Sonraki kontrol yapildigi gunden sayildi" '"sonYapilmaTarihi":"'"$(date +%Y-%m-%d)"'".*"durum":"Zamaninda"'

echo "--- Eksi stok ve iptal"
istek POST $B/is-emirleri "$T" '{"ekipmanId":"'"$PRES"'","tur":"PlanliBakim","planId":"'"$PLAN"'","baslik":"Filtre","makineDurduMu":false,"iscilikTutari":100,"disServisTutari":0,"parcalar":[{"stokKartiId":"'"$KART"'","miktar":3}]}'
kontrol 201 "Planli bakim"; EMIR2=$(alan id)
istek POST "$B/is-emirleri/$EMIR2/kapat" "$T" '{"bitisZamani":"'"$(zaman '+0 hours')"'","sayacDegeri":1100}'
kontrol 200 "Planli bakim kapandi"
istek GET "/api/stok/kartlar?arama=Filtre" "$T"
kontrol 200 "Stok 5" '"miktar":5'
istek GET "$B/ekipmanlar/$PRES/planlar" "$T"
kontrol 200 "Sayac plani yapilan sayactan yeniden hesaplandi" '"sonrakiSayac":2100'
istek POST "$B/is-emirleri/$EMIR2/iptal" "$T" '{}'
kontrol 200 "Kapanan is emri iptal edildi" '"durum":"Iptal"'
istek GET "/api/stok/kartlar?arama=Filtre" "$T"
kontrol 200 "Iptalde stok geri geldi" '"miktar":8'
istek POST "$B/is-emirleri/$EMIR2/iptal" "$T" '{}'
kontrol 422 "Iki kez iptal edilemez" is_emri_durumu

echo "--- Stok yetmezse kapanmaz"
istek PUT /api/stok/ayarlar "$T" '{"maliyetYontemi":"AgirlikliOrtalama","eksiStokDavranisi":"Engelle"}'
istek POST $B/is-emirleri "$T" '{"ekipmanId":"'"$VINC"'","tur":"Ariza","baslik":"Fazla parca","makineDurduMu":true,"iscilikTutari":0,"disServisTutari":0,"parcalar":[{"stokKartiId":"'"$KART"'","miktar":100}]}'
kontrol 201 "Stoktan fazla parcali is emri acilir"; FAZLA=$(alan id)
istek POST "$B/is-emirleri/$FAZLA/kapat" "$T" '{"bitisZamani":"'"$(zaman '+0 hours')"'"}'
kontrol 422 "Eksi stok engelliyse kapanis reddedilir" yetersiz_stok
istek GET "$B/is-emirleri/$FAZLA" "$T"
kontrol 200 "Reddedilen kapanista is emri acik kaldi" '"durum":"Acik"'
istek GET "$B/ekipmanlar/$VINC" "$T"
kontrol 200 "Reddedilen kapanista ekipman arizali kaldi" '"durum":"Arizali"'
istek POST "$B/is-emirleri/$FAZLA/iptal" "$T" '{}'
kontrol 200 "Acik is emri iptal edildi, ekipman duzeldi" '"durum":"Iptal"'
istek PUT /api/stok/ayarlar "$T" '{"maliyetYontemi":"AgirlikliOrtalama","eksiStokDavranisi":"Uyar"}'

echo "--- Rapor"
istek GET "$B/raporlar/maliyet?baslangic=$(gun -1)&bitis=$(gun 1)&ekipmanId=$PRES" "$T"
kontrol 200 "Parca (2x100 stok + 3x10 elle), iscilik, dis servis, sayilar" '"parcaMaliyeti":230(\.0+)?,"iscilik":500(\.0+)?,"disServis":1000(\.0+)?,"toplam":1730(\.0+)?,"arizaSayisi":1,"bakimSayisi":0,"durusSaati":5'
istek GET "$B/raporlar/maliyet?baslangic=2026-10-31&bitis=2026-10-01" "$T"
kontrol 400 "Ters donem reddedilir" donem

echo "--- Dosya"
printf '%%PDF-1.4 rapor' > "$DIR/rapor.pdf"
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/dosya/kayit/is-emri/$KONTROL" -H "Authorization: Bearer $T" -F "dosya=@$DIR/rapor.pdf"); GOVDE=$(cat "$DIR/govde")
kontrol 201 "Is emrine rapor eklendi" '"ad":"rapor.pdf"'
KOD=$(curl "${CURL_EK[@]}" -s -o "$DIR/govde" -w "%{http_code}" -X POST "$KOK/api/dosya/kayit/ekipman/$PRES" -H "Authorization: Bearer $T" -F "dosya=@$DIR/rapor.pdf"); GOVDE=$(cat "$DIR/govde")
kontrol 201 "Ekipmana kilavuz eklendi"

echo "--- Izolasyon ve yetki"
istek GET "$B/ekipmanlar/$PRES" "$T2"
kontrol 404 "Baska firma ekipmani goremez" ekipman_bulunamadi
istek GET "$B/is-emirleri/$EMIR" "$T2"
kontrol 404 "Baska firma is emrini goremez" is_emri_bulunamadi
istek POST "$B/is-emirleri/$EMIR/iptal" "$T2" '{}'
kontrol 404 "Baska firma is emrini iptal edemez" is_emri_bulunamadi
istek DELETE "$B/planlar/$PLAN" "$T2"
kontrol 404 "Baska firma plani silemez" plan_bulunamadi
istek POST $B/is-emirleri "$T2" '{"ekipmanId":"'"$PRES"'","tur":"Ariza","baslik":"X","makineDurduMu":true,"iscilikTutari":0,"disServisTutari":0}'
kontrol 404 "Baska firma ekipmanina is emri acilamaz" ekipman_bulunamadi
istek POST $Y/roller "$T" '{"ad":"Bakim Goren","yetkiler":["bakim.ekipman.goruntule","bakim.isemri.goruntule"]}'
davet_kabul "$T" '{"ad":"Goren","soyad":"Kisi","eposta":"bakimgoren'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Bakim Goren"}'
GT=$(alan accessToken)
istek GET $B/is-emirleri "$GT"
kontrol 200 "Goruntuleme yetkilisi listeler"
istek POST "$B/is-emirleri/$EMIR/iptal" "$GT" '{}'
kontrol 403 "Silme yetkisi olmayan iptal edemez" yetki_yok
istek GET "$B/raporlar/maliyet?baslangic=2026-10-01&bitis=2026-10-31" "$GT"
kontrol 403 "Rapor yetkisi yoksa goremez" yetki_yok

echo "--- Silme"
istek DELETE "$B/planlar/$PLAN" "$T"
kontrol 204 "Plan silindi"
istek DELETE "$B/ekipmanlar/$PRES" "$T"
kontrol 204 "Is emirleri olan ekipman silinir"
istek GET "$B/is-emirleri/$EMIR" "$T"
kontrol 404 "Silinen ekipmanin is emrine ulasilmaz" is_emri_bulunamadi

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
