using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
        [SerializeField] private string spriteName = "icon";
        [Tooltip("Visible text used when the sprite asset or alias is missing.")]
        [SerializeField] private string fallbackText;

        public string Id => id;
        public string SpriteName => spriteName;
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
        [SerializeField] private TMP_SpriteAsset spriteAsset;
        [SerializeField]
        private List<LocalizationMarkupStyleDefinition> styles = new();
        [SerializeField]
        private List<LocalizationIconDefinition> icons = new();

        public TMP_SpriteAsset SpriteAsset => spriteAsset;
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

        public bool TryGetIcon(string id, out string spriteName)
        {
            for (int index = 0; index < icons.Count; index++)
            {
                LocalizationIconDefinition definition = icons[index];
                if (definition != null &&
                    string.Equals(
                        definition.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase) &&
                    LocalizationMarkupDefaults.IsSafeIdentifier(
                        definition.SpriteName))
                {
                    spriteName = definition.SpriteName;
                    return true;
                }
            }

            return LocalizationMarkupDefaults.TryGetIcon(id, out spriteName);
        }

        public bool TryGetRenderableIcon(string id, out string spriteName)
        {
            if (!TryGetIcon(id, out spriteName))
            {
                return false;
            }

            TMP_SpriteAsset effectiveAsset = spriteAsset != null
                ? spriteAsset
                : TMP_Settings.defaultSpriteAsset;
            return ContainsSprite(effectiveAsset, spriteName);
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

        public static bool ContainsSprite(
            TMP_SpriteAsset asset,
            string spriteName)
        {
            if (asset == null ||
                !LocalizationMarkupDefaults.IsSafeIdentifier(spriteName))
            {
                return false;
            }

            int hashCode = TMP_TextUtilities.GetHashCode(spriteName);
            return TMP_SpriteAsset.SearchForSpriteByHashCode(
                       asset,
                       hashCode,
                       true,
                       out int spriteIndex) != null &&
                   spriteIndex >= 0;
        }
    }

    public static class LocalizationMarkupDefaults
    {
        public static readonly string[] StyleIds =
        {
            "fire",
            "focus",
            "damage",
            "energy",
            "warning",
            "positive",
            "title",
        };

        public static readonly string[] IconIds =
        {
            "fire",
            "focus",
            "damage",
            "energy",
            "attack",
            "speed",
        };

        public static bool TryGetStyle(
            string id,
            out Color color,
            out bool bold,
            out bool italic,
            out bool underline)
        {
            bold = false;
            italic = false;
            underline = false;

            switch ((id ?? string.Empty).ToLowerInvariant())
            {
                case "fire":
                    color = new Color32(255, 112, 67, 255);
                    bold = true;
                    return true;
                case "focus":
                    color = new Color32(255, 209, 102, 255);
                    bold = true;
                    return true;
                case "damage":
                    color = new Color32(255, 92, 92, 255);
                    bold = true;
                    return true;
                case "energy":
                    color = new Color32(102, 204, 255, 255);
                    bold = true;
                    return true;
                case "warning":
                    color = new Color32(255, 193, 7, 255);
                    bold = true;
                    return true;
                case "positive":
                    color = new Color32(117, 230, 143, 255);
                    return true;
                case "title":
                    color = Color.white;
                    bold = true;
                    return true;
                default:
                    color = Color.white;
                    return false;
            }
        }

        public static bool TryGetIcon(string id, out string spriteName)
        {
            for (int index = 0; index < IconIds.Length; index++)
            {
                if (string.Equals(
                    IconIds[index],
                    id,
                    StringComparison.OrdinalIgnoreCase))
                {
                    spriteName = IconIds[index];
                    return true;
                }
            }

            spriteName = string.Empty;
            return false;
        }

        public static string GetIconFallback(string id)
        {
            switch ((id ?? string.Empty).ToLowerInvariant())
            {
                case "fire":
                    return "FIRE";
                case "focus":
                    return "FOCUS";
                case "damage":
                    return "DMG";
                case "energy":
                    return "ENERGY";
                case "attack":
                    return "ATK";
                case "speed":
                    return "SPD";
                default:
                    return string.IsNullOrWhiteSpace(id)
                        ? "ICON"
                        : id.Trim().ToUpperInvariant();
            }
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
