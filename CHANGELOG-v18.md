# v18 — Reports, receipts and document printing (6 November 2026)

## Added

- Completed discovery inventory of legacy JasperReports assets: 15 JRXML templates, 13 compiled Jasper files, connected report methods and Swing UI triggers.
- Added a target document catalogue covering service intake, service payment, sale, refurbished sale, refurbished guarantee, deposit, refund, instalment, collection, daily cash closing, procurement, stock, transfers and complaint documents.
- Added A4 and 80 mm thermal PDF layout requirements, preview/download/browser print workflow and `Europe/Dublin` / EUR / `en-IE` formatting rules.
- Added `GeneratedDocument` immutable snapshot metadata and `DocumentReprintEvent` audit model; PDF BLOB persistence is deferred for v1.
- Added document-generation lifecycle and focused document ERD.
- Added regression criteria for document content, Shop isolation, PDF layouts, VAT display, warranty wording and safe reprints.

## Confirmed legacy features

- Service-order intake form (`ServiceOrderReport.jrxml`).
- Service payment receipt (`ServiceOrderReceiptReport.jrxml`).
- Standard sale receipt (`SaleOrderReport.jrxml`).
- Refurbished-sale receipt (`RefurbSaleReceiptReport.jrxml`).
- Daily till-close report (`TillCloseReport.jrxml`).
- Refurbished-equipment label (`PrintRefurbLabel.java`), which contains obsolete six-month guarantee wording.

## Important boundaries

- Legacy `RefurbSaleOrderReport.jrxml` exists but was not connected to an active generator method in the inspected flows; its purpose remains unverified.
- Deposit/refund/instalment receipts and collection confirmation are new target documents, not confirmed standalone legacy templates.
- The .NET report renderer and React document UI are specified, not implemented.
