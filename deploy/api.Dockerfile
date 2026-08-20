# Build context is the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Vilmo.App/Vilmo.App.csproj src/Vilmo.App/
COPY src/Vilmo.Api/Vilmo.Api.csproj src/Vilmo.Api/
RUN dotnet restore src/Vilmo.Api/Vilmo.Api.csproj
COPY src/Vilmo.App src/Vilmo.App
COPY src/Vilmo.Api src/Vilmo.Api
COPY MD/seed MD/seed
RUN dotnet publish src/Vilmo.Api/Vilmo.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
COPY MD/seed /app/seed
ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV FIRST_COMPANY_JSON=/app/seed/first-company.json
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --retries=8 CMD curl -fsS http://127.0.0.1/health || exit 1
ENTRYPOINT ["dotnet", "Vilmo.Api.dll"]
