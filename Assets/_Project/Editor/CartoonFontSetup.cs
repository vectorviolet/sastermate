using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace Sastermate.Editor
{
    /// <summary>Creates the bundled OFL font assets and applies them through one manual Editor action.</summary>
    public static class CartoonFontSetup
    {
        private const string FontFolder = "Assets/_Project/UI/Fonts/Fredoka";
        private const string MenuPath = "Tools/Sastermate/Apply Cartoon UI Fonts";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var regular = GetOrCreateFont("Regular");
            var semibold = GetOrCreateFont("SemiBold");
            if (!regular || !semibold) return;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply cartoon UI fonts");
            var sceneTexts = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
                var changed = 0;
                foreach (var root in scene.GetRootGameObjects()) changed += ApplyToTree(root, regular, semibold, true);
                if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
                sceneTexts += changed;
            }
            Undo.CollapseUndoOperations(undoGroup);

            var prefabTexts = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = ApplyToTree(root, regular, semibold, false);
                    if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabTexts += changed;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CartoonFontSetup] Fredoka applied to {sceneTexts} scene texts and {prefabTexts} prefab texts. Save the open scene to keep its font changes.");
            Selection.activeObject = regular;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanApply() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static TMP_FontAsset GetOrCreateFont(string style)
        {
            var assetPath = $"{FontFolder}/Fredoka-{style} SDF.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset) return asset;

            var sourcePath = $"{FontFolder}/Fredoka-{style}.ttf";
            var importer = AssetImporter.GetAtPath(sourcePath) as TrueTypeFontImporter;
            if (importer && !importer.includeFontData)
            {
                importer.includeFontData = true;
                importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (!source) throw new InvalidOperationException("Bundled font source was not imported: " + sourcePath);
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (!asset) throw new InvalidOperationException("Unity could not create the font asset for " + sourcePath);
            asset.name = "Fredoka-" + style + " SDF";
            AssetDatabase.CreateAsset(asset, assetPath);
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            var characters = new StringBuilder();
            for (var c = 32; c <= 126; c++) characters.Append((char)c);
            characters.Append("ÇçĞğİıÖöŞşÜü°");
            if (!asset.TryAddCharacters(characters.ToString(), out var missing))
                Debug.LogWarning("[CartoonFontSetup] Characters not preloaded: " + missing, asset);
            // A dynamic atlas may create additional textures while preloading.
            foreach (var atlas in asset.atlasTextures)
                if (atlas && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (fallback)
            {
                asset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                asset.fallbackFontAssetTable.Add(fallback);
            }
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            foreach (var atlas in asset.atlasTextures) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static int ApplyToTree(GameObject root, TMP_FontAsset regular, TMP_FontAsset semibold, bool undo)
        {
            var count = 0;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                // Keep package/plugin debug widgets and imported third-party UI out of this project theme.
                var original = PrefabUtility.GetCorrespondingObjectFromSource(text);
                if (original)
                {
                    var path = AssetDatabase.GetAssetPath(original);
                    if (!string.IsNullOrEmpty(path) && !path.StartsWith("Assets/_Project/", StringComparison.Ordinal)) continue;
                }
                var font = IsHeading(text.transform) ? semibold : regular;
                if (text.font == font && text.fontSharedMaterial == font.material) continue;
                if (undo) Undo.RecordObject(text, "Apply cartoon UI font");
                text.font = font;
                text.fontSharedMaterial = font.material;
                EditorUtility.SetDirty(text);
                if (undo && PrefabUtility.IsPartOfPrefabInstance(text))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                count++;
            }
            return count;
        }

        private static bool IsHeading(Transform transform)
        {
            for (var current = transform; current; current = current.parent)
            {
                var name = current.name;
                if (name.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("heading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("header", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
