using HarmonyLib;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[assembly: MelonInfo(typeof(TingFontReplacer.Core), "TingFontReplacer", "1.7.0", "SkipperSkip", null)]
[assembly: MelonGame("DrawMeAPixel", "Ting")]

namespace TingFontReplacer
{
    public class Core : MelonMod
    {
        // -------------------- Font Logic --------------------
        private static TMP_FontAsset replacementFont;
        private static string targetFontName = "ChevyRay_-_Love_Bug_Regular_02 SDF";

        // -------------------- Texture Logic --------------------
        private static string texturesPath = Path.Combine(MelonEnvironment.ModsDirectory, "textures");

        public override void OnLateInitializeMelon()
        {
            // --- Load font bundle ---
            string fontsBundlePath = Path.Combine(MelonEnvironment.ModsDirectory, "fonts", "localized_fonts.bundle");
            if (File.Exists(fontsBundlePath))
            {
                var bundle = Il2CppAssetBundleManager.LoadFromFile(fontsBundlePath);
                if (bundle != null)
                {
                    replacementFont = bundle.LoadAsset<TMP_FontAsset>(targetFontName);
                    if (replacementFont != null)
                    {
                        LoggerInstance.Msg($"[FontSwap] Loaded replacement font: {replacementFont.name}");
                        ReplaceAllMatchingFonts();

                        HarmonyInstance.Patch(
                            original: AccessTools.Method(typeof(TMP_FontAsset), nameof(TMP_FontAsset.Awake)),
                            postfix: new HarmonyMethod(typeof(Core), nameof(AwakePostfix))
                        );
                        LoggerInstance.Msg("[FontSwap] Harmony patch applied to TMP_FontAsset.Awake.");
                    }
                    else
                        LoggerInstance.Error($"[FontSwap] Replacement font '{targetFontName}' not found in AssetBundle!");
                }
                else
                    LoggerInstance.Error("[FontSwap] Failed to load AssetBundle!");
            }
            else
                LoggerInstance.Error($"[FontSwap] Font AssetBundle not found: {fontsBundlePath}");
        }

        // -------------------- Font Methods --------------------
        private static void ReplaceAllMatchingFonts()
        {
            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var font in allFonts)
            {
                if (font != null)
                {
                    MelonLogger.Msg($"[FontSwap] Replacing preloaded font: {font.name}");
                    ReplaceFontData(font, replacementFont);
                }
            }
        }

        private static void AwakePostfix(TMP_FontAsset __instance)
        {
            if (__instance != null && __instance.name == "ChevyRay_-_Love_Bug_Regular_02 SDF" && replacementFont != null)
            {
                MelonLogger.Msg($"[FontSwap] Late-loaded font detected: {__instance.name}");
                ReplaceFontData(__instance, replacementFont);
            }
        }

        private static void ReplaceFontData(TMP_FontAsset target, TMP_FontAsset replacement)
        {
            if (target == null || replacement == null)
                return;

            var originalMaterial = target.material;

            target.faceInfo = replacement.faceInfo;

            target.glyphTable.Clear();
            foreach (var g in replacement.glyphTable)
                target.glyphTable.Add(g);

            target.characterTable.Clear();
            foreach (var c in replacement.characterTable)
                target.characterTable.Add(c);

            target.atlasPopulationMode = replacement.atlasPopulationMode;
            target.atlasTextures = replacement.atlasTextures;
            target.fallbackFontAssetTable = replacement.fallbackFontAssetTable;

            if (originalMaterial != null)
            {
                originalMaterial.mainTexture = replacement.material.mainTexture;
                target.material = originalMaterial;
            }
            else
            {
                target.material = replacement.material;
            }

            target.materialHashCode = replacement.materialHashCode;
            target.m_CharacterLookupDictionary = replacement.m_CharacterLookupDictionary;
            target.m_GlyphLookupDictionary = replacement.m_GlyphLookupDictionary;

            MelonLogger.Msg($"[FontSwap] Font data replaced successfully for: {target.name}");
        }
    }
}
