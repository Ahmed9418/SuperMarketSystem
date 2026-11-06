using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Inventory read models plus the manual correction workflow.
/// Every write goes through <see cref="IStockService"/> so the ledger stays the single source of truth.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private readonly SupermarketDbContext _db;
    private readonly IStockService _stockService;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(SupermarketDbContext db, IStockService stockService, ILogger<InventoryService> logger)
    {
        _db = db;
        _stockService = stockService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementFilter filter, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);

        var query = _db.StockMovements.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(m => m.Product!.Name.Contains(term) || m.Product.Barcode.Contains(term));
        }

        if (filter.ProductId is > 0)
        {
            query = query.Where(m => m.ProductId == filter.ProductId);
        }

        if (filter.MovementType.HasValue)
        {
            query = query.Where(m => m.MovementType == filter.MovementType.Value);
        }

        // Local dates are converted to UTC because every timestamp is stored in UTC.
        if (filter.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(m => m.CreatedAtUtc >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(filter.ToDate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(m => m.CreatedAtUtc < toUtc);
        }

        var total = await query.CountAsync(cancellationToken);

        // SQL Server cannot translate a computed property, so the projection is inlined here.
        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                ProductId = m.ProductId,
                ProductName = m.Product != null ? m.Product.Name : string.Empty,
                Barcode = m.Product != null ? m.Product.Barcode : null,
                MovementType = m.MovementType,
                Quantity = m.Quantity,
                BalanceAfter = m.BalanceAfter,
                UnitCost = m.UnitCost,
                ReferenceNumber = m.ReferenceNumber,
                Reason = m.Reason,
                PerformedBy = m.PerformedBy,
                CreatedAtUtc = m.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>(items, total, pageNumber, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockMovementDto>> GetProductMovementsAsync(int productId, int take = 200, CancellationToken cancellationToken = default)
    {
        return await _db.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(Math.Clamp(take, 1, 1000))
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                ProductId = m.ProductId,
                ProductName = m.Product != null ? m.Product.Name : string.Empty,
                Barcode = m.Product != null ? m.Product.Barcode : null,
                MovementType = m.MovementType,
                Quantity = m.Quantity,
                BalanceAfter = m.BalanceAfter,
                UnitCost = m.UnitCost,
                ReferenceNumber = m.ReferenceNumber,
                Reason = m.Reason,
                PerformedBy = m.PerformedBy,
                CreatedAtUtc = m.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> AdjustAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProductId <= 0)
        {
            throw new ArgumentException("يجب اختيار منتج صحيح");
        }

        if (request.Quantity <= 0m)
        {
            throw new ArgumentException("الكمية يجب أن تكون أكبر من صفر");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("سبب التسوية مطلوب لتوثيق الحركة");
        }

        // Guard rails: the movement type must agree with the direction the user picked,
        // otherwise the ledger would report a "sale" that actually increases the stock.
        var isInboundType = request.MovementType is StockMovementType.AdjustmentIn
            or StockMovementType.Purchase
            or StockMovementType.SaleReturn
            or StockMovementType.StockCount;

        if (request.Direction == StockAdjustmentDirection.Increase && !isInboundType)
        {
            throw new ArgumentException("نوع الحركة المختار لا يتوافق مع التسوية بالزيادة");
        }

        if (request.Direction == StockAdjustmentDirection.Decrease && isInboundType
            && request.MovementType != StockMovementType.StockCount)
        {
            throw new ArgumentException("نوع الحركة المختار لا يتوافق مع التسوية بالنقص");
        }

        var signedQuantity = request.Direction == StockAdjustmentDirection.Increase
            ? Math.Abs(request.Quantity)
            : -Math.Abs(request.Quantity);

        var newBalance = await _stockService.AdjustStockAsync(
            request.ProductId,
            signedQuantity,
            request.MovementType,
            referenceNumber: string.IsNullOrWhiteSpace(request.ReferenceNumber) ? "ADJ" : request.ReferenceNumber,
            reason: request.Reason.Trim(),
            performedBy: request.PerformedBy,
            cancellationToken: cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Manual stock adjustment on product {ProductId}: {Quantity} ({Type}) -> balance {Balance}",
            request.ProductId, signedQuantity, request.MovementType, newBalance);

        return newBalance;
    }
}
