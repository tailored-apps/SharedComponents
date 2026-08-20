using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using Xunit;

namespace TailoredApps.Shared.MediatR.ML.Tests
{
    public class ModelHelperTests : IDisposable
    {
        private readonly ModelHelper sut = new ModelHelper();
        private readonly string workFolder;

        public ModelHelperTests()
        {
            workFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workFolder);
        }

        public void Dispose()
        {
            if (Directory.Exists(workFolder))
            {
                Directory.Delete(workFolder, recursive: true);
            }
        }

        private string CreateEmptyZip()
        {
            var path = Path.Combine(workFolder, Guid.NewGuid().ToString("N") + ".zip");
            using var fileStream = new FileStream(path, FileMode.CreateNew);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
            return path;
        }

        [Fact]
        public void When_GetChecksum_Is_Called_Should_Return_Lower_Case_Md5_Of_File_Content()
        {
            // arrange
            var path = Path.Combine(workFolder, "checksum.bin");
            File.WriteAllText(path, "hello");

            // act
            var checksum = sut.GetChecksum(path);

            // assert
            Assert.Equal("5d41402abc4b2a76b9719d911017c592", checksum);
        }

        [Fact]
        public void When_AddVersion_Is_Called_Should_Return_Timestamp_Version_And_Store_It_In_Archive()
        {
            // arrange
            var path = CreateEmptyZip();

            // act
            var version = sut.AddVersion(path);

            // assert
            Assert.Matches(new Regex(@"^\d{8}\.\d{6}$"), version);
            Assert.Equal(version, sut.GetVersion(path));
        }

        [Fact]
        public void When_GetVersion_Is_Called_On_Archive_Without_Version_Entry_Should_Return_Unknown()
        {
            // arrange
            var path = CreateEmptyZip();

            // act
            var version = sut.GetVersion(path);

            // assert
            Assert.Equal("UNKNOWN", version);
        }

        [Fact]
        public void When_GetVersion_Is_Called_On_Missing_File_Should_Return_Unknown()
        {
            // arrange
            var path = Path.Combine(workFolder, "does-not-exist.zip");

            // act
            var version = sut.GetVersion(path);

            // assert
            Assert.Equal("UNKNOWN", version);
        }

        [Fact]
        public void When_GetVersion_Is_Called_On_File_That_Is_Not_A_Zip_Should_Return_Unknown()
        {
            // arrange
            var path = Path.Combine(workFolder, "not-a-zip.zip");
            File.WriteAllText(path, "plain text, not a zip archive");

            // act
            var version = sut.GetVersion(path);

            // assert
            Assert.Equal("UNKNOWN", version);
        }

        [Fact]
        public void When_GetLabels_Is_Called_On_Archive_Without_Labels_Entry_Should_Return_Empty_Array()
        {
            // arrange
            var path = CreateEmptyZip();

            // act
            var labels = sut.GetLabels(path);

            // assert
            Assert.Empty(labels);
        }

        [Fact]
        public void When_GetLabels_Is_Called_On_Missing_File_Should_Return_Empty_Array()
        {
            // arrange
            var path = Path.Combine(workFolder, "does-not-exist.zip");

            // act
            var labels = sut.GetLabels(path);

            // assert
            Assert.Empty(labels);
        }

        [Fact]
        public void When_AddLabels_Is_Called_Should_Store_Labels_Readable_By_GetLabels()
        {
            // arrange
            var path = CreateEmptyZip();

            // act
            sut.AddLabels(path, new[] { "red", "green", "blue" });

            // assert
            Assert.Equal(new[] { "red", "green", "blue" }, sut.GetLabels(path));
        }

        [Fact]
        public void When_AddLabels_Is_Called_Should_Not_Affect_Version_Entry()
        {
            // arrange
            var path = CreateEmptyZip();

            // act
            sut.AddLabels(path, new[] { "red", "green" });

            // assert
            Assert.Equal("UNKNOWN", sut.GetVersion(path));
        }

        [Fact]
        public void When_AddVersion_Is_Followed_By_AddLabels_Should_Keep_Version_And_Labels_Independent()
        {
            // arrange
            var path = CreateEmptyZip();

            // act
            var version = sut.AddVersion(path);
            sut.AddLabels(path, new[] { "red", "green" });

            // assert
            Assert.Equal(version, sut.GetVersion(path));
            Assert.Equal(new[] { "red", "green" }, sut.GetLabels(path));
        }
    }
}
