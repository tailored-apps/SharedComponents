using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    /// <summary>
    /// Configuration options for the image classification feature, bound from the application configuration.
    /// </summary>
    public class ImageClassificationOptions
    {
        /// <summary>
        /// The configuration section key used to bind <see cref="ImageClassificationOptions"/>.
        /// </summary>
        public const string ConfigurationKey = "ImageClassification";

        /// <summary>
        /// The configuration key path for the model file path setting.
        /// </summary>
        public const string ModelFilePathConfig = "ImageClassification:ModelFilePath";

        /// <summary>Default upper bound for the size of an image submitted for classification (10 MB).</summary>
        public const int DefaultMaxImageBytes = 10 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the file system path to the ML model file.
        /// </summary>
        public string ModelFilePath { get; set; }

        /// <summary>
        /// Gets or sets the directory under which training sets must live. The
        /// <c>TrainImageClassificationModel</c> command only accepts a <c>Source</c> that resolves inside
        /// this directory, so a request cannot make the host enumerate and read arbitrary folders.
        /// Required for training.
        /// </summary>
        public string TrainingRoot { get; set; }

        /// <summary>
        /// Gets or sets the directory under which trained models may be written. The
        /// <c>TrainImageClassificationModel</c> command only accepts a <c>ModelDestFolderPath</c> that
        /// resolves inside this directory, so a request cannot overwrite arbitrary files. Required for training.
        /// </summary>
        public string ModelsRoot { get; set; }

        /// <summary>
        /// Gets or sets the maximum accepted size, in bytes, of an image submitted to <c>ClassifyImage</c>.
        /// Defaults to <see cref="DefaultMaxImageBytes"/>. Set to <c>0</c> to disable the limit.
        /// </summary>
        public int MaxImageBytes { get; set; } = DefaultMaxImageBytes;

        /// <summary>
        /// Implements <see cref="IConfigureOptions{TOptions}"/> to populate <see cref="ImageClassificationOptions"/>
        /// from the application configuration.
        /// </summary>
        public class ImageClassificationConfigureOptions : IConfigureOptions<ImageClassificationOptions>
        {
            private readonly IConfiguration configuration;

            /// <summary>
            /// Initializes a new instance of <see cref="ImageClassificationConfigureOptions"/>.
            /// </summary>
            /// <param name="configuration">The application configuration to read settings from.</param>
            public ImageClassificationConfigureOptions(IConfiguration configuration)
            {
                this.configuration = configuration;
            }

            /// <summary>
            /// Configures the <see cref="ImageClassificationOptions"/> by binding values from the configuration section.
            /// </summary>
            /// <param name="options">The options instance to populate.</param>
            public void Configure(ImageClassificationOptions options)
            {
                var section = configuration.GetSection(ImageClassificationOptions.ConfigurationKey).Get<ImageClassificationOptions>();
                if (section == null)
                {
                    throw new System.InvalidOperationException($"Configuration section '{ImageClassificationOptions.ConfigurationKey}' is missing.");
                }

                options.ModelFilePath = section.ModelFilePath;
                options.TrainingRoot = section.TrainingRoot;
                options.ModelsRoot = section.ModelsRoot;
                options.MaxImageBytes = section.MaxImageBytes;
            }
        }
    }
}
