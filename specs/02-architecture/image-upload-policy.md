# Image Upload and PostgreSQL Storage Policy — pcHouseStore Web

**Status:** DRAFT · Owner decision: all binary images must be stored in the same PostgreSQL database. No external object storage, file share, local filesystem or separate file-management service is used.

## 1. Supported image use cases and limits

| Scope | Max images per record/line | Max file size | Aggregate limit |
|---|---:|---:|---:|
| Service procurement request line | 10 | 10 MiB each | 100 MiB per request line |
| Refurbished physical unit | 10 | 10 MiB each | 100 MiB per unit |
| Customer complaint case | 10 | 10 MiB each | 100 MiB per case |

The limits are centralized in `MediaUploadPolicy` and enforced by both React and ASP.NET Core. API limits are authoritative. These are project defaults, not a universal market standard.

## 2. PostgreSQL physical storage

- Store each normalized binary image in `stored_image.content BYTEA` in the same PostgreSQL database as operational data.
- Store an optional compact thumbnail in `stored_image.thumbnail_content BYTEA`; the generated thumbnail must be at most 60 KiB.
- Enforce the 10 MiB image and 60 KiB thumbnail limits in the application and include their storage/backup impact in PostgreSQL capacity planning.
- A shared `stored_image` row contains binary content and technical metadata: `id`, `company_id`, `shop_id`, `content`, `thumbnail_content`, `content_type`, `size_bytes`, `width_px`, `height_px`, `sha256`, `uploaded_by_employee_id`, `authorization_id`, `uploaded_at`, `deleted_at`, `deleted_by_employee_id` and `delete_authorization_id`.
- Domain-specific relation tables keep strict foreign keys to the image and owning record: `service_procurement_request_image`, `refurbished_unit_image` and `customer_complaint_case_attachment`. They store the parent FK, `stored_image_id`, image role, caption and sort order. Do not use a polymorphic `owner_type/owner_id` reference.
- The `stored_image_id` is unique within each attachment table so one physical image attachment is not accidentally reused by multiple owners. Each image is owned by one Shop and one Company.
- ASP.NET Core streams binary content from PostgreSQL through an authorization-checked endpoint. Do not return a public URL, storage key or filesystem path; the client loads a scoped image endpoint after normal Shop/role validation.
- PostgreSQL is the sole source of truth for uploaded image bytes.

## 3. Lightweight image processing

1. Accept JPEG/JPG, PNG and WebP. HEIC/HEIF is converted to JPEG by a supported client or the user is asked to export as JPEG.
2. Reject SVG, animated GIF, archives, executable content and arbitrary documents through this endpoint.
3. Validate file signature/content, decode in a resource-limited process and re-encode before storing. Never trust the browser MIME header or extension.
4. Normalize photographic uploads to WebP where supported (quality target around 80–85); preserve PNG for images where transparency/line clarity requires it. The original untrusted byte stream is not stored by default.
5. Keep max input at 20 megapixels and 8,000 px on either side. Generate a 320 px thumbnail at WebP quality tuned to keep it <=60 KiB.
6. Strip EXIF metadata including GPS. Compute SHA-256 over the normalized full-size content.
7. Preserve enough resolution to read part numbers, model/serial labels and connectors; do not apply aggressive compression that makes technical evidence illegible.

## 4. Access, deletion and retention

- Upload, view and deletion require a valid ShopSession and an employee authorization permitted for that operation. Manager can access all records; Technician can upload/view service procurement and technical inspection evidence in their permitted workflow.
- Scope checks verify the attachment parent, `stored_image.company_id` and `stored_image.shop_id` all match the active context. Do not trust IDs supplied by React.
- Deletion is soft and audited. A previously used purchase/inspection/complaint image is not physically removed when a user removes it from a screen.
- Initial v1 behavior has no automatic physical purge. Image retention follows the parent business record. The Company must later approve a GDPR/data-retention schedule; implement any purge as a database-controlled background job, not manual external file handling.
- Database backups and restores include image BLOBs. Backup capacity, restore time and storage growth must be measured with representative image volume before production.

## 5. Performance and operations

- Use thumbnails for queue/list screens; fetch full-size image bytes only when previewing/downloading the image.
- Use EF Core projections that exclude BLOB columns in ordinary entity queries. Load BLOB only by explicit image endpoint/query.
- Do not eagerly include BLOB columns in service-order, procurement, unit or complaint lists.
- Use pagination for image galleries and set explicit response/cache headers. Private responses must not enter public/shared caches.
- Monitor PostgreSQL data size, redo/undo pressure, backup size and restore duration. Since the product owner chose PostgreSQL-only storage, growth management stays in the database/backup plan rather than a separate object-store account.

## 6. Security controls

Follow the OWASP File Upload Cheat Sheet:

- Allowlist only required image types; validate file signatures/content and dimensions.
- Enforce request and per-file size limits at proxy, API and application layers.
- Generate database IDs server-side; do not use the supplied filename as an identifier or path.
- Use maintained image-decoding libraries with memory/time limits and re-encode accepted images.
- Require authorization for every upload, preview and delete; log failures without logging image bytes or credentials.
- Store no public image URL and do not expose raw BLOB data outside the scoped endpoint.

OWASP source: [File Upload Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html).
