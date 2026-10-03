# Design Doc: Fakturama Image-to-Cash Automation

## 1. Goal and non-goals

**Goal.** Given one order image, produce a saved Fakturama Order and a linked, correctly-paid Invoice, creating Debtor / Payment Method / VAT / Product master data only when an exact existing record is unavailable.

**Non-goals.** Multi-image batches, Delivery/Correction/Dunning documents, non-EUR currency handling, running unattended against ambiguous data (we stop instead).

**Guiding principle.** The scope is larger than the clock, and a wrong invoice is worse than no invoice. So: *deterministic checks wherever possible, an LLM only where perception is needed, and stop for manual review on any ambiguity.*

## 2. Architecture

```
image ──► Extractor (LLM vision) ──► Validator (pure Python) ──► Order model
                                                                    │
            ┌───────────────────────────────────────────────────────┘
            ▼
   Flow runner (state machine; every step = action + postcondition)
            │ uses
            ▼
   Grounder:  Tier 1 UIA lookup ► Tier 2 vision lookup ► Tier 3 stop
            │
            ▼
   Fakturama (Eclipse/SWT desktop app, Windows)
```

Three layers with hard boundaries, so each can be tested alone:

1. **Extraction + validation** (no UI): image → typed `Order`; arithmetic cross-checked.
2. **Grounding** (`ui.py`): "find the control described as X", never "click (412, 310)".
3. **Flows** (`flows.py`): the business procedure from the spec (Order → Debtor → Products → Save → Invoice → verify).

## 3. Image-extraction strategy

- **Primary: a vision LLM** with a strict JSON schema (all fields in spec §1.2). One call; temperature 0. Chosen over classic OCR because the image is a table with merged semantics (aliases, billing vs. delivery), and an LLM returns structure directly.
- **Normalisation in code, not in the prompt:** dates → ISO, decimals → `decimal` (never double/float), whitespace trimmed, payment method matched case-insensitively to the known set {Bank Transfer, Credit Card, SEPA Direct Debit}.
- **Validation is the safety net** (deterministic):
  - each line: `qty × unit_net × (1 − disc/100)` must equal the image's line total;
  - sum of lines must equal NET TOTAL; VAT grouped by rate must equal VAT TOTAL; net + VAT must equal GROSS TOTAL;
  - required fields present; PAID ⇒ payment date present.
  
  Any mismatch aborts *before touching the UI*. An LLM misreading a digit nearly always breaks one of these identities, so this catches the dominant error class cheaply.
- **Optional second opinion:** run Tesseract OCR and compare numeric tokens to the LLM output (listed as future work; the arithmetic identities already cover most of the value).

## 4. Control-discovery / grounding strategy

Fakturama is an Eclipse RCP (Java SWT) app. SWT exposes a **partial** UIA tree: menus, buttons, text fields and dialogs are usually reachable; custom-drawn grids (the Items table) and icon-only toolbar buttons often are not. So a single technique won't do. We use a ladder:

| Tier | Technique | Used for | Failure mode |
|---|---|---|---|
| 1 | **UIA** (FlaUI, UIA3 backend): match by control type + accessible name/regex, scoped to the active dialog or tab | Text fields, dialogs, buttons with names, list rows | Name missing/ambiguous |
| 2 | **Vision grounding**: screenshot → LLM returns a bounding box for a *described* element ("the upper contact icon beside Addresses") → click centre | Icon-only buttons, grid cells | Wrong box |
| 3 | **Stop** with a screenshot saved to `artifacts/` | Anything still unresolved | n/a (safe) |

Rules that make this robust:

- **No hardcoded coordinates.** Tier 2 coordinates are computed per call from the current screenshot and never cached across steps.
- **Targets are data, not code** (`labels.py`): each has a UIA name pattern *and* a natural-language description. Fixing a wrong label is a one-line edit; the logic doesn't change. This also handles localisation (German UI) by swapping the table.
- **Scope before search.** Dialog controls are searched only inside the dialog window; avoids matching the Order's "Date" when the Debtor editor is also open.
- **Wait on conditions, not sleeps.** Waits poll for "dialog exists", "table rows identical across two polls" (the spec's "list stabilised"), or "tab title contains X", with timeouts.
- **Typing via clipboard paste** (`Ctrl+V`) for reliability with umlauts and long strings; field is cleared with `Ctrl+A` first.

## 5. Verify-every-step execution model

Each step is `do → verify`; failing verification raises `StepFailed` (retry once for transient UI timing) or `ManualReview` (never retried).

Examples: after setting Date, read the field back and compare; after selecting a Debtor, read the invoice address and compare ZIP/city to the image; after saving the Order, open *Data → Documents* and assert one row with expected Date, Cust.Ref., state=open and Total; after saving the Invoice, assert state=paid and Total.

**Exact-match policy** (spec §2.3, §3.3) lives in pure functions (`matching.py`) over rows read from the selector: Debtor requires normalised Company, First Name, Name, ZIP, City to be equal; Product requires the SKU equal. 0 matches → create; exactly 1 → select; ≥2 or conflicting → `ManualReview`. Being pure, these are unit-tested without Fakturama.

**Create-then-reselect** (spec §2.12, §3.12): after creating master data we re-run the *same selector* from the still-open Order. That doubles as proof the record was saved.

## 6. Money and rounding

All money is `decimal` with `ROUND_HALF_UP` to 2 places. Product master price (gross) = `unit_net × (1 + vat/100)` with **no** line discount; line price = `qty × unit_net × (1 − disc/100)`. Floats are banned because `0.1 + 0.2 != 0.3` would eventually produce a one-cent invoice error.

## 7. Failure handling and idempotency

- Fail fast, loudly, with a screenshot and a step name; never "best-guess" a click.
- Running twice on the same image would create a duplicate Order. v1 mitigation: before opening a New Order, search *Data → Documents* for the Cust.Ref.; if found, stop. (Future work: resume mode.)
- Secrets (API key) via environment variable only.

## 8. Tradeoffs

| Choice | Benefit | Cost |
|---|---|---|
| LLM extraction + arithmetic validation | Fast to build, handles layout changes | Nondeterministic; needs the validator and API access |
| UIA-first, vision fallback | Fast and exact where UIA works; survives layout change where it doesn't | Two code paths; vision is slower, costs tokens, can misplace clicks |
| Targets in a config table | Easy to repair, localisable | Another file to keep accurate |
| Strict stop-on-ambiguity | Never writes bad financial data | Needs a human more often |
| Drive the UI instead of writing Fakturama's DB | Matches the spec; respects app validation | Much slower/more fragile than DB or REST |

## 9. Main risks (ranked)

1. **Items grid editing** (Qty / U.Price / VAT / Discount cells): custom widget, weakest UIA support. Mitigation: vision grounding + read-back of the row after each edit.
2. **Icon-only buttons** (existing-contact vs green "+"): the spec warns about picking the wrong one. Mitigation: descriptive vision targets and a post-click check that the expected dialog title appeared.
3. **Timing of SWT dialogs.** Mitigation: condition-based waits.
4. **Unverified labels**: written against the spec's wording; must be confirmed on a real install (see README).

## 10. Testing plan

- Unit: money math, validation, exact-match, header normalisation (done; no UI needed).
- Extraction: run on the sample image; compare to a hand-written expected JSON.
- End-to-end: fresh Fakturama profile; run twice (second run must stop on duplicate Cust.Ref.); variant images (unknown VAT 7 %, SEPA payment, one item, three items).
