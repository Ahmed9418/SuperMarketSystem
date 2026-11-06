-- =============================================================================================
--  نظام إدارة السوبر ماركت - سكربت إنشاء قاعدة البيانات
--  Supermarket Management System - database creation script (idempotent, EF Core 8)
--
--  HOW TO RUN (SQL Server Management Studio):
--    1. Connect to your instance (e.g. .\SQLEXPRESS)
--    2. Open this file and press F5 (Execute)
--    3. Right click "Databases" in the Object Explorer -> Refresh
--  Pick the database from the drop-down in the toolbar before running if you prefer to create
--  it yourself; the script also creates it when it does not exist yet.
-- =============================================================================================

USE [master];
GO

IF DB_ID(N'SupermarketDb') IS NULL
BEGIN
    CREATE DATABASE [SupermarketDb];
END;
GO

-- Arabic/Unicode safe collation for every text column created below.
ALTER DATABASE [SupermarketDb] COLLATE Arabic_CI_AS;
GO

USE [SupermarketDb];
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ParentCategoryId] int NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [PhoneNumber] varchar(30) NULL,
        [Address] nvarchar(200) NULL,
        [Balance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [LoyaltyPoints] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [StockCounts] (
        [Id] int NOT NULL IDENTITY,
        [ReferenceNumber] varchar(40) NOT NULL,
        [CountDateUtc] datetime2 NOT NULL,
        [CountedBy] nvarchar(120) NULL,
        [IsPosted] bit NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_StockCounts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [StoreSettings] (
        [Id] int NOT NULL IDENTITY,
        [StoreName] nvarchar(200) NOT NULL,
        [Address] nvarchar(300) NULL,
        [PhoneNumber] varchar(30) NULL,
        [TaxNumber] varchar(50) NULL,
        [ReceiptFooterMessage] nvarchar(400) NULL,
        [ReceiptPrinterName] nvarchar(200) NULL,
        [ReceiptLineWidth] int NOT NULL DEFAULT 42,
        [DefaultVatRate] decimal(9,4) NOT NULL,
        [PricesIncludeVat] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DecimalPlaces] int NOT NULL DEFAULT 2,
        [AutoGenerateBarcode] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_StoreSettings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [ContactPerson] nvarchar(150) NULL,
        [PhoneNumber] varchar(30) NULL,
        [Email] varchar(150) NULL,
        [Address] nvarchar(400) NULL,
        [TaxNumber] varchar(50) NULL,
        [Balance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Notes] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Units] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [Symbol] nvarchar(20) NULL,
        [AllowFractions] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Units] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [UserName] varchar(80) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [PasswordHash] varchar(500) NOT NULL,
        [Role] int NOT NULL,
        [PhoneNumber] varchar(30) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LastLoginAtUtc] datetime2 NULL,
        [PinHash] varchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Barcode] varchar(50) NOT NULL,
        [AlternativeBarcode] varchar(50) NULL,
        [CategoryId] int NOT NULL,
        [SupplierId] int NULL,
        [UnitId] int NOT NULL,
        [CostPrice] decimal(18,4) NOT NULL,
        [SellingPrice] decimal(18,4) NOT NULL,
        [DiscountPrice] decimal(18,4) NULL,
        [VatRate] decimal(9,4) NOT NULL,
        [StockQuantity] decimal(18,3) NOT NULL,
        [MinimumStockLevel] decimal(18,3) NOT NULL,
        [ReorderQuantity] decimal(18,3) NOT NULL,
        [ShelfLocation] nvarchar(120) NULL,
        [IsWeighted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [ImagePath] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Products_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [PurchaseOrders] (
        [Id] int NOT NULL IDENTITY,
        [OrderNumber] varchar(40) NOT NULL,
        [SupplierId] int NOT NULL,
        [UserId] int NULL,
        [OrderDateUtc] datetime2 NOT NULL,
        [ExpectedDeliveryDateUtc] datetime2 NULL,
        [ReceivedAtUtc] datetime2 NULL,
        [Status] int NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrders_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] int NOT NULL IDENTITY,
        [ShiftNumber] varchar(40) NOT NULL,
        [UserId] int NOT NULL,
        [TerminalId] varchar(60) NOT NULL DEFAULT 'POS-01',
        [OpenedAtUtc] datetime2 NOT NULL,
        [ClosedAtUtc] datetime2 NULL,
        [Status] int NOT NULL,
        [OpeningCash] decimal(18,2) NOT NULL,
        [ClosingCash] decimal(18,2) NULL,
        [ExpectedCash] decimal(18,2) NOT NULL,
        [CashDifference] decimal(18,2) NOT NULL,
        [TotalCashSales] decimal(18,2) NOT NULL,
        [TotalCardSales] decimal(18,2) NOT NULL,
        [TotalWalletSales] decimal(18,2) NOT NULL,
        [TotalCreditSales] decimal(18,2) NOT NULL,
        [TotalSales] decimal(18,2) NOT NULL,
        [TotalDiscount] decimal(18,2) NOT NULL,
        [TotalTax] decimal(18,2) NOT NULL,
        [TotalRefunds] decimal(18,2) NOT NULL,
        [InvoiceCount] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [ClosedBy] nvarchar(120) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shifts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [UserPermissions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PermissionKey] varchar(80) NOT NULL,
        [IsGranted] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [PriceHistories] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [OldCostPrice] decimal(18,4) NOT NULL,
        [NewCostPrice] decimal(18,4) NOT NULL,
        [OldSellingPrice] decimal(18,4) NOT NULL,
        [NewSellingPrice] decimal(18,4) NOT NULL,
        [Reason] nvarchar(400) NULL,
        [ChangedBy] nvarchar(120) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_PriceHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PriceHistories_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [StockCountItems] (
        [Id] int NOT NULL IDENTITY,
        [StockCountId] int NOT NULL,
        [ProductId] int NOT NULL,
        [SystemQuantity] decimal(18,3) NOT NULL,
        [CountedQuantity] decimal(18,3) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_StockCountItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockCountItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockCountItems_StockCounts_StockCountId] FOREIGN KEY ([StockCountId]) REFERENCES [StockCounts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [PurchaseOrderItems] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseOrderId] int NOT NULL,
        [ProductId] int NOT NULL,
        [OrderedQuantity] decimal(18,3) NOT NULL,
        [ReceivedQuantity] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_PurchaseOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseOrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceNumber] varchar(40) NOT NULL,
        [InvoiceDateUtc] datetime2 NOT NULL,
        [CustomerId] int NULL,
        [UserId] int NOT NULL,
        [ShiftId] int NULL,
        [Status] int NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [DiscountPercentage] decimal(9,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [ChangeAmount] decimal(18,2) NOT NULL,
        [RemainingAmount] decimal(18,2) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [ReferenceNumber] varchar(40) NULL,
        [Notes] nvarchar(1000) NULL,
        [IsWalkIn] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Invoices_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Invoices_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [ShiftReports] (
        [Id] int NOT NULL IDENTITY,
        [ShiftId] int NOT NULL,
        [ReportType] int NOT NULL,
        [GeneratedAtUtc] datetime2 NOT NULL,
        [GeneratedBy] nvarchar(120) NULL,
        [InvoiceCount] int NOT NULL,
        [TotalSales] decimal(18,2) NOT NULL,
        [TotalDiscount] decimal(18,2) NOT NULL,
        [TotalTax] decimal(18,2) NOT NULL,
        [TotalCashSales] decimal(18,2) NOT NULL,
        [TotalCardSales] decimal(18,2) NOT NULL,
        [TotalWalletSales] decimal(18,2) NOT NULL,
        [TotalCreditSales] decimal(18,2) NOT NULL,
        [TotalRefunds] decimal(18,2) NOT NULL,
        [OpeningCash] decimal(18,2) NOT NULL,
        [ExpectedCash] decimal(18,2) NOT NULL,
        [ClosingCash] decimal(18,2) NULL,
        [CashDifference] decimal(18,2) NOT NULL,
        [PrintedContent] nvarchar(400) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_ShiftReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftReports_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [InvoiceItems] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [ProductId] int NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [Barcode] varchar(50) NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [VatRate] decimal(9,4) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [LineTotalWithTax] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_InvoiceItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceItems_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_InvoiceItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceId] int NOT NULL,
        [Method] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaidAtUtc] datetime2 NOT NULL,
        [TransactionReference] varchar(60) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [MovementType] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [BalanceAfter] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        [ReferenceNumber] varchar(60) NULL,
        [InvoiceId] int NULL,
        [PurchaseOrderId] int NULL,
        [ShiftId] int NULL,
        [Reason] nvarchar(400) NULL,
        [PerformedBy] nvarchar(120) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockMovements_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_StockMovements_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockMovements_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [PurchaseOrders] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_StockMovements_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'Name', N'ParentCategoryId', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [CreatedAtUtc], [CreatedBy], [Description], [DisplayOrder], [IsActive], [Name], [ParentCategoryId], [UpdatedAtUtc], [UpdatedBy])
    VALUES (1, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 1, CAST(1 AS bit), N''ألبان وأجبان'', NULL, NULL, NULL),
    (2, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 2, CAST(1 AS bit), N''مخبوزات'', NULL, NULL, NULL),
    (3, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 3, CAST(1 AS bit), N''مشروبات'', NULL, NULL, NULL),
    (4, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 4, CAST(1 AS bit), N''بقالة جافة'', NULL, NULL, NULL),
    (5, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 5, CAST(1 AS bit), N''منظفات'', NULL, NULL, NULL),
    (6, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 6, CAST(1 AS bit), N''لحوم ودواجن'', NULL, NULL, NULL),
    (7, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 7, CAST(1 AS bit), N''خضار وفاكهة'', NULL, NULL, NULL),
    (8, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, 8, CAST(1 AS bit), N''مجمدات'', NULL, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DisplayOrder', N'IsActive', N'Name', N'ParentCategoryId', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'AutoGenerateBarcode', N'CreatedAtUtc', N'CreatedBy', N'DecimalPlaces', N'DefaultVatRate', N'PhoneNumber', N'PricesIncludeVat', N'ReceiptFooterMessage', N'ReceiptLineWidth', N'ReceiptPrinterName', N'StoreName', N'TaxNumber', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[StoreSettings]'))
        SET IDENTITY_INSERT [StoreSettings] ON;
    EXEC(N'INSERT INTO [StoreSettings] ([Id], [Address], [AutoGenerateBarcode], [CreatedAtUtc], [CreatedBy], [DecimalPlaces], [DefaultVatRate], [PhoneNumber], [PricesIncludeVat], [ReceiptFooterMessage], [ReceiptLineWidth], [ReceiptPrinterName], [StoreName], [TaxNumber], [UpdatedAtUtc], [UpdatedBy])
    VALUES (1, N''شارع الجمهورية - أخميم - سوهاج'', CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', N''seed'', 2, 0.14, ''01001234567'', CAST(1 AS bit), N''شكراً لزيارتكم - نتشرف بزيارتكم مرة أخرى'', 42, N''XP-80C'', N''سوبر ماركت الأمانة'', ''512-336-778'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'AutoGenerateBarcode', N'CreatedAtUtc', N'CreatedBy', N'DecimalPlaces', N'DefaultVatRate', N'PhoneNumber', N'PricesIncludeVat', N'ReceiptFooterMessage', N'ReceiptLineWidth', N'ReceiptPrinterName', N'StoreName', N'TaxNumber', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[StoreSettings]'))
        SET IDENTITY_INSERT [StoreSettings] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'Balance', N'ContactPerson', N'CreatedAtUtc', N'CreatedBy', N'Email', N'IsActive', N'Name', N'Notes', N'PhoneNumber', N'TaxNumber', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Suppliers]'))
        SET IDENTITY_INSERT [Suppliers] ON;
    EXEC(N'INSERT INTO [Suppliers] ([Id], [Address], [Balance], [ContactPerson], [CreatedAtUtc], [CreatedBy], [Email], [IsActive], [Name], [Notes], [PhoneNumber], [TaxNumber], [UpdatedAtUtc], [UpdatedBy])
    VALUES (1, N''المنطقة الصناعية - أسيوط'', 4800.0, N''مصطفى كامل'', ''2026-01-01T00:00:00.0000000Z'', NULL, ''sales@nile-dairy.example'', CAST(1 AS bit), N''شركة النيل للألبان'', NULL, ''0223456789'', ''200-145-987'', NULL, NULL),
    (2, N''شارع الجمهورية - سوهاج'', 1200.0, N''إبراهيم فتحي'', ''2026-01-01T00:00:00.0000000Z'', NULL, ''orders@alsafa-bakery.example'', CAST(1 AS bit), N''مخابز الصفا'', NULL, ''01122334455'', ''200-778-114'', NULL, NULL),
    (3, N''مدينة نصر - القاهرة'', 9500.0, N''هاني رمزي'', ''2026-01-01T00:00:00.0000000Z'', NULL, ''info@east-beverages.example'', CAST(1 AS bit), N''توزيع الشرق للمشروبات'', NULL, ''01099887766'', ''200-554-332'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'Balance', N'ContactPerson', N'CreatedAtUtc', N'CreatedBy', N'Email', N'IsActive', N'Name', N'Notes', N'PhoneNumber', N'TaxNumber', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Suppliers]'))
        SET IDENTITY_INSERT [Suppliers] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'ContactPerson', N'CreatedAtUtc', N'CreatedBy', N'Email', N'IsActive', N'Name', N'Notes', N'PhoneNumber', N'TaxNumber', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Suppliers]'))
        SET IDENTITY_INSERT [Suppliers] ON;
    EXEC(N'INSERT INTO [Suppliers] ([Id], [Address], [ContactPerson], [CreatedAtUtc], [CreatedBy], [Email], [IsActive], [Name], [Notes], [PhoneNumber], [TaxNumber], [UpdatedAtUtc], [UpdatedBy])
    VALUES (4, N''طنطا - الغربية'', N''ناهد سمير'', ''2026-01-01T00:00:00.0000000Z'', NULL, ''supply@delta-group.example'', CAST(1 AS bit), N''مجموعة الدلتا للتوريدات'', NULL, ''01555443322'', ''200-901-223'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Address', N'ContactPerson', N'CreatedAtUtc', N'CreatedBy', N'Email', N'IsActive', N'Name', N'Notes', N'PhoneNumber', N'TaxNumber', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Suppliers]'))
        SET IDENTITY_INSERT [Suppliers] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'Name', N'Symbol', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Units]'))
        SET IDENTITY_INSERT [Units] ON;
    EXEC(N'INSERT INTO [Units] ([Id], [CreatedAtUtc], [CreatedBy], [Name], [Symbol], [UpdatedAtUtc], [UpdatedBy])
    VALUES (1, ''2026-01-01T00:00:00.0000000Z'', NULL, N''قطعة'', N''قطعة'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'Name', N'Symbol', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Units]'))
        SET IDENTITY_INSERT [Units] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowFractions', N'CreatedAtUtc', N'CreatedBy', N'Name', N'Symbol', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Units]'))
        SET IDENTITY_INSERT [Units] ON;
    EXEC(N'INSERT INTO [Units] ([Id], [AllowFractions], [CreatedAtUtc], [CreatedBy], [Name], [Symbol], [UpdatedAtUtc], [UpdatedBy])
    VALUES (2, CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, N''كيلوجرام'', N''كجم'', NULL, NULL),
    (3, CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, N''جرام'', N''جم'', NULL, NULL),
    (4, CAST(1 AS bit), ''2026-01-01T00:00:00.0000000Z'', NULL, N''لتر'', N''لتر'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowFractions', N'CreatedAtUtc', N'CreatedBy', N'Name', N'Symbol', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Units]'))
        SET IDENTITY_INSERT [Units] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'Name', N'Symbol', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Units]'))
        SET IDENTITY_INSERT [Units] ON;
    EXEC(N'INSERT INTO [Units] ([Id], [CreatedAtUtc], [CreatedBy], [Name], [Symbol], [UpdatedAtUtc], [UpdatedBy])
    VALUES (5, ''2026-01-01T00:00:00.0000000Z'', NULL, N''عبوة'', N''عبوة'', NULL, NULL),
    (6, ''2026-01-01T00:00:00.0000000Z'', NULL, N''كرتونة'', N''كرتونة'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'Name', N'Symbol', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Units]'))
        SET IDENTITY_INSERT [Units] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'FullName', N'IsActive', N'LastLoginAtUtc', N'PasswordHash', N'PhoneNumber', N'PinHash', N'Role', N'UpdatedAtUtc', N'UpdatedBy', N'UserName') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] ON;
    EXEC(N'INSERT INTO [Users] ([Id], [CreatedAtUtc], [CreatedBy], [FullName], [IsActive], [LastLoginAtUtc], [PasswordHash], [PhoneNumber], [PinHash], [Role], [UpdatedAtUtc], [UpdatedBy], [UserName])
    VALUES (1, ''2026-01-01T00:00:00.0000000Z'', N''seed'', N''أحمد عبد الرحمن'', CAST(1 AS bit), NULL, ''AQAAAAIAAYagAAAAEHo+tjmWUP5eDBGx2UMKzfmY2Repq9HXP5vAv0f1iL71mG10EJdlj8yui0/XTvM/Zw=='', ''01000000001'', ''AQAAAAIAAYagAAAAEDn60arXI8DRG9j5TS9tIM8i1ROo//UvhJPMh08kkuOzOm8kKIP1ONCEHK0j14hrQA=='', 1, NULL, NULL, ''admin''),
    (2, ''2026-01-01T00:00:00.0000000Z'', N''seed'', N''محمود السيد'', CAST(1 AS bit), NULL, ''AQAAAAIAAYagAAAAEJ1mbCWmq8jgHumh7b2LNUch3no/rTW34acisg+LBFKutSf+SCIzjwC90ksjlUTBbw=='', ''01000000002'', ''AQAAAAIAAYagAAAAEOwa2h+1iYAGVnRlNR91rfKfDVmu1mfLDNMThVGlCOz4iHXH2zu8jUB2YeAl1iA3bQ=='', 3, NULL, NULL, ''cashier''),
    (3, ''2026-01-01T00:00:00.0000000Z'', N''seed'', N''سارة إبراهيم'', CAST(1 AS bit), NULL, ''AQAAAAIAAYagAAAAEAG96yFRFwHSPaN++Sx5xwTCer8/O/z8+Xg/8Ny9fisul95zBepcJnyTZ7mqpV1f7A=='', ''01000000003'', ''AQAAAAIAAYagAAAAEHTJRagas15v+RMepSM9UVTxlrC7Ac44UC1Gl8W1DlmuPe6DrjS9/80+dS9UDb3IQQ=='', 2, NULL, NULL, ''manager''),
    (4, ''2026-01-01T00:00:00.0000000Z'', N''seed'', N''خالد مصطفى'', CAST(1 AS bit), NULL, ''AQAAAAIAAYagAAAAENF54TPclEard4RbQ5+XzRyqWHl55IQixK9Xw2uk/KFaCQnDg5kGpFVIgyQAJ0W8zg=='', ''01000000004'', ''AQAAAAIAAYagAAAAEJrTSXhcqvfj4sUe1syTx5Bpzh6PksuEQSsaoM3ubZ4Krztseas/Q/ZO4XLlSYdGFw=='', 4, NULL, NULL, ''stockkeeper'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'FullName', N'IsActive', N'LastLoginAtUtc', N'PasswordHash', N'PhoneNumber', N'PinHash', N'Role', N'UpdatedAtUtc', N'UpdatedBy', N'UserName') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (1, NULL, ''6223000011111'', 1, 22.5, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 24.0, N''لبن جهينة كامل الدسم 1 لتر'', 48.0, 27.0, N''رف 1'', 120.0, 1, 4, NULL, NULL, 4.0),
    (2, NULL, ''6223000011128'', 1, 5.25, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 12.0, N''زبادي المراعي 170 جم'', 24.0, 7.5, N''رف 1'', 60.0, 1, 1, NULL, NULL, 24.0),
    (3, NULL, ''6223000011135'', 1, 48.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 6.0, N''جبنة بيضاء دومتي 500 جم'', 12.0, 62.0, N''رف 1'', 18.0, 1, 1, NULL, NULL, 6.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [IsWeighted], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (4, NULL, ''2000000000014'', 1, 210.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 1.0, N''جبنة رومي طازجة (كيلو)'', 2.0, 285.0, N''رف 1'', 4.0, 1, 2, NULL, NULL, 5.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (5, NULL, ''6223000011159'', 2, 14.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 8.0, N''توست أبيض الصفا'', 16.0, 19.0, N''رف 2'', 25.0, 2, 5, NULL, NULL, 3.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [IsWeighted], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (6, NULL, ''2000000000021'', 2, 9.5, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 3.0, N''عيش فينو (كيلو)'', 6.0, 14.0, N''رف 2'', 9.0, 2, 2, NULL, NULL, 1.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (7, NULL, ''6223000011173'', 2, 7.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 10.0, N''كرواسون بالشوكولاتة'', 20.0, 10.0, N''رف 2'', 40.0, 2, 1, NULL, NULL, 2.0),
    (8, NULL, ''6223000011180'', 3, 21.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 24.0, N''كوكا كولا 1.5 لتر'', 48.0, 26.0, N''رف 3'', 96.0, 3, 4, NULL, NULL, 12.0),
    (9, NULL, ''6223000011197'', 3, 4.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 48.0, N''مياه معدنية نستله 600 مل'', 96.0, 6.0, N''رف 3'', 240.0, 3, 4, NULL, NULL, 12.0),
    (10, NULL, ''6223000011203'', 3, 18.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 12.0, N''عصير جهينة مانجو 1 لتر'', 24.0, 23.5, N''رف 3'', 15.0, 3, 4, NULL, NULL, 12.0),
    (11, NULL, ''6223000011210'', 4, 42.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 10.0, N''شاي العروسة 250 جم'', 20.0, 54.0, N''رف 4'', 34.0, 4, 5, NULL, NULL, 1.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [IsWeighted], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (12, NULL, ''6223000011227'', 4, 26.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 20.0, N''أرز مصري فاخر 1 كجم'', 40.0, 33.0, N''رف 4'', 80.0, 4, 2, NULL, NULL, 56.0),
    (13, NULL, ''6223000011234'', 4, 27.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 16.0, N''سكر ناعم 1 كجم'', 32.0, 34.0, N''رف 4'', 64.0, 4, 2, NULL, NULL, 12.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (14, NULL, ''6223000011241'', 4, 78.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 8.0, N''زيت عافية عباد الشمس 1.5 لتر'', 16.0, 96.0, N''رف 4'', 22.0, 4, 5, NULL, NULL, 1.0),
    (15, NULL, ''6223000011258'', 4, 11.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 24.0, N''مكرونة الملكة 400 جم'', 48.0, 15.0, N''رف 4'', 110.0, 4, 5, NULL, NULL, 7.0),
    (16, NULL, ''6223000011265'', 4, 24.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 6.0, N''صلصة طماطم هاينز 320 جم'', 12.0, 32.0, N''رف 4'', 5.0, 4, 5, NULL, NULL, 9.0),
    (17, NULL, ''6223000011272'', 5, 45.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 10.0, N''منظف أرضيات فلاش 1 لتر'', 20.0, 58.0, N''رف 5'', 30.0, 4, 4, NULL, NULL, 26.0),
    (18, NULL, ''6223000011289'', 5, 52.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 6.0, N''سائل غسيل الأطباق فيري 750 مل'', 12.0, 68.0, N''رف 5'', 12.0, 4, 5, NULL, NULL, 15.0),
    (19, NULL, ''6223000011296'', 5, 38.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 8.0, N''مناديل مطبخ فاين 6 لفة'', 16.0, 49.0, N''رف 5'', 21.0, 4, 5, NULL, NULL, 33.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [IsWeighted], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (20, NULL, ''2000000000038'', 6, 165.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 2.0, N''صدور دجاج طازجة (كيلو)'', 4.0, 210.0, N''رف 6'', 6.0, 4, 2, NULL, NULL, 7.0),
    (21, NULL, ''2000000000045'', 6, 320.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 1.0, N''لحم بقري مفروم (كيلو)'', 2.0, 395.0, N''رف 6'', 3.0, 4, 2, NULL, NULL, 1.0),
    (22, NULL, ''2000000000052'', 7, 12.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 20.0, N''بطاطس (كيلو)'', 40.0, 18.0, N''رف 7'', 85.0, 4, 2, NULL, NULL, 5.0),
    (23, NULL, ''2000000000069'', 7, 14.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 15.0, N''طماطم (كيلو)'', 30.0, 19.5, N''رف 7'', 45.0, 4, 2, NULL, NULL, 5.0),
    (24, NULL, ''2000000000076'', 7, 18.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), CAST(1 AS bit), 10.0, N''موز بلدي (كيلو)'', 20.0, 25.0, N''رف 7'', 2.0, 4, 2, NULL, NULL, 50.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'IsWeighted', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AlternativeBarcode], [Barcode], [CategoryId], [CostPrice], [CreatedAtUtc], [CreatedBy], [Description], [DiscountPrice], [ImagePath], [IsActive], [MinimumStockLevel], [Name], [ReorderQuantity], [SellingPrice], [ShelfLocation], [StockQuantity], [SupplierId], [UnitId], [UpdatedAtUtc], [UpdatedBy], [VatRate])
    VALUES (25, NULL, ''6223000011302'', 8, 43.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, NULL, NULL, CAST(1 AS bit), 5.0, N''بطاطس محمرة مجمدة مكعبات 750 جم'', 10.0, 56.0, N''رف 8'', 0.0, 4, 5, NULL, NULL, 1.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AlternativeBarcode', N'Barcode', N'CategoryId', N'CostPrice', N'CreatedAtUtc', N'CreatedBy', N'Description', N'DiscountPrice', N'ImagePath', N'IsActive', N'MinimumStockLevel', N'Name', N'ReorderQuantity', N'SellingPrice', N'ShelfLocation', N'StockQuantity', N'SupplierId', N'UnitId', N'UpdatedAtUtc', N'UpdatedBy', N'VatRate') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CashDifference', N'ClosedAtUtc', N'ClosedBy', N'ClosingCash', N'CreatedAtUtc', N'CreatedBy', N'ExpectedCash', N'InvoiceCount', N'Notes', N'OpenedAtUtc', N'OpeningCash', N'ShiftNumber', N'Status', N'TerminalId', N'TotalCardSales', N'TotalCashSales', N'TotalCreditSales', N'TotalDiscount', N'TotalRefunds', N'TotalSales', N'TotalTax', N'TotalWalletSales', N'UpdatedAtUtc', N'UpdatedBy', N'UserId') AND [object_id] = OBJECT_ID(N'[Shifts]'))
        SET IDENTITY_INSERT [Shifts] ON;
    EXEC(N'INSERT INTO [Shifts] ([Id], [CashDifference], [ClosedAtUtc], [ClosedBy], [ClosingCash], [CreatedAtUtc], [CreatedBy], [ExpectedCash], [InvoiceCount], [Notes], [OpenedAtUtc], [OpeningCash], [ShiftNumber], [Status], [TerminalId], [TotalCardSales], [TotalCashSales], [TotalCreditSales], [TotalDiscount], [TotalRefunds], [TotalSales], [TotalTax], [TotalWalletSales], [UpdatedAtUtc], [UpdatedBy], [UserId])
    VALUES (1, 0.0, NULL, NULL, NULL, ''2026-01-01T00:00:00.0000000Z'', N''seed'', 0.0, 0, NULL, ''2026-01-01T08:00:00.0000000Z'', 500.0, ''SH-20260101-0001'', 1, ''POS-01'', 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, NULL, NULL, 2)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CashDifference', N'ClosedAtUtc', N'ClosedBy', N'ClosingCash', N'CreatedAtUtc', N'CreatedBy', N'ExpectedCash', N'InvoiceCount', N'Notes', N'OpenedAtUtc', N'OpeningCash', N'ShiftNumber', N'Status', N'TerminalId', N'TotalCardSales', N'TotalCashSales', N'TotalCreditSales', N'TotalDiscount', N'TotalRefunds', N'TotalSales', N'TotalTax', N'TotalWalletSales', N'UpdatedAtUtc', N'UpdatedBy', N'UserId') AND [object_id] = OBJECT_ID(N'[Shifts]'))
        SET IDENTITY_INSERT [Shifts] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BalanceAfter', N'CreatedAtUtc', N'CreatedBy', N'InvoiceId', N'MovementType', N'PerformedBy', N'ProductId', N'PurchaseOrderId', N'Quantity', N'Reason', N'ReferenceNumber', N'ShiftId', N'UnitCost', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[StockMovements]'))
        SET IDENTITY_INSERT [StockMovements] ON;
    EXEC(N'INSERT INTO [StockMovements] ([Id], [BalanceAfter], [CreatedAtUtc], [CreatedBy], [InvoiceId], [MovementType], [PerformedBy], [ProductId], [PurchaseOrderId], [Quantity], [Reason], [ReferenceNumber], [ShiftId], [UnitCost], [UpdatedAtUtc], [UpdatedBy])
    VALUES (1, 120.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 1, NULL, 120.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 22.5, NULL, NULL),
    (2, 60.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 2, NULL, 60.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 5.25, NULL, NULL),
    (3, 18.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 3, NULL, 18.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 48.0, NULL, NULL),
    (4, 4.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 4, NULL, 4.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 210.0, NULL, NULL),
    (5, 25.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 5, NULL, 25.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 14.0, NULL, NULL),
    (6, 9.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 6, NULL, 9.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 9.5, NULL, NULL),
    (7, 40.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 7, NULL, 40.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 7.0, NULL, NULL),
    (8, 96.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 8, NULL, 96.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 21.0, NULL, NULL),
    (9, 240.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 9, NULL, 240.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 4.0, NULL, NULL),
    (10, 15.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 10, NULL, 15.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 18.0, NULL, NULL),
    (11, 34.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 11, NULL, 34.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 42.0, NULL, NULL),
    (12, 80.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 12, NULL, 80.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 26.0, NULL, NULL),
    (13, 64.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 13, NULL, 64.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 27.0, NULL, NULL),
    (14, 22.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 14, NULL, 22.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 78.0, NULL, NULL),
    (15, 110.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 15, NULL, 110.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 11.0, NULL, NULL),
    (16, 5.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 16, NULL, 5.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 24.0, NULL, NULL),
    (17, 30.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 17, NULL, 30.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 45.0, NULL, NULL),
    (18, 12.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 18, NULL, 12.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 52.0, NULL, NULL),
    (19, 21.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 19, NULL, 21.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 38.0, NULL, NULL),
    (20, 6.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 20, NULL, 6.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 165.0, NULL, NULL),
    (21, 3.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 21, NULL, 3.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 320.0, NULL, NULL),
    (22, 85.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 22, NULL, 85.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 12.0, NULL, NULL),
    (23, 45.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 23, NULL, 45.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 14.0, NULL, NULL),
    (24, 2.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 24, NULL, 2.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 18.0, NULL, NULL),
    (25, 0.0, ''2026-01-01T00:00:00.0000000Z'', N''seed'', NULL, 1, N''seed'', 25, NULL, 0.0, N''رصيد افتتاحي'', ''OPENING-2026'', NULL, 43.0, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BalanceAfter', N'CreatedAtUtc', N'CreatedBy', N'InvoiceId', N'MovementType', N'PerformedBy', N'ProductId', N'PurchaseOrderId', N'Quantity', N'Reason', N'ReferenceNumber', N'ShiftId', N'UnitCost', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[StockMovements]'))
        SET IDENTITY_INSERT [StockMovements] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentCategoryId] ON [Categories] ([ParentCategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_PhoneNumber] ON [Customers] ([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceItems_InvoiceId] ON [InvoiceItems] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvoiceItems_ProductId] ON [InvoiceItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_CustomerId] ON [Invoices] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_Date] ON [Invoices] ([InvoiceDateUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Invoices_InvoiceNumber] ON [Invoices] ([InvoiceNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_Shift_Status] ON [Invoices] ([ShiftId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoices_UserId] ON [Invoices] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_InvoiceId_Method] ON [Payments] ([InvoiceId], [Method]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PriceHistories_Product_Date] ON [PriceHistories] ([ProductId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Active_Stock] ON [Products] ([IsActive], [StockQuantity]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Products_Barcode_Unique] ON [Products] ([Barcode]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Name] ON [Products] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_SupplierId] ON [Products] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_UnitId] ON [Products] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderItems_ProductId] ON [PurchaseOrderItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderItems_PurchaseOrderId] ON [PurchaseOrderItems] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_OrderDateUtc] ON [PurchaseOrders] ([OrderDateUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PurchaseOrders_OrderNumber] ON [PurchaseOrders] ([OrderNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_SupplierId] ON [PurchaseOrders] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrders_UserId] ON [PurchaseOrders] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShiftReports_ShiftId_ReportType] ON [ShiftReports] ([ShiftId], [ReportType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Shifts_ShiftNumber] ON [Shifts] ([ShiftNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shifts_User_Terminal_Status] ON [Shifts] ([UserId], [TerminalId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockCountItems_ProductId] ON [StockCountItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockCountItems_StockCountId_ProductId] ON [StockCountItems] ([StockCountId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StockCounts_ReferenceNumber] ON [StockCounts] ([ReferenceNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_InvoiceId] ON [StockMovements] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_Product_Date] ON [StockMovements] ([ProductId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_PurchaseOrderId] ON [StockMovements] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_ShiftId] ON [StockMovements] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_Type] ON [StockMovements] ([MovementType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_Name] ON [Suppliers] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Suppliers_Phone] ON [Suppliers] ([PhoneNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Units_Name] ON [Units] ([Name]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPermissions_UserId_PermissionKey] ON [UserPermissions] ([UserId], [PermissionKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_UserName] ON [Users] ([UserName]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105175130_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105175130_InitialCreate', N'8.0.33');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105181139_AddHeldCarts'
)
BEGIN
    CREATE TABLE [HeldCarts] (
        [Id] int NOT NULL IDENTITY,
        [Label] nvarchar(120) NOT NULL,
        [UserId] int NOT NULL,
        [ShiftId] int NULL,
        [TerminalId] varchar(60) NOT NULL DEFAULT 'POS-01',
        [CartJson] nvarchar(4000) NOT NULL,
        [LineCount] int NOT NULL,
        [TotalQuantity] decimal(18,3) NOT NULL,
        [EstimatedTotal] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(120) NULL,
        [UpdatedBy] nvarchar(120) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_HeldCarts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HeldCarts_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_HeldCarts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105181139_AddHeldCarts'
)
BEGIN
    CREATE INDEX [IX_HeldCarts_ShiftId] ON [HeldCarts] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105181139_AddHeldCarts'
)
BEGIN
    CREATE INDEX [IX_HeldCarts_UserId_IsDeleted] ON [HeldCarts] ([UserId], [IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105181139_AddHeldCarts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105181139_AddHeldCarts', N'8.0.33');
END;
GO

COMMIT;
GO

-- ---------------------------------------------------------------------------------------------
-- Seed data added with Phase 5: per-user permission overrides.
-- The application falls back to the role defaults for every key that is NOT listed here; these rows
-- only record the deliberate deviations. They are inserted only when the table is still empty so the
-- script stays idempotent and never overwrites the choices made in the admin screens.
-- ---------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [UserPermissions])
BEGIN
    -- Cashier 2 (محمود السيد): the trusted cashier may grant invoice discounts.
    -- Everything else follows the Cashier role defaults.
    INSERT INTO [UserPermissions] ([CreatedAtUtc], [CreatedBy], [IsGranted], [IsDeleted], [PermissionKey], [UserId])
    VALUES ('2026-01-01T00:00:00.0000000Z', N'seed', CAST(1 AS bit), CAST(0 AS bit), N'Pos.Discount', 2);

    -- Stock keeper 4 (خالد مصطفى): read-only catalogue, plus the low stock alerts of the dashboard.
    INSERT INTO [UserPermissions] ([CreatedAtUtc], [CreatedBy], [IsGranted], [IsDeleted], [PermissionKey], [UserId])
    VALUES ('2026-01-01T00:00:00.0000000Z', N'seed', CAST(0 AS bit), CAST(0 AS bit), N'Catalog.Edit', 4),
           ('2026-01-01T00:00:00.0000000Z', N'seed', CAST(1 AS bit), CAST(0 AS bit), N'Dashboard.View', 4);
END;
GO

