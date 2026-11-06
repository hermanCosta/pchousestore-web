# Design System — PC House back-office

**Status:** DRAFT v16 — brand reference extracted from pchouse.ie; palette values are approximate until compared with owner-provided master artwork.

## Product direction

A practical, modern back-office for PC House's multi-branch computer retail, refurbishment and repair operation. It is not a clone of the public e-commerce storefront. Operational speed, data readability and traceability take priority.

## Brand reference

Use the [PC House Brand Identity](brand-identity.md) as the source of design tokens and logo guidance. The observed identity is blue-led: deep navy navigation/header, brighter blue actions, cyan/blue wordmark accent and orange sale badges. Approximate CSS tokens are in [pc-house-theme.css](pc-house-theme.css).

The official logo reference and source are in [brand-assets](brand-assets/README.md). Do not fabricate alternate logo variants. The source image is currently linked from the ILAC directory; copy an owner-approved source file into `public/brand/` before production.

## Layout

- Collapsible left navigation with a deep-navy brand surface.
- Top bar with active Shop name, business date and session status; no persistent employee identity in the Shop session.
- A separate short-lived employee authorization indicator only while an individual action is being confirmed; do not display one employee as the ambient session owner.
- Breadcrumbs for nested workflows.
- Responsive content area, with data tables optimized for desktop and usable on tablet.
- Tables with search, filters, sorting, pagination and explicit empty/loading/error states.
- Consistent forms, confirmation dialogs and audit history panels.

## Visual tokens

- Light content theme by default.
- `pc-navy-900` for global navigation and Shop context.
- `pc-blue-600` for primary actions, links and active navigation.
- `pc-cyan-400` sparingly for visual emphasis.
- `pc-orange-500` only for promotions/sale labels; do not use orange as a generic error/warning colour.
- White cards over a pale neutral canvas.
- Clear hierarchy and readable typography.
- Consistent spacing, radii, borders and focus states.
- Dense business tables balanced with generous form spacing.
- Status labels always include text or icon in addition to colour.

## Typography

The website source does not provide an authoritative typeface specification. Use a modern sans-serif stack consistently (Inter where bundled, with system fallbacks) as an application design choice, not a claim about the public brand font.

## Accessibility

- Keyboard navigation and visible focus.
- Semantic controls and labels.
- WCAG contrast checks for text and interactive controls.
- Colour is never the only status signal.
- Reduced-motion support.

## Brand-content boundaries

- Product/service category labels and current branch information may be seeded from the website only after owner confirmation.
- The current website's “1-Year Warranty on Laptops!” is a marketing statement; do not assume it applies to every product category or overrides the application's separately approved refurbished warranty terms.
- “Same-day, on-site repairs” appears in the ILAC directory description. Do not encode it as a guaranteed SLA without an operational decision.
- Campaign banners and warranty badges are not UI logos; do not reuse them as persistent application branding.

## Still to approve

- Exact official colour codes from original brand artwork/CSS.
- Original horizontal logo source and any approved compact/monochrome variants.
- Final address and display name of the ILAC Shop (public sources differ).
- Owner approval of category seed list and the initial TECHNICIAN action matrix.
- Mobile navigation behaviour, dashboard KPIs and component library.


## Print and document visual system

- Customer documents use a consistent PC House header with the approved provisional logo, legal/business identity, active Shop address/contact and document number.
- Screen colours must remain readable in grayscale; critical values cannot rely on colour alone.
- A4 layouts use a formal document hierarchy, readable tables, page numbers and preserved signature space.
- 80 mm receipts use compact typography, restrained logo size, clear totals and minimal decoration; never truncate payment, refund, warranty or customer-reference values.
- Customer-facing receipts show the transaction reference, date/time, Shop, line details and tender breakdown as applicable.
- Internal-only annotations and notes must not appear on customer copies.
- The document catalogue, fields, triggers and output rules live in [Reports, Receipts and Document Printing](../02-architecture/reports-and-document-printing.md).
