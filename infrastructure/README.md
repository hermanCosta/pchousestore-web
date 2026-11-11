# Infrastructure

Deployment and environment assets will be added during deployment planning. Runtime secrets and connection strings belong in environment configuration or an external secret store, not source control.

- Authentication persistence DDL is a review-only draft at [`database/authentication-foundation.draft.sql`](database/authentication-foundation.draft.sql). It assumes the reviewed Company/Shop/Employee core schema and must not be applied to a live database.
- EF Core authentication persistence records and mappings are under `src/PcHouseStore.Infrastructure/Persistence/`; migration generation remains gated on MySQL provider/concurrency validation and completion of core table mappings.
