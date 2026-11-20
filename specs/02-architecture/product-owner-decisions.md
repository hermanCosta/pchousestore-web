# Product-owner Decisions and Remaining Questions — pcHouseStore Web

**Revision:** v49 · 20 November 2026
**Status:** Confirmed product decisions are incorporated. A small set of commercial/legal details remains explicitly proposed or deferred; the model is not a production-approved DDL.

## Confirmed decisions

| Area | Confirmed rule |
|---|---|
| Organization | One legal Company with multiple operational Shops/branches. |
| Database platform | PostgreSQL is selected; use EF Core with the Npgsql provider. This supersedes the earlier MySQL decision. |
| Shop printable identity | Each Shop has optional branch-specific address and phone fields for print reports/documents. Snapshot their values when a document is issued; populate only after the owner confirms the authoritative branch details. |
| Shop access | Persistent session represents the active Shop only; it has no Employee identity. |
| Active profile and application header | The persistent header shows the owner-supplied PC House logo with the wordmark `PC House` and displays the address of the authenticated Shop in place of a search field, sourced only from `ShopSession.shopAddress`. The hamburger Settings menu retains Shop profile and Shop access credential; Shop profile shows the full Shop name, address, phone and Company email where available. No persistent Employee identity is displayed. Employee registration details remain staff-administration data only. |
| Employee registration profile | Employee registration requires name, email and profile (`EMPLOYEE`, `MANAGER` or `ADMIN`). Other employee data is optional. Employee email is unique case-insensitively within the Company; duplicate names are allowed. |
| Employee administration | `MANAGER` and `ADMIN` administer employee accounts according to `specs/04-features/employee-management.md`. Use deactivation rather than hard deletion. The immutable account identifier is not editable. |
| Shop sign-in identifier | Stable Shop code + Shop credential password. |
| Employee identity | One Company-wide employee identity works in every Shop. Each feature-defined protected operation requires fresh direct backend validation of the acting employee's own password; no employee authorization token is issued or reused. |
| Customer/catalogue operations | Customer, Product, Service, product-category and Fault create/edit/deactivate/reactivate operations require an active Shop session and Company scope, but no individual employee credential challenge. Attribute their audit events to the authenticated ShopSession. |
| Customer duplicates and phone entry | A Company must not contain duplicate customer records for the same phone number. A match is a hard block on create, not a warning or automatic merge. Phone entry defaults to Ireland; an “International number” checkbox switches the text field to international input. Store/compare the canonical phone in E.164 format. A customer's phone remains reserved while its soft-deleted record exists. |
| Customer directory and email | TECHNICIAN may read the Company customer directory. Customer email is optional and may repeat; do not enforce a unique email constraint. |
| Product/Service kind and SKU | Catalogue items are only `PRODUCT` or `SERVICE`; remove legacy `OTHER` from the target UI/model. SKU is optional. When present it must match `SKU-[A-Z0-9]{8}`, is normalized to uppercase, unique per Company and remains reserved after deactivation. |
| Fault catalogue | Fault description remains the primary value used to display/select/search faults. Fault records also include an immutable Company-unique code generated automatically as `FLT-` plus a Company sequence, zero-padded to at least 6 digits (for example `FLT-000001`). Codes are never reused. |
| Catalogue stock minimum | Minimum stock is configured per Shop in `ShopInventory`; low-stock reporting compares on-hand quantity to that Shop's configured minimum, replacing the legacy fixed threshold. |
| Catalogue deactivation and legacy OTHER | Deactivated customers/catalogue entries remain visible in existing histories but are unavailable for new operations. Existing drafts may display existing references but may not add another reference to an inactive record. MANAGER and TECHNICIAN may reactivate records. Legacy `OTHER` catalogue records are excluded from the target catalogue and are not migrated. |
| Roles | Active profiles are `EMPLOYEE`, `MANAGER` and `ADMIN`. All profiles access every Shop in the Company. `MANAGER` administers employee/manager accounts but cannot grant `ADMIN`; `ADMIN` has full Company-wide administrative authority. No dual-role assignment. Business-operation permission matrices remain in their feature SPECs. |
| Authentication credential operations | `MANAGER` and `ADMIN` provision, disable and reset employee credentials using the existing `EmployeeCredential` and reset flow. Employee reset forces a password change before protected operations. Employee recovery is administrative only. Shop credential bootstrap remains governed by the Shop authentication process. |
| Employee initial credential | Generate `<normalized-first-name>@pchouse` for creation/reset. Normalize by removing accents, spaces and special characters. Display once to the authorized Manager/Admin; require change before protected operations. The pattern is temporary only and must be reconciled with the permanent-password minimum-length policy before implementation. |
| Employee password policy | No arbitrary periodic expiry. Creation/reset credentials must be changed before protected operations. Self-service password change requires the current password. No dedicated employee-administration audit log is introduced. Never log passwords or hashes. |
| Employee administration audit | Do not write employee-administration actions to business `notes` and do not introduce a dedicated employee-administration audit log. General application logs remain subject to existing redaction rules. |
| Last active administrator/manager | A Company must retain at least one active `ADMIN` and at least one active user with management authority (`MANAGER` or `ADMIN`). Reject profile/status changes that violate either invariant. |
| Employee removal | Use deactivation rather than hard deletion; preserve historical employee references. `MANAGER`/`ADMIN` deactivation permissions and invariants are defined in Employee Management. |
| Employee password baseline and failed attempts | Minimum 6 characters; support at least 128 characters and password managers/paste; no arbitrary periodic expiry; rotate after compromise or authorized reset; use vetted adaptive hashing. Failed attempts never lock or disable an account or trigger credential-specific waiting/rejection windows. Limit authentication request rates by source/network and endpoint. |
| Authentication request-rate limit | Maximum 10 requests per minute per source IP and authentication endpoint, counting successes and failures. Excess requests receive HTTP 429 through the end of that one-minute window; no credential lockout. |
| Inventory | Only Product catalogue items have stock. Product quantities are whole units. Quantity-tracked stock may become negative; reservations cannot. One stock pool per Shop; no reservation-expiry automation or multiple internal locations in v1. Minimum quantity is configured per Shop and drives its low-stock reporting. |
| Physical stock returns | Receive selected returned goods as `PENDING_INSPECTION`, outside sellable stock. Only an explicit inspection disposition `RESTOCK` creates an inbound movement; `QUARANTINE` and `DISPOSE` do not increase sellable stock. A serialized refurbished return remains `QUARANTINED` until inspection passes and it is explicitly restocked. |
| Inter-Shop transfer scope | Transfers support physical Product stock and service orders sent to another Shop. On service-order transfer, the destination Shop assumes operational and financial responsibility; all transactions from the transfer onward run through that destination. Product movement remains dispatch/in-transit/receipt. Previously settled transactions remain attributed to the source; outstanding balance and held/unapplied deposit liability transfer to the destination through an audited internal event. |
| Product transfer discrepancies | Receive only verified, acceptable quantities up to the dispatched quantity. Short/damaged units remain outstanding and keep the transfer open; overage is excluded from stock and needs a separate transfer/document. Log discrepancies with reason and authorization. No automatic loss write-off or closure. |
| Stock deduction | Sale and ServiceOrder product stock is consumed when the outstanding balance reaches zero through settled payments and explicitly applied service deposits. Partial payment does not consume stock. Service parts stay reserved at `FIXED` until full settlement; payment consumes them before `PICKED`. |
| Payment transaction | One `PaymentTransaction` may have multiple `PaymentAllocation` records; mixed cash/card is one financial transaction, not duplicate gross sales. |
| Tender methods | Initial release supports `CASH` and `CARD` only. Card is recorded after approval on an external card terminal; no payment-processor integration is selected or required for v1. |
| Processor terminology | A payment processor/acquirer is the provider behind a card terminal that authorizes/processes card payments and later settles funds. It is not a payment method. Provider choice/integration is deferred. |
| Refunds | Partial and full refunds are supported. Every refund is Manager-only (one Manager authorization; no second Manager approver). Default is return to the original tender; mixed-tender refunds are bounded by the remaining refundable amount of each original allocation. |
| Deposits | Service deposits are recorded at booking, have a written terms snapshot, no automatic expiry and no automatic forfeiture. After 90 days with no customer response, flag for Manager review/contact only. Cancellation/forfeiture rules are in `financial-model.md`. |
| Instalments | Weekly refurbished-sale plans remain in scope, with `LAYAWAY` as the initial mode. Missed-payment fees, collections and renegotiation are out of initial scope; overdue is shown as a status only. Early handover/credit-sale remains disabled pending Irish legal review. |
| Currency | EUR, `decimal`/`DECIMAL(19,2)`, `Europe/Dublin`; optional €0.05 cash-only rounding per Shop. |
| VAT | VAT configuration is preimplemented and Company-level. `vat_enabled=false` initially; setup remains `NOT_REVIEWED`. Rates/tax codes are configurable/effective-dated but no rate or tax treatment is assumed. Owner will confirm legal status with an Irish accountant later. This flag is not a legal exemption. |
| Refurbished grading | Cosmetic grades A (Excellent), B (Very Good), C (Good); functional test is separate and must pass for ordinary sale. |
| Refurbished identity | Every physical refurbished unit has a Company-unique internal `asset_code`. Capture manufacturer serial when present; serial is not required if manufacturer did not provide one. Serial uniqueness is Company-wide when populated, with normalized manufacturer + serial values. |
| Refurbished guarantee | 12-month commercial guarantee begins at actual documented customer handover. Proposed coverage/exclusions are documented separately and remain subject to owner/legal review; statutory rights are not limited. |
| Complaints | `CustomerComplaintCase` tracks product/service claims, evidence, inspection, manager resolution and remedy history. The 12-month commercial guarantee must not auto-reject statutory-rights claims. |
| Service parts visibility | A Shop-scoped Parts to Buy list presents automatically calculated uncovered ServiceOrder part requirements and manually added reminders as visibly distinct sources. Keep their quantities and totals separate; manual entries must never be included in calculated ServiceOrder demand totals. Sort both sources by creation date descending (newest first): manual entries by entry creation time and calculated Product groups by the newest contributing ServiceOrder creation time, with contributing orders newest first. Provide one search across both sources by Product name or manual free-text description; update results automatically as the user types, without requiring Enter or a separate submit action. Search is case-insensitive and supports partial matches within Product names and free-text descriptions (for example, `mem` matches `Memória RAM`). Preserve source labels and separate quantities in the results. Manual entries may select an existing Product or use a free-text description for a part not yet catalogued, and may switch between these modes during editing; quantity defaults to `1` and remains editable as a positive whole number. Existing Products display multiple external reference links maintained centrally in the Product catalogue; free-text entries may store multiple links directly on the entry. Manual entries can be edited after creation, including changing the Product reference or switching between Product-backed and free-text modes, and removed from the active list without affecting ServiceOrder data or history. Removed manual entries are permanently deleted; no removal history or restoration feature is required. Edits are audited with timestamp and employee attribution. Links are informational only. No supplier, purchase-order, external-purchase, delivery-tracking or supplier-receipt workflow is included; stock replenishment remains in the Stock module. |
| Manual Parts to Buy maintenance | `EMPLOYEE`, `MANAGER` and `ADMIN` may add, edit and remove manual entries in their active Shop. Each action requires the standard fresh employee code/password challenge and backend authorization; the Shop is derived from ShopSession. Removal permanently deletes the manual entry and its entry-owned reference URLs. No removal history or restoration feature is required; the delete action remains protected by the standard fresh employee code/password challenge. |
| Images | Multiple images are uploaded and stored inside the same PostgreSQL database. No external object storage, file share or filesystem path is used. Binary storage uses `BYTEA`. Initial image limits remain 10 MiB/file, 10 images and 100 MiB aggregate per target. |
| Cash closing | One `DailyClosing` per Shop/business date. Expected cash is opening float + settled cash receipts net of change + cash-in − cash-out − cash refunds − cash deposit refunds − safe drops. Non-zero variance requires a reason; close is immutable, cannot be reopened, and is corrected only through separate Manager-authorized records. |

## Financial policies incorporated as recommended v1 defaults

These policies turn the owner's request to use a standard, controlled approach into an implementable first-release baseline. Deposit forfeiture and commercial warranty wording must still be reviewed against the Company's actual terms and applicable Irish law before launch.

### Refund policy

- Refund may be partial or full, but cannot exceed the original settled amount less all prior settled refunds.
- Every refund requires a fresh `MANAGER` employee authorization, regardless of amount. No second Manager approval is required.
- Refunds default to the original tender. For a split payment, each refund allocation must not exceed the refundable balance of its corresponding original allocation.
- A card refund is processed on the external terminal/provider against the original card transaction; the application records the result and external reference. Do not store PAN, CVV or card credentials.
- An alternative refund tender is disabled in v1. Exceptional cases are handled manually outside the automated refund flow and require documented Manager review before being recorded.
- Refund and physical stock return remain separate operations. Only selected returned products/units are restocked after a return/disposition record.

### Deposit validity, cancellation and forfeiture

- A deposit remains active while its ServiceOrder/booking remains active. `valid_until` is nullable and no automatic expiry is applied.
- If there has been no customer response for 90 days, the system flags `REVIEW_REQUIRED` and prompts the Shop to contact the customer. This is an operational reminder, not a legal expiry date and not an automatic cancellation/forfeiture.
- Before taking a deposit, record the service/part purpose, amount, expected fulfilment date, refund/cancellation terms and the customer's acknowledgement.
- If the Company cannot provide the agreed service/item, or the agreed delivery date is missed and no replacement date is agreed, the unconsumed deposit balance is refundable, subject to applicable consumer rights.
- If the customer changes their mind, apply the terms disclosed and accepted before payment. A deposit may be retained only where the contractual term is clear, fair and legally valid. Do not automatically forfeit the entire deposit.
- For special-order parts, any non-refundable amount must be disclosed and accepted in advance and must be supported by the actual, documented, non-recoverable supplier cost. Manager review is required; no hidden or invented cancellation fee is allowed.
- A `SECURITY` deposit is returned when its stated purpose is satisfied, less only documented deductions accepted in the terms and permitted by law.
- `FORFEITED` is never automatic. It requires a Manager authorization, reason, terms snapshot and evidence/cost reference. The application must support full refund, partial refund, partial application and remaining held balance.
- CCPC guidance says deposits form part of a contract; businesses should disclose refundability/cancellation charges in writing. A business generally does not have to refund merely because a customer changes their mind, but a refund may be due where the business cannot supply what was agreed. See the official [CCPC deposits guidance](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/deposits/).

### Instalment arrears

- Weekly instalment schedules remain supported.
- When a due date passes without full settlement, set the schedule row to `OVERDUE`; do not automatically add interest, late fees, penalties or collection actions.
- Payment recording continues to work. Rescheduling, renegotiation, debt collection and default enforcement are out of scope for v1.
- `CREDIT_SALE` / handover before full settlement remains feature-disabled pending Irish legal/regulatory review. `LAYAWAY` is the enabled default.

### Payment processor clarification

“Payment method” is what the customer uses (`CASH` or `CARD`). A “payment processor/acquirer” is the company/service behind a card terminal that authorizes card transactions, handles processing and later pays the merchant. For v1 the Shop uses its existing external card terminal; the employee records `CARD` only after the terminal approves. The application does not select a provider, call a processor API, store card data or reconcile provider fees/payouts. A processor integration can be added later without changing the domain payment method.

## VAT status and readiness

The Company tax tables and `vat_enabled` switch are implemented in the model and default OFF. The Company has not yet confirmed its VAT registration/status or tax treatments. Those details remain pending the owner's later discussion with an Irish accountant. Do not issue VAT invoices or infer a 0%/exempt treatment from the OFF switch. Keep `setup_status=NOT_REVIEWED`; no VAT rate is hard-coded.

## PostgreSQL image storage decision

All uploaded image bytes stay in PostgreSQL. A shared `stored_image` table contains normalized binary image content in `BYTEA`; business-specific tables (`refurbished_unit_image`, `customer_complaint_case_attachment`) reference the image through `stored_image_id`. There is no S3/object-storage provider, local filesystem path or external storage workflow. Enforce existing per-file and per-target limits in application validation and database capacity planning.

Image retention is tied to the business record. Deletion is initially soft/audited; automated physical purge is not enabled in v1. The Company still needs a documented GDPR/data-retention schedule before production, but it will be implemented as database-controlled retention/purge jobs, not as manual external file management.

## Still open or deferred

1. **VAT/legal setup:** Company VAT registration/status, applicable rates/tax codes, tax-inclusive/exclusive display and legal reason for the chosen flag setting. Owner will confirm with an Irish accountant; `vat_enabled=false` is only the initial software switch.
2. **Card processor:** no provider selection is needed for v1; the existing physical terminal is used and CARD is manually recorded after approval. Integration can be considered later.
3. **Refund exception policy:** v1 uses Manager-only and original-tender refund. Alternative tender is disabled; exceptional out-of-band case handling may be defined later.
4. **Deposit contract wording:** operational default is defined above, but the final written terms/non-refundable special-order wording must be reviewed for fairness and Irish consumer-law compliance.
5. **Refurbished commercial guarantee:** 12 months is confirmed; the detailed coverage/exclusions below are a proposed default and require owner/legal review before publishing to customers.
6. **Customer data/image retention:** image storage is PostgreSQL-only, but exact business-record retention period and lawful purge schedule remain to be approved. Soft delete is used in v1.
7. **TECHNICIAN permission matrix:** v1 action-level matrix and dual-role precedence approved 2026-11-10, with customer/catalogue/Product/Fault/ServiceOrder permissions updated 2026-11-13; refunds remain Manager-only. See `employee-access-and-roles.md`.
8. **Complete Sale/ServiceOrder status transition matrix and executable production DDL/EF Core migration review.**
9. **Shop and employee authentication operations:** credential administration/recovery baseline, employee password policy, failed-attempt throttling without account lockout, action matrix and dual-role precedence approved 2026-11-10. ShopSession timeout values and multi-device/sign-out policy remain configurable and must be decided before production.
10. **Service-order transfer details:** Device custody, service-part inventory attribution, transfer authorization and final audit fields remain to define. The financial handling is decided: preserve already-settled transactions against the source Shop; transfer the outstanding balance and held/unapplied deposit liability to the destination through an auditable internal event; all post-transfer transactions use the destination.

**Deferred by owner instruction:** installment delinquency treatment beyond `OVERDUE`, renegotiation/late fees, and accounting/bookkeeper reconciliation mapping. These are not v1 implementation blockers.

## OIDC access-token renewal

On 2026-11-14 the owner approved automatic renewal of the SPA's short-lived access token. Renew through OIDC Authorization Code + PKCE silent reauthorization against the existing Shop sign-in cookie; keep access tokens in memory and continue to disable refresh-token issuance. If the Shop session/cookie has expired or been revoked, silent renewal fails, the SPA clears its local user and asks the operator to sign in again. This does not extend or alter the ShopSession timeout policy.

## Website-derived brand and operating profile

- On 2026-11-11 the owner requested branch-specific address and phone fields on `shop` for printed reports. On 2026-11-13 the owner approved local seed data from the official PC House homepage, selecting its ILAC address over the conflicting directory listing. The legal Company identity/registered address remain unconfirmed; this approval applies to local operational display data only.

- PC House identity is blue-led, with a deep-blue header/nav, blue/cyan wordmark and orange promotional `SALE` badges. The proposed CSS palette is explicitly approximate pending comparison with master artwork.
- The official logo reference is linked from the ILAC retailer directory; a local binary logo could not be bundled because the image host did not allow reliable retrieval. The source link and instructions are in `specs/03-design/brand-assets/README.md`.
- Product category seed candidates: MacBook, Laptop, TV, iMac, All-in-one, Desktop, Monitor, Console, Accessories; Phone appears in a secondary browse/filter view.
- Public business profile, candidate Shop records, contact details, opening hours, warranty marketing statement and repair description are documented in `specs/01-discovery/business-profile-from-website.md`.
- The ILAC branch address differs between the PC House homepage and the ILAC directory. The owner selected the homepage value for the local development seed; production/legal postal address confirmation remains open.

## Processor terminology — owner clarification

No processor must be selected for v1. `CASH` and `CARD` are the payment methods. A processor/acquirer is the financial provider behind a card terminal that authorizes a card payment and settles funds to the business. For v1, the employee uses the existing physical terminal and records a `CARD` allocation only after the terminal approves it. No API integration, cardholder data storage, terminal contract selection or automated bank settlement is part of the first release.

## Current approval status after website review

- **Confirmed:** CASH/CARD only; no processor integration in v1; PostgreSQL-only image storage; the current website's publicly listed categories/contact/branch data are captured as a reference snapshot.
- **Recommended terms still pending approval:** final customer-facing deposit cancellation/non-refundable-cost wording and the detailed refurbished guarantee coverage/exclusions. The 90-day deposit review reminder, no automatic expiry/forfeiture, Manager-only original-tender refunds, immutable cash-close policy and v1 TECHNICIAN action matrix are approved operational rules.
- **Owner/accountant confirmation still needed:** VAT registration/tax codes/rates and price display; exact legal Company name and registered address; final ILAC legal/postal address; final warranty policy wording; official logo and exact brand colours; GDPR retention period.

## External references used

- CCPC, [Deposits](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/deposits/) — deposit contract terms, cancellation/refund conditions and written disclosure.
- CCPC, [Your rights when buying goods](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/your-rights-when-buying-goods/) — consumer rights also apply to second-hand goods.
- CCPC, [Repairs and replacements](https://www.ccpc.ie/consumer-advice/faulty-products-and-goods/repairs-and-replacements) — remedies and reasonable-time principles.
- OWASP, [File Upload Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) — secure upload validation, re-encoding and controlled access.


## ServiceOrder owner decisions — 19 November 2026

- New ServiceOrders start in `CREATED`, not the legacy `IN_PROGRESS` initial state.
- Device serial number is mandatory at booking. Brand and model remain required as in the inspected legacy booking form. Serial-number uniqueness across the Company remains undecided.
- The legacy `badSector` integer represents the count of defective/bad sectors on the device HDD. It is required only for selected Services that need this measurement; operating-system installation is a confirmed example. The exact Service trigger list/configuration and numeric bounds remain open.
- ServiceOrder workflows must not edit Customer master data. Equipment and service details may be edited through the ServiceOrder edit workflow; payment and handover actions are separate notes-only interactions for order details. Whether Product/Service lines or prices may be changed after reservation or partial/settled payment, and how their historical snapshots are preserved, remains open.
- Payment and handover actions must not modify Customer, Device, Fault or ServiceOrder line details. They may append a Note alongside their own financial or handover event.
- Customer handover is allowed only for a ServiceOrder whose technical outcome is `NOT_FIXED`, and is prohibited while its current status is `FIXED`. On handover, the lifecycle status changes to `PICKED`; the technical outcome `NOT_FIXED` must remain independently persisted and visible.
- MANAGER and TECHNICIAN have equal permission to mark a ServiceOrder `NOT_FIXED` and to record its eligible handover. This does not change Manager-only payment, cancellation, deposit-forfeiture or refund permissions.
- Every successfully committed action in the ServiceOrder context must append an immutable, chronological, employee-attributed entry to `Notes`, describing the action, timestamp and concise result/reference. The corresponding structured domain or financial record remains authoritative; its Note is committed atomically. Failed/rolled-back actions do not create Notes.
- At collection of a `NOT_FIXED` order, eligible amounts already received for the order, including refundable deposit balances, may be refunded through FIN-01. Refund is a separate, explicit Manager-only action and is not automatic on handover. Existing original-tender, deposit-lifecycle and refundable-balance limits remain in force.
- Whether an outstanding ServiceOrder balance blocks handover of a `NOT_FIXED` order remains open.


## Employee Management — approved 20 November 2026

The detailed approved requirements are maintained in [Employee Management](../04-features/employee-management.md). The following decisions are authoritative:

- Active profiles are `EMPLOYEE`, `MANAGER` and `ADMIN`; retire `TECHNICIAN` and do not use dual-role assignment.
- All three profiles can operate in every Shop in the same Company; no per-Shop employee assignment exists.
- `MANAGER` retains the full application permission set assigned to the Manager profile in approved feature SPECs and administers `EMPLOYEE`/`MANAGER` accounts, but cannot create/grant `ADMIN`. `ADMIN` inherits all Manager permissions and has full Company-wide administrative authority, can manage other administrators and multiple admins are allowed.
- Keep at least one active `ADMIN` and at least one active user with management authority in each Company.
- Employee registration requires name, email and profile. Email is unique case-insensitively within the Company; duplicate names are allowed.
- Reuse `EmployeeCredential` and the existing reset flow. Temporary password is `<normalized-first-name>@pchouse`, displayed once after creation/reset and changed before protected operations. Employee recovery is performed only by `MANAGER` or `ADMIN`; self-service change requires the current password.
- Every protected operation requires a fresh direct backend validation of the acting employee's own password, active status, Company membership and feature-specific permission. Do not cache/reuse employee authentication.
- Remove `EmployeeActionAuthorization` token/entity/endpoint from the target design. Keep employee identity out of ShopSession and Shop OIDC tokens.
- Deactivation blocks further employee operations immediately but does not revoke the shared ShopSession. Reactivation preserves the credential and any pending password-change requirement.
- Successful protected business operations record the employee name as an immutable snapshot in business `notes`. Employee-administration operations and failed challenges create no `notes` entries; no dedicated employee-administration audit log is added.
- The operation-to-profile matrix for business workflows remains defined by each feature SPEC. The legacy TECHNICIAN matrix is historical until explicitly reconciled.
- Employee passwords have a minimum length of 6 characters; accept at least 128 characters and support password managers/paste. Do not require arbitrary periodic expiry.
- The Company-wide employee directory is available to any authenticated Shop session and shows only name, email, profile and active/inactive status. Search by name or email is case-insensitive, results are alphabetical by name, and status filtering is supported.
- Editing an employee's email or profile does not invalidate or change the password and does not force a password change.
- Deactivation blocks new protected operations but does not close the shared ShopSession. An operation that already passed credential, active-status, Company-membership and permission checks may complete even if the employee is deactivated while it is in flight.
- Reactivation is immediate, requires no additional approval, and preserves the existing credential and any pending mandatory-password-change state.


## ServiceOrder owner decisions — 20 November 2026 (continued)

- Preserve the legacy `Undo Fixed` capability with a settlement guard: a `FIXED` ServiceOrder may return to `IN_PROGRESS` only while its balance remains outstanding. Keep active stock reservations; do not reverse payments or inventory postings. Once full settlement consumes reserved parts, block direct reversal. Any later rework must use a separate corrective workflow that does not undo settled financial or stock movements.

- ServiceOrder booking creates the order in `CREATED` and does not imply repair has started. An explicit employee-authorized start-work action moves it to `IN_PROGRESS`. A blocking part shortage found before work starts may move it directly from `CREATED` to `AWAITING_PARTS`; receiving/reserving parts never starts work automatically, and resumption remains explicit.

- Starting ServiceOrder work (`CREATED` → `IN_PROGRESS`) requires fresh employee code/password authentication. Record the server-resolved employee as the actor in the immutable ServiceOrder event and `Notes`; ShopSession alone is insufficient for this protected action. Resuming from `AWAITING_PARTS` likewise remains an employee-authenticated explicit action.

- A ServiceOrder in `AWAITING_PARTS` returns to `IN_PROGRESS` only after every blocking part requirement is fully reserved. Resumption is an explicit action requiring fresh employee code/password authentication; receipt/reservation alone never restarts work. Record the authenticated employee in the event and `Notes`.

- ServiceOrder blocking Product-part requirements support partial reservations as stock becomes available. The order remains in `AWAITING_PARTS` while any blocking requirement is not fully reserved. Resume to `IN_PROGRESS` is allowed only after all blocking requirements are fully reserved; partial reservation alone is insufficient.

- A ServiceOrder stock shortage does not automatically create a procurement/purchase request. The order remains `AWAITING_PARTS` while blocking requirements are short; the team may initiate sourcing separately through the Shop-wide Service Parts Purchase Queue when needed.






## Simplified parts purchasing scope — owner decision

The owner has replaced the earlier supplier-procurement workflow with a simple **Parts to Buy** list. The earlier approvals for multiple procurement request rounds, request cancellation, released demand, supplier selection, grouped supplier purchases, purchase allocations, ETA/tracking and supplier receipt tracking are superseded and must not be implemented. The list presents uncovered Product-part requirements calculated from ServiceOrders (grouped by Product and broken down by originating order) separately from manual entries for additional parts not planned on an order. Clearly label each source and never combine manual quantities into calculated ServiceOrder demand totals. Manual entries may reference an existing Product or use a free-text description for a part not yet catalogued; quantity defaults to `1` and remains editable as a positive whole number. Existing Products reuse their multiple informational URLs from the Product catalogue; free-text entries may store multiple URLs directly on the manual entry. Manual entries can be removed from the active list without changing ServiceOrder requirements, events or Notes. These links are references only, not supplier or purchase records. The automatic portion updates as stock is replenished and reserved through the Stock module. The business may purchase parts from any source outside the application; no supplier or purchase workflow information is captured.
