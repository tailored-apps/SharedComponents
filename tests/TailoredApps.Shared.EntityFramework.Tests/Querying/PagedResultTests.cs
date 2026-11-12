using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Shared.EntityFramework.Querying;
using TailoredApps.Shared.Querying;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.Querying;

public class PagedResultTests
{
    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class ItemsContext : DbContext
    {
        public ItemsContext()
            : base(new DbContextOptionsBuilder<ItemsContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
        {
        }

        public DbSet<Item> Items => Set<Item>();
    }

    private sealed class Paging : IPagingParameters
    {
        public int? Page { get; set; }
        public int? Count { get; set; }
        public bool IsPagingSpecified => Page.HasValue && Count.HasValue;
    }

    private static IQueryable<Item> Items(int count)
        => Enumerable.Range(1, count).Select(i => new Item { Id = i, Name = "item-" + i }).AsQueryable();

    private static async Task<ItemsContext> SeededContext(int count)
    {
        var context = new ItemsContext();
        context.Items.AddRange(Items(count));
        await context.SaveChangesAsync();
        return context;
    }

    // ─── PagedResult ─────────────────────────────────────────────────────────

    [Fact]
    public void When_Built_From_A_List_Should_Count_Its_Items_Unless_A_Total_Is_Given()
    {
        var list = Items(3).ToList();

        var page = new PagedResult<Item>(list);
        var pageOfMany = new PagedResult<Item>(list, 250);

        Assert.Same(list, page.Results);
        Assert.Equal(3, page.Count);
        Assert.Equal(250, pageOfMany.Count);
    }

    [Fact]
    public void When_Built_From_Null_Should_Throw_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PagedResult<Item>((List<Item>)null));
        Assert.Throws<ArgumentNullException>(() => new PagedResult<Item>((PagingQuery<Item>)null));
    }

    [Fact]
    public void When_Empty_Is_Requested_Should_Return_A_New_Result_With_No_Items()
    {
        var empty = PagedResult<Item>.Empty;

        Assert.Empty(empty.Results);
        Assert.Equal(0, empty.Count);
        Assert.NotSame(empty, PagedResult<Item>.Empty);
    }

    [Fact]
    public async Task When_Built_From_A_List_Should_Refuse_To_Execute_A_Query()
    {
        var page = new PagedResult<Item>(Items(1).ToList());

        Assert.Throws<ArgumentNullException>(() => page.GetPagedResult());
        await Assert.ThrowsAsync<ArgumentNullException>(() => page.GetPagedResultAsync());
    }

    [Fact]
    public void When_A_Page_Is_Requested_Should_Return_Its_Items_And_The_Total_Count()
    {
        var page = Items(25).Paging(new Paging { Page = 2, Count = 10 });

        Assert.Equal(Enumerable.Range(11, 10), page.Results.Select(i => i.Id));
        Assert.Equal(25, page.Count);
    }

    [Fact]
    public void When_The_Last_Page_Is_Partial_Should_Return_The_Remaining_Items()
    {
        var page = Items(25).Paging(new Paging { Page = 3, Count = 10 });

        Assert.Equal(new[] { 21, 22, 23, 24, 25 }, page.Results.Select(i => i.Id));
        Assert.Equal(25, page.Count);
    }

    [Fact]
    public void When_The_Page_Is_Past_The_End_Should_Return_No_Items_But_Keep_The_Total()
    {
        var page = Items(25).Paging(new Paging { Page = 9, Count = 10 });

        Assert.Empty(page.Results);
        Assert.Equal(25, page.Count);
    }

    [Fact]
    public void When_Paging_Is_Not_Specified_Should_Return_Everything()
    {
        var page = Items(25).Paging(new Paging());

        Assert.Equal(25, page.Results.Count);
        Assert.Equal(25, page.Count);
    }

    [Fact]
    public void When_The_Source_Is_Empty_Should_Return_An_Empty_Page()
    {
        var page = Items(0).Paging(new Paging { Page = 1, Count = 10 });

        Assert.Empty(page.Results);
        Assert.Equal(0, page.Count);
    }

    [Fact]
    public async Task When_A_Page_Is_Requested_Asynchronously_Should_Return_Its_Items_And_The_Total_Count()
    {
        await using var context = await SeededContext(25);

        var page = await context.Items.OrderBy(i => i.Id).PagingAsync(new Paging { Page = 3, Count = 10 });

        Assert.Equal(new[] { 21, 22, 23, 24, 25 }, page.Results.Select(i => i.Id));
        Assert.Equal(25, page.Count);
    }

    [Fact]
    public async Task When_Paging_Is_Not_Specified_Asynchronously_Should_Return_Everything()
    {
        await using var context = await SeededContext(4);

        var page = await context.Items.PagingAsync(new Paging());

        Assert.Equal(4, page.Results.Count);
        Assert.Equal(4, page.Count);
    }

    [Fact]
    public async Task When_The_Page_Is_Past_The_End_Asynchronously_Should_Return_No_Items_But_Keep_The_Total()
    {
        await using var context = await SeededContext(5);

        var page = await context.Items.OrderBy(i => i.Id).PagingAsync(new Paging { Page = 4, Count = 5 });

        Assert.Empty(page.Results);
        Assert.Equal(5, page.Count);
    }

    [Fact]
    public async Task When_The_Table_Is_Empty_Asynchronously_Should_Return_An_Empty_Page()
    {
        await using var context = await SeededContext(0);

        var page = await context.Items.PagingAsync(new Paging { Page = 1, Count = 10 });

        Assert.Empty(page.Results);
        Assert.Equal(0, page.Count);
    }

    [Fact]
    public async Task When_Page_Size_Is_Invalid_Asynchronously_Should_Throw_Before_Querying()
    {
        await using var context = await SeededContext(1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Items.PagingAsync(new Paging { Page = 1, Count = 0 }));
    }

    [Fact]
    public void When_A_Page_Is_Projected_Should_Map_Its_Items_And_Keep_The_Total()
    {
        var page = Items(25).Paging(new Paging { Page = 1, Count = 2 });

        var names = page.Project(item => item.Name);

        Assert.Equal(new[] { "item-1", "item-2" }, names.Results);
        Assert.Equal(25, names.Count);
        Assert.Throws<ArgumentNullException>(() => page.Project<Item, string>(null));
        Assert.Throws<ArgumentNullException>(() => ((PagedResult<Item>)null).Project(item => item.Name));
    }

    // ─── PagingQuery ─────────────────────────────────────────────────────────

    [Fact]
    public void When_Paging_Query_Arguments_Are_Invalid_Should_Throw()
    {
        var paging = new Paging { Page = 1, Count = 10 };

        Assert.Throws<ArgumentNullException>(() => new PagingQuery<Item>(Items(1), null));
        Assert.Throws<ArgumentNullException>(() => new PagingQuery<Item>(null, paging));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PagingQuery<Item>(Items(1), paging, 0));
    }

    [Fact]
    public void When_Paging_Query_Is_Applied_Should_Behave_As_The_Paged_Queryable()
    {
        // arrange
        var source = Items(25);

        // act
        var query = new PagingQuery<Item>(source, new Paging { Page = 2, Count = 10 }).GetPagingQuery();

        // assert
        Assert.Equal(2, query.PageNumber);
        Assert.Equal(10, query.PageCount);
        Assert.Equal(25, query.TotalCount);
        Assert.True(query.IsMoreDataToFetch);
        Assert.Equal(typeof(Item), query.ElementType);
        Assert.Same(query.Query.Provider, query.Provider);
        Assert.Same(query.Query.Expression, query.Expression);
        Assert.Equal(Enumerable.Range(11, 10), query.Select(i => i.Id));
        var enumerator = ((IEnumerable)query).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(11, ((Item)enumerator.Current).Id);
    }

    [Fact]
    public void When_The_Requested_Page_Starts_After_The_Last_Row_Should_Report_Nothing_More_To_Fetch()
    {
        var query = new PagingQuery<Item>(Items(25), new Paging { Page = 9, Count = 10 }).GetPagingQuery();

        Assert.False(query.IsMoreDataToFetch);
    }

    [Fact]
    public async Task When_Paging_Query_Is_Applied_Asynchronously_Should_Count_And_Page()
    {
        await using var context = await SeededContext(12);

        var query = await new PagingQuery<Item>(context.Items.OrderBy(i => i.Id), new Paging { Page = 2, Count = 5 }, maxPageSize: 5).GetPagingQueryAsync();

        Assert.Equal(12, query.TotalCount);
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, (await query.Query.ToListAsync()).Select(i => i.Id));
    }
}
