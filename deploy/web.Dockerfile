# Web imajı: Blazor WebAssembly dosyaları + Caddy (statik sunum, /api yönlendirmesi, otomatik HTTPS, güvenlik başlıkları).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS derleme
WORKDIR /kaynak
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Web/OnMuhasebe.Web -c Release -o /yayin
# CSP, yayınlanan index.html'deki importmap'in özetiyle üretilir (her derlemede değişebilir).
COPY deploy/csp.cs deploy/
RUN dotnet run deploy/csp.cs -- /yayin/wwwroot/index.html /yayin/csp.caddy

FROM caddy:2.10-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=derleme /yayin/csp.caddy /etc/caddy/csp.caddy
COPY --from=derleme /yayin/wwwroot /srv
