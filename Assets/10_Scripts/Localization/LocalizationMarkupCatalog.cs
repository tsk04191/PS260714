using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace PS260714.Localization
{
    [Serializable]
    public sealed class LocalizationMarkupStyleDefinition
    {
        [SerializeField] private string id = "body";
        [SerializeField] private Color color = Color.white;
        [SerializeField] private bool bold;
        [SerializeField] private bool italic;
        [SerializeField] private bool underline;

        public string Id => id;
        public Color Color => color;
        public bool Bold => bold;
        public bool Italic => italic;
        public bool Underline => underline;
    }

    [Serializable]
    public sealed class LocalizationIconDefinition
    {
        [SerializeField] private string id = "icon";
        [SerializeField] private Sprite sprite;
        [Tooltip("Visible text used when the sprite is not assigned.")]
        [SerializeField] private string fallbackText;

        public string Id => id;
        public Sprite Sprite => sprite;
        public string FallbackText => fallbackText;
    }

    /// <summary>
    /// Maps semantic CSV markup to presentation values. Translation rows only
    /// store stable aliases such as fire or energy, never Unity asset GUIDs.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationMarkupCatalog",
        menuName = "PS260714/Localization/Markup Catalog")]
    public sealed class LocalizationMarkupCatalog : ScriptableObject
    {
        private const string RuntimeSpriteAssetVersionJson =
            "{\"m_Version\":\"1.1.0\"}";
        private const float RuntimeIconEmSize = 100f;
        private const float RuntimeIconAscent = 80f;
        private const float RuntimeIconDescent = -20f;

        [SerializeField]
        private List<LocalizationMarkupStyleDefinition> styles = new();
        [SerializeField]
        private List<LocalizationIconDefinition> icons = new();

        [NonSerialized] private TMP_SpriteAsset runtimeSpriteAsset;
        [NonSerialized] private List<TMP_SpriteAsset> runtimeSpriteAssets;
        [NonSerialized] private List<Material> runtimeSpriteMaterials;
        [NonSerialized] private bool runtimeSpriteAssetBuilt;

        public TMP_SpriteAsset SpriteAsset => GetOrCreateSpriteAsset();
        public IReadOnlyList<LocalizationMarkupStyleDefinition> Styles => styles;
        public IReadOnlyList<LocalizationIconDefinition> Icons => icons;

        public bool TryGetStyle(
            string id,
            out Color color,
            out bool bold,
            out bool italic,
            out bool underline)
        {
            for (int index = 0; index < styles.Count; index++)
            {
                LocalizationMarkupStyleDefinition definition = styles[index];
                if (definition != null &&
                    string.Equals(
                        definition.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    color = definition.Color;
                    bold = definition.Bold;
                    italic = definition.Italic;
                    underline = definition.Underline;
                    return true;
                }
            }

            return LocalizationMarkupDefaults.TryGetStyle(
                id,
                out color,
                out bold,
                out italic,
                out underline);
        }

        public bool TryGetIcon(string id, out Sprite sprite)
        {
            for (int index = 0; index < icons.Count; index++)
            {
                LocalizationIconDefinition definition = icons[index];
                if (definition != null &&
                    string.Equals(
                        definition.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase) &&
                    definition.Sprite != null)
                {
                    sprite = definition.Sprite;
                    return true;
                }
            }

            sprite = null;
            return false;
        }

        public bool TryGetRenderableIcon(string id, out string spriteKey)
        {
            spriteKey = string.Empty;
            if (!LocalizationMarkupDefaults.IsSafeIdentifier(id) ||
                !TryGetIcon(id, out Sprite _))
            {
                return false;
            }

            TMP_SpriteAsset asset = SpriteAsset;
            int hashCode = TMP_TextUtilities.GetHashCode(id);
            if (asset == null ||
                TMP_SpriteAsset.SearchForSpriteByHashCode(
                    asset,
                    hashCode,
                    true,
                    out int spriteIndex) == null ||
                spriteIndex < 0)
            {
                return false;
            }

            spriteKey = id;
            return true;
        }

        public string GetIconFallback(string id)
        {
            for (int index = 0; index < icons.Count; index++)
            {
                LocalizationIconDefinition definition = icons[index];
                if (definition != null &&
                    string.Equals(
                        definition.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(definition.FallbackText))
                {
                    return definition.FallbackText.Trim();
                }
            }

            return LocalizationMarkupDefaults.GetIconFallback(id);
        }

        private TMP_SpriteAsset GetOrCreateSpriteAsset()
        {
            if (runtimeSpriteAssetBuilt)
                return runtimeSpriteAsset;

            runtimeSpriteAssetBuilt = true;
            runtimeSpriteAssets = new List<TMP_SpriteAsset>();
            runtimeSpriteMaterials = new List<Material>();

            Dictionary<Texture, List<LocalizationIconDefinition>> groups =
                new Dictionary<Texture, List<LocalizationIconDefinition>>();
            List<Texture> textureOrder = new List<Texture>();
            for (int index = 0; index < icons.Count; index++)
            {
                LocalizationIconDefinition definition = icons[index];
                if (definition == null ||
                    definition.Sprite == null ||
                    !LocalizationMarkupDefaults.IsSafeIdentifier(
                        definition.Id))
                {
                    continue;
                }

                Texture texture = definition.Sprite.texture;
                if (texture == null)
                    continue;

                if (!groups.TryGetValue(
                        texture,
                        out List<LocalizationIconDefinition> definitions))
                {
                    definitions = new List<LocalizationIconDefinition>();
                    groups.Add(texture, definitions);
                    textureOrder.Add(texture);
                }

                definitions.Add(definition);
            }

            for (int index = 0; index < textureOrder.Count; index++)
            {
                Texture texture = textureOrder[index];
                TMP_SpriteAsset asset = CreateRuntimeSpriteAsset(
                    texture,
                    groups[texture],
                    index);
                if (asset != null)
                    runtimeSpriteAssets.Add(asset);
            }

            if (runtimeSpriteAssets.Count == 0)
                return null;

            runtimeSpriteAsset = runtimeSpriteAssets[0];
            if (runtimeSpriteAssets.Count > 1)
            {
                runtimeSpriteAsset.fallbackSpriteAssets =
                    runtimeSpriteAssets.GetRange(
                        1,
                        runtimeSpriteAssets.Count - 1);
            }

            return runtimeSpriteAsset;
        }

        private TMP_SpriteAsset CreateRuntimeSpriteAsset(
            Texture texture,
            List<LocalizationIconDefinition> definitions,
            int groupIndex)
        {
            Material template = TMP_Settings.defaultSpriteAsset != null
                ? TMP_Settings.defaultSpriteAsset.material
                : null;
            Shader shader = template != null
                ? template.shader
                : Shader.Find("TextMeshPro/Sprite");
            if (shader == null)
                return null;

            Material material = template != null
                ? new Material(template)
                : new Material(shader);
            material.name = $"{name} Runtime Icon Material {groupIndex}";
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetTexture(Shader.PropertyToID("_MainTex"), texture);
            runtimeSpriteMaterials.Add(material);

            TMP_SpriteAsset asset = CreateInstance<TMP_SpriteAsset>();
            asset.name = $"{name} Runtime Icons {groupIndex}";
            asset.hideFlags = HideFlags.HideAndDontSave;
            JsonUtility.FromJsonOverwrite(
                RuntimeSpriteAssetVersionJson,
                asset);
            asset.spriteInfoList = new List<TMP_Sprite>();
            asset.spriteSheet = texture;
            asset.material = material;
            asset.faceInfo = CreateRuntimeIconFaceInfo();
            asset.UpdateLookupTables();

            List<TMP_SpriteGlyph> glyphs = new List<TMP_SpriteGlyph>();
            List<TMP_SpriteCharacter> characters =
                new List<TMP_SpriteCharacter>();
            for (int index = 0; index < definitions.Count; index++)
            {
                LocalizationIconDefinition definition = definitions[index];
                Sprite sprite = definition.Sprite;
                Rect rect = sprite.rect;
                TMP_SpriteGlyph glyph = new TMP_SpriteGlyph(
                    (uint)index,
                    new GlyphMetrics(
                        RuntimeIconEmSize,
                        RuntimeIconEmSize,
                        0f,
                        RuntimeIconAscent,
                        RuntimeIconEmSize),
                    new GlyphRect(rect),
                    1f,
                    0,
                    sprite);
                TMP_SpriteCharacter character = new TMP_SpriteCharacter(
                    0xFFFE,
                    asset,
                    glyph)
                {
                    name = definition.Id,
                    scale = 1f,
                };
                glyphs.Add(glyph);
                characters.Add(character);
            }

            asset.spriteGlyphTable.AddRange(glyphs);
            asset.spriteCharacterTable.AddRange(characters);
            asset.UpdateLookupTables();
            return asset;
        }

        private static FaceInfo CreateRuntimeIconFaceInfo()
        {
            return new FaceInfo
            {
                pointSize = RuntimeIconEmSize,
                scale = 1f,
                lineHeight = RuntimeIconEmSize,
                ascentLine = RuntimeIconAscent,
                capLine = RuntimeIconAscent,
                meanLine = RuntimeIconAscent * 0.625f,
                baseline = 0f,
                descentLine = RuntimeIconDescent,
            };
        }

        private void OnValidate()
        {
            ReleaseRuntimeSpriteAssets();
        }

        private void OnDisable()
        {
            ReleaseRuntimeSpriteAssets();
        }

        private void ReleaseRuntimeSpriteAssets()
        {
            if (runtimeSpriteAssets != null)
            {
                for (int index = 0;
                     index < runtimeSpriteAssets.Count;
                     index++)
                {
                    DestroyRuntimeObject(runtimeSpriteAssets[index]);
                }
            }

            if (runtimeSpriteMaterials != null)
            {
                for (int index = 0;
                     index < runtimeSpriteMaterials.Count;
                     index++)
                {
                    DestroyRuntimeObject(runtimeSpriteMaterials[index]);
                }
            }

            runtimeSpriteAsset = null;
            runtimeSpriteAssets = null;
            runtimeSpriteMaterials = null;
            runtimeSpriteAssetBuilt = false;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(value);
                return;
            }
#endif
            Destroy(value);
        }
    }

    public static class LocalizationMarkupDefaults
    {
        public static readonly string[] StyleIds = Array.Empty<string>();
        public static readonly string[] IconIds = Array.Empty<string>();

        public static bool TryGetStyle(
            string id,
            out Color color,
            out bool bold,
            out bool italic,
            out bool underline)
        {
            color = Color.white;
            bold = false;
            italic = false;
            underline = false;
            return false;
        }

        public static string GetIconFallback(string id)
        {
            return string.IsNullOrWhiteSpace(id)
                ? "ICON"
                : id.Trim().ToUpperInvariant();
        }

        public static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character) &&
                    character != '_' &&
                    character != '-' &&
                    character != '.')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
