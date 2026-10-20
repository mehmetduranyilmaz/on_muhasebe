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
Canlıda migration'lar script ile kontrollü uygulanır:

```bash
dotnet ef migrations script --idempotent --project src/Modules/Yonetim/OnMuhasebe.Modules.Yonetim --startup-project src/Api/OnMuhasebe.Api --context YonetimDbContext
```

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
src/Modules    İş modülleri (Yonetim, Doviz, Cari; sırada Stok, Fatura, KasaBanka)
src/Web        Blazor WebAssembly PWA
tests          Hesaplama testleri (dotnet test)
```
