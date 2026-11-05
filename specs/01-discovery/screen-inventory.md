# Swing Screen Inventory

**Status:** DISCOVERY  
**Source package:** `src/main/java/com/pchouse/pchousestoremvn/views/`

The repository tree contains 37 Java view classes, including modal dialogs. This is an inventory of class names, not a claim that each class is a separate user-facing page.

## Main views

- LoginView
- MainMenuView
- CustomerView
- EmployeeView
- FaultView
- ProductServiceView
- NewSaleView
- NewServiceOrderView
- OrderSaleListView
- RefurbView
- RefurbComputerView
- RefurbConsoleView
- RefurbCustomView
- RefurbMonitorView
- RefurbTelevisionView
- NewRefurbSaleView
- CreatedServiceOrderView
- FixedServiceOrderView
- NotFixedOrderView
- PickedOrderView
- RefundedOrderView
- CreatedRefurbSaleView
- PickedRefurbSaleView
- RefundedRefurbSaleView
- PickedSaleView
- RefundedSaleView
- CashInRegistryView
- CashOutRegistryView
- ClosingTillView

## Modal views

- CustomerModal
- DepositModal
- NoteModal
- PaymentHistoryModal
- PaymentModal
- ProductServiceModal
- TransferingOrderModal
- CloseTillSummaryDialog

## Navigation evidence

Source: `views/MainMenuView.java`

Observed menu groups and actions:

- Create: New Order (F1), New Sale (F2), New Refurb Sale (F3)
- Manage: Customers (F4), Users (F5), Products (F6), Faults (F7)
- Refurbs: All Refurbs, Computer, Monitor, Television, Console, Custom
- Consult: Orders (F8), Sales (F9)
- Report: Close Till (F10)
- Cash: Cash In (F11), Cash Out (F12)

## High-priority screen analysis

### NewSaleView

Source: `views/NewSaleView.java`

The class contains customer resolution, required-field validation, operator identification, product/service search, line-item handling, totals and save actions. The complete behavior must be split into application use cases and web UI interactions.

### NewServiceOrderView

Source: `views/NewServiceOrderView.java`

The class contains customer handling, device data entry, fault search, product/service search, notes, deposits, totals and save behavior.

## Follow-up

For each view, document fields, labels, validation, event handlers, modal interactions, state transitions, controller calls and DAO operations before finalizing feature specifications.
