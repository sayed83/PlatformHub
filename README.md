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

**Tenant API enforcement checklist**
- Navigation and endpoint-level menu checks require a validated active tenant context; when a project is selected in the context, a different requested project is denied.
- Effective menu access is evaluated for the selected tenant only. A reusable/shared project is not authorization by itself: that tenant must have an active TenantProjects subscription and the user's role assignment must be explicitly anchored to that tenant (or to a branch belonging to it).
- Keep platform-wide administration endpoints under their existing PlatformAdmin policies. Do not apply tenant-context requirements to platform catalog/tenant management endpoints.
- Every tenant data endpoint must resolve the tenant/branch/project from the target resource and call IScopedAccessService.CanAccessAsync before returning or mutating data. For writes, validate that every referenced child record belongs to the same tenant/workspace before saving.
- EF Core query helpers are opt-in, not global filters. Dapper, raw SQL, stored procedures, exports, background jobs and bulk updates must include explicit tenant/workspace predicates and must fail closed when scope is missing. Do not pass an untrusted client-provided tenant ID straight into a query.
- Security regression tests must cover cross-tenant resource IDs, inactive tenants/users/branches/projects, missing permissions, user-specific deny overrides, and shared projects with and without tenant subscriptions.


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

The query helper fails closed when no active context is resolved. When a branch or project is selected, `WhereInActiveWorkspace` also fails closed if the caller omits that selected dimension's ownership property; this prevents an accidentally tenant-only query from leaking rows across branches/projects. Use `WhereInActiveTenant` explicitly for entities that are tenant-owned only. It filters only mapped ownership keys named by the caller; it does not automatically secure arbitrary raw SQL/Dapper, exports, background jobs, or entities without these ownership columns. Those paths need explicit scope predicates and authorization tests. No database migration is required for this query-helper hardening.

Security regression tests are in `tests/PlatformHub.SecurityTests/TenantSafeQueryTests.cs`.


## Tenant API security audit and regression coverage

### Scope boundaries
- Platform-wide administration routes (including `/api/v1/platform`, `/api/v1/platform/access`, `/api/v1/platform/tenants/{tenantId}/projects`, and `/api/v1/project-menus`) remain protected by the `PlatformAdmin` policy. They intentionally operate across tenants for platform administration and must not be forced through the selected tenant context.
- Tenant workspace endpoints (including `/api/v1/workspace/scopes` and `/api/v1/workspace/context`) validate the authenticated active user and resolve tenant, branch, project and subscription scope before exposing workspace details.
- Navigation and menu endpoint authorization require a validated active tenant context and tenant-aware effective role resolution. Menu visibility is not a substitute for endpoint authorization.
- Bootstrap endpoints are a separate one-time provisioning boundary and must remain protected by setup-key/rate-limit controls; disable the endpoint after initial provisioning in deployed environments.

### Required checks for each business-data endpoint
1. Resolve the tenant from the target resource or a validated active workspace; never trust a tenant ID supplied only by the caller.
2. Call `IScopedAccessService.CanAccessAsync` with the endpoint's permission and the resolved tenant/branch/project IDs before reading or mutating business data.
3. For reads, include tenant and workspace predicates in the database query. For updates/deletes, load the target row through that scoped query first.
4. Validate every related/child ID (for example, a detail row, parent, or foreign key) belongs to the same tenant/workspace before saving.
5. Include explicit predicates in Dapper, raw SQL, stored procedures, bulk operations, exports and background jobs; EF helper methods are opt-in and do not automatically secure every query.
6. Preserve reusable/shared projects: require an active `TenantProjects` subscription and an assignment explicitly anchored to the selected tenant. A tenantless project assignment is not a wildcard.

### Regression tests
The security test project covers cross-tenant branch IDs, tenant/project mismatches, inactive users/tenants/projects, missing permissions, disabled shared-project subscriptions, tenantless shared-project assignments, and explicit user menu-deny overrides. Add an endpoint-level integration test whenever a new tenant-facing controller or business-data route is introduced.


## Tenant-facing endpoint authorization (Phase 3)

Use `[RequireScopedPermission("Permission.Code")]` on tenant-facing business-data API controllers/actions. The filter requires an authenticated user, a validated active tenant context, and a current database-backed permission check through `IScopedAccessService`. Optional `branchId`/`projectId` query parameters or `X-Branch-Id`/`X-Project-Id` headers can only confirm the selected workspace; they cannot switch the active scope.

Do **not** add this filter globally or to platform-wide administration controllers. Platform administration endpoints intentionally operate across tenants and continue to use their existing `PlatformAdmin` policy. Workspace discovery/context endpoints have their own explicit scope validation and should not be forced through an already-selected workspace.

For every new tenant business endpoint:
- Add `[Authorize]` plus `[RequireScopedPermission("...")]` (or equivalent explicit service check).
- Resolve the target resource within the active tenant/branch/project; authorization alone does not scope the data query.
- For updates/deletes, fetch the target using tenant/workspace predicates before mutating it.
- Validate parent/detail/foreign-key IDs belong to the same workspace.
- Keep Dapper/raw SQL, exports, bulk operations and background jobs explicitly scoped; the attribute cannot automatically secure query predicates.
- Add endpoint-level tests for cross-tenant IDs and denied writes/deletes.


## Phase 4 tenant-isolation audit — current API inventory

The current API is a platform-management scaffold; it does not yet contain Inventory, Sales, Purchase, or Accounting business-data controllers. Do not treat those future endpoints as secured merely because active workspace context exists.

- `WorkspaceController` is workspace discovery/context functionality. Its context endpoint validates requested tenant, branch, and project access explicitly through `IScopedAccessService`.
- `NavigationController` requires a resolved active tenant and resolves menu access for the selected tenant/project. Navigation visibility is not a substitute for business endpoint authorization.
- `PlatformController`, `PlatformAccessController`, `TenantProjectsController`, `ProjectMenusController`, and `MenuAccessController` are intentionally platform-administration endpoints protected by Platform Admin policies; do not force them through a tenant-only filter.
- The current Dapper call in `PlatformService.GetProjectsAsync` invokes `dbo.usp_Platform_GetProjects` from a Platform Admin endpoint. No tenant-facing business stored-procedure call is present in the current API inventory. Any future business procedure must receive validated tenant/workspace IDs and enforce those predicates inside SQL.
- Tenant-facing business endpoints must require authentication and a specific scoped permission, load resources through tenant/branch/project-scoped predicates, validate related IDs on writes, and add cross-tenant read/update/delete tests.
- Background jobs and exports must resolve their tenant scope explicitly; they must never depend on an ambient HTTP request context.

Authorization-filter regression tests in `tests/PlatformHub.SecurityTests/RequireScopedPermissionAttributeTests.cs` cover unresolved context denial, client scope override denial, permission-service invocation with server-resolved IDs, and allowed/denied permission results.

## Phase 4 closure checklist — existing API surface

Phase 4 hardening for the current PlatformHub API surface is being closed as one auditable change set. The repository currently contains platform/workspace/navigation APIs, not tenant-facing Inventory, Sales, Purchase, or Accounting business-data controllers. Those future APIs cannot be marked secured until they exist and have endpoint-level tests.

Completed controls in the current API:
- Resolve workspace headers only after checking that the authenticated user, tenant, branch, and project are active and structurally consistent.
- Reject inactive users even when a previously issued JWT and role assignment still exist.
- Reject tenantless project-only assignments; shared projects require an explicit tenant anchor and an enabled tenant subscription.
- Check the requested permission against current database-backed role assignments and the resolved workspace; menu visibility does not grant API access.
- Provide fail-closed EF query helpers and a reusable `RequireScopedPermission` authorization filter. These are opt-in protections and must be applied to each future business endpoint.
- Validate role-assignment writes against active tenant, branch, and project records; reject cross-tenant branch/project combinations and disabled shared-project subscriptions; keep the protected Platform Super Admin role global-only.
- Keep platform-wide management routes under the Platform Admin policy instead of forcing them through tenant workspace scope.

Release verification:
1. Run `dotnet test tests/PlatformHub.SecurityTests/PlatformHub.SecurityTests.csproj`.
2. Run `dotnet build` from the solution root.
3. Confirm the GitHub Actions build/security-test workflow succeeds for the Phase 4 pull request.
4. No database migration is expected for these authorization and validation changes.

Remaining release rule (not a current-code defect): when tenant-facing business controllers, exports, stored procedures, or background jobs are added, each must explicitly resolve tenant/workspace scope, enforce permission checks, apply scoped data predicates, validate related IDs on writes, and include cross-tenant read/write/delete regression tests before release. The current platform-only Dapper stored procedure is an intentionally platform-admin catalogue query; do not use it as a pattern for tenant business data.


## Tenant-scoped Inventory Catalog API

The first business-data vertical slice is the Inventory Item catalog, stored in the `dbo.Products` table. It is deliberately separate from platform administration data and uses the validated active workspace from `ActiveTenantContext`.

- Routes: `GET/POST /api/inventory/items`, `GET/PUT/DELETE /api/inventory/items/{id}`.
- Authorization uses database-backed `Inventory.Item.View`, `Inventory.Item.Create`, `Inventory.Item.Update`, and `Inventory.Item.Deactivate` permissions.
- Every query and write is constrained by the server-resolved tenant, branch, and project. Client request bodies cannot choose ownership IDs.
- Delete is a soft deactivation; duplicate SKU checks are workspace-scoped.
- The inventory catalog has its own `InventoryDbContext` and `__InventoryMigrationsHistory` table, sharing the configured `PlatformDb` SQL Server connection. Platform tables/migrations are not changed.

Apply the new catalog schema after deploying this version:

```powershell
dotnet ef database update --context InventoryDbContext --project src/PlatformHub.Infrastructure --startup-project src/PlatformHub.Api
```

A tenant user needs an active tenant/branch/project workspace selected through the existing workspace context middleware and an assigned role with the relevant Inventory permissions. The built-in Super Admin receives all seeded permissions; tenant and branch admin roles receive catalog permissions as part of the permission seeding. Custom roles must be granted permissions in Role & Permissions.

This is the first business-data slice, not a claim that Sales, Purchase, stock movements, accounting postings, reports, exports, or background jobs are already implemented. Each subsequent module must use the same explicit scope checks and add its own cross-tenant regression tests.
