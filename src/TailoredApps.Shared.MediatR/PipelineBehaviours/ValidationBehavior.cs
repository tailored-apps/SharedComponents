using FluentValidation;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.PipelineBehaviours
{
    /// <summary>
    /// Pipeline behavior MediatR uruchamiający wszystkie zarejestrowane walidatory FluentValidation
    /// przed przekazaniem żądania do handlera.
    /// </summary>
    /// <typeparam name="TRequest">Typ żądania MediatR.</typeparam>
    /// <typeparam name="TResponse">Typ odpowiedzi.</typeparam>
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        /// <summary>Inicjalizuje instancję <see cref="ValidationBehavior{TRequest, TResponse}"/>.</summary>
        /// <param name="validators">Kolekcja walidatorów dla żądania.</param>
        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        /// <inheritdoc/>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = _validators
                .Select(v => v.Validate(context))
                .SelectMany(result => result.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }

            return await next();
        }
    }
}
