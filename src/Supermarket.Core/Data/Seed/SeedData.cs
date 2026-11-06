using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Seed;

/// <summary>
/// Everything the seeded database needs: units, categories, suppliers, demo products,
/// the two default users, the store settings row and one open cashier shift.
/// All Arabic text is stored as nvarchar thanks to the Unicode configuration.
/// </summary>
internal static class SeedData
{
    internal static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ----------------------------------------------------------------- Users
    //
    // IMPORTANT
    // The password / PIN hashes below are FIXED values, they are never produced at build time.
    // PBKDF2 salts are random, so hashing inside the model seeding would make EF Core consider the
    // model "changed" on every build and scaffold a spurious data migration.
    // The constants are PBKDF2-SHA256 (Microsoft.AspNetCore.Identity v3 format) hashes of:
    //   admin       -> Admin@123    PIN 1111
    //   cashier     -> Cashier@123  PIN 2222
    //   manager     -> Manager@123  PIN 3333
    //   stockkeeper -> Stock@123    PIN 4444
    // `PasswordHasher<User>.VerifyHashedPassword` validates them at sign-in.
    internal static readonly User Admin = CreateUser(
        id: 1,
        userName: "admin",
        fullName: "أحمد عبد الرحمن",
        role: Common.UserRole.Administrator,
        phone: "01000000001",
        passwordHash: "AQAAAAIAAYagAAAAEHo+tjmWUP5eDBGx2UMKzfmY2Repq9HXP5vAv0f1iL71mG10EJdlj8yui0/XTvM/Zw==",
        pinHash: "AQAAAAIAAYagAAAAEDn60arXI8DRG9j5TS9tIM8i1ROo//UvhJPMh08kkuOzOm8kKIP1ONCEHK0j14hrQA==");

    internal static readonly User Cashier = CreateUser(
        id: 2,
        userName: "cashier",
        fullName: "محمود السيد",
        role: Common.UserRole.Cashier,
        phone: "01000000002",
        passwordHash: "AQAAAAIAAYagAAAAEJ1mbCWmq8jgHumh7b2LNUch3no/rTW34acisg+LBFKutSf+SCIzjwC90ksjlUTBbw==",
        pinHash: "AQAAAAIAAYagAAAAEOwa2h+1iYAGVnRlNR91rfKfDVmu1mfLDNMThVGlCOz4iHXH2zu8jUB2YeAl1iA3bQ==");

    internal static readonly User Manager = CreateUser(
        id: 3,
        userName: "manager",
        fullName: "سارة إبراهيم",
        role: Common.UserRole.Manager,
        phone: "01000000003",
        passwordHash: "AQAAAAIAAYagAAAAEAG96yFRFwHSPaN++Sx5xwTCer8/O/z8+Xg/8Ny9fisul95zBepcJnyTZ7mqpV1f7A==",
        pinHash: "AQAAAAIAAYagAAAAEHTJRagas15v+RMepSM9UVTxlrC7Ac44UC1Gl8W1DlmuPe6DrjS9/80+dS9UDb3IQQ==");

    internal static readonly User StockKeeper = CreateUser(
        id: 4,
        userName: "stockkeeper",
        fullName: "خالد مصطفى",
        role: Common.UserRole.StockKeeper,
        phone: "01000000004",
        passwordHash: "AQAAAAIAAYagAAAAENF54TPclEard4RbQ5+XzRyqWHl55IQixK9Xw2uk/KFaCQnDg5kGpFVIgyQAJ0W8zg==",
        pinHash: "AQAAAAIAAYagAAAAEJrTSXhcqvfj4sUe1syTx5Bpzh6PksuEQSsaoM3ubZ4Krztseas/Q/ZO4XLlSYdGFw==");

    private static User CreateUser(
        int id,
        string userName,
        string fullName,
        Common.UserRole role,
        string phone,
        string passwordHash,
        string pinHash) => new()
    {
        Id = id,
        UserName = userName,
        FullName = fullName,
        Role = role,
        PhoneNumber = phone,
        PasswordHash = passwordHash,
        PinHash = pinHash,
        IsActive = true,
        CreatedAtUtc = SeedTimestamp,
        CreatedBy = "seed"
    };

    // ----------------------------------------------------------------- Units
    internal static readonly Unit[] Units =
    {
        new() { Id = 1, Name = "قطعة", Symbol = "قطعة", AllowFractions = false, CreatedAtUtc = SeedTimestamp },
        new() { Id = 2, Name = "كيلوجرام", Symbol = "كجم", AllowFractions = true, CreatedAtUtc = SeedTimestamp },
        new() { Id = 3, Name = "جرام", Symbol = "جم", AllowFractions = true, CreatedAtUtc = SeedTimestamp },
        new() { Id = 4, Name = "لتر", Symbol = "لتر", AllowFractions = true, CreatedAtUtc = SeedTimestamp },
        new() { Id = 5, Name = "عبوة", Symbol = "عبوة", AllowFractions = false, CreatedAtUtc = SeedTimestamp },
        new() { Id = 6, Name = "كرتونة", Symbol = "كرتونة", AllowFractions = false, CreatedAtUtc = SeedTimestamp }
    };

    // ------------------------------------------------------------ Categories
    internal static readonly Category[] Categories =
    {
        new() { Id = 1, Name = "ألبان وأجبان", DisplayOrder = 1, CreatedAtUtc = SeedTimestamp },
        new() { Id = 2, Name = "مخبوزات", DisplayOrder = 2, CreatedAtUtc = SeedTimestamp },
        new() { Id = 3, Name = "مشروبات", DisplayOrder = 3, CreatedAtUtc = SeedTimestamp },
        new() { Id = 4, Name = "بقالة جافة", DisplayOrder = 4, CreatedAtUtc = SeedTimestamp },
        new() { Id = 5, Name = "منظفات", DisplayOrder = 5, CreatedAtUtc = SeedTimestamp },
        new() { Id = 6, Name = "لحوم ودواجن", DisplayOrder = 6, CreatedAtUtc = SeedTimestamp },
        new() { Id = 7, Name = "خضار وفاكهة", DisplayOrder = 7, CreatedAtUtc = SeedTimestamp },
        new() { Id = 8, Name = "مجمدات", DisplayOrder = 8, CreatedAtUtc = SeedTimestamp }
    };

    // ------------------------------------------------------------- Suppliers
    internal static readonly Supplier[] Suppliers =
    {
        new()
        {
            Id = 1, Name = "شركة النيل للألبان", ContactPerson = "مصطفى كامل", PhoneNumber = "0223456789",
            Email = "sales@nile-dairy.example", Address = "المنطقة الصناعية - أسيوط", TaxNumber = "200-145-987",
            Balance = 4800m, CreatedAtUtc = SeedTimestamp
        },
        new()
        {
            Id = 2, Name = "مخابز الصفا", ContactPerson = "إبراهيم فتحي", PhoneNumber = "01122334455",
            Email = "orders@alsafa-bakery.example", Address = "شارع الجمهورية - سوهاج", TaxNumber = "200-778-114",
            Balance = 1200m, CreatedAtUtc = SeedTimestamp
        },
        new()
        {
            Id = 3, Name = "توزيع الشرق للمشروبات", ContactPerson = "هاني رمزي", PhoneNumber = "01099887766",
            Email = "info@east-beverages.example", Address = "مدينة نصر - القاهرة", TaxNumber = "200-554-332",
            Balance = 9500m, CreatedAtUtc = SeedTimestamp
        },
        new()
        {
            Id = 4, Name = "مجموعة الدلتا للتوريدات", ContactPerson = "ناهد سمير", PhoneNumber = "01555443322",
            Email = "supply@delta-group.example", Address = "طنطا - الغربية", TaxNumber = "200-901-223",
            Balance = 0m, CreatedAtUtc = SeedTimestamp
        }
    };

    // -------------------------------------------------------------- Products
    internal static readonly Product[] Products =
    {
        Product(1, "لبن جهينة كامل الدسم 1 لتر", "6223000011111", 1, 4, 1, 22.50m, 27.00m, 4, 120m, 24m, false),
        Product(2, "زبادي المراعي 170 جم", "6223000011128", 1, 1, 1, 5.25m, 7.50m, 24, 60m, 12m, false),
        Product(3, "جبنة بيضاء دومتي 500 جم", "6223000011135", 1, 1, 1, 48.00m, 62.00m, 6, 18m, 6m, false),
        Product(4, "جبنة رومي طازجة (كيلو)", "2000000000014", 1, 2, 1, 210.00m, 285.00m, 5, 4m, 1m, true),
        Product(5, "توست أبيض الصفا", "6223000011159", 2, 5, 2, 14.00m, 19.00m, 3, 25m, 8m, false),
        Product(6, "عيش فينو (كيلو)", "2000000000021", 2, 2, 2, 9.50m, 14.00m, 1, 9m, 3m, true),
        Product(7, "كرواسون بالشوكولاتة", "6223000011173", 2, 1, 2, 7.00m, 10.00m, 2, 40m, 10m, false),
        Product(8, "كوكا كولا 1.5 لتر", "6223000011180", 3, 4, 3, 21.00m, 26.00m, 12, 96m, 24m, false),
        Product(9, " مياه معدنية نستله 600 مل", "6223000011197", 3, 4, 3, 4.00m, 6.00m, 12, 240m, 48m, false),
        Product(10, "عصير جهينة مانجو 1 لتر", "6223000011203", 3, 4, 3, 18.00m, 23.50m, 12, 15m, 12m, false),
        Product(11, "شاي العروسة 250 جم", "6223000011210", 4, 5, 4, 42.00m, 54.00m, 1, 34m, 10m, false),
        Product(12, "أرز مصري فاخر 1 كجم", "6223000011227", 4, 2, 4, 26.00m, 33.00m, 56, 80m, 20m, true),
        Product(13, "سكر ناعم 1 كجم", "6223000011234", 4, 2, 4, 27.00m, 34.00m, 12, 64m, 16m, true),
        Product(14, "زيت عافية عباد الشمس 1.5 لتر", "6223000011241", 4, 5, 4, 78.00m, 96.00m, 1, 22m, 8m, false),
        Product(15, "مكرونة الملكة 400 جم", "6223000011258", 4, 5, 4, 11.00m, 15.00m, 7, 110m, 24m, false),
        Product(16, "صلصة طماطم هاينز 320 جم", "6223000011265", 4, 5, 4, 24.00m, 32.00m, 9, 5m, 6m, false),
        Product(17, "منظف أرضيات فلاش 1 لتر", "6223000011272", 5, 4, 4, 45.00m, 58.00m, 26, 30m, 10m, false),
        Product(18, "سائل غسيل الأطباق فيري 750 مل", "6223000011289", 5, 5, 4, 52.00m, 68.00m, 15, 12m, 6m, false),
        Product(19, "مناديل مطبخ فاين 6 لفة", "6223000011296", 5, 5, 4, 38.00m, 49.00m, 33, 21m, 8m, false),
        Product(20, "صدور دجاج طازجة (كيلو)", "2000000000038", 6, 2, 4, 165.00m, 210.00m, 7, 6m, 2m, true),
        Product(21, "لحم بقري مفروم (كيلو)", "2000000000045", 6, 2, 4, 320.00m, 395.00m, 1, 3m, 1m, true),
        Product(22, "بطاطس (كيلو)", "2000000000052", 7, 2, 4, 12.00m, 18.00m, 5, 85m, 20m, true),
        Product(23, "طماطم (كيلو)", "2000000000069", 7, 2, 4, 14.00m, 19.50m, 5, 45m, 15m, true),
        Product(24, "موز بلدي (كيلو)", "2000000000076", 7, 2, 4, 18.00m, 25.00m, 50, 2m, 10m, true),
        Product(25, "بطاطس محمرة مجمدة مكعبات 750 جم", "6223000011302", 8, 5, 4, 43.00m, 56.00m, 1, 0m, 5m, false)
    };

    private static Product Product(
        int id, string name, string barcode, int categoryId, int unitId, int supplierId,
        decimal cost, decimal price, decimal vatRate, decimal stock, decimal minimum, bool weighted) => new()
    {
        Id = id,
        Name = name.Trim(),
        Barcode = barcode,
        CategoryId = categoryId,
        UnitId = unitId,
        SupplierId = supplierId,
        CostPrice = cost,
        SellingPrice = price,
        VatRate = vatRate,
        StockQuantity = stock,
        MinimumStockLevel = minimum,
        ReorderQuantity = Math.Max(minimum, 1m) * 2m,
        IsWeighted = weighted,
        IsActive = true,
        ShelfLocation = $"رف {categoryId}",
        CreatedAtUtc = SeedTimestamp,
        CreatedBy = "seed"
    };

    // ---------------------------------------------------------- Store setting
    internal static readonly StoreSetting StoreSetting = new()
    {
        Id = 1,
        StoreName = "سوبر ماركت الأمانة",
        Address = "شارع الجمهورية - أخميم - سوهاج",
        PhoneNumber = "01001234567",
        TaxNumber = "512-336-778",
        ReceiptFooterMessage = "شكراً لزيارتكم - نتشرف بزيارتكم مرة أخرى",
        ReceiptPrinterName = "XP-80C",
        ReceiptLineWidth = 42,
        DefaultVatRate = 0.14m,
        PricesIncludeVat = true,
        DecimalPlaces = 2,
        AutoGenerateBarcode = true,
        CreatedAtUtc = SeedTimestamp,
        CreatedBy = "seed"
    };

    // ---------------------------------------------------------------- Shift
    internal static readonly Shift OpenShift = new()
    {
        Id = 1,
        ShiftNumber = "SH-20260101-0001",
        UserId = Cashier.Id,
        TerminalId = "POS-01",
        OpenedAtUtc = SeedTimestamp.AddHours(8),
        Status = Common.ShiftStatus.Open,
        OpeningCash = 500m,
        CreatedAtUtc = SeedTimestamp,
        CreatedBy = "seed"
    };

    // --------------------------------------------------- Stock opening ledger
    //
    // Every seeded product gets one inbound movement equal to its opening quantity, so the
    // ledger sum reconciles exactly with Product.StockQuantity from the very first run.
    internal static StockMovement[] BuildOpeningMovements() =>
        Products.Select((p, index) => new StockMovement
        {
            Id = index + 1,
            ProductId = p.Id,
            MovementType = Common.StockMovementType.Purchase,
            Quantity = p.StockQuantity,
            BalanceAfter = p.StockQuantity,
            UnitCost = p.CostPrice,
            ReferenceNumber = "OPENING-2026",
            Reason = "رصيد افتتاحي",
            PerformedBy = "seed",
            CreatedAtUtc = SeedTimestamp,
            CreatedBy = "seed",
            IsDeleted = false
        }).ToArray();

    /// <summary>
    /// Applies the seed data with <c>HasData</c>. Seeding through the model builder keeps the
    /// migration fully deterministic (no runtime code, no random values, fixed timestamps).
    /// </summary>
    internal static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(Admin, Cashier, Manager, StockKeeper);
        modelBuilder.Entity<Unit>().HasData(Units);
        modelBuilder.Entity<Category>().HasData(Categories);
        modelBuilder.Entity<Supplier>().HasData(Suppliers);
        modelBuilder.Entity<Product>().HasData(Products);
        modelBuilder.Entity<StoreSetting>().HasData(StoreSetting);
        modelBuilder.Entity<Shift>().HasData(OpenShift);
        modelBuilder.Entity<StockMovement>().HasData(BuildOpeningMovements());
    }
}
