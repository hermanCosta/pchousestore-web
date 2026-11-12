# PostgreSQL Image Storage Architecture

**Decision:** Uploaded images are stored in the same PostgreSQL 16 database as the pcHouseStore operational data. No external object store, NAS/file share, local filesystem path or separately managed file service is part of the initial architecture.

## Schema

### `stored_image`

- `id BIGINT` primary key
- `company_id BIGINT` and `shop_id BIGINT` tenant/context ownership
- `content BYTEA NOT NULL` — normalized full-size image bytes (max application payload 10 MiB)
- `thumbnail_content BYTEA NULL` — thumbnail <=60 KiB
- `content_type VARCHAR(40)` — normalized MIME type
- `size_bytes INTEGER`, `width_px SMALLINT`, `height_px SMALLINT` with positive-value checks
- `sha256 BYTEA` with a 32-byte length check
- `uploaded_by_employee_id BIGINT`, `authorization_id BIGINT`
- `uploaded_at TIMESTAMPTZ`, `deleted_at TIMESTAMPTZ NULL`, `deleted_by_employee_id BIGINT NULL`, `delete_authorization_id BIGINT NULL`

The 10 MiB full-image and 60 KiB thumbnail limits are application-level constraints. Database and backup capacity must be tested with representative image volume before production.

### Owner-specific attachment tables

Each attachment has a direct foreign key to its owning record and to `stored_image`:

- `service_procurement_request_image(service_procurement_request_line_id, stored_image_id, image_role, caption, sort_order)`
- `refurbished_unit_image(refurbished_unit_id, stored_image_id, image_role, caption, sort_order)`
- `customer_complaint_case_attachment(complaint_case_id, stored_image_id, image_role, caption, sort_order)`

Do not implement a polymorphic `owner_type + owner_id` association. Attachment parent and `stored_image` must have matching `CompanyId` and `ShopId`, enforced by composite keys/foreign keys where practical and rechecked in the command handler.

## Upload and delivery flow

1. React sends a file to an ASP.NET Core upload endpoint under the current ShopSession.
2. The API checks the target entity's Company/Shop, employee role and fresh employee password authorization.
3. The API enforces the 10 MiB limit, validates signature and MIME, decodes in a resource-limited process, removes EXIF/GPS and normalizes/re-encodes the image.
4. The API creates `stored_image` and the owner-specific attachment row in one DB transaction. A SHA-256 hash is stored for integrity/deduplication analysis; do not deduplicate across shops.
5. The upload response returns an opaque image ID, not a storage key or public URL.
6. The UI loads the thumbnail or full image from an API endpoint. The endpoint validates active Shop access and role before streaming the relevant BLOB.

## Performance controls

- Default list/query projections never select `content` or `thumbnail_content` unless the endpoint specifically requests the thumbnail.
- Queue/list screens select thumbnails only; full image is streamed only when a preview is opened.
- Use `AsNoTracking`/projection for read paths and avoid eager-loading BLOB columns in EF Core aggregates.
- Use explicit pagination for galleries.
- Normalize uploaded photos to WebP quality 80–85 where appropriate; keep text/transparent images in PNG where needed. Preserve readability of labels/serial numbers.
- Keep the current 10 images / 100 MiB aggregate limit per target, subject to future operational evidence.

## Backup and retention

Database backups include the image BLOB data. Capacity planning and restore-time testing must include image volume. Soft-delete is used; no automatic physical purge runs in v1. Retention follows the parent record until the Company adopts a GDPR/data-retention schedule. Any later purge is a database job with an audit record, not external file cleanup.

## Trade-offs accepted by the owner

- PostgreSQL backup size and database storage increase as images grow.
- Image reads are isolated behind streaming endpoints and thumbnail queries to avoid loading binary columns into ordinary API responses.
- This keeps image ownership, Shop authorization, backups and lifecycle in one system, avoiding a second storage provider or manual file administration.
