using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Paginação e ordenação comuns às listagens (?page=1&amp;pageSize=20&amp;sortBy=name&amp;sortDir=desc).</summary>
public class PageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public string? Search { get; set; }

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 or > 500 ? 20 : PageSize;
    public bool Descending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.SafePage, page.SafePageSize, total);
    }

    /// <summary>Ordena por uma coluna da lista permitida; colunas desconhecidas usam a ordenação padrão.</summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, PageQuery page,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> columns, string defaultColumn, bool defaultDescending = false)
    {
        var key = page.SortBy is not null && columns.ContainsKey(page.SortBy) ? page.SortBy : defaultColumn;
        var desc = page.SortBy is not null && columns.ContainsKey(page.SortBy) ? page.Descending : defaultDescending;
        var expr = columns[key];
        return desc ? query.OrderByDescending(expr) : query.OrderBy(expr);
    }
}

/// <summary>Período inclusivo de datas usado nos filtros de relatórios e dashboard.</summary>
public record DateRange(DateOnly From, DateOnly To)
{
    public static DateRange Resolve(DateOnly? from, DateOnly? to, DateOnly today, int defaultDays = 30)
    {
        var end = to ?? today;
        var start = from ?? end.AddDays(-(defaultDays - 1));
        if (start > end) (start, end) = (end, start);
        return new DateRange(start, end);
    }

    public int Days => To.DayNumber - From.DayNumber + 1;
}
