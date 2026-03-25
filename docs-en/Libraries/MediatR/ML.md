# TailoredApps.Shared.MediatR.ML

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.ML)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.ML/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library integrates **ML.NET** image classification with the MediatR pipeline. It provides ready-made commands (`ClassifyImage`, `TrainImageClassificationModel`) and their handlers, making image classification a first-class citizen in CQRS architecture.

Under the hood, `ImageClassificationService` uses ML.NET with a `PredictionEnginePool` for efficient concurrent inference. The library also supports in-app model training — you can train a new model by providing a labeled image dataset.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.MediatR.ML
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;

builder.Services.AddPredictionEngine(config =>
{
    config.AddImageClassificationModel(modelBuilder =>
    {
        modelBuilder.AddFromFile("Models/image-classifier.zip");
    });
});

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

### Konfiguracja `appsettings.json`

```json
{
  "ML": {
    "ImageClassification": {
      "ModelPath": "Models/image-classifier.zip",
      "LabelsPath": "Models/labels.txt"
    }
  }
}
```

---

## Przykład użycia

### Klasyfikacja obrazu

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

        return Ok(new
        {
            result.FileName,
            result.PredictedLabel,
            result.PredictedScore,
            Confidence = $"{result.PredictedScore:P2}"
        });
    }
}
```

### Trening modelu

```csharp
var trainingResult = await _mediator.Send(new TrainImageClassificationModel
{
    TrainingSetFolder = "/data/training-images",   // katalog z podfolderami per klasa
    ModelDestinationPath = "Models/new-model.zip"
});

Console.WriteLine($"Trained! Labels: {string.Join(", ", trainingResult.Labels)}");
Console.WriteLine(trainingResult.EvaluationInfo);
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `ClassifyImage` | Command | Dane obrazu do klasyfikacji: `FileByteArray`, `FileName` |
| `ClassifyImageResponse` | Klasa | Wynik: `FileName`, `PredictedLabel`, `PredictedScore` |
| `TrainImageClassificationModel` | Command | Parametry treningu: `TrainingSetFolder`, `ModelDestinationPath` |
| `TrainImageClassificationModelResponse` | Klasa | Wynik treningu: `Labels[]`, `EvaluationInfo` |
| `IImageClassificationService` | Interfejs | `Predict(byte[], fileName)`, `Train(images, folder, dest)`, `GetModelInfo()` |
| `IPredictionEnginePoolAdapter<TData, TPred>` | Interfejs | Abstrakcja nad ML.NET `PredictionEnginePool` |
| `ModelInfo` | Klasa | Metadane modelu: nazwa, checksum, wersja, etykiety |
| `ImagePrediction` | Klasa | Wynik predykcji: etykieta, score, nazwa pliku |
| `InMemoryImageData` | Klasa | Dane wejściowe do modelu: obraz jako `byte[]` |
| `AddPredictionEngineExtension.AddPredictionEngine` | Metoda ext. | Rejestruje cały stack ML w DI |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.MediatR.ML — Instrukcja dla agenta AI

Używasz TailoredApps.Shared.MediatR.ML do klasyfikacji obrazów przez ML.NET i MediatR.

### Rejestracja
```csharp
builder.Services.AddPredictionEngine(config =>
    config.AddImageClassificationModel(b => b.AddFromFile("Models/model.zip")));
```

### Klasyfikacja obrazu
```csharp
var result = await _mediator.Send(new ClassifyImage
{
    FileByteArray = imageBytes,
    FileName = "photo.jpg"
});
// result.PredictedLabel  — przewidywana klasa
// result.PredictedScore  — pewność (0-1)
```

### Trening modelu
```csharp
var result = await _mediator.Send(new TrainImageClassificationModel
{
    TrainingSetFolder = "/data/images",       // podfoldery = nazwy klas
    ModelDestinationPath = "Models/new.zip"
});
```

### Zasady
- Model musi być plikiem ZIP (ML.NET format)
- Folder treningowy: każdy podfolder = jedna klasa, nazwa folderu = etykieta
- PredictionEnginePool jest thread-safe — bezpieczne współbieżne użycie
- PredictedScore ∈ [0,1] — im bliżej 1, tym wyższe zaufanie modelu
```
