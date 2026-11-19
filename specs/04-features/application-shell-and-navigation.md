# Feature: Application shell and primary navigation

**Status:** APPROVED — owner-approved layout direction 2026-11-18; implementation pending  
**Type:** REDESIGN — new back-office navigation experience, not an inherited Java Swing workflow.  
**Depends on:** Existing ShopSession response, approved brand assets, customer/catalogue screens and employee/shop settings screens.

## 1. Objective

Replace the current single brand header plus administration hamburger layout with a PC House back-office shell inspired by the public website's horizontal header and category navigation.

The shell must make primary working areas accessible from a horizontal navigation row, keep Shop/application configuration in the hamburger menu, and make the active Shop's address visible in the header.

## 2. Reference and current implementation

### Public visual reference

- Official website: https://www.pchouse.ie/
- The website uses a dark blue header area with a brand region and a search field, followed by a horizontal navigation row (Home and product categories).
- The back-office adopts the two-level visual hierarchy, but replaces the search field with the active Shop address and uses operational navigation rather than storefront product categories.
- Website category links are visual inspiration only; they are not automatically target back-office modules.

### Current frontend evidence

| File | Existing behavior |
|---|---|
| frontend/pchousestore-web/src/app/App.tsx | Brand renders the logo and text PC HOUSE STORE; the hamburger contains Shop, Employee and Catalogue groups; default active page is shop-profile. |
| frontend/pchousestore-web/src/app/App.tsx | ShopSession already includes shopName, shopAddress and shopPhone. |
| frontend/pchousestore-web/src/app/App.tsx | Existing catalogue destinations are customer-directory, catalog-items, product-categories and fault-catalogue. |
| frontend/pchousestore-web/src/styles.css | Existing .app-header, .admin-menu and .admin-menu-panel styles are the starting point for the shell update. |
| specs/03-design/brand-identity.md | The provisional primary logo is the owner-supplied transparent mark; the adjacent wordmark must be rendered as UI text PC House. |

## 3. Approved layout requirements

### 3.1 Brand and active-Shop context row

1. Use a deep-navy horizontal top row.
2. Render the existing owner-supplied primary logo asset on the left.
3. Render the exact adjacent brand text **PC House**. Replace the current PC HOUSE STORE text.
4. Replace the reference website's search-field position with a location display containing a location-pin icon and the active Shop address.
5. Read the address only from ShopSession.shopAddress, returned by the authenticated session endpoint. Never hard-code the address or substitute Company or Employee address values.
6. If shopAddress is null, empty or whitespace, display **Shop address not configured**.
7. Keep the hamburger trigger at the right of this row.
8. Do not display a persistent Employee name, employee code or employee role. The persistent shell context remains the authenticated Shop.
9. Do not introduce a global search control as part of this header change.

### 3.2 Primary navigation row

The primary navigation is a separate horizontal row immediately below the brand/context row.

| Item | Behavior |
|---|---|
| Home | Direct navigation to the default signed-in workspace. It becomes the initial destination after successful Shop sign-in. |
| Catalogue | Opens a dropdown with the current catalogue screens listed below. |

Catalogue dropdown entries:

- Customers → customer-directory
- Products and Services → catalog-items
- Product Categories → product-categories
- Fault Catalogue → fault-catalogue

When a destination is selected:
- Navigate to the existing corresponding screen without changing its business behavior.
- Close the dropdown.
- Mark the selected destination as active.
- Preserve the selected screen when the user interacts with its forms and dialogs.

Catalogue entries are removed from the Settings hamburger to avoid duplicate navigation.

The Home link is part of this shell change. Dashboard cards, KPIs, sales figures, operational queues and other Home content are not defined here; do not fabricate those elements. A separate Home/dashboard SPEC is required before adding operational widgets.

### 3.3 Settings hamburger

The hamburger remains the entry point for Shop/application settings and secondary administration.

| Group | Entries |
|---|---|
| SETTINGS | Shop profile; Shop access credential |
| EMPLOYEES | Change my password; Load employees; Create employee; Employee directory |
| SESSION | Sign out |

Rules:

- Shop profile remains available under SETTINGS and displays the full active Shop profile (Shop name, address, phone and Company email where available).
- Shop access credential retains the current Manager credential challenge and existing rotate/enable/disable workflows.
- Employee administration and sign-out retain their existing behavior and authorization.
- Customers, Products and Services, Product Categories and Fault Catalogue must not appear in this hamburger.

## 4. Data and API contracts

No backend/API change is required for this layout.

The shell must use the existing session response fields:
- shopName — available for the Shop profile/settings view.
- shopAddress — displayed in the top header in place of search.
- shopPhone and companyEmail — remain available on the Shop profile/settings view.

No store address may be sourced from static frontend configuration, the public website, Company defaults or employee profile data.

## 5. Visual and responsive requirements

- Deep navy/blue header and navigation treatment consistent with PC House Brand Identity and Design System.
- Use the existing owner-supplied primary logo asset; preserve its aspect ratio and transparency.
- Use PC House as the wordmark. Do not use the third-party ILAC directory logo as the application logo.
- Location-pin icon precedes the address. Long addresses may truncate visually on wide layouts but the complete value must remain available via accessible text/title.
- On narrow screens, keep the brand and settings trigger reachable; the address may wrap to a compact second line.
- Primary navigation may horizontally scroll or use a separate primary-navigation disclosure on small screens. Do not merge Settings and Catalogue navigation.
- Both dropdowns must support keyboard and touch interaction, visible focus, Escape-to-close, correct aria-expanded and semantic navigation landmarks.
- The active primary destination must be identifiable without relying on colour alone.

## 6. Out of scope

- New customer/catalogue business rules or API endpoints.
- Dashboard KPIs or operational widgets on Home.
- New search functionality.
- Changes to ShopSession identity, OIDC, authorization or EmployeeActionAuthorization.
- Changes to the contents or authorization of Shop profile, Shop credential and Employee administration screens.
- Adding unimplemented modules (sales, service orders, stock, finance or reports) as active menu items.

## 7. Acceptance criteria

- [ ] AC-01: After Shop sign-in, the application shows a two-level horizontal shell.
- [ ] AC-02: The brand area shows the existing primary logo beside the exact text PC House.
- [ ] AC-03: The old PC HOUSE STORE wordmark is no longer displayed in the shell.
- [ ] AC-04: The header displays the address for the authenticated Shop from shopAddress instead of a search field.
- [ ] AC-05: Switching to another Shop session displays that Shop's own address; no address is carried over from the previous session.
- [ ] AC-06: An absent Shop address displays Shop address not configured and does not use another address as fallback.
- [ ] AC-07: Home is the default signed-in destination and can be reached from the primary navigation.
- [ ] AC-08: Catalogue opens a dropdown containing Customers, Products and Services, Product Categories and Fault Catalogue.
- [ ] AC-09: Selecting each Catalogue entry opens its existing screen, closes the dropdown and marks the entry active.
- [ ] AC-10: Catalogue entries no longer appear in the Settings hamburger.
- [ ] AC-11: The Settings hamburger contains Shop profile and Shop access credential under SETTINGS.
- [ ] AC-12: Employee administration actions and Sign out remain available and retain their current behavior.
- [ ] AC-13: The shell remains usable at desktop, tablet and mobile widths without hiding the Shop address entirely or merging Settings and Catalogue navigation.
- [ ] AC-14: The shell exposes no persistent Employee identity.

## 8. Test scenarios

- Render the brand image and wordmark; verify the exact accessible label.
- Render a valid ShopSession and assert the exact address appears in the header.
- Render a ShopSession with null/blank address and assert the neutral fallback.
- Change the active Shop session and verify the address updates to the new Shop.
- Navigate Home → Catalogue dropdown → each of its four destinations.
- Verify the dropdown closes after selection and on Escape.
- Verify current page/active state is available semantically.
- Verify Shop profile and Shop access credential are present in Settings and Catalogue entries are absent there.
- Verify employee menu actions and Sign out remain reachable.
- Verify responsive layout and keyboard operation.

## 9. Approval

- Product-owner layout direction: APPROVED — 2026-11-18.
- Technical approval: PENDING implementation review.
- API/data-model impact: NONE — existing ShopSession DTO already supplies the required Shop address.
