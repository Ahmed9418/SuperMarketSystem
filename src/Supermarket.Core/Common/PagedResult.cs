using System.Linq.Expressions;

namespace Supermarket.Core.Common;

/// <summary>
/// Paged result wrapper shared by every grid query in both applications.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class PagedResult<T>
{
    public PagedResult()
    {
    }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>Items of the requested page.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Total number of rows that matched the filter (all pages).</summary>
    public int TotalCount { get; init; }

    /// <summary>1-based page index.</summary>
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;

    public static PagedResult<T> Empty(int pageSize = 50) => new(Array.Empty<T>(), 0, 1, pageSize);
}

/// <summary>
/// Query options accepted by repository level read operations.
/// </summary>
public sealed class QueryOptions<T>
{
    public Expression<Func<T, bool>>? Predicate { get; set; }

    public Func<IQueryable<T>, IOrderedQueryable<T>>? OrderBy { get; set; }

    public int? Skip { get; set; }

    public int? Take { get; set; }

    public bool AsNoTracking { get; set; } = true;
}
