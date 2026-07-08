using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    /// <summary>
    /// Provides helper operations for ML model files, including versioning, label management, and checksum computation.
    /// </summary>
    public class ModelHelper : IModelHelper
    {
        /// <summary>The date/time format used when generating model version strings.</summary>
        const string versionFormat = "yyyyMMdd.HHmmss";

        /// <summary>The name of the version entry stored inside the model zip archive.</summary>
        const string versionFileName = "Version.txt";

        /// <summary>The name of the labels entry stored inside the model zip archive.</summary>
        const string labelsFileName = "Labels.txt";

        /// <summary>
        /// Adds a timestamp-based version entry to the model zip archive.
        /// </summary>
        /// <param name="modelFilePath">The file path of the model zip archive to update.</param>
        /// <returns>The generated version string in <c>yyyyMMdd.HHmmss</c> format.</returns>
        public string AddVersion(string modelFilePath)
        {
            string version = DateTime.Now.ToString(versionFormat);
            using (FileStream fs = new FileStream(modelFilePath, FileMode.Open))
            {
                using (ZipArchive ARCHIVE = new ZipArchive(fs, ZipArchiveMode.Update))
                {
                    Stream readmeStream = null;
                    try
                    {
                        readmeStream = ARCHIVE.CreateEntry(versionFileName).Open();
                        using (StreamWriter sw = new StreamWriter(readmeStream))
                        {
                            readmeStream = null;
                            sw.WriteLine(version);
                        }
                    }
                    finally
                    {
                        if (readmeStream != null)
                            readmeStream.Dispose();
                    }

                }
            }
            return version;
        }

        /// <summary>
        /// Adds a pipe-separated list of class labels to the model zip archive.
        /// </summary>
        /// <param name="modelFilePath">The file path of the model zip archive to update.</param>
        /// <param name="labels">An array of label strings to embed in the archive.</param>
        public void AddLabels(string modelFilePath, string[] labels)
        {
            using (FileStream fs = new FileStream(modelFilePath, FileMode.Open))
            {
                using (ZipArchive ARCHIVE = new ZipArchive(fs, ZipArchiveMode.Update))
                {
                    Stream readmeStream = null;
                    try
                    {
                        readmeStream = ARCHIVE.CreateEntry(versionFileName).Open();
                        using (StreamWriter sw = new StreamWriter(readmeStream))
                        {
                            readmeStream = null;
                            sw.WriteLine(string.Join("|", labels));
                        }
                    }
                    finally
                    {
                        if (readmeStream != null)
                            readmeStream.Dispose();
                    }

                }
            }
        }

        /// <summary>
        /// Computes the MD5 checksum of the model file.
        /// </summary>
        /// <param name="modelFilePath">The file path of the model to compute the checksum for.</param>
        /// <returns>A lowercase hexadecimal string representing the MD5 hash of the file.</returns>
        public string GetChecksum(string modelFilePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(modelFilePath);
            var checksum = md5.ComputeHash(stream);
            return BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower();
        }

        /// <summary>
        /// Reads the version string from the model zip archive.
        /// </summary>
        /// <param name="modelFilePath">The file path of the model zip archive.</param>
        /// <returns>
        /// The version string stored in the archive, or <c>"UNKNOWN"</c> if not found or an error occurs.
        /// </returns>
        public string GetVersion(string modelFilePath)
        {
            try
            {
                using FileStream fileStream = new FileStream(modelFilePath, FileMode.Open);
                using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
                ZipArchiveEntry zipArchiveEntry = archive.GetEntry(versionFileName);
                if (zipArchiveEntry != null)
                {
                    using StreamReader streamReader = new StreamReader(zipArchiveEntry.Open());
                    return streamReader.ReadLine();
                }
            }
            catch (Exception)
            {

            }
            return "UNKNOWN";
        }

        /// <summary>
        /// Reads the class labels from the model zip archive.
        /// </summary>
        /// <param name="modelFilePath">The file path of the model zip archive.</param>
        /// <returns>
        /// An array of label strings parsed from the archive, or an empty array if not found or an error occurs.
        /// </returns>
        public string[] GetLabels(string modelFilePath)
        {
            try
            {
                using FileStream fileStream = new FileStream(modelFilePath, FileMode.Open);
                using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
                ZipArchiveEntry zipArchiveEntry = archive.GetEntry(versionFileName);
                if (zipArchiveEntry != null)
                {
                    using StreamReader streamReader = new StreamReader(zipArchiveEntry.Open());
                    return streamReader.ReadLine().Split('|').Select(z => z.Trim()).Where(z => z.Length > 0).ToArray();
                }
            }
            catch (Exception)
            {

            }
            return new string[0];
        }
    }
}
