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

## Güncelleme

```bash
git pull && deploy/kur.sh
```

İmajlar yeniden derlenir; `migrator` yeni migration'ları uygular, ardından API yeni sürümle açılır.

## Güvenlik notları

- API veritabanına satır güvenliğine (RLS) tabi `onmuhasebe_uygulama` rolüyle bağlanır. Tablo sahibi (`onmuhasebe`)
  bağlantısı yalnızca `migrator` kapsayıcısına verilir.
- PostgreSQL dışarıya açılmaz; yalnızca kapsayıcı ağında erişilir.
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
