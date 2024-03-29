﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class ImageClassificationOptions
    {
        public const string ConfigurationKey = "ImageClassification";
        public const string ModelFilePathConfig = "ImageClassification:ModelFilePath";
        public string ModelFilePath { get; set; }
        public class ImageClassificationConfigureOptions : IConfigureOptions<ImageClassificationOptions>
        {
            private readonly IConfiguration configuration;
            public ImageClassificationConfigureOptions(IConfiguration configuration)
            {
                this.configuration = configuration;
            }

            public void Configure(ImageClassificationOptions options)
            {
                var section = configuration.GetSection(ImageClassificationOptions.ConfigurationKey).Get<ImageClassificationOptions>();

                options.ModelFilePath = section.ModelFilePath;
            }
        }
    }
}
