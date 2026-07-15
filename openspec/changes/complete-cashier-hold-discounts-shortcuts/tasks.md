# Tasks: Complete Cashier Hold, Discounts, and Shortcuts

## 1. Domain and settings

- [x] 1.1 Add the administrator-managed cashier discount maximum with a 20% default and 0–100 validation.
- [x] 1.2 Add the `OnHold` to `Draft` resume transition.
- [x] 1.3 Add settings DTO, mapping, ViewModel, WPF field, EF mapping, and migration.

## 2. Application and infrastructure

- [x] 2.1 Add the held-sale repository query and EF implementation.
- [x] 2.2 Add hold, list, resume, and complete-held-sale use cases with stock-safe completion.
- [x] 2.3 Enforce the persisted discount maximum in sale creation and hold requests.
- [x] 2.4 Register the new use cases and wire the production composition root.
- [x] 2.5 Make loyalty-account creation idempotent at the SQLite boundary and award points when a held customer sale is completed.

## 3. Cashier presentation

- [x] 3.1 Add visible line discount editing and maximum feedback.
- [x] 3.2 Replace the hold stub with persistence, held-sale list, and resume action.
- [x] 3.3 Add explicit cancel action and keyboard delegation for Esc/F5/F6/F7.
- [x] 3.4 Rename the production confirmation command away from prototype wording while preserving test compatibility if needed.

## 4. Verification

- [x] 4.1 Add focused tests for domain, Application, Settings, ViewModel, and WPF binding behavior.
- [x] 4.2 Run focused tests, `dotnet test POS.sln --no-restore`, and `dotnet build POS.sln --no-restore`.
- [x] 4.3 Review the diff and update this task list with completed items.
- [x] 4.4 Add regression coverage for duplicate account prevention and held-sale loyalty accumulation.
