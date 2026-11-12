# Infrastructure

Deployment and environment assets will be added during deployment planning. Runtime secrets and connection strings belong in environment configuration or an external secret store, not source control.

- PostgreSQL 16.11 is installed locally through Postgres.app. On macOS 12, start it manually:

  ```sh
  /Applications/Postgres.app/Contents/Versions/16/bin/pg_ctl \
    --pgdata="$HOME/Library/Application Support/Postgres/var-16" \
    --log="$HOME/Library/Application Support/Postgres/var-16/server.log" start
  ```

- The local development database is `pchousestore_dev`; EF Core migration `20261112005610_AuthenticationFoundationInitial` is applied. It creates the Company/Shop/Person/Employee identity foundation, authentication persistence and OpenIddict tables. No Company, Shop, Employee or contact data has been seeded. The local cluster uses peer authentication over `/tmp` and SCRAM for TCP connections. Keep connection strings in environment configuration, not source control. Example for the local Unix socket: `Host=/tmp;Port=5432;Database=pchousestore_dev;Username=<your-macOS-user>`.
- Authentication persistence SQL at [`database/authentication-foundation.draft.sql`](database/authentication-foundation.draft.sql) remains a review-only reference. Do not execute it directly; the local schema is managed by EF Core migrations.
- EF Core persistence records, Npgsql mappings and the applied migration are under `src/PcHouseStore.Infrastructure/Persistence/`. PostgreSQL concurrency/auth integration validation and the remaining domain tables are still outstanding; do not treat this foundation migration as the complete application schema or production approval.
