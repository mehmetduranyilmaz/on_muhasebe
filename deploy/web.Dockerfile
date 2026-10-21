# Web imajı: Blazor WebAssembly dosyaları + Caddy (statik sunum, /api yönlendirmesi, otomatik HTTPS, güvenlik başlıkları).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS derleme
WORKDIR /kaynak
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Web/OnMuhasebe.Web -c Release -o /yayin
# CSP, yayınlanan index.html'deki importmap'in özetiyle üretilir (her derlemede değişebilir).
COPY deploy/csp.cs deploy/
RUN dotnet run deploy/csp.cs -- /yayin/wwwroot/index.html /yayin/csp.caddy

FROM caddy:2.11-alpine@sha256:d8542f48d34a9cf4e4c11a478865229840e87e4c96ea3f439101f31a5d35f75f
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=derleme /yayin/csp.caddy /etc/caddy/csp.caddy
COPY --from=derleme /yayin/wwwroot /srv
