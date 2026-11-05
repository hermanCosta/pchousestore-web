# Risks and Findings

**Status:** DISCOVERY

## R-001 — Secrets in persistence configuration

**Severity:** Critical

Source: `src/main/resources/META-INF/persistence.xml`

Credential values appear in the tracked file, including a commented remote-database connection. Do not copy them into the new application, documentation, issue comments or configuration. Rotate any exposed credentials, review Git history and follow a controlled cleanup. Commenting out a secret does not make it safe.

## R-002 — Business behavior coupled to Swing views

**Severity:** High

Sources:
- `views/NewSaleView.java` (~996 lines)
- `views/NewServiceOrderView.java` (~1467 lines)

The UI classes include validations, customer resolution, item handling and calculations. Extract behavior into tested application/domain services.

## R-003 — Monetary values use floating-point types

**Severity:** High

Source: `models/Sale.java`, `models/ServiceOrder.java`

The models use double/Double for monetary-looking values. The target must use C# decimal and MySQL DECIMAL with explicit precision and scale.

## R-004 — Potential cardinality mismatch

**Severity:** High

Source: `models/ServiceOrder.java`

Customer, Device, Employee and Company are annotated OneToOne. Validate actual uniqueness constraints and the data model; employee/customer/company reuse patterns may indicate that the annotation does not express intended cardinality.

## R-005 — Persistence unit class-name inconsistency

**Severity:** High

Source: `src/main/resources/META-INF/persistence.xml`

The persistence XML lists `models.Customerr`, while the source model class inventory includes `Customer`. Validate whether this is stale configuration or a runtime defect.

## R-006 — Authentication implementation needs security redesign

Sources: `views/LoginView.java`, `common/CommonExtension.java`, `controllers/EmployeeController.java`

The login view logs username, password length and an encrypted password string. The sale/order views request an employee password for operator authorization. Inspect password transformation and storage before specifying the new authentication model. Do not port reversible/legacy password logic.

## R-007 — Report and print behavior may be operationally important

Sources: `src/main/resources/com/pchouse/pchousestoremvn/reports/`, `util/ReportGenerator.java`, `util/PrinterUtil.java`

Inventory each Jasper template, output type, parameters, printing behavior and business trigger. Decide which reports become browser PDF downloads, previews or printable documents.

## R-008 — Date and time semantics

The legacy models mix java.util.Date and LocalDateTime. Define database timezone, business timezone, date-only versus timestamp semantics and audit behavior explicitly in the target.
