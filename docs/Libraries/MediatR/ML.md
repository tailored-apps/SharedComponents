# TailoredApps.Shared.MediatR.ML

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.ML)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.ML/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Biblioteka integruje klasyfikację obrazów przez **ML.NET** z pipeline MediatR. Dostarcza gotowe komendy (`ClassifyImage`, `TrainImageClassificationModel`) i ich handlery, dzięki czemu klasyfikacja obrazów staje się pełnoprawnym elementem architektury CQRS.

Pod spodem działa `ImageClassificationService` oparty na ML.NET z poolem silników predykcji (`PredictionEnginePool`) dla wydajnej współbieżnej inferencji. Biblioteka obsługuje też trening modelu z poziomu aplikacji — możesz wytrenować nowy model podając zestaw oznaczonych obrazów.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.MediatR.ML
```

---

## Rejestracja w DI

```csharp
// Program.cs
using Microsoft.Extensions.ML;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;

// MediatR: handlery obu komend są w assembly biblioteki, więc trzeba je także przeskanować
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).Assembly);
});

// Serwisy biblioteki + PredictionEnginePool z ML.NET z modelem
builder.Services.AddPredictionEngine(config =>
{
    config.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(pool =>
    {
        pool.FromFile(builder.Configuration[ImageClassificationOptions.ModelFilePathConfig]!);
    });
});
```

`AddPredictionEngine` rejestruje `IImageClassificationService`, `IModelInfoService` i `IModelHelper` (scoped) oraz wiąże `ImageClassificationOptions`. Nie rejestruje handlerów MediatR — do tego służy skanowanie assembly biblioteki. `RegisterMachineLearningModel` dodaje `PredictionEnginePool` z ML.NET; o tym, skąd ładowany jest model, decyduje builder poola (powyżej `FromFile`). Model rejestruj bez nazwy: predykcje korzystają z domyślnego modelu poola.

Typy znajdują się w przestrzeni nazw `TailoredApps.Shared.MediatR.ImageClassification`, a nie pod nazwą pakietu.

### Konfiguracja `appsettings.json`

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

| Klucz | Znaczenie | Domyślnie |
|-------|-----------|-----------|
| `ModelFilePath` | Plik modelu opisywany przez `IImageClassificationService.GetModelInfo()` (nazwa, checksum, wersja, etykiety); powyższa rejestracja ładuje z niego także model | brak |
| `TrainingRoot` | Katalog, wewnątrz którego muszą leżeć zestawy treningowe; wymagany do treningu | brak |
| `ModelsRoot` | Katalog, wewnątrz którego zapisywane są wytrenowane modele; wymagany do treningu | brak |
| `MaxImageBytes` | Największy obraz przyjmowany przez `ClassifyImage`, w bajtach; `0` wyłącza limit | `10485760` (10 MB) |

Sekcja jest wymagana: bez niej opcje zgłaszają `InvalidOperationException` przy pierwszym użyciu.

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

        var prediction = result.ImagePrediction!;   // handler zawsze ją ustawia

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

### Trening modelu

```csharp
var trainingResult = await _mediator.Send(new TrainImageClassificationModel
{
    Source = "pets",                    // katalog wewnątrz ImageClassification:TrainingRoot, jeden podfolder per klasa
    ModelDestFolderPath = "pets.zip"    // plik modelu wewnątrz ImageClassification:ModelsRoot
});

Console.WriteLine($"Model {trainingResult.ModelPath}, wersja {trainingResult.ModelVersion}");
Console.WriteLine($"Etykiety: {string.Join(", ", trainingResult.Labels)}");
Console.WriteLine(trainingResult.ModelInfo);   // metryki ewaluacji jako tekst
```

Trening czyta pliki z rozszerzeniem `.jpg` lub `.png` z katalogu źródłowego i jego podfolderów; nazwa folderu, w którym leży plik, staje się jego etykietą.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `ClassifyImage` | Command | Dane obrazu do klasyfikacji: `FileByteArray`, `FileName` |
| `ClassifyImageResponse` | Klasa | Wynik: `ImagePrediction` |
| `TrainImageClassificationModel` | Command | Parametry treningu: `Source`, `ModelDestFolderPath` |
| `TrainImageClassificationModelResponse` | Klasa | Wynik treningu: `ModelPath`, `ModelVersion`, `ModelInfo` (metryki ewaluacji jako tekst), `Labels` |
| `IImageClassificationService` | Interfejs | `Predict(byte[], fileName)`, `Train(images, folder, dest)`, `GetModelInfo()` |
| `IPredictionEnginePoolAdapter<TData, TPred>` | Interfejs | Abstrakcja nad ML.NET `PredictionEnginePool` |
| `ModelInfo` | Klasa | Metadane modelu: nazwa, checksum, wersja, etykiety |
| `ImagePrediction` | Klasa | Wynik predykcji: `PredictedLabel`, `PredictedScore`, `FileName`; `Scores` i `ModelInfo` nie są wypełniane przez `ClassifyImage` |
| `InMemoryImageData` | Klasa | Dane wejściowe do modelu: obraz jako `byte[]` |
| `ImagePredictionScore` | Klasa | Surowy wynik modelu: `Score` (per klasa), `PredictedLabel` |
| `ImageClassificationOptions` | Klasa | Opcje wiązane z sekcji `ImageClassification`: `ModelFilePath`, `TrainingRoot`, `ModelsRoot`, `MaxImageBytes` |
| `AddPredictionEngineExtension.AddPredictionEngine` | Metoda ext. | Rejestruje w DI `IImageClassificationService`, `IModelInfoService`, `IModelHelper` i opcje (bez handlerów MediatR) |
| `PredictionEngineServiceConfiguration.RegisterMachineLearningModel<D, R>` | Metoda | Dodaje `PredictionEnginePool<D, R>` z ML.NET i jego adapter; wywoływana wewnątrz `AddPredictionEngine` |

---

## 🔒 Bezpieczeństwo

- **Ścieżki treningu są ograniczone do katalogów z konfiguracji.** `TrainImageClassificationModel.Source` musi wskazywać katalog wewnątrz `ImageClassification:TrainingRoot`, a `ModelDestFolderPath` — plik wewnątrz `ImageClassification:ModelsRoot` (ścieżki względne są rozwiązywane względem tych katalogów). Ścieżka poza korzeniem kończy się `ArgumentException`, a brak konfiguracji korzeni — `InvalidOperationException`. Bez tego żądanie mogło rekurencyjnie czytać dowolny folder hosta i nadpisać dowolny plik.
- **Walidacja obrazu przed dekoderem.** `ClassifyImage` odrzuca (`ArgumentException`) puste dane, pliki większe niż `ImageClassification:MaxImageBytes` (domyślnie 10 MB) i dane bez sygnatury JPEG/PNG.

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
## TailoredApps.Shared.MediatR.ML — Instrukcja dla agenta AI

Używasz TailoredApps.Shared.MediatR.ML do klasyfikacji obrazów przez ML.NET i MediatR.

### Rejestracja
```csharp
// using Microsoft.Extensions.ML;
// using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
// using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).Assembly); // handlery biblioteki
});
builder.Services.AddPredictionEngine(config =>
    config.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(pool =>
        pool.FromFile(builder.Configuration[ImageClassificationOptions.ModelFilePathConfig]!)));
```

### Co jest konfigurowane
- IImageClassificationService, IModelInfoService, IModelHelper (scoped) oraz ImageClassificationOptions
- PredictionEnginePool<InMemoryImageData, ImagePredictionScore> z ML.NET z modelem (domyślnym, bez nazwy)
- Handlery ClassifyImage i TrainImageClassificationModel — wyłącznie przez skan assembly biblioteki w MediatR

### Konfiguracja (sekcja "ImageClassification")
- ModelFilePath — plik modelu (używany do metadanych modelu i przez powyższą rejestrację)
- TrainingRoot, ModelsRoot — katalogi, z których czytane są zestawy treningowe / do których zapisywane są modele; wymagane do treningu
- MaxImageBytes — limit rozmiaru obrazu dla ClassifyImage, domyślnie 10485760 (10 MB), 0 = bez limitu

### Klasyfikacja obrazu
```csharp
var result = await _mediator.Send(new ClassifyImage
{
    FileByteArray = imageBytes,
    FileName = "photo.jpg"
});
// result.ImagePrediction.PredictedLabel  — przewidywana klasa
// result.ImagePrediction.PredictedScore  — pewność (0-1)
```

### Trening modelu
```csharp
var result = await _mediator.Send(new TrainImageClassificationModel
{
    Source = "pets",                   // wewnątrz TrainingRoot; podfoldery = nazwy klas
    ModelDestFolderPath = "pets.zip"   // plik modelu wewnątrz ModelsRoot
});
// result.ModelPath, result.ModelVersion, result.Labels, result.ModelInfo (metryki jako tekst)
```

### Zasady
- Przestrzeń nazw to TailoredApps.Shared.MediatR.ImageClassification, a nie nazwa pakietu
- AddMediatR musi przeskanować assembly biblioteki — AddPredictionEngine nie rejestruje handlerów
- Model musi być plikiem ZIP (ML.NET format)
- Sekcja konfiguracji `ImageClassification` jest wymagana — jej brak powoduje `InvalidOperationException` przy pierwszym użyciu opcji
- Wersja i etykiety modelu są zapisywane w archiwum jako osobne wpisy `Version.txt` i `Labels.txt`
- Folder treningowy: każdy podfolder = jedna klasa, nazwa folderu = etykieta
- PredictionEnginePool jest thread-safe — bezpieczne współbieżne użycie
- PredictedScore ∈ [0,1] — im bliżej 1, tym wyższe zaufanie modelu
- Skonfiguruj ImageClassification:TrainingRoot i ImageClassification:ModelsRoot — trening odrzuca ścieżki poza tymi katalogami
- ClassifyImage przyjmuje wyłącznie JPEG/PNG do MaxImageBytes (domyślnie 10 MB); większe lub obce pliki dają ArgumentException
- Komendę treningu wystawiaj tylko dla administratorów — uruchamia długi proces TensorFlow na wskazanych danych
````
