namespace TradeFolio.Common.Pagination;

public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public static PageRequest Create(int? page, int? pageSize)
    {
        var safePage = page is null or < 1 ? 1 : page.Value;
        var safeSize = pageSize switch
        {
            null or < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize.Value
        };
        return new PageRequest(safePage, safeSize);
    }
}