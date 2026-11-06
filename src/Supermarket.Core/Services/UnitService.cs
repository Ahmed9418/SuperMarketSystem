using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>Measurement unit master data (قطعة، كيلوجرام، لتر …).</summary>
public sealed class UnitService : IUnitService
{
    private readonly SupermarketDbContext _db;
    private readonly ILogger<UnitService> _logger;

    public UnitService(SupermarketDbContext db, ILogger<UnitService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UnitDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Units
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto
            {
                Id = u.Id,
                Name = u.Name,
                Symbol = u.Symbol,
                AllowFractions = u.AllowFractions,
                ProductCount = u.Products.Count(p => !p.IsDeleted)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UnitDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Units
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UnitDto
            {
                Id = u.Id,
                Name = u.Name,
                Symbol = u.Symbol,
                AllowFractions = u.AllowFractions,
                ProductCount = u.Products.Count(p => !p.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CreateAsync(UnitDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(dto, isUpdate: false, cancellationToken);

        var unit = new Unit
        {
            Name = dto.Name.Trim(),
            Symbol = string.IsNullOrWhiteSpace(dto.Symbol) ? dto.Name.Trim() : dto.Symbol.Trim(),
            AllowFractions = dto.AllowFractions
        };

        _db.Units.Add(unit);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unit {UnitId} ({Name}) created", unit.Id, unit.Name);
        return unit.Id;
    }

    public async Task UpdateAsync(UnitDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0)
        {
            throw new ArgumentException("رقم الوحدة غير صحيح");
        }

        await ValidateAsync(dto, isUpdate: true, cancellationToken);

        var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == dto.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Unit {dto.Id} was not found.");

        unit.Name = dto.Name.Trim();
        unit.Symbol = string.IsNullOrWhiteSpace(dto.Symbol) ? dto.Name.Trim() : dto.Symbol.Trim();
        unit.AllowFractions = dto.AllowFractions;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("وحدة القياس المطلوبة غير موجودة أو تم حذفها بالفعل.");

        var productCount = await _db.Products.CountAsync(p => p.UnitId == id, cancellationToken);
        if (productCount > 0)
        {
            throw new InvalidOperationException(
                $"لا يمكن حذف الوحدة لأن {productCount} منتج يستخدمها. غيّر وحدة المنتجات أولاً.");
        }

        _db.Units.Remove(unit);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unit {UnitId} ({Name}) deleted", unit.Id, unit.Name);
    }

    private async Task ValidateAsync(UnitDto dto, bool isUpdate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ArgumentException("اسم الوحدة مطلوب");
        }

        if (dto.Name.Trim().Length > 50)
        {
            throw new ArgumentException("اسم الوحدة لا يمكن أن يتجاوز 50 حرف");
        }

        var duplicate = await _db.Units
            .AnyAsync(u => u.Name == dto.Name.Trim() && (!isUpdate || u.Id != dto.Id), cancellationToken);

        if (duplicate)
        {
            throw new ArgumentException($"توجد وحدة بنفس الاسم ({dto.Name.Trim()}) بالفعل");
        }
    }
}
