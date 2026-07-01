# Barcode View Specification

## Purpose

Defines the BarcodeView UI translation: a single-label generator with live preview, and a batch panel that lets the operator search the catalog or add ad-hoc codes for bulk label printing. Per the wireframe, this is an administrative utility reached from Productos or the Admin hub, not a primary funcbar module.

## Requirements

### Requirement: Generator captures barcode value, format, label, price, and copies

The generator panel MUST accept: barcode value, format (`CODE 128`, `EAN-13`, `CODE 39`, `QR Code`), a label text, an optional price, and a copies count (1-100), and MUST render a live preview reflecting these inputs.

#### Scenario: Preview updates as the operator types

- GIVEN the operator enters value "7770001234567" and label "Ibuprofeno"
- WHEN the fields change
- THEN the preview MUST show the updated label text and barcode number
- AND leaving price empty MUST hide the price line in the preview

#### Scenario: Setting a price shows it in the preview

- GIVEN the operator enters price 8500
- WHEN the preview renders
- THEN it MUST display "$8,500" in the preview price line

### Requirement: Printing a single label requires a barcode value

#### Scenario: Printing without a value is blocked

- GIVEN the barcode value field is empty
- WHEN the operator activates "Imprimir"
- THEN the system MUST show an error ("Ingresa el valor del código") and MUST NOT send a print job

#### Scenario: Printing with a value confirms the copy count

- GIVEN a valid barcode value and copies = 2
- WHEN the operator activates "Imprimir"
- THEN a success toast MUST confirm "2 etiqueta(s) enviada(s) a imprimir"

### Requirement: Batch panel supports catalog search and manual add

The batch panel MUST let the operator search the product catalog (by name or barcode) to add items to the print batch, or add the currently generated code via "Agregar", with each batch row showing a copies stepper and a remove action.

#### Scenario: Adding the same catalog product twice increments copies

- GIVEN "Ibuprofeno 400mg · Caja x10" is already in the batch with 1 copy
- WHEN the operator adds it again from search results
- THEN the batch row copies MUST increment to 2, not create a duplicate row

#### Scenario: Empty batch shows a prompt

- GIVEN no items have been added
- WHEN the batch panel renders
- THEN it MUST display "Agrega productos para imprimir etiquetas en lote"

### Requirement: Printing the batch requires at least one item

#### Scenario: Printing an empty batch is blocked

- GIVEN the batch is empty
- WHEN the operator activates "Imprimir lote"
- THEN the system MUST show an error ("El lote está vacío") and MUST NOT send a print job

#### Scenario: Printing a non-empty batch confirms total labels and clears it

- GIVEN the batch has 3 items with copies 2, 1, and 5
- WHEN the operator activates "Imprimir lote"
- THEN a success toast MUST confirm 8 total labels sent to print
- AND the batch MUST reset to empty
