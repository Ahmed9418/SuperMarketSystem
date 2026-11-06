using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Product catalog service. All database access is asynchronous and projection based so that
/// the WPF grids never materialize tracked entities.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly SupermarketDbContext _db;
    private readonly IStockService _stockService;
    private readonly IStoreSettingsService _storeSettings;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        SupermarketDbContext db,
        IStockService stockService,
        IStoreSettingsService storeSettings,
        ILogger<ProductService> logger)
    {
        _db = db;
        _stockService = stockService;
        _storeSettings = storeSettings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductFilter filter, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);

        var query = ApplyFilter(BuildProjection(_db.Products.AsNoTracking()), filter);

        var total = await query.CountAsync(cancellationToken);

        query = (filter.SortBy, filter.SortDescending) switch
        {
            (nameof(ProductDto.Barcode), false) => query.OrderBy(p => p.Barcode),
            (nameof(ProductDto.Barcode), true) => query.OrderByDescending(p => p.Barcode),
            (nameof(ProductDto.StockQuantity), false) => query.OrderBy(p => p.StockQuantity),
            (nameof(ProductDto.StockQuantity), true) => query.OrderByDescending(p => p.StockQuantity),
            (nameof(ProductDto.SellingPrice), false) => query.OrderBy(p => p.SellingPrice),
            (nameof(ProductDto.SellingPrice), true) => query.OrderByDescending(p => p.SellingPrice),
            (nameof(ProductDto.CreatedAtUtc), false) => query.OrderBy(p => p.CreatedAtUtc),
            (nameof(ProductDto.CreatedAtUtc), true) => query.OrderByDescending(p => p.CreatedAtUtc),
            (nameof(ProductDto.Name), true) => query.OrderByDescending(p => p.Name),
            _ => query.OrderBy(p => p.Name)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, total, pageNumber, pageSize);
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await BuildProjection(_db.Products.AsNoTracking())
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProductLookupResultDto> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var normalized = (barcode ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return ProductLookupResultDto.NotFound(string.Empty);
        }

        var product = await BuildProjection(_db.Products.AsNoTracking())
            .FirstOrDefaultAsync(p => p.Barcode == normalized || p.AlternativeBarcode == normalized, cancellationToken);

        if (product is null)
        {
            return ProductLookupResultDto.NotFound(normalized);
        }

        if (!product.IsActive)
        {
            return new ProductLookupResultDto
            {
                Found = false,
                Message = $"المنتج ({product.Name}) غير مفعّل للبيع",
                Product = product
            };
        }

        return new ProductLookupResultDto
        {
            Found = true,
            Message = $"{product.Name} - {product.EffectivePrice:N2} {DomainConstants.CurrencySymbol}",
            Product = product
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string term, int take = 40, CancellationToken cancellationToken = default)
    {
        var query = BuildProjection(_db.Products.AsNoTracking()).Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(p => p.Name.Contains(trimmed) || p.Barcode.Contains(trimmed));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductDto>> GetLowStockAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        return await BuildProjection(_db.Products.AsNoTracking())
            .Where(p => p.IsActive && p.StockQuantity <= p.MinimumStockLevel)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.Name)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(ProductCreateUpdateDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(dto, isUpdate: false, cancellationToken);

        var settings = await _storeSettings.GetAsync(cancellationToken);
        var barcode = string.IsNullOrWhiteSpace(dto.Barcode)
            ? (settings.AutoGenerateBarcode ? await GenerateBarcodeAsync(cancellationToken) : throw new InvalidOperationException("البار كود مطلوب"))
            : dto.Barcode.Trim();

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Barcode = barcode,
            AlternativeBarcode = string.IsNullOrWhiteSpace(dto.AlternativeBarcode) ? null : dto.AlternativeBarcode.Trim(),
            CategoryId = dto.CategoryId,
            SupplierId = dto.SupplierId,
            UnitId = dto.UnitId,
            CostPrice = dto.CostPrice,
            SellingPrice = dto.SellingPrice,
            DiscountPrice = dto.DiscountPrice,
            VatRate = dto.VatRate,
            MinimumStockLevel = dto.MinimumStockLevel,
            ReorderQuantity = dto.ReorderQuantity,
            StockQuantity = 0m,
            ShelfLocation = dto.ShelfLocation?.Trim(),
            IsWeighted = dto.IsWeighted,
            IsActive = dto.IsActive,
            ImagePath = dto.ImagePath,
            CreatedBy = dto.ChangedBy
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        // The opening quantity is written through the stock service so the ledger starts balanced.
        if (dto.InitialStock > 0m)
        {
            await _stockService.AdjustStockAsync(
                product.Id,
                dto.InitialStock,
                StockMovementType.Purchase,
                referenceNumber: "OPENING",
                reason: "رصيد افتتاحي",
                performedBy: dto.ChangedBy,
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Product {ProductId} ({Name}) created with barcode {Barcode}", product.Id, product.Name, product.Barcode);
        await _db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ProductCreateUpdateDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0)
        {
            throw new ArgumentException("A valid product id is required for an update.", nameof(dto));
        }

        await ValidateAsync(dto, isUpdate: true, cancellationToken);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == dto.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Product {dto.Id} was not found.");

        var priceChanged = product.CostPrice != dto.CostPrice || product.SellingPrice != dto.SellingPrice;
        if (priceChanged)
        {
            _db.PriceHistories.Add(new PriceHistory
            {
                ProductId = product.Id,
                OldCostPrice = product.CostPrice,
                NewCostPrice = dto.CostPrice,
                OldSellingPrice = product.SellingPrice,
                NewSellingPrice = dto.SellingPrice,
                Reason = "تعديل أسعار من شاشة المنتجات",
                ChangedBy = dto.ChangedBy
            });
        }

        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? product.Barcode : dto.Barcode.Trim();
        product.AlternativeBarcode = string.IsNullOrWhiteSpace(dto.AlternativeBarcode) ? null : dto.AlternativeBarcode.Trim();
        product.CategoryId = dto.CategoryId;
        product.SupplierId = dto.SupplierId;
        product.UnitId = dto.UnitId;
        product.CostPrice = dto.CostPrice;
        product.SellingPrice = dto.SellingPrice;
        product.DiscountPrice = dto.DiscountPrice;
        product.VatRate = dto.VatRate;
        product.MinimumStockLevel = dto.MinimumStockLevel;
        product.ReorderQuantity = dto.ReorderQuantity;
        product.ShelfLocation = dto.ShelfLocation?.Trim();
        product.IsWeighted = dto.IsWeighted;
        product.IsActive = dto.IsActive;
        product.ImagePath = dto.ImagePath;

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Product {ProductId} updated", product.Id);
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("المنتج المطلوب غير موجود أو تم حذفه بالفعل.");

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Product {ProductId} ({ProductName}) deleted", product.Id, product.Name);
    }

    /// <inheritdoc />
    public async Task SetActiveAsync(int id, bool isActive, string? userName = null, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Product {id} was not found.");

        product.IsActive = isActive;
        product.UpdatedBy = userName;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateBarcodeAsync(CancellationToken cancellationToken = default)
    {
        // Internal codes start with 20 to stay clear of the EAN-13 manufacturer ranges.
        var lastInternal = await _db.Products
            .IgnoreQueryFilters()
            .Where(p => p.Barcode.StartsWith(DomainConstants.LooseItemBarcodePrefix))
            .OrderByDescending(p => p.Barcode)
            .Select(p => p.Barcode)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrWhiteSpace(lastInternal) && lastInternal.Length == 13
            && int.TryParse(lastInternal[7..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{DomainConstants.LooseItemBarcodePrefix}{next:D6}";
    }

    /// <inheritdoc />
    public async Task<bool> IsBarcodeTakenAsync(string barcode, int? excludeProductId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return false;
        }

        var trimmed = barcode.Trim();
        return await _db.Products
            .IgnoreQueryFilters()
            .AnyAsync(p => (p.Barcode == trimmed || p.AlternativeBarcode == trimmed)
                           && (excludeProductId == null || p.Id != excludeProductId), cancellationToken);
    }

    // ------------------------------------------------------------- Helpers

    /// <summary>Projection reused by every read path so the DTO shape stays consistent.</summary>
    private static IQueryable<ProductDto> BuildProjection(IQueryable<Product> source) =>
        source.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Barcode = p.Barcode,
            AlternativeBarcode = p.AlternativeBarcode,
            CategoryId = p.CategoryId,
            CategoryName = p.Category != null ? p.Category.Name : string.Empty,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier != null ? p.Supplier.Name : null,
            UnitId = p.UnitId,
            UnitName = p.Unit != null ? p.Unit.Name : string.Empty,
            CostPrice = p.CostPrice,
            SellingPrice = p.SellingPrice,
            DiscountPrice = p.DiscountPrice,
            VatRate = p.VatRate,
            StockQuantity = p.StockQuantity,
            MinimumStockLevel = p.MinimumStockLevel,
            ReorderQuantity = p.ReorderQuantity,
            ShelfLocation = p.ShelfLocation,
            IsWeighted = p.IsWeighted,
            IsActive = p.IsActive,
            ImagePath = p.ImagePath,
            CreatedAtUtc = p.CreatedAtUtc
        });

    private static IQueryable<ProductDto> ApplyFilter(IQueryable<ProductDto> query, ProductFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Barcode.Contains(term));
        }

        if (filter.CategoryId is > 0)
        {
            query = query.Where(p => p.CategoryId == filter.CategoryId);
        }

        if (filter.SupplierId is > 0)
        {
            query = query.Where(p => p.SupplierId == filter.SupplierId);
        }

        if (filter.UnitId is > 0)
        {
            query = query.Where(p => p.UnitId == filter.UnitId);
        }

        if (filter.LowStockOnly)
        {
            query = query.Where(p => p.StockQuantity <= p.MinimumStockLevel);
        }

        if (filter.OutOfStockOnly)
        {
            query = query.Where(p => p.StockQuantity <= 0m);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == filter.IsActive.Value);
        }

        return query;
    }

    /// <summary>Business validation that must run before touching the database.</summary>
    private async Task ValidateAsync(ProductCreateUpdateDto dto, bool isUpdate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ArgumentException("اسم المنتج مطلوب");
        }

        if (dto.Name.Trim().Length > DomainConstants.ProductNameMaxLength)
        {
            throw new ArgumentException($"اسم المنتج لا يمكن أن يتجاوز {DomainConstants.ProductNameMaxLength} حرف");
        }

        if (dto.SellingPrice <= 0m)
        {
            throw new ArgumentException("سعر البيع يجب أن يكون أكبر من صفر");
        }

        if (dto.CostPrice < 0m)
        {
            throw new ArgumentException("سعر التكلفة لا يمكن أن يكون سالباً");
        }

        if (dto.DiscountPrice is < 0m)
        {
            throw new ArgumentException("سعر الخصم لا يمكن أن يكون سالباً");
        }

        if (dto.CategoryId <= 0 || !await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId, cancellationToken))
        {
            throw new ArgumentException("يجب اختيار قسم صحيح");
        }

        if (dto.UnitId <= 0 || !await _db.Units.AnyAsync(u => u.Id == dto.UnitId, cancellationToken))
        {
            throw new ArgumentException("يجب اختيار وحدة قياس صحيحة");
        }

        if (!string.IsNullOrWhiteSpace(dto.Barcode)
            && await IsBarcodeTakenAsync(dto.Barcode, isUpdate ? dto.Id : null, cancellationToken))
        {
            throw new ArgumentException($"البار كود ({dto.Barcode}) مستخدم بالفعل لمنتج آخر");
        }
    }
}
