using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TailoredApps.Shared.MediatR.ML.Infrastructure
{
    internal class FileUtils
    {
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


        public static string GetAbsolutePath(Assembly assembly, string relative)
        {
            var assemblyFolderPath = new FileInfo(assembly.Location).Directory.FullName;
            return Path.Combine(assemblyFolderPath, relative);
        }
    }
}
