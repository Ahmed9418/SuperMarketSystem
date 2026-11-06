using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Park and resume baskets on the POS.
///
/// <para>
/// The basket is stored as JSON and the product names / prices are refreshed from the database when
/// it is resumed: a parked basket never freezes yesterday's price. Resuming deletes the row (a held
/// cart can be resumed only once), which keeps the list clean without a "state" column.
/// </para>
/// </summary>
public sealed class HeldCartService : IHeldCartService
{
    private readonly SupermarketDbContext _context;
    private readonly ILogger<HeldCartService> _logger;

    public HeldCartService(SupermarketDbContext context, ILogger<HeldCartService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> HoldAsync(HoldCartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("لا يمكن تعليق سلة فارغة", nameof(request));
        }

        var payload = new HeldCartPayload
        {
            InvoiceDiscount = request.InvoiceDiscount,
            InvoiceDiscountPercentage = request.InvoiceDiscountPercentage,
            Lines = request.Lines.Select(l => new HeldCartLineDto
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                DiscountAmount = l.DiscountAmount
            }).ToList()
        };

        var held = new HeldCart
        {
            Label = string.IsNullOrWhiteSpace(request.Label)
                ? $"سلة معلقة - {DateTime.Now:HH:mm}"
                : request.Label.Trim(),
            UserId = request.UserId,
            ShiftId = request.ShiftId,
            TerminalId = string.IsNullOrWhiteSpace(request.TerminalId) ? "POS-01" : request.TerminalId,
            CartJson = payload.Serialize(),
            LineCount = payload.Lines.Count,
            TotalQuantity = payload.Lines.Sum(l => l.Quantity),
            EstimatedTotal = request.EstimatedTotal
        };

        _context.HeldCarts.Add(held);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cart #{HeldCartId} held by user {UserId} with {Lines} lines",
            held.Id, request.UserId, held.LineCount);

        return held.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HeldCartDto>> GetHeldAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.HeldCarts
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<HeldCartDto?> ResumeAsync(int heldCartId, CancellationToken cancellationToken = default)
    {
        var held = await _context.HeldCarts.FirstOrDefaultAsync(h => h.Id == heldCartId, cancellationToken);
        if (held is null)
        {
            return null;
        }

        var dto = Map(held);

        // Re-read names, prices, units and stock so the resumed basket is priced today.
        var productIds = dto.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Barcode,
                p.SellingPrice,
                p.DiscountPrice,
                p.CostPrice,
                p.VatRate,
                p.StockQuantity,
                p.IsWeighted,
                p.UnitId,
                UnitName = p.Unit != null ? p.Unit.Name : string.Empty
            })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var line in dto.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                continue;
            }

            line.ProductName = product.Name;
            line.Barcode = product.Barcode;
            line.UnitName = product.UnitName;
            line.UnitPrice = product.DiscountPrice is > 0m && product.DiscountPrice < product.SellingPrice
                ? product.DiscountPrice!.Value
                : product.SellingPrice;
            line.CostPrice = product.CostPrice;
            line.VatRate = product.VatRate;
            line.StockOnHand = product.StockQuantity;
            line.IsWeighted = product.IsWeighted;
            line.IsAvailable = product.StockQuantity >= line.Quantity;
        }

        _context.HeldCarts.Remove(held);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cart #{HeldCartId} resumed", heldCartId);

        return dto;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int heldCartId, CancellationToken cancellationToken = default)
    {
        var held = await _context.HeldCarts.FirstOrDefaultAsync(h => h.Id == heldCartId, cancellationToken);
        if (held is null)
        {
            return false;
        }

        _context.HeldCarts.Remove(held);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public Task<int> CountAsync(int userId, CancellationToken cancellationToken = default) =>
        _context.HeldCarts.AsNoTracking().CountAsync(h => h.UserId == userId, cancellationToken);

    private static HeldCartDto Map(HeldCart held)
    {
        var payload = HeldCartPayload.Deserialize(held.CartJson);

        return new HeldCartDto
        {
            Id = held.Id,
            Label = held.Label,
            UserId = held.UserId,
            ShiftId = held.ShiftId ?? 0,
            LineCount = held.LineCount,
            TotalQuantity = held.TotalQuantity,
            EstimatedTotal = held.EstimatedTotal,
            CreatedAtUtc = held.CreatedAtUtc,
            InvoiceDiscount = payload.InvoiceDiscount,
            InvoiceDiscountPercentage = payload.InvoiceDiscountPercentage,
            Lines = payload.Lines.Select(l => new HeldCartLineDto
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                DiscountAmount = l.DiscountAmount
            }).ToList()
        };
    }
}
