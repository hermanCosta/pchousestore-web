# Design System — PC House back-office

**Status:** DRAFT v17 — owner-directed application shell and navigation update; website observations are verified and palette values remain approximate.

## Product direction

A practical, modern back-office for PC House's multi-branch computer retail, refurbishment and repair operation. It is not a clone of the public e-commerce storefront. Operational speed, data readability and traceability take priority.

## Brand reference

Use the [PC House Brand Identity](brand-identity.md) as the source of design tokens and logo guidance. The observed identity is blue-led: deep navy navigation/header, brighter blue actions, cyan/blue wordmark accent and orange sale badges. Approximate CSS tokens are in [pc-house-theme.css](pc-house-theme.css).

The owner-supplied provisional logo assets are preserved in [brand-assets](brand-assets/README.md). Use pc-house-logo-primary-transparent.png as the primary mark. The current application serves its copy from src/PcHouseStore.Api/wwwroot/assets/pc-house-logo.png; keep one centrally managed UI asset and do not replace it with the third-party ILAC directory logo.

## Application shell and primary navigation

The authenticated workspace uses a two-level horizontal header inspired by the public [PC House website](https://www.pchouse.ie/), adapted for back-office workflows rather than copied as an e-commerce storefront.

### Brand and Shop-context bar

- Use a deep-navy first row.
- On the left, render the owner-supplied primary logo mark with the exact adjacent wordmark **PC House**. Do not render the current text **PC HOUSE STORE** in the application header.
- On the right, replace the reference website's search field with the address of the currently authenticated Shop. Use the shopAddress value returned by the Shop session API; never hard-code a branch address or fall back to Company/Employee address data.
- Show a location-pin icon beside the address. If the active Shop address is null/empty, show the neutral text **Shop address not configured**.
- Keep the settings hamburger trigger at the right edge of this first row.
- Do not display a persistent employee name, code or role in the shell. The session identity remains the Shop.
- Do not add a global search control in the space occupied by the Shop address.

### Primary navigation row

- Place a horizontal primary navigation row immediately below the brand/context row, using the site's deep-blue/navy treatment and accessible active/hover states.
- **Home** is a direct navigation item and the default destination after successful Shop sign-in. Dashboard KPIs and the full Home page content are outside this shell specification and require their own feature scope.
- **Catalogue** is a dropdown navigation item. It contains the existing catalogue destinations: Customers; Products and Services; Product Categories; Fault Catalogue.
- Selecting a Catalogue entry navigates to the corresponding existing screen and closes the dropdown. The selected destination has a visible active state.
- Catalogue destinations must no longer be duplicated inside the settings hamburger.

### Settings hamburger

Keep the hamburger as the Shop/application settings menu, not as the primary feature navigation.

- **SETTINGS:** Shop profile; Shop access credential.
- **EMPLOYEES:** Change my password; Load employees; Create employee; Employee directory.
- **SESSION:** Sign out.
- Shop profile remains available from this menu and continues to display the active Shop's full profile fields (name, address, phone and Company email where available).
- Preserve existing Manager credential challenge, Shop credential rotation/status behavior, employee administration and sign-out behavior. This layout change does not change their authorization rules.
- Do not move Customers, Products and Services, Product Categories or Fault Catalogue back into the hamburger.

### Responsive behavior

- Desktop: keep the brand/context row and horizontal primary navigation visible.
- Narrow screens: keep the logo/wordmark and settings trigger reachable; allow the active Shop address to wrap or move to a compact second line rather than replacing it with search.
- The primary navigation may become horizontally scrollable or use a separate primary-navigation disclosure. Do not merge Catalogue navigation into the Settings hamburger.
- Catalogue dropdown and Settings menu must remain keyboard accessible and usable on touch devices.

### Content area

- Keep the application content on a light neutral canvas with readable forms and business tables.
- Preserve the existing screen components and their loading, empty, error and success states while moving their entry points into the new navigation.
- The Home destination is a shell landing point in this change; do not invent dashboard metrics or operational actions that do not have an approved specification.

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
- Final responsive treatment for very narrow devices, dashboard KPIs and component library.


## Print and document visual system

- Customer documents use a consistent PC House header with the approved provisional logo, legal/business identity, active Shop address/contact and document number.
- Screen colours must remain readable in grayscale; critical values cannot rely on colour alone.
- A4 layouts use a formal document hierarchy, readable tables, page numbers and preserved signature space.
- 80 mm receipts use compact typography, restrained logo size, clear totals and minimal decoration; never truncate payment, refund, warranty or customer-reference values.
- Customer-facing receipts show the transaction reference, date/time, Shop, line details and tender breakdown as applicable.
- Internal-only annotations and notes must not appear on customer copies.
- The document catalogue, fields, triggers and output rules live in [Reports, Receipts and Document Printing](../02-architecture/reports-and-document-printing.md).
