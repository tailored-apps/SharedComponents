# TailoredApps.Shared.MediatR.ML

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.ML)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.ML/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library integrates **ML.NET** image classification with the MediatR pipeline. It provides ready-made commands (`ClassifyImage`, `TrainImageClassificationModel`) and their handlers, making image classification a first-class citizen in CQRS architecture.

Under the hood, `ImageClassificationService` uses ML.NET with a `PredictionEnginePool` for efficient concurrent inference. The library also supports in-app model training — you can train a new model by providing a labeled image dataset.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.ML
```

---

## DI registration

```csharp
// Program.cs
using Microsoft.Extensions.ML;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;

// MediatR: the handlers of both commands live in the library assembly, so scan it as well
builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).Assembly);
});

// Library services + the ML.NET PredictionEnginePool with the model
builder.Services.AddPredictionEngine(config =>
{
    config.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(pool =>
    {
        pool.FromFile(builder.Configuration[ImageClassificationOptions.ModelFilePathConfig]!);
    });
});
```

`AddPredictionEngine` registers `IImageClassificationService`, `IModelInfoService` and `IModelHelper` (scoped) and binds `ImageClassificationOptions`. It does not register the MediatR handlers - that is what scanning the library assembly is for. `RegisterMachineLearningModel` adds the ML.NET `PredictionEnginePool`; where the model is loaded from is decided by the pool builder (`FromFile` above). Register the model without a name: predictions use the default model of the pool.

The types live in the namespace `TailoredApps.Shared.MediatR.ImageClassification`, not under the package name.

### `appsettings.json` configuration

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
|-----|---------|---------|
| `ModelFilePath` | Model file described by `IImageClassificationService.GetModelInfo()` (name, checksum, version, labels); the registration above also loads the model from it | none |
| `TrainingRoot` | Directory that training sets must be inside; required for training | none |
| `ModelsRoot` | Directory that trained models are written inside; required for training | none |
| `MaxImageBytes` | Largest image accepted by `ClassifyImage`, in bytes; `0` turns the limit off | `10485760` (10 MB) |

The section is required: without it the options throw `InvalidOperationException` when they are first used.

---

## Usage example

### Image classification

```csharp
public class ImageAnalysisController : ControllerBase
{
    private readonly IMediator _mediator;

    public ImageAnalysisController(IMediator mediator) => _mediator = mediator;

    [HttpPost("classify")]
    public async Task<IActionResult> ClassifyImage(IFormFile imageFile)
    {
        using var ms = new MemoryStream();
        await imageFile.CopyToAsync(ms);

        var result = await _mediator.Send(new ClassifyImage
        {
            FileByteArray = ms.ToArray(),
            FileName = imageFile.FileName
        });

        var prediction = result.ImagePrediction!;   // the handler always sets it

        return Ok(new
        {
            prediction.FileName,
            prediction.PredictedLabel,
            prediction.PredictedScore,
            Confidence = $"{prediction.PredictedScore:P2}"
        });
    }
}
```

### Model training

```csharp
var trainingResult = await _mediator.Send(new TrainImageClassificationModel
{
    Source = "pets",                    // directory inside ImageClassification:TrainingRoot, one subfolder per class
    ModelDestFolderPath = "pets.zip"    // model file inside ImageClassification:ModelsRoot
});

Console.WriteLine($"Model {trainingResult.ModelPath}, version {trainingResult.ModelVersion}");
Console.WriteLine($"Labels: {string.Join(", ", trainingResult.Labels)}");
Console.WriteLine(trainingResult.ModelInfo);   // evaluation metrics as text
```

Training reads the files with the extension `.jpg` or `.png` from the source directory and its subfolders; the name of the folder a file is in becomes its label.

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `ClassifyImage` | Command | Image data to classify: `FileByteArray`, `FileName` |
| `ClassifyImageResponse` | Class | Result: `ImagePrediction` |
| `TrainImageClassificationModel` | Command | Training parameters: `Source`, `ModelDestFolderPath` |
| `TrainImageClassificationModelResponse` | Class | Training result: `ModelPath`, `ModelVersion`, `ModelInfo` (evaluation metrics as text), `Labels` |
| `IImageClassificationService` | Interface | `Predict(byte[], fileName)`, `Train(images, folder, dest)`, `GetModelInfo()` |
| `IPredictionEnginePoolAdapter<TData, TPred>` | Interface | Abstraction over the ML.NET `PredictionEnginePool` |
| `ModelInfo` | Class | Model metadata: name, checksum, version, labels |
| `ImagePrediction` | Class | Prediction result: `PredictedLabel`, `PredictedScore`, `FileName`; `Scores` and `ModelInfo` are not filled by `ClassifyImage` |
| `InMemoryImageData` | Class | Model input data: the image as `byte[]` |
| `ImagePredictionScore` | Class | Raw model output: `Score` (per class), `PredictedLabel` |
| `ImageClassificationOptions` | Class | Options bound from the `ImageClassification` section: `ModelFilePath`, `TrainingRoot`, `ModelsRoot`, `MaxImageBytes` |
| `AddPredictionEngineExtension.AddPredictionEngine` | Ext. method | Registers `IImageClassificationService`, `IModelInfoService`, `IModelHelper` and the options in DI (not the MediatR handlers) |
| `PredictionEngineServiceConfiguration.RegisterMachineLearningModel<D, R>` | Method | Adds the ML.NET `PredictionEnginePool<D, R>` and its adapter; called inside `AddPredictionEngine` |

---

## 🔒 Security

- **Training paths are confined to configured directories.** `TrainImageClassificationModel.Source` must resolve inside `ImageClassification:TrainingRoot` and `ModelDestFolderPath` inside `ImageClassification:ModelsRoot` (relative paths are resolved against those roots). A path outside its root throws `ArgumentException`; missing root configuration throws `InvalidOperationException`. Without this a request could recursively read any folder on the host and overwrite any file.
- **Images are validated before the decoder runs.** `ClassifyImage` rejects (`ArgumentException`) empty content, files larger than `ImageClassification:MaxImageBytes` (default 10 MB) and content without a JPEG/PNG signature.

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

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.MediatR.ML - instructions for an AI agent

You are using TailoredApps.Shared.MediatR.ML to classify images through ML.NET and MediatR.

### Registration
```csharp
// using Microsoft.Extensions.ML;
// using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
// using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).Assembly); // handlers of the library
});
builder.Services.AddPredictionEngine(config =>
    config.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(pool =>
        pool.FromFile(builder.Configuration[ImageClassificationOptions.ModelFilePathConfig]!)));
```

### What gets configured
- IImageClassificationService, IModelInfoService, IModelHelper (scoped) and ImageClassificationOptions
- The ML.NET PredictionEnginePool<InMemoryImageData, ImagePredictionScore> with the model (default, unnamed)
- The handlers of ClassifyImage and TrainImageClassificationModel - only through the MediatR scan of the library assembly

### Configuration (section "ImageClassification")
- ModelFilePath - the model file (used for the model metadata and by the registration above)
- TrainingRoot, ModelsRoot - directories training sets are read from / models are written to; required for training
- MaxImageBytes - image size limit for ClassifyImage, default 10485760 (10 MB), 0 = no limit

### Image classification
```csharp
var result = await _mediator.Send(new ClassifyImage
{
    FileByteArray = imageBytes,
    FileName = "photo.jpg"
});
// result.ImagePrediction.PredictedLabel  — the predicted class
// result.ImagePrediction.PredictedScore  — confidence (0-1)
```

### Model training
```csharp
var result = await _mediator.Send(new TrainImageClassificationModel
{
    Source = "pets",                   // inside TrainingRoot; subfolders = class names
    ModelDestFolderPath = "pets.zip"   // model file inside ModelsRoot
});
// result.ModelPath, result.ModelVersion, result.Labels, result.ModelInfo (metrics as text)
```

### Rules
- The namespace is TailoredApps.Shared.MediatR.ImageClassification, not the package name
- AddMediator must scan the library assembly - AddPredictionEngine does not register the handlers
- The model must be a ZIP file (ML.NET format)
- The `ImageClassification` configuration section is required — a missing section throws `InvalidOperationException` when the options are first used
- The model version and labels are stored in the archive as separate `Version.txt` and `Labels.txt` entries
- Training folder: each subfolder = one class, folder name = label
- PredictionEnginePool is thread-safe — safe for concurrent use
- PredictedScore ∈ [0,1] — the closer to 1, the higher the model's confidence
- Configure ImageClassification:TrainingRoot and ImageClassification:ModelsRoot - training rejects paths outside those directories
- ClassifyImage accepts only JPEG/PNG up to MaxImageBytes (default 10 MB); larger or foreign files throw ArgumentException
- Expose the training command to administrators only - it starts a long TensorFlow job on the given data
````
