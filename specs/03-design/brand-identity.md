# PC House Brand Identity — website-derived reference

**Revision:** v17 · 5 November 2026  
**Status:** DRAFT — website observations are verified; hexadecimal palette is a close visual approximation, not an official published brand guide.

## Reference sources

- PC House official website: https://www.pchouse.ie/
- PC House listing in the ILAC Shopping Centre directory: https://www.ilac.ie/shop/pc-house
- Logo image shown as “PC House logo” on the ILAC listing: [https://images.prismic.io/hammerson/afOHysBOoF08xg9C_PC_HOUSE-112x112.png?auto=format%2Ccompress&fit=max&w=256](https://images.prismic.io/hammerson/afOHysBOoF08xg9C_PC_HOUSE-112x112.png?auto=format%2Ccompress&fit=max&w=256)
- Website capture used only to inspect the visible header/colour treatment: https://gridinsoft.com/online-virus-scanner/url/pchouse-ie

The live PC House website presents a blue-led identity: a deep blue navigation/header, a cyan/blue symbol in the PC House wordmark, white lettering on dark backgrounds, and brighter blue for promotional headlines and action links. Orange appears on `SALE` labels as a promotional signal rather than a primary brand colour.

## Owner-supplied logo assets — provisional approved set

The owner supplied three PNG logo images for the application. These files are preserved unchanged under [`brand-assets/originals/`](brand-assets/README.md). The transparent-background 248 × 273 image is the **primary provisional mark**. The 248 × 273 white-background image is an alternative for placements where a baked-in white background is acceptable. The 320 × 120 image is retained as an alternate original; it has a wide transparent canvas and should be checked in the target UI before being selected.

### Primary provisional mark

![PC House logo — owner-supplied transparent PNG](brand-assets/originals/pc-house-logo-primary-transparent.png)

### White-background alternative

![PC House logo — owner-supplied white-background PNG](brand-assets/originals/pc-house-logo-light-background.png)

These are the currently approved working assets for the product UI. They may be refined or replaced later, as requested by the owner. Do not treat the images as a finalized brand guide or infer official brand colour values solely from them. The logo artwork contains a computer symbol rather than a readable PC House wordmark; render the text **PC House** as normal UI text beside the mark where a brand name is needed.

### Usage rules

- Use `pc-house-logo-primary-transparent.png` as the default mark.
- Keep the original aspect ratio and transparent pixels; do not stretch, skew, recolour, apply filters or crop the source file.
- The transparent mark's white laptop details need sufficient contrast. Prefer a navy/blue brand surface or a light-blue panel; avoid placing it directly on a plain white surface where white details may disappear.
- Use the white-background alternative only on light surfaces where its rectangular white canvas is acceptable.
- Keep `pc-house-logo-alternate-original.png` available for future review; do not assume its wide canvas represents a complete wordmark.
- Keep the logo replaceable through one central frontend asset path. Do not duplicate copies throughout the UI.
- The former ILAC directory logo remains a third-party reference only, not the selected application logo.

## Approximate colour tokens

These values are visual implementation starting points based on the website observations and the owner-supplied provisional logo images. They are **not claimed to be exact official CSS values**. Revisit the palette when the logo is professionally refined or an official brand guide becomes available.

| Token | Approx. HEX | Intended use |
|---|---|---|
| `pc-navy-900` | `#21466F` | Main header/sidebar, dark brand surfaces |
| `pc-navy-800` | `#28547F` | Secondary dark surface / hover |
| `pc-blue-600` | `#1688C9` | Primary actions, links, selected navigation |
| `pc-blue-500` | `#239DD8` | Informational highlights and focus accents |
| `pc-cyan-400` | `#27B8E8` | Logo-adjacent accent, decorative highlights only |
| `pc-orange-500` | `#EA842B` | Sale/promotion badge only; not a general success colour |
| `pc-surface-0` | `#FFFFFF` | Cards and main content surface |
| `pc-surface-50` | `#F5F7FA` | Application canvas |
| `pc-border` | `#DCE3EA` | Dividers and control borders |
| `pc-text-900` | `#243447` | Primary text |
| `pc-text-600` | `#5D6B79` | Secondary text |

## Product and service language visible on the site

The main navigation and category sections show: MacBook, Laptop, TV, iMac, All-in-one, Desktop, Monitor, Console and Accessories. Product browsing/filter content also exposes Phone as a category. Treat these as suggested category seeds, not an immutable catalogue taxonomy.

The homepage currently advertises **“1-Year Warranty on Laptops!”**. The ILAC listing describes same-day, on-site computer and laptop repairs and lists laptops, MacBooks, iMacs, desktop PCs, monitors, TVs, consoles and accessories. These are source observations, not automatically approved legal warranty terms or a promise that every repair can be completed same day.

## Back-office design direction

The new web application is an operational back-office, not a replica of the e-commerce storefront. Apply the identity as follows:

- Light content canvas for long service records, stock tables and financial forms.
- Deep navy for global navigation and shop context.
- Blue for primary actions and active navigation.
- Cyan sparingly for decorative/brand emphasis, never as the only signal for statuses.
- Orange reserved for sale/promotion markers, not errors or generic warnings.
- Keep status labels semantic and accessible: text + icon/shape + colour.
- Prioritise dense but readable data tables, clear forms, keyboard support and responsive layouts.

## Typography

The source pages do not publish a brand type specification. Use the application's consistent sans-serif system stack (for example, Inter with system fallbacks) as a product UI choice, not as an official PC House typeface.

## Content and operational data extracted from the site

See [Website-derived business profile](../01-discovery/business-profile-from-website.md). Branch addresses, hours and phone details are snapshot data and should be owner-verified before becoming production seed data.
