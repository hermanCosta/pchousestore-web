# Feature: Application shell and primary navigation

**Status:** IMPLEMENTED — owner-confirmed 2026-11-26; automated and responsive accessibility validation remains pending.  
**Type:** REDESIGN — back-office shell inspired by the public PC House website; not an inherited Java Swing workflow.  
**Depends on:** Existing ShopSession response, approved brand asset, catalogue screens and Shop/Employee settings screens.

## 1. Objective

Provide a consistent PC House back-office shell with:
- the PC House brand and active Shop identity in the first header row;
- a Settings hamburger on the right of that row;
- operational navigation in a separate second row;
- no global search field.

The shell must retain the existing Shop-session authentication boundary. It must not introduce a persistent signed-in Employee identity.

## 2. Current implementation baseline

The product owner confirmed the implemented header reorganisation and successful frontend build on 2026-11-26. The current implementation is in:

| File | Responsibility |
|---|---|
| `frontend/pchousestore-web/src/app/App.tsx` | Shop session loading, brand/header composition, Settings menu, primary navigation and existing screen selection. |
| `frontend/pchousestore-web/src/styles.css` | Header, menu and primary-navigation presentation. |
| `frontend/pchousestore-web/src/auth/oidc.ts` | Shop OIDC client and access-token retrieval. |

The first row contains the PC House logo mark on the left, with the brand name and tagline immediately to its right. On the right side, the active Shop name and Shop address are displayed together in one identity block, followed by the hamburger trigger at the far right. The second row contains the operational navigation. The previous search bar is not part of this shell.

The Shop name is sourced from the authenticated session response (`ShopSession.shopName`); do not hard-code it or substitute Company or Employee data.

## 3. Approved layout and navigation

### 3.1 First header row

1. Render the PC House logo mark at the left of the header.
2. Place the owner-provided PC HOUSE wordmark artwork image immediately to the right of the primary logo mark. The artwork already contains the wording “PC HOUSE” and “SELLS AND REPAIRS LAPTOPS”; use the image itself, not separately rendered text. The asset is `frontend/pchousestore-web/public/assets/pc-house-wordmark.webp`, derived from the supplied image with its aspect ratio preserved. Do not recreate, retype or overlay the embedded words.
3. When a Shop session is active, display the active Shop name and Shop address together in the same Shop identity block on the right side of the header. Do not separate the address into another header region or place it in the brand lockup.
4. Keep the hamburger trigger at the far right of the first row, after the Shop identity block.
5. The hamburger is the entry point for Shop settings, Employee settings and session actions.
6. Do not add a global search field.
7. Do not display a persistent Employee name, code or role. The persistent authenticated context is the Shop.

### 3.2 Settings hamburger

The menu contains the following groups and entries:

| Group | Entries |
|---|---|
| SHOP | Shop profile; Shop access credential |
| EMPLOYEE SETTINGS | Change my password; Load employees; Create employee; Employee directory |
| SESSION | Sign out |

Rules:
- Shop profile displays the active Shop's available details.
- Shop access credential retains its existing workflow and authorization behavior.
- Employee administration retains its existing screens and authorization behavior.
- Sign out retains the existing OIDC sign-out behavior.
- Operational catalogue destinations must not be duplicated in this menu.
- Selecting a menu item closes the menu and selects the corresponding existing screen.

### 3.3 Second-row operational navigation

The second row is a separate navigation landmark labelled **Main navigation** and contains:

| Label | Existing destination |
|---|---|
| Customers | `customer-directory` |
| Products & Services | `catalog-items` |
| Categories | `product-categories` |
| Fault catalogue | `fault-catalogue` |

Rules:
- Each item opens its existing screen without changing its business behavior.
- The selected destination is visibly marked active and exposes `aria-current="page"`.
- Navigation does not reset the active Shop session.
- Do not add unimplemented modules (Sales, Service Orders, Stock, Finance or Reports) until their feature specifications authorize them.
- Do not add a Dashboard/Home destination or fabricate dashboard content as part of this shell.

## 4. Data and API contracts

No backend/API change is required for the shell layout.

The shell uses the existing authenticated Shop session:
- `shopName` — active Shop name displayed in the header.
- `shopAddress`, `shopPhone` and `companyEmail` — remain available to the Shop profile screen.

Do not source Shop details from static frontend configuration, the public website, Company defaults or Employee profile data.

The shell must not change OIDC, token handling, ShopSession identity, API authorization or employee-attribution rules.

## 5. Visual and responsive requirements

- Retain the established PC House visual identity and existing logo asset.
- In the first row, place the primary logo mark on the left and the owner-provided PC HOUSE wordmark artwork image immediately to its right. Use the artwork as an image asset; do not recreate its embedded wording with HTML text.
- Keep the active Shop name and address together in one identity block on the right side of the first row; place the hamburger at the far right, after that block.
- Use two distinct horizontal rows: brand/Shop/menu first, operational navigation second.
- Do not restore the removed search bar.
- Keep the Shop identity and menu reachable at narrow viewport widths.
- Keep the operational navigation usable at desktop, tablet and mobile widths; responsive behavior must be verified rather than assumed.
- Provide visible keyboard focus and semantic navigation/menu controls.
- The active operational destination must be identifiable without relying on colour alone.
- Support keyboard and touch interaction, including a predictable way to close the menu.

## 6. Out of scope

- New customer/catalogue business rules or API endpoints.
- Dashboard KPIs, sales figures, operational queues or Home content.
- Global search.
- Changes to ShopSession identity, OIDC, authorization or employee credential validation.
- Changes to Shop profile, Shop credential or Employee administration business behavior.
- Adding unimplemented business modules as active navigation destinations.

## 7. Acceptance criteria and validation status

The owner confirmed the reorganised header and successful frontend build on 2026-11-26. That confirmation does not imply that every automated, responsive or accessibility test below has run.

- [x] AC-01: The application uses a two-row header/navigation structure.
- [x] AC-02: The first row displays the existing PC House logo/wordmark and active Shop name.
- [x] AC-03: The hamburger is positioned at the right of the first row.
- [x] AC-04: Shop and Employee settings plus Sign out are available from the hamburger.
- [x] AC-05: Operational navigation is displayed in the separate second row.
- [x] AC-06: The global search bar is absent.
- [x] AC-07: The frontend build was reported successful by the owner on 2026-11-26.
- [x] AC-08: Frontend tests have been added for menu contents, settings-menu selection and sign-out initiation; execution remains pending.
- [x] AC-09: Frontend tests have been added for each operational navigation destination and active state; execution remains pending.
- [ ] AC-10: Keyboard interaction, focus visibility and menu dismissal are verified.
- [ ] AC-11: Desktop, tablet and mobile layouts are verified.
- [ ] AC-12: Navigation preserves the active Shop session and does not expose persistent Employee identity.
- [x] AC-13: The supplied wordmark artwork is displayed as an image beside the primary logo; embedded wording is not recreated as HTML text.
- [x] AC-14: The active Shop name and address are grouped in one identity block in the first header row.

## 8. Automated test implementation and remaining scenarios

Automated frontend tests are defined in `frontend/pchousestore-web/src/app/App.test.tsx` using Vitest, React Testing Library and user-event. The current suite covers authenticated shell rendering, the approved settings-menu groups and entries, operational navigation destinations and active states, closing the settings menu after a selection, and initiation of the existing OIDC sign-out flow. The frontend CI workflow now invokes `npm run test` before the production build.

The tests and CI change have been committed but have not yet been executed or observed in a CI run. Therefore, test success is NOT YET VERIFIED.

Remaining validation:

- Render the authenticated shell and verify the primary logo mark with the supplied PC HOUSE wordmark artwork image to its right, the active Shop name and address grouped together, and the right-aligned hamburger. A test assertion covers the wordmark image source and Shop address.
- Select each remaining settings item and verify the correct existing screen opens and the menu closes.
- Verify the Shop identity/address grouping and brand lockup after their implementation is aligned with the approved reference.
- Verify the global search field is absent.
- Verify navigation does not clear or replace the active Shop session.
- Verify keyboard and touch operation, focus visibility and menu dismissal.
- Verify the layout at desktop, tablet and mobile viewport widths.

## 9. Approval

- Product-owner layout direction: APPROVED.
- Implementation: OWNER-CONFIRMED — 2026-11-26.
- Frontend build: REPORTED SUCCESSFUL BY OWNER — 2026-11-26; not independently rerun during this documentation update.
- Automated test suite: IMPLEMENTED; execution and CI result PENDING. A CI run triggered by the implementation commits is in progress.
- Responsive and accessibility validation: PENDING.
- API/data-model impact: NONE.
