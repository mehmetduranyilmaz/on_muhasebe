# API imajı. Aynı imaj "--veritabani-guncelle" argümanıyla veritabanı güncelleme sürecini de çalıştırır.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS derleme
WORKDIR /kaynak
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Api/OnMuhasebe.Api -c Release -o /yayin -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca
WORKDIR /app
COPY --from=derleme /yayin .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
EXPOSE 8080
# Dosya ekleri kalıcı birimde: birim ilk bağlanışta bu sahipliği alır.
RUN mkdir -p /veri/dosyalar && chown "$APP_UID" /veri/dosyalar
USER $APP_UID
ENTRYPOINT ["dotnet", "OnMuhasebe.Api.dll"]
