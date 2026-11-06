using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>Category master data used by the admin catalog screen and by the product form.</summary>
public sealed class CategoryService : ICategoryService
{
    /// <summary>Category that receives the products of a deleted category.</summary>
    private const string FallbackCategoryName = "عام";

    private readonly SupermarketDbContext _db;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(SupermarketDbContext db, ILogger<CategoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count(p => !p.IsDeleted)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count(p => !p.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CreateAsync(CategoryDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(dto, isUpdate: false, cancellationToken);

        var category = new Category
        {
            Name = dto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            ParentCategoryId = dto.ParentCategoryId,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Category {CategoryId} ({Name}) created", category.Id, category.Name);
        return category.Id;
    }

    public async Task UpdateAsync(CategoryDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0)
        {
            throw new ArgumentException("رقم القسم غير صحيح");
        }

        await ValidateAsync(dto, isUpdate: true, cancellationToken);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == dto.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Category {dto.Id} was not found.");

        category.Name = dto.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        category.ParentCategoryId = dto.ParentCategoryId;
        category.DisplayOrder = dto.DisplayOrder;
        category.IsActive = dto.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Category {id} was not found.");

        // Deactivating a category hides all of its products from the POS, so the operation is
        // refused while products still reference it.
        if (!isActive)
        {
            var activeProducts = await _db.Products.CountAsync(p => p.CategoryId == id && p.IsActive, cancellationToken);
            if (activeProducts > 0)
            {
                throw new InvalidOperationException(
                    $"لا يمكن إيقاف القسم لوجود {activeProducts} منتج مُفعّل بداخله. أوقف المنتجات أولاً أو انقلها لقسم آخر.");
            }
        }

        category.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("القسم المطلوب غير موجود أو تم حذفه بالفعل.");

        if (await _db.Categories.AnyAsync(c => c.ParentCategoryId == id, cancellationToken))
        {
            throw new InvalidOperationException("لا يمكن حذف القسم لأنه القسم الرئيسي لأقسام أخرى. انقل الأقسام الفرعية أولاً.");
        }

        // Products must keep a valid category, so they are moved to the fallback ("عام") category
        // instead of being orphaned. The fallback is created on demand.
        var affected = await _db.Products.Where(p => p.CategoryId == id).ToListAsync(cancellationToken);
        if (affected.Count > 0)
        {
            var fallback = await GetOrCreateFallbackCategoryAsync(cancellationToken);
            foreach (var product in affected)
            {
                product.CategoryId = fallback.Id;
            }

            _logger.LogWarning(
                "Category {CategoryId} ({Name}) deleted; {Count} products moved to category {FallbackId}",
                category.Id, category.Name, affected.Count, fallback.Id);
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Category {CategoryId} ({Name}) deleted", category.Id, category.Name);
    }

    private async Task<Category> GetOrCreateFallbackCategoryAsync(CancellationToken cancellationToken)
    {
        var fallback = await _db.Categories
            .FirstOrDefaultAsync(c => c.Name == FallbackCategoryName, cancellationToken);

        if (fallback is not null)
        {
            return fallback;
        }

        fallback = new Category
        {
            Name = FallbackCategoryName,
            Description = "قسم افتراضي تُنقل إليه منتجات الأقسام المحذوفة",
            DisplayOrder = 999,
            IsActive = true
        };

        _db.Categories.Add(fallback);
        await _db.SaveChangesAsync(cancellationToken);
        return fallback;
    }

    private async Task ValidateAsync(CategoryDto dto, bool isUpdate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ArgumentException("اسم القسم مطلوب");
        }

        if (dto.Name.Trim().Length > 120)
        {
            throw new ArgumentException("اسم القسم لا يمكن أن يتجاوز 120 حرف");
        }

        var duplicate = await _db.Categories
            .AnyAsync(c => c.Name == dto.Name.Trim() && (!isUpdate || c.Id != dto.Id), cancellationToken);

        if (duplicate)
        {
            throw new ArgumentException($"يوجد قسم بنفس الاسم ({dto.Name.Trim()}) بالفعل");
        }

        if (isUpdate && dto.ParentCategoryId == dto.Id)
        {
            throw new ArgumentException("لا يمكن أن يكون القسم تابعاً لنفسه");
        }

        if (dto.ParentCategoryId is > 0
            && !await _db.Categories.AnyAsync(c => c.Id == dto.ParentCategoryId, cancellationToken))
        {
            throw new ArgumentException("القسم الرئيسي المختار غير موجود");
        }
    }
}
