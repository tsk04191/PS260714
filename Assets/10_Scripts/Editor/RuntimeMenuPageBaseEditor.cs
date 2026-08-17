using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RuntimeMenuPageBase), true)]
[CanEditMultipleObjects]
public sealed class RuntimeMenuPageBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PS260714EditorText.DrawDefaultInspector(serializedObject);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "Designer UI",
            EditorStyles.boldLabel);

        RuntimeMenuPageBase page = target as RuntimeMenuPageBase;
        if (page == null)
            return;

        MessageType messageType = page.HasDesignerLayout
            ? MessageType.Info
            : MessageType.Warning;
        string message = page.HasDesignerLayout
            ? "This page uses the designer-owned scene layout. " +
              "Runtime code will not overwrite its RectTransforms."
            : "Saved designer UI references are incomplete. Repair the " +
              "Scene hierarchy and inspector references directly.";
        EditorGUILayout.HelpBox(message, messageType);

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   targets.Length != 1))
        {
            if (page is StageSelectPage stageSelectPage &&
                GUILayout.Button("Validate Stage Select UI"))
            {
                if (!stageSelectPage.ValidateEditorUi(out string error))
                {
                    Debug.LogError(error, stageSelectPage);
                }
                else
                {
                    Debug.Log("Stage Select UI validation passed.", page);
                }
            }

            if (GUILayout.Button("Validate Designer UI References"))
            {
                IReadOnlyList<string> issues =
                    MenuPageSceneBuilder.ValidateDesignerUiForScene(
                        page.gameObject.scene);
                if (issues.Count == 0)
                {
                    Debug.Log(
                        "Designer UI validation passed.",
                        page);
                }
                else
                {
                    Debug.LogWarning(
                        "Designer UI validation found:\n- " +
                        string.Join("\n- ", issues),
                        page);
                }
            }
        }
    }
}

[CustomEditor(typeof(PageBgmSelection))]
public sealed class PageBgmSelectionEditor : Editor
{
    private const string BgmClipNamePropertyName = "bgmClipName";
    private const string KeepCurrentLabel = "현재 BGM 유지";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty clipNameProperty =
            serializedObject.FindProperty(BgmClipNamePropertyName);
        PageBgmSelection selection = target as PageBgmSelection;
        DataManager dataManager = FindDataManager(selection);
        if (clipNameProperty == null)
        {
            EditorGUILayout.HelpBox(
                "BGM 선택 필드를 찾을 수 없습니다.",
                MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (dataManager == null || dataManager.MusicList == null)
        {
            EditorGUILayout.HelpBox(
                "현재 씬에서 DataManager의 Music List를 찾을 수 없어 " +
                "이름을 직접 입력합니다.",
                MessageType.Warning);
            EditorGUILayout.PropertyField(
                clipNameProperty,
                new GUIContent("페이지 BGM 이름"));
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawMusicPopup(clipNameProperty, dataManager.MusicList);
        DrawCatalogWarnings(clipNameProperty.stringValue, dataManager.MusicList);
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawMusicPopup(
        SerializedProperty clipNameProperty,
        AudioClipList musicList)
    {
        List<string> names = new() { string.Empty };
        List<string> labels = new() { KeepCurrentLabel };
        HashSet<string> addedNames = new(StringComparer.Ordinal);
        if (musicList.list != null)
        {
            foreach (AudioClipData entry in musicList.list)
            {
                string name = NormalizeName(entry?.clip_name);
                if (string.IsNullOrEmpty(name) || !addedNames.Add(name))
                    continue;

                names.Add(name);
                labels.Add(entry.clip != null
                    ? $"{name} ({entry.clip.name})"
                    : $"{name} (AudioClip 미지정)");
            }
        }

        string currentName = NormalizeName(clipNameProperty.stringValue);
        int currentIndex = names.FindIndex(name => string.Equals(
            name,
            currentName,
            StringComparison.Ordinal));
        if (currentIndex < 0 && !string.IsNullOrEmpty(currentName))
        {
            currentIndex = names.Count;
            names.Add(currentName);
            labels.Add($"목록에서 찾을 수 없음: {currentName}");
        }
        if (currentIndex < 0)
            currentIndex = 0;

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup(
            new GUIContent(
                "페이지 BGM",
                "페이지가 열릴 때 재생할 Music List 항목입니다. " +
                "현재 BGM 유지는 음악을 변경하지 않습니다."),
            currentIndex,
            labels.ToArray());
        if (EditorGUI.EndChangeCheck())
            clipNameProperty.stringValue = names[nextIndex];
    }

    private static void DrawCatalogWarnings(
        string selectedName,
        AudioClipList musicList)
    {
        string normalizedSelection = NormalizeName(selectedName);
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        AudioClipData selectedEntry = null;
        if (musicList.list != null)
        {
            foreach (AudioClipData entry in musicList.list)
            {
                string name = NormalizeName(entry?.clip_name);
                if (string.IsNullOrEmpty(name))
                    continue;

                counts.TryGetValue(name, out int count);
                counts[name] = count + 1;
                if (selectedEntry == null && string.Equals(
                        name,
                        normalizedSelection,
                        StringComparison.Ordinal))
                {
                    selectedEntry = entry;
                }
            }
        }

        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value <= 1)
                continue;

            EditorGUILayout.HelpBox(
                $"Music List에 '{pair.Key}' 이름이 {pair.Value}개 있습니다. " +
                "첫 번째 항목이 재생되므로 이름을 고유하게 수정하세요.",
                MessageType.Warning);
        }

        if (string.IsNullOrEmpty(normalizedSelection))
            return;
        if (selectedEntry == null)
        {
            EditorGUILayout.HelpBox(
                $"선택한 BGM '{normalizedSelection}'을 Music List에서 " +
                "찾을 수 없습니다.",
                MessageType.Error);
        }
        else if (selectedEntry.clip == null)
        {
            EditorGUILayout.HelpBox(
                $"BGM '{normalizedSelection}'에 AudioClip이 지정되지 " +
                "않았습니다.",
                MessageType.Error);
        }
    }

    private static DataManager FindDataManager(PageBgmSelection selection)
    {
        if (selection == null)
            return null;

        DataManager fallback = null;
        foreach (DataManager candidate in
                 Resources.FindObjectsOfTypeAll<DataManager>())
        {
            if (candidate == null || EditorUtility.IsPersistent(candidate))
                continue;
            if (candidate.gameObject.scene == selection.gameObject.scene)
                return candidate;
            fallback ??= candidate;
        }

        return fallback;
    }

    private static string NormalizeName(string clipName)
    {
        return string.IsNullOrWhiteSpace(clipName)
            ? string.Empty
            : clipName.Trim();
    }
}

[CustomEditor(typeof(DungeonBgmProfile))]
public sealed class DungeonBgmProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "Ready is used for entry choices and post-battle rewards. " +
            "BattleSO and DungeonEventSO can override the other defaults. " +
            "Overrides inherit the volume of their Battle or Rest state. " +
            "Different clips fade out completely before the next clip " +
            "fades in.",
            MessageType.Info);

        EditorGUILayout.LabelField("Default Music", EditorStyles.boldLabel);
        DrawTrack("Ready", "readyClip", "readyVolumePercent");
        DrawTrack("Battle", "battleClip", "battleVolumePercent");
        DrawTrack("Rest", "restClip", "restVolumePercent");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Sequential Fade", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("fadeOutDuration"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("fadeInDuration"));
        serializedObject.ApplyModifiedProperties();

        DungeonBgmProfile profile = target as DungeonBgmProfile;
        if (profile != null && !profile.TryValidate(out string error))
            EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    private void DrawTrack(
        string label,
        string clipPropertyName,
        string volumePropertyName)
    {
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(clipPropertyName),
            new GUIContent($"{label} Clip"));
        EditorGUILayout.IntSlider(
            serializedObject.FindProperty(volumePropertyName),
            0,
            100,
            new GUIContent($"{label} Volume (%)"));
        EditorGUILayout.Space(3f);
    }

}
