using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.MediatR.Caching;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class CacheTests
    {
        public class CachedPoco
        {
            public int Number { get; set; }

            public string Text { get; set; }

            public DateTime Timestamp { get; set; }
        }

        private static Cache CreateSut()
            => new Cache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        [Fact]
        public async Task Should_RoundTrip_Poco_Through_Real_Distributed_Cache()
        {
            // arrange
            var sut = CreateSut();
            var poco = new CachedPoco
            {
                Number = 42,
                Text = "cached value",
                Timestamp = new DateTime(2026, 8, 20, 10, 30, 0, DateTimeKind.Utc),
            };

            // act
            await sut.SetAsync("poco-key", poco, TimeSpan.FromMinutes(1), null, TimeSpan.FromMinutes(5), CancellationToken.None);
            var loaded = await sut.GetAsync<CachedPoco>("poco-key", CancellationToken.None);

            // assert
            Assert.NotNull(loaded);
            Assert.NotSame(poco, loaded);
            Assert.Equal(poco.Number, loaded.Number);
            Assert.Equal(poco.Text, loaded.Text);
            Assert.Equal(poco.Timestamp, loaded.Timestamp);
        }

        [Fact]
        public async Task When_Key_Is_Absent_Should_Return_Default_For_Reference_Type()
        {
            // arrange
            var sut = CreateSut();

            // act
            var loaded = await sut.GetAsync<CachedPoco>("missing-key", CancellationToken.None);

            // assert
            Assert.Null(loaded);
        }

        [Fact]
        public async Task When_Key_Is_Absent_Should_Return_Default_For_Value_Type()
        {
            // arrange
            var sut = CreateSut();

            // act
            var loaded = await sut.GetAsync<int>("missing-key", CancellationToken.None);

            // assert
            Assert.Equal(0, loaded);
        }

        [Fact]
        public async Task Should_RoundTrip_Value_Type_Through_Real_Distributed_Cache()
        {
            // arrange
            var sut = CreateSut();

            // act
            await sut.SetAsync("int-key", 1234, null, null, TimeSpan.FromMinutes(5), CancellationToken.None);
            var loaded = await sut.GetAsync<int>("int-key", CancellationToken.None);

            // assert
            Assert.Equal(1234, loaded);
        }
    }
}
