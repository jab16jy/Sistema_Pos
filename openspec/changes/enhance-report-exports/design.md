# Report export enhancement design

## Scope

The change is limited to `ReportExportService` and its export tests. No reporting DTO, WPF view model, persistence, or query contract changes are required because the existing export inputs already contain all required values.

## Excel detail

The operational workbook adds a `Detalle de recaudo` worksheet. It records every available sales and receivable collection component, its calculation, whether it affects physical cash, and its operational use. This keeps digital income visibly separate from expected drawer cash.

The profitability workbook adds a `Comparativo` worksheet. It shows current value, prior value, absolute difference, percentage difference, and an interpretation for revenue, costs, and gross margin.

## PDF scale

Both report PDF methods apply a shared `0.75f` content scale directly to `page.Content()`. This follows the existing invoice/quote pattern and intentionally leaves the document header and footer at their current legible size.

## Verification

Export tests load the generated workbooks through ClosedXML and assert the new worksheet names and representative detailed values. Existing PDF generation tests continue to validate successful PDF output. The full solution formatter, build, and test suite are run before delivery.
