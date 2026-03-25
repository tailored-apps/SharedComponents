using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.ML.Infrastructure
{
    /// <summary>
    /// Provides utility methods for file and directory operations used in ML pipelines.
    /// </summary>
    internal class FileUtils
    {
        /// <summary>
        /// Loads image file paths and their associated labels from a directory.
        /// </summary>
        /// <param name="folder">The root folder to scan for image files.</param>
        /// <param name="useFolderNameAsLabel">
        /// When <c>true</c>, the parent folder name is used as the label.
        /// When <c>false</c>, the leading alphabetic characters of the file name are used.
        /// </param>
        /// <returns>
        /// An enumerable of tuples containing the full image path and its derived label.
        /// </returns>
        public static IEnumerable<(string ImagePath, string Label)> LoadImagesFromDirectory(string folder, bool useFolderNameAsLabel)
        {

            var imagePath = Directory
                .GetFiles(folder, "*", searchOption: SearchOption.AllDirectories)
                .Where(x => Path.GetExtension(x) == ".jpg" || Path.GetExtension(x) == ".png");
            return useFolderNameAsLabel
                ? imagePath.Select(imagePath => (imagePath, Directory.GetParent(imagePath).Name))
                : imagePath.Select(imagePath =>
                {
                    var label = Path.GetFileName(imagePath);
                    for (var index = 0; index < label.Length; index++)
                    {
                        if (!char.IsLetter(label[index]))
                        {
                            label = label.Substring(0, index);
                            break;
                        }
                    }
                    return (imagePath, label);
                });
        }


        /// <summary>
        /// Resolves a relative path to an absolute path based on the location of the given assembly.
        /// </summary>
        /// <param name="assembly">The assembly whose directory is used as the base path.</param>
        /// <param name="relative">The relative path to resolve.</param>
        /// <returns>The absolute path combining the assembly directory and the relative path.</returns>
        public static string GetAbsolutePath(Assembly assembly, string relative)
        {
            var assemblyFolderPath = new FileInfo(assembly.Location).Directory.FullName;
            return Path.Combine(assemblyFolderPath, relative);
        }
    }
}
