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

The first row contains the existing PC House logo/wordmark, the active Shop name when authenticated, and the hamburger trigger aligned to the right. The second row contains the operational navigation. The previous search bar is not part of this shell.

The Shop name is sourced from the authenticated session response (`ShopSession.shopName`); do not hard-code it or substitute Company or Employee data.

## 3. Approved layout and navigation

### 3.1 First header row

1. Render the existing PC House logo and current brand wordmark.
2. When a Shop session is active, display the active Shop name in the header.
3. Keep the hamburger trigger aligned to the right of this row.
4. The hamburger is the entry point for Shop settings, Employee settings and session actions.
5. Do not add a global search field.
6. Do not display a persistent Employee name, code or role. The persistent authenticated context is the Shop.

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
- Use two distinct horizontal rows: brand/Shop/menu first, operational navigation second.
- Keep the hamburger at the right of the first row.
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
- [ ] AC-08: Automated tests cover menu contents, menu selection and sign-out initiation.
- [ ] AC-09: Automated tests cover each operational navigation destination and active state.
- [ ] AC-10: Keyboard interaction, focus visibility and menu dismissal are verified.
- [ ] AC-11: Desktop, tablet and mobile layouts are verified.
- [ ] AC-12: Navigation preserves the active Shop session and does not expose persistent Employee identity.

## 8. Remaining test scenarios

- Render the authenticated shell and verify the logo/wordmark, active Shop name and right-aligned hamburger.
- Verify the hamburger contains the three approved groups and all specified entries.
- Select each settings item and verify the correct existing screen opens and the menu closes.
- Verify Sign out starts the existing OIDC sign-out flow.
- Select each of the four operational destinations and verify its screen and active state.
- Verify the global search field is absent.
- Verify navigation does not clear or replace the active Shop session.
- Verify keyboard and touch operation, focus visibility and menu dismissal.
- Verify the layout at desktop, tablet and mobile viewport widths.

## 9. Approval

- Product-owner layout direction: APPROVED.
- Implementation: OWNER-CONFIRMED — 2026-11-26.
- Frontend build: REPORTED SUCCESSFUL BY OWNER — 2026-11-26; not independently rerun during this documentation update.
- Automated, responsive and accessibility validation: PENDING.
- API/data-model impact: NONE.
