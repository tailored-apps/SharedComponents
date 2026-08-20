using TailoredApps.Shared.MediatR.Interfaces.Messages;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    /// <summary>
    /// Tests the C# default interface implementations of <see cref="IRetryableRequest{TRequest, TResponse}"/>.
    /// The members must be invoked through the interface type - an implementing class does not surface them.
    /// </summary>
    public class IRetryableRequestTests
    {
        [Fact]
        public void Should_Provide_Default_Retry_Configuration_Values()
        {
            // arrange
            IRetryableRequest<RetryRequest, string> config = new DefaultRetryConfig();

            // act & assert
            Assert.Equal(1, config.RetryAttempts);
            Assert.Equal(250, config.RetryDelay);
            Assert.False(config.RetryWithExponentialBackoff);
            Assert.Equal(1, config.ExceptionsAllowedBeforeCircuitTrip);
        }

        [Fact]
        public void When_Implementation_Overrides_Members_Should_Return_Overridden_Values_Through_Interface()
        {
            // arrange
            IRetryableRequest<RetryRequest, string> config = new TwoAttemptsExponentialBackoffRetryConfig();

            // act & assert
            Assert.Equal(2, config.RetryAttempts);
            Assert.Equal(1, config.RetryDelay);
            Assert.True(config.RetryWithExponentialBackoff);
            Assert.Equal(10, config.ExceptionsAllowedBeforeCircuitTrip);
        }
    }
}
