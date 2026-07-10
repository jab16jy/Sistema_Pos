<Delta for product-catalog>
## ADDED Requirements

### Requirement: Category synchronization

The categories selection dropdown in the product editing interface MUST automatically update to reflect any creation, renaming, or deletion of a category in real time.

#### Scenario: Category dropdown synchronizes on creation
- GIVEN a user is viewing the product editing form
- WHEN a new category is created in the category editor
- THEN the product category selection dropdown MUST immediately include the new category

## MODIFIED Requirements

### Requirement: Category CRUD UI

A `CategoryViewModel` in POS.Presentation MUST expose a list of all categories, a form for creating and editing a category (Name required, max 100 chars), and commands: `CreateCategoryCommand`, `UpdateCategoryCommand`, `DeleteCategoryCommand`. The form MUST bind the category's active state (`IsActive`) to a CheckBox. Validation messages MUST be visible inline when a command fails. The interface MUST dynamically toggle between "Nueva categoría" and "Editando categoría" headers based on whether a category is selected.
(Previously: A `CategoryViewModel` in POS.Presentation MUST expose a list of all categories, a form for creating and editing a category (Name required, max 100 chars), and commands: `CreateCategoryCommand`, `UpdateCategoryCommand`, `DeleteCategoryCommand`. Validation messages MUST be visible inline when a command fails.)

#### Scenario: Bind active status checkbox in category form
- GIVEN the category "Bebidas" has active status set to true
- WHEN the user selects "Bebidas" in the category list
- THEN the active CheckBox in the category form MUST be checked

#### Scenario: Execute delete category command
- GIVEN category C3 has no products associated with it
- WHEN the user triggers the DeleteCategoryCommand for C3
- THEN the category record MUST be deleted from the database
- AND the categories list MUST reload automatically
</Delta for product-catalog>
