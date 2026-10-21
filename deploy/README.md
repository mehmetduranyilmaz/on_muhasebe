# Canlı kurulum

Tek sunucu (VPS) üzerinde Docker Compose ile: PostgreSQL, veritabanı güncelleme (tek seferlik), API ve Web.
Web kapsayıcısı Caddy'dir: uygulama dosyalarını sunar, `/api` isteklerini API'ye iletir ve HTTPS sertifikasını
Let's Encrypt'ten otomatik alır/yeniler. Web ve API aynı alan adındadır; CORS gerekmez.

```
İnternet ──443──> web (Caddy) ──/api──> api:8080 ──> postgres:5432
                      └── /  → WASM dosyaları          ▲
                                     migrator (açılışta bir kez) ┘
```

## Gereksinimler

- Linux sunucu, 2 GB RAM üstü, Docker Engine + Compose eklentisi, `openssl`
- Alan adının A kaydı sunucunun IP'sini göstermeli; 80 ve 443 portları açık olmalı

## İlk kurulum

```bash
git clone <repo> onmuhasebe && cd onmuhasebe
deploy/kur.sh muhasebe.firmaniz.com siz@firmaniz.com
```

Betik `deploy/.env` dosyasını rastgele gizli değerlerle oluşturur, imajları derler ve sistemi başlatır.
Sistem yöneticisi şifresi ekrana yazılır. `.env` gizlidir; repoya eklenmez, yedeğini güvenli bir yerde saklayın.

Yerelde denemek için alan adı yerine `localhost` verin (Caddy kendi sertifikasını kullanır, tarayıcı uyarı verir).

**Kurulumdan hemen sonra e-postayı ayarlayın:** sistem yöneticisiyle girip *Sistem → E-posta ve Bildirimler* ekranından
SMTP bilgilerini kaydedin ve test e-postası gönderin. Kayıt (e-posta doğrulama), kullanıcı daveti, firma yöneticisi
daveti ve şifre sıfırlama e-postayla çalışır; SMTP ayarlanmadan kimse hesap açamaz.

## Güncelleme

```bash
git pull && deploy/kur.sh
```

İmajlar yeniden derlenir; `migrator` yeni migration'ları uygular, ardından API yeni sürümle açılır.

## Yedekleme

`yedek` kapsayıcısı her gece 03.00'te (TSİ) veritabanının tam yedeğini (`pg_dump`, sıkıştırılmış) alır ve
`deploy/yedekler/` klasörüne yazar; 14 günden eskileri siler (`.env` içinde `YEDEK_SAATI_UTC`, `YEDEK_GUN` ile değişir).

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env run --rm yedek simdi   # elle yedek
deploy/geri-yukle.sh deploy/yedekler/onmuhasebe-20261021-030000.dump --evet              # geri yükleme
```

Geri yükleme önce mevcut durumun yedeğini alır, API'yi durdurur, yedeği yükler, migration'ları yeniden uygular ve
API'yi başlatır. **Yedekler aynı sunucuda durduğu için sunucu kaybına karşı korumaz:** `deploy/yedekler/` klasörünü
düzenli olarak başka bir yere (nesne depolama, ikinci sunucu) kopyalayın. `.env` dosyası da yedeklenmelidir; o olmadan
yedek açılır ama oturum anahtarları ve SMTP şifresi kaybolur.

## Güvenlik notları

- Web sıkı içerik güvenlik politikasıyla (CSP) sunulur: yalnızca kendi dosyalarımız ve derlemede özeti alınan importmap
  çalışabilir (`deploy/csp.cs`). Kamera izni yalnızca kendi sayfalarımıza açıktır. Tüm yanıtlar HSTS içerir. Yazı tipi
  dahil hiçbir dış kaynak yüklenmez (üçüncü taraflara kullanıcı IP'si gitmez).
- API, Caddy'den gelen `X-Forwarded-For` ile gerçek istemci IP'sini görür (hız sınırı buna göre işler); dışarıdan
  gönderilen sahte başlık Caddy tarafından geçersiz kılınır.
- API veritabanına satır güvenliğine (RLS) tabi `onmuhasebe_uygulama` rolüyle bağlanır. Tablo sahibi (`onmuhasebe`)
  bağlantısı yalnızca `migrator` kapsayıcısına verilir.
- API açılışta veritabanına bağlandığı rolü denetler; rol satır güvenliğini atlayabiliyorsa (süper kullanıcı, BYPASSRLS,
  tablo sahibi) hiç açılmaz.
- PostgreSQL dışarıya açılmaz; yalnızca kapsayıcı ağında erişilir.
- Fatura satırı, stok birimi gibi `firma_id` sütunu olmayan alt tablolar da RLS ile korunur: satır, ancak bağlı olduğu üst
  kayıt firmaya görünüyorsa görünür. Politikalar dış anahtarlardan otomatik türetilir; `rls_senaryo` korumasız tablo kalırsa kalır.
- İmajlar özetle (digest), GitHub Actions adımları commit SHA'sıyla sabitlidir; güncellemeleri Dependabot haftalık PR olarak önerir.
- Kapsayıcılar: API ve Web tüm Linux yetkileri bırakılmış (`cap_drop: ALL`), yetki yükseltme kapalı; API salt okunur dosya
  sisteminde ve root olmayan kullanıcıyla çalışır. Günlükler döndürülür (10 MB × 5).
- Hatalı şifre denemeleri e-posta + IP çiftine sayılır (15 dakikada 5); başkası kendi IP'sinden bir kullanıcıyı
  kilitleyemez. Çok IP'den dağıtık denemeye karşı e-posta başına 50 deneme sınırı vardır.
- Yedek dosyaları yalnızca sahibince okunabilir (0600). Son başarılı yedek 26 saatten eskiyse `yedek` kapsayıcısı
  `unhealthy` görünür.
- `.env` içindeki değerler: `JWT_ANAHTARI` değişirse tüm oturumlar düşer; `BILDIRIM_SIFRELEME_ANAHTARI` değişirse
  kayıtlı SMTP şifresi yeniden girilmelidir.

## Faydalı komutlar

```bash
C="docker compose -f deploy/docker-compose.yml --env-file deploy/.env"
$C ps                     # durum
$C logs -f api            # API günlükleri
$C restart api            # API'yi yeniden başlat
$C down                   # durdur (veriler kalır)
```
