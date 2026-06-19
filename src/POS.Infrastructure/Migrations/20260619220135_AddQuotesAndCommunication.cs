using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotesAndCommunication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CASH_CLOSINGS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    expected_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    counted_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    digital_payments = table.Column<decimal>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CASH_CLOSINGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    Identification = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DOCUMENT_SEND_LOGS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<string>(type: "TEXT", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientContact = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOCUMENT_SEND_LOGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PRODUCTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    cost_price = table.Column<decimal>(type: "TEXT", nullable: false),
                    sale_price = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimumStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCTS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PURCHASES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PURCHASES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QUOTES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    QuoteNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CustomerPhone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    CustomerEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Observations = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ValidityDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConvertedSaleId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUOTES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SALES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CashierUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SALES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TaxId = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PURCHASE_ITEMS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PurchaseId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    total_cost = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PURCHASE_ITEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PURCHASE_ITEMS_PURCHASES_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "PURCHASES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QUOTE_ITEMS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    QuoteId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    unit_price = table.Column<decimal>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    discount = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUOTE_ITEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QUOTE_ITEMS_QUOTES_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "QUOTES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAYMENTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SaleId = table.Column<string>(type: "TEXT", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    received_amount = table.Column<decimal>(type: "TEXT", nullable: true),
                    BankOrPlatform = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYMENTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAYMENTS_SALES_SaleId",
                        column: x => x.SaleId,
                        principalTable: "SALES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SALE_ITEMS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SaleId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    unit_price = table.Column<decimal>(type: "TEXT", nullable: false),
                    discount = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SALE_ITEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SALE_ITEMS_SALES_SaleId",
                        column: x => x.SaleId,
                        principalTable: "SALES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENTS_SaleId",
                table: "PAYMENTS",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PURCHASE_ITEMS_PurchaseId",
                table: "PURCHASE_ITEMS",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_QUOTE_ITEMS_QuoteId",
                table: "QUOTE_ITEMS",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QUOTES_QuoteNumber",
                table: "QUOTES",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SALE_ITEMS_SaleId",
                table: "SALE_ITEMS",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CASH_CLOSINGS");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "DOCUMENT_SEND_LOGS");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "PAYMENTS");

            migrationBuilder.DropTable(
                name: "PRODUCTS");

            migrationBuilder.DropTable(
                name: "PURCHASE_ITEMS");

            migrationBuilder.DropTable(
                name: "QUOTE_ITEMS");

            migrationBuilder.DropTable(
                name: "SALE_ITEMS");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "PURCHASES");

            migrationBuilder.DropTable(
                name: "QUOTES");

            migrationBuilder.DropTable(
                name: "SALES");
        }
    }
}
