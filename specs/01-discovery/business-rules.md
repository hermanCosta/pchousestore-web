# Business Rules — Initial Evidence

**Status:** DISCOVERY  
**Policy:** This document records only rules evidenced in source files inspected so far. It is not yet a complete or approved business specification.

## Sales

Source: `views/NewSaleView.java`

- **CONFIRMED:** First name, last name, contact number and at least one product/service row are required by `validateRequiredFields`.
- **CONFIRMED:** The flow requests an employee password and resolves an employee before constructing a sale.
- **CONFIRMED:** Product/service rows contribute to a displayed total.
- **CONFIRMED:** Customer lookup/matching is performed when an existing customer is selected.
- **OPEN:** Whether an email is mandatory.
- **OPEN:** Which payment methods and partial-payment rules are supported.
- **OPEN:** Whether discounts, price overrides or refunds are allowed in this workflow.

Source: `controllers/SaleController.java`

- **CONFIRMED:** Sale creation can include a sale, sale items, payments, deposit and note in a single use-case call.
- **CONFIRMED:** Refurbished sales have a separate creation flow.

## Service orders

Source: `views/NewServiceOrderView.java`

- **CONFIRMED:** First name, last name, contact, device brand, device model, serial number, at least one product/service and at least one fault are required by the inspected save validation.
- **CONFIRMED:** The operator is resolved using a password before the order is created.
- **CONFIRMED:** The workflow supports searching and adding faults and products/services.
- **OPEN:** Exact semantics of `badSector`.
- **OPEN:** Which status transitions are permitted and by whom.
- **OPEN:** Deposit and remaining-balance rules.
- **OPEN:** Whether serial number uniqueness is required.

Source: `controllers/ServiceOrderController.java`

- **CONFIRMED:** Service orders can be created, updated and transferred.
- **CONFIRMED:** A creation call can include faults, product/services, payments, deposit and note.

## Sale model

Source: `models/Sale.java`

- **CONFIRMED:** Setter rejects a negative total.
- **CONFIRMED:** Setter rejects a negative remaining balance.
- **OPEN:** Whether zero totals are valid in the domain.
- **REDESIGN:** Use decimal monetary types.

## Status enumeration

Source: `enums/OrderStatus.java`

Stored labels are IN_PROGRESS, FIXED, NOT_FIXED, FINISHED, PICKED, REFUNDED, CANCELLED and CREATED.

**OPEN:** Map valid transitions and identify which views/actions cause each transition before implementing a state machine.

## Important implementation note

Do not translate UI validation directly into API validation without evaluating the domain. API validation must be authoritative so that the rules apply regardless of client.
