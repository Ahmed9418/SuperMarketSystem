using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Core.Common;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Supermarket.Core.Tests;
using Supermarket.POS.Services;
using Supermarket.POS.ViewModels;

namespace Supermarket.POS.Tests;

public sealed class MockPosDialogService : IDialogService
{
    public List<string> Informations { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Errors { get; } = new();

    public bool ConfirmResult { get; set; } = true;
    public string? PromptResult { get; set; }

    public void ShowInformation(string message, string? title = null) =>
        Informations.Add($"{title ?? "معلومة"}: {message}");

    public void ShowWarning(string message, string? title = null) =>
        Warnings.Add($"{title ?? "تنبيه"}: {message}");

    public void ShowError(string message, string? title = null) =>
        Errors.Add($"{title ?? "خطأ"}: {message}");

    public bool Confirm(string message, string? title = null) => ConfirmResult;

    public string? Prompt(string message, string? title = null, string? defaultValue = null) =>
        PromptResult ?? defaultValue;
}

public sealed class MockReceiptPrinterService : IReceiptPrinterService
{
    public bool IsPrinterAvailable => true;
    public List<string> PrintedTexts { get; } = new();

    public Task<PrintResult> PrintAsync(string receiptText, string? printerName = null, CancellationToken cancellationToken = default)
    {
        PrintedTexts.Add(receiptText);
        return Task.FromResult(new PrintResult { Succeeded = true, Message = "تمت الطباعة بنجاح" });
    }

    public Task<PrintResult> PrintTestAsync(string? printerName = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PrintResult { Succeeded = true, Message = "تمت طباعة الاختبار" });
    }

    public string ResolvePrinterName(string? printerName = null) => printerName ?? "XP-80C";
}

public sealed class PosTestFixture : IDisposable
{
    public SqliteTestDatabase Database { get; }
    public FixedClock Clock { get; }
    public MockPosDialogService Dialogs { get; }
    public MockReceiptPrinterService Printer { get; }
    public IReceiptFormatter Formatter { get; }

    public IProductService ProductService { get; }
    public ISalesService SalesService { get; }
    public IShiftService ShiftService { get; }
    public IStoreSettingsService StoreSettings { get; }
    public ICatalogService CatalogService { get; }
    public IHeldCartService HeldCartService { get; }
    public IStockService StockService { get; }

    public PosTestFixture()
    {
        Database = new SqliteTestDatabase();
        Clock = new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
        Dialogs = new MockPosDialogService();
        Printer = new MockReceiptPrinterService();
        Formatter = new ReceiptFormatter();

        var contextFactory = new TestContextFactory(Database);
        StockService = new StockService(Database.Context, NullLogger<StockService>.Instance);
        StoreSettings = new StoreSettingsService(contextFactory);
        ProductService = new ProductService(Database.Context, StockService, StoreSettings, NullLogger<ProductService>.Instance);
        CatalogService = new CatalogService(Database.Context, NullLogger<CatalogService>.Instance);
        HeldCartService = new HeldCartService(Database.Context, NullLogger<HeldCartService>.Instance);

        var numberSequence = new NumberSequenceService(contextFactory, Clock);
        SalesService = new SalesService(Database.Context, StockService, numberSequence, StoreSettings, Clock, NullLogger<SalesService>.Instance);
        ShiftService = new ShiftService(Database.Context, numberSequence, Formatter, StoreSettings, Clock, NullLogger<ShiftService>.Instance);
    }

    public SalesViewModel CreateSalesViewModel()
    {
        return new SalesViewModel(
            ProductService,
            SalesService,
            ShiftService,
            StoreSettings,
            CatalogService,
            HeldCartService,
            Dialogs);
    }

    public PaymentViewModel CreatePaymentViewModel()
    {
        return new PaymentViewModel(CatalogService, SalesService, Dialogs);
    }

    public ShiftCloseViewModel CreateShiftCloseViewModel()
    {
        return new ShiftCloseViewModel(ShiftService, Formatter, StoreSettings, Printer, Dialogs);
    }

    public void Dispose() => Database.Dispose();
}
