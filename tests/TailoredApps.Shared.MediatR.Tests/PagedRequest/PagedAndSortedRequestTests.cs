using System.Collections.Generic;
using TailoredApps.Shared.MediatR.PagedRequest;
using TailoredApps.Shared.Querying;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class PagedAndSortedRequestTests
    {
        public class ItemQuery : QueryBase
        {
        }

        public class ItemPagedResult : IPagedResult<string>
        {
            public ICollection<string> Results { get; set; }

            public int Count { get; set; }
        }

        private static PagedAndSortedRequest<ItemPagedResult, ItemQuery, string> CreateSut()
            => new PagedAndSortedRequest<ItemPagedResult, ItemQuery, string>();

        [Theory]
        [InlineData(null, null, false)]
        [InlineData(1, null, false)]
        [InlineData(null, 10, false)]
        [InlineData(1, 10, true)]
        [InlineData(0, 0, true)]
        public void When_Page_Or_Count_Is_Missing_Should_Report_IsPagingSpecified_Accordingly(int? page, int? count, bool expected)
        {
            // arrange
            var sut = CreateSut();
            sut.Page = page;
            sut.Count = count;

            // act
            var isPagingSpecified = sut.IsPagingSpecified;

            // assert
            Assert.Equal(expected, isPagingSpecified);
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData("Name", null, false)]
        [InlineData(null, SortDirection.Asc, false)]
        [InlineData("", SortDirection.Asc, false)]
        [InlineData("   ", SortDirection.Asc, false)]
        [InlineData("Name", SortDirection.Asc, true)]
        [InlineData("Name", SortDirection.Desc, true)]
        // Pins current behavior: SortDirection.Undefined still counts as a specified direction.
        [InlineData("Name", SortDirection.Undefined, true)]
        public void When_SortField_Or_SortDir_Is_Missing_Should_Report_IsSortingSpecified_Accordingly(string sortField, SortDirection? sortDir, bool expected)
        {
            // arrange
            var sut = CreateSut();
            sut.SortField = sortField;
            sut.SortDir = sortDir;

            // act
            var isSortingSpecified = sut.IsSortingSpecified;

            // assert
            Assert.Equal(expected, isSortingSpecified);
        }

        [Theory]
        [InlineData("Name", "Name", true)]
        [InlineData("Name", "name", true)]
        [InlineData("name", "NAME", true)]
        [InlineData("nAmE", "NaMe", true)]
        [InlineData("Name", "Other", false)]
        [InlineData("Name", null, false)]
        [InlineData(null, "Name", false)]
        // Pins current behavior: string.Equals(null, null) is true, so a null SortField matches a null field name.
        [InlineData(null, null, true)]
        public void When_Comparing_Sort_Field_Should_Match_Case_Insensitively(string sortField, string fieldName, bool expected)
        {
            // arrange
            var sut = CreateSut();
            sut.SortField = sortField;

            // act
            var isSortBy = sut.IsSortBy(fieldName);

            // assert
            Assert.Equal(expected, isSortBy);
        }

        [Fact]
        public void Should_Hold_Assigned_Filter()
        {
            // arrange
            var sut = CreateSut();
            var filter = new ItemQuery();

            // act
            sut.Filter = filter;

            // assert
            Assert.Same(filter, sut.Filter);
        }
    }
}
