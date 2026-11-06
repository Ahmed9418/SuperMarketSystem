using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Data;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Reads the single-row <see cref="StoreSetting"/> table once per application session and caches it,
/// because the POS header and the receipt formatter read it on every transaction.
/// A pooled context factory is used so the cached instance never keeps a DbContext alive.
/// </summary>
public sealed class StoreSettingsService : IStoreSettingsService
{
    private readonly IDbContextFactory<SupermarketDbContext> _contextFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private StoreSetting? _cached;
    private static bool _columnsEnsured;
    private static readonly object _columnLock = new();

    public StoreSettingsService(IDbContextFactory<SupermarketDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private static async Task EnsureColumnsAsync(SupermarketDbContext db, CancellationToken cancellationToken)
    {
        if (_columnsEnsured)
        {
            return;
        }

        lock (_columnLock)
        {
            if (_columnsEnsured)
            {
                return;
            }
        }

        try
        {
            if (db.Database.IsSqlServer())
            {
                await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StoreSettings') AND name = 'InvoicePaperSize')
BEGIN
    ALTER TABLE StoreSettings ADD InvoicePaperSize nvarchar(20) NOT NULL CONSTRAINT DF_StoreSettings_PaperSize DEFAULT '80mm';
END
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StoreSettings') AND name = 'LogoPath')
BEGIN
    ALTER TABLE StoreSettings ADD LogoPath nvarchar(500) NULL;
END
", cancellationToken);
            }
        }
        catch
        {
            // Defensive: if migrations or table not created yet, do not throw.
        }

        lock (_columnLock)
        {
            _columnsEnsured = true;
        }
    }

    public async Task<StoreSetting> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await EnsureColumnsAsync(db, cancellationToken);
            var settings = await db.StoreSettings
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // Defensive fallback: the seeder always inserts row 1, but a manual database
            // cleanup must never crash the POS at start-up.
            _cached = settings ?? new StoreSetting
            {
                StoreName = "سوبر ماركت",
                ReceiptLineWidth = 42,
                DecimalPlaces = 2,
                PricesIncludeVat = true,
                DefaultVatRate = Common.DomainConstants.DefaultVatRate,
                AutoGenerateBarcode = true,
                InvoicePaperSize = "80mm",
                LogoPath = null
            };

            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(StoreSetting settings, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureColumnsAsync(db, cancellationToken);

        var existing = await db.StoreSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            settings.Id = 0;
            db.StoreSettings.Add(settings);
        }
        else
        {
            existing.StoreName = settings.StoreName;
            existing.Address = settings.Address;
            existing.PhoneNumber = settings.PhoneNumber;
            existing.TaxNumber = settings.TaxNumber;
            existing.ReceiptFooterMessage = settings.ReceiptFooterMessage;
            existing.ReceiptPrinterName = settings.ReceiptPrinterName;
            existing.ReceiptLineWidth = settings.ReceiptLineWidth;
            existing.DefaultVatRate = settings.DefaultVatRate;
            existing.PricesIncludeVat = settings.PricesIncludeVat;
            existing.DecimalPlaces = settings.DecimalPlaces;
            existing.AutoGenerateBarcode = settings.AutoGenerateBarcode;
            existing.InvoicePaperSize = string.IsNullOrWhiteSpace(settings.InvoicePaperSize) ? "80mm" : settings.InvoicePaperSize;
            existing.LogoPath = settings.LogoPath;
        }

        await db.SaveChangesAsync(cancellationToken);
        Invalidate();
    }

    public void Invalidate() => _cached = null;
}
