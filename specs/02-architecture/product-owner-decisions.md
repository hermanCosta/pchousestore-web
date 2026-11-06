# Product-owner Decisions and Remaining Questions — pcHouseStore Web

**Revision:** v16 · 5 November 2026  
**Status:** Confirmed product decisions are incorporated. A small set of commercial/legal details remains explicitly proposed or deferred; the model is not a production-approved DDL.

## Confirmed decisions

| Area | Confirmed rule |
|---|---|
| Organization | One legal Company with multiple operational Shops/branches. |
| Shop access | Persistent session represents the active Shop only; it has no Employee identity. |
| Employee identity | One Company-wide employee identity; employee code + individual password works in every Shop. Every protected operation requires a fresh, one-use employee authorization. |
| Roles | `MANAGER` has full access across every Shop. `TECHNICIAN` has partial technical/operational access under the matrix in `employee-access-and-roles.md`; the proposed baseline is ready for implementation subject to owner review. |
| Inventory | Only Product catalogue items have stock. Product quantities are whole units. Quantity-tracked stock may become negative; reservations cannot. One stock pool per Shop; no reservation-expiry automation or multiple internal locations in v1. |
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
| Supplier procurement | Separate service-part purchase request queue linked to a ServiceOrder, multiple request rounds, line-level reference images, purchase links, ETA/tracking and partial receipts. Supplier purchase creation/update/receipt does not require second approval. |
| Images | Multiple images are uploaded and stored inside the same MySQL database. No external object storage, file share or filesystem path is used. Binary storage uses `MEDIUMBLOB`; thumbnails use bounded `BLOB`. Initial image limits remain 10 MiB/file, 10 images and 100 MiB aggregate per target. |
| Cash closing | One `DailyClosing` per Shop/business date. Opening float, cash receipts/change, cash-in/out, cash refunds, deposit refunds, safe drops, denomination count and variance are recorded. Close is immutable; corrections are separate Manager-authorized records. |

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

## MySQL image storage decision

All uploaded image bytes stay in MySQL. A shared `stored_image` table contains the normalized binary image in `MEDIUMBLOB` and an optional compact thumbnail in `BLOB`; business-specific tables (`service_procurement_request_image`, `refurbished_unit_image`, `customer_complaint_case_attachment`) reference the image through `stored_image_id`. There is no S3/object-storage provider, local filesystem path or external storage workflow. The 10 MiB per-image cap is below MySQL `MEDIUMBLOB` capacity (16,777,215 bytes). See the [MySQL 8.0 BLOB/TEXT type reference](https://dev.mysql.com/doc/refman/8.0/en/blob.html).

Image retention is tied to the business record. Deletion is initially soft/audited; automated physical purge is not enabled in v1. The Company still needs a documented GDPR/data-retention schedule before production, but it will be implemented as database-controlled retention/purge jobs, not as manual external file management.

## Still open or deferred

1. **VAT/legal setup:** Company VAT registration/status, applicable rates/tax codes, tax-inclusive/exclusive display and legal reason for the chosen flag setting. Owner will confirm with an Irish accountant; `vat_enabled=false` is only the initial software switch.
2. **Card processor:** no provider selection is needed for v1; the existing physical terminal is used and CARD is manually recorded after approval. Integration can be considered later.
3. **Refund exception policy:** v1 uses Manager-only and original-tender refund. Alternative tender is disabled; exceptional out-of-band case handling may be defined later.
4. **Deposit contract wording:** operational default is defined above, but the final written terms/non-refundable special-order wording must be reviewed for fairness and Irish consumer-law compliance.
5. **Refurbished commercial guarantee:** 12 months is confirmed; the detailed coverage/exclusions below are a proposed default and require owner/legal review before publishing to customers.
6. **Customer data/image retention:** image storage is MySQL-only, but exact business-record retention period and lawful purge schedule remain to be approved. Soft delete is used in v1.
7. **TECHNICIAN permission matrix:** partial access is confirmed; the proposed action-level matrix is in `employee-access-and-roles.md` and still needs owner sign-off before fine-grained API policies are frozen.
8. **Complete Sale/ServiceOrder status transition matrix and executable production DDL/EF Core migration review.**

**Deferred by owner instruction:** installment delinquency treatment beyond `OVERDUE`, renegotiation/late fees, and accounting/bookkeeper reconciliation mapping. These are not v1 implementation blockers.

## Website-derived brand and operating profile

- PC House identity is blue-led, with a deep-blue header/nav, blue/cyan wordmark and orange promotional `SALE` badges. The proposed CSS palette is explicitly approximate pending comparison with master artwork.
- The official logo reference is linked from the ILAC retailer directory; a local binary logo could not be bundled because the image host did not allow reliable retrieval. The source link and instructions are in `specs/03-design/brand-assets/README.md`.
- Product category seed candidates: MacBook, Laptop, TV, iMac, All-in-one, Desktop, Monitor, Console, Accessories; Phone appears in a secondary browse/filter view.
- Public business profile, candidate Shop records, contact details, opening hours, warranty marketing statement and repair description are documented in `specs/01-discovery/business-profile-from-website.md`.
- The ILAC branch address differs between the PC House homepage and the ILAC directory; do not seed the final postal address until owner confirmation.

## Processor terminology — owner clarification

No processor must be selected for v1. `CASH` and `CARD` are the payment methods. A processor/acquirer is the financial provider behind a card terminal that authorizes a card payment and settles funds to the business. For v1, the employee uses the existing physical terminal and records a `CARD` allocation only after the terminal approves it. No API integration, cardholder data storage, terminal contract selection or automated bank settlement is part of the first release.

## Current approval status after website review

- **Confirmed:** CASH/CARD only; no processor integration in v1; MySQL-only image storage; the current website's publicly listed categories/contact/branch data are captured as a reference snapshot.
- **Recommended defaults (not yet approved contract terms):** 90-day deposit inactivity review reminder; no automatic expiry/forfeiture; Manager-only original-tender refunds; TECHNICIAN permissions matrix; proposed refurbished guarantee coverage/exclusion wording.
- **Owner/accountant confirmation still needed:** VAT registration/tax codes/rates and price display; exact legal Company name; final ILAC address/display name; final warranty policy wording; official logo and exact brand colours; GDPR retention period; final action-level TECHNICIAN permissions.

## External references used

- CCPC, [Deposits](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/deposits/) — deposit contract terms, cancellation/refund conditions and written disclosure.
- CCPC, [Your rights when buying goods](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/your-rights-when-buying-goods/) — consumer rights also apply to second-hand goods.
- CCPC, [Repairs and replacements](https://www.ccpc.ie/consumer-advice/faulty-products-and-goods/repairs-and-replacements) — remedies and reasonable-time principles.
- MySQL 8.0, [BLOB and TEXT types](https://dev.mysql.com/doc/refman/8.0/en/blob.html) — BLOB family capacities and behavior.
- OWASP, [File Upload Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) — secure upload validation, re-encoding and controlled access.
