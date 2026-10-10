# PlatformHub — Clean Architecture (.NET 8)

A single solution with a central API and a separate MVC client. Both login panels call the same API:
- Platform Admin panel: `/PlatformAdmin/Account/Login`
- Tenant/user panel: `/Account/Login`

## Projects
- `PlatformHub.Domain` — entities and domain rules
- `PlatformHub.Application` — DTOs, contracts, use cases
- `PlatformHub.Infrastructure` — EF Core, password hashing, repositories
- `PlatformHub.Api` — central API, authentication endpoints, admin endpoints
- `PlatformHub.Mvc` — UI only; calls the API using `HttpClient`

## Requirements
- .NET 8 SDK
- SQL Server
- Visual Studio 2022

## Configure
1. Update `PlatformHub.Api/appsettings.json` connection string.
2. In `PlatformHub.Mvc/appsettings.json`, set `Api:BaseUrl` to the API URL (default `https://localhost:7201/`).
3. Run from solution folder:
   ```bash
   dotnet restore
   dotnet dev-certs https --trust
   dotnet run --project src/PlatformHub.Api
   dotnet run --project src/PlatformHub.Mvc
   ```
   Or set both projects as startup projects in Visual Studio.
4. Apply database migrations after reviewing the model:
   ```bash
   dotnet tool install --global dotnet-ef --version 8.*
   dotnet ef migrations add InitialPlatformSchema --project src/PlatformHub.Infrastructure --startup-project src/PlatformHub.Api
   dotnet ef database update --project src/PlatformHub.Infrastructure --startup-project src/PlatformHub.Api
   ```
5. Provision the first Platform Admin securely. This starter intentionally does **not** ship a hard-coded administrator password or a public admin-registration endpoint. Implement a one-time CLI/bootstrap command or private deployment seed before production.

## Important security notes
- The API is the authority for identity and permissions.
- The MVC app does not connect directly to SQL Server.
- MVC uses a server-side session cookie for its own browser session and calls the API from server-side `HttpClient`; it does not expose API credentials or tokens to browser JavaScript.
- The API returns an opaque session token in this demo scaffold only if you implement the TODO. For production, prefer API-managed secure cookies on a same-site deployment, or short-lived access tokens plus refresh-token rotation. Do not store bearer tokens in localStorage.
- Platform Admin access is checked against the database on each protected admin API request, not solely from a user-supplied claim.
- This is a scaffold, not a production-certified security implementation. Add rate limiting, CSRF protections, lockout, password reset, email verification, secret management, logging redaction, and automated security tests before deployment.
