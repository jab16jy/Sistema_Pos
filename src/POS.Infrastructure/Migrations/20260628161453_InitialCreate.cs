using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUDIT_LOGS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_LOGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BUSINESS_SETTINGS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BusinessAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrinterCommand = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrinterWorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BackupDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BUSINESS_SETTINGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CASH_CLOSINGS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    expected_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    counted_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    digital_payments = table.Column<decimal>(type: "TEXT", nullable: false),
                    billed_total = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    cash_collected = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    digital_collected = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    receivable_settlement_total = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CASH_CLOSINGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CASHIER_SHIFTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CashierUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CashierUsername = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    opening_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    closing_cash = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CASHIER_SHIFTS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CATEGORIES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIES", x => x.Id);
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
                name: "LOYALTY_RULES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PointsPerCurrencyUnit = table.Column<decimal>(type: "TEXT", nullable: false),
                    RedemptionValuePerPoint = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimumRedeemPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_RULES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LOYALTY_TIERS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MinPoints = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    PointsMultiplier = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 1.0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_TIERS", x => x.Id);
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
                name: "RECEIVABLES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    SaleId = table.Column<string>(type: "TEXT", nullable: false),
                    original_balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    outstanding_balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEIVABLES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SALE_RETURNS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalSaleId = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SALE_RETURNS", x => x.Id);
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
                name: "UNIT_OF_MEASURES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UNIT_OF_MEASURES", x => x.Id);
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
                name: "SUBCATEGORIES",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SUBCATEGORIES", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SUBCATEGORIES_CATEGORIES_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CATEGORIES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoyaltyTierId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_LOYALTY_TIERS_LoyaltyTierId",
                        column: x => x.LoyaltyTierId,
                        principalTable: "LOYALTY_TIERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "RECEIVABLE_SETTLEMENTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivableId = table.Column<string>(type: "TEXT", nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECEIVABLE_SETTLEMENTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RECEIVABLE_SETTLEMENTS_RECEIVABLES_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "RECEIVABLES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SALE_RETURN_ITEMS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SaleReturnId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    QuantityReturned = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SALE_RETURN_ITEMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SALE_RETURN_ITEMS_SALE_RETURNS_SaleReturnId",
                        column: x => x.SaleReturnId,
                        principalTable: "SALE_RETURNS",
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
                    discount = table.Column<decimal>(type: "TEXT", nullable: false),
                    tax_rate = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 19m),
                    UnitCost = table.Column<decimal>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "PRODUCTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", nullable: false),
                    SubCategoryId = table.Column<string>(type: "TEXT", nullable: true),
                    UnitOfMeasureId = table.Column<string>(type: "TEXT", nullable: true),
                    cost_price = table.Column<decimal>(type: "TEXT", nullable: false),
                    sale_price = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinimumStock = table.Column<decimal>(type: "TEXT", nullable: false),
                    tax_rate = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 19m),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PRODUCTS_SUBCATEGORIES_SubCategoryId",
                        column: x => x.SubCategoryId,
                        principalTable: "SUBCATEGORIES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PRODUCTS_UNIT_OF_MEASURES_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UNIT_OF_MEASURES",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LOYALTY_ACCOUNTS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_ACCOUNTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LOYALTY_ACCOUNTS_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRODUCT_SUPPLIERS",
                columns: table => new
                {
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRODUCT_SUPPLIERS", x => new { x.ProductId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_PRODUCT_SUPPLIERS_PRODUCTS_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PRODUCTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRODUCT_SUPPLIERS_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LOYALTY_TRANSACTIONS",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    LoyaltyAccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    SaleId = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOYALTY_TRANSACTIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LOYALTY_TRANSACTIONS_LOYALTY_ACCOUNTS_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalTable: "LOYALTY_ACCOUNTS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_CreatedAt",
                table: "AUDIT_LOGS",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOGS_EntityName",
                table: "AUDIT_LOGS",
                column: "EntityName");

            migrationBuilder.CreateIndex(
                name: "IX_CASHIER_SHIFTS_CashierUserId_Status",
                table: "CASHIER_SHIFTS",
                columns: new[] { "CashierUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CASHIER_SHIFTS_OpenedAt",
                table: "CASHIER_SHIFTS",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_LoyaltyTierId",
                table: "Customers",
                column: "LoyaltyTierId");

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_ACCOUNTS_CustomerId",
                table: "LOYALTY_ACCOUNTS",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LOYALTY_TRANSACTIONS_LoyaltyAccountId",
                table: "LOYALTY_TRANSACTIONS",
                column: "LoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENTS_SaleId",
                table: "PAYMENTS",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCT_SUPPLIERS_SupplierId",
                table: "PRODUCT_SUPPLIERS",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_SubCategoryId",
                table: "PRODUCTS",
                column: "SubCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PRODUCTS_UnitOfMeasureId",
                table: "PRODUCTS",
                column: "UnitOfMeasureId");

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
                name: "IX_RECEIVABLE_SETTLEMENTS_ReceivableId",
                table: "RECEIVABLE_SETTLEMENTS",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_SALE_ITEMS_SaleId",
                table: "SALE_ITEMS",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURN_ITEMS_SaleReturnId",
                table: "SALE_RETURN_ITEMS",
                column: "SaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SALE_RETURNS_OriginalSaleId",
                table: "SALE_RETURNS",
                column: "OriginalSaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SUBCATEGORIES_CategoryId",
                table: "SUBCATEGORIES",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AUDIT_LOGS");

            migrationBuilder.DropTable(
                name: "BUSINESS_SETTINGS");

            migrationBuilder.DropTable(
                name: "CASH_CLOSINGS");

            migrationBuilder.DropTable(
                name: "CASHIER_SHIFTS");

            migrationBuilder.DropTable(
                name: "DOCUMENT_SEND_LOGS");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "LOYALTY_RULES");

            migrationBuilder.DropTable(
                name: "LOYALTY_TRANSACTIONS");

            migrationBuilder.DropTable(
                name: "PAYMENTS");

            migrationBuilder.DropTable(
                name: "PRODUCT_SUPPLIERS");

            migrationBuilder.DropTable(
                name: "PURCHASE_ITEMS");

            migrationBuilder.DropTable(
                name: "QUOTE_ITEMS");

            migrationBuilder.DropTable(
                name: "RECEIVABLE_SETTLEMENTS");

            migrationBuilder.DropTable(
                name: "SALE_ITEMS");

            migrationBuilder.DropTable(
                name: "SALE_RETURN_ITEMS");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "LOYALTY_ACCOUNTS");

            migrationBuilder.DropTable(
                name: "PRODUCTS");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "PURCHASES");

            migrationBuilder.DropTable(
                name: "QUOTES");

            migrationBuilder.DropTable(
                name: "RECEIVABLES");

            migrationBuilder.DropTable(
                name: "SALES");

            migrationBuilder.DropTable(
                name: "SALE_RETURNS");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "SUBCATEGORIES");

            migrationBuilder.DropTable(
                name: "UNIT_OF_MEASURES");

            migrationBuilder.DropTable(
                name: "LOYALTY_TIERS");

            migrationBuilder.DropTable(
                name: "CATEGORIES");
        }
    }
}
