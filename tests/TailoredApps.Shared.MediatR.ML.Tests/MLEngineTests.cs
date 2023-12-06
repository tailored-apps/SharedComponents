using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests
{
    public class MLEngineTests
    {

        [Fact]
        public async Task CanRegisterAndGetModel()
        {
            var host = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration(a => a.AddEnvironmentVariables())
                 .ConfigureServices((_, services) =>
                 {
                     services.AddPredictionEngine(_.Configuration);
                 }).Build();

            var paymentService = host.Services.GetService<IClassificationService>();
            var providers = paymentService.GetModelInfo();
            Assert.NotNull(providers);
        }


    }
}
