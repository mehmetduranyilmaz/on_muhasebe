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

# --- Hesap akışları (e-postadaki bağlantılar Mailpit'ten okunur)
OM_KOK="${KOK:-${API%/api/yonetim}}"

# Adrese giden son e-postadaki bağlantı kodu. Kullanım: mailpit_kodu EPOSTA YOL (eposta-dogrula | davet | sifre-sifirla)
mailpit_kodu() {
  local id kod i
  for i in $(seq 1 30); do
    id=$(curl -s "$MAILPIT/search?query=to:$1" | grep -o '"ID":"[^"]*"' | head -1 | cut -d'"' -f4)
    [ -n "$id" ] && kod=$(curl -s "$MAILPIT/message/$id" | grep -o "$2#kod=[A-Za-z0-9_-]*" | head -1 | cut -d= -f2)
    [ -n "$kod" ] && { echo "$kod"; return 0; }
    sleep 0.3
  done
  return 1
}

_hesap_istek() { # YOL GOVDE [TOKEN] [CEREZ] -> KOD, GOVDE
  local args=(-s -o "$GECICI/om_hesap_govde" -w "%{http_code}" -X POST "$OM_KOK$1" -H "Content-Type: application/json" --data-binary "$2")
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(-b "$4" -c "$4")
  KOD=$(curl "${args[@]}"); GOVDE=$(cat "$GECICI/om_hesap_govde")
}

# Kayıt + e-postadaki bağlantıyla doğrulama. Kullanım: kayit_ol KAYIT_JSON [CEREZ]; sonuç doğrulama yanıtı (200 + accessToken).
kayit_ol() {
  local eposta kod
  eposta=$(echo "$1" | grep -o '"eposta":"[^"]*"' | cut -d'"' -f4 | tr 'A-Z' 'a-z')
  _hesap_istek /api/yonetim/kayit "$1"
  [ "$KOD" = 202 ] || return 1
  kod=$(mailpit_kodu "$eposta" eposta-dogrula) || { KOD=000; GOVDE="dogrulama e-postasi gelmedi: $eposta"; return 1; }
  _hesap_istek /api/yonetim/eposta-dogrula '{"kod":"'"$kod"'"}' "" "$2"
}

# Yöneticinin daveti + davet edilenin bağlantıyla kabulü (yeni hesap). Kullanım: davet_kabul YONETICI_TOKEN '{"ad":..,"soyad":..,"eposta":..,"sifre":..,"rol":..}'
# Davet reddedilirse (ör. limit) davet yanıtı, kabul edilirse kabul yanıtı (200 + accessToken) döner.
davet_kabul() {
  local alan_ eposta rol kod
  alan_() { echo "$2" | grep -o "\"$1\":\"[^\"]*\"" | cut -d'"' -f4; }
  eposta=$(alan_ eposta "$2" | tr 'A-Z' 'a-z'); rol=$(alan_ rol "$2")
  _hesap_istek /api/yonetim/kullanicilar '{"eposta":"'"$eposta"'","rol":"'"$rol"'"}' "$1"
  [ "$KOD" = 201 ] || return 1
  kod=$(mailpit_kodu "$eposta" davet) || { KOD=000; GOVDE="davet e-postasi gelmedi: $eposta"; return 1; }
  _hesap_istek /api/yonetim/davet-kabul '{"kod":"'"$kod"'","ad":"'"$(alan_ ad "$2")"'","soyad":"'"$(alan_ soyad "$2")"'","sifre":"'"$(alan_ sifre "$2")"'"}'
}

# Kayıt, davet ve şifre e-postaları için SMTP'yi Mailpit'e ayarlar (boş veritabanında testlerden önce bir kez).
smtp_ayarla() {
  _hesap_istek /api/yonetim/giris "$SISTEM_GIRIS"
  local token; token=$(echo "$GOVDE" | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4)
  curl -s -o /dev/null -w "%{http_code}" -X PUT "$OM_KOK/api/bildirim/sistem/eposta" -H "Content-Type: application/json" \
    -H "Authorization: Bearer $token" \
    --data-binary '{"aktifMi":true,"sunucu":"'"$SMTP_SUNUCU"'","port":'"$SMTP_PORT"',"guvenlik":"Yok","kullaniciAdi":null,"gonderenAdres":"bildirim@onmuhasebe.local","gonderenAd":"On Muhasebe","sifreKayitliMi":false}'
}
