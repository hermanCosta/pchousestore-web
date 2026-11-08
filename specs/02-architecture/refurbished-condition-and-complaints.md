# Refurbished Condition Grades and Customer Complaint Workflow

**Status:** Condition-grade and inspection baseline approved; customer-facing commercial guarantee coverage/exclusions remain proposed and require owner/legal review. Irish consumer remedies must be reviewed against the facts of each case; the application records evidence and decisions but does not make legal determinations automatically.

## 1. Refurbished condition grades

Condition grade describes cosmetic condition. Functional quality is recorded separately and is not inferred from the grade.

| Grade | Display name | Standard |
|---|---|---|
| `A` | Excellent | Fully tested and functional; no or only very minor visible cosmetic marks; clean and complete as described. |
| `B` | Very Good | Fully tested and functional; light signs of normal use, such as small surface marks; no material cosmetic damage. |
| `C` | Good | Fully tested and functional; visible scratches, scuffs or cosmetic wear; every material mark must be photographed and disclosed to the buyer. |

These grades are the Company's approved commercial vocabulary, not an official Irish/EU legal grading standard. Grade A/B/C cannot be used to conceal a functional defect. A unit that fails a required functional test is `QUARANTINED` or sent for repair and cannot enter `AVAILABLE` refurbished stock until it passes inspection. A separate “for parts/as-is” sales workflow is not included in this initial baseline.

### Inspection fields

Each inspection records:

- exact `RefurbishedUnit`, Shop, employee and action authorization;
- `condition_grade` A/B/C;
- functional test status: `NOT_TESTED`, `IN_PROGRESS`, `PASSED`, `FAILED`;
- category-specific checklist results (e.g. ports, keyboard, display, battery health, controllers, speakers, connectivity, factory reset/data wipe where relevant);
- known cosmetic defects and missing accessories;
- inspection date, notes and multiple reference photos.

Only `PASSED` units with a valid A/B/C grade may transition to `AVAILABLE` for ordinary refurbished sale. The displayed listing/receipt snapshots the grade, known defects and included accessories at sale time. Re-inspection creates a new inspection record; it does not erase a prior inspection.

## 2. Twelve-month commercial guarantee

Every refurbished unit sold by the Company has the agreed 12-month commercial guarantee beginning at the actual documented customer handover/collection date. The warranty record snapshots the unit, sale item, start/end date and terms presented to the customer.

The 12-month commercial guarantee is additional to statutory consumer rights. Expiration of the commercial guarantee must never automatically reject a customer complaint or hide the statutory-rights pathway.

### Proposed v1 commercial coverage

The additional commercial guarantee is proposed to cover internal hardware/component failures that arise during normal intended use and are not attributable to an excluded external cause. The first remedy is normally inspection and repair at no charge; if repair is not possible or does not resolve the covered failure, the Manager records an appropriate replacement or other remedy under the published terms and statutory rights.

Proposed exclusions from the additional commercial guarantee (not from statutory rights):

- accidental impact, drops, crushing or other physical damage occurring after handover;
- liquid ingress/corrosion caused after handover;
- misuse, use outside manufacturer operating conditions or incorrect power supply;
- unauthorized opening, repair, modification or tampering where this caused the failure;
- theft, loss or cosmetic damage arising after handover;
- normal wear/consumable deterioration, including ordinary battery-capacity reduction, where it is not evidence of a defect or lack of conformity;
- a specific cosmetic defect or missing accessory clearly described and acknowledged by the customer at sale (this does not exclude a different undisclosed fault).

No exclusion may be used to remove statutory Irish consumer rights. A claim involving a battery, disclosed defect, accidental damage or unauthorized repair must be assessed on its actual cause; the system records evidence and does not automatically reject it. The final customer terms are proposed, not yet approved, and must be reviewed before publication.

## 3. Complaint case scope and classification

A `CustomerComplaintCase` can relate to:

- a sold product/refurbished `SaleItem`; or
- a completed `ServiceOrder` (for example, a complaint about repair workmanship).

The case records the customer, active Shop, source sale item/service order, reported date, date the issue first appeared (when known), description, requested remedy, evidence, assigned employee and status.

`claim_basis` is one of:

- `STATUTORY_CONSUMER_RIGHTS`;
- `COMMERCIAL_GUARANTEE`;
- `SERVICE_WORKMANSHIP`;
- `GOODWILL`;
- `PAID_OUT_OF_WARRANTY_REPAIR`.

These categories are workflow labels, not legal findings. A case can be reviewed under statutory rights even if the 12-month commercial guarantee has expired.

## 4. Complaint workflow

1. **Receive:** record customer and proof of purchase/service, exact serialised unit or service order, when the customer first noticed the problem, when they reported it, symptom description and requested remedy. Attach photos/videos only if the supported media policy allows them; initial release supports image evidence.
2. **Acknowledge:** create a case reference, record date/time and Shop, explain next steps and preserve all customer communications.
3. **Triage:** classify the claim basis for routing; do not automatically reject based only on elapsed commercial-guarantee time, grade C, or a technician's first “no fault found” result.
4. **Inspect:** technician records the received condition, accessories, serial/asset code, diagnostic tests, reproducibility, photos and findings. Record evidence of accidental damage/misuse/wear separately; do not convert an allegation into a final rejection without manager review.
5. **Assess:** Manager reviews the evidence, applicable commercial terms and possible statutory consumer rights. If entitlement is uncertain, the case remains `UNDER_REVIEW` and is escalated for appropriate advice rather than auto-rejected.
6. **Agree remedy:** where appropriate, record `REPAIR`, `REPLACEMENT`, `FULL_REFUND`, `PARTIAL_REFUND`/`PRICE_REDUCTION`, `NO_FAULT_FOUND_AFTER_REVIEW`, or `CUSTOMER_WITHDREW`. “No fault found” is a diagnostic finding, not automatically a denial of rights.
7. **Execute:** repair creates/links a ServiceOrder; replacement identifies the exact replacement unit; refund creates a partial/total `PaymentTransaction` linked to the original payment. Physical stock return is a separate selected `StockReturn` and inspection/disposition event.
8. **Resolve:** Manager records the final outcome, reason, dates, customer communication, any refund/replacement/repair references, and closes the case. Preserve append-only event history.

### Suggested case states

`RECEIVED -> TRIAGE -> AWAITING_INSPECTION -> UNDER_REVIEW -> AWAITING_CUSTOMER / AWAITING_PARTS / IN_REPAIR / AWAITING_REPLACEMENT -> RESOLVED -> CLOSED`

An escalation can be recorded from any non-closed state. A case is not set to `REJECTED` automatically; where the business concludes that a requested remedy is not available, the decision and its reason are recorded and communicated, without removing the customer's ability to pursue statutory rights.

## 5. Ireland-specific consumer-rights guardrails

The application must not hard-code the 12-month commercial guarantee as the only claim window. Current CCPC business/consumer guidance states, among other things:

- Consumer rights apply to goods bought from a business whether new or second-hand.
- For a fault reported within 30 days of receipt, the consumer may use the short-term right to terminate and receive a full refund after returning the goods; the customer may instead prefer repair or replacement.
- After 30 days, the customer can generally choose repair or replacement; a refund or price reduction may apply where the fault is serious or repair/replacement is not possible or does not resolve the issue.
- CCPC guidance notes that a consumer may expect a retailer to resolve a fault for up to six years after purchase where the goods could reasonably be expected to last that long. The application must not equate the 12-month commercial guarantee with the end of statutory rights.
- The business must resolve qualifying issues at no cost and within a reasonable time; reasonable time depends on the product and issue.
- Faults caused by accidental damage, misuse or normal wear and tear may be treated differently, but the case should preserve evidence and record the review.

Sources:
- CCPC, [Repairs and replacements](https://www.ccpc.ie/consumer-advice/faulty-products-and-goods/repairs-and-replacements).
- CCPC, [Selling products — legal obligations](https://www.ccpc.ie/information-for-businesses/selling-goods-and-services/selling-products).
- CCPC, [Your rights when buying goods](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/your-rights-when-buying-goods/).
- CCPC, [Guarantees and warranties](https://www.ccpc.ie/consumer-advice/consumer-rights/buying-goods/guarantees-and-warranties).

These are system guardrails and workflow prompts, not a legal decision engine. Before launch, have an Irish consumer-law professional review the Company's terms, notice wording, handling times and remedy policy.

## 6. Roles and audit

- `TECHNICIAN`: may open a case, attach technical evidence, inspect the device, record diagnosis and propose a technical remedy. Cannot approve refund/price reduction/replacement or close the case.
- `MANAGER`: full access; reviews entitlement/policy, approves and records resolution, refund, replacement and closure.
- Every mutation requires the acting employee's password challenge and single-use action authorization. The ShopSession still identifies only the Shop.

## 7. Data model additions

| Entity | Responsibility |
|---|---|
| `RefurbishedConditionGrade` | Company-configurable grade codes/labels; initial seed A/B/C. |
| `RefurbishedUnitInspection` | Immutable inspection/test record, checklist, grade, condition and technician attribution. |
| `CustomerComplaintCase` | Complaint header, customer, source SaleItem or ServiceOrder, dates, claim basis, status and assignment. |
| `CustomerComplaintCaseEvent` | Append-only state transitions, notes and customer communications. |
| `CustomerComplaintCaseAttachment` | Private image evidence and metadata. |
| `CustomerComplaintResolution` | Final remedy, reason, manager actor and linked refund/replacement/repair records. |

A complaint must reference at least one supported source (`sale_item_id` or `service_order_id`). A refurbished sale claim uses the SaleItem whose `refurbished_unit_id` identifies the physical unit. A claim about workmanship links to the ServiceOrder.

## 8. Acceptance scenarios

1. A Grade B unit with passed functional tests can be listed; its cosmetic notes and photos appear in the sales snapshot.
2. A unit with failed tests remains quarantined and cannot be sold as an ordinary refurbished unit.
3. A customer reports a fault 8 months after handover; the case can be reviewed under statutory rights even if the commercial guarantee terms are disputed.
4. A customer reports a fault 14 months after handover; the case is not auto-rejected and can still be assessed under statutory rights.
5. A complaint within 30 days is flagged for manager review of the short-term right to terminate; the system does not force repair where the customer is entitled to choose another remedy.
6. A technician can add inspection evidence but cannot approve a refund or close the claim.
7. A full/partial refund is linked to the original payment; returned physical goods are handled by a separate StockReturn.
8. Every communication, inspection, decision and remedy has an employee actor and timestamp.
