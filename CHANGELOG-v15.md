# v15 — Payment, deposits, refurb identity and MySQL image storage (2026-11-05)

- Confirmed initial tender methods: CASH and CARD only. Card is captured after approval on an external card terminal; no processor API integration in v1.
- Defined Manager-only partial/total refunds, bounded by original settled tender allocations and returned to original tender by default; no second Manager approver.
- Defined service-deposit validity/cancellation baseline: no automatic expiry or forfeiture; 90 days without response prompts review/contact; disclosed/fair terms and documented non-recoverable special-order cost required for any retention.
- Deferred installment late fees, collections and renegotiation; `OVERDUE` status only.
- Selected MySQL-only image storage: `stored_image.content MEDIUMBLOB`, optional <=60 KiB thumbnail `BLOB`, strict domain-specific attachment FKs, no external storage provider or filesystem.
- Confirmed Company-unique internal refurbished asset code; capture manufacturer serial when present and enforce Company-scoped serial uniqueness.
- Added proposed refurbished commercial guarantee coverage/exclusions while preserving statutory Irish consumer rights; final customer terms need review.
- Updated technician permission baseline and removed accounting/bookkeeper reconciliation as a v1 blocker per owner instruction.
- VAT remains preimplemented but OFF/unreviewed until owner later confirms Irish tax status with accountant.
