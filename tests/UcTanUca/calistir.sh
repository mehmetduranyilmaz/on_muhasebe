#!/usr/bin/env bash
# Uçtan uca testleri çalıştırır. Kullanım: calistir.sh [senaryolar|izolasyon|tarayici|hepsi] [test-adı ...]
# API ve Web ayakta olmalı; ayarlar ortam değişkenlerinden okunur (README). Giriş/kayıt hız sınırı varsayılan (10/dk) ise
# testler arasında OM_HIZ_BEKLE saniye beklenir; CI'da sınır yükseltildiği için 0 verilir.
set -u
KOK="$(cd "$(dirname "$0")" && pwd)"
BEKLE="${OM_HIZ_BEKLE:-65}"
NE="${1:-hepsi}"; shift || true
KALAN=0
SENARYOLAR="guvenlik senaryo cari stok fatura kasabanka ekstre cek_senet zorunlu tekrar denetim vade abonelik karne karekod bildirim sifre davet rls yetki isyeri muhasebe aktarim dosya evraktakip arac bakim satinalma tekrar_fatura"

ara() { [ "$BEKLE" -gt 0 ] && sleep "$BEKLE"; return 0; }
# GitHub Actions'ta kalan kontroller hata notu (annotation) olarak da yazılır: özet sayfasında görünür.
ozet() { # GRUP GUNLUK
  grep -E "^KALDI|^SONUC|^ATLANDI" "$2"
  if [ -n "${GITHUB_ACTIONS:-}" ]; then grep -E "^KALDI" "$2" | head -20 | while read -r satir; do echo "::error title=$1::${satir:0:500}"; done; fi
}

senaryolar() {
  local liste="${*:-$SENARYOLAR}" ilk=1
  for s in $liste; do
    [ $ilk = 1 ] || ara; ilk=0
    local dosya="$KOK/Senaryolar/${s}_senaryo.sh"; [ "$s" = senaryo ] && dosya="$KOK/Senaryolar/senaryo.sh"
    echo "== $s"
    bash "$dosya" > "$KOK/cikti/$s.log" 2>&1 || KALAN=$((KALAN+1))
    ozet "$s" "$KOK/cikti/$s.log"
  done
}

izolasyon() {
  echo "== izolasyon"
  dotnet run --project "$KOK/OnMuhasebe.Izolasyon" -c Release > "$KOK/cikti/izolasyon.log" 2>&1 || KALAN=$((KALAN+1))
  ozet izolasyon "$KOK/cikti/izolasyon.log"
}

tarayici() {
  local liste="${*:-hepsi}"
  dotnet build "$KOK/OnMuhasebe.Tarayici" -c Release -v q -nologo > /dev/null || { KALAN=$((KALAN+1)); return; }
  if [ "$liste" = hepsi ]; then
    liste=$(dotnet run --project "$KOK/OnMuhasebe.Tarayici" -c Release --no-build | sed 's/^Testler: //; s/ | hepsi$//; s/,//g')
  fi
  local ilk=1
  for t in $liste; do
    [ $ilk = 1 ] || ara; ilk=0
    dotnet run --project "$KOK/OnMuhasebe.Tarayici" -c Release --no-build -- "$t" > "$KOK/cikti/tarayici-$t.log" 2>&1 || KALAN=$((KALAN+1))
    ozet "tarayici $t" "$KOK/cikti/tarayici-$t.log"
  done
}

mkdir -p "$KOK/cikti"
# Kayıtlar e-posta doğrulamasıyla açılır: SMTP (Mailpit) testlerden önce ayarlanır.
SMTP_SONUC=$(KOK="${OM_API:-http://localhost:5128}" bash -c '. "$0"; smtp_ayarla; echo " $GOVDE"' "$KOK/Senaryolar/ortam.sh" 2>&1)
if [ "${SMTP_SONUC:0:3}" != 200 ]; then
  echo "SMTP ayarlanamadi: ${SMTP_SONUC:0:300}"
  [ -n "${GITHUB_ACTIONS:-}" ] && echo "::error title=SMTP ayarlanamadi::${SMTP_SONUC:0:300}"
  exit 1
fi
case "$NE" in
  senaryolar) senaryolar "$@" ;;
  izolasyon) izolasyon ;;
  tarayici) tarayici "$@" ;;
  hepsi) senaryolar; ara; izolasyon; ara; tarayici ;;
  *) echo "Kullanım: $0 [senaryolar|izolasyon|tarayici|hepsi] [test-adı ...]"; exit 2 ;;
esac

echo; [ $KALAN -eq 0 ] && echo "== HEPSI GECTI" || echo "== $KALAN test grubunda hata var (ayrıntı: $KOK/cikti/*.log)"
exit $((KALAN > 0))
