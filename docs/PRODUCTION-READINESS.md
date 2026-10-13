# PlatformHub production deployment and monitoring checklist

This checklist is an operational gate, not a claim that any environment has already passed production verification.

## Before deployment

- Deploy the API and MVC applications over HTTPS. Keep HSTS enabled outside Development.
- Configure secrets in the hosting platform's secret store (never in committed appsettings): `ConnectionStrings__PlatformDb`, `Jwt__SigningKey` (at least 32 random UTF-8 bytes), `Jwt__Issuer`, `Jwt__Audience`, and any bootstrap/seed secrets needed for provisioning.
- Use separate signing keys and database credentials per environment. Restrict the database identity to the required schema operations and runtime CRUD.
- Configure trusted reverse-proxy forwarding only for known proxies/networks before relying on client IP rate limits. Do not blindly trust arbitrary forwarded headers.
- Review the SQL migration, take a database backup, and verify a restore procedure. Apply reviewed migrations as a deployment step before switching application traffic; do not rely on an implicit startup migration.
- Confirm the new `dbo.RefreshSessions` migration is applied before deploying the version that requires the table.
- Confirm the first platform administrator is provisioned through the private production process; the development bootstrap endpoint is unavailable outside Development.
- Ensure the deployment platform has persistent/shared storage for ASP.NET Data Protection keys when running multiple MVC instances. Otherwise cookie authentication tickets may not be readable across instances.
- If running multiple MVC instances, use shared distributed session/coordination for refresh operations and review concurrency behavior. Do not use an in-memory lock as a cross-instance lock.

## Health probes

- `GET /health/live`: process liveness only; returns HTTP 200 when the API pipeline is running.
- `GET /health/ready`: checks SQL Server connectivity; returns HTTP 200 when ready and HTTP 503 when the database is unavailable. The response intentionally does not expose connection details.
- Configure the hosting platform to use liveness for restart decisions and readiness for traffic routing. Alert if readiness fails repeatedly.

## Monitoring and alerts

Collect structured application logs and retain the correlation ID returned in `X-Correlation-ID`. Never log passwords, access tokens, refresh tokens, connection strings, or raw authentication payloads.

Create alerts for:
- repeated HTTP 5xx responses or unhandled exceptions;
- readiness failures, database connection failures, and rising request latency;
- spikes in HTTP 401/403/429 responses;
- refresh-token reuse detection (possible token theft) and unusual login lockout rates;
- migration/deployment failure and unexpected restart loops.

Set alert thresholds against a measured baseline and assign an on-call owner. Test that alerts actually reach the owner before release.

## Authentication lifecycle smoke test

1. Sign in and verify that the response includes a short-lived access token and a refresh token.
2. Call `POST /api/v1/auth/refresh` with the refresh token. Verify the response contains a new pair.
3. Verify the old refresh token cannot be used again. Reuse detection revokes active sessions for that account; sign in again to recover.
4. Call `POST /api/v1/auth/logout` with the current refresh token. Verify the current access token is rejected on protected API routes.
5. Deactivate a user and verify its access token is rejected immediately by API session validation.
6. Verify refresh sessions expire after 14 days and that the MVC logout button revokes its current refresh token.

## Rollback and recovery

- Keep the previous application package available until smoke tests pass.
- Prefer forward-fix for additive schema changes. If rolling back application code, preserve the `RefreshSessions` table until no deployed version depends on it.
- Restore a database backup only under the approved recovery procedure; never delete migration history to force an application startup.
- After a suspected token-signing-key exposure, rotate the key through a coordinated deployment and force users to sign in again.
