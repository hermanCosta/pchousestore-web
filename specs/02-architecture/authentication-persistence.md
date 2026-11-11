# Authentication and Signing-Key Persistence — DATA-05 Slice

**Status:** APPROVED FOR IMPLEMENTATION — physical design and initial EF Core mappings validated 2026-11-10; provider-specific validation remains
**Scope:** DATA-05 authentication tables only. This is a design artifact; do not apply it to a live database before review and migration generation.

The initial EF Core persistence records/configurations compile as part of the solution. `CompanyRecord`, `ShopRecord` and `EmployeeRecord` are currently minimal auth-context projections; merge them with the complete core-table mappings before generating any migration.

## Decisions carried in

- One Company owns multiple Shops. `ShopSession` represents one active Shop and contains no Employee identity.
- A protected business command consumes one short-lived `EmployeeActionAuthorization` atomically with its business write.
- Shop credential bootstrap and no-Manager recovery use the audited deployment-controlled process. Normal credential administration is Manager-authorized.
- Failed login/challenge attempts never lock or disable a credential.
- OpenIddict owns its application/authorization/token schema. Application rows correlate ShopSession to OpenIddict authorization identifiers but do not depend on undocumented provider columns.
- Product owner requested the signing-key registry table name `system_services`.

## Proposed physical tables

### `shop_access_credential`

One row per Shop; login identifier remains `shop.code`.

| Column | Type | Constraints / purpose |
|---|---|---|
| `shop_id` | `BIGINT` | PK and FK to `shop.id` |
| `password_hash` | `VARCHAR(512)` | Required adaptive hash; never reversible or plaintext |
| `credential_version` | `BIGINT` | Increment for every reset/rotation; optimistic concurrency token |
| `is_active` | `BOOLEAN` | Required; false disables sign-in |
| `created_at` | `DATETIME(6)` | UTC |
| `rotated_at` | `DATETIME(6)` | Nullable UTC |
| `updated_at` | `DATETIME(6)` | UTC |
| `updated_by_employee_id` | `BIGINT` | Nullable for bootstrap; FK to Employee |

`company_id` is repeated here only to enforce the composite Shop/Company FK. Credential changes increment `credential_version`. A successful rotation/reset revokes active ShopSessions for that Shop in the same database transaction. Initial provisioning is performed by the audited bootstrap process.

`shop_access_credential_event` is append-only and records provision/rotation/reset/enable/disable, credential version, timestamp, actor type, employee actor where applicable and a required reason. It must never contain old/new password values or hashes.

### `shop_session`

| Column | Type | Constraints / purpose |
|---|---|---|
| `id` | `BIGINT` | PK; internal session identifier used as server-issued `shop_session_id` |
| `company_id` | `BIGINT` | Required tenant key |
| `shop_id` | `BIGINT` | Required; composite FK `(shop_id, company_id)` to `shop` |
| `oidc_authorization_id` | `VARCHAR(100)` | Nullable provider correlation ID; no dependency on provider table layout |
| `issued_at` | `DATETIME(6)` | Required UTC |
| `last_seen_at` | `DATETIME(6)` | Nullable UTC; update policy configurable |
| `idle_expires_at` | `DATETIME(6)` | Nullable/configurable until pre-production policy is approved |
| `absolute_expires_at` | `DATETIME(6)` | Nullable/configurable until pre-production policy is approved |
| `revoked_at` | `DATETIME(6)` | Nullable UTC; revocation is terminal |
| `revocation_reason` | `VARCHAR(80)` | Nullable, controlled reason code |
| `row_version` | `BIGINT` | Required concurrency token |

Indexes: `UNIQUE(id, shop_id)` for composite authorization FKs; `(shop_id, revoked_at, absolute_expires_at)` for active-session operations; unique nullable `oidc_authorization_id` if OpenIddict guarantees one session per authorization.

No single-active-session-per-Shop constraint is used. Idle/absolute expiry values and simultaneous-device policy remain configurable/deferred.

### `employee_action_authorization`

| Column | Type | Constraints / purpose |
|---|---|---|
| `id` | `BIGINT` | PK |
| `authorization_token_hash` | `BINARY(32)` | Required unique SHA-256 hash of a random 256-bit opaque authorization token; never store the raw token |
| `shop_session_id` | `BIGINT` | Required; ShopSession FK with Shop scope |
| `company_id` | `BIGINT` | Required tenant key |
| `shop_id` | `BIGINT` | Required active Shop; FK `(shop_id,company_id)` to `shop` |
| `target_shop_id` | `BIGINT` | Nullable resource Shop for Company-wide Manager actions; if present, must belong to the same Company |
| `employee_id` | `BIGINT` | Required; composite FK `(employee_id,company_id)` to `employee` |
| `action_code` | `VARCHAR(100)` | Required stable action identifier |
| `resource_type` | `VARCHAR(80)` | Nullable target type |
| `resource_id` | `VARCHAR(100)` | Nullable target identifier |
| `operation_context_hash` | `BINARY(32)` | Nullable SHA-256 of server-canonicalized command context, where the feature defines one |
| `authenticated_at` | `DATETIME(6)` | Required UTC |
| `expires_at` | `DATETIME(6)` | Required UTC; default authorization lifetime 2 minutes, configurable |
| `consumed_at` | `DATETIME(6)` | Nullable UTC; one-use state |
| `row_version` | `BIGINT` | Required concurrency token |

Indexes: unique `authorization_token_hash`; `(shop_session_id, shop_id, consumed_at, expires_at)`; `(employee_id, authenticated_at)` for audit. Keep consumed records for audit; purge policy is a separate retention decision.

Atomic consumption is a conditional update inside the same transaction as the protected command:

```sql
UPDATE employee_action_authorization
SET consumed_at = UTC_TIMESTAMP(6), row_version = row_version + 1
WHERE authorization_token_hash = @token_hash
  AND shop_session_id = @session_id
  AND shop_id = @active_shop_id
  AND action_code = @action_code
  AND consumed_at IS NULL
  AND expires_at > UTC_TIMESTAMP(6);
```

Exactly one affected row is required. The transaction also locks/revalidates the active `shop_session` row before consuming the authorization. A concurrent second command then observes the consumed state; if the business write fails, transaction rollback leaves the authorization unconsumed.

### `system_services` — signing-key registry

One row per OIDC signing-certificate version. Token encryption keys are not part of this v1 slice. The table is a registry and encrypted persistence mechanism; it is **not** a vault for plaintext private keys.

| Column | Type | Constraints / purpose |
|---|---|---|
| `id` | `BIGINT` | PK |
| `service_name` | `VARCHAR(80)` | Required; e.g. `oidc-signing` |
| `key_id` | `VARCHAR(100)` | Required public `kid`; unique with `service_name` |
| `key_purpose` | `VARCHAR(24)` | `SIGNING` or `ENCRYPTION` |
| `algorithm` | `VARCHAR(24)` | Allowlisted algorithm, e.g. `RS256` |
| `public_certificate_der` | `MEDIUMBLOB` | Required public X.509 certificate only |
| `private_key_ciphertext` | `MEDIUMBLOB` | Nullable for public-only retired keys; encrypted PKCS#8 private key for active/candidate keys |
| `cipher_nonce` | `BINARY(12)` | Required when private key ciphertext is present; AES-256-GCM nonce |
| `cipher_tag` | `BINARY(16)` | Required when private key ciphertext is present; AES-256-GCM authentication tag |
| `kek_reference` | `VARCHAR(255)` | Required when ciphertext is present; reference to KEK in environment secret manager/KMS, never the KEK itself |
| `status` | `VARCHAR(20)` | `STAGED`, `ACTIVE`, `VERIFY_ONLY`, `REVOKED` |
| `not_before` | `DATETIME(6)` | Required UTC |
| `not_after` | `DATETIME(6)` | Required UTC |
| `created_at` | `DATETIME(6)` | Required UTC |
| `activated_at` | `DATETIME(6)` | Nullable UTC |
| `retired_at` | `DATETIME(6)` | Nullable UTC |

Constraints: `UNIQUE(service_name,key_id)`; a generated nullable `active_signing_slot` and unique index enforce at most one `ACTIVE` signing key per service; `not_after > not_before`; encrypted private key, nonce, tag and KEK reference are either all present or all absent. A transaction activates the replacement and demotes the previous key to `VERIFY_ONLY`; old signing certificates remain available during overlap. Key lifecycle changes create append-only audit events without secret material.

The KEK must be held outside MySQL in a deployment secret manager/KMS or protected host certificate store and must not be backed up beside the database. The application decrypts the private key only at startup/rotation, keeps it in process memory only as required by OpenIddict, and never logs it. If the deployment cannot provide an external KEK, do not put private key material in this table; use a platform certificate/key store instead.

Rotation: stage and validate a new key, activate it with a recorded event, publish its public certificate/JWKS, retain the old public key as `VERIFY_ONLY` until the maximum access-token lifetime plus clock-skew allowance has elapsed, then remove its private ciphertext and retain public audit metadata. Emergency compromise revokes the key immediately and invalidates affected ShopSessions/authorizations as required.

## Concurrency and revocation protocol

1. Every protected mutation begins a MySQL/InnoDB transaction and re-reads/locks its ShopSession row (`SELECT ... FOR UPDATE` or provider-equivalent) to confirm it remains active.
2. It conditionally consumes the authorization token using the atomic update above.
3. It writes the business change and audit event, then commits once.
4. Logout/credential rotation locks the same ShopSession rows before marking them revoked. Once logout/rotation returns successfully, no later command can pass the transactional active-session check.
5. API bearer-token validation checks session status on every request (or uses a cache with a bounded invalidation strategy that preserves immediate revocation). Protected writes always recheck inside the transaction.

## Open implementation choices for technical review

- Confirm MySQL provider support/translation for row locking and `UPDATE ... WHERE ...` concurrency behavior; otherwise use parameterized SQL in the Infrastructure repository.
- Confirm OpenIddict authorization IDs fit the selected column length and are stable correlation identifiers.
- Confirm deployment KMS/secret-manager integration and recovery/backup procedure before production. Do not commit KEKs or key material.
- Set session idle/absolute lifetime and concurrent-device policy before production; keep configuration external.
- Apply the approved auth request-rate limit: 10 requests/minute per source IP and endpoint, success or failure, returning HTTP 429 with `Retry-After`. Trust forwarded client IP headers only from explicitly configured reverse proxies/networks.

## References

- [OpenIddict signing/encryption credentials](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials) — asymmetric X.509 signing credentials and multiple keys for rotation.
- [OWASP Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html) — envelope encryption and keeping KEKs separate from encrypted secrets.
- [OWASP Key Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Key_Management_Cheat_Sheet.html) — never store keys in plaintext; use dedicated key management where possible.
- Initial EF Core persistence records/configurations: `src/PcHouseStore.Infrastructure/Persistence/Entities/AuthenticationEntities.cs` and `src/PcHouseStore.Infrastructure/Persistence/Configurations/AuthenticationConfigurations.cs`.
