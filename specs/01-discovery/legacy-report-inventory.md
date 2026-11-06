# Legacy Report and Print Inventory

**Status:** DISCOVERY COMPLETE for the files and call paths listed below. The web replacement is specified separately in [Reports, Receipts and Document Printing](../02-architecture/reports-and-document-printing.md).

**Repository:** https://github.com/hermanCosta/pchousestoremvn — branch `main`.

## Repository-level findings

- 15 `.jrxml` source templates exist in `src/main/resources/com/pchouse/pchousestoremvn/reports/`.
- 13 compiled `.jasper` templates exist in the same resource folder.
- The Java report facade is `src/main/java/com/pchouse/pchousestoremvn/util/ReportGenerator.java`.
- The facade uses JasperReports and `JasperViewer.viewReport(...)` for preview.
- The connected print flows inspected use A4 portrait JRXML page dimensions (`595 × 842` points).
- A separate Swing form, `PrintRefurbLabel.java`, prepares a refurbished-item label and sends it to a printer helper.
- `PrinterUtil.printPanelAsImage(JPanel)` exists as a generic utility; an active caller in the inspected report flows was not established.

## Connected document workflows

| Document | Template/class | Generator method | UI trigger |
|---|---|---|---|
| Service-order intake | `ServiceOrderReport.jrxml` | `generateServiceOrderReport(...)` | `CreatedServiceOrderView.btn_printActionPerformed(...)` |
| Service payment receipt | `ServiceOrderReceiptReport.jrxml` | `generateServiceOrderReceiptReport(...)` | `FixedServiceOrderView` after successful service payment persistence in pickup flow |
| Standard sale receipt | `SaleOrderReport.jrxml` | `generateSaleReceiptReport(...)` | `PickedSaleView.btn_printActionPerformed(...)` |
| Refurbished sale receipt | `RefurbSaleReceiptReport.jrxml` | `generateRefurbSaleReceiptReport(...)` | `CreatedRefurbSaleView` flow and `PickedRefurbSaleView.btn_printActionPerformed(...)` |
| Daily cash-close report | `TillCloseReport.jrxml` | `generateDailyClosingTillReport(...)` | `ClosingTillView` |
| Refurbished unit label | `PrintRefurbLabel.java` | Standalone Swing print implementation | `CommonSetting.printReport(...)` |

## Source links

- [ReportGenerator.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/util/ReportGenerator.java)
- [CreatedServiceOrderView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/CreatedServiceOrderView.java)
- [FixedServiceOrderView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/FixedServiceOrderView.java)
- [PickedSaleView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/PickedSaleView.java)
- [PickedRefurbSaleView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/PickedRefurbSaleView.java)
- [CreatedRefurbSaleView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/CreatedRefurbSaleView.java)
- [ClosingTillView.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/views/ClosingTillView.java)
- [PrintRefurbLabel.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/reports/PrintRefurbLabel.java)
- [PrinterUtil.java](https://github.com/hermanCosta/pchousestoremvn/blob/main/src/main/java/com/pchouse/pchousestoremvn/common/PrinterUtil.java)

## Templates present but not confirmed as active print features

- `RefurbSaleOrderReport.jrxml`: present in resources, but no active call was found in the inspected `ReportGenerator` methods. It should be reviewed before deciding whether to reuse or discard it.
- `MainReporTest.jrxml`: test-oriented template; no customer-facing production trigger was identified.
- Subreports: `subreport_header.jrxml`, `subreport_faults.jrxml`, `subreport_prodserv.jrxml`, `subreport_refurbs_sale.jrxml`, `subreport_sale_payments.jrxml`, `subreport_service_payments.jrxml`, `subreport_cash_in.jrxml` and `subreport_cash_out.jrxml`.

## Important migration warnings

1. `PrintRefurbLabel.java` hard-codes `6 Months Warranty`; the approved target rule is a 12-calendar-month commercial guarantee from documented customer handover, additional to statutory rights.
2. The internal Jasper name in `ServiceOrderReceiptReport.jrxml` and `SaleOrderReport.jrxml` is `ServiceOrderReport`. Do not reuse those internal names as target document identifiers.
3. Jasper `.jasper` files are compiled Java artifacts. The web application must use its own versioned templates and should not ship these files as runtime dependencies.
4. The old preview is a desktop JasperViewer. The web redesign uses PDF preview/download and the browser's print dialog.
5. Deposit receipt, refund receipt, instalment payment receipt, collection confirmation, transfer note and complaint summary were not found as standalone legacy templates in this inventory. They are new target requirements, not inherited legacy features.
