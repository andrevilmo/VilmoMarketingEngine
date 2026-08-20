FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Vilmo.App/Vilmo.App.csproj src/Vilmo.App/
COPY src/Vilmo.Nfe/Vilmo.Nfe.csproj src/Vilmo.Nfe/
RUN dotnet restore src/Vilmo.Nfe/Vilmo.Nfe.csproj
COPY src/Vilmo.App src/Vilmo.App
COPY src/Vilmo.Nfe src/Vilmo.Nfe
RUN dotnet publish src/Vilmo.Nfe/Vilmo.Nfe.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV Nfe__CertificatesDirectory=/certs
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --retries=8 CMD curl -fsS http://127.0.0.1/health || exit 1
ENTRYPOINT ["dotnet", "Vilmo.Nfe.dll"]
