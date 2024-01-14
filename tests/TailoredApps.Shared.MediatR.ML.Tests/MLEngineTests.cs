using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ML;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class MLEngineTests
    {
        [Fact(Skip = "brak")]
        public async Task CanLearnAndVerifyModel()
        {
            var modelName = ".\\testmodle.model";


            var hostCreateModel = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration(a => a.AddEnvironmentVariables())
                 .ConfigureServices((_, services) =>
                 {
                     services.AddMediatR(conf =>
                     {
                         conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                         conf.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).GetTypeInfo().Assembly);
                     });
                     services.AddPredictionEngine(services =>
                     {
                         services.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(builder =>
                         {
                            // builder.FromFile(modelName);
                         });
                     });
                 }).Build();
            var mediatoR = hostCreateModel.Services.GetService<IMediator>();
            Assert.NotNull(mediatoR);

            var response =await mediatoR.Send(new TrainImageClassificationModel { Source= "D:\\WORK\\SharedComponents\\tests\\TailoredApps.Shared.MediatR.ML.Tests\\TestData\\ImageClassification\\LearningSets", ModelDestFolderPath=modelName });
            Assert.NotNull(response);

            var hostVerify = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration(a => a.AddEnvironmentVariables())
                 .ConfigureServices((_, services) =>
                 {
                     services.AddMediatR(conf =>
                     {
                         conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                         conf.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).GetTypeInfo().Assembly);
                     });
                     services.AddPredictionEngine(services =>
                     {
                         services.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(builder =>
                         {
                            builder.FromFile(modelName);
                         });
                     });
                 }).Build();

            var mediatorVerify = hostVerify.Services.GetService<IMediator>();
            Assert.NotNull(mediatorVerify);
            var testFileRed = File.OpenRead("D:\\WORK\\SharedComponents\\tests\\TailoredApps.Shared.MediatR.ML.Tests\\TestData\\ImageClassification\\TestImages\\testred.png");
            var responseClassification = await mediatorVerify.Send(new ClassifyImage { FileByteArray = ReadFully(testFileRed), FileName= "testred.png" });
            Assert.NotNull(responseClassification);

        }

        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }

}
