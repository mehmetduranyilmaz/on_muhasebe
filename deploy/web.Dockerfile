# Web imajı: Blazor WebAssembly dosyaları + Caddy (statik sunum, /api yönlendirmesi, otomatik HTTPS).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS derleme
WORKDIR /kaynak
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Web/OnMuhasebe.Web -c Release -o /yayin

FROM caddy:2.10-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=derleme /yayin/wwwroot /srv
