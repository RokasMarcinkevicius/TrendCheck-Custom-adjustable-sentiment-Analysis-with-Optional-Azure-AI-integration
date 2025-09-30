
# TrendCheck — .NET 9, Clean Architecture, Azure + Local Analysis

Implements two analysis engines:
- `local`: offline, rule-based (company extraction + finance-tuned sentiment => direction & magnitude).
- `azure`: Azure AI Text Analytics (entities + sentiment) and optional Azure Translator. Calls are throttled (Option A requirement).

Includes:
- REST API (Option A polling + Option B 'translation' step)
- gRPC service (Part 3 alternative transport)
- VS Code/JetBrains `.http` tests

## Projects
- **Domain** — core entities/records only
- **Application** — use cases + abstractions (ports)
- **Infrastructure** — adapters (in-memory store, Local/ Azure analyzers, throttled queue)
- **Api** — ASP.NET Core Web API
- **GrpcService** — ASP.NET Core gRPC server

## Configure Azure (optional for `azure` engine)
Edit **Api/appsettings.json** (and **GrpcService/appsettings.json** if you create one):

```json
{
  "Processing": { "MinSecondsBetweenAzureCalls": 30 },
  "Azure": {
    "TextAnalytics": {
      "Endpoint": "https://<your-text-analytics>.cognitiveservices.azure.com/",
      "Key": "<key>"
    },
    "Translator": {
      "Key": "<key>",
      "Region": "<region>"
    }
  }
}
```

## Run
```
dotnet run --project Api/Api.csproj
# Swagger: http://localhost:5069/swagger
```

## Test via HTTP
Open `tests/analyze.http` (VS Code REST client) and execute requests. Flow:
1. POST `/api/analyze/submit` with `engine = "local"` or `"azure"`
2. GET `/api/analyze/status/{id}` until `Completed`
3. GET `/api/analyze/result/{id}`

## gRPC (Part 3)
```
dotnet run --project GrpcService/GrpcService.csproj
```
Use the generated C# client from `Protos/article.proto` (or any gRPC client) to call `Submit`, `Status`, `Result`.

## Part 0 (spec idea)
Model a FSM with states: `Pending`, `Completed`, `Failed`; actions: `Submit`, `Process`, `Complete`, `Fail`; invariant: for `azure` engine, inter-call delay ≥ configured throttle.

---

Notes:
- Local engine is fully offline and deterministic.
- Azure engine uses Text Analytics (entities+sentiment) + optional Translator. A throttled queue enforces spacing between calls.
- Company-to-ticker resolution comes from a small in-memory directory; extend with your watchlist.
