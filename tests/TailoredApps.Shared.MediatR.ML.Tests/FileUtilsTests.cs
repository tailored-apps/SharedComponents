using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TailoredApps.Shared.MediatR.ML.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    /// <summary>
    /// Tests for the internal <c>FileUtils</c> class. The source assembly declares no
    /// InternalsVisibleTo, so the static methods are invoked through reflection.
    /// </summary>
    public class FileUtilsTests : IDisposable
    {
        private static readonly Type FileUtilsType = typeof(PredictionEngineServiceConfiguration).Assembly
            .GetType("TailoredApps.Shared.MediatR.ML.Infrastructure.FileUtils", throwOnError: true);

        private readonly string rootFolder;

        public FileUtilsTests()
        {
            rootFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootFolder);
        }

        public void Dispose()
        {
            if (Directory.Exists(rootFolder))
            {
                Directory.Delete(rootFolder, recursive: true);
            }
        }

        private static IEnumerable<(string ImagePath, string Label)> LoadImagesFromDirectory(string folder, bool useFolderNameAsLabel)
        {
            var method = FileUtilsType.GetMethod("LoadImagesFromDirectory", BindingFlags.Public | BindingFlags.Static);
            try
            {
                return (IEnumerable<(string ImagePath, string Label)>)method.Invoke(null, new object[] { folder, useFolderNameAsLabel });
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }

        private static string GetAbsolutePath(Assembly assembly, string relative)
        {
            var method = FileUtilsType.GetMethod("GetAbsolutePath", BindingFlags.Public | BindingFlags.Static);
            return (string)method.Invoke(null, new object[] { assembly, relative });
        }

        private string CreateFile(string relativePath)
        {
            var fullPath = Path.Combine(rootFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, new byte[] { 1 });
            return fullPath;
        }

        [Fact]
        public void When_UseFolderNameAsLabel_Is_True_Should_Label_Images_With_Parent_Folder_Name()
        {
            // arrange
            var redJpg = CreateFile(Path.Combine("red", "1.jpg"));
            var greenPng = CreateFile(Path.Combine("green", "1.png"));

            // act
            var result = LoadImagesFromDirectory(rootFolder, useFolderNameAsLabel: true).ToList();

            // assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, x => x.ImagePath == redJpg && x.Label == "red");
            Assert.Contains(result, x => x.ImagePath == greenPng && x.Label == "green");
        }

        [Fact]
        public void When_Directory_Contains_Non_Image_Files_Should_Ignore_Them()
        {
            // arrange
            var jpg = CreateFile(Path.Combine("red", "photo.jpg"));
            CreateFile(Path.Combine("red", "notes.txt"));
            CreateFile(Path.Combine("red", "raw.bmp"));
            CreateFile(Path.Combine("red", "noextension"));

            // act
            var result = LoadImagesFromDirectory(rootFolder, useFolderNameAsLabel: true).ToList();

            // assert
            var single = Assert.Single(result);
            Assert.Equal(jpg, single.ImagePath);
        }

        [Fact]
        public void When_Extension_Casing_Differs_Should_Ignore_The_File()
        {
            // arrange
            // Pins current behavior: the extension comparison is ordinal and case
            // sensitive, so "UPPER.JPG" is not treated as an image.
            CreateFile(Path.Combine("red", "UPPER.JPG"));
            var lowerCase = CreateFile(Path.Combine("red", "lower.jpg"));

            // act
            var result = LoadImagesFromDirectory(rootFolder, useFolderNameAsLabel: true).ToList();

            // assert
            var single = Assert.Single(result);
            Assert.Equal(lowerCase, single.ImagePath);
        }

        [Fact]
        public void When_UseFolderNameAsLabel_Is_False_Should_Use_Leading_Letters_Of_File_Name_As_Label()
        {
            // arrange
            var catImage = CreateFile(Path.Combine("animals", "cat1.jpg"));

            // act
            var result = LoadImagesFromDirectory(rootFolder, useFolderNameAsLabel: false).ToList();

            // assert
            var single = Assert.Single(result);
            Assert.Equal(catImage, single.ImagePath);
            Assert.Equal("cat", single.Label);
        }

        [Fact]
        public void When_UseFolderNameAsLabel_Is_False_And_File_Name_Starts_With_Digit_Should_Return_Empty_Label()
        {
            // arrange
            CreateFile(Path.Combine("red", "1.png"));

            // act
            var result = LoadImagesFromDirectory(rootFolder, useFolderNameAsLabel: false).ToList();

            // assert
            var single = Assert.Single(result);
            Assert.Equal(string.Empty, single.Label);
        }

        [Fact]
        public void When_Folder_Does_Not_Exist_Should_Throw_DirectoryNotFoundException()
        {
            // arrange
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            // act & assert
            Assert.Throws<DirectoryNotFoundException>(() => LoadImagesFromDirectory(missing, useFolderNameAsLabel: true));
        }

        [Fact]
        public void When_GetAbsolutePath_Is_Called_Should_Combine_Assembly_Directory_With_Relative_Path()
        {
            // arrange
            var assembly = typeof(FileUtilsTests).Assembly;
            var expectedFolder = new FileInfo(assembly.Location).Directory.FullName;

            // act
            var result = GetAbsolutePath(assembly, Path.Combine("TestData", "model.zip"));

            // assert
            Assert.Equal(Path.Combine(expectedFolder, "TestData", "model.zip"), result);
        }
    }
}
