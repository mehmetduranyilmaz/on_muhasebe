# Ön Muhasebe

Çok firmalı, modül bazlı lisanslanan ön muhasebe uygulaması.
ASP.NET Core Minimal API + Blazor WebAssembly (PWA, MudBlazor) + PostgreSQL.

## Gereksinimler

- .NET SDK 10.0.400 veya üstü (`global.json`)
- Docker Desktop (PostgreSQL için)

## İlk kurulum

```bash
dotnet tool restore                                       # dotnet-ef (dotnet-tools.json)
docker compose up -d                                      # PostgreSQL 18

# Gizli ayarlar repoda tutulmaz; her geliştirici kendi makinesinde user-secrets ile girer.
cd src/Api/OnMuhasebe.Api
dotnet user-secrets set "Jwt:Anahtar" "<en az 32 karakterlik rastgele değer>"
dotnet user-secrets set "SistemYoneticisi:Eposta" "sen@ornek.com"
dotnet user-secrets set "SistemYoneticisi:Sifre" "<güçlü şifre>"
```

`Jwt:Anahtar` için örnek: `openssl rand -base64 48`. Sistem yöneticisi hesabı API ilk açılışta oluşturulur.

## Çalıştırma

```bash
dotnet run --project src/Api/OnMuhasebe.Api --launch-profile http   # http://localhost:5128
dotnet run --project src/Web/OnMuhasebe.Web --launch-profile http   # http://localhost:5167
```

Geliştirme ortamında migration'lar API açılışında otomatik uygulanır.
Canlıda API açılışta veritabanına dokunmaz; migration'ları ayrı bir süreç uygular
(`dotnet OnMuhasebe.Api.dll --veritabani-guncelle`, yönetici bağlantısıyla).

## Canlı kurulum

Tek komutla Docker kurulumu (PostgreSQL, API, Web, otomatik HTTPS): [deploy/README.md](deploy/README.md).

## Yapılandırma

| Anahtar | Açıklama | Varsayılan |
|---|---|---|
| `ConnectionStrings:OnMuhasebe` | PostgreSQL bağlantısı | geliştirmede docker-compose |
| `Jwt:Anahtar` | Token imza anahtarı (gizli) | — |
| `Jwt:AccessTokenDakika` / `Jwt:RefreshTokenGun` | Token süreleri | 15 / 14 |
| `Cors:IzinliKaynaklar` | Web uygulamasının adresleri | geliştirmede localhost |
| `Kimlik:DakikalikIstekLimiti` | Giriş/kayıt için IP başına dakikalık sınır | 10 |
| `Kimlik:DakikalikYenilemeLimiti` | Oturum yenileme için IP başına dakikalık sınır | 60 |
| `SistemYoneticisi:Eposta` / `Sifre` | İlk sistem yöneticisi (gizli) | — |

Web tarafında `wwwroot/appsettings.json` içindeki `ApiAdresi` boş bırakılırsa Web ile aynı adresteki API kullanılır.
Canlıda Web ve API **aynı ana alan adında** olmalıdır (ör. `app.firma.com` ve `api.firma.com`); oturum çerezi `SameSite=Strict`'tir.

## Yapı

```
src/Api        API host
src/Shared     Ortak domain, altyapı ve sözleşmeler (DTO)
src/Modules    İş modülleri (Yonetim, Doviz, Cari, Stok, Fatura, KasaBanka, Bildirim)
src/Web        Blazor WebAssembly PWA
tests          Hesaplama testleri (dotnet test) ve uçtan uca testler (tests/UcTanUca)
```

## Testler

```bash
dotnet test tests/OnMuhasebe.Hesaplama.Tests     # birim (hesaplama) testleri
```

Uçtan uca testler çalışan API ve Web'e karşı koşar (`tests/UcTanUca`):

| Grup | İçerik |
|---|---|
| `senaryolar` | Modül başına API senaryoları (bash + curl), `Senaryolar/*.sh` |
| `izolasyon` | Firma A'nın kayıtlarına firma B ile API'deki her uçtan erişim denemesi; uç listesi OpenAPI'den okunur, eşlemesi olmayan yeni uç hata sayılır |
| `tarayici` | Headless Chrome ile ekran testleri (DevTools Protocol); karekod testi için `ffmpeg` gerekir |

```bash
export OM_SISTEM_SIFRE="<API'deki SistemYoneticisi:Sifre>"
bash tests/UcTanUca/calistir.sh hepsi                 # ya da: senaryolar | izolasyon | tarayici
bash tests/UcTanUca/calistir.sh tarayici bildirim     # tek test (adlar: dotnet run --project tests/UcTanUca/OnMuhasebe.Tarayici)
```

Giriş/kayıt hız sınırı (10/dk) nedeniyle testler arasında 65 sn beklenir. API `Kimlik__DakikalikIstekLimiti=100000` ile
açılırsa `OM_HIZ_BEKLE=0` verilerek beklemeden çalışır. Günlükler ve ekran görüntüleri `tests/UcTanUca/cikti/` altına yazılır.

| Ortam değişkeni | Açıklama | Varsayılan |
|---|---|---|
| `OM_SISTEM_SIFRE` / `OM_SISTEM_EPOSTA` | Sistem yöneticisi girişi | — / `sistem@onmuhasebe.local` |
| `OM_API` / `OM_WEB` | Adresler | `http://localhost:5128` / `http://localhost:5167` |
| `OM_MAILPIT`, `OM_SMTP_SUNUCU`, `OM_SMTP_PORT` | Bildirim e-postaları için Mailpit | `http://localhost:8025`, `localhost`, `1025` |
| `OM_PSQL` | Doğrudan SQL için komut (ör. `psql postgresql://...`) | geliştirme konteyneri (`docker exec`) |
| `OM_CHROME` | Chrome yolu | standart kurulum yerleri |
| `OM_BEKLEME_CARPANI` | Yavaş makinede tarayıcı bekleme sürelerini uzatır | `1` |
| `OM_GUVENSIZ_SERTIFIKA` | `1`: kendi imzalı sertifikayı kabul et (ör. `deploy` ile localhost) | `0` |
| `OM_PERF_EPOSTA` | Vade testi için 20 bin carili performans firmasının hesabı; yoksa test atlanır | — |

GitHub Actions (`.github/workflows/ci.yml`) her push'ta derleme, birim testleri, API senaryoları ve izolasyon testini;
her gece ve elle tetiklendiğinde tarayıcı testlerini de çalıştırır. Gizli değerler her çalışmada rastgele üretilir.
