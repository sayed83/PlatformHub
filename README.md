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

Ensure the local API signing key is random and at least 32 characters. The bootstrap secret must be different from the JWT signing key.

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
- This is a starter scaffold, not production-certified. Add rate limiting, lockout, CSRF protection where applicable, refresh-token rotation, logging redaction and automated security tests before deployment.


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
