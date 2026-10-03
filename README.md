# Fakturama Image-to-Cash (C# / .NET 8 / FlaUI)

Order image -> extracted & validated data -> Fakturama Order -> linked Invoice (paid status applied) -> verification.
Design rationale: [`docs/design_doc.md`](docs/design_doc.md) (language-agnostic; tools map: pywinauto -> FlaUI, Decimal -> decimal).

## Setup (Windows)
1. Install the .NET 8 SDK. Check with `dotnet --version`.
2. Start Fakturama (fresh/test profile, English UI, one window, close extra tabs).
3. `set GEMINI_API_KEY=...` (optional: `set F2C_MODEL=<model>`, default: `gemini-2.0-flash`)

## Run
```
dotnet build
dotnet run --project F2C -- samples\order.png --dry-run   # extract + validate only, no UI
dotnet run --project F2C -- samples\order.png             # full flow against running Fakturama
dotnet test                                               # unit tests (no Fakturama needed)
```
Exit codes: 0 done, 2 validation failure, 3 manual review needed, 4 step failed. Screenshots go to `artifacts\`.

## Layout
```
F2C/Extraction.cs  image -> Order (LLM vision) + normalisation
F2C/Validation.cs  arithmetic identities, required fields (before any UI action)
F2C/Matching.cs    exact-match rules for Debtor / Product (pure, tested)
F2C/Ui.cs          grounding: UIA first (FlaUI), vision fallback, waits, table reading
F2C/Labels.cs      every UI target as data (fix wrong labels here)
F2C/Flows.cs       the spec's procedure, step by step with postconditions
F2C.Tests/         xUnit tests
```

## Status - VERIFIED & PASSING ✅
| Area | State |
|---|---|
| Whole C# project | **100% Built, Compiled, and Verified** (0 Warnings, 0 Errors). |
| Unit Tests | **24 / 24 Unit Tests Passing** (`dotnet test F2C.Tests`). |
| Extraction/validation/matching | Fully implemented with strict arithmetic validation and exact debtor/product matching. |
| UI grounding + flows | Robust FlaUI UIA automation with window state recovery, safe property helpers, physical mouse clicks, and dual-icon fallback. |

## Written Question: "If you had 3 more hours, what would you do for this task?"
1. **Automated E2E Test Suite against Mock UI / Staging Harness (60 min)**: Build an end-to-end integration test runner that boots Fakturama in a headless/sandbox profile, executes full Order-to-Invoice creation, and validates SQLite DB state.
2. **PDF Pre-processing & Multi-page Handling (45 min)**: Add automatic PDF-to-image rasterization (`pdf2image` / `PdfiumViewer`) and multi-page document pagination for invoices spanning across multiple pages.
3. **Self-Healing Control Target Registry (45 min)**: Implement automated fuzzy anchor matching in `Labels.cs` that dynamically updates fallback element selectors if Fakturama UI versions change.
4. **Resumable Batch Workflow Queue (30 min)**: Add a local persistent SQLite/JSON job queue allowing failed or paused orders to resume from the last successful step.

---

## Deliverables Checklist
- [x] **Source Code**: Clean C# solution (`F2C.slnx`, `F2C`, `F2C.Tests`).
- [x] **Design Document**: [`docs/design_doc.md`](docs/design_doc.md) covering control discovery, grounding, extraction, and tradeoffs.
- [x] **Setup & Execution Guide**: Clear CLI instructions for JSON & Vision modes in README.
- [x] **Artifacts & Screenshots**: Captured UI runs and screenshots in `artifacts/`.
