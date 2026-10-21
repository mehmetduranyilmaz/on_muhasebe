#!/usr/bin/env bash
# Güvenlik sertleştirme senaryosu: başlıklar, gövde sınırları, hız sınırı. Kullanım: guvenlik_senaryo.sh <api-adresi>
KOK="${1:-${OM_API:-http://localhost:5128}}"
. "$(dirname "$0")/ortam.sh"
DIR="$GECICI/om_tmp_guvenlik"; rm -rf "$DIR"; mkdir -p "$DIR"
GECEN=0; KALAN=0
dogru() { if eval "$1"; then echo "GECTI  $2"; GECEN=$((GECEN+1)); else echo "KALDI  $2 ${3:+-> $3}"; KALAN=$((KALAN+1)); fi; }

U=$(date +%s)
kayit_ol '{"firmaUnvani":"Guvenlik Test","ad":"G","soyad":"T","eposta":"guv'"$U"'@ornek.com","sifre":"Sifre1234"}'
T=$(echo "$GOVDE" | grep -o '"accessToken":"[^"]*' | sed 's/.*:"//')
H="Authorization: Bearer $T"

echo "--- Guvenlik basliklari"
curl -s -D "$DIR/b" -o /dev/null "$KOK/api/cari/hesaplar" -H "$H"
B=$(tr -d '\r' < "$DIR/b")
dogru 'echo "$B" | grep -qi "^X-Content-Type-Options: nosniff"' "nosniff"
dogru 'echo "$B" | grep -qi "^X-Frame-Options: DENY"' "Cerceveye gomme yasak"
dogru 'echo "$B" | grep -qi "^Content-Security-Policy: default-src .none.; frame-ancestors .none."' "API icin kati CSP"
dogru 'echo "$B" | grep -qi "^Referrer-Policy: no-referrer"' "Referrer-Policy"
dogru 'echo "$B" | grep -qi "^Permissions-Policy: camera=()"' "Permissions-Policy"
dogru 'echo "$B" | grep -qi "^Cache-Control: no-store"' "API cevaplari onbelleklenmez"
curl -s -D "$DIR/b2" -o /dev/null "$KOK/api/cari/hesaplar/00000000-0000-0000-0000-000000000000" -H "$H"
dogru 'tr -d "\r" < "$DIR/b2" | grep -qi "^X-Frame-Options: DENY"' "Hata cevaplarinda da basliklar var"
curl -s -D "$DIR/b3" -o "$DIR/g3" -X POST "$KOK/api/cari/hesaplar" -H "$H" -H "Content-Type: application/json" -d '{bozuk'
dogru '! grep -qi "exception\|stack\|at OnMuhasebe" "$DIR/g3"' "Hata cevabinda ic ayrinti (stack trace) yok" "$(cat "$DIR/g3")"

echo "--- Govde boyutu"
head -c 2000000 /dev/zero | tr '\0' 'a' | sed 's/^/{"unvan":"/; s/$/"}/' > "$DIR/buyuk.json"
KOD=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$KOK/api/cari/hesaplar" -H "$H" -H "Content-Type: application/json" --data-binary @"$DIR/buyuk.json")
dogru '[ "$KOD" = 413 ]' "1 MB ustu JSON govde reddedildi (413)" "$KOD"
curl -s -X POST "$KOK/api/kasabanka/hesaplar" -H "$H" -H "Content-Type: application/json" -d '{"ad":"Banka","tur":"Banka","paraBirimi":"TRY"}' -o "$DIR/h"
BANKA=$(grep -o '"id":"[^"]*' "$DIR/h" | head -1 | sed 's/.*:"//')
KOD=$(curl -s -o "$DIR/y" -w "%{http_code}" -X POST "$KOK/api/kasabanka/hesaplar/$BANKA/ekstre-aktarimlari" -H "$H" -F "dosya=@$DOS/banka.csv")
dogru '[ "$KOD" = 201 ]' "Ekstre yukleme ucu genel sinirdan bagimsiz calisiyor" "$KOD"
KOD=$(curl -s -o "$DIR/y" -w "%{http_code}" -X POST "$KOK/api/kasabanka/hesaplar/$BANKA/ekstre-aktarimlari" -H "$H" -F "dosya=@$DOS/buyuk.csv")
dogru '[ "$KOD" = 400 ] && grep -q dosya_cok_buyuk "$DIR/y"' "5 MB ustu ekstre anlasilir hatayla reddedildi" "$KOD $(head -c 200 "$DIR/y")"
head -c 7000000 /dev/zero > "$DIR/cok_buyuk.csv"
KOD=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$KOK/api/kasabanka/hesaplar/$BANKA/ekstre-aktarimlari" -H "$H" -F "dosya=@$DIR/cok_buyuk.csv")
dogru '[ "$KOD" = 413 ]' "Yukleme sinirinin (6 MB) ustu baglantida reddedildi (413)" "$KOD"

echo "--- Hiz siniri"
# Paralel gönderilir: seri istekler bir dakikayı aşarsa sabit pencere sıfırlanır ve sınır hiç aşılmaz.
seq 1 620 | xargs -P 20 -I{} curl -s -o /dev/null -w "%{http_code}\n" "$KOK/api/cari/hesaplar?boyut=1" -H "$H" > "$DIR/kodlar"
SINIR=$(grep -c 429 "$DIR/kodlar")
dogru '[ "$SINIR" -ge 1 ] && [ "$SINIR" -le 40 ]' "Kullanici basina dakikalik sinir asilinca 429 ($SINIR istek reddedildi)"
curl -s -D "$DIR/b4" -o /dev/null "$KOK/api/cari/hesaplar" -H "$H"
dogru 'tr -d "\r" < "$DIR/b4" | grep -qi "^Retry-After: [0-9]"' "429 cevabinda Retry-After"
dogru '[ "$(curl -s -o /dev/null -w "%{http_code}" "$KOK/health")" = 200 ]' "Saglik ucu sinirdan muaf"

echo; echo "SONUC: $GECEN gecti, $KALAN kaldi"
[ "$KALAN" -eq 0 ]
