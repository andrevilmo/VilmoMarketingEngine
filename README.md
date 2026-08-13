# VilmoMarketingEngine

A .NET 10 (LTS) starter for the Vilmo Marketing Engine, built as a minimal ASP.NET Core
Web API. It ships with a small campaigns feature and an automated test suite so the
project is runnable and verifiable end to end.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)

## Project layout

| Path | Description |
| --- | --- |
| `src/VilmoMarketingEngine.Api` | ASP.NET Core minimal API (health + campaigns endpoints) |
| `tests/VilmoMarketingEngine.Api.Tests` | xUnit unit and integration tests |
| `.cursor/` | Cloud Agent environment (Dockerfile + `environment.json`) |

## Common commands

```bash
# Restore dependencies
dotnet restore VilmoMarketingEngine.slnx

# Build
dotnet build VilmoMarketingEngine.slnx -c Release

# Run the test suite
dotnet test VilmoMarketingEngine.slnx

# Run the API (listens on http://localhost:5080)
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/VilmoMarketingEngine.Api \
  --no-launch-profile --urls http://0.0.0.0:5080
```

## API

| Method | Route | Description |
| --- | --- | --- |
| GET | `/health` | Liveness probe |
| GET | `/campaigns` | List campaigns |
| GET | `/campaigns/{id}` | Fetch a campaign by id |
| POST | `/campaigns` | Create a campaign (`name`, `channel`, `budget`) |

Example:

```bash
curl -X POST http://localhost:5080/campaigns \
  -H "Content-Type: application/json" \
  -d '{"name":"Back to School","channel":"email","budget":12000}'
```
