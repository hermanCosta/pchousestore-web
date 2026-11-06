# Reports, Receipts and Document Printing

**Status:** SPECIFIED — functional baseline drafted from verified legacy assets and migration decisions. Visual templates and implementation remain to be built.

## 1. Goal

Provide a consistent, auditable document-generation capability for the PC House web back-office. Users must be able to preview, download and print customer-facing documents and internal operational reports from the relevant business record.

The web application will reproduce the business purpose and important information of the legacy JasperReports documents, not mechanically port `.jrxml` or compiled `.jasper` files. The new templates must use the PC House logo assets, Shop identity, EUR formatting and the configured VAT state.

## 2. Scope

### In scope

- Service-order intake/work authorisation form.
- Service payment receipt and payment transaction receipts.
- Product sale receipt.
- Refurbished-equipment sale receipt and 12-month commercial guarantee certificate.
- Deposit receipt and refund receipt.
- Daily Shop cash-closing report.
- Service handover/collection confirmation.
- Preview, PDF download, browser printing and controlled reprint history.
- A4 and 80 mm thermal-paper layouts.
- Internal printable reports for stock, procurement and complaints as follow-up work.

### Out of scope for v1

- Fiscal invoice / tax-authority integration or claiming that a generated receipt is a tax invoice.
- Direct printer-device, cash-register or ESC/POS integration from the server.
- Automatic email/SMS dispatch.
- External payment-processor integration; card is captured after approval on the Shop's existing external terminal.
- User-authored arbitrary report designer.
- Migrating Jasper `.jasper` binaries as runtime assets.

## 3. Verified legacy report inventory

Repository inspected: [`hermanCosta/pchousestoremvn`, branch `main`](https://github.com/hermanCosta/pchousestoremvn).

The recursive repository tree contains 15 `.jrxml` files under `src/main/resources/com/pchouse/pchousestoremvn/reports/` and 13 compiled `.jasper` files. The core report templates inspected use page dimensions 595 × 842 points (A4 portrait). The legacy report engine is `ReportGenerator.java`, which uses JasperReports and opens a `JasperViewer` preview.

| Legacy document / asset | Source template or class | Legacy generator / caller | Discovery result |
|---|---|---|---|
| Service-order intake form | `reports/ServiceOrderReport.jrxml` | `ReportGenerator.generateServiceOrderReport`; Print action in `views/CreatedServiceOrderView.java` | **Confirmed wired.** Includes customer/device information, reported faults, products/services, financial summary, important notes and customer authorisation/signature area. |
| Service-order payment receipt | `reports/ServiceOrderReceiptReport.jrxml` | `ReportGenerator.generateServiceOrderReceiptReport`; called from `views/FixedServiceOrderView.java` after payment is saved in the pickup flow | **Confirmed wired.** Includes customer/device, products/services and financial/payment summary. |
| Standard sale receipt | `reports/SaleOrderReport.jrxml` | `ReportGenerator.generateSaleReceiptReport`; Print action in `views/PickedSaleView.java` | **Confirmed wired.** Includes customer, sale items and financial/payment summary. |
| Refurbished-sale receipt | `reports/RefurbSaleReceiptReport.jrxml` | `ReportGenerator.generateRefurbSaleReceiptReport`; called from `views/CreatedRefurbSaleView.java` and Print action in `views/PickedRefurbSaleView.java` | **Confirmed wired.** Includes customer, refurbished items and financial/payment summary. |
| Daily till-close report | `reports/TillCloseReport.jrxml` | `ReportGenerator.generateDailyClosingTillReport`; called from `views/ClosingTillView.java` | **Confirmed wired.** Includes sale payments, service-order payments, cash-in, cash-out and summary totals. |
| Refurbished equipment label | `reports/PrintRefurbLabel.java` | Swing `PrintRefurbLabel` panel and `CommonSetting.printReport(...)` | **Confirmed print UI.** Includes brand/model/screen, processor, graphics, memory, storage and price. It currently prints the literal `6 Months Warranty`, which conflicts with the approved 12-month commercial guarantee and must not be copied. |
| Refurbished sale/order template | `reports/RefurbSaleOrderReport.jrxml` | No active generator call found in the inspected `ReportGenerator` methods | **Present, usage unverified.** Its labels resemble a service-order authorisation form; confirm intent before reuse. |
| Test report | `reports/MainReporTest.jrxml` | No production flow identified | **Test artefact; do not migrate as a customer document.** |
| Shared subreports | `subreport_header`, `subreport_faults`, `subreport_prodserv`, `subreport_refurbs_sale`, `subreport_sale_payments`, `subreport_service_payments`, `subreport_cash_in`, `subreport_cash_out` | Loaded by `ReportGenerator` | **Supporting templates.** Rebuild as reusable web template components where useful. |
| Generic Swing printing helper | `common/PrinterUtil.java` | `printPanelAsImage(JPanel)` | Utility exists; no active report use was established in the inspected print paths. |

### Legacy source references

- [`ReportGenerator.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/util/ReportGenerator.java): `generateServiceOrderReport`, `generateSaleReceiptReport`, `generateServiceOrderReceiptReport`, `generateRefurbSaleReceiptReport`, `generateDailyClosingTillReport`.
- [`CreatedServiceOrderView.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/CreatedServiceOrderView.java): `btn_printActionPerformed`.
- [`FixedServiceOrderView.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/FixedServiceOrderView.java): payment-save flow invokes the service receipt generator.
- [`PickedSaleView.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/PickedSaleView.java): `btn_printActionPerformed`.
- [`PickedRefurbSaleView.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/PickedRefurbSaleView.java): `btn_printActionPerformed`.
- [`CreatedRefurbSaleView.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/CreatedRefurbSaleView.java): invokes the refurbished-sale receipt generator.
- [`ClosingTillView.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/ClosingTillView.java): invokes the daily-close report generator.
- [`PrintRefurbLabel.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/reports/PrintRefurbLabel.java): refurbished equipment label and legacy six-month text.
- [`PrinterUtil.java`](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/common/PrinterUtil.java): generic Swing panel print utility.

### Legacy template anomalies to resolve during redesign

- `ServiceOrderReceiptReport.jrxml` and `SaleOrderReport.jrxml` declare the internal Jasper report name `ServiceOrderReport`; the web templates must use unique document keys and names.
- `RefurbSaleOrderReport.jrxml` is present but not called by the inspected report generator; do not assume it is an active print feature.
- `PrintRefurbLabel.java` says six months; the new product decision is twelve months from recorded customer handover.
- Legacy code opens a JasperViewer rather than providing a web PDF/document history. The web workflow is a redesign.

## 4. Target document catalogue

`LEGACY` indicates an existing, connected legacy print behaviour. `NEW` indicates a document added to complete the target workflows.

| Document key | Origin | Trigger / availability | Required output |
|---|---|---|---|
| `SERVICE_ORDER_INTAKE` | LEGACY | After a ServiceOrder is created; also manually available from the order detail | A4 PDF with Shop/company identity, order number/date, customer, device identifiers, reported fault(s), requested products/services, estimate/financial summary, notes and customer authorisation/signature area. |
| `SERVICE_PAYMENT_RECEIPT` | LEGACY + REDESIGN | One receipt for every settled service PaymentTransaction; the final service balance settlement must not be the only receipt if earlier partial payments were received | 80 mm receipt and A4 PDF with receipt number, service-order reference, customer, payment date, tender breakdown (CASH/CARD), amount received, change where applicable, cumulative paid and remaining balance. |
| `SERVICE_COLLECTION_CONFIRMATION` | NEW | When equipment is handed back and the ServiceOrder transitions to `PICKED` | A4 or 80 mm confirmation with order, customer, equipment identifiers, handover timestamp, collected-by acknowledgement/signature and relevant notes. It must not create another payment or stock movement. |
| `SALE_RECEIPT` | LEGACY + REDESIGN | After sale settlement; separate payment receipt is available for each settled payment transaction where the sale supports multiple payments | 80 mm receipt and A4 PDF with sale number/date, Shop, customer where supplied, item lines, quantities, prices, discount if supported, total, CASH/CARD tender allocations, cash received/change, and payment reference if available. |
| `REFURBISHED_SALE_RECEIPT` | LEGACY + REDESIGN | For each settled payment and final sale/handover as appropriate; avoid duplicate receipt numbering for the same payment | 80 mm receipt and A4 PDF with sale, serialized refurbished unit(s), asset code, manufacturer serial when available, category, condition grade, total and tender breakdown. |
| `REFURBISHED_WARRANTY_CERTIFICATE` | NEW replacement for legacy label | At documented handover of each refurbished unit | A4 or compact customer copy with unit asset code, manufacturer serial if available, make/model/category, condition grade, included specification summary, handover date, 12-calendar-month commercial guarantee end date, and clear statement that statutory consumer rights are unaffected. Never show the legacy six-month wording. |
| `DEPOSIT_RECEIPT` | NEW | Every settled deposit receipt at service booking or other approved deposit event | 80 mm and A4 with deposit reference, ServiceOrder, purpose (`PARTS_PREORDER`, `BOOKING`, `SECURITY_HOLD`), amount, tender, date, and concise cancellation/application terms snapshot. |
| `REFUND_RECEIPT` | NEW | Every settled partial or total refund | 80 mm and A4 with original transaction reference, original document reference, refunded amount, tender allocation(s), date, remaining refundable balance and manager authorization identity/reference. |
| `INSTALLMENT_PAYMENT_RECEIPT` | NEW | Every settled weekly instalment payment | 80 mm and A4 with plan, refurbished unit/sale, instalment number(s), amount received, paid/remaining schedule summary and tender. |
| `DAILY_CASH_CLOSING_REPORT` | LEGACY + REDESIGN | When a Shop's daily closing is finalised | A4 report with Shop/date, opening float, cash receipts net of change, cash-in/out, cash refunds, safe drops, expected cash, denomination count, variance, electronic tender summary separately and closing actor/time. |
| `INTER_SHOP_TRANSFER_NOTE` | NEW | Transfer dispatch and receiving-Shop acceptance | A4 or compact copy with transfer number, source/destination Shop, item/quantity lines, dispatch/receipt times, actors and discrepancy notes. |
| `SERVICE_PROCUREMENT_QUEUE_REPORT` | NEW | On demand from the Service Parts Purchase Queue | A4 list of open/overdue requests, ServiceOrder IDs, part descriptions, reference-image indicators, quantity outstanding, supplier/source, external order link, ETA and tracking status. |
| `STOCK_MOVEMENT_REPORT` | NEW | On demand for a Shop and date range | A4 report of product, movement type, signed quantity, source reference, timestamp and actor; includes negative-stock deficits. |
| `CUSTOMER_COMPLAINT_SUMMARY` | NEW | On demand or when a complaint case is resolved | A4 case chronology, product/refurbished unit, purchase/handover reference, inspection findings, communications and resolution. Customer-facing exports must omit internal-only notes. |

### Receipt semantics

- A *payment receipt* proves one settled receipt transaction. A *sale receipt* or *service summary* describes the underlying commercial record. They are related but are not interchangeable.
- Every partial payment/deposit/instalment that is actually settled can generate its own transaction receipt. Do not wait for full balance settlement to provide evidence that money was received.
- The full-payment event is still the inventory deduction trigger defined in ADR-016. Printing a document never posts inventory or financial movements.
- An application of an already-received deposit to an order balance is not a second cash receipt and must not generate a misleading new receipt for money received at that point.
- Refund receipts are created only after the refund transaction is settled. A refund preview or approval is not proof of money returned.

## 5. Layout and output rules

### Required layouts

- **A4 portrait PDF:** service intake/authorisation, collection confirmation, warranty certificate, cash-close report and operational reports.
- **80 mm thermal PDF:** compact customer payment receipts, sale receipts, deposit receipts, refund receipts and instalment receipts.
- Customer receipts should also offer A4 output for email/archive workflows.
- The output layout is explicitly selected or defaulted by the document type; the chosen layout is stored in `GeneratedDocument` metadata.

The legacy JRXML templates inspected use A4 portrait dimensions. No verified 80 mm thermal template was found; 80 mm support is a **REDESIGN** requirement.

### PDF and browser print

- The backend creates a PDF stream; the React client opens a preview with Download and Print actions.
- The browser/OS print dialog selects the physical printer. The server does not attempt to select a local printer in v1.
- Thermal output uses 80 mm page width, printer-safe margins and compact typography; A4 output uses standard printable margins and page numbering for multi-page reports.
- Use embedded fonts or safe server-side fonts to avoid layout shifts. Prevent table rows, totals and signature blocks from clipping across pages.
- Dates/times use `Europe/Dublin`; monetary output uses EUR and `en-IE` formatting.
- VAT rows are shown only when Company VAT is enabled and the transaction has a valid tax snapshot. VAT disabled must not be represented as a legal statement that the Company is VAT-exempt.
- Render the Shop address/contact details and approved PC House logo captured for the Shop at transaction time.

## 6. Document history and data model

The document service is a reusable module; templates are versioned application assets, not user-uploaded templates.

### `GeneratedDocument`

Minimum metadata:

- `Id` (UUID or BIGINT key according to final DB convention)
- `CompanyId`, `ShopId`
- `DocumentType`, `DocumentNumber` (when a customer-facing sequence is required)
- `SourceType`, `SourceId`, `SourceVersion`
- `TemplateKey`, `TemplateVersion`, `LayoutCode` (`A4` / `THERMAL_80MM`)
- `IssuedAtUtc`, `IssuedByEmployeeId`
- `SnapshotJson` (immutable render payload, including Shop/contact/logo reference, customer-facing values, financial/tax snapshots and line descriptions)
- `ContentHash` (integrity/checksum for the rendered payload or output)
- `Status` (`ISSUED`, `VOIDED`); corrections create a replacement/correction document instead of editing an issued document.

### `DocumentReprintEvent`

- `Id`, `GeneratedDocumentId`, `ShopId`, `PrintedAtUtc`, `PrintedByEmployeeId`, `LayoutCode`, `CopyLabel` (`ORIGINAL`, `COPY`, `REPRINT`), optional reason.
- Reprints do not create a new payment, sale, refund, stock movement or daily-close event.

### Storage decision

- Persist the immutable render snapshot and metadata in MySQL.
- Do not persist every rendered PDF binary in v1 by default; regenerate from the stored snapshot using the retained `TemplateVersion`. This keeps the document history queryable and avoids duplicating all rendered output files in the database.
- Retain old template versions for document types already issued. If exact bit-for-bit PDF archival is later required by accounting/legal policy, add a separately approved PDF BLOB retention policy; it is not implied by the existing image BLOB decision.
- The document record may refer to source rows through `SourceType`/`SourceId`; service-layer validation must prove the source belongs to the same Company/Shop. Avoid trusting client-supplied IDs or Shop context.

## 7. API and UI behaviour (target proposal)

Illustrative API shape; final routing conventions are set in the API contract phase:

- `POST /api/documents/{documentType}/generate` — source reference + layout; creates an immutable GeneratedDocument snapshot and returns its ID.
- `GET /api/documents/{documentId}/pdf` — render/stream PDF from the stored snapshot and versioned template.
- `GET /api/documents?sourceType=...&sourceId=...` — list issued documents for a source record.
- `POST /api/documents/{documentId}/reprints` — append a reprint event and return PDF.

UI entry points:

- Service Order detail: Print service form, payment receipts, collection confirmation and document history.
- Sale detail: Print sale receipt and payment receipts.
- Refurbished unit/sale detail: Print sale receipt and warranty certificate.
- Deposit/payment/refund detail: Print its transaction receipt.
- Daily Closing detail: Print closing report.
- Service Parts Purchase Queue, Stock, Transfers and Complaints: Print corresponding operational report on demand.

The preview must display document type, number, Shop, issue date and selected layout before printing. Reprint actions must visibly label copies as `COPY`/`REPRINT` when required by the business document type.

## 8. Authorization, audit and tenant isolation

- Every document query and generation is scoped to the active ShopSession and the source record's Company/Shop ownership.
- Customer-facing documents may be generated only from a valid business record; users cannot post financial effects through a print endpoint.
- `IssuedByEmployeeId` and `PrintedByEmployeeId` identify the employee who performed the action, not the ShopSession owner.
- Manager-only document actions follow existing Manager permissions (e.g. refund authorization and void/correction). Technician may print service intake/technical work documents where allowed, but cannot access cash-close, refund or financial documents outside assigned permissions.
- Reprinting is auditable; it must not duplicate document numbering, payments, stock or refund events.
- Do not expose internal diagnostic notes, passwords, security tokens, private procurement credentials or private complaint notes on customer-facing documents.

## 9. Acceptance criteria

1. All five active legacy report workflows have a mapped target document and equivalent required business information.
2. Service-order intake PDF contains customer/device identifiers, reported faults, products/services, financial summary, notes and authorisation area.
3. Each settled service/sale/deposit/refund/instalment transaction can produce a receipt for the amount actually settled, including partial receipts.
4. Full service/sale settlement triggers inventory posting once; generating or reprinting a document never posts inventory.
5. Refurbished warranty certificate shows 12 calendar months from recorded handover, unit identity and statutory-rights disclaimer; no six-month wording remains.
6. Daily closing report reconciles to the Shop cash-closing ledger and separates CASH from CARD.
7. A4 and 80 mm output are checked for clipping, page breaks, readable totals and correct logo placement.
8. Every document is Shop-scoped; cross-Shop source IDs are rejected.
9. Historical reprint uses the stored immutable snapshot and retained template version; reprint history records employee, timestamp and copy type.
10. VAT output follows the Company VAT flag and effective-dated tax snapshot; VAT-off output does not claim that the Company is legally exempt.
11. Currency and dates render as EUR / `en-IE` / `Europe/Dublin`.
12. Customer-facing PDF omits internal-only notes and sensitive data.

## 10. Open items

- Exact fiscal invoice requirements and VAT display remain subject to owner/accountant confirmation; this feature does not replace a compliant fiscal invoicing workflow.
- Confirm customer-facing document numbering convention and whether receipts need a distinct Shop prefix.
- Confirm exact wording/terms on service authorisation and collection signature blocks.
- Confirm whether PDF binary retention is required; v1 stores snapshots and template versions in MySQL, not PDF BLOBs by default.
- Confirm which operational reports beyond the required closing report and service procurement queue are part of v1.
- Confirm if thermal printer width is exactly 80 mm for each Shop; 58 mm devices are not included in this baseline.

## 11. Implementation status

This is a specification, not an implemented report engine. Do not copy JasperReports binaries into the .NET application. Implement document rendering as a dedicated application service with versioned templates, snapshot-based generation, PDF streaming and integration/UI tests.
