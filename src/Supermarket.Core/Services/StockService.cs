using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// The only component allowed to change <see cref="Product.StockQuantity"/>.
/// Every change is mirrored into the <see cref="StockMovement"/> ledger inside the same
/// transaction, so the ledger sum always equals the cached quantity on the product row.
/// </summary>
public sealed class StockService : IStockService
{
    private readonly SupermarketDbContext _db;
    private readonly ILogger<StockService> _logger;

    public StockService(SupermarketDbContext db, ILogger<StockService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<decimal> AdjustStockAsync(
        int productId,
        decimal quantityDelta,
        StockMovementType movementType,
        string? referenceNumber = null,
        string? reason = null,
        string? performedBy = null,
        int? invoiceId = null,
        int? purchaseOrderId = null,
        int? shiftId = null,
        CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {productId} was not found.");

        var newBalance = product.StockQuantity + quantityDelta;
        if (newBalance < 0m)
        {
            throw new InvalidOperationException(
                $"Insufficient stock for product '{product.Name}'. On hand: {product.StockQuantity}, requested: {quantityDelta}.");
        }

        product.StockQuantity = newBalance;

        var movement = new StockMovement
        {
            ProductId = productId,
            MovementType = movementType,
            Quantity = quantityDelta,
            BalanceAfter = newBalance,
            UnitCost = product.CostPrice,
            ReferenceNumber = referenceNumber,
            Reason = reason,
            PerformedBy = performedBy,
            InvoiceId = invoiceId,
            PurchaseOrderId = purchaseOrderId,
            ShiftId = shiftId
        };

        _db.StockMovements.Add(movement);

        _logger.LogDebug("Stock adjusted for product {ProductId}: {Delta} -> {Balance} ({Type})",
            productId, quantityDelta, newBalance, movementType);

        return newBalance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockMovement>> GetMovementsAsync(int productId, int take = 100, CancellationToken cancellationToken = default)
    {
        return await _db.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
