# CAT-01 Legacy Audit — Customers and Shared Catalogues

**Status:** Discovery recorded; product rules remain open. No implementation is authorized by this audit.
**Scope:** Customer records, Product/Service catalogue entries, product categories, and the Fault catalogue. Employee administration is tracked separately under EMP-01 and is not repeated here.

## Evidence and source references

All paths below are relative to the legacy repository `hermanCosta/pchousestoremvn`.

| Area | Sources and inspected methods | Confirmed legacy behavior |
|---|---|---|
| Customer list and form | `src/main/java/com/pchouse/pchousestoremvn/views/CustomerView.java`: `CustomerView`, `loadCustomerListTable`, `getCustomerFields`, `searchCustomer`, `btn_addActionPerformed`, `btn_updateActionPerformed`; `views/modals/CustomerModal.java`: `loadCustomerListTable`, `getCustomerFields`, `searchCustomer`, `btn_selectActionPerformed`, `btn_updateActionPerformed` | First name, last name and contact number are required. Names are uppercased, contact formatting characters are removed, optional email is lowercased. Search uses Person and resolves the linked Customer. Duplicate contact is checked during add. Update prompts confirmation. CustomerModal can select a customer for sale, service-order or refurbished-sale workflows. |
| Customer persistence | `controllers/CustomerController.java`: `getAllCustomers`, `addCustomer`, `updateCustomer`, `deleteCustomer`, `getCustomer`, `getCustomerById`, `searchCustomerByContactNo`; `dao/CustomerDAO.java`: `getAllCustomerDAO`, `addCustomerDAO`, `updateCustomerDAO`, `deleteCustomerDAO`, `getCustomerDAO`, `getItemCustomerDAO`, `searchCustomerByContactNoDAO`; `models/Customer.java`; `models/Person.java` | Customer references Person and Company. Add/update persist Person and Customer together. DAO deletion hard-deletes Customer only. `Person.email` is nullable and declared unique in JPA, but DDL has no matching unique index. Customer's Company FK is nullable in DDL. Contact number has no unique constraint. |
| Person lookup | `controllers/PersonController.java`; `dao/PersonDAO.java`: `searchPersonDAO`, contact lookup | Search matches first name, last name, email and contact using case-insensitive/partial matching where applicable. Legacy search is not Company-scoped. Exact contact lookup is used for duplicate checks. |
| Product/Service screen | `views/ProductServiceView.java`: `loadProdServListTable`, `getProdServFields`, add/update/delete handlers; `views/modals/ProductServiceModal.java`: `loadProdServListTable`, live search, row selection | Form requires name, price and a selected category. Legacy category choices are `PRODUCT`, `SERVICE`, `OTHER`. Quantity and minimum quantity default to 1 when blank. Name/note are uppercased. Add checks duplicate name; update/delete require confirmation. Delete is hard delete. Modal selects catalogue rows for order lines. |
| Product/Service persistence | `controllers/ProductServiceController.java`; `dao/ProductServiceDAO.java`: all/min-stock/add/update/delete/search/order-search/exists/get-by-id methods; `models/ProductService.java` | Model has integer `qty` and `minQty`, `double price`, free-text `category`, note and Company relation. The JPA Company mapping is `@OneToOne`, while DDL permits multiple items per Company. Minimum-stock DAO uses hardcoded `qty <= 2` rather than `minQty`. Some searches and ID lookups omit Company scope; one search query's OR predicates are not grouped before its Company predicate. |
| Fault catalogue | `views/FaultView.java`: list/search/form/add/update/delete handlers; `controllers/FaultController.java`; `dao/FaultDAO.java`: all/CRUD/search/order-search/exists methods; `models/Fault.java` | Fault form requires description only. Search matches description. Duplicate check is exact description. CRUD includes hard delete. Model has ID and description only; it has no Company, code or active fields. DDL separately has nullable unique `FAULT.CODE`, which is absent from the model/view. |
| Operational selection | `views/NewSaleView.java`: `resolveCustomer`, `validateRequiredFields`, `searchProdServ`; `views/NewServiceOrderView.java`: `getOrderFields`, customer resolution, product/fault search and line handlers; `views/modals/ProductServiceModal.java`: row selection | Sale and service-order creation can resolve an existing customer by contact or carry a transient customer. Sales and service orders select ProductService rows; service orders additionally select Fault rows. These flows depend on preserving historical line references when catalogue records later change. |

## Confirmed target decisions already recorded

- One Company owns shared Customer, catalogue, category and Fault data; Shop is the operational context. See `specs/02-architecture/domain-model.md`, `database-model.md`, and `product-owner-decisions.md`.
- Customer requires first name, last name and phone; email is optional. Employee contact data may repeat and is not automatically matched to existing People. That employee decision does not by itself settle customer duplicate rules.
- Product only has stock, as whole units. Stock is per Shop, and quantity-tracked on-hand may be negative. See `product-owner-decisions.md` and `database-model.md`.
- The role matrix, updated by the Product Owner on 2026-11-13, allows TECHNICIAN to create/edit customers, Products, Services, categories, sale prices and Fault catalogue entries, and create/edit ServiceOrders. Refunds remain Manager-only. See `employee-access-and-roles.md`.
- Product Owner decisions for CAT-01: remove `OTHER` and do not migrate those rows; SKU is optional and, when present, is uppercase-normalized `SKU-` + eight alphanumeric characters, Company-unique and reserved after deactivation; block duplicate Company customers by E.164 phone, with Irish input default and an international-number checkbox; allow repeated customer emails; permit Technician Company-directory reads; set low-stock minimum per Shop; deactivate records from new operations while retaining history; permit Manager/Technician reactivation; existing drafts may display linked inactive records but may not add another reference; use Fault description as primary display/search/selection value and automatically generate immutable Company-unique `FLT-` + sequence codes, padded to at least six digits and never reused. Remaining phone-display details are non-blocking.
- Soft deletion is the v1 baseline; the exact retention period remains open. See `product-owner-decisions.md`.

## Confirmed legacy defects or mismatches

- Customer listing/search and some customer lookups are not Company-scoped.
- ProductService search OR grouping, order search, duplicate check and ID lookup do not consistently enforce Company scope.
- ProductService's legacy `OTHER` choice and rows are excluded from the target; they are not migrated.
- Legacy minimum-stock reporting ignores the stored `minQty` value and uses a fixed threshold of 2.
- Fault DDL's `code` and JPA/UI's missing code disagree; target Fault uses description as the primary selection value and also stores code.
- Person email uniqueness differs between JPA and the supplied DDL. Customer contact numbers are checked in the UI but not uniquely constrained.
- Customer/ProductService/Fault legacy delete methods hard-delete; the approved target baseline is soft delete.

## Evidence limitations

The legacy schema audit is based on the supplied schema-only MySQL dump documented in `database-schema-audit.md`; it contains no production data and is not a PostgreSQL target schema. The legacy repository did not provide that dump at the inspected repository path. No live customer/catalogue records or migration mapping were inspected.

## Open product rules

See the CAT-01 draft SPEC for remaining details before approval and the separately deferred customer-retention schedule. Phone display/error copy is a non-blocking UI detail.
