# Senaryoların ortak ortam ayarları; her senaryo başında kaynak olarak okunur. Varsayılanlar yerel geliştirme içindir.
: "${OM_SISTEM_SIFRE:?OM_SISTEM_SIFRE ortam degiskeni tanimli degil - API ayari SistemYoneticisi:Sifre}"
OM_SISTEM_EPOSTA="${OM_SISTEM_EPOSTA:-sistem@onmuhasebe.local}"
SISTEM_GIRIS='{"eposta":"'"$OM_SISTEM_EPOSTA"'","sifre":"'"$OM_SISTEM_SIFRE"'"}'
MAILPIT="${OM_MAILPIT:-http://localhost:8025}/api/v1"
SMTP_SUNUCU="${OM_SMTP_SUNUCU:-localhost}"
SMTP_PORT="${OM_SMTP_PORT:-1025}"
DOS="${OM_DOSYALAR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../Dosyalar" && pwd)}"
GECICI="${OM_GECICI:-${TMPDIR:-/tmp}}"

# Veritabanına doğrudan sorgu (yönetici rolüyle). OM_PSQL verilmezse geliştirme konteyneri kullanılır.
sql() {
  if [ -n "$OM_PSQL" ]; then $OM_PSQL -Atc "$1"
  else MSYS_NO_PATHCONV=1 docker exec onmuhasebe-postgres psql -U onmuhasebe -d onmuhasebe -Atc "$1"; fi
}

# 5 MB sınırını aşan dosya repoda tutulmaz, ilk kullanımda üretilir.
[ -f "$DOS/buyuk.csv" ] || head -c 5242890 /dev/zero | tr '\0' 'A' > "$DOS/buyuk.csv"
