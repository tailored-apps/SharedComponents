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

        /// <summary>
        /// Gets or sets the file system path to the ML model file.
        /// </summary>
        public string ModelFilePath { get; set; }

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

                options.ModelFilePath = section.ModelFilePath;
            }
        }
    }
}
