using System;
using System.Collections.Generic;
using MediatR;
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
        public void Should_Build_Default_Cache_Key_From_Request_Type_FullName_And_Content_Hash()
        {
            // arrange
            ICachePolicy<CachedRequest, string> policy = new DefaultCachePolicy();
            var request = new CachedRequest { Id = 7 };

            // act
            var cacheKey = policy.GetCacheKey(request);

            // assert - namespaced by type, content represented by a hash (no plain-text property values)
            Assert.StartsWith($"{typeof(CachedRequest).FullName}:", cacheKey);
            Assert.Equal(CacheKeyGenerator.Generate(typeof(CachedRequest), request), cacheKey);
            Assert.DoesNotContain("Id:7", cacheKey);
            Assert.Equal(cacheKey, policy.GetCacheKey(new CachedRequest { Id = 7 }));
        }

        [Fact]
        public void When_Requests_Differ_Only_In_Collection_Contents_Should_Produce_Different_Keys()
        {
            // arrange
            ICachePolicy<CollectionRequest, string> policy = new CollectionCachePolicy();

            // act
            var first = policy.GetCacheKey(new CollectionRequest { CustomerIds = new List<int> { 1 } });
            var second = policy.GetCacheKey(new CollectionRequest { CustomerIds = new List<int> { 2 } });

            // assert - the old key used List.ToString(), which made these collide
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void When_Request_Contains_Sensitive_Value_Should_Not_Expose_It_In_Key()
        {
            ICachePolicy<CachedRequest, string> policy = new DefaultCachePolicy();
            var key = policy.GetCacheKey(new CachedRequest { Id = 424242 });
            Assert.DoesNotContain("424242", key);
        }

        private sealed class CollectionRequest : IRequest<string>
        {
            public List<int> CustomerIds { get; set; }
        }

        private sealed class CollectionCachePolicy : ICachePolicy<CollectionRequest, string>
        {
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
