using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class ValidationBehaviorTests
    {
        private class NameValidator : AbstractValidator<ValidatedRequest>
        {
            public NameValidator()
            {
                RuleFor(x => x.Name).NotEmpty();
            }
        }

        private class AgeValidator : AbstractValidator<ValidatedRequest>
        {
            public AgeValidator()
            {
                RuleFor(x => x.Age).GreaterThan(0);
            }
        }

        private static ValidationBehavior<ValidatedRequest, string> CreateSut(params IValidator<ValidatedRequest>[] validators)
            => new ValidationBehavior<ValidatedRequest, string>(validators);

        [Fact]
        public async Task When_No_Validators_Are_Registered_Should_Pass_Through_To_Next()
        {
            // arrange
            var sut = CreateSut();
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var response = await sut.Handle(new ValidatedRequest(), next, CancellationToken.None);

            // assert
            Assert.Equal("handler-response", response);
            Assert.Equal(1, nextCalls);
        }

        [Fact]
        public async Task When_All_Validators_Pass_Should_Invoke_Next_And_Return_Its_Response()
        {
            // arrange
            var sut = CreateSut(new NameValidator(), new AgeValidator());
            var request = new ValidatedRequest { Name = "Jan", Age = 30 };
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var response = await sut.Handle(request, next, CancellationToken.None);

            // assert
            Assert.Equal("handler-response", response);
            Assert.Equal(1, nextCalls);
        }

        [Fact]
        public async Task When_Validation_Fails_Should_Throw_ValidationException_Aggregating_Failures_From_All_Validators()
        {
            // arrange
            var sut = CreateSut(new NameValidator(), new AgeValidator());
            var request = new ValidatedRequest { Name = string.Empty, Age = 0 };
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => sut.Handle(request, next, CancellationToken.None));

            // assert - failures from every registered validator are aggregated exactly once, next is never invoked
            Assert.Equal(2, exception.Errors.Count());
            Assert.Equal(1, exception.Errors.Count(e => e.PropertyName == nameof(ValidatedRequest.Name)));
            Assert.Equal(1, exception.Errors.Count(e => e.PropertyName == nameof(ValidatedRequest.Age)));
            Assert.Equal(0, nextCalls);
        }

        [Fact]
        public async Task When_Only_One_Validator_Fails_Should_Throw_With_That_Single_Failure()
        {
            // arrange
            var sut = CreateSut(new NameValidator(), new AgeValidator());
            var request = new ValidatedRequest { Name = "Jan", Age = -1 };
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => sut.Handle(request, next, CancellationToken.None));

            // assert
            var failure = Assert.Single(exception.Errors);
            Assert.Equal(nameof(ValidatedRequest.Age), failure.PropertyName);
            Assert.Equal(0, nextCalls);
        }
    }
}
