using System;
using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces;
using TailoredApps.Shared.EntityFramework.Querying;
using TailoredApps.Shared.Querying;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.Querying
{
    public class QueryingHardeningTests
    {
        private sealed class Owner
        {
            public string Secret { get; set; }
        }

        private sealed class Person : IModelBase
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string PasswordHash { get; set; }
            public Owner Owner { get; set; }
        }

        private sealed class Sorting : ISortingParameters
        {
            public string SortField { get; set; }
            public SortDirection? SortDir { get; set; }
            public bool IsSortingSpecified => !string.IsNullOrWhiteSpace(SortField) && SortDir.HasValue;
        }

        private sealed class Paging : IPagingParameters
        {
            public int? Page { get; set; }
            public int? Count { get; set; }
            public bool IsPagingSpecified => Page.HasValue && Count.HasValue;
        }

        private static IQueryable<Person> People() => new List<Person>
        {
            new() { Id = 1, Name = "Charlie", PasswordHash = "c", Owner = new Owner { Secret = "3" } },
            new() { Id = 2, Name = "Alice", PasswordHash = "a", Owner = new Owner { Secret = "1" } },
            new() { Id = 3, Name = "Bob", PasswordHash = "b", Owner = new Owner { Secret = "2" } },
        }.AsQueryable();

        // ─── Sorting ─────────────────────────────────────────────────────────

        [Fact]
        public void When_Sort_Field_Is_A_Plain_Property_Should_Sort()
        {
            var result = People().ApplySorting(new Sorting { SortField = "Name", SortDir = SortDirection.Asc }).ToList();
            Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, result.Select(p => p.Name));
        }

        [Fact]
        public void When_Sort_Direction_Is_Desc_Should_Sort_Descending()
        {
            var result = People().ApplySorting(new Sorting { SortField = "Id", SortDir = SortDirection.Desc }).ToList();
            Assert.Equal(new[] { 3, 2, 1 }, result.Select(p => p.Id));
        }

        [Theory]
        [InlineData("Owner.Secret")]
        [InlineData("Name, Id desc")]
        [InlineData("Name desc")]
        [InlineData("iif(Id > 1, 1, 0)")]
        [InlineData("Name.Length")]
        [InlineData("1")]
        public void When_Sort_Field_Is_Not_A_Simple_Identifier_Should_Throw_Without_Querying(string sortField)
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                People().ApplySorting(new Sorting { SortField = sortField, SortDir = SortDirection.Asc }));
            Assert.False(QuerySortingExtensions.IsSimpleSortField(sortField));
            Assert.DoesNotContain("Person", ex.Message);
        }

        [Fact]
        public void When_Sort_Field_Does_Not_Exist_Should_Throw_ArgumentException_Without_Type_Details()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                People().ApplySorting(new Sorting { SortField = "DoesNotExist", SortDir = SortDirection.Asc }).ToList());
            Assert.DoesNotContain("Person", ex.Message);
            Assert.DoesNotContain("PasswordHash", ex.Message);
        }

        [Fact]
        public void When_Allow_List_Is_Given_Should_Reject_Fields_Outside_It()
        {
            var allowed = new[] { "Name", "Id" };

            var ok = People().ApplySorting(new Sorting { SortField = "name", SortDir = SortDirection.Asc }, allowed).ToList();
            Assert.Equal("Alice", ok.First().Name);

            Assert.Throws<ArgumentException>(() =>
                People().ApplySorting(new Sorting { SortField = "PasswordHash", SortDir = SortDirection.Asc }, allowed));
        }

        [Fact]
        public void When_Multiple_Sortings_Are_Given_Should_Validate_Each()
        {
            var sortings = new ISortingParameters[]
            {
                new Sorting { SortField = "Name", SortDir = SortDirection.Asc },
                new Sorting { SortField = "Owner.Secret", SortDir = SortDirection.Asc },
            };

            Assert.Throws<ArgumentException>(() => People().ApplySorting(sortings));
        }

        [Fact]
        public void When_No_Sorting_Specified_Should_Return_Query_Unchanged()
        {
            var query = People();
            Assert.Same(query, query.ApplySorting(new Sorting()));
            Assert.Same(query, query.ApplySorting((ISortingParameters)null));
        }

        // ─── Paging ──────────────────────────────────────────────────────────

        [Fact]
        public void When_Paging_Is_Valid_Should_Apply_Skip_And_Take()
        {
            var paged = new PagingQuery<Person>(People(), new Paging { Page = 2, Count = 2 }).GetPagingQuery();

            Assert.Equal(3, paged.TotalCount);
            Assert.Equal(2, paged.PageNumber);
            Assert.Equal(2, paged.PageCount);
            Assert.Single(paged.ToList());
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(-1, 10)]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        public void When_Page_Or_Count_Is_Below_One_Should_Throw(int page, int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PagingQuery<Person>(People(), new Paging { Page = page, Count = count }).GetPagingQuery());
        }

        [Fact]
        public void When_Count_Exceeds_Max_Page_Size_Should_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PagingQuery<Person>(People(), new Paging { Page = 1, Count = int.MaxValue }).GetPagingQuery());
        }

        [Fact]
        public void When_Explicit_Max_Page_Size_Is_Given_Should_Enforce_It()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PagingQuery<Person>(People(), new Paging { Page = 1, Count = 3 }, maxPageSize: 2).GetPagingQuery());

            var ok = new PagingQuery<Person>(People(), new Paging { Page = 1, Count = 2 }, maxPageSize: 2).GetPagingQuery();
            Assert.Equal(2, ok.ToList().Count);
        }

        [Fact]
        public void When_Skip_Would_Overflow_Should_Throw_Instead_Of_Wrapping()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PagingQuery<Person>(People(), new Paging { Page = int.MaxValue, Count = 1000 }).GetPagingQuery());
        }

        [Fact]
        public void When_Global_Max_Page_Size_Is_Invalid_Should_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PagingQuery<Person>.MaxPageSize = 0);
            Assert.Equal(PagingQuery<Person>.DefaultMaxPageSize, PagingQuery<Person>.MaxPageSize);
        }
    }
}
