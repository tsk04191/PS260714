using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PS260714.Localization
{
    [Serializable]
    public sealed class SerializedLocalizationArgument
    {
        [SerializeField] private string name;
        [SerializeField, TextArea] private string value;

        public string Name => name;
        public string Value => value;
    }

    /// <summary>
    /// Binds a TMP text component to a generated localization key and refreshes
    /// both its message and resolved font when locale/font settings change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private string fontRoleOverride;
        [SerializeField]
        private List<SerializedLocalizationArgument> inspectorArguments = new();

        private readonly Dictionary<string, object> runtimeArguments =
            new(StringComparer.Ordinal);
        private TMP_Text target;

        public string Key => key;
        public string FontRoleOverride => fontRoleOverride;
        public TMP_Text Target => target != null ? target : ResolveTarget();

        private void Awake()
        {
            ResolveTarget();
        }

        private void OnEnable()
        {
            LocalizationService.LocaleChanged += HandleLocaleChanged;
            LocalizationService.FontChanged += HandleFontChanged;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationService.LocaleChanged -= HandleLocaleChanged;
            LocalizationService.FontChanged -= HandleFontChanged;
        }

        public void SetKey(string localizationKey, bool refresh = true)
        {
            key = localizationKey ?? string.Empty;
            if (refresh)
            {
                Refresh();
            }
        }

        public void SetFontRoleOverride(string fontRole, bool refresh = true)
        {
            fontRoleOverride = fontRole ?? string.Empty;
            if (refresh)
            {
                Refresh();
            }
        }

        public void SetArgument(string name, object value, bool refresh = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            runtimeArguments[name] = value;
            if (refresh)
            {
                Refresh();
            }
        }

        public void ClearArguments(bool refresh = true)
        {
            runtimeArguments.Clear();
            if (refresh)
            {
                Refresh();
            }
        }

        [ContextMenu("Refresh Localized Text")]
        public void Refresh()
        {
            TMP_Text text = ResolveTarget();
            if (text == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            Dictionary<string, object> arguments =
                new Dictionary<string, object>(StringComparer.Ordinal);
            for (int index = 0; index < inspectorArguments.Count; index++)
            {
                SerializedLocalizationArgument argument =
                    inspectorArguments[index];
                if (argument != null &&
                    !string.IsNullOrWhiteSpace(argument.Name))
                {
                    arguments[argument.Name] = argument.Value;
                }
            }

            foreach (KeyValuePair<string, object> pair in runtimeArguments)
            {
                arguments[pair.Key] = pair.Value;
            }

            LocalizedMessage message = LocalizationService.Resolve(
                key,
                arguments);
            text.richText = true;
            text.text = message.Text;

            string fontRole = string.IsNullOrWhiteSpace(fontRoleOverride)
                ? message.FontRole
                : fontRoleOverride;
            if (LocalizationFontResolver.Current != null)
            {
                LocalizationFontResolver.Current.Apply(text, fontRole);
            }
            else
            {
                LocalizationFontResolver.ApplyGameDefault(text, fontRole);
            }
        }

        private TMP_Text ResolveTarget()
        {
            if (target == null)
            {
                target = GetComponent<TMP_Text>();
            }

            return target;
        }

        private void HandleLocaleChanged(string unusedLocale)
        {
            Refresh();
        }

        private void HandleFontChanged(string unusedFontId)
        {
            Refresh();
        }
    }
}
