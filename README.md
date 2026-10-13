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

## Local configuration (User Secrets)
Sensitive values must not be committed to source control. From the solution directory run:

```powershell
dotnet user-secrets set "ConnectionStrings:PlatformDb" "YOUR_SQL_SERVER_CONNECTION_STRING" --project src/PlatformHub.Api
dotnet user-secrets set "Jwt:SigningKey" "GENERATE-A-RANDOM-SECRET-AT-LEAST-32-CHARACTERS" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformBootstrap:SetupKey" "GENERATE-A-DIFFERENT-RANDOM-SECRET-AT-LEAST-32-CHARACTERS" --project src/PlatformHub.Api
```

Ensure the API signing key is random and at least 32 characters. Startup now fails fast if the JWT signing key is missing/short or the issuer/audience are blank. The bootstrap secret must be different from the JWT signing key.

Configure `Api:BaseUrl` in `src/PlatformHub.Mvc/appsettings.json` to match the API URL (default `https://localhost:7201/`).

## Run
```powershell
dotnet restore
dotnet dev-certs https --trust
dotnet run --project src/PlatformHub.Api
dotnet run --project src/PlatformHub.Mvc
```
Or set both web projects as startup projects in Visual Studio.

## Database migrations
Review and apply migrations using EF Core tools:
```powershell
dotnet tool install --global dotnet-ef --version 8.*
dotnet ef database update --project src/PlatformHub.Infrastructure --startup-project src/PlatformHub.Api
```
Do not create another initial migration if one has already been generated.

## First Platform Admin (local development only)
This repo contains a one-time API endpoint for local initialization:
- `POST /api/v1/platform-bootstrap/first-admin`
- Header: `X-Platform-Setup-Key: <your PlatformBootstrap:SetupKey>`
- JSON body: `{ "userName": "platformadmin", "displayName": "Platform Administrator", "password": "<a long unique password>" }`

The endpoint returns 404 outside Development, refuses initialization if any Platform Admin already exists, verifies the private setup key in constant time, and hashes the password with ASP.NET Core's `PasswordHasher`. Configure the setup key with User Secrets. Do not expose this endpoint in a deployed environment; use a private provisioning process for production.

## Security notes
- The API is the authority for identity and permissions.
- MVC does not connect directly to SQL Server.
- Platform Admin access must be checked server-side for every protected operation, not merely from a client-supplied claim.
- Use environment variables or a secret manager in deployments. Do not commit credentials.
- If credentials were ever committed, rotate them; replacing the file alone does not remove them from Git history.
- Production hardening includes startup validation for JWT key length/issuer/audience, HSTS outside Development, IP-based fixed-window limits on login (10 requests/minute) and bootstrap (3 requests/5 minutes), and persistent account lockout after 5 consecutive incorrect passwords for 15 minutes. When deployed behind a reverse proxy, configure trusted forwarded headers before relying on client IP-based limits. This is still a starter scaffold, not production-certified: add refresh-token rotation, session/token invalidation, monitoring/alerting, deployment secret management, and production integration tests before deployment.


## Role-based access control (RBAC)

The platform access model uses custom claim names (prefixed `ph_`) and explicit tenant/branch/project scope assignments.

### Built-in protected roles
- `PLATFORM_SUPER_ADMIN` — system role seeded with the complete permission catalog. Its role and permission set cannot be edited through the role-management API.
- `PLATFORM_ADMIN`
- `TENANT_ADMIN`
- `BRANCH_ADMIN`
- `PROJECT_USER`

System roles are seeded idempotently at API startup **after the RBAC migration has been applied**. Existing active accounts with `AppUsers.IsPlatformAdmin = 1` are assigned to the protected Platform Super Admin role without creating duplicate accounts.

### Apply the RBAC migration

Review the migration and confirm the previous migrations are recorded in `dbo.__EFMigrationsHistory` before applying:

```powershell
dotnet ef database update --project src/PlatformHub.Infrastructure --startup-project src/PlatformHub.Api --context PlatformDbContext
dotnet build
```

Do not drop or recreate `AppUsers`, `Tenants`, `Projects`, or `Branches`.

### Access API
- Platform Admin navigation includes a Roles & Permissions page for listing roles, creating custom roles, and assigning permissions to custom roles.
- `GET /api/v1/platform/access/roles` — list roles and assigned permission codes
- `POST /api/v1/platform/access/roles` — create a custom role
- `GET /api/v1/platform/access/permissions` — list permission catalog
- `PUT /api/v1/platform/access/roles/{roleId}/permissions` — replace permissions on a custom role
- `GET /api/v1/platform/access/users/{userId}/roles` — list role assignments
- `POST /api/v1/platform/access/users/{userId}/roles` — assign a role with optional tenant/branch/project scope
- `DELETE /api/v1/platform/access/users/{userId}/roles/{assignmentId}` — remove a non-Super-Admin assignment

These endpoints require Platform Admin authentication plus the corresponding permission claim. A new login is required after role or permission changes because permissions are placed in the short-lived JWT. Scope assignments are stored independently in the database; every tenant-facing API must enforce the relevant scope server-side before exposing tenant data.


## User & Access Management

The Platform Admin UI includes **Users & access** for accounts with the `User.View` permission.

- Create regular user accounts with a password hashed by ASP.NET Core Identity's `PasswordHasher<AppUser>`; platform-admin status cannot be self-selected on the create form.
- Search users and filter by active/inactive state.
- Update display name, user-specific redirect URL, and account status.
- Reset a user's password (minimum 12 characters).
- Assign multiple roles to a user, with optional tenant, branch, and project scope.
- Remove non-Super-Admin role assignments.
- Prevent deactivation of the last active Platform Super Admin.
- Validate scope references server-side and reject inconsistent tenant/branch/project assignments.

API routes:
- `GET /api/v1/platform/access/users?search=&active=`
- `POST /api/v1/platform/access/users`
- `PUT /api/v1/platform/access/users/{userId}`
- `POST /api/v1/platform/access/users/{userId}/reset-password`
- `GET /api/v1/platform/access/users/{userId}/roles`
- `POST /api/v1/platform/access/users/{userId}/roles`
- `DELETE /api/v1/platform/access/users/{userId}/roles/{assignmentId}`

All routes require a valid Platform Admin JWT and the corresponding `User.View`, `User.Create`, `User.Update`, `User.Deactivate`, or `Role.Assign` permission. The MVC screen is only a UI layer; authorization is enforced by the API.

After a role/permission change, users should sign in again to refresh claims. Before production, implement forced session/token invalidation and a first-login password-change flow; do not distribute temporary passwords over insecure channels.


## Tenant / branch / project scope authorization

The API now provides `IScopedAccessService` (`PlatformHub.Infrastructure.Platform`) for database-backed scope checks. It verifies the user is active, verifies that the tenant/branch/project are active and belong to the same tenant, and checks that the user's active role assignment grants the requested permission at a compatible scope. Platform Admin status is re-read from the database before bypassing tenant scopes; JWT scope claims alone are not trusted.

A sample endpoint is available at:

```http
GET /api/v1/workspace/context?tenantId=1&branchId=2&projectId=3
Authorization: Bearer <access-token>
```

`branchId` and `projectId` are optional. The endpoint requires the active assignment to grant `Tenant.View`; a scope mismatch returns `403 Forbidden`.

**Required for each future tenant-facing endpoint:** call `IScopedAccessService.CanAccessAsync(userId, requiredPermissionCode, tenantId, branchId, projectId, cancellationToken)` before querying or returning tenant data. Use the tenant/branch/project IDs resolved from the requested resource on the server, not IDs supplied only by the UI. Platform management endpoints are global administration endpoints and remain protected by Platform Admin policies.


## Optional default users for built-in roles

The existing `platformadmin` account is seeded/assigned to the protected `PLATFORM_SUPER_ADMIN` role. To provision one additional default account for each other built-in role, configure each account through User Secrets (never hard-code passwords in source or appsettings). The API seeds these accounts idempotently at startup: existing users/passwords are not overwritten and role assignments are not duplicated.

Use unique, strong passwords of at least 12 characters. Set `TenantId` for Tenant Admin, `BranchId` for Branch Admin, and `ProjectId` for Project User to existing active records. Branch and project IDs must belong to the selected tenant.

Run from the solution root, replacing every placeholder with values from your own database and unique passwords:

```powershell
dotnet user-secrets set "PlatformRoleUserSeeds:PlatformAdmin:UserName" "platformops" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:PlatformAdmin:DisplayName" "Platform Operations Admin" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:PlatformAdmin:Password" "REPLACE_WITH_UNIQUE_STRONG_PASSWORD" --project src/PlatformHub.Api

dotnet user-secrets set "PlatformRoleUserSeeds:TenantAdmin:UserName" "tenantadmin" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:TenantAdmin:DisplayName" "Default Tenant Admin" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:TenantAdmin:Password" "REPLACE_WITH_DIFFERENT_STRONG_PASSWORD" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:TenantAdmin:TenantId" "1" --project src/PlatformHub.Api

dotnet user-secrets set "PlatformRoleUserSeeds:BranchAdmin:UserName" "branchadmin" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:BranchAdmin:DisplayName" "Default Branch Admin" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:BranchAdmin:Password" "REPLACE_WITH_ANOTHER_STRONG_PASSWORD" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:BranchAdmin:TenantId" "1" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:BranchAdmin:BranchId" "1" --project src/PlatformHub.Api

dotnet user-secrets set "PlatformRoleUserSeeds:ProjectUser:UserName" "projectuser" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:ProjectUser:DisplayName" "Default Project User" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:ProjectUser:Password" "REPLACE_WITH_A_FOURTH_STRONG_PASSWORD" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:ProjectUser:TenantId" "1" --project src/PlatformHub.Api
dotnet user-secrets set "PlatformRoleUserSeeds:ProjectUser:ProjectId" "1" --project src/PlatformHub.Api
```

If an account's settings are only partially configured, startup fails with a clear configuration error rather than creating an insecure default. Leave a role's section entirely unset to skip seeding that account. After configuring the desired accounts, restart the API. Remove or rotate these seed secrets after initial provisioning if they are no longer needed.


## Security tests

A focused automated test project is available under `tests/PlatformHub.SecurityTests`. It covers branch-to-tenant isolation, exact branch scope, missing permission denial, and inactive-user denial.

Run it from the repository root:

```powershell
dotnet test tests/PlatformHub.SecurityTests/PlatformHub.SecurityTests.csproj
```

The tests use EF Core InMemory and do not connect to or modify your development SQL Server database. Add equivalent integration tests for every new tenant-facing API endpoint before release.


## Refresh tokens and session revocation

- Access tokens expire after 15 minutes and are bound to a persisted refresh session.
- `POST /api/v1/auth/refresh` rotates a refresh token. Only a SHA-256 hash is stored in SQL Server; the previous token is revoked atomically.
- Reuse of a rotated refresh token revokes the user's active refresh sessions and requires a fresh login.
- `POST /api/v1/auth/logout` revokes the supplied refresh session. Protected API requests validate that the access token's session remains active and that the user is still active.
- Apply the `AddRefreshSessions` migration before deploying this version. The MVC login ticket holds the refresh token in an encrypted, HttpOnly authentication cookie; browser JavaScript does not receive it.

## Production readiness

See [docs/PRODUCTION-READINESS.md](docs/PRODUCTION-READINESS.md) for deployment gates, health probes, monitoring/alert recommendations, authentication smoke tests, and rollback guidance.

- `GET /health/live` reports process liveness.
- `GET /health/ready` verifies SQL Server connectivity and returns 503 when the database is unavailable.
- These endpoints do not expose database errors or secrets. Configure your hosting platform to monitor them and alert on repeated failures.


## Active tenant context and tenant-safe query access

The MVC workspace selector now activates a selected scope with a CSRF-protected POST. The selected tenant, branch and project IDs are stored in the encrypted, HttpOnly MVC authentication ticket and forwarded server-to-server as `X-Platform-TenantId`, `X-Platform-BranchId`, and `X-Platform-ProjectId`. The browser does not set these API headers directly.

The API middleware resolves these headers into a request-scoped `IActiveTenantContext` only after validating that the tenant, branch and project are active and consistent and that the user has a matching active role assignment. Scope headers are optional for requests made before a workspace is selected. A stale or invalid selected scope is rejected; MVC clears a stale selection when the current assignment list no longer contains it.

**Important:** an active context is scope metadata, not an authorization grant. Every tenant-facing operation must still call `IScopedAccessService.CanAccessAsync` using the permission required by that operation and IDs resolved from the validated context/resource. For EF Core business queries, use `WhereInActiveTenant` or `WhereInActiveWorkspace` from `PlatformHub.Api.Platform` to apply tenant/branch/project filters. For update/delete, load the target row through the same scoped query and validate related foreign keys before saving. Do not rely on menu visibility or request IDs alone.

Example:

```csharp
var products = await db.Set<Product>()
    .WhereInActiveWorkspace(activeScope,
        nameof(Product.TenantId),
        nameof(Product.BranchId),
        nameof(Product.ProjectId))
    .AsNoTracking()
    .ToListAsync(ct);
```

The query helper fails closed when no active context is resolved. It filters only mapped ownership keys named by the caller; it does not automatically secure arbitrary raw SQL/Dapper, exports, background jobs, or entities without these ownership columns. Those paths need explicit scope predicates and authorization tests. No database migration is required for the active-context change.

Security regression tests are in `tests/PlatformHub.SecurityTests/TenantSafeQueryTests.cs`.
