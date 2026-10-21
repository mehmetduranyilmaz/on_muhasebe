#!/usr/bin/env bash
# Değişiklik geçmişi (denetim kaydı) ve dönem kilidi uçtan uca senaryosu. Kullanım: denetim_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_denetim"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
DK=/api/yonetim/donem-kilidi

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
fatura() { # CARI TARIH SATIRLAR [EK]
  echo '{"tur":"Satis","cariHesapId":"'"$1"'","tarih":"'"$2"'","genelIskontoTuru":"Yuzde","genelIskonto":0,"satirlar":['"$3"']'"${4:+,$4}"'}'
}
fsurum() { echo "$GOVDE" | grep -o "\"surum\":[0-9]*" | tail -1 | cut -d: -f2; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Denetim Test AS","ad":"Deniz","soyad":"Ak","eposta":"denetim'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(alan accessToken)
kayit_ol '{"firmaUnvani":"Diger Firma","ad":"D","soyad":"F","eposta":"denetimdiger'"$U"'@ornek.com","sifre":"Sifre1234"}'
T2=$(alan accessToken)
davet_kabul "$T" '{"ad":"Kemal","soyad":"Kul","eposta":"denetimkul'"$U"'@ornek.com","sifre":"Sifre1234","rol":"Kullanici"}'
istek POST /api/yonetim/giris "" '{"eposta":"denetimkul'"$U"'@ornek.com","sifre":"Sifre1234"}'
TK=$(alan accessToken)

istek POST /api/cari/hesaplar "$T" '{"unvan":"Takip Ltd","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","telefon":"0212 111"}'
CARI=$(alan id); SURUM=$(alan surum)
istek POST /api/kasabanka/hesaplar "$T" '{"ad":"Kasa","tur":"Kasa","paraBirimi":"TRY"}'; KASA=$(alan id)

echo "--- Degisiklik gecmisi"
istek GET "/api/cari/degisiklikler?kayitId=$CARI" "$T"
kontrol 200 "Yeni kaydin gecmisi bos" '^\[\]$'
istek PUT "/api/cari/hesaplar/$CARI" "$T" '{"unvan":"Takip Ltd Sti","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","telefon":"0212 222","riskLimiti":1500.50,"surum":'"$SURUM"'}'
kontrol 200 "Cari guncellendi"; SURUM=$(alan surum)
istek GET "/api/cari/degisiklikler?kayitId=$CARI" "$T"
kontrol 200 "Guncelleme kim/ne zaman/eski-yeni ile yazildi" '"kullaniciAdi":"Deniz Ak".*"tablo":"cari_hesap".*"islem":"Guncelleme"'
kontrol 200 "Degisen alanlar eski ve yeni degerleriyle" '"alan":"Unvan","eski":"Takip Ltd","yeni":"Takip Ltd Sti"'
kontrol 200 "Tutar kultur bagimsiz yazildi" '"alan":"RiskLimiti","eski":null,"yeni":"1500.5"'
if echo "$GOVDE" | grep -qE '"alan":"(Surum|GuncellemeTarihi|GuncelleyenId|KisiTipi)"'; then KOD=999; fi
kontrol 200 "Teknik ve degismeyen alanlar yazilmadi"
istek PUT "/api/cari/hesaplar/$CARI" "$T" '{"unvan":"Takip Ltd Sti","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","telefon":"0212 222","riskLimiti":1500.5,"surum":'"$SURUM"'}'
kontrol 200 "Degisiklik olmadan kaydet"; SURUM=$(alan surum)
istek GET "/api/cari/degisiklikler?kayitId=$CARI" "$T"
N=$(echo "$GOVDE" | grep -o '"islem":' | wc -l); [ "$N" = "1" ] || KOD=999
kontrol 200 "Degismeyen kaydetme (10.50 = 10.5) gecmise satir eklemedi"
istek GET "/api/cari/degisiklikler?kayitId=$CARI" "$T2"
kontrol 200 "Baska firma gecmisi goremez" '^\[\]$'
istek GET "/api/cari/degisiklikler?kayitId=$CARI" "$TK"
kontrol 403 "Yonetici olmayan kullanici gecmisi goremez"

istek POST /api/fatura/faturalar "$T" "$(fatura "$CARI" 2026-09-15 "$(satir 1 100 Danismanlik)")"
kontrol 201 "Eylul faturasi"; F1=$(alan id); FS=$(fsurum)
istek PUT "/api/fatura/faturalar/$F1" "$T" "$(fatura "$CARI" 2026-09-15 "$(satir 2 100 Danismanlik),$(satir 1 50 Yol)" '"surum":'"$FS")"
kontrol 200 "Fatura satirlari degisti"; FS=$(fsurum)
istek GET "/api/fatura/degisiklikler?kayitId=$F1" "$T"
kontrol 200 "Fatura satirlarinin degisimi faturanin gecmisinde" '"tablo":"fatura_satiri"'

echo "--- Donem kilidi"
istek POST "/api/cari/hesaplar/$CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Borc","tarih":"2026-09-10","tutar":250}'
kontrol 201 "Kilit oncesi eylul dekontu"; D1=$(alan id)
istek POST "/api/cari/hesaplar/$CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Alacak","tarih":"2026-10-05","tutar":100}'
kontrol 201 "Ekim dekontu"; D2=$(alan id)
istek GET $DK "$T"
kontrol 200 "Baslangicta kilit yok" '"kilitTarihi":null'
istek PUT $DK "$TK" '{"kilitTarihi":"2026-09-30","aciklama":"Eylul KDV"}'
kontrol 403 "Kullanici rolu kilit koyamaz"
istek PUT $DK "$T" '{"kilitTarihi":"2099-01-01"}'
kontrol 400 "Gelecek tarihe kilit konamaz" 'bugünden ileri'
istek PUT $DK "$T" '{"kilitTarihi":"2026-09-30","aciklama":"Eylul KDV beyannamesi verildi"}'
kontrol 200 "Eylul sonuna kilit kondu" '"kilitTarihi":"2026-09-30","aciklama":"Eylul KDV beyannamesi verildi"'
istek GET $DK "$T2"
kontrol 200 "Baska firmanin kilidi yok" '"kilitTarihi":null'

istek POST "/api/cari/hesaplar/$CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Borc","tarih":"2026-09-30","tutar":10}'
kontrol 422 "Kilit gunune hareket eklenemez" '30.09.2026 ve öncesi kilitli.*"kod":"donem_kilitli"'
istek POST "/api/cari/hesaplar/$CARI/hareketler" "$T" '{"tur":"Dekont","yon":"Borc","tarih":"2026-10-01","tutar":10}'
kontrol 201 "Acik doneme hareket eklendi"
istek POST "/api/cari/hareketler/$D1/iptal" "$T"
kontrol 422 "Kilitli donemdeki hareket iptal edilemez" donem_kilitli
istek POST "/api/cari/hareketler/$D2/iptal" "$T"
kontrol 200 "Acik donemdeki hareket iptal edildi"
istek PUT "/api/fatura/faturalar/$F1" "$T" "$(fatura "$CARI" 2026-09-15 "$(satir 3 100 Danismanlik)" '"surum":'"$FS")"
kontrol 422 "Kilitli donemdeki fatura degistirilemez" donem_kilitli
istek PUT "/api/fatura/faturalar/$F1" "$T" "$(fatura "$CARI" 2026-10-15 "$(satir 2 100 Danismanlik),$(satir 1 50 Yol)" '"surum":'"$FS")"
kontrol 422 "Kilitli donemdeki fatura acik doneme tasinamaz" donem_kilitli
istek POST "/api/fatura/faturalar/$F1/iptal" "$T"
kontrol 422 "Kilitli donemdeki fatura iptal edilemez" donem_kilitli
istek POST /api/fatura/faturalar "$T" "$(fatura "$CARI" 2026-10-20 "$(satir 1 100)")"
kontrol 201 "Ekim faturasi"; F2=$(alan id); F2S=$(fsurum)
istek PUT "/api/fatura/faturalar/$F2" "$T" "$(fatura "$CARI" 2026-09-20 "$(satir 1 100)" '"surum":'"$F2S")"
kontrol 422 "Acik donemdeki fatura kilitli doneme tasinamaz" donem_kilitli
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$KASA"'","cariHesapId":"'"$CARI"'","tarih":"2026-09-29","tutar":50}'
kontrol 422 "Kilitli doneme tahsilat girilemez" donem_kilitli
istek POST /api/kasabanka/islemler/tahsilat "$T" '{"hesapId":"'"$KASA"'","cariHesapId":"'"$CARI"'","tarih":"2026-10-02","tutar":50}'
kontrol 201 "Acik doneme tahsilat girildi"
istek PUT "/api/cari/hesaplar/$CARI" "$T" '{"unvan":"Takip Ltd Sti","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY","telefon":"0212 333","riskLimiti":1500.5,"surum":'"$SURUM"'}'
kontrol 200 "Kart bilgileri kilitten etkilenmez"
istek GET "/api/cari/hesaplar/$CARI/hareketler" "$T"
kontrol 200 "Kilitli hareket listede duruyor" "$D1"

istek PUT $DK "$T" '{"kilitTarihi":null}'
kontrol 200 "Kilit kaldirildi" '"kilitTarihi":null'
istek POST "/api/cari/hareketler/$D1/iptal" "$T"
kontrol 200 "Kilit kalkinca eylul hareketi iptal edilebildi"
istek GET "/api/yonetim/degisiklikler?kayitId=$CARI" "$T"
kontrol 404 "Yonetim modulunde gecmis ucu yok"

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
