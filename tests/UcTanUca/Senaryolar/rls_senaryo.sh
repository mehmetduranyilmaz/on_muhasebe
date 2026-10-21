#!/usr/bin/env bash
# Veritabanı satır güvenliği (RLS): uygulama rolüyle doğrudan SQL'de firma izolasyonu, alt tablolar dahil.
# Uygulama koddaki filtreyi atlasa bile veritabanı başka firmanın satırını vermemeli. Kullanım: rls_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_rls"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0

dogru() { if eval "$1"; then echo "GECTI  $2"; GECEN=$((GECEN+1)); else echo "KALDI  $2 ${3:+-> $3}"; KALAN=$((KALAN+1)); fi; }
alan() { echo "$GOVDE" | grep -o "\"$1\":\"[^\"]*\"" | head -1 | cut -d'"' -f4; }
firma_id() { echo "$1" | cut -d. -f2 | tr '_-' '/+' | base64 -d 2>/dev/null | grep -o '"firma_id":"[^"]*' | cut -d'"' -f4; }
firmada() { echo "SELECT set_config('app.firma_id', '$1', false); $2"; }   # FIRMA SQL: firma oturumunda çalıştırılacak SQL
api() { curl -s -X POST "$KOK$1" -H "Content-Type: application/json" -H "Authorization: Bearer $2" --data-binary "$3"; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"RLS A","ad":"A","soyad":"A","eposta":"rlsa'"$U"'@ornek.com","sifre":"Sifre1234"}'; TA=$(alan accessToken)
kayit_ol '{"firmaUnvani":"RLS B","ad":"B","soyad":"B","eposta":"rlsb'"$U"'@ornek.com","sifre":"Sifre1234"}'; TB=$(alan accessToken)
FA=$(firma_id "$TA"); FB=$(firma_id "$TB")
CARI=$(api /api/cari/hesaplar "$TA" '{"unvan":"RLS Cari","tur":"Musteri","kisiTipi":"Tuzel","paraBirimi":"TRY"}' | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)
FATURA=$(api /api/fatura/faturalar "$TA" '{"tur":"Satis","cariHesapId":"'"$CARI"'","tarih":"2026-10-15","genelIskontoTuru":"Yuzde","genelIskonto":0,"satirlar":[{"stokKartiId":null,"birim":"ADET","miktar":1,"birimFiyat":100,"kdvOrani":20,"kdvDahilMi":false,"iskonto1":0,"iskonto2":0,"iskonto3":0,"ad":"RLS hizmet"}]}' \
  | grep -o '"fatura":{"id":"[^"]*' | cut -d'"' -f6)
dogru '[ -n "$FA" ] && [ -n "$FB" ] && [ -n "$FATURA" ]' "Hazirlik: iki firma ve A'nin faturasi" "FA=$FA FB=$FB FATURA=$FATURA"

echo "--- Firma oturumu olmadan hicbir satir gorunmez"
for t in fatura.fatura fatura.fatura_satiri stok.stok_karti_birimi kasabanka.ekstre_satiri kasabanka.karne_yaprak_iptali cari.cari_hesap; do
  N=$(uygulama_sql "SELECT count(*) FROM $t")
  dogru '[ "$N" = 0 ]' "$t: firma secilmeden 0 satir" "$N"
done

echo "--- Alt tablo (fatura satiri) ust kayit uzerinden korunur"
N=$(uygulama_sql "$(firmada "$FA" "SELECT count(*) FROM fatura.fatura_satiri WHERE fatura_id = '$FATURA'")" | tail -1)
dogru '[ "$N" = 1 ]' "A kendi fatura satirini gorur" "$N"
N=$(uygulama_sql "$(firmada "$FB" "SELECT count(*) FROM fatura.fatura_satiri WHERE fatura_id = '$FATURA'")" | tail -1)
dogru '[ "$N" = 0 ]' "B, A'nin fatura satirini goremez (filtre olmadan, dogrudan SQL)" "$N"
N=$(uygulama_sql "$(firmada "$FB" "WITH g AS (UPDATE fatura.fatura_satiri SET ad = 'SIZDI' WHERE fatura_id = '$FATURA' RETURNING 1) SELECT count(*) FROM g")" | tail -1)
dogru '[ "$N" = 0 ]' "B, A'nin fatura satirini degistiremez" "$N"
HATA=$(uygulama_sql "$(firmada "$FA" "CREATE TEMP TABLE kopya AS SELECT * FROM fatura.fatura_satiri WHERE fatura_id = '$FATURA'; UPDATE kopya SET id = gen_random_uuid();") SELECT set_config('app.firma_id', '$FB', false); INSERT INTO fatura.fatura_satiri SELECT * FROM kopya;" 2>&1)
dogru 'echo "$HATA" | grep -qi "row-level security"' "B, A'nin faturasina satir ekleyemez (WITH CHECK)" "${HATA:0:200}"
N=$(uygulama_sql "$(firmada "$FA" "SELECT count(*) FROM fatura.fatura_satiri WHERE fatura_id = '$FATURA' AND ad = 'RLS hizmet'")" | tail -1)
dogru '[ "$N" = 1 ]' "A'nin satiri degismeden duruyor" "$N"

echo "--- Kapsam: korumasiz tablo kalmadi"
KORUMASIZ=$(sql "SELECT string_agg(n.nspname || '.' || c.relname, ' ' ORDER BY n.nspname, c.relname) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
  WHERE c.relkind = 'r' AND NOT c.relrowsecurity AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'yonetim', 'public')
    AND n.nspname NOT LIKE 'pg\_%' AND c.relname <> '__ef_migrations_history'")
# Bilinçli olarak firmalar arası ortak tablolar: TCMB kurları ve sistem SMTP ayarı.
dogru '[ "$KORUMASIZ" = "bildirim.eposta_ayari doviz.kur doviz.kur_bulteni" ]' "RLS'siz tablolar yalnizca ortak (firmasiz) tablolar" "$KORUMASIZ"

echo
echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
