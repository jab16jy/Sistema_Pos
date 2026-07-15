using POS.Application.DTOs.Sales;
using POS.Domain.Entities;

namespace POS.Application.UseCases.Sales;

internal static class HeldSaleMapper
{
    public static HeldSaleSummary Map(Sale sale) => new(
        sale.Id,
        sale.CashierUserId,
        sale.CustomerId,
        sale.InvoiceNumber,
        sale.CreatedAt,
        sale.Total().Amount,
        sale.Items.Select(item => new HeldSaleItem(
            item.ProductId,
            item.ProductName,
            item.Quantity,
            item.UnitPrice.Amount,
            item.Discount.Amount,
            item.Presentation,
            item.UnitsPerPresentation)).ToArray());
}
