# Frontend Guidelines

**Status:** DRAFT

## Stack

- React
- TypeScript strict mode
- Vite
- React Router
- TanStack Query
- Form library and schema validation to be selected during foundation design.
- Vitest and React Testing Library.

## Structure

- Organize by business feature.
- Keep shared UI primitives in components.
- Keep route composition in app.
- Centralize HTTP configuration and error mapping.
- Keep server state in TanStack Query.
- Keep local UI state close to the component.

## UX behavior

Every data view must define loading, empty, error and success states. Forms must provide inline validation, keyboard support and accessible labels. Destructive actions require confirmation and clear feedback.

## Brand and assets

- Follow [PC House Brand Identity](../03-design/brand-identity.md) and import shared CSS tokens from `../03-design/pc-house-theme.css` into the application theme.
- Use the owner-approved logo from the single `public/brand/` asset location. Do not hotlink the third-party image CDN in production and do not generate substitute marks.
- The palette values are approximate until the owner approves the official master artwork.

## Quality

- Strict TypeScript; avoid any.
- Responsive layouts.
- Semantic HTML and accessible interactions.
- No business rules that must be authoritative only in the browser.
- Test critical user flows and form behavior.
