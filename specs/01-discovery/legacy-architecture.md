# Legacy Architecture — Initial Findings

**Status:** DISCOVERY  
**Source:** https://github.com/hermanCosta/pchousestoremvn  
**Reference branch:** `main`

## 1. Repository structure verified

The recursive Git tree returned 309 entries, including 280 blobs, 29 directories and 123 Java source files.

Main Java package:

`src/main/java/com/pchouse/pchousestoremvn/`

Observed packages:

- `common/`
- `controllers/`
- `dao/`
- `enums/`
- `exception/`
- `models/`
- `reports/`
- `util/`
- `views/`
- `views/modals/`

## 2. Technology evidence

From `pom.xml`:

- Java compiler release: 21
- Maven packaging: JAR
- Jakarta Persistence API: 3.1.0
- Hibernate ORM: 6.4.4.Final
- MySQL Connector/J: 8.4.0
- Hibernate Validator: 8.0.1.Final
- Apache POI: 5.2.5
- JFreeChart: 1.5.3
- JasperReports: 6.20.0
- iText: 5.5.13.3
- SwingX: 1.6.5-1
- FlatLaf: 3.4
- JCalendar: 1.4

## 3. Application startup

**CONFIRMED:** `MainApp.main` applies FlatDarkLaf, configures common Swing behavior and opens `LoginView`.

Source: `src/main/java/com/pchouse/pchousestoremvn/MainApp.java`

## 4. Persistence

**CONFIRMED:** `persistence.xml` defines a RESOURCE_LOCAL persistence unit named `pcHouseStorePU`, uses the MySQL JDBC driver and configures Hibernate schema action as `none`.

Source: `src/main/resources/META-INF/persistence.xml`

The file contains credential material. Do not copy, repeat or reuse it. Rotate exposed credentials and remove secrets from repository history through a controlled security procedure.

## 5. Layering observed

- Swing Views invoke Controllers.
- Controllers delegate to DAOs.
- DAOs use JPA EntityManager through `JPAUtil`.
- Domain objects are under `models`.
- Reports are implemented with JasperReports templates.

This is a descriptive observation, not an endorsement of the same layering in the web solution.

## 6. Initial module candidates

- Authentication and company context
- People, customers and employees
- Product/service catalog and fault catalog
- Sales and sale payments
- Service orders, faults, items and payments
- Refurbishment and refurbished sales
- Deposits, refunds and cash movements
- Daily cash closing
- Reports and receipts

## 7. Remaining discovery

- Inspect all DAOs and transaction boundaries.
- Map every entity relationship and database constraint.
- Inspect remaining views and modal workflows.
- Inspect status transitions and payment rules.
- Inspect JasperReports parameters and output requirements.
- Identify any scheduled tasks, external integrations or printer dependencies.
