using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class CachingBehaviorTests
    {
        private static CachingBehavior<CachedRequest, string> CreateSut(ICache cache, IEnumerable<ICachePolicy<CachedRequest, string>> policies)
            => new CachingBehavior<CachedRequest, string>(cache, NullLogger<CachingBehavior<CachedRequest, string>>.Instance, policies);

        [Fact]
        public async Task When_No_Cache_Policy_Is_Registered_Should_Invoke_Next_Once_And_Not_Touch_Cache()
        {
            // arrange
            var cacheMock = new Mock<ICache>(MockBehavior.Strict);
            var sut = CreateSut(cacheMock.Object, Enumerable.Empty<ICachePolicy<CachedRequest, string>>());
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var response = await sut.Handle(new CachedRequest { Id = 1 }, next, CancellationToken.None);

            // assert
            Assert.Equal("handler-response", response);
            Assert.Equal(1, nextCalls);
            cacheMock.Verify(c => c.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            cacheMock.Verify(
                c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<DateTime?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task When_Cached_Response_Exists_Should_Return_It_Without_Invoking_Next()
        {
            // arrange
            var request = new CachedRequest { Id = 7 };
            var policyMock = new Mock<ICachePolicy<CachedRequest, string>>();
            policyMock.Setup(p => p.GetCacheKey(request)).Returns("cache-key-7");
            var cacheMock = new Mock<ICache>();
            cacheMock.Setup(c => c.GetAsync<string>("cache-key-7", It.IsAny<CancellationToken>())).ReturnsAsync("cached-response");
            var sut = CreateSut(cacheMock.Object, new[] { policyMock.Object });
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var response = await sut.Handle(request, next, CancellationToken.None);

            // assert
            Assert.Equal("cached-response", response);
            Assert.Equal(0, nextCalls);
            cacheMock.Verify(
                c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<DateTime?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task When_Cache_Misses_Should_Invoke_Next_Once_And_Store_Response_With_Policy_Expirations()
        {
            // arrange
            var request = new CachedRequest { Id = 3 };
            var absoluteExpiration = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var policyMock = new Mock<ICachePolicy<CachedRequest, string>>();
            policyMock.Setup(p => p.GetCacheKey(request)).Returns("cache-key-3");
            policyMock.Setup(p => p.SlidingExpiration).Returns(TimeSpan.FromSeconds(42));
            policyMock.Setup(p => p.AbsoluteExpiration).Returns(absoluteExpiration);
            policyMock.Setup(p => p.AbsoluteExpirationRelativeToNow).Returns(TimeSpan.FromMinutes(7));
            var cacheMock = new Mock<ICache>();
            cacheMock.Setup(c => c.GetAsync<string>("cache-key-3", It.IsAny<CancellationToken>())).ReturnsAsync((string)null);
            var sut = CreateSut(cacheMock.Object, new[] { policyMock.Object });
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("fresh-response");
            };

            // act
            var response = await sut.Handle(request, next, CancellationToken.None);

            // assert
            Assert.Equal("fresh-response", response);
            Assert.Equal(1, nextCalls);
            cacheMock.Verify(
                c => c.SetAsync("cache-key-3", "fresh-response", TimeSpan.FromSeconds(42), absoluteExpiration, TimeSpan.FromMinutes(7), CancellationToken.None),
                Times.Once);
        }
    }
}
