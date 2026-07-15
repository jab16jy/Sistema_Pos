# Proposal: Complete Cashier Hold, Discounts, and Shortcuts

## Intent

Complete the cashier workflows requested by the Caja audit without duplicating existing sale, settings, or authorization concepts.

## Scope

### In Scope

- Persist and resume the current cashier's held sales.
- Let a cashier apply a manual line discount within an administrator-managed maximum.
- Keep manual discounts separate from tier and loyalty calculations.
- Cancel the current unsaved cart and wire Esc, F5, F6, and F7 to existing cashier actions.
- Add focused domain, application, shared-presentation, and WPF smoke coverage.

### Out of Scope

- Mixed tender editing, transfer-platform selection, or credit UI.
- Cancelling an already persisted held sale and releasing its reserved stock.
- A new held-sale entity or a new permission family.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| Domain | Modified | Add safe discount setting bounds and a hold resume transition. |
| Application | Modified/New | Persist holds, resume/complete held sales, and enforce discount limits. |
| Infrastructure | Modified | Map the setting, repository query, DI, and schema migration. |
| Presentation.Shared | Modified | Expose discount maximum, hold/resume commands, cancel, and key actions. |
| WPF | Modified | Add discount editing, held-sale selection, cancel confirmation, and key bindings. |
| Tests | Modified | Cover stock-safe hold/resume, settings, discount enforcement, commands, and bindings. |

## Risks and Rollback

Held sales reserve stock through the existing `Sale.AddItem` behavior. Resume must complete the existing aggregate and record inventory movements without adding its items again. Rollback is a code revert plus removal of the additive migration before deployment; no existing completed sale data is rewritten.

## Success Criteria

- A cashier can hold a non-empty cart, see their held sales, and resume one.
- Manual discounts above the persisted maximum are rejected in Application, not just hidden in WPF.
- Loyalty and tier discounts retain their existing calculations.
- Esc cancels the current cart after confirmation; F5/F6/F7 invoke checkout/customer/hold.
- Focused tests, full tests, and build pass.
