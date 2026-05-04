using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using System;
using System.IO;

namespace TingTR_Installer
{
    public static class ArchiveHelper
    {
        public static void ExtractArchive(string archivePath, string extractPath)
        {
            Directory.CreateDirectory(extractPath);

            using (IArchive archive = ArchiveFactory.Open(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        string entryDestinationPath = Path.Combine(extractPath, entry.Key);
                        string destinationDirectory = Path.GetDirectoryName(entryDestinationPath);
                        if (!string.IsNullOrEmpty(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }

                        using (Stream stream = entry.OpenEntryStream())
                        using (FileStream fileStream = new FileStream(entryDestinationPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                }
            }
        }

        public static void ExtractArchiveEntry(string archivePath, string extractPath, string entryName)
        {
            using (IArchive archive = ArchiveFactory.Open(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory && entry.Key.Equals(entryName, StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(extractPath);
                        using (Stream stream = entry.OpenEntryStream())
                        using (FileStream fileStream = new FileStream(Path.Combine(extractPath, entryName), FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fileStream);
                        }
                        return;
                    }
                }
            }
        }
    }
}
