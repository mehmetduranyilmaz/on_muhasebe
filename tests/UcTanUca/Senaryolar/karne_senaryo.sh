#!/usr/bin/env bash
# Çek karnesi ve senet defteri (verilen evrak yaprak takibi) uçtan uca senaryosu. Kullanım: karne_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_karne"; rm -rf "$DIR"; mkdir -p "$DIR"
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
CS=/api/kasabanka/cek-senet
cek() { # SERI(bos olabilir) TUTAR [KARNE]
  local s=""; [ -n "$1" ] && s='"seriNo":"'"$1"'",'
  local k=""; [ -n "$3" ] && k=',"karneId":"'"$3"'"'
  echo '{"tur":"Cek",'"$s"'"vadeTarihi":"2026-12-20","tutar":'"$2"',"kendiHesapId":"'"$BANKA"'"'"$k"'}'
}
senet() { # SERI TUTAR [KARNE]
  local s=""; [ -n "$1" ] && s='"seriNo":"'"$1"'",'
  local k=""; [ -n "$3" ] && k=',"karneId":"'"$3"'"'
  echo '{"tur":"Senet",'"$s"'"vadeTarihi":"2026-12-20","tutar":'"$2"',"paraBirimi":"TRY"'"$k"'}'
}
cikis() { istek POST "$CS/cikis" "$T" '{"cariHesapId":"'"$CARI"'","tarih":"2026-10-21","evraklar":['"$1"']}'; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Karne Test AS","ad":"K","soyad":"T","eposta":"karne'"$U"'@ornek.com","sifre":"Sifre1234"}'; T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger","ad":"D","soyad":"F","eposta":"karnediger'"$U"'@ornek.com","sifre":"Sifre1234"}'; T2=$(alan accessToken)
istek POST /api/cari/hesaplar "$T" '{"unvan":"Tedarikci AS","tur":"Tedarikci","kisiTipi":"Tuzel","paraBirimi":"TRY"}'; CARI=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Is Bankasi","tur":"Banka","paraBirimi":"TRY","hesapNo":"1234"}'; BANKA=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Akbank","tur":"Banka","paraBirimi":"TRY"}'; BANKA2=$(alan id)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Kasa","tur":"Kasa","paraBirimi":"TRY"}'; KASA=$(alan id)

echo "--- Karne tanimi"
istek POST $CS/karneler "$T" '{"tur":"Cek","kendiHesapId":"'"$BANKA"'","baslangicSeri":"ABC 0001098","yaprakSayisi":5,"tarih":"2026-10-01","aciklama":"Ekim karnesi"}'
kontrol 201 "5 yaprakli cek karnesi" '"tur":"Cek".*"hesapAdi":"Is Bankasi","baslangicSeri":"ABC0001098","bitisSeri":"ABC0001102","yaprakSayisi":5,"kullanilan":0,"iptalEdilen":0,"kalan":5,"siradakiSeri":"ABC0001098","azaldiMi":true'
K1=$(alan id)
istek POST $CS/karneler "$T" '{"tur":"Cek","kendiHesapId":"'"$BANKA"'","baslangicSeri":"ABC0001102","yaprakSayisi":10,"tarih":"2026-10-01"}'
kontrol 409 "Ayni hesapta cakisan aralik reddedildi" karne_cakisiyor
istek POST $CS/karneler "$T" '{"tur":"Cek","kendiHesapId":"'"$BANKA2"'","baslangicSeri":"ABC0001098","yaprakSayisi":10,"tarih":"2026-10-01"}'
kontrol 201 "Baska bankada ayni seri olabilir"; K2=$(alan id)
istek POST $CS/karneler "$T" '{"tur":"Cek","kendiHesapId":"'"$KASA"'","baslangicSeri":"1","yaprakSayisi":10,"tarih":"2026-10-01"}'
kontrol 400 "Cek karnesi kasaya baglanamaz" verilen_cek_hesabi
istek POST $CS/karneler "$T" '{"tur":"Cek","baslangicSeri":"ABC","yaprakSayisi":0,"tarih":"2026-10-01"}'
kontrol 400 "Hesapsiz, sayisiz, sifir yaprak" 'banka hesabı.*sayı ile bitmelidir.*YaprakSayisi'
istek POST $CS/karneler "$T" '{"tur":"Senet","baslangicSeri":"S-001","yaprakSayisi":50,"tarih":"2026-10-01","aciklama":"Senet defteri"}'
kontrol 201 "Senet defteri bankasiz" '"tur":"Senet","kendiHesapId":null,"hesapAdi":null,"baslangicSeri":"S-001","bitisSeri":"S-050"'
D1=$(alan id)

echo "--- Verirken yaprak"
cikis "$(cek "" 1000 "$K1")"
kontrol 201 "Seri bos + karne: siradaki yaprak (ABC0001098) kullanildi"
istek GET "$CS/karneler/$K1" "$T"
kontrol 200 "Yaprak kullanildi, cari ve tutarla" '"seriNo":"ABC0001098","durum":"Kullanildi","evrakId":"[^"]*","evrakKodu":"[^"]*","cariUnvan":"Tedarikci AS","tutar":1000'
istek POST "$CS/karneler/$K1/yaprak-iptal" "$T" '{"seriNo":"abc 0001099","neden":"Yirtildi"}'
kontrol 200 "Yirtik yaprak iptal edildi" '"seriNo":"ABC0001099","durum":"Iptal".*"iptalNedeni":"Yirtildi"'
kontrol 200 "Siradaki yaprak iptali atladi" '"siradakiSeri":"ABC0001100"'
istek POST "$CS/karneler/$K1/yaprak-iptal" "$T" '{"seriNo":"ABC0001098","neden":"x"}'
kontrol 409 "Kullanilan yaprak iptal edilemez" karne_yapragi_kullanildi
cikis "$(cek "ABC 0001099" 500 "$K1")"
kontrol 422 "Iptal yaprak kullanilamaz" karne_yapragi_iptal
cikis "$(cek "ABC0001098" 500)"
kontrol 409 "Karnesiz girilse de kullanilan yaprak taninir" 'karne_yapragi_kullanildi|evrak_zaten_kayitli'
cikis "$(cek "ABC0001101" 700)"
kontrol 201 "Karne secmeden aralikta seri: karneye kendiliginden baglandi"
cikis "$(cek "ABC0002000" 700 "$K1")"
kontrol 422 "Karne disi seri" karne_seri_disinda
cikis "$(cek "" 700 "$K2")"
kontrol 422 "Baska bankanin karnesi bu hesaba kullanilamaz" karne_uyumsuz
cikis "$(cek "" 300 "$K1"),$(cek "" 200 "$K1")"
kontrol 201 "Ayni bordroda iki bos seri: sirayla 1100 ve 1102"
istek GET "$CS/karneler/$K1" "$T"
kontrol 200 "Karne doldu: kullanilan 4, iptal 1, kalan 0" '"kullanilan":4,"iptalEdilen":1,"kalan":0,"siradakiSeri":null'
cikis "$(cek "" 100 "$K1")"
kontrol 422 "Dolu karneden yaprak istenemez" karne_doldu
istek DELETE "$CS/karneler/$K1/yaprak-iptal/ABC0001099" "$T"
kontrol 200 "Iptal geri alindi, yaprak yeniden bos" '"siradakiSeri":"ABC0001099"'
istek POST "$CS/karneler/$K1/kapat" "$T"
kontrol 200 "Karne kapatildi" '"aktifMi":false'
cikis "$(cek "" 100 "$K1")"
kontrol 422 "Kapali karne kullanilamaz" karne_kapali
istek POST "$CS/karneler/$K1/ac" "$T"
kontrol 200 "Karne yeniden acildi" '"aktifMi":true'
cikis "$(cek "9999" 100)"
kontrol 201 "Karnesi olmayan seri karnesiz verilir"

echo "--- Senet defteri"
cikis "$(senet "" 250 "$D1"),$(senet "" 250 "$D1")"
kontrol 201 "Iki senet sirayla S-001, S-002"
istek GET "$CS/karneler/$D1" "$T"
kontrol 200 "Senet defterinde 2 yaprak kullanildi" '"kullanilan":2,.*"siradakiSeri":"S-003"'
cikis "$(senet "s-002" 100)"
kontrol 409 "Kullanilan senet numarasi yeniden verilemez" karne_yapragi_kullanildi
cikis "$(senet "VS-9" 100)"
kontrol 201 "Defter disi senet karnesiz"

echo "--- Liste, silme, izolasyon"
istek GET "$CS/karneler?tur=Cek&hesapId=$BANKA" "$T"
kontrol 200 "Hesaba gore liste, azalan uyarisi" "\"id\":\"$K1\".*\"azaldiMi\":true"
istek DELETE "$CS/karneler/$K1" "$T"
kontrol 422 "Yapragi kullanilmis karne silinemez" karne_kullanildi
istek DELETE "$CS/karneler/$K2" "$T"
kontrol 204 "Kullanilmamis karne silindi"
istek GET "$CS/karneler/$D1" "$T2"
kontrol 404 "Baska firma karneyi goremez"
istek GET "$CS/karneler" "$T2"
kontrol 200 "Baska firmanin listesi bos" '^\[\]$'
istek PUT "$CS/ayarlar" "$T" '{"kesideciKimlikZorunlu":true,"senetBorcluKimlikZorunlu":false,"riskDavranisi":"UyarVeOnayIste","karneUyariEsigi":0}'
kontrol 200 "Uyari esigi 0" '"karneUyariEsigi":0'
istek GET "$CS/karneler?tur=Senet" "$T"
kontrol 200 "Esik 0 iken senet defteri azalmis sayilmaz" '"azaldiMi":false'

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
