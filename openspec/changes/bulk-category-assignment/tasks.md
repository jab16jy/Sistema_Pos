# Tasks: Bulk Category Assignment

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ~400 source and tests, plus SDD artifacts |
| 400-line budget risk | Medium |
| Delivery strategy | Single cohesive work unit |
| Rationale | Domain behavior, Application use case, UI bindings, and tests are required for one usable workflow. |

## Phase 1: Business Behavior

- [x] 1.1 Add a domain method for assigning a valid category to an active product. [Req: Assign selected products]
- [x] 1.2 Add a bulk assignment request and Application use case. [Req: Assign selected products]
- [x] 1.3 Validate category and all products before changing any product. [Req: Reject invalid batch]
- [x] 1.4 Save once only when at least one product changes. [Req: Persist changed products]

## Phase 2: Presentation Workflow

- [x] 2.1 Add assignment rows and selection state to `CategoriesViewModel`. [Req: Select products]
- [x] 2.2 Load active target categories and active searchable products. [Req: Choose active category]
- [x] 2.3 Add WPF bindings for search, selection, target category, count, and assignment. [Req: Complete assignment from Categories]
- [x] 2.4 Refresh `ProductsViewModel` after a successful assignment. [Req: Reflect assignment in products]

## Phase 3: Verification

- [x] 3.1 Add Application tests for successful batch assignment and atomic missing-product failure. [Req: Batch integrity]
- [x] 3.2 Add shared ViewModel workflow coverage. [Req: Complete assignment from Categories]
- [x] 3.3 Run `dotnet build POS.sln --no-restore`. [Req: WPF bindings compile]
- [x] 3.4 Run `dotnet test POS.sln --no-restore`. [Req: All scenarios]
