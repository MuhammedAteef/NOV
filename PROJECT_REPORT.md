# 📊 NOV ERP - System Architecture & Comprehensive Features Report

## Executive Summary
**NOV ERP** (`SystemManagementSystem`) is an enterprise-grade Enterprise Resource Planning (ERP) web application built using **ASP.NET Core 10.0 Razor Pages**, **Entity Framework Core 10.0**, and **SQL Server**. The system provides end-to-end management of sales transactions, procurement/purchases, automated inventory tracking, customer/supplier relationships, stock audit logging, and multi-channel payment processing via **Stripe.net** and **Cash**.

---

## 🛠️ Technology Stack & System Specifications

| Layer | Technology / Package | Description / Details |
| :--- | :--- | :--- |
| **Framework** | ASP.NET Core 10.0 | Modular Razor Pages Web Application Architecture |
| **Database ORM** | Entity Framework Core `10.0.10` | SQL Server Provider (`SalesManagementSystemContext`) |
| **Authentication** | ASP.NET Core Identity | Cookie Authentication with `IdentityDbContext<IdentityUser>` |
| **Payment Gateway** | Stripe.net `52.2.0` | Credit Card checkout with Stripe Elements & Payment Intents |
| **UI Framework** | Bootstrap 5 & Bootstrap Icons | Modern Glassmorphism / Sleek ERP Dashboard Theme |
| **Client Logic** | Vanilla JS (ES6+) | Dynamic line-item calculation, AJAX, and client-side stock validation |
| **Design Patterns** | Repository & Unit of Work | Decoupled Data Access Layer (`IUnitOfWork`, `IRepository<T>`) |

---

## 🏗️ Architecture & Core Design Patterns

### 1. Repository & Unit of Work Pattern
- **`IRepository<T>` / `Repository<T>`**: Generic repository providing reusable CRUD operations, specification filtering, and `IQueryable` access.
- **`IPurchaseInvoiceRepository` / `PurchaseInvoiceRepository`**: Specialized repository with eager loading (`Include`) for supplier and line items, plus transactional soft-delete logic.
- **`ISalesInvoiceRepository` / `SalesInvoiceRepository`**: Specialized repository handling sales invoice details and customer relationship eager loading.
- **`IUnitOfWork` / `UnitOfWork`**: Encapsulates database contexts to commit atomic multi-repository operations within a single `SaveChangesAsync()` transaction.

### 2. Domain Service Layer
- **`IPaymentService` / `PaymentService`**: Encapsulates Stripe payment intent creation (`CreateStripePaymentIntentAsync`), payment status verification (`PaymentStatusAsync`), and payment ledger insertion (`ProcessInvoicePaymentAsync`).

### 3. Soft-Delete & Audit Pattern
- Both `PurchaseInvoice` and `SalesInvoice` include `IsDeleted` and `DeletedAt` fields.
- Soft-deleting an invoice automatically reverses product stock quantities and records detailed audit log entries into `StockHistory`.

---

## 🗄️ Database Schema & Data Models

### Entity Relationship Model

```
       +--------------+
       |   Category   |
       +-------+------+
               | 1
               |
               | N
       +-------v------+
       |   Product    |<------------------+
       +---+------+---+                   |
           |      |                       |
         1 |    1 |                       |
           |      |                       |
         N |    N |                       |
+----------v-+  +-v------------+  +-------+------+
| SalesItem  |  | PurchaseItem |  | StockHistory |
+------------+  +--------------+  +--------------+
       ^              ^
     N |            N |
       |              |
     1 |            1 |
+------+-----+  +-----+--------+
|SalesInvoice|  |PurchaseInvoice
+------+-----+  +-----+--------+
       ^              ^
     1 |            1 |
       |              |
     N |            N |
+------+-----+  +-----+--------+
| Customer   |  |   Supplier   |
+------------+  +--------------+
```

### Database Entity Breakdown

1. **`Category`**: Product classifications (`CategoryId`, `Name`, `Description`).
2. **`Product`**: Inventory catalog (`ProductId`, `Name`, `Description`, `PurchasePrice`, `SellingPrice`, `StockQuantity`, `CategoryId`).
3. **`Customer`**: Client directory (`CustomerId`, `Name`, `Email`, `Phone`, `Address`).
4. **`Supplier`**: Procurement vendor directory (`SupplierId`, `Name`, `Email`, `Phone`, `Address`).
5. **`PurchaseInvoice`**: Purchase transaction header (`PurchaseInvoiceId`, `SupplierId`, `InvoiceDate`, `TotalAmount`, `PaidAmount`, `RemainingAmount`, `IsDeleted`, `DeletedAt`).
6. **`PurchaseInvoiceItem`**: Purchase line items (`PurchaseItemId`, `PurchaseInvoiceId`, `ProductId`, `Quantity`, `UnitPrice`, Computed `SubTotal = Quantity * UnitPrice`, `IsDeleted`).
7. **`SalesInvoice`**: Sales transaction header (`SalesInvoiceId`, `CustomerId`, `InvoiceDate`, `TotalAmount`, `IsDeleted`, `DeletedAt`).
8. **`SalesInvoiceItem`**: Sales line items (`SalesItemId`, `SalesInvoiceId`, `ProductId`, `Quantity`, `UnitPrice`, Computed `SubTotal = Quantity * UnitPrice`, `IsDeleted`).
9. **`StockHistory`**: Inventory movement ledger (`StockHistoryId`, `ProductId`, `ChangeQuantity`, `MovementType` ["IN"/"OUT"], `MovementDate`, `Notes`).
10. **`Payment`**: Financial ledger (`PaymentId`, `Amount`, `PaymentDate`, `PaymentMethod` ["Cash"/"Card"], `Status` ["Paid"], `ReferenceNumber`, `StripePaymentIntentId`, `SalesInvoiceId`, `PurchaseInvoiceId`).

---

## 💡 Key System Modules & Features

### 1. Executive Dashboard (`/Index`)
- **Real-Time Financial & Inventory KPIs**:
  - Revenue Today
  - Total Active Products
  - Total Customers
  - Total Sales Invoices
  - Low Stock Alert Count (`StockQuantity < 10`)
- **Dynamic Contextual Greeting**: Time-based salutation ("Good Morning", "Good Afternoon", "Good Evening").
- **Recent Sales Stream**: Quick overview table displaying recent customer sales orders.
- **Low Stock Warning Panel**: Displays products nearing depletion for immediate purchasing action.

---

### 2. Sales Management Module (`/SalesInvoices`)
- **Dynamic Invoice Creator (`Create.cshtml`)**:
  - Interactive JavaScript client-side builder (`invoice-create.js`).
  - Stock availability badges & real-time client-side stock checking (prevents adding quantities greater than warehouse stock).
  - Automatic Unit Price calculation from product selling price.
  - Flexible payment options: Cash or Stripe Credit Card checkout.
  - **Atomic Transaction Workflow**:
    1. Create Sales Invoice Header.
    2. Save line items.
    3. Decrement product `StockQuantity`.
    4. Log stock movement (`"OUT"`) in `StockHistory`.
    5. Record payment entry in `Payments` ledger.
- **Search & Advanced Filtering (`Index.cshtml`)**:
  - Filter by Customer dropdown.
  - Filter by Invoice ID.
  - Search Term (Customer Name or Invoice #).
  - Status Filter (`Completed`/`Active`, `Deleted`).
  - Date Filter.
- **Invoice Overview & Pay Remaining Balance (`Show.cshtml`)**:
  - Visual invoice layout with line items and customer info.
  - Outstanding balance calculation (`TotalAmount - TotalPaid`).
  - "Pay Remaining" Modal with Cash and Stripe Card payment handling.
- **Soft Delete & Stock Restoral**:
  - Restores deducted inventory quantities and logs `"IN"` movements in `StockHistory`.

---

### 3. Purchase Management Module (`/PurchaseInvoices` - Admin Only)
- **Procurement Invoice Creator (`Create.cshtml`)**:
  - Supplier selection & dynamic line-item builder.
  - Automatic inventory stock incrementation.
  - **Automated Price Markup Algorithm**: Automatically updates `PurchasePrice` and recalculates `SellingPrice` with a 30% profit margin (`SellingPrice = Round(UnitPrice * 1.30, 2)`).
  - Immediate payment processing via Cash or Stripe Credit Card.
  - **Atomic Transaction Workflow**:
    1. Save Purchase Invoice Header.
    2. Create purchase line items.
    3. Increment product `StockQuantity`.
    4. Update product cost & selling prices.
    5. Log stock movement (`"IN"`) in `StockHistory`.
    6. Insert payment transaction in `Payments` ledger.
- **Search & Filtering (`Index.cshtml`)**:
  - Filter by Supplier, Invoice ID, Search Term, Status (`Completed`, `UnCompleted`/`Pending`, `Deleted`), and Date.
- **Invoice Details & Balance Tracking (`Show.cshtml`)**:
  - Tracks `PaidAmount` vs. `RemainingAmount`.
  - Modal support to pay remaining vendor balances.
- **Soft Delete & Stock Reversal**:
  - Deducts added stock quantities and logs `"OUT"` movement in `StockHistory`.

---

### 4. Inventory & Stock Audit Module (`/Products`, `/Categories`, `/StockHistory`)
- **Products Management**: Complete CRUD operations for products, costs, prices, and stock tracking.
- **Category Hierarchy**: Category CRUD operations to organize products (Admin Only).
- **Stock Audit Log (`/StockHistory`)**:
  - Comprehensive immutable ledger of all product movements.
  - Records Movement Type (`"IN"` / `"OUT"`), Quantity Delta, Timestamp, and automated audit descriptions (e.g. *"Purchase Invoice #5"*, *"Sales Invoice #12 deleted — stock restored"*).

---

### 5. CRM & Directory Modules (`/Customers`, `/Suppliers`)
- **Customer CRM**: CRUD management for client profiles, emails, phone numbers, and addresses.
- **Supplier CRM**: Procurement vendor catalog management (Admin Only).

---

### 6. Security, Identity & Role-Based Access Control (RBAC)
- **Identity System**: ASP.NET Core Identity with custom security requirements:
  - Minimum password length: 9 characters (requires digit, uppercase, lowercase, non-alphanumeric).
  - Account Lockout: 5 failed attempts locks the account for 15 minutes.
- **Role Policies**:
  - Roles: `Admin` and `User`.
  - **Folder Authorization Conventions**:
    - **Public**: `/Index`, `/Account/Login`, `/Account/Register`
    - **Admin Only**: `/Categories`, `/Suppliers`, `/PurchaseInvoices`, `/PurchaseInvoiceItems`, `/StockHistory`, `/InvoiceHistory`
    - **Authenticated Users (Admin + User)**: `/Products`, `/Customers`, `/SalesInvoices`, `/SalesInvoiceItems`
- **Automatic Database Seeding**:
  - Applies EF Core migrations automatically on startup (`context.Database.MigrateAsync()`).
  - Ensures default roles (`Admin`, `User`) exist.
  - Seeds default admin accounts (`admin1@system.com`, `admin2@system.com`).

---

## 🎨 User Interface & Layout (NOV ERP Theme)
- **Top Header Bar**:
  - Brand Logo & Title (*NOV ERP*).
  - Global Search Input with shortcut hint (`Ctrl K`).
  - Quick Action Dropdown (fast creation of Sales Invoices, Purchase Invoices, Products, Customers).
  - Notification Popover Menu (alerts for new orders, low stock warnings, payment receipts).
  - User Dropdown Profile with role indicator badges (`Administrator` / `Standard User`).
- **Sidebar Navigation**: Categorized navigation links (Core, Inventory, Sales, Purchases, System Admin) with active route detection.
- **Dynamic Breadcrumbs**: Automatic route-based breadcrumb generation (e.g. `Dashboard > Sales > Sales Invoices`).
- **Responsive Layout**: Mobile-friendly sidebar toggle button and fluid container elements.
