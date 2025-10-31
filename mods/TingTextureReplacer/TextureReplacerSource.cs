using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[assembly: MelonInfo(typeof(TingTextureReplacer.Core), "TingTextureReplacer", "1.0.0", "SkipperSkip", null)]
[assembly: MelonGame("DrawMeAPixel", "Ting")]

namespace TingTextureReplacer
{
    public class Core : MelonMod
    {
        private static string texturesFolder;
        private static Dictionary<string, byte[]> replacementTexturesByName = new Dictionary<string, byte[]>();
        private static Dictionary<int, byte[]> replacementTexturesByPathID = new Dictionary<int, byte[]>();
        private static HashSet<int> patchedTextureInstances = new HashSet<int>();
        private static bool isScanning = true;
        private float scanTimer = 0f;
        private const float scanDuration = 10f;

        public override void OnInitializeMelon()
        {
            // Create textures folder in the mod's directory
            texturesFolder = Path.Combine(MelonEnvironment.ModsDirectory, "textures");

            if (!Directory.Exists(texturesFolder))
            {
                Directory.CreateDirectory(texturesFolder);
                LoggerInstance.Msg($"Created texture replacement folder at: {texturesFolder}");
                LoggerInstance.Msg("You can name files either:");
                LoggerInstance.Msg("  - By texture name: TextureName.png");
                LoggerInstance.Msg("  - By UABE Path ID: 12345.png");
            }

            LoadReplacementTextures();
            LoggerInstance.Msg($"Texture Replacer initialized!");
            LoggerInstance.Msg($"Loaded {replacementTexturesByName.Count} name-based replacements");
            LoggerInstance.Msg($"Loaded {replacementTexturesByPathID.Count} PathID-based replacements");
        }

        private void LoadReplacementTextures()
        {
            if (!Directory.Exists(texturesFolder))
                return;

            string[] imageFiles = Directory.GetFiles(texturesFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string filePath in imageFiles)
            {
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    byte[] fileData = File.ReadAllBytes(filePath);

                    // Check if the filename is a number (PathID from UABE)
                    if (int.TryParse(fileName, out int pathID))
                    {
                        replacementTexturesByPathID[pathID] = fileData;
                        LoggerInstance.Msg($"Loaded replacement for PathID: {pathID}");
                    }
                    else
                    {
                        // It's a texture name
                        replacementTexturesByName[fileName] = fileData;
                        LoggerInstance.Msg($"Loaded replacement for texture name: {fileName}");
                    }
                }
                catch (System.Exception e)
                {
                    LoggerInstance.Error($"Error loading texture {filePath}: {e.Message}");
                }
            }
        }

        public override void OnUpdate()
        {
            if (!isScanning)
                return;

            scanTimer += Time.deltaTime;

            if (scanTimer >= scanDuration)
            {
                isScanning = false;
                LoggerInstance.Msg("Texture scanning completed.");
                return;
            }

            ScanAndReplaceTextures();
        }

        private void ScanAndReplaceTextures()
        {
            Texture2D[] allTextures = Resources.FindObjectsOfTypeAll<Texture2D>();

            foreach (Texture2D texture in allTextures)
            {
                if (texture == null)
                    continue;

                int instanceID = texture.GetInstanceID();

                // Skip if already patched
                if (patchedTextureInstances.Contains(instanceID))
                    continue;

                byte[] replacementData = null;
                string matchType = "";

                // Try to match by PathID first (instance ID in Unity corresponds to PathID)
                // Note: GetInstanceID() returns a unique runtime ID, but we can try matching
                // You might need to log instance IDs first to map them to UABE PathIDs
                if (replacementTexturesByPathID.ContainsKey(instanceID))
                {
                    replacementData = replacementTexturesByPathID[instanceID];
                    matchType = $"PathID {instanceID}";
                }
                // Try to match by name
                else if (!string.IsNullOrEmpty(texture.name) && replacementTexturesByName.ContainsKey(texture.name))
                {
                    replacementData = replacementTexturesByName[texture.name];
                    matchType = $"name '{texture.name}'";
                }

                // If we found a replacement, apply it
                if (replacementData != null)
                {
                    try
                    {
                        bool success = texture.LoadImage(replacementData);

                        if (success)
                        {
                            patchedTextureInstances.Add(instanceID);
                            LoggerInstance.Msg($"Replaced texture by {matchType} (InstanceID: {instanceID})");
                        }
                    }
                    catch (System.Exception e)
                    {
                        LoggerInstance.Error($"Error replacing texture {matchType}: {e.Message}");
                        patchedTextureInstances.Add(instanceID);
                    }
                }
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            isScanning = true;
            scanTimer = 0f;
            LoggerInstance.Msg($"Scene loaded: {sceneName}. Scanning for textures...");
        }

        public override void OnLateUpdate()
        {
            // F5: Manual scan and log all texture info
            if (Input.GetKeyDown(KeyCode.F5))
            {
                LoggerInstance.Msg("=== Manual Texture Scan ===");
                Texture2D[] allTextures = Resources.FindObjectsOfTypeAll<Texture2D>();

                LoggerInstance.Msg($"Found {allTextures.Length} textures in memory:");
                foreach (Texture2D texture in allTextures)
                {
                    if (texture != null)
                    {
                        int instanceID = texture.GetInstanceID();
                        string name = string.IsNullOrEmpty(texture.name) ? "<no name>" : texture.name;
                        LoggerInstance.Msg($"  InstanceID: {instanceID}, Name: {name}, Size: {texture.width}x{texture.height}");
                    }
                }

                isScanning = true;
                scanTimer = 0f;
                ScanAndReplaceTextures();
            }

            // F6: Reload replacement textures
            if (Input.GetKeyDown(KeyCode.F6))
            {
                LoggerInstance.Msg("Reloading replacement textures...");
                replacementTexturesByName.Clear();
                replacementTexturesByPathID.Clear();
                patchedTextureInstances.Clear();
                LoadReplacementTextures();
                isScanning = true;
                scanTimer = 0f;
            }
        }
    }
}