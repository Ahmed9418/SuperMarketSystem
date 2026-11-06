using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Common;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>Supplier master data operations used by the admin application (Phase 2 and 5).</summary>
public sealed class SupplierService : ISupplierService
{
    private readonly SupermarketDbContext _db;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(SupermarketDbContext db, ILogger<SupplierService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<SupplierDto>> GetPagedAsync(string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(s => s.Name.Contains(term)
                                     || (s.ContactPerson != null && s.ContactPerson.Contains(term))
                                     || (s.PhoneNumber != null && s.PhoneNumber.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                ContactPerson = s.ContactPerson,
                PhoneNumber = s.PhoneNumber,
                Email = s.Email,
                Address = s.Address,
                TaxNumber = s.TaxNumber,
                Balance = s.Balance,
                Notes = s.Notes,
                IsActive = s.IsActive,
                ProductCount = s.Products.Count(p => !p.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierDto>(items, total, pageNumber, pageSize);
    }

    public async Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                ContactPerson = s.ContactPerson,
                PhoneNumber = s.PhoneNumber,
                Email = s.Email,
                Address = s.Address,
                TaxNumber = s.TaxNumber,
                Balance = s.Balance,
                Notes = s.Notes,
                IsActive = s.IsActive,
                ProductCount = s.Products.Count(p => !p.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CreateAsync(SupplierDto dto, CancellationToken cancellationToken = default)
    {
        Validate(dto);

        var supplier = new Supplier
        {
            Name = dto.Name.Trim(),
            ContactPerson = dto.ContactPerson?.Trim(),
            PhoneNumber = dto.PhoneNumber?.Trim(),
            Email = dto.Email?.Trim(),
            Address = dto.Address?.Trim(),
            TaxNumber = dto.TaxNumber?.Trim(),
            Balance = dto.Balance,
            Notes = dto.Notes?.Trim(),
            IsActive = dto.IsActive
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Supplier {SupplierId} ({Name}) created", supplier.Id, supplier.Name);
        return supplier.Id;
    }

    public async Task UpdateAsync(SupplierDto dto, CancellationToken cancellationToken = default)
    {
        Validate(dto);

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier {dto.Id} was not found.");

        supplier.Name = dto.Name.Trim();
        supplier.ContactPerson = dto.ContactPerson?.Trim();
        supplier.PhoneNumber = dto.PhoneNumber?.Trim();
        supplier.Email = dto.Email?.Trim();
        supplier.Address = dto.Address?.Trim();
        supplier.TaxNumber = dto.TaxNumber?.Trim();
        supplier.Balance = dto.Balance;
        supplier.Notes = dto.Notes?.Trim();
        supplier.IsActive = dto.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier {id} was not found.");

        supplier.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("المورد المطلوب غير موجود أو تم حذفه بالفعل.");

        var orderCount = await _db.PurchaseOrders.CountAsync(o => o.SupplierId == id, cancellationToken);
        if (orderCount > 0)
        {
            throw new InvalidOperationException(
                $"لا يمكن حذف المورد لوجود {orderCount} أمر توريد مسجل باسمه. أوقفه بدلاً من حذفه للحفاظ على السجلات.");
        }

        // Products survive the delete: only the optional supplier link is cleared.
        var linkedProducts = await _db.Products
            .Where(p => p.SupplierId == id)
            .ToListAsync(cancellationToken);

        foreach (var product in linkedProducts)
        {
            product.SupplierId = null;
        }

        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Supplier {SupplierId} ({Name}) deleted; {Count} products unlinked",
            supplier.Id, supplier.Name, linkedProducts.Count);
    }

    private static void Validate(SupplierDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ArgumentException("اسم المورد مطلوب");
        }

        if (dto.Name.Trim().Length > 200)
        {
            throw new ArgumentException("اسم المورد طويل جداً");
        }
    }
}
