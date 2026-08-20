using System;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    /// <summary>
    /// Tests the C# default interface implementations of <see cref="ICachePolicy{TRequest, TResponse}"/>.
    /// The members must be invoked through the interface type - an implementing class does not surface them.
    /// </summary>
    public class ICachePolicyTests
    {
        [Fact]
        public void Should_Provide_Default_Expiration_Values()
        {
            // arrange
            ICachePolicy<CachedRequest, string> policy = new DefaultCachePolicy();

            // act & assert
            Assert.Null(policy.AbsoluteExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), policy.AbsoluteExpirationRelativeToNow);
            Assert.Equal(TimeSpan.FromSeconds(30), policy.SlidingExpiration);
        }

        [Fact]
        public void Should_Build_Default_Cache_Key_From_Request_Type_FullName_And_Property_Values()
        {
            // arrange
            ICachePolicy<CachedRequest, string> policy = new DefaultCachePolicy();
            var request = new CachedRequest { Id = 7 };

            // act
            var cacheKey = policy.GetCacheKey(request);

            // assert
            Assert.Equal($"{typeof(CachedRequest).FullName}{{Id:7}}", cacheKey);
        }

        [Fact]
        public void When_Requests_Differ_Should_Produce_Different_Default_Cache_Keys()
        {
            // arrange
            ICachePolicy<CachedRequest, string> policy = new DefaultCachePolicy();

            // act
            var firstKey = policy.GetCacheKey(new CachedRequest { Id = 1 });
            var secondKey = policy.GetCacheKey(new CachedRequest { Id = 2 });

            // assert
            Assert.NotEqual(firstKey, secondKey);
        }
    }
}
