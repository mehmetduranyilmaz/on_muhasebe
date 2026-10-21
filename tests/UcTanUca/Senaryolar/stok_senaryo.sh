#!/usr/bin/env bash
# Stok modülü uçtan uca senaryosu. Kullanım: stok_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_stok"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

istek() { # METOT YOL [TOKEN] [GOVDE]
  local args=(-s -o "$DIR/govde" -w "%{http_code}" -X "$1" "$KOK$2" -H "Content-Type: application/json")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(-d "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$DIR/govde")
}
alan() { echo "$GOVDE" | grep -o "\"$1\":\"\?[^,\"}]*" | head -1 | sed -E "s/\"$1\":\"?//"; }
kontrol() {
  if [ "$KOD" = "$1" ] && { [ -z "$3" ] || echo "$GOVDE" | grep -q -- "$3"; }; then
    echo "GECTI  [$KOD] $2"; GECEN=$((GECEN+1))
  else
    echo "KALDI  [$KOD] $2 (beklenen $1 ${3:+ve '$3'}) -> ${GOVDE:0:400}"; KALAN=$((KALAN+1))
  fi
}

U=$(date +%s)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Stok Test AS","ad":"Seda","soyad":"Can","eposta":"stok'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
istek GET /api/yonetim/ben "$T"; FIRMA=$(echo "$GOVDE" | grep -o '"aktifFirma":{"id":"[^"]*"' | cut -d'"' -f6)
istek POST /api/yonetim/kayit "" '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"stokdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
istek POST /api/yonetim/giris "" "$SISTEM_GIRIS"
SYS=$(alan accessToken)

echo "--- Depo ve grup"
istek GET /api/stok/depolar "$T"
kontrol 200 "Ilk erisimde Merkez Depo otomatik acildi" '"ad":"Merkez Depo","varsayilanMi":true'
MERKEZ=$(alan id)
istek POST /api/stok/depolar "$T" '{"ad":"Sube Depo"}'
kontrol 201 "Ikinci depo eklendi"; SUBE=$(alan id)
istek POST /api/stok/depolar "$T" '{"ad":"Sube Depo"}'
kontrol 409 "Ayni depo adi reddedildi"
istek POST /api/stok/gruplar "$T" '{"ad":"Icecekler"}'
kontrol 201 "Grup eklendi"; GRUP=$(alan id)

echo "--- Stok kartlari"
istek POST /api/stok/kartlar "$T" '{"ad":"Su 0.5 L","tur":"Urun","temelBirim":"ADET","barkod":"8690000000001","kdvOrani":10,"alisFiyati":10,"satisFiyati":15,"paraBirimi":"TRY","grupId":"'"$GRUP"'","kritikSeviye":50,"ekBirimler":[{"birimKodu":"KOLI","katsayi":12,"barkod":"8690000000012"}]}'
kontrol 201 "Urun karti: kod S00001, KOLI=12 ADET" '"kod":"S00001"'
SU=$(alan id)
istek POST /api/stok/kartlar "$T" '{"ad":"Nakliye","tur":"Hizmet","temelBirim":"SAAT","kdvOrani":20,"paraBirimi":"TRY"}'
kontrol 201 "Hizmet karti S00002" '"kod":"S00002"'; HIZMET=$(alan id)
istek POST /api/stok/kartlar "$T" '{"ad":"Kopya","tur":"Urun","temelBirim":"ADET","barkod":"8690000000012","kdvOrani":20,"paraBirimi":"TRY"}'
kontrol 409 "Baska kartin birim barkodu reddedildi" barkod_kullaniliyor
istek POST /api/stok/kartlar "$T" '{"ad":"X","tur":"Urun","temelBirim":"ADET","kdvOrani":18,"paraBirimi":"TRY"}'
kontrol 400 "Gecersiz KDV orani (%18) reddedildi"
istek POST /api/stok/kartlar "$T" '{"ad":"X","tur":"Urun","temelBirim":"ADET","kdvOrani":20,"paraBirimi":"TRY","ekBirimler":[{"birimKodu":"ADET","katsayi":2}]}'
kontrol 400 "Temel birimle ayni ek birim reddedildi" ek_birim_gecersiz
istek GET /api/stok/kartlar/barkod/8690000000012 "$T"
kontrol 200 "Koli barkodu karti ve birimi buldu" '"birim":"KOLI","katsayi":12'

echo "--- Hareketler"
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"Acilis","tarih":"2026-01-01","birim":"KOLI","miktar":2,"birimFiyat":120}'
kontrol 201 "Acilis 2 koli @120 -> 24 adet, adet maliyeti 10" '"giris":24.*"birimFiyat":10'
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"SayimFazlasi","tarih":"2026-02-01","birim":"ADET","miktar":10}'
kontrol 400 "Maliyetsiz giris reddedildi" maliyet_gerekli
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"SayimFazlasi","tarih":"2026-02-01","birim":"ADET","miktar":10,"birimFiyat":20}'
kontrol 201 "Sayim fazlasi 10 adet @20"
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"Fire","tarih":"2026-03-01","birim":"ADET","miktar":2.5}'
kontrol 400 "Adette kesirli miktar reddedildi" kesirli_miktar
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"Fire","tarih":"2026-03-01","birim":"ADET","miktar":4}'
kontrol 201 "Fire 4 adet (yeterli stok, uyari yok)" '"uyari":null'
istek POST "/api/stok/kartlar/$HIZMET/hareketler" "$T" '{"tur":"Acilis","tarih":"2026-01-01","birim":"SAAT","miktar":1,"birimFiyat":1}'
kontrol 422 "Hizmet kartina hareket reddedildi" hizmet_karti
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"Sarf","tarih":"2026-03-05","birim":"ADET","miktar":40}'
kontrol 201 "Varsayilan ayar (Uyar): eksiye dusen sarf kaydedildi, uyari dondu" '"uyari":"Dikkat'
SARF=$(echo "$GOVDE" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

echo "--- Maliyet yontemleri (ayni hareketler, farkli deger)"
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Agirlikli ortalama: miktar -10, birim maliyet 12,9412" '"miktar":-10.*"birimMaliyet":12.9412.*"maliyetYontemi":"AgirlikliOrtalama"'
istek PUT /api/stok/ayarlar "$T" '{"maliyetYontemi":"Fifo","eksiStokDavranisi":"Engelle"}'
kontrol 200 "Ayar: FIFO + eksi stok engelle" '"maliyetYontemi":"Fifo"'
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "FIFO: birim maliyet 20 (eksi miktar son fiyattan)" '"birimMaliyet":20.*"maliyetYontemi":"Fifo"'
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"Fire","tarih":"2026-03-06","birim":"ADET","miktar":1}'
kontrol 422 "Engelle ayarinda eksi stoka cikis reddedildi" yetersiz_stok
istek POST "/api/stok/hareketler/$SARF/iptal" "$T"
kontrol 200 "Sarf iptal edildi (ters kayit)" '"iptalKaydiMi":true'
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Iptal sonrasi miktar 30; FIFO kalan 20@10 + 10@20 = 400 TL" '"miktar":30.*"stokDegeri":400'
istek PUT /api/stok/ayarlar "$T" '{"maliyetYontemi":"SonAlisFiyati","eksiStokDavranisi":"IzinVer"}'
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Son alis fiyati: 30 x 20 = 600 TL" '"stokDegeri":600'
istek PUT /api/stok/ayarlar "$T" '{"maliyetYontemi":"AgirlikliOrtalama","eksiStokDavranisi":"Uyar"}'

echo "--- Transfer"
istek POST "/api/stok/kartlar/$SU/transferler" "$T" '{"kaynakDepoId":"'"$MERKEZ"'","hedefDepoId":"'"$SUBE"'","tarih":"2026-04-01","birim":"KOLI","miktar":1}'
kontrol 201 "1 koli (12 adet) Merkez -> Sube" '"tur":"TransferCikis"'
TRANSFER=$(echo "$GOVDE" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Depo miktarlari: Merkez 18, Sube 12, toplam 30" '"depoAdi":"Merkez Depo","miktar":18.*"depoAdi":"Sube Depo","miktar":12'
istek POST "/api/stok/kartlar/$SU/transferler" "$T" '{"kaynakDepoId":"'"$MERKEZ"'","hedefDepoId":"'"$MERKEZ"'","tarih":"2026-04-01","birim":"ADET","miktar":1}'
kontrol 400 "Ayni depoya transfer reddedildi"
istek POST "/api/stok/hareketler/$TRANSFER/iptal" "$T"
kontrol 200 "Transfer iptali iki bacagi birden ters kaydetti" 'TransferCikis.*TransferGiris'
istek GET "/api/stok/kartlar/$SU" "$T"
kontrol 200 "Transfer iptalinden sonra Sube 0" '"depoAdi":"Sube Depo","miktar":0'

echo "--- Raporlar"
istek GET "/api/stok/kartlar/$SU/ekstre?baslangic=2026-02-01&bitis=2026-12-31" "$T"
kontrol 200 "Ekstre: devir 24, son miktar 30" '"devirMiktar":24.*"sonMiktar":30'
istek GET "/api/stok/kartlar/$SU/ekstre?baslangic=2026-01-01&bitis=2026-12-31&depoId=$SUBE" "$T"
kontrol 200 "Depo filtreli ekstre: Sube son miktar 0" '"sonMiktar":0'
istek GET "/api/stok/raporlar/durum" "$T"
kontrol 200 "Stok durumu: Su kritik seviyede (30 <= 50)" '"kritikMi":true'
istek GET "/api/stok/raporlar/durum?depoId=$SUBE" "$T"
kontrol 200 "Depo filtreli durum: Sube degeri 0" '"toplamDeger":0'
istek GET "/api/stok/raporlar/ozet" "$T"
kontrol 200 "Ozet: 1 urun, 1 kritik" '"urunSayisi":1.*"kritikSayisi":1'
istek GET "/api/stok/kartlar?arama=8690000000012" "$T"
kontrol 200 "Listede koli barkoduyla arama" '"toplamKayit":1'

echo "--- Kart kurallari"
istek GET "/api/stok/kartlar/$SU" "$T"; SURUM=$(alan surum)
istek PUT "/api/stok/kartlar/$SU" "$T" '{"ad":"Su 0.5 L","tur":"Urun","temelBirim":"KG","kdvOrani":10,"paraBirimi":"TRY","surum":'"$SURUM"'}'
kontrol 422 "Hareketli kartin temel birimi degismez" temel_birim_degistirilemez
istek PUT "/api/stok/kartlar/$SU" "$T" '{"ad":"Su 0.5 Litre","tur":"Urun","temelBirim":"ADET","kdvOrani":10,"paraBirimi":"TRY","kritikSeviye":50,"ekBirimler":[{"birimKodu":"KOLI","katsayi":12},{"birimKodu":"PAKET","katsayi":6}],"surum":'"$SURUM"'}'
kontrol 200 "Guncelleme: PAKET=6 eklendi" '"birimKodu":"PAKET"'
istek PUT "/api/stok/kartlar/$SU" "$T" '{"ad":"Eski","tur":"Urun","temelBirim":"ADET","kdvOrani":10,"paraBirimi":"TRY","surum":'"$SURUM"'}'
kontrol 409 "Eski surumle guncelleme reddedildi"
istek DELETE "/api/stok/kartlar/$SU" "$T"
kontrol 422 "Hareketli kart silinemez" hareketli_kart_silinemez
istek DELETE "/api/stok/kartlar/$HIZMET" "$T"
kontrol 204 "Hareketsiz hizmet karti silindi"
istek DELETE "/api/stok/depolar/$MERKEZ" "$T"
kontrol 422 "Varsayilan depo silinemez" varsayilan_depo_silinemez
istek DELETE "/api/stok/depolar/$SUBE" "$T"
kontrol 422 "Hareketli depo silinemez" hareketli_depo_silinemez

echo "--- Yetki, izolasyon, lisans"
istek POST /api/yonetim/kullanicilar "$T" '{"ad":"Kullanici","soyad":"Bir","eposta":"stokkul'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Kullanici"}'
istek POST /api/yonetim/giris "" '{"eposta":"stokkul'"$U"'@ornek.com","sifre":"Sifre1234"}'
TK=$(alan accessToken)
istek PUT /api/stok/ayarlar "$TK" '{"maliyetYontemi":"Fifo","eksiStokDavranisi":"IzinVer"}'
kontrol 403 "Kullanici rolu stok ayarini degistiremez"
istek GET /api/stok/ayarlar "$TK"
kontrol 200 "Kullanici rolu ayari okuyabilir" '"maliyetYontemi":"AgirlikliOrtalama"'
istek GET "/api/stok/kartlar/$SU" "$T2"
kontrol 404 "Baska firma stok kartini goremiyor"
istek PUT "/api/yonetim/sistem/firmalar/$FIRMA/lisans" "$SYS" '{"paketKodu":"basic","bitisTarihi":"2020-01-01","ekModuller":[]}'
istek POST "/api/stok/kartlar/$SU/hareketler" "$T" '{"tur":"Fire","tarih":"2026-03-06","birim":"ADET","miktar":1}'
kontrol 403 "Suresi dolan lisansta hareket girilemez" lisans_suresi_doldu
istek GET "/api/stok/raporlar/durum" "$T"
kontrol 200 "Suresi dolan lisansta rapor okunur"

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
