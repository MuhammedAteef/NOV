# NOV ERP Project Documentation

## Overview

NOV ERP is an ASP.NET Core Razor Pages application for sales, purchasing, inventory, customer and supplier records, and payment tracking.

| Area | Technology |
| --- | --- |
| Web application | ASP.NET Core 10 Razor Pages |
| Persistence | Entity Framework Core 10 with SQL Server |
| Authentication | ASP.NET Core Identity |
| Card payments | Stripe Payment Intents using manual capture |
| UI | Bootstrap and Bootstrap Icons |
| Automated tests | xUnit with SQLite in-memory |

## Business workflows

### Sales

- The server calculates invoice totals from the current product selling prices; submitted prices are not trusted.
- A sales invoice is created only when the full invoice total is paid in the same workflow. Cash is recorded directly; a card payment must have a matching Stripe authorization and successful full capture.
- Sales invoice records are immutable after creation. Product returns are recorded as cash refunds against the original invoice.
- Return quantities are limited to the unreturned quantity on each original invoice line. Returns increase stock and write linked stock-history entries.

### Purchasing

- Purchase invoices record the supplier's unit costs and receive stock when the invoice is created.
- An initial payment is required, but the existing staged-payment workflow is retained: invoices may have an outstanding balance, which can be paid later.
- Product purchase and selling prices are updated from purchase lines; the selling price is set to 130% of the last submitted purchase-line price for each product.
- Purchase invoices are immutable. Supplier returns are recorded separately against original purchase lines, use original purchase costs, and reduce on-hand stock.

### Inventory and transaction integrity

- Stock is received through purchases, reduced by sales and supplier returns, and increased by customer returns. Product creation initializes stock at zero.
- Workflow operations use serializable database transactions and conditional stock updates. A database check constraint also prohibits negative stock.
- Stock history records the related invoice, return, or supplier refund where applicable.
- Transaction request keys and unique database indexes help make retries safe. Reusing an invoice request key with different customer, line, or payment details is rejected.
- Stripe capture is performed as part of the sales or purchase workflow. If a database operation fails after authorization, the service attempts to cancel or refund the intent and logs compensation failures for reconciliation. This is a coordinated database/payment workflow, not a distributed database transaction.

## Application structure

- `Pages/`: Razor Pages for authentication, dashboard, customers, suppliers, products, sales invoices, purchase invoices, profile, and stock history.
- `Models/`: Entity Framework entities and `SalesManagementSystemContext`.
- `Repositories/` and `Interfaces/`: repository and unit-of-work abstractions used by page handlers.
- `Services/`: domain workflows, payment integration, and email delivery.
- `Migrations/`: SQL Server schema migrations and the EF Core model snapshot.
- `Tests/SystemManagementSystem.Tests/`: focused SQLite workflow tests using a fake payment service.

`InvoiceWorkflowService` owns invoice, payment, stock, and refund operations that must succeed or fail together. Razor Page handlers use it rather than independently changing invoice and inventory records.

## Configuration and startup

Use environment variables, user secrets, or a managed secret store for sensitive values. Do not commit credentials.

Required configuration:

- `ConnectionStrings__DefaultConnection`: SQL Server connection string.
- `Stripe__SecretKey` and `Stripe__PublishableKey`: Stripe credentials for card checkout.
- `EmailSettings__SmtpServer`, `EmailSettings__SmtpPort`, `EmailSettings__SenderEmail`, `EmailSettings__SenderName`, `EmailSettings__Username`, `EmailSettings__Password`, and `EmailSettings__EnableSsl`: SMTP settings used by the email service.

The application always ensures the `Admin` and `User` roles exist. To create an initial administrator, configure both `SeedAdmin__Email` and `SeedAdmin__Password`; if either is configured without the other, startup fails. The password must satisfy the Identity password policy. If neither is configured, no bootstrap administrator is created.

Application startup applies pending EF Core migrations. Review and test schema changes against a backup or disposable database before deploying, because starting the application can update its configured database.

## Database migrations

The latest `AddInvoiceWorkflowIntegrity` migration adds request keys, stock-history references, purchase-refund tables, uniqueness constraints, and the nonnegative-stock check. It is scaffolded but has **not** been applied as part of this work.

Before applying it to an existing database, review the data for negative product stock and duplicate values that would violate the new unique indexes. Resolve any such rows according to business records; do not silently delete or rewrite transaction history.

EF Core tools use `DesignTime/SalesManagementSystemContextFactory.cs`. Supply a disposable or otherwise safe connection string to EF tooling; scaffolding a migration does not require applying it:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=NOVDesignTimeOnly;Trusted_Connection=True;TrustServerCertificate=True"
dotnet ef migrations add <MigrationName> --output-dir Migrations
```

Do not run `dotnet ef database update` against a production or otherwise configured database without the normal review, backup, and deployment process.

## Build and tests

From the repository root:

```powershell
dotnet build
dotnet test Tests\SystemManagementSystem.Tests\SystemManagementSystem.Tests.csproj
```

The workflow tests use SQLite in-memory and a fake `IPaymentService`; they do not call Stripe or a configured SQL Server. They cover full sales payments, rejecting partial card authorization, idempotent retries and key reuse, cash returns and quantity limits, supplier refunds and stock limits, rollback after failed card capture, and concurrent stock deductions.

## Important operational limits

- Customer refunds are cash-only; card refunds are not automatically issued through Stripe.
- Supplier refunds are recorded in the system and reduce inventory. The application does not send money to the supplier.
- Purchase invoices retain partial payments; sales invoices do not.
- The SQLite tests verify workflow behavior but do not replace SQL Server migration testing or live Stripe integration testing.
