using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Shared.MediatR.ImageClassification.Infrastructure
{
    public class ModelHelper : IModelHelper
    {
        const string versionFormat = "yyyyMMdd.HHmmss";
        const string versionFileName = "Version.txt";
        const string labelsFileName = "Labels.txt";

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
        public  void AddLabels(string modelFilePath, string[] labels)
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
                            sw.WriteLine(string.Join("|",labels));
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

        public string GetChecksum(string modelFilePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(modelFilePath);
            var checksum = md5.ComputeHash(stream);
            return BitConverter.ToString(checksum).Replace("-", string.Empty).ToLower();
        }

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
                    return streamReader.ReadLine().Split('|').Select(z=>z.Trim()).Where(z=>z.Length>0).ToArray();
                }
            }
            catch (Exception)
            {

            }
            return new string[0];
        }
    }
}
