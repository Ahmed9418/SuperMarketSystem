using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Data;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Builds the sequential document numbers used by the invoices, the purchase orders,
/// the stock counts and the shifts.
/// Format: <c>PREFIX-yyyyMMdd-####</c> (e.g. <c>INV-20261105-0007</c>).
/// The next value is derived from the highest number already stored for the current day,
/// which is sufficient for the single-store deployment targeted by this solution.
/// </summary>
public sealed class NumberSequenceService : INumberSequenceService
{
    private readonly IDbContextFactory<SupermarketDbContext> _contextFactory;
    private readonly IDateTimeProvider _clock;

    public NumberSequenceService(IDbContextFactory<SupermarketDbContext> contextFactory, IDateTimeProvider clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken = default) =>
        NextAsync("INV", (db, prefix, ct) => db.Invoices.IgnoreQueryFilters()
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public Task<string> NextShiftNumberAsync(CancellationToken cancellationToken = default) =>
        NextAsync("SH", (db, prefix, ct) => db.Shifts.IgnoreQueryFilters()
            .Where(s => s.ShiftNumber.StartsWith(prefix))
            .OrderByDescending(s => s.ShiftNumber)
            .Select(s => s.ShiftNumber)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public Task<string> NextPurchaseOrderNumberAsync(CancellationToken cancellationToken = default) =>
        NextAsync("PO", (db, prefix, ct) => db.PurchaseOrders.IgnoreQueryFilters()
            .Where(p => p.OrderNumber.StartsWith(prefix))
            .OrderByDescending(p => p.OrderNumber)
            .Select(p => p.OrderNumber)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public Task<string> NextStockCountNumberAsync(CancellationToken cancellationToken = default) =>
        NextAsync("SC", (db, prefix, ct) => db.StockCounts.IgnoreQueryFilters()
            .Where(s => s.ReferenceNumber.StartsWith(prefix))
            .OrderByDescending(s => s.ReferenceNumber)
            .Select(s => s.ReferenceNumber)
            .FirstOrDefaultAsync(ct), cancellationToken);

    /// <summary>Shared implementation: read the latest matching document number and increment it.</summary>
    private async Task<string> NextAsync(
        string prefix,
        Func<SupermarketDbContext, string, CancellationToken, Task<string?>> readLatest,
        CancellationToken cancellationToken)
    {
        var fullPrefix = $"{prefix}-{_clock.Now:yyyyMMdd}-";

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var latest = await readLatest(db, fullPrefix, cancellationToken);

        var next = 1;
        if (!string.IsNullOrWhiteSpace(latest) && latest.StartsWith(fullPrefix, StringComparison.Ordinal))
        {
            var tail = latest[fullPrefix.Length..];
            if (int.TryParse(tail, out var parsed))
            {
                next = parsed + 1;
            }
        }

        return $"{fullPrefix}{next:D4}";
    }
}
