# PC House — business profile extracted from public website

**Reviewed:** 5 November 2026  
**Status:** Website snapshot for owner validation; not approved as final seed data.

## Public identity and offering

- Public trading name used by the website: **Pc House / Pc House Dublin**.
- Official site: https://www.pchouse.ie/
- Public tagline visible in the site's header lock-up: **“SELLS AND REPAIRS LAPTOPS”**.
- The homepage currently highlights a one-year warranty on laptops.
- The ILAC retailer directory describes same-day, on-site computer and laptop repairs; this is marketing/listing text and must not become an unconditional service-level promise in the application.

## Product categories observed

The PC House website navigation/category panels expose the following category labels:

- MacBook
- Laptop
- TV
- iMac
- All-in-one
- Desktop
- Monitor
- Console
- Accessories

A product browsing/filter view also exposes **Phone**. Recommended seed taxonomy may include Phone as an optional category, subject to owner confirmation. Categories should remain editable in the system.

## Branch candidates from the website

The PC House homepage lists a `SHOP` and an `ILAC` / `KIOSK`. These should be modelled as two `Shop` records under the single legal `Company`, subject to the owner confirming the address of the ILAC branch.

| Proposed Shop | Address as listed on pchouse.ie | Public phone | Published opening hours |
|---|---|---|---|
| `FREDERICK_ST_SHOP` | 12A Frederick St. North, Dublin 1, Ireland | 01 563 9520; 083 012 8190 | Monday–Saturday, 10:00–18:00 |
| `ILAC_KIOSK` | Kiosk 24, iLAC Centre, Henry St., Dublin 1, Ireland | 01 547 1536; 083 055 5506 | Monday–Saturday, 10:00–19:00; Sunday, 12:00–18:00 |

**Address discrepancy to verify:** the ILAC Shopping Centre directory currently lists PC House at **Moore Mall, Moore Street, D01 E6P5**, whereas the PC House homepage calls it **Kiosk 24, iLAC Centre, Henry St.** The public phone number `083 055 5506` matches. Confirm the authoritative postal address and branch display name with the owner before seeding or printing receipts.

The ILAC directory page lists opening hours of 10:00–19:00 on weekdays and Saturday, 12:00–18:00 on Sunday. The PC House homepage lists the same hours for the ILAC location. Hours may change and must be stored as Shop configuration, not hard-coded into frontend components.

## Shared contact information shown on pchouse.ie

- Email: `info@pchouse.ie`
- WhatsApp: `+353 83 012 8190` (homepage displays `+353 083 012 8190`)
- Main shop phones: `01 563 9520` and `083 012 8190`
- ILAC/Kiosk phones: `01 547 1536` and `083 055 5506`

Normalise Irish phone numbers for storage only after owner confirmation; preserve the display format configured per branch.

## Payment methods shown publicly

The website states card payments online and card/cash payments in physical stores. This aligns with the owner's initial application tender selection: `CASH` and `CARD` only. Do not infer the online checkout provider from this statement.

## Suggested editable data model

Store the following in database-backed Company/Shop settings rather than constants:

- company trading name and legal name (legal name still to confirm)
- Shop code, display name, address, phone, email, WhatsApp, time zone
- weekly opening schedule and special closures
- product/service category labels and active flag
- warranty template version and customer-facing terms
- payment methods enabled per Shop

## Sources

1. [PC House official homepage](https://www.pchouse.ie/) — navigation, categories, homepage warranty statement, shop/kiosk details, contact, opening hours and payment methods.
2. [ILAC Shopping Centre — PC House listing](https://www.ilac.ie/shop/pc-house) — logo asset, kiosk address, opening hours and description of same-day on-site repair service and product range.
3. [PC House logo image on the ILAC directory](https://images.prismic.io/hammerson/afOHysBOoF08xg9C_PC_HOUSE-112x112.png?auto=format%2Ccompress&fit=max&w=256).
