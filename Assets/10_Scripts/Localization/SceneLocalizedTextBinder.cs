using TMPro;
using UnityEngine;

namespace PS260714.Localization
{
    /// <summary>
    /// Compatibility binding for scene-authored TMP text when scene assets are
    /// not part of the script repository. Only an exact, unambiguous en-US
    /// source match is bound; dynamic or conflicting text stays view-owned.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9990)]
    public sealed class SceneLocalizedTextBinder : MonoBehaviour
    {
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool scanForRuntimeText = true;
        [SerializeField, Min(0.1f)] private float runtimeScanInterval = 0.5f;

        private float nextRuntimeScan;

        private void OnEnable()
        {
            BindHierarchy();
            ScheduleNextScan();
        }

        private void LateUpdate()
        {
            if (!scanForRuntimeText || Time.unscaledTime < nextRuntimeScan)
            {
                return;
            }

            BindHierarchy();
            ScheduleNextScan();
        }

        [ContextMenu("Bind Exact Scene Localization Text")]
        public void BindHierarchy()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(
                includeInactive);
            for (int index = 0; index < texts.Length; index++)
            {
                TryBind(texts[index]);
            }
        }

        private static void TryBind(TMP_Text text)
        {
            if (text == null || text.GetComponent<LocalizedText>() != null)
            {
                return;
            }

            if (IsDropdownManagedText(text))
            {
                return;
            }

            if (IsRuntimeManagedText(text))
            {
                return;
            }

            // Deliberately do not trim, normalize case, strip rich text, or
            // format placeholders. A view is safe to bind only when its
            // authored value exactly matches one unique reference entry.
            if (!GeneratedLocalizationTables.TryGetUniqueKeyByReferenceText(
                    text.text,
                    out string key))
            {
                return;
            }

            LocalizedText localizedText =
                text.gameObject.AddComponent<LocalizedText>();
            localizedText.SetKey(key, refresh: true);
        }

        private static bool IsDropdownManagedText(TMP_Text text)
        {
            TMP_Dropdown dropdown =
                text.GetComponentInParent<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                return false;
            }

            if (dropdown.itemText == text || dropdown.captionText == text)
            {
                return true;
            }

            RectTransform template = dropdown.template;
            return template != null &&
                   (text.transform == template ||
                    text.transform.IsChildOf(template));
        }

        private static bool IsRuntimeManagedText(TMP_Text text)
        {
            // These views already refresh their text from state plus the
            // current locale. Attaching a key inferred from one transient
            // value (for example PAUSE or READY) would restore that old state
            // on the next locale change.
            if (text.GetComponentInParent<RuntimeMenuPageBase>(true) != null ||
                text.GetComponentInParent<SettingPage>(true) != null ||
                text.GetComponentInParent<PracticeBattlePanelView>(
                    true) != null ||
                text.GetComponentInParent<LoadingPage>(true) != null ||
                text.GetComponentInParent<CharacterRuntime>(true) != null ||
                text.GetComponentInParent<EnemyCard>(true) != null ||
                text.GetComponentInParent<DungeonSpawnQueueItemView>(true) != null ||
                text.GetComponentInParent<DungeonItemHandView>(true) != null ||
                text.GetComponentInParent<DungeonRewardCardHoverView>(true) != null)
            {
                return true;
            }

            return text.name == "txtGamePause" ||
                   text.name == "txtPauseOverlay" ||
                   text.name == "txtPlayerPartyInfoTitle" ||
                   text.name == "txtSpawnTimer" ||
                   text.name == "txtEventTitle" ||
                   text.name == "txtEventDescription" ||
                   text.name == "txtRewardCategory" ||
                   text.name == "txtRewardTitle" ||
                   text.name == "txtRewardDescription" ||
                   text.name == "txtRewardFooter" ||
                   text.name == "txtChoice" ||
                   text.name == "txtItemTargetInstruction" ||
                   text.name == "txtItemSummary" ||
                   text.name == "txtItemDetail";
        }

        private void ScheduleNextScan()
        {
            nextRuntimeScan = Time.unscaledTime +
                              Mathf.Max(0.1f, runtimeScanInterval);
        }
    }
}
