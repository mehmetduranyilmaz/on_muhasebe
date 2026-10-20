# Ön Muhasebe

Çok firmalı, modül bazlı lisanslanan ön muhasebe uygulaması.
ASP.NET Core Minimal API + Blazor WebAssembly (PWA) + PostgreSQL.

## Gereksinimler

- .NET SDK 10.0.400 veya üstü (`global.json`)
- Docker Desktop (PostgreSQL için)

## Çalıştırma

```bash
docker compose up -d                                     # PostgreSQL
dotnet run --project src/Api/OnMuhasebe.Api              # API
dotnet run --project src/Web/OnMuhasebe.Web              # Web (PWA)
```

## Yapı

```
src/Api        API host
src/Shared     Ortak domain, altyapı ve sözleşmeler (DTO)
src/Modules    İş modülleri (Yonetim, Cari, Stok, Fatura, KasaBanka)
src/Web        Blazor WebAssembly PWA
tests          Hesaplama testleri
```
