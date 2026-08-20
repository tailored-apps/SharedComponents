using Xunit;

namespace TailoredApps.Shared.Querying.Tests
{
    public class PagedAndSortedQueryTests
    {
        private sealed class TestQueryFilter : QueryBase
        {
            public string Name { get; set; }
        }

        private sealed class TestPagedAndSortedQuery : PagedAndSortedQuery<TestQueryFilter>
        {
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData(1, null, false)]
        [InlineData(null, 10, false)]
        [InlineData(1, 10, true)]
        [InlineData(0, 0, true)]
        [InlineData(-1, -5, true)]
        public void When_Page_And_Count_Combination_Set_Should_Report_IsPagingSpecified_Accordingly(int? page, int? count, bool expected)
        {
            // arrange
            var query = new TestPagedAndSortedQuery
            {
                Page = page,
                Count = count
            };

            // act
            var result = query.IsPagingSpecified;

            // assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData("Name", null, false)]
        [InlineData(null, SortDirection.Asc, false)]
        [InlineData("", SortDirection.Asc, false)]
        [InlineData("  ", SortDirection.Asc, false)]
        [InlineData("\t", SortDirection.Desc, false)]
        [InlineData("\r\n", SortDirection.Asc, false)]
        [InlineData("Name", SortDirection.Asc, true)]
        [InlineData("Name", SortDirection.Desc, true)]
        public void When_SortField_And_SortDir_Combination_Set_Should_Report_IsSortingSpecified_Accordingly(string sortField, SortDirection? sortDir, bool expected)
        {
            // arrange
            var query = new TestPagedAndSortedQuery
            {
                SortField = sortField,
                SortDir = sortDir
            };

            // act
            var result = query.IsSortingSpecified;

            // assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void When_SortDir_Is_Undefined_But_Set_Should_Report_Sorting_As_Specified()
        {
            // arrange
            // Pins current behavior: SortDirection.Undefined is a non-null value, so IsSortingSpecified
            // only checks SortDir.HasValue and still returns true for Undefined.
            var query = new TestPagedAndSortedQuery
            {
                SortField = "Name",
                SortDir = SortDirection.Undefined
            };

            // act
            var result = query.IsSortingSpecified;

            // assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("Name", "Name", true)]
        [InlineData("Name", "name", true)]
        [InlineData("name", "NAME", true)]
        [InlineData("nAmE", "NaMe", true)]
        [InlineData("Name", "Other", false)]
        [InlineData("Name", "Nam", false)]
        [InlineData("Name", "", false)]
        [InlineData(" Name ", "Name", false)]
        public void When_IsSortBy_Called_Should_Compare_Field_Names_Case_Insensitively(string sortField, string fieldName, bool expected)
        {
            // arrange
            var query = new TestPagedAndSortedQuery
            {
                SortField = sortField
            };

            // act
            var result = query.IsSortBy(fieldName);

            // assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void When_SortField_Is_Null_IsSortBy_Should_Return_False_For_Non_Null_Field_Name()
        {
            // arrange
            var query = new TestPagedAndSortedQuery
            {
                SortField = null
            };

            // act
            var result = query.IsSortBy("Name");

            // assert
            Assert.False(result);
        }

        [Fact]
        public void When_SortField_Is_Null_IsSortBy_Should_Return_True_For_Null_Field_Name()
        {
            // arrange
            // Pins current behavior: string.Equals(null, null, ...) returns true,
            // so a query with no SortField reports being "sorted by" a null field name.
            var query = new TestPagedAndSortedQuery
            {
                SortField = null
            };

            // act
            var result = query.IsSortBy(null);

            // assert
            Assert.True(result);
        }

        [Fact]
        public void Should_Store_Filter_Assigned_To_Query()
        {
            // arrange
            var filter = new TestQueryFilter { Name = "abc" };
            var query = new TestPagedAndSortedQuery();

            // act
            query.Filter = filter;

            // assert
            Assert.Same(filter, query.Filter);
        }

        [Fact]
        public void Should_Have_Null_Defaults_And_Report_Nothing_Specified_On_New_Instance()
        {
            // arrange
            // act
            var query = new TestPagedAndSortedQuery();

            // assert
            Assert.Null(query.Page);
            Assert.Null(query.Count);
            Assert.Null(query.SortField);
            Assert.Null(query.SortDir);
            Assert.Null(query.Filter);
            Assert.False(query.IsPagingSpecified);
            Assert.False(query.IsSortingSpecified);
        }

        [Fact]
        public void Should_Implement_IPagedAndSortedQuery_And_Expose_Same_Values_Through_Interface()
        {
            // arrange
            var query = new TestPagedAndSortedQuery
            {
                Page = 2,
                Count = 25,
                SortField = "Name",
                SortDir = SortDirection.Desc
            };

            // act
            IPagedAndSortedQuery<TestQueryFilter> asInterface = query;

            // assert
            Assert.Equal(2, asInterface.Page);
            Assert.Equal(25, asInterface.Count);
            Assert.Equal("Name", asInterface.SortField);
            Assert.Equal(SortDirection.Desc, asInterface.SortDir);
            Assert.True(asInterface.IsPagingSpecified);
            Assert.True(asInterface.IsSortingSpecified);
            Assert.True(asInterface.IsSortBy("name"));
        }
    }
}
