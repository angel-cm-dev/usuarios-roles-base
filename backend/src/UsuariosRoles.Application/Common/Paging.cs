using Microsoft.EntityFrameworkCore;

namespace UsuariosRoles.Application.Common;

/// <summary>Parámetros comunes de listados: página, tamaño, búsqueda y orden.</summary>
public class PagedQuery
{
    public const int MaxPageSize = 100;

    // Nullable a propósito: en Minimal APIs un int no anulable en la query sería obligatorio.
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? Search { get; set; }

    public string? SortBy { get; set; }

    /// <summary>"asc" o "desc".</summary>
    public string? SortDir { get; set; }

    public int SafePage => Math.Max(1, Page ?? 1);

    // Limitar el tamaño evita que un cliente pida 1 millón de filas en una sola llamada.
    public int SafePageSize => Math.Clamp(PageSize ?? 10, 1, MaxPageSize);

    public bool Descending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);

    public string? SearchTerm => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        PagedQuery query,
        CancellationToken cancellationToken)
    {
        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<T>(items, query.SafePage, query.SafePageSize, total);
    }

    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> source,
        System.Linq.Expressions.Expression<Func<T, TKey>> key,
        bool descending) =>
        descending ? source.OrderByDescending(key) : source.OrderBy(key);
}
