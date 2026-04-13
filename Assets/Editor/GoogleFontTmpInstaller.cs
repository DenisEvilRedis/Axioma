using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class GoogleFontTmpInstaller
{
    private const string FontsRoot = "Assets/Fonts/Google";
    private const string TmpRoot = "Assets/Fonts/Google/TMP";
    private const string CharacterSet =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "«»…“”„№–—ЁАБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
        "абвгдежзийклмнопрстуфхцчшщъыьэюяё";
    [MenuItem("Tools/Axioma/Generate TMP Google Fonts")]
    public static void Generate()
    {
        EnsureFolder("Assets/Fonts");
        EnsureFolder(FontsRoot);
        EnsureFolder(TmpRoot);

        TMP_FontAsset russoOne = RebuildFontAsset(
            "Assets/Fonts/Google/RussoOne/RussoOne-Regular.ttf",
            "Assets/Fonts/Google/TMP/Russo One TMP.asset",
            96,
            9);

        TMP_FontAsset ibmRegular = RebuildFontAsset(
            "Assets/Fonts/Google/IBMPlexSansCondensed/IBMPlexSansCondensed-Regular.ttf",
            "Assets/Fonts/Google/TMP/IBM Plex Sans Condensed Regular TMP.asset",
            72,
            8);

        TMP_FontAsset ibmMedium = RebuildFontAsset(
            "Assets/Fonts/Google/IBMPlexSansCondensed/IBMPlexSansCondensed-Medium.ttf",
            "Assets/Fonts/Google/TMP/IBM Plex Sans Condensed Medium TMP.asset",
            72,
            8);

        TMP_FontAsset ibmSemiBold = RebuildFontAsset(
            "Assets/Fonts/Google/IBMPlexSansCondensed/IBMPlexSansCondensed-SemiBold.ttf",
            "Assets/Fonts/Google/TMP/IBM Plex Sans Condensed SemiBold TMP.asset",
            72,
            8);

        ConfigureFallbacks(ibmRegular, ibmMedium, ibmSemiBold, russoOne);
        ConfigureFallbacks(russoOne, ibmRegular);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Axioma] Google Fonts imported and TMP assets generated.");
    }

    [MenuItem("Tools/Axioma/Repair TMP Google Fonts")]
    public static void RepairInstalledFonts()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { TmpRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset == null)
            {
                continue;
            }

            EnsureFontAssetSubAssets(fontAsset, assetPath);
            EditorUtility.SetDirty(fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static TMP_FontAsset RebuildFontAsset(string fontPath, string assetPath, int samplingPointSize, int padding)
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (sourceFont == null)
        {
            throw new FileNotFoundException($"Source font not found: {fontPath}");
        }

        TMP_FontAsset existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existingAsset != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize,
            padding,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(fontAsset, assetPath);
        fontAsset.TryAddCharacters(CharacterSet, out _);
        EnsureFontAssetSubAssets(fontAsset, assetPath);
        EditorUtility.SetDirty(fontAsset);
        return fontAsset;
    }

    private static void EnsureFontAssetSubAssets(TMP_FontAsset fontAsset, string assetPath)
    {
        if (fontAsset == null)
        {
            return;
        }

        Texture2D atlasTexture = null;
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
        {
            atlasTexture = fontAsset.atlasTextures[0];
        }

        if (atlasTexture == null)
        {
            atlasTexture = new Texture2D(fontAsset.atlasWidth, fontAsset.atlasHeight, TextureFormat.Alpha8, false)
            {
                name = $"{fontAsset.name} Atlas"
            };
        }
        else if (string.IsNullOrWhiteSpace(atlasTexture.name))
        {
            atlasTexture.name = $"{fontAsset.name} Atlas";
        }

        if (AssetDatabase.GetAssetPath(atlasTexture) != assetPath)
        {
            AssetDatabase.AddObjectToAsset(atlasTexture, assetPath);
        }

        Material material = fontAsset.material;
        if (material == null)
        {
            Shader shader = Shader.Find("TextMeshPro/Distance Field");
            material = new Material(shader)
            {
                name = $"{fontAsset.name} Material"
            };
            fontAsset.material = material;
        }

        material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
        material.SetFloat(ShaderUtilities.ID_TextureWidth, atlasTexture.width);
        material.SetFloat(ShaderUtilities.ID_TextureHeight, atlasTexture.height);
        material.SetFloat(ShaderUtilities.ID_GradientScale, fontAsset.atlasPadding + 1);
        material.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
        material.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);

        if (AssetDatabase.GetAssetPath(material) != assetPath)
        {
            AssetDatabase.AddObjectToAsset(material, assetPath);
        }

        SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
        SerializedProperty materialProperty = serializedFontAsset.FindProperty("m_Material");
        if (materialProperty != null)
        {
            materialProperty.objectReferenceValue = material;
        }

        SerializedProperty atlasTexturesProperty = serializedFontAsset.FindProperty("m_AtlasTextures");
        if (atlasTexturesProperty != null)
        {
            if (atlasTexturesProperty.arraySize == 0)
            {
                atlasTexturesProperty.InsertArrayElementAtIndex(0);
            }

            atlasTexturesProperty.GetArrayElementAtIndex(0).objectReferenceValue = atlasTexture;
        }

        serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(atlasTexture);
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureFallbacks(TMP_FontAsset fontAsset, params TMP_FontAsset[] fallbacks)
    {
        if (fontAsset == null)
        {
            return;
        }

        try
        {
            if (fontAsset.fallbackFontAssetTable == null)
            {
                return;
            }

            fontAsset.fallbackFontAssetTable.Clear();
            for (int i = 0; i < fallbacks.Length; i++)
            {
                TMP_FontAsset fallback = fallbacks[i];
                if (fallback == null || fallback == fontAsset)
                {
                    continue;
                }

                if (!fontAsset.fallbackFontAssetTable.Contains(fallback))
                {
                    fontAsset.fallbackFontAssetTable.Add(fallback);
                }
            }

            EditorUtility.SetDirty(fontAsset);
        }
        catch
        {
            // Fallback table access is unstable in Unity 6 batchmode for some fresh TMP assets.
        }
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(assetPath);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
