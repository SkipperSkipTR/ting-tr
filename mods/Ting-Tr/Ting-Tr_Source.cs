using MelonLoader;
using MelonLoader.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

[assembly: MelonInfo(typeof(AssetDownloaderMod.AssetDownloader), "Ting-Tr", "1.0.0", "SkipperSkip")]
[assembly: MelonGame("DrawMeAPixel", "Ting")]

namespace AssetDownloaderMod
{
    public class AssetDownloader : MelonMod
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private string cachePath;
        private bool isInitialized = false;

        // ============================================
        // CONFIGURATION - Edit these values
        // ============================================
        private const string GITHUB_REPO_OWNER = "SkipperSkipTR";
        private const string GITHUB_REPO_NAME = "ting-tr";
        private const string GITHUB_BRANCH = "main";

        // Assets to download - Add your files here
        private readonly List<AssetEntry> hardcodedAssets = new List<AssetEntry>
        {
            new AssetEntry
            {
                Name = "CustomFontTexture",
                GitHubPath = "assets/textures/ChevyRay - Love Bug Extended DMAP SDF Atlas.png",
                TargetPath = "Mods/Textures/ChevyRay - Love Bug Extended DMAP SDF Atlas.png",
                ExpectedHash = "" // Optional: Add SHA256 hash for verification
            },
            new AssetEntry
            {
                Name = "CustomSpriteTexture",
                GitHubPath = "assets/textures/sactx-0-1024x1024-BC7-_LanguageCommon-e8fdcd7e.png",
                TargetPath = "Mods/Textures/sactx-0-1024x1024-BC7-_LanguageCommon-e8fdcd7e.png",
                ExpectedHash = "" // Optional: Add SHA256 hash for verification
            },
            new AssetEntry
            {
                Name = "CustomFont",
                GitHubPath = "assets/fonts/localized_fonts.bundle",
                TargetPath = "Mods/fonts/localized_fonts.bundle",
                ExpectedHash = ""
            },
            new AssetEntry
            {
                Name = "LocalizedText",
                GitHubPath = "assets/Localization.csv",
                TargetPath = "Ting_Data/StreamingAssets/Localization.csv",
                ExpectedHash = ""
            }
        };

        // Optional: Use a version file to check for updates
        private const string VERSION_FILE_PATH = "version.json"; // Path in your GitHub repo
        private const bool CHECK_FOR_UPDATES = true;
        // ============================================

        public override void OnInitializeMelon()
        {
            // Set up paths
            string modFolder = Path.Combine(MelonEnvironment.UserDataDirectory, "TR-Yama");
            cachePath = Path.Combine(modFolder, "cache");

            // Create directories
            Directory.CreateDirectory(modFolder);
            Directory.CreateDirectory(cachePath);

            // Configure HttpClient
            httpClient.DefaultRequestHeaders.Add("User-Agent", "MelonLoader-AssetDownloader");
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            // Blocking call, downloads all assets before continuing
            Task.Run(async () =>
            {
                await InitializeAssetsSync();
            }).GetAwaiter().GetResult(); // <- Block here

            LoggerInstance.Msg("Asset Downloader initialized!");
            LoggerInstance.Msg($"GitHub Repo: {GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}");
        }

        private async Task InitializeAssetsSync()
        {
            List<AssetEntry> assetsToDownload = hardcodedAssets;

            // Optional version check
            if (CHECK_FOR_UPDATES && !string.IsNullOrEmpty(VERSION_FILE_PATH))
            {
                var versionInfo = await CheckVersion();
                if (versionInfo?.Assets != null && versionInfo.Assets.Count > 0)
                    assetsToDownload = versionInfo.Assets;
            }

            foreach (var asset in assetsToDownload)
                await ProcessAsset(asset); // await here ensures completion

            isInitialized = true;
            LoggerInstance.Msg("Asset initialization complete! All files are ready.");
        }

        private IEnumerator InitializeAssets()
        {
            LoggerInstance.Msg("Starting asset initialization...");

            List<AssetEntry> assetsToDownload = new List<AssetEntry>(hardcodedAssets);

            // Check for updates if enabled
            if (CHECK_FOR_UPDATES && !string.IsNullOrEmpty(VERSION_FILE_PATH))
            {
                Task<VersionInfo> versionTask = CheckVersion();
                while (!versionTask.IsCompleted)
                {
                    yield return null;
                }

                if (versionTask.Result != null)
                {
                    LoggerInstance.Msg($"Remote version: {versionTask.Result.Version}");

                    // You can add logic here to update asset list based on version info
                    if (versionTask.Result.Assets != null && versionTask.Result.Assets.Count > 0)
                    {
                        LoggerInstance.Msg("Using asset list from version file");
                        assetsToDownload = versionTask.Result.Assets;
                    }
                }
            }

            // Download and verify all assets
            foreach (var asset in assetsToDownload)
            {
                Task downloadTask = ProcessAsset(asset);
                while (!downloadTask.IsCompleted)
                {
                    yield return null;
                }
            }

            isInitialized = true;
            LoggerInstance.Msg("Asset initialization complete! All files are ready.");
        }

        private async Task<VersionInfo> CheckVersion()
        {
            try
            {
                string versionUrl = GetGitHubRawUrl(VERSION_FILE_PATH);
                LoggerInstance.Msg($"Checking version at: {versionUrl}");

                var response = await httpClient.GetAsync(versionUrl);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(json);

                return versionInfo;
            }
            catch (Exception e)
            {
                LoggerInstance.Warning($"Could not check version: {e.Message}");
                return null;
            }
        }

        private async Task ProcessAsset(AssetEntry asset)
        {
            try
            {
                string cacheFilePath = Path.Combine(cachePath, SanitizeFileName(asset.Name));
                string targetPath = GetFullTargetPath(asset.TargetPath);

                bool needsDownload = true;

                // Check if cached file exists and is valid
                if (File.Exists(cacheFilePath))
                {
                    if (!string.IsNullOrEmpty(asset.ExpectedHash))
                    {
                        string cachedHash = ComputeFileHash(cacheFilePath);
                        if (cachedHash == asset.ExpectedHash)
                        {
                            needsDownload = false;
                            LoggerInstance.Msg($"✓ Using cached: {asset.Name}");
                        }
                        else
                        {
                            LoggerInstance.Msg($"⟳ Hash mismatch for {asset.Name}, re-downloading...");
                        }
                    }
                    else
                    {
                        needsDownload = false;
                        LoggerInstance.Msg($"✓ Using cached: {asset.Name}");
                    }
                }

                // Download if needed
                if (needsDownload)
                {
                    string downloadUrl = GetGitHubRawUrl(asset.GitHubPath);
                    LoggerInstance.Msg($"Downloading: {asset.Name}");
                    LoggerInstance.Msg($"  URL: {downloadUrl}");

                    await DownloadFile(downloadUrl, cacheFilePath);

                    // Verify hash after download
                    if (!string.IsNullOrEmpty(asset.ExpectedHash))
                    {
                        string downloadedHash = ComputeFileHash(cacheFilePath);
                        if (downloadedHash != asset.ExpectedHash)
                        {
                            LoggerInstance.Error($"Hash verification failed for {asset.Name}!");
                            LoggerInstance.Error($"  Expected: {asset.ExpectedHash}");
                            LoggerInstance.Error($"  Got: {downloadedHash}");
                            File.Delete(cacheFilePath);
                            return;
                        }
                        LoggerInstance.Msg($"Hash verified for {asset.Name}");
                    }

                    LoggerInstance.Msg($"Downloaded: {asset.Name}");
                }

                // Copy to target location
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(cacheFilePath, targetPath, true);
                LoggerInstance.Msg($"Installed to: {asset.TargetPath}");
            }
            catch (Exception e)
            {
                LoggerInstance.Error($"Failed to process {asset.Name}: {e.Message}");
                LoggerInstance.Error($"  Stack trace: {e.StackTrace}");
            }
        }

        private string GetGitHubRawUrl(string filePath)
        {
            // Use raw.githubusercontent.com for direct file access
            return $"https://raw.githubusercontent.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/{GITHUB_BRANCH}/{filePath}";
        }

        private async Task DownloadFile(string url, string destinationPath)
        {
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long? contentLength = response.Content.Headers.ContentLength;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (contentLength.HasValue && totalRead % (1024 * 1024) == 0) // Log every MB
                        {
                            double progress = (double)totalRead / contentLength.Value * 100;
                            LoggerInstance.Msg($"  Progress: {progress:F1}% ({totalRead / 1024} KB / {contentLength.Value / 1024} KB)");
                        }
                    }
                }
            }
        }

        private string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private string GetFullTargetPath(string relativePath)
        {
            // Get the game's root directory
            string gameRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(gameRoot, relativePath);
        }

        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        public override void OnLateInitializeMelon()
        {
            if (!isInitialized)
            {
                LoggerInstance.Warning("Assets may not be fully loaded yet!");
            }
        }

        public override void OnApplicationQuit()
        {
            httpClient?.Dispose();
        }
    }

    [Serializable]
    public class AssetEntry
    {
        public string Name { get; set; }
        public string GitHubPath { get; set; } // Path in the GitHub repo
        public string TargetPath { get; set; } // Where to install in the game
        public string ExpectedHash { get; set; } // Optional SHA256 hash
    }

    [Serializable]
    public class VersionInfo
    {
        public string Version { get; set; }
        public List<AssetEntry> Assets { get; set; }
    }
}
