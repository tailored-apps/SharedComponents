using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.Handlers.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests;

/// <summary>
/// The constructors that shipped before the security hardening must keep working for code that
/// creates the handlers by hand, without weakening the checks the hardening added.
/// </summary>
public class ConstructorCompatibilityTests
{
    [Fact]
    public async Task When_Train_Handler_Is_Created_Without_Options_Should_Reject_Training()
    {
        // arrange: the pre-hardening constructor, which has no TrainingRoot / ModelsRoot
        var classificationService = new Mock<IImageClassificationService>(MockBehavior.Strict);
        var sut = new TrainImageClassificationModelCommandHandler(classificationService.Object, Mock.Of<IModelHelper>());
        var request = new TrainImageClassificationModel { Source = "training", ModelDestFolderPath = "models" };

        // act + assert: fails closed instead of training from an unconfined path
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(request, CancellationToken.None));
    }

    [Fact]
    public async Task When_Classify_Handler_Is_Created_Without_Options_Should_Still_Validate_Images()
    {
        // arrange: the pre-hardening constructor
        var classificationService = new Mock<IImageClassificationService>(MockBehavior.Strict);
        var sut = new ClassifyImageCommandHandler(classificationService.Object);
        var request = new ClassifyImage { FileName = "not-an-image.txt", FileByteArray = new byte[] { 1, 2, 3, 4 } };

        // act + assert: content that is neither JPEG nor PNG never reaches the model
        await Assert.ThrowsAsync<ArgumentException>(() => sut.Handle(request, CancellationToken.None));
    }
}
