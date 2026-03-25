using System;
using System.Linq;
using System.Text;

namespace TailoredApps.Shared.MediatR.ImageClassification.Domain.Validation
{
    /// <summary>
    /// Provides extension methods for validating image byte arrays.
    /// </summary>
    public static class ImageValidationExtension
    {
        /// <summary>
        /// Determines whether the given byte array represents a valid image (JPEG or PNG).
        /// </summary>
        /// <param name="image">The byte array to validate.</param>
        /// <returns><c>true</c> if the image is a valid JPEG or PNG; otherwise, <c>false</c>.</returns>
        public static bool IsValidImage(this byte[] image)
        {
            var imageFormat = GetImageFormat(image);
            return imageFormat == ImageFormat.jpeg || imageFormat == ImageFormat.png;
        }

        /// <summary>
        /// Detects the image format of the given byte array by inspecting its file header signature.
        /// </summary>
        /// <param name="bytes">The byte array to inspect.</param>
        /// <returns>The detected <see cref="ImageFormat"/>, or <see cref="ImageFormat.unknown"/> if unrecognised.</returns>
        private static ImageFormat GetImageFormat(byte[] bytes)
        {
            // see http://www.mikekunz.com/image_file_header.html
            var bmp = Encoding.ASCII.GetBytes("BM");     // BMP
            var gif = Encoding.ASCII.GetBytes("GIF");    // GIF
            var png = new byte[] { 137, 80, 78, 71 };    // PNG
            var tiff = new byte[] { 73, 73, 42 };         // TIFF
            var tiff2 = new byte[] { 77, 77, 42 };         // TIFF
            var jpeg = new byte[] { 255, 216, 255, 224 }; // jpeg
            var jpeg2 = new byte[] { 255, 216, 255, 225 }; // jpeg canon


            if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
                return ImageFormat.bmp;

            if (gif.SequenceEqual(bytes.Take(gif.Length)))
                return ImageFormat.gif;

            if (png.SequenceEqual(bytes.Take(png.Length)))
                return ImageFormat.png;

            if (tiff.SequenceEqual(bytes.Take(tiff.Length)))
                return ImageFormat.tiff;

            if (tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
                return ImageFormat.tiff;

            if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
                return ImageFormat.jpeg;

            if (jpeg2.SequenceEqual(bytes.Take(jpeg2.Length)))
                return ImageFormat.jpeg;

            return ImageFormat.unknown;
        }

        /// <summary>
        /// Represents the supported image file formats identified by header byte signatures.
        /// </summary>
        public enum ImageFormat
        {
            /// <summary>Format could not be determined.</summary>
            unknown,
            /// <summary>Windows Bitmap (BMP) format.</summary>
            bmp,
            /// <summary>JPEG / JFIF format.</summary>
            jpeg,
            /// <summary>Graphics Interchange Format (GIF).</summary>
            gif,
            /// <summary>Tagged Image File Format (TIFF).</summary>
            tiff,
            /// <summary>Portable Network Graphics (PNG) format.</summary>
            png
        }
    }
}
