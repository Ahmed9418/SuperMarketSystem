using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Read-only aggregations for the admin dashboard. All queries are executed as SQL aggregates,
/// never by loading entities into memory.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly SupermarketDbContext _db;
    private readonly IDateTimeProvider _clock;

    public DashboardService(SupermarketDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var todayStartUtc = _clock.Now.Date.ToUniversalTime();
        var tomorrowStartUtc = todayStartUtc.AddDays(1);
        var monthStartUtc = new DateTime(_clock.Now.Year, _clock.Now.Month, 1).ToUniversalTime();

        var summary = new DashboardSummaryDto
        {
            TotalProducts = await _db.Products.CountAsync(cancellationToken),
            ActiveProducts = await _db.Products.CountAsync(p => p.IsActive, cancellationToken),
            LowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= p.MinimumStockLevel, cancellationToken),
            OutOfStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= 0m, cancellationToken),
            TotalCategories = await _db.Categories.CountAsync(cancellationToken),
            TotalSuppliers = await _db.Suppliers.CountAsync(cancellationToken),
            TotalUsers = await _db.Users.CountAsync(cancellationToken),
            TotalStockValue = await _db.Products
                .Where(p => p.IsActive)
                .SumAsync(p => p.StockQuantity * p.CostPrice, cancellationToken),
            OpenShifts = await _db.Shifts.CountAsync(s => s.Status == ShiftStatus.Open, cancellationToken),
            GeneratedAtUtc = _clock.UtcNow
        };

        var todayInvoices = _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Completed
                        && i.InvoiceDateUtc >= todayStartUtc
                        && i.InvoiceDateUtc < tomorrowStartUtc);

        summary.TodayInvoiceCount = await todayInvoices.CountAsync(cancellationToken);
        summary.TodaySales = await todayInvoices.SumAsync(i => (decimal?)i.TotalAmount, cancellationToken) ?? 0m;

        var monthInvoices = _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Completed && i.InvoiceDateUtc >= monthStartUtc);

        summary.MonthSales = await monthInvoices.SumAsync(i => (decimal?)i.TotalAmount, cancellationToken) ?? 0m;

        summary.MonthProfit = await _db.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice!.Status == InvoiceStatus.Completed && ii.Invoice.InvoiceDateUtc >= monthStartUtc)
            .SumAsync(ii => (decimal?)((ii.UnitPrice - ii.UnitCost) * ii.Quantity - ii.DiscountAmount), cancellationToken) ?? 0m;

        return summary;
    }
}
