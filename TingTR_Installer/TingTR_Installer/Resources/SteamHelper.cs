using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TingTR_Installer
{
    public static class SteamHelper
    {
        public static string GetSteamPathFromRegistry()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (key != null)
                {
                    object path = key.GetValue("SteamPath");
                    if (path != null)
                        return path.ToString().Replace('/', '\\');
                }
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object path = key.GetValue("SteamPath");
                        if (path != null)
                            return path.ToString().Replace('/', '\\');
                    }
                }
            }
            catch { }

            return null;
        }

        public static List<string> GetSteamLibraryFolders(string steamPath)
        {
            var folders = new List<string>();

            if (string.IsNullOrEmpty(steamPath))
                return folders;

            folders.Add(steamPath);

            string libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersVdf))
                return folders;

            try
            {
                string[] lines = File.ReadAllLines(libraryFoldersVdf);

                bool insideLibraryBlock = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (Regex.IsMatch(line, "^\"\\d+\"$"))
                    {
                        if (i + 1 < lines.Length && lines[i + 1].Trim() == "{")
                        {
                            insideLibraryBlock = true;
                            i++;
                            continue;
                        }
                    }

                    if (insideLibraryBlock)
                    {
                        if (line == "}")
                        {
                            insideLibraryBlock = false;
                            continue;
                        }

                        var match = Regex.Match(line, "^\"path\"\\s+\"(.+)\"$");
                        if (match.Success)
                        {
                            string path = match.Groups[1].Value.Replace(@"\\", @"\");
                            if (Directory.Exists(path) && !folders.Contains(path))
                                folders.Add(path);
                        }
                    }
                }
            }
            catch { }

            return folders;
        }

        public static string FindGameFolderInSteamLibraries(List<string> libraries, string gameExeName)
        {
            foreach (var lib in libraries)
            {
                string steamAppsPath = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamAppsPath))
                    continue;

                string commonPath = Path.Combine(steamAppsPath, "common");
                if (!Directory.Exists(commonPath))
                    continue;

                foreach (var folder in Directory.GetDirectories(commonPath))
                {
                    string exePath = Path.Combine(folder, gameExeName);
                    if (File.Exists(exePath))
                    {
                        return folder;
                    }
                }
            }
            return null;
        }

        public static string FindGamePath()
        {
            string steamPath = GetSteamPathFromRegistry();
            if (string.IsNullOrEmpty(steamPath))
                return null;

            List<string> libraryFolders = GetSteamLibraryFolders(steamPath);

            string gamePath = FindGameFolderInSteamLibraries(libraryFolders, "Ting.exe");
            if (!string.IsNullOrEmpty(gamePath))
                return gamePath;

            return null;
        }
    }
}
