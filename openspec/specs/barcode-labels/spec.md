# Spec: Barcode Labels Module

**Change**: new-pos-modules
**Feature**: barcode-labels
**Version**: 1.0
**Date**: 2026-06-27

---

## Purpose

Define what must be true after the barcode-labels feature is implemented. This spec covers the IBarcodeGenerationService port, symbology selection rules, the ILabelPrintService port, the label design UI, single-product and bulk-by-category printing, and navigation.

---

## Application Layer Requirements

### R-BL-01: IBarcodeGenerationService port is defined in POS.Application

A port interface `IBarcodeGenerationService` must be declared in POS.Application.

**Required methods:**
```
Task<BarcodeImageResult> GenerateAsync(string content, BarcodeSymbology symbology, CancellationToken ct = default);
BarcodeSymbology ResolveSymbology(Product product);
```

Where `BarcodeSymbology` is an enum with values: `Code128`, `EAN13`.

---

### R-BL-02: Symbology selection follows a deterministic rule

`ResolveSymbology` must return:
- `BarcodeSymbology.EAN13` if `product.Barcode` is exactly 12 digits (numeric only); the EAN-13 check digit is auto-calculated and appended by the generation service.
- `BarcodeSymbology.Code128` in all other cases (barcode is null, empty, non-numeric, not exactly 12 digits).

**This rule must be enforced at the Application layer**, not in Infrastructure or Presentation.

---

### R-BL-03: ILabelPrintService port is defined in POS.Application

A port interface `ILabelPrintService` must be declared in POS.Application.

**Required methods:**
```
Task PrintSingleLabelAsync(LabelPrintRequest request, CancellationToken ct = default);
Task PrintBulkLabelsAsync(IReadOnlyList<LabelPrintRequest> requests, CancellationToken ct = default);
```

`LabelPrintRequest` must include:
- `ProductId` (Guid)
- `ProductName` (string)
- `Price` (decimal)
- `BarcodeContent` (string) — the raw barcode string (not the image)
- `Symbology` (BarcodeSymbology)
- `Copies` (int, default 1)

---

## Infrastructure Requirements

### R-BL-04: IBarcodeGenerationService is implemented using ZXing.Net (SkiaSharp binding)

The implementation in POS.Infrastructure must:
- Use the `ZXing.Net` package (SkiaSharp binding) for barcode image generation.
- Return a `BarcodeImageResult` containing the image as a byte array (PNG format) and the final barcode string (including EAN-13 check digit when applicable).
- Throw an `InvalidOperationException` (or a domain-defined `BarcodeGenerationException`) with a descriptive message if generation fails.

**Constraint:** Infrastructure must not expose ZXing types to Application or Presentation layers.

---

### R-BL-05: ILabelPrintService renders labels via QuestPDF at standard label dimensions

The `LabelPrintService` implementation must:
- Render label PDFs using QuestPDF.
- Target label dimensions of 57 × 32 mm (Dymo/Zebra compatible standard).
- Each label must include: product name, barcode image (rendered from `IBarcodeGenerationService`), and sale price.
- The PDF is sent to the default system printer or a configured label printer.

---

## Presentation Requirements

### R-BL-06: Label design UI exposes single and bulk print actions

A `BarcodeLabelViewModel` must exist in POS.Presentation and expose:
- Product search (by name or barcode) to select a single product for preview and single-label printing.
- A category selector for bulk print — operator selects a category and all products in that category are printed.
- A quantity/copies field (default 1, minimum 1, maximum 100 per product).
- A preview area showing the barcode image for the selected product.
- "Imprimir" button for single label.
- "Imprimir por Categoría" button for bulk.

---

### R-BL-07: Print errors are surfaced without crashing the application

If `IBarcodeGenerationService.GenerateAsync` or `ILabelPrintService.PrintSingleLabelAsync` fails:
- The ViewModel must catch the error and display a user-readable message.
- The application must remain in a usable state after the error.
- No partial print job state should be left in a corrupt or unrecoverable state.

---

### R-BL-08: Barcode Labels is a new top-level sidebar entry

The main navigation shell must include a new sidebar entry for "Código de Barras / Etiquetas" that routes to the BarcodeLabelViewModel/View. It must appear after "Productos" and before "Inventario" (or at a logical position in the sidebar order; exact position deferred to design, but it must be top-level, not a sub-tab).

---

## Acceptance Scenarios

### Scenario BL-S1: Code128 barcode generated for product without barcode

```
GIVEN a product with Barcode = null (or empty)
WHEN ResolveSymbology is called for this product
THEN the result is BarcodeSymbology.Code128
AND when GenerateAsync is called with the product's Id as content and Code128 symbology
THEN a valid PNG byte array is returned
AND the BarcodeImageResult.FinalContent equals the product's Id string (no modification)
```

### Scenario BL-S2: EAN-13 barcode generated for product with exactly 12-digit numeric barcode

```
GIVEN a product with Barcode = "123456789012" (12 numeric digits)
WHEN ResolveSymbology is called for this product
THEN the result is BarcodeSymbology.EAN13
AND when GenerateAsync is called with content="123456789012" and EAN13 symbology
THEN the check digit is calculated: for "123456789012" the EAN-13 check digit = 8
AND BarcodeImageResult.FinalContent = "1234567890128" (13 digits)
AND a valid PNG byte array is returned
```

### Scenario BL-S3: Code128 used when barcode is non-12-digit numeric

```
GIVEN a product with Barcode = "ABC-123" (non-numeric)
WHEN ResolveSymbology is called
THEN the result is BarcodeSymbology.Code128

GIVEN a product with Barcode = "12345" (5 digits, not 12)
WHEN ResolveSymbology is called
THEN the result is BarcodeSymbology.Code128
```

### Scenario BL-S4: Single label printed successfully

```
GIVEN the operator has selected ProductX in the Barcode/Labels screen
AND ProductX has Name="Leche Entera", Price=$3,200 COP, Barcode=null
AND Copies=2
WHEN the operator clicks "Imprimir"
THEN IBarcodeGenerationService.GenerateAsync is called with Code128 symbology
AND ILabelPrintService.PrintSingleLabelAsync is called with Copies=2, ProductName="Leche Entera", Price=$3,200
AND the label job is sent to the printer
AND the ViewModel shows a success message (e.g., "Etiqueta enviada a imprimir")
```

### Scenario BL-S5: Bulk print by category prints all products in that category

```
GIVEN Category "Lácteos" contains 5 products: P1, P2, P3, P4, P5
AND the operator selects "Lácteos" in the category selector
AND Copies = 1
WHEN the operator clicks "Imprimir por Categoría"
THEN ILabelPrintService.PrintBulkLabelsAsync is called with a list of 5 LabelPrintRequests
AND each request corresponds to one of the 5 products in "Lácteos"
AND all 5 labels are included in a single print job
```

### Scenario BL-S6: Barcode generation failure shows recoverable error

```
GIVEN IBarcodeGenerationService.GenerateAsync throws a BarcodeGenerationException with message "Invalid barcode content"
WHEN the operator clicks "Imprimir" for the selected product
THEN the ViewModel displays an error message: "Error al generar el código de barras: Invalid barcode content"
AND the application does not crash
AND the print button remains enabled for retry
```

### Scenario BL-S7: Label content meets minimum visual requirements

```
GIVEN a LabelPrintRequest for ProductX with Name="Yogur Natural", Price=$2,500 COP, Barcode="123456789012"
WHEN ILabelPrintService renders the label
THEN the generated label PDF contains:
  - The text "Yogur Natural"
  - The price "$2,500"
  - A barcode image representing "1234567890128" (EAN-13)
AND the label dimensions are 57 mm × 32 mm
```

### Scenario BL-S8: Barcode Labels sidebar entry is visible and navigable

```
GIVEN the POS application is running with a logged-in administrator
WHEN the operator looks at the main sidebar
THEN a "Código de Barras" or "Etiquetas" entry is visible as a top-level item
AND clicking it navigates to the BarcodeLabelViewModel/View without error
AND the product search field is displayed and responsive
```

---

## Constraints and Non-Goals

- Custom label paper sizes are not supported; only the standard 57×32 mm label is required.
- QR code generation is out of scope for this slice.
- ESC/POS direct label printing (without PDF generation) is out of scope; QuestPDF PDF is the only rendering path.
- Generated internal Code128 content (for products without a barcode) uses the product's Guid or a short internal identifier — uniqueness is ensured by the identifier chosen; no separate barcode assignment sequence is required.
