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

    public StoreSettingsService(IDbContextFactory<SupermarketDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
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
                AutoGenerateBarcode = true
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
        }

        await db.SaveChangesAsync(cancellationToken);
        Invalidate();
    }

    public void Invalidate() => _cached = null;
}
