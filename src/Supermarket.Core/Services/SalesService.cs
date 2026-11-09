using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Checkout engine of the POS.
///
/// <para>
/// <see cref="CompleteSaleAsync"/> is the only entry point that turns a cart into money, therefore it
/// re-reads every product from the database instead of trusting the prices the UI is showing (a
/// cashier terminal may have been left open for hours). The whole operation — invoice, lines,
/// payments, stock ledger rows and the credit customer balance — is written in one
/// database transaction.
/// </para>
/// </summary>
public sealed class SalesService : ISalesService
{
    private readonly SupermarketDbContext _db;
    private readonly IStockService _stockService;
    private readonly INumberSequenceService _numberSequence;
    private readonly IStoreSettingsService _storeSettings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SalesService> _logger;

    public SalesService(
        SupermarketDbContext db,
        IStockService stockService,
        INumberSequenceService numberSequence,
        IStoreSettingsService storeSettings,
        IDateTimeProvider clock,
        ILogger<SalesService> logger)
    {
        _db = db;
        _stockService = stockService;
        _numberSequence = numberSequence;
        _storeSettings = storeSettings;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CartPricingDto> PriceCartAsync(
        IReadOnlyList<CartLineDto> lines,
        decimal invoiceDiscount = 0m,
        decimal invoiceDiscountPercentage = 0m,
        CancellationToken cancellationToken = default)
    {
        var settings = await _storeSettings.GetAsync(cancellationToken);
        return CalculateTotals(lines, invoiceDiscount, invoiceDiscountPercentage, settings.PricesIncludeVat);
    }

    /// <inheritdoc />
    public async Task<SaleResultDto> CompleteSaleAsync(SaleRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
        {
            throw new InvalidOperationException("السلة فارغة، لا يمكن إتمام عملية البيع");
        }

        if (request.UserId <= 0)
        {
            throw new InvalidOperationException("يجب تسجيل دخول الكاشير قبل إتمام عملية البيع");
        }

        var settings = await _storeSettings.GetAsync(cancellationToken);

        // ---------------------------------------------------------------- Pricing
        var pricedLines = await RepriceLinesAsync(request.Lines, cancellationToken);
        var totals = CalculateTotals(pricedLines, request.InvoiceDiscount, request.InvoiceDiscountPercentage, settings.PricesIncludeVat);

        // Credit sales must be attached to a real customer, everything else may be anonymous.
        if (request.PaymentMethod == PaymentMethod.Credit && request.CustomerId is null)
        {
            throw new InvalidOperationException("البيع الآجل يتطلب اختيار عميل مسجل");
        }

        if (request.PaymentMethod != PaymentMethod.Credit && request.PaidAmount < totals.TotalAmount)
        {
            throw new InvalidOperationException(
                $"المبلغ المدفوع ({request.PaidAmount:N2}) أقل من الإجمالي المطلوب ({totals.TotalAmount:N2})");
        }

        var cashier = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("لم يتم العثور على بيانات الكاشير");

        var changeAmount = request.PaymentMethod == PaymentMethod.Credit
            ? 0m
            : Math.Max(0m, Math.Round(request.PaidAmount - totals.TotalAmount, 2));

        var remainingAmount = request.PaymentMethod == PaymentMethod.Credit
            ? totals.TotalAmount
            : 0m;

        // ------------------------------------------------------------ Transaction
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var shift = await ResolveShiftAsync(request, cancellationToken);

                // A completed invoice must always belong to an open shift, otherwise the drawer
                // reconciliation of the Z-report would silently miss the transaction.
                if (shift is null)
                {
                    throw new InvalidOperationException(
                        "لا توجد ورديّة مفتوحة لهذا الكاشير. يجب فتح ورديّة قبل إتمام أي عملية بيع.");
                }

                var invoiceNumber = await _numberSequence.NextInvoiceNumberAsync(cancellationToken);

                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    InvoiceDateUtc = _clock.UtcNow,
                    CustomerId = request.CustomerId,
                    UserId = request.UserId,
                    ShiftId = shift.Id,
                    Status = InvoiceStatus.Completed,
                    SubTotal = totals.SubTotal,
                    DiscountAmount = totals.TotalDiscount,
                    DiscountPercentage = totals.InvoiceDiscountPercentage,
                    TaxAmount = totals.TaxAmount,
                    TotalAmount = totals.TotalAmount,
                    PaidAmount = request.PaymentMethod == PaymentMethod.Credit ? 0m : request.PaidAmount,
                    ChangeAmount = changeAmount,
                    RemainingAmount = remainingAmount,
                    PaymentMethod = request.PaymentMethod,
                    Notes = request.Notes,
                    IsWalkIn = request.CustomerId is null,
                    CreatedBy = cashier.UserName
                };

                foreach (var line in pricedLines)
                {
                    invoice.Items.Add(new InvoiceItem
                    {
                        ProductId = line.ProductId,
                        ProductName = line.ProductName,
                        Barcode = line.Barcode,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        UnitCost = line.UnitCost,
                        DiscountAmount = line.DiscountAmount,
                        VatRate = line.VatRate,
                        TaxAmount = line.TaxAmount,
                        LineTotal = line.LineTotal,
                        LineTotalWithTax = line.LineTotalWithTax
                    });
                }

                var settledAmount = request.PaymentMethod == PaymentMethod.Credit ? 0m : Math.Min(request.PaidAmount, totals.TotalAmount);
                if (settledAmount > 0m)
                {
                    invoice.Payments.Add(new Payment
                    {
                        Method = request.PaymentMethod,
                        Amount = settledAmount,
                        PaidAtUtc = _clock.UtcNow
                    });
                }

                _db.Invoices.Add(invoice);
                await _db.SaveChangesAsync(cancellationToken);

                // Stock is deducted through the ledger so the movement history stays complete.
                foreach (var line in pricedLines)
                {
                    await _stockService.AdjustStockAsync(
                        line.ProductId,
                        -Math.Abs(line.Quantity),
                        StockMovementType.Sale,
                        referenceNumber: invoiceNumber,
                        reason: "بيع من نقطة البيع",
                        performedBy: cashier.UserName,
                        invoiceId: invoice.Id,
                        shiftId: shift.Id,
                        cancellationToken: cancellationToken);
                }

                if (request.PaymentMethod == PaymentMethod.Credit && request.CustomerId is int customerId)
                {
                    var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
                    if (customer is not null)
                    {
                        customer.Balance += remainingAmount;
                    }
                }

                if (shift is not null)
                {
                    shift.TotalSales += totals.TotalAmount;
                    shift.TotalDiscount += totals.TotalDiscount;
                    shift.TotalTax += totals.TaxAmount;
                    shift.InvoiceCount += 1;

                    switch (request.PaymentMethod)
                    {
                        case PaymentMethod.Cash:
                            shift.TotalCashSales += totals.TotalAmount;
                            break;
                        case PaymentMethod.Card:
                            shift.TotalCardSales += totals.TotalAmount;
                            break;
                        case PaymentMethod.Wallet:
                            shift.TotalWalletSales += totals.TotalAmount;
                            break;
                        case PaymentMethod.Credit:
                            shift.TotalCreditSales += totals.TotalAmount;
                            break;
                    }

                    // Expected cash = opening float + cash taken during the shift.
                    shift.ExpectedCash = shift.OpeningCash + shift.TotalCashSales - shift.TotalRefunds;
                }

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Sale {InvoiceNumber} committed: {Total} EGP, {LineCount} lines, payment {Method}, cashier {Cashier}",
                    invoiceNumber, totals.TotalAmount, pricedLines.Count, request.PaymentMethod, cashier.UserName);

                return new SaleResultDto
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoiceNumber,
                    InvoiceDateUtc = invoice.InvoiceDateUtc,
                    SubTotal = totals.SubTotal,
                    DiscountAmount = totals.TotalDiscount,
                    TaxAmount = totals.TaxAmount,
                    TotalAmount = totals.TotalAmount,
                    PaidAmount = invoice.PaidAmount,
                    ChangeAmount = changeAmount,
                    RemainingAmount = remainingAmount,
                    PaymentMethod = request.PaymentMethod,
                    Lines = pricedLines,
                    CashierName = cashier.FullName,
                    CustomerName = request.CustomerName,
                    ShiftNumber = shift?.ShiftNumber
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<SaleResultDto?> GetInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        return await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new SaleResultDto
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDateUtc = i.InvoiceDateUtc,
                SubTotal = i.SubTotal,
                DiscountAmount = i.DiscountAmount,
                TaxAmount = i.TaxAmount,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                ChangeAmount = i.ChangeAmount,
                RemainingAmount = i.RemainingAmount,
                PaymentMethod = i.PaymentMethod,
                CashierName = i.User != null ? i.User.FullName : string.Empty,
                CustomerName = i.Customer != null ? i.Customer.Name : null,
                ShiftNumber = i.Shift != null ? i.Shift.ShiftNumber : null,
                Lines = i.Items.Select(item => new CartLineDto
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Barcode = item.Barcode ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    UnitCost = item.UnitCost,
                    DiscountAmount = item.DiscountAmount,
                    VatRate = item.VatRate
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ Helpers

    /// <summary>
    /// Re-reads every scanned product and rebuilds the cart lines from the authoritative master data.
    /// Rejects unknown / inactive products, merges duplicate scans and validates the stock.
    /// </summary>
    private async Task<List<CartLineDto>> RepriceLinesAsync(IReadOnlyList<CartLineDto> requestedLines, CancellationToken cancellationToken)
    {
        var productIds = requestedLines.Select(l => l.ProductId).Distinct().ToList();

        IQueryable<Product> productQuery = _db.Products.AsNoTracking();
        if (productIds.Count == 1)
        {
            var singleId = productIds[0];
            productQuery = productQuery.Where(p => p.Id == singleId);
        }
        else
        {
            productQuery = productQuery.Where(p => productIds.Contains(p.Id));
        }

        var products = await productQuery
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
                p.IsActive,
                p.IsWeighted
            })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var result = new List<CartLineDto>(requestedLines.Count);

        foreach (var requested in requestedLines)
        {
            if (requested.Quantity <= 0m)
            {
                throw new InvalidOperationException($"الكمية غير صحيحة للمنتج ({requested.ProductName})");
            }

            if (!products.TryGetValue(requested.ProductId, out var product))
            {
                throw new InvalidOperationException($"المنتج ({requested.ProductName}) لم يعد موجوداً في النظام");
            }

            if (!product.IsActive)
            {
                throw new InvalidOperationException($"المنتج ({product.Name}) موقوف ولا يمكن بيعه");
            }

            if (!product.IsWeighted && requested.Quantity != Math.Truncate(requested.Quantity))
            {
                throw new InvalidOperationException($"المنتج ({product.Name}) لا يُباع بكسور، يجب إدخال كمية صحيحة");
            }

            if (requested.Quantity > product.StockQuantity)
            {
                throw new InvalidOperationException(
                    $"الكمية المتوفرة من ({product.Name}) هي {product.StockQuantity:N2} فقط");
            }

            var unitPrice = product.DiscountPrice is > 0m && product.DiscountPrice < product.SellingPrice
                ? product.DiscountPrice!.Value
                : product.SellingPrice;

            var existing = result.FirstOrDefault(l => l.ProductId == product.Id);
            if (existing is not null)
            {
                // The same barcode scanned twice becomes one line with an accumulated quantity.
                existing.Quantity += requested.Quantity;
                if (existing.Quantity > product.StockQuantity)
                {
                    throw new InvalidOperationException(
                        $"الكمية المتوفرة من ({product.Name}) هي {product.StockQuantity:N2} فقط");
                }

                continue;
            }

            result.Add(new CartLineDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Barcode = product.Barcode,
                UnitPrice = unitPrice,
                UnitCost = product.CostPrice,
                Quantity = requested.Quantity,
                DiscountAmount = requested.DiscountAmount,
                VatRate = product.VatRate
            });
        }

        return result;
    }

    /// <summary>
    /// Shared money math used by the POS totals panel and by the committed invoice.
    ///
    /// <para>
    /// When <c>PricesIncludeVat</c> is true (the Egyptian retail default) every unit price already
    /// contains the VAT, therefore the tax is extracted out of the payable total instead of being
    /// added on top of it. Otherwise the tax is added.
    /// </para>
    /// </summary>
    private static CartPricingDto CalculateTotals(
        IReadOnlyList<CartLineDto> lines,
        decimal requestedInvoiceDiscount,
        decimal requestedInvoiceDiscountPercentage,
        bool pricesIncludeVat)
    {
        var subTotal = lines.Sum(l => l.LineTotal); // line totals already net of line discounts
        var lineDiscountTotal = lines.Sum(l => l.DiscountAmount);

        var invoiceDiscount = requestedInvoiceDiscount;
        var percentage = requestedInvoiceDiscountPercentage;

        if (invoiceDiscount <= 0m && percentage > 0m)
        {
            invoiceDiscount = Math.Round(subTotal * Math.Min(percentage, 100m) / 100m, 2);
        }
        else if (invoiceDiscount > 0m && subTotal > 0m)
        {
            percentage = Math.Round(invoiceDiscount / subTotal * 100m, 2);
        }

        invoiceDiscount = Math.Min(Math.Max(invoiceDiscount, 0m), subTotal);

        var taxAmount = lines.Sum(l => l.TaxAmount);
        if (invoiceDiscount > 0m && subTotal > 0m)
        {
            // An invoice level discount also reduces the VAT proportionally.
            taxAmount = Math.Round(taxAmount * (1m - invoiceDiscount / subTotal), 2);
        }

        var totalAmount = pricesIncludeVat
            ? Math.Round(subTotal - invoiceDiscount, 2)
            : Math.Round(subTotal - invoiceDiscount + taxAmount, 2);

        return new CartPricingDto
        {
            SubTotal = Math.Round(subTotal, 2),
            LineDiscountTotal = Math.Round(lineDiscountTotal, 2),
            InvoiceDiscount = Math.Round(invoiceDiscount, 2),
            InvoiceDiscountPercentage = percentage,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            TotalQuantity = lines.Sum(l => l.Quantity),
            LineCount = lines.Count
        };
    }

    /// <summary>
    /// Uses the shift supplied by the POS; if none was supplied it silently attaches the sale to the
    /// cashier's open shift so the Z-report never misses a transaction.
    /// </summary>
    private async Task<Shift?> ResolveShiftAsync(SaleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.ShiftId is > 0)
        {
            return await _db.Shifts.FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);
        }

        return await _db.Shifts
            .Where(s => s.UserId == request.UserId && s.Status == ShiftStatus.Open)
            .OrderByDescending(s => s.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
