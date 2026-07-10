# Proposal: Bulk Category Assignment

## Intent

Make the existing Categories subsection useful for catalog maintenance by allowing an operator to select several active products and assign them to one category in a single action.

## Scope

### In Scope

- Search active products from the Categories subsection.
- Select multiple products and an active target category.
- Reassign the selected products through an Application use case.
- Refresh the product list after a successful assignment.
- Preserve existing category CRUD and individual product category selection.

### Out of Scope

- A separate shell navigation module for categories.
- Category hierarchy, merge, or deletion behavior changes.
- Assigning inactive products or choosing inactive categories from the UI.

## Capability

### New Capability

- `bulk-product-category-assignment`: Search, select, and reassign multiple catalog products to a category from the existing Products/Categories UI.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| Domain | Modified | Encapsulate category reassignment on `Product`. |
| Application | New | Validate and execute a bulk assignment request. |
| Presentation.Shared | Modified/New | Manage selection, feedback, and product-list refresh events. |
| WPF | Modified | Add the assignment surface to the Categories subsection. |
| Tests | Modified | Cover atomic failure, single-save behavior, and ViewModel workflow. |

## Risks and Rollback

The batch operation changes multiple product records. It validates every selected product before mutating any of them, then saves once, preventing a missing product from producing a partial update. Rollback is a code revert; no schema or migration change is required.

## Success Criteria

- [x] An operator can search and select active products in Categories.
- [x] An operator can assign the selected products to an active category.
- [x] Missing selected products produce no partial assignment or save.
- [x] The products view refreshes after the assignment.
- [x] Application, shared presentation, full solution build, and full solution tests pass.
