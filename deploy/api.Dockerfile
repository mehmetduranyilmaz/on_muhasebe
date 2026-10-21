# API imajı. Aynı imaj "--veritabani-guncelle" argümanıyla veritabanı güncelleme sürecini de çalıştırır.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS derleme
WORKDIR /kaynak
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Api/OnMuhasebe.Api -c Release -o /yayin -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=derleme /yayin .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OnMuhasebe.Api.dll"]
