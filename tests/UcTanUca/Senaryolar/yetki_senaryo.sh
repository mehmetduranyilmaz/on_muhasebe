#!/usr/bin/env bash
# Rol ve yetki uçtan uca senaryosu. Kullanım: yetki_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_yetki"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
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

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Yetki Test AS","ad":"Yasin","soyad":"Yonetici","eposta":"yetki'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)

echo "--- Hazir roller ve yetki tanimlari"
istek GET $Y/roller "$T"
kontrol 200 "Yeni firmaya hazir roller acildi, Yonetici tam yetkili ve basta" '^\[\{"id":"[^"]*","ad":"Y[^"]*","aciklama":"[^"]*","tamYetkiliMi":true'
for onek in Kullan Muhasebe Sat Depo Kasa "Salt Okur"; do
  echo "$GOVDE" | grep -q "\"ad\":\"$onek" || { KOD=999; break; }
done
kontrol 200 "Kullanici, Muhasebe, Satis, Satin Alma, Depo, Kasa, Salt Okur rolleri var"
YONETICI_ROL=$(rol_id "$T" Yonetici)
istek GET $Y/yetki-tanimlari "$T"
kontrol 200 "Yetki tanimlari lisansli modullerden geliyor" '"kod":"cari.hesap".*"kod":"stok.maliyet".*"kod":"fatura.belge".*"islemler":\["goruntule","ekle","duzenle","sil","onayla","iptal"\]'

echo "--- Ozel rol"
istek POST $Y/roller "$T" '{"ad":"Cari Bakici","aciklama":"Yalniz cari gorur","yetkiler":["cari.hesap.goruntule"]}'
kontrol 201 "Rol eklendi" '"ad":"Cari Bakici"'; ROL=$(alan id)
istek POST $Y/roller "$T" '{"ad":"Cari Bakici","yetkiler":[]}'
kontrol 409 "Ayni adla ikinci rol acilmaz" rol_adi_kayitli
istek POST $Y/roller "$T" '{"ad":"Uydurma","yetkiler":["uydurma.yetki.goruntule"]}'
kontrol 400 "Katalogda olmayan yetki verilemez" yetki_gecersiz
istek PUT "$Y/roller/$YONETICI_ROL" "$T" '{"ad":"Patron","yetkiler":[]}'
kontrol 422 "Yonetici rolu degistirilemez" yonetici_rolu_degistirilemez
istek DELETE "$Y/roller/$YONETICI_ROL" "$T"
kontrol 422 "Yonetici rolu silinemez" yonetici_rolu_degistirilemez

istek POST /api/stok/kartlar "$T" '{"ad":"Maliyetli Urun","tur":"Urun","temelBirim":"ADET","kdvOrani":20,"alisFiyati":40,"satisFiyati":55,"paraBirimi":"TRY"}'
KART=$(alan id)
istek POST "/api/stok/kartlar/$KART/hareketler" "$T" '{"tur":"Acilis","tarih":"2026-01-01","birim":"ADET","miktar":10,"birimFiyat":40}'

KEMAL="yetkikemal$U@ornek.com"
davet_kabul "$T" '{"ad":"Kemal","soyad":"Kisitli","eposta":"'"$KEMAL"'","sifre":"Sifre1234","rol":"Cari Bakici"}'
kontrol 200 "Kemal ozel rolle firmaya katildi" accessToken; K=$(alan accessToken)
istek GET $Y/kullanicilar "$T"
KEMAL_ID=$(echo "$GOVDE" | tr -d '\n' | sed 's/},{/}\n{/g' | grep "$KEMAL" | grep -o '"kullaniciId":"[^"]*"' | cut -d'"' -f4)
kontrol 200 "Kullanici listesinde rol adi ve ozel yetkiler" '"rolAdi":"Cari Bakici","tamYetkiliMi":false,"ekYetkiler":\[\],"yasakYetkiler":\[\]'

echo "--- Kisitli kullanici"
istek GET $Y/ben "$K"
kontrol 200 "Ben: tam yetkili degil, cari gorur ve bagimli grup yetkisi eklendi" '"yetki":\{"tamYetkiliMi":false,"yetkiler":\["cari.grup.goruntule","cari.hesap.goruntule"\]\}'
istek GET /api/cari/hesaplar "$K"
kontrol 200 "Cari listesini gorur"
istek POST /api/cari/hesaplar "$K" '{"unvan":"Yetkisiz","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'
kontrol 403 "Cari ekleyemez" 'Cari hesaplar - Ekle.*yetki_yok'
istek GET /api/fatura/faturalar "$K"
kontrol 403 "Faturalari goremez" yetki_yok
istek GET /api/cari/degisiklikler?kayitId=$KART "$K"
kontrol 403 "Degisiklik gecmisini goremez" yetki_yok
istek GET $Y/kullanicilar "$K"
kontrol 403 "Kullanici yonetimine giremez" yetki_yok
istek GET $Y/roller "$K"
kontrol 403 "Rolleri goremez" yetki_yok
istek GET /api/fatura/ayarlar "$K"
kontrol 200 "Modul ayarlarini okuyabilir (formlar icin)"
istek PUT /api/fatura/ayarlar "$K" '{}'
kontrol 403 "Modul ayarini degistiremez" yetki_yok

echo "--- Yetki degisikligi aninda gecerli"
istek PUT "$Y/kullanicilar/$KEMAL_ID/yetkiler" "$T" '{"rolId":"'"$ROL"'","ekYetkiler":["cari.hesap.ekle","stok.kart.goruntule"],"yasakYetkiler":[]}'
kontrol 204 "Kemal'e ozel ek yetki verildi"
istek POST /api/cari/hesaplar "$K" '{"unvan":"Yetkili Cari","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}'
kontrol 201 "Ayni token'la cari ekleyebildi (onbellek temizlendi)"
istek GET "/api/stok/kartlar/$KART" "$K"
kontrol 200 "Stok kartini gorur ama maliyet gizli" '"alisFiyati":null.*"birimMaliyet":0,"stokDegeri":0'
istek GET "/api/stok/kartlar/$KART" "$T"
kontrol 200 "Yonetici maliyeti gorur" '"alisFiyati":40.*"birimMaliyet":40'
istek GET /api/fatura/raporlar/karlilik "$K"
kontrol 403 "Karlilik raporu maliyet yetkisi ister" yetki_yok

istek PUT "$Y/kullanicilar/$KEMAL_ID/yetkiler" "$T" '{"rolId":"'"$ROL"'","ekYetkiler":["stok.kart.goruntule","stok.kart.duzenle","stok.rapor.goruntule"],"yasakYetkiler":["cari.hesap.goruntule"]}'
istek GET "/api/stok/kartlar/$KART" "$K"; KART_SURUM=$(alan surum)
istek PUT "/api/stok/kartlar/$KART" "$K" '{"ad":"Maliyetli Urun 2","tur":"Urun","temelBirim":"ADET","kdvOrani":20,"satisFiyati":60,"paraBirimi":"TRY","surum":'"$KART_SURUM"'}'
kontrol 200 "Maliyet gormeyen kullanici karti guncelledi"
istek GET "/api/stok/kartlar/$KART" "$T"
kontrol 200 "Gormedigi alis fiyati korundu" '"ad":"Maliyetli Urun 2".*"alisFiyati":40'
istek GET /api/stok/raporlar/durum "$K"
kontrol 200 "Stok durumunda deger sifir" '"birimMaliyet":0[.0]*,"deger":0[.0]*,.*"toplamDeger":0[.0]*,'
istek GET /api/cari/hesaplar "$K"
kontrol 403 "Yasaklanan yetki kalkti" yetki_yok

echo "--- Rol duzenleme ve silme"
istek PUT "$Y/roller/$ROL" "$T" '{"ad":"Cari Bakici","yetkiler":["cari.hesap.goruntule","cari.rapor.goruntule"]}'
kontrol 200 "Rol guncellendi, kullanici sayisi 1" '"kullaniciSayisi":1'
istek DELETE "$Y/roller/$ROL" "$T"
kontrol 422 "Kullanilan rol silinemez" rol_kullaniliyor
istek POST $Y/roller "$T" '{"ad":"Gecici","yetkiler":[]}'; GECICI_ROL=$(alan id)
istek DELETE "$Y/roller/$GECICI_ROL" "$T"
kontrol 204 "Kullanilmayan rol silindi"

echo "--- Yonetici korumalari"
istek GET $Y/ben "$T"; YASIN_ID=$(alan id)
istek PUT "$Y/kullanicilar/$YASIN_ID/yetkiler" "$T" '{"rolId":"'"$ROL"'"}'
kontrol 422 "Kisi kendi rolunu degistiremez" kendi_yetkisini_degistiremez
istek PUT "$Y/kullanicilar/$KEMAL_ID/yetkiler" "$T" '{"rolId":"'"$ROL"'","ekYetkiler":["yonetim.kullanici.goruntule","yonetim.kullanici.duzenle","yonetim.kullanici.ekle"]}'
kontrol 204 "Kemal'e kullanici yonetimi verildi"
istek PUT "$Y/kullanicilar/$YASIN_ID/yetkiler" "$K" '{"rolId":"'"$ROL"'"}'
kontrol 403 "Yonetici olmayan, yoneticinin rolunu alamaz" yonetici_atama_yetkisi_yok
istek POST $Y/kullanicilar "$K" '{"eposta":"yetkiyeni'"$U"'@ornek.com","rolId":"'"$YONETICI_ROL"'"}'
kontrol 403 "Yonetici olmayan, yonetici davet edemez" yonetici_atama_yetkisi_yok
istek PUT "$Y/roller/$ROL" "$K" '{"ad":"Cari Bakici","yetkiler":["fatura.belge.onayla"]}'
kontrol 422 "Yonetici olmayan kendi rolune yetki ekleyemez" kendi_yetkisini_degistiremez

echo "--- Pasiflestirilen kullanici"
istek POST "$Y/kullanicilar/$KEMAL_ID/pasiflestir" "$T"
kontrol 204 "Kemal pasiflestirildi"
istek GET /api/stok/kartlar "$K"
kontrol 403 "Suresi dolmamis token'la da artik islem yapamaz" yetki_yok

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
