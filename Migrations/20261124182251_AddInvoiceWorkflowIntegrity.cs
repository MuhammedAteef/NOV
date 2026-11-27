using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceWorkflowIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesReturnItems_SalesReturnId",
                table: "SalesReturnItems");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_SalesReturnId",
                table: "Refunds");

            migrationBuilder.AddColumn<int>(
                name: "PurchaseInvoiceId",
                table: "StockHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseRefundId",
                table: "StockHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalesInvoiceId",
                table: "StockHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalesReturnId",
                table: "StockHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestKey",
                table: "SalesReturns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestKey",
                table: "SalesInvoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestKey",
                table: "PurchaseInvoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Payments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseRefunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseInvoiceId = table.Column<int>(type: "int", nullable: false),
                    RefundDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRefunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRefund_Invoice",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "PurchaseInvoiceId");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRefundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRefundId = table.Column<int>(type: "int", nullable: false),
                    PurchaseInvoiceItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(21,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRefundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRefundItems_PurchaseInvoiceItems_PurchaseInvoiceItemId",
                        column: x => x.PurchaseInvoiceItemId,
                        principalTable: "PurchaseInvoiceItems",
                        principalColumn: "PurchaseItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRefundItems_PurchaseRefunds_PurchaseRefundId",
                        column: x => x.PurchaseRefundId,
                        principalTable: "PurchaseRefunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockHistory_PurchaseInvoiceId_ProductId",
                table: "StockHistory",
                columns: new[] { "PurchaseInvoiceId", "ProductId" },
                unique: true,
                filter: "[PurchaseInvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockHistory_PurchaseRefundId_ProductId",
                table: "StockHistory",
                columns: new[] { "PurchaseRefundId", "ProductId" },
                unique: true,
                filter: "[PurchaseRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockHistory_SalesInvoiceId_ProductId",
                table: "StockHistory",
                columns: new[] { "SalesInvoiceId", "ProductId" },
                unique: true,
                filter: "[SalesInvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockHistory_SalesReturnId_ProductId",
                table: "StockHistory",
                columns: new[] { "SalesReturnId", "ProductId" },
                unique: true,
                filter: "[SalesReturnId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_RequestKey",
                table: "SalesReturns",
                column: "RequestKey",
                unique: true,
                filter: "[RequestKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_SalesReturnId_InvoiceItemId",
                table: "SalesReturnItems",
                columns: new[] { "SalesReturnId", "InvoiceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_RequestKey",
                table: "SalesInvoices",
                column: "RequestKey",
                unique: true,
                filter: "[RequestKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_SalesReturnId",
                table: "Refunds",
                column: "SalesReturnId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_RequestKey",
                table: "PurchaseInvoices",
                column: "RequestKey",
                unique: true,
                filter: "[RequestKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_StockQuantity_NonNegative",
                table: "Products",
                sql: "[StockQuantity] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StripePaymentIntentId",
                table: "Payments",
                column: "StripePaymentIntentId",
                unique: true,
                filter: "[StripePaymentIntentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRefundItems_PurchaseInvoiceItemId",
                table: "PurchaseRefundItems",
                column: "PurchaseInvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRefundItems_PurchaseRefundId_PurchaseInvoiceItemId",
                table: "PurchaseRefundItems",
                columns: new[] { "PurchaseRefundId", "PurchaseInvoiceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRefunds_PurchaseInvoiceId",
                table: "PurchaseRefunds",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRefunds_RequestKey",
                table: "PurchaseRefunds",
                column: "RequestKey",
                unique: true,
                filter: "[RequestKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_StockHistory_PurchaseInvoices_PurchaseInvoiceId",
                table: "StockHistory",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "PurchaseInvoiceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockHistory_PurchaseRefunds_PurchaseRefundId",
                table: "StockHistory",
                column: "PurchaseRefundId",
                principalTable: "PurchaseRefunds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockHistory_SalesInvoices_SalesInvoiceId",
                table: "StockHistory",
                column: "SalesInvoiceId",
                principalTable: "SalesInvoices",
                principalColumn: "SalesInvoiceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockHistory_SalesReturns_SalesReturnId",
                table: "StockHistory",
                column: "SalesReturnId",
                principalTable: "SalesReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockHistory_PurchaseInvoices_PurchaseInvoiceId",
                table: "StockHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_StockHistory_PurchaseRefunds_PurchaseRefundId",
                table: "StockHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_StockHistory_SalesInvoices_SalesInvoiceId",
                table: "StockHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_StockHistory_SalesReturns_SalesReturnId",
                table: "StockHistory");

            migrationBuilder.DropTable(
                name: "PurchaseRefundItems");

            migrationBuilder.DropTable(
                name: "PurchaseRefunds");

            migrationBuilder.DropIndex(
                name: "IX_StockHistory_PurchaseInvoiceId_ProductId",
                table: "StockHistory");

            migrationBuilder.DropIndex(
                name: "IX_StockHistory_PurchaseRefundId_ProductId",
                table: "StockHistory");

            migrationBuilder.DropIndex(
                name: "IX_StockHistory_SalesInvoiceId_ProductId",
                table: "StockHistory");

            migrationBuilder.DropIndex(
                name: "IX_StockHistory_SalesReturnId_ProductId",
                table: "StockHistory");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_RequestKey",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturnItems_SalesReturnId_InvoiceItemId",
                table: "SalesReturnItems");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_RequestKey",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_SalesReturnId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_RequestKey",
                table: "PurchaseInvoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_StockQuantity_NonNegative",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_StripePaymentIntentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "PurchaseRefundId",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "SalesInvoiceId",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "SalesReturnId",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "RequestKey",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "RequestKey",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "RequestKey",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_SalesReturnId",
                table: "SalesReturnItems",
                column: "SalesReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_SalesReturnId",
                table: "Refunds",
                column: "SalesReturnId");
        }
    }
}
