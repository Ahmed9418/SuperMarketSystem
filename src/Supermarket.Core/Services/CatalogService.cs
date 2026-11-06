using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supermarket.Core.Data;
using Supermarket.Core.DTOs;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Services;

/// <summary>
/// Provides the little lookup lists consumed by the combo boxes of the Admin and POS screens.
/// </summary>
public sealed class CatalogService : ICatalogService
{
    private readonly SupermarketDbContext _db;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(SupermarketDbContext db, ILogger<CatalogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking();
        if (onlyActive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new LookupDto { Id = c.Id, Name = c.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Units
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new LookupDto { Id = u.Id, Name = u.Name, Description = u.Symbol })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetSuppliersAsync(bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var query = _db.Suppliers.AsNoTracking();
        if (onlyActive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new LookupDto { Id = s.Id, Name = s.Name, Description = s.PhoneNumber })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new LookupDto { Id = c.Id, Name = c.Name, Description = c.PhoneNumber })
            .ToListAsync(cancellationToken);
    }
}
