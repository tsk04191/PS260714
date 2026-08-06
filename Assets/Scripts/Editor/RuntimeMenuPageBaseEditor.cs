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
        DrawDefaultInspector();

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
            : "This page still uses a generated layout. Run the migration " +
              "before editing RectTransforms.";
        EditorGUILayout.HelpBox(message, messageType);

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   targets.Length != 1))
        {
            if (GUILayout.Button("Migrate / Unlock Runtime UI"))
            {
                MenuPageSceneBuilder.MigrateRuntimeUiForDesigner(
                    page.gameObject.scene);
            }

            if (page is StageSelectPage stageSelectPage &&
                GUILayout.Button("Sync Stage Select UI & Save Scene"))
            {
                if (stageSelectPage.SyncEditorUi(out string error))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
                        page.gameObject.scene);
                }
                else
                {
                    Debug.LogError(error, stageSelectPage);
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
        DataManager dataManager = FindDataManager();
        AudioClipList musicList = dataManager != null
            ? dataManager.MusicList
            : null;

        EditorGUILayout.HelpBox(
            "Music List에서 Intro, 상황별 Loop, 종료 Exit를 선택합니다. " +
            "Intro/Exit는 비워 둘 수 있지만 Default Loop는 반드시 필요합니다.",
            MessageType.Info);
        if (musicList == null)
        {
            EditorGUILayout.HelpBox(
                "열린 씬에서 DataManager의 Music List를 찾지 못했습니다. " +
                "클립 이름을 직접 입력합니다.",
                MessageType.Warning);
        }

        EditorGUILayout.LabelField("Intro / Loop", EditorStyles.boldLabel);
        DrawMusicField(
            serializedObject.FindProperty("introClipName"),
            "Intro (Optional)",
            musicList);
        SerializedProperty defaultLoop =
            serializedObject.FindProperty("defaultLoopClipName");
        DrawMusicField(defaultLoop, "Default Loop", musicList);
        if (string.IsNullOrWhiteSpace(defaultLoop.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Default Loop를 지정해야 던전 BGM을 시작할 수 있습니다.",
                MessageType.Error);
        }

        DrawPhaseLoops(
            serializedObject.FindProperty("phaseLoops"),
            musicList);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Exit", EditorStyles.boldLabel);
        DrawMusicField(
            serializedObject.FindProperty("clearExitClipName"),
            "Clear Exit (Optional)",
            musicList);
        DrawMusicField(
            serializedObject.FindProperty("defeatExitClipName"),
            "Defeat Exit (Optional)",
            musicList);
        DrawMusicField(
            serializedObject.FindProperty("abortedExitClipName"),
            "Abort Exit (Optional)",
            musicList);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Musical Transition",
            EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bpm"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("beatsPerBar"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("transitionMode"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("scheduleLeadTime"));

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawPhaseLoops(
        SerializedProperty phaseLoops,
        AudioClipList musicList)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Phase Loop Overrides",
            EditorStyles.boldLabel);
        int nextSize = Mathf.Max(
            0,
            EditorGUILayout.DelayedIntField("Size", phaseLoops.arraySize));
        if (nextSize != phaseLoops.arraySize)
            phaseLoops.arraySize = nextSize;

        HashSet<EDungeonPhase> phases = new();
        for (int index = 0; index < phaseLoops.arraySize; index++)
        {
            SerializedProperty element =
                phaseLoops.GetArrayElementAtIndex(index);
            SerializedProperty phase = element.FindPropertyRelative("phase");
            SerializedProperty clipName =
                element.FindPropertyRelative("clipName");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(phase, new GUIContent("Phase"));
                DrawMusicField(clipName, "Loop", musicList);
            }

            EDungeonPhase phaseValue = (EDungeonPhase)phase.enumValueIndex;
            if (!phases.Add(phaseValue))
            {
                EditorGUILayout.HelpBox(
                    $"{phaseValue} Override가 중복되었습니다. 첫 항목만 사용됩니다.",
                    MessageType.Warning);
            }
        }
    }

    private static void DrawMusicField(
        SerializedProperty clipName,
        string label,
        AudioClipList musicList)
    {
        if (musicList == null || musicList.list == null)
        {
            EditorGUILayout.PropertyField(clipName, new GUIContent(label));
            return;
        }

        List<string> values = new() { string.Empty };
        List<string> labels = new() { "(None)" };
        HashSet<string> added = new(StringComparer.Ordinal);
        foreach (AudioClipData entry in musicList.list)
        {
            string name = Normalize(entry?.clip_name);
            if (string.IsNullOrEmpty(name) || !added.Add(name))
                continue;

            values.Add(name);
            labels.Add(entry.clip != null
                ? $"{name} ({entry.clip.name})"
                : $"{name} (AudioClip missing)");
        }

        string current = Normalize(clipName.stringValue);
        int selectedIndex = values.FindIndex(value => string.Equals(
            value,
            current,
            StringComparison.Ordinal));
        if (selectedIndex < 0 && !string.IsNullOrEmpty(current))
        {
            selectedIndex = values.Count;
            values.Add(current);
            labels.Add($"Missing from Music List: {current}");
        }
        if (selectedIndex < 0)
            selectedIndex = 0;

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup(
            new GUIContent(label),
            selectedIndex,
            labels.ToArray());
        if (EditorGUI.EndChangeCheck())
            clipName.stringValue = values[nextIndex];

        if (!string.IsNullOrEmpty(current))
        {
            AudioClipData entry = musicList.FindData(current);
            if (entry == null)
            {
                EditorGUILayout.HelpBox(
                    $"'{current}'을 Music List에서 찾을 수 없습니다.",
                    MessageType.Error);
            }
            else if (entry.clip == null)
            {
                EditorGUILayout.HelpBox(
                    $"'{current}'에 AudioClip이 지정되지 않았습니다.",
                    MessageType.Error);
            }
        }
    }

    private static DataManager FindDataManager()
    {
        foreach (DataManager candidate in
                 Resources.FindObjectsOfTypeAll<DataManager>())
        {
            if (candidate != null && !EditorUtility.IsPersistent(candidate))
                return candidate;
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
