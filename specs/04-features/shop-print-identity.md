# Feature: Shop printable identity fields

**Status:** APPROVED — owner approved optional Shop address and phone fields for printed reports/documents on 2026-11-11 and selected the PC House homepage values for local seed records on 2026-11-13. Legal/postal production confirmation remains open.
**Scope:** Per-Shop persistence of address and phone for future print templates. This does not define a Shop administration screen or implement report rendering.

## 1. Goal

Let a Shop carry its own address and phone so printed documents can identify the branch responsible for the transaction.

## 2. Legacy references and evidence

| Type | Path | Class/member | Evidence |
|---|---|---|---|
| Model / persistence mapping | `src/main/java/com/pchouse/pchousestoremvn/models/Company.java` in the legacy repository | `address` (`ADDRESS`), `contactOne` (`CONTACT_ONE`), `contactTwo` (`CONTACT_TWO`) | **CONFIRMED:** address and contacts exist on the legacy Company model; address and first contact are non-null, second contact optional. These are Company-scoped. |
| Shop model / mapping | Legacy repository search for `Shop.java` | — | **OPEN / NOT FOUND:** no legacy Shop model or Shop persistence mapping was located in the inspected source. Do not claim branch-level contact data as legacy behavior. |
| Business profile discovery | `specs/01-discovery/business-profile-from-website.md` | “Branch locations”, “Address discrepancy” | **CONFIRMED:** public branch profiles list address/phone details. The owner selected the homepage ILAC address for local seed; the legal/postal address remains open. |
| Reports specification | `specs/02-architecture/reports-and-document-printing.md` | Print requirements and `GeneratedDocument.SnapshotJson` | **REDESIGN:** Shop identity is rendered and snapshotted for future issuance/reprint. |

## 3. Behavior and rules

- **CONFIRMED legacy behavior:** `Company` contains one address and contact fields. The inspected mapping does not establish Shop-specific address/phone values.
- **REDESIGN approved:** `shop.address` is nullable `VARCHAR(500)` and `shop.phone` is nullable `VARCHAR(40)`. These are display strings owned by each Shop.
- **REDESIGN approved:** print templates may render the active Shop's available address and phone; an unset value is omitted. Values are captured into the immutable document snapshot at issue time so reprints preserve the issued identity.
- Do not copy Company or another Shop's contact values into these columns automatically.
- Do not normalize or infer address/phone values. Local seed values are the original website display strings approved by the owner; do not treat them as production legal/postal confirmation.
- This feature does not change the legacy Company fields or define how they are administered.

## 4. Data model and implementation boundary

`ShopRecord` exposes nullable `Address` and `Phone` properties mapped to `shop.address` and `shop.phone`. The PostgreSQL reference DDL and logical database model carry the same optional columns and limits.

No public API or UI contract is added by this feature. Shop administration endpoints and screens require their own approved feature scope. The report service must consume these values through a DTO/application contract and must not expose persistence entities.

## 5. Authorization

No new operation or permission is introduced. Any future committed Shop contact edit must follow the separately approved Shop-administration authorization policy and employee-action challenge requirements.

## 6. Acceptance criteria

1. A Shop can persist with either, both, or neither address and phone populated.
2. Address values longer than 500 characters and phone values longer than 40 characters are rejected by persistence validation/database constraints.
3. Shop contact values are independent across Shops and are never inferred from Company contact fields.
4. Print generation includes the available values for the transaction's Shop, omits unset values, and snapshots the rendered identity for historical reprints.
5. The local seed uses the homepage ILAC address selected by the owner; production documents must not treat it as confirmed legal/postal data.

## 7. Test scenarios

- Persistence mapping accepts null and configured maximum lengths for both fields.
- Persistence rejects values exceeding configured limits.
- Print rendering omits null fields, renders available Shop values, and uses the stored snapshot when reprinting after a Shop contact edit.
- Cross-Shop values are not substituted when a Shop field is absent.

## 8. Open items

- **OPEN:** Owner confirmation of each production Shop's display name, authoritative address and phone.
- **OPEN:** Shop contact editing workflow and its authorization policy; outside this feature's scope.
- **OPEN:** Report-rendering implementation and integration tests; the report engine is not implemented yet.

## 9. Local seed implementation

- Created Company `Pc House Dublin` (local Company ID `2`) using the public trading name and main SHOP contact values. The Company address is the site's main SHOP operational address, not a registered-office assertion; shared email is stored on Company.
- Created active Shop records `FREDERICK_ST_SHOP` and `ILAC_KIOSK` from the PC House homepage display values. The homepage address was selected for ILAC as requested by the owner.
- Added the shared Company email to the Shop session DTO/profile display. The existing `pcHouseStore Local Test` Company and `TEST-001` fixture were left intact.
- On 2026-11-13 the owner supplied and approved separate access codes/passwords for both Shops. Codes were set to `FREDERICK` and `ILAC`; active credential hashes and `PROVISIONED` deployment-operator audit events were created. Plaintext passwords are not stored in the repository or database.
- Sign in with the requested code and password. The existing `TEST-001` Shop remains available under the original test Company.
