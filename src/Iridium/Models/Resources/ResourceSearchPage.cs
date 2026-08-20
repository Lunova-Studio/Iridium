namespace Iridium.Models.Resources;


public sealed record ResourceSearchPage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
