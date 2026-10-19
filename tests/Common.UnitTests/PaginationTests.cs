using TradeFolio.Common.Pagination;

namespace Common.UnitTests;

public class PaginationTests
{
    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-5, 10, 1, 10)]
    [InlineData(2, 1000, 2, 100)]
    [InlineData(3, 0, 3, 20)]
    [InlineData(4, 50, 4, 50)]
    public void PageRequest_normalizes_input(int? page, int? size, int expectedPage, int expectedSize)
    {
        var request = PageRequest.Create(page, size);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedSize, request.PageSize);
    }

    [Fact]
    public void Skip_is_zero_for_the_first_page_and_grows_by_page_size()
    {
        Assert.Equal(0, PageRequest.Create(1, 25).Skip);
        Assert.Equal(50, PageRequest.Create(3, 25).Skip);
    }

    [Fact]
    public void PagedResult_calculates_total_pages_and_neighbours()
    {
        var page2 = new PagedResult<int>([11, 12], Page: 2, PageSize: 10, TotalCount: 25);

        Assert.Equal(3, page2.TotalPages);
        Assert.True(page2.HasNext);
        Assert.True(page2.HasPrevious);

        var page3 = page2 with { Page = 3 };
        Assert.False(page3.HasNext);
    }

    [Fact]
    public void PagedResult_with_no_items_has_zero_pages()
    {
        var empty = new PagedResult<int>([], Page: 1, PageSize: 20, TotalCount: 0);

        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasNext);
        Assert.False(empty.HasPrevious);
    }
}