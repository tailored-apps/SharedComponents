# TailoredApps.Shared.MediatR.ML

Image classification with ML.NET as MediatR requests. `ClassifyImage` runs a trained model on an uploaded image
and `TrainImageClassificationModel` trains a new model from folders of labelled images. The code lives in the
namespace `TailoredApps.Shared.MediatR.ImageClassification`, not under the package name.

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.ML
```

## Registration

```csharp
using Microsoft.Extensions.ML;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;

// The handlers of both commands are in this package's assembly
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).Assembly);
});

builder.Services.AddPredictionEngine(engine =>
    engine.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(pool =>
        pool.FromFile(builder.Configuration[ImageClassificationOptions.ModelFilePathConfig]!)));
```

`AddPredictionEngine` registers `IImageClassificationService`, `IModelInfoService` and `IModelHelper` (scoped)
and binds `ImageClassificationOptions`. `RegisterMachineLearningModel` adds the ML.NET `PredictionEnginePool`;
where the model is loaded from is decided by the pool builder you configure (`FromFile` above), not by the options.

## Usage

```csharp
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;

var classified = await mediator.Send(new ClassifyImage { FileByteArray = imageBytes, FileName = "photo.jpg" });
var label = classified.ImagePrediction?.PredictedLabel;
var score = classified.ImagePrediction?.PredictedScore;

var trained = await mediator.Send(new TrainImageClassificationModel
{
    Source = "pets",                   // folder inside TrainingRoot, one sub-folder per label
    ModelDestFolderPath = "pets.zip",  // model file inside ModelsRoot
});
// trained.ModelPath, trained.ModelVersion, trained.Labels, trained.ModelInfo (evaluation metrics as text)
```

## Configuration

The `ImageClassification` section:

```json
{
  "ImageClassification": {
    "ModelFilePath": "/var/app/models/current.zip",
    "TrainingRoot": "/var/app/training-sets",
    "ModelsRoot": "/var/app/models",
    "MaxImageBytes": 10485760
  }
}
```

| Key | Meaning | Default |
| --- | --- | --- |
| `ModelFilePath` | Model file described by `IImageClassificationService.GetModelInfo()` (name, checksum, version, labels); the registration above also loads the model from it | none |
| `TrainingRoot` | Directory that training sets must be inside; required for training | none |
| `ModelsRoot` | Directory that trained models are written inside; required for training | none |
| `MaxImageBytes` | Largest image accepted by `ClassifyImage`; `0` turns the limit off | `10485760` (10 MB) |

## Good to know

- The section is required: without it the options throw `InvalidOperationException` when first used.
- `ClassifyImage` throws `ArgumentException` for empty content, for content over `MaxImageBytes` and for
  content that does not start with a JPEG or PNG signature. Only `FileName`, `PredictedLabel` and
  `PredictedScore` of the returned `ImagePrediction` are filled; `Scores` and `ModelInfo` stay `null`.
- Training paths are resolved against `TrainingRoot` and `ModelsRoot`. A path outside its root throws
  `ArgumentException`; a root that is not configured throws `InvalidOperationException`.
- Training reads files with the extension `.jpg` or `.png` from `Source` and its sub-folders and uses the name
  of the folder a file is in as its label. It runs inside the request and can take long; a fifth of the images
  is held back for evaluation. The saved model is a zip archive to which `Version.txt` (a `yyyyMMdd.HHmmss`
  timestamp) and `Labels.txt` are added.
- Prediction uses the default model of the pool, so register the model without a model name.
- `PredictionEngineServiceConfiguration` is the one public type in the namespace
  `TailoredApps.Shared.MediatR.ML.Infrastructure`.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/MediatR/ML/
