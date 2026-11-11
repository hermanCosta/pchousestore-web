# Service Parts Purchase Queue — Screen Specification

**Status:** DRAFT — product-owner requirement recorded 2026-11-05.  
**Module:** Supplier Procurement / Service Operations  
**Route proposal:** `/procurement/service-parts`  
**Detail route proposal:** `/procurement/service-parts/requests/{requestId}`

## 1. Objective

Provide a dedicated Shop-scoped screen to manage every part that must be purchased for a service order. The queue is independent from the service-order screen so buyers can review and process purchasing work across multiple jobs without opening each order one by one.

Every request must be traceable to a `ServiceOrderId`. A single ServiceOrder may generate multiple procurement requests at different moments during booking, diagnosis or repair. The screen retains each request round and its images/history rather than replacing an earlier request.

## 2. Main concepts

- `ServicePartRequirement` is the durable technical demand for a Product on a ServiceOrderItem.
- `ServiceProcurementRequest` is one purchasing work item for one ServiceOrder and one sourcing round.
- `ServiceProcurementRequestLine` states which part requirement and whole-unit quantity must be sourced in that round.
- `ServiceProcurementRequestImage` stores metadata for private reference images attached to a request line.
- `SupplierPurchase` is the actual external order placed with a retailer/supplier. It remains separate from the internal request and may consolidate request lines from different ServiceOrders in the same Shop.

## 3. Navigation and list screen

The queue is available from the Shop's main navigation under **Purchasing → Service Parts**. It always uses the active Shop from `ShopSession`; the browser must not choose or override `ShopId`.

### 3.1 Summary counters

Show counts for:

- Open / to source
- In progress / researching
- Ordered / awaiting delivery
- Partially received
- Overdue supplier purchases (derived from ETA)

Counters are Shop-scoped and must use the same filters as the list where appropriate.

### 3.2 Filters and search

- Request status (multi-select)
- ServiceOrder ID / request number (exact or partial)
- Customer name/contact identifier, subject to customer-data permissions
- Device type, brand and model
- Product name, SKU, supplier part number or description
- Priority
- Requested date range and needed-by date range
- Assigned buyer / employee
- Supplier and purchase status, where a supplier order already exists
- Toggle to include fulfilled and cancelled history

The ServiceOrder ID is a first-class search field and remains visible in every result row.

### 3.3 Queue columns

Display one row per request line, grouped by request and ServiceOrder:

| Column | Content |
|---|---|
| Request | Request number and creation date |
| Service | Clickable ServiceOrder ID and current service status |
| Customer / device | Brief context to help identify the job |
| Reference | Main image thumbnail with image-count indicator |
| Part | Catalogue product, SKU and supplier part number if known |
| Quantity | Requested, reserved, ordered, accepted/received and still outstanding |
| Priority | Low, normal, high or urgent |
| Needed by | Requested date and overdue/warning indicator when applicable |
| Purchasing status | Request status and linked supplier-purchase status |
| Buyer | Assigned employee, when assigned |
| Actions | Open, assign, update priority, add image or cancel (permission-gated) |

The UI must differentiate the **request status** from the **SupplierPurchase status**. For example, a request can be `PARTIALLY_ORDERED` while a supplier purchase is `ORDERED`.

## 4. Request creation

### 4.1 From a ServiceOrder

In the ServiceOrder details, add a **Request parts** action. It creates a new `ServiceProcurementRequest` sourcing round, prefilled with the current Shop and ServiceOrder ID. The employee can add one or more known `ServicePartRequirement` lines and attach images.

### 4.2 From the purchase queue

A **New request** action prompts the user to search for or enter the ServiceOrder ID. The backend validates that the order exists in the active Shop and is eligible for additional parts sourcing. The user then creates request lines linked to that order's part requirements.

The request number is sequential within the ServiceOrder (for example, `SO-1042 / PR-02`). Do not enforce one request per ServiceOrder. A request created after later diagnosis must remain separate from the earlier request.

## 5. Request detail view

The detail view contains:

1. **Service context:** ServiceOrder ID, status, customer/device summary, booking date and a link back to the complete order.
2. **Request metadata:** request number, created/requested date, needed-by date, priority, status, assigned buyer and notes.
3. **Requested parts:** one or more lines with catalogue product, required quantity, current reserved quantity, sourcing quantity, part number, compatibility notes and line status.
4. **Reference images:** thumbnail gallery, full-size preview, captions, image role and upload date/employee.
5. **Sourcing:** supplier/source, product-page URL, purchase/order URL, external order reference, order date, currency and unit-cost snapshot when available.
6. **Delivery tracking:** expected delivery date, carrier/tracking reference and URL, accepted/received quantity and supplier-purchase timeline.
7. **History:** append-only `ServiceProcurementRequestEvent` for creation, edits, reassignment, priority changes, image uploads/deletions and cancellation; supplier-order linkage, ETA changes and receipts appear in the linked `SupplierPurchaseEvent` timeline.

## 6. Image capture and storage

- Allow capture from a mobile device camera as well as file selection from desktop/mobile storage.
- Allow multiple images per request line. Suggested roles: `PART_OVERVIEW`, `MODEL_OR_SERIAL_LABEL`, `CONNECTOR`, `DAMAGE`, `OTHER`.
- Images should help identify compatibility: the removed/old part, printed part number, model label, connectors, dimensions and visible damage are common useful references.
- Store image bytes in the same MySQL database using the shared `stored_image.content MEDIUMBLOB` table and a bounded `thumbnail_content BLOB`. No object storage, filesystem or external file service is used.
- Validate content by file signature/MIME, enforce the 10 MiB limit and image-count limits, re-encode accepted images, strip EXIF/GPS and store a SHA-256 digest.
- Do not expose public URLs or storage keys. The client requests an opaque image ID through an authorization-checked ASP.NET Core endpoint. The endpoint verifies the attachment parent and active Shop before streaming thumbnail/full BLOB bytes.
- Deleting an image is a soft-delete/audited action recorded in `ServiceProcurementRequestEvent`; preserve history where the image was previously used for a purchase decision. A replacement image can be added without erasing the old one.

Initial limits are 10 MiB per image, 10 images and 100 MiB aggregate per target. The image bytes are stored in MySQL; soft-delete is audited and no automatic physical purge runs in v1. Exact GDPR retention schedule remains pending.

## 7. Actions and employee authentication

Supplier purchase creation, linking, updates and receipt do not require a second manager approval. This does not bypass the executing employee's role permission or per-operation password challenge. The persistent session identifies only the Shop. Every write action requires fresh employee authentication and a single-use action authorization, including create/edit/cancel request, upload/delete image, assignment, purchase linkage and receipt. The employee who created a request is not assumed to be the buyer or the employee who later receives the part.

Action codes use the approved v1 matrix in `employee-access-and-roles.md`: `PROCUREMENT_REQUEST_CREATE`, `PROCUREMENT_REQUEST_UPDATE`, `PROCUREMENT_REQUEST_CANCEL`, `PROCUREMENT_IMAGE_UPLOAD`, `PROCUREMENT_IMAGE_DELETE`, `PROCUREMENT_ASSIGN`, `SUPPLIER_PURCHASE_LINK`, `SUPPLIER_PURCHASE_RECEIVE`.

## 8. Status behavior

Request header status is an aggregate over its request lines and associated allocations:

- `DRAFT`: not yet published to the queue.
- `OPEN`: active quantity is waiting to be researched/ordered.
- `IN_PROGRESS`: a buyer is reviewing supplier options, compatibility or prices.
- `PARTIALLY_ORDERED`: some, but not all, requested units are linked to SupplierPurchaseLine allocations.
- `ORDERED`: all requested units are linked to external order lines; none has been accepted yet.
- `PARTIALLY_RECEIVED`: at least one accepted unit has arrived but some requested quantity remains outstanding.
- `FULFILLED`: all active quantities were accepted and reserved for the relevant ServiceOrder requirement.
- `CANCELLED`: this sourcing round is closed. The underlying service requirement remains active unless explicitly cancelled on the ServiceOrder.

Request status is not manually editable to arbitrary values. The command layer derives allowed status changes from line allocations, accepted receipts and cancellation events. ETA overdue is a derived indicator on the supplier purchase, not a request status.

## 9. Business rules

1. Each request header belongs to one ServiceOrder and one Shop; all request lines and images inherit that scope.
2. A ServiceOrder may have multiple request headers, including requests created after the order has been `IN_PROGRESS` or after an earlier request has been fulfilled/cancelled.
3. Each request line refers to a `ServicePartRequirement` in the same ServiceOrder and Shop. Only Product catalogue items are procurable through this workflow.
4. Quantities are positive whole units. A request line records the shortage being sourced in that round, not the full service requirement by default.
5. Active requests and outstanding supplier allocations must be checked together under concurrency control so the same shortage is not requested twice.
6. A request line may be associated with multiple supplier purchase lines (split sourcing); a supplier purchase line may be allocated to multiple request lines (consolidated purchasing), provided Shop and product match.
7. Receiving is recorded against the supplier purchase line and allocated request lines. Only accepted physical units create stock movements. Allocated accepted units are reserved atomically for the corresponding service orders.
8. A newly created request does not by itself change the ServiceOrder status; `AWAITING_PARTS` is driven by blocking ServicePartRequirement reservation state.
9. Cancelling a request closes that sourcing round but does not cancel the technical requirement or delete prior purchases/images/events. Any unreceived purchase allocation must be explicitly released/reallocated.
10. All mutations and image operations are attributed to the employee who authenticated that operation.

## 10. Acceptance criteria

- A user can open the queue and see only requests from the active Shop.
- Searching by ServiceOrder ID returns all request rounds for that service, with independent statuses and images.
- A ServiceOrder can have a first request for a battery and a later second request for a keyboard, without losing the first request.
- A new request can be created from the ServiceOrder screen or by selecting ServiceOrder ID in the queue.
- A request line can contain several images, which can be captured by mobile camera or selected from disk.
- The buyer can identify the requested part from its thumbnail, full-size images, part number and compatibility notes.
- Each image has Shop-scoped access; a different Shop cannot read it by guessing the request/image ID or storage key.
- Buyer can assign a request, change priority and link it to a new or existing SupplierPurchase, with each operation attributed to the employee who authorized it.
- A consolidated SupplierPurchase can fulfil request lines belonging to several ServiceOrders in the same Shop.
- Partial receipts update accepted quantities, stock and reservations once; queue statuses are recalculated correctly.
- Cancelling one procurement request does not cancel another request or erase the underlying ServicePartRequirement.
