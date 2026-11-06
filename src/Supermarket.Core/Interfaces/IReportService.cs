using Supermarket.Core.Common;
using Supermarket.Core.DTOs;

namespace Supermarket.Core.Interfaces;

/// <summary>
/// Reporting contracts of Phase 5 (analytics, invoice browser and the exported reports).
/// Every method is read-only and safe to call from several screens at once.
/// </summary>
public interface IReportService
{
    /// <summary>KPI tiles + chart series + tables for one period, in a single round trip.</summary>
    Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(ReportFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Paged invoice browser (search by number / customer, filter by cashier, method, status).</summary>
    Task<PagedResult<InvoiceListDto>> GetInvoicesAsync(InvoiceFilter filter, CancellationToken cancellationToken = default);

    /// <summary>One invoice with its lines (detail panel, reprint, audit).</summary>
    Task<InvoiceDetailDto?> GetInvoiceDetailAsync(int invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Best selling products of the period, ordered by revenue.</summary>
    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(ReportFilter filter, int take = 10, CancellationToken cancellationToken = default);

    /// <summary>Daily sales / profit / invoice count series (used by the dashboard line chart).</summary>
    Task<IReadOnlyList<DailySalesDto>> GetDailySalesAsync(ReportFilter filter, CancellationToken cancellationToken = default);
}
