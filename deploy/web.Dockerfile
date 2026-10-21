# Web imajı: Blazor WebAssembly dosyaları + Caddy (statik sunum, /api yönlendirmesi, otomatik HTTPS, güvenlik başlıkları).
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS derleme
WORKDIR /kaynak
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Web/OnMuhasebe.Web -c Release -o /yayin
# CSP, yayınlanan index.html'deki importmap'in özetiyle üretilir (her derlemede değişebilir).
COPY deploy/csp.cs deploy/
RUN dotnet run deploy/csp.cs -- /yayin/wwwroot/index.html /yayin/csp.caddy

FROM caddy:2.10-alpine@sha256:4c6e91c6ed0e2fa03efd5b44747b625fec79bc9cd06ac5235a779726618e530d
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=derleme /yayin/csp.caddy /etc/caddy/csp.caddy
COPY --from=derleme /yayin/wwwroot /srv
