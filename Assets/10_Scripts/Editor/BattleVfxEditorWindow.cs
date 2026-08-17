using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class BattleVfxEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.BattleVfxEditor;
    public const string ValidateMenuPath =
        PS260714EditorMenu.ValidateBattleVfx;

    private const string AssetFolder = "Assets/06_Runtime/Resources/BattleVfx";
    private const string RenameControlName = "BattleVfxRenameField";
    private const float GridPreviewSize = 220f;
    private const int VfxGridDimension = 10;

    private readonly List<BattleVfxCueSO> _cues = new();

    private BattleVfxCueSO _selected;
    private SerializedObject _serialized;
    private Vector2 _listScroll;
    private Vector2 _editorScroll;
    private string _searchText = string.Empty;
    private string _renameAssetName = string.Empty;
    private bool _isRenaming;
    private bool _focusRenameField;
    private bool _identityExpanded = true;
    private bool _timelineExpanded = true;
    private bool _anchorExpanded = true;
    private bool _motionExpanded = true;
    private bool _lifetimeExpanded = true;
    private bool _poolExpanded = true;
    private int _selectedClipIndex = -1;

    [MenuItem(
        MenuPath,
        false,
        PS260714EditorMenu.BattleVfxEditorPriority)]
    public static void Open()
    {
        BattleVfxEditorWindow window =
            GetWindow<BattleVfxEditorWindow>();
        window.titleContent = new GUIContent("Battle VFX");
        window.minSize = new Vector2(820f, 520f);
        window.Show();
    }

    public static void Open(BattleVfxCueSO cue)
    {
        Open();
        BattleVfxEditorWindow window =
            GetWindow<BattleVfxEditorWindow>();
        window.RefreshList();
        if (cue != null)
            window.SelectCue(cue);
        window.Repaint();
    }

    [MenuItem(
        MenuPath,
        true,
        PS260714EditorMenu.BattleVfxEditorPriority)]
    private static bool ValidateOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(
        ValidateMenuPath,
        false,
        PS260714EditorMenu.ValidateBattleVfxPriority)]
    public static void ValidateProject()
    {
        List<BattleVfxCueSO> cues = LoadAllAssets<BattleVfxCueSO>();
        List<BattleVfxQualityProfileSO> profiles =
            LoadAllAssets<BattleVfxQualityProfileSO>();
        List<StatusEffectSO> statuses = LoadAllAssets<StatusEffectSO>();
        BattleVfxCueValidationResult result =
            BattleVfxCueValidator.ValidateAll(cues);

        foreach (BattleVfxCueDiagnostic diagnostic in
                 result.Diagnostics)
        {
            if (diagnostic.Severity ==
                BattleVfxCueDiagnosticSeverity.Error)
            {
                Debug.LogError(diagnostic.ToString());
            }
            else
            {
                Debug.LogWarning(diagnostic.ToString());
            }
        }

        int legacyStatusPrefabCount = 0;
        foreach (StatusEffectSO status in statuses)
        {
            if (status == null || status.VisualEffectPrefab == null)
                continue;

            legacyStatusPrefabCount++;
            Debug.LogWarning(
                $"Status effect '{status.name}' still uses the legacy " +
                $"direct visualEffectPrefab field. Assign Battle VFX cues " +
                $"and remove the legacy reference when migration is complete.",
                status);
        }

        string summary =
            $"Battle VFX project validation complete.\n\n" +
            $"Cues: {cues.Count}\n" +
            $"Quality profiles: {profiles.Count}\n" +
            $"Errors: {result.ErrorCount}\n" +
            $"Warnings: {result.WarningCount}\n" +
            $"Legacy status prefabs: {legacyStatusPrefabCount}";
        if (result.ErrorCount > 0 || legacyStatusPrefabCount > 0)
            Debug.LogWarning(summary);
        else
            Debug.Log(summary);

        EditorUtility.DisplayDialog(
            "Validate Battle VFX",
            summary,
            "OK");
    }

    [MenuItem(
        ValidateMenuPath,
        true,
        PS260714EditorMenu.ValidateBattleVfxPriority)]
    private static bool ValidateProjectMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Battle VFX");
        RefreshList();
    }

    private void OnProjectChange()
    {
        RefreshList();
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is BattleVfxCueSO cue)
            SelectCue(cue);
        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();
        if (_isRenaming)
            DrawRenameRow();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAssetList();
            DrawEditor();
        }
    }

    private void DrawToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Battle VFX: {_cues.Count}",
            _selected != null,
            () =>
            {
                CreateCue();
                GUIUtility.ExitGUI();
            },
            () =>
            {
                SaveSelected();
                GUIUtility.ExitGUI();
            },
            () =>
            {
                DuplicateSelected();
                GUIUtility.ExitGUI();
            },
            BeginRename,
            () =>
            {
                DeleteSelected();
                GUIUtility.ExitGUI();
            },
            () => PS260714AssetEditorList.Ping(_selected),
            RefreshList);
    }

    private void DrawRenameRow()
    {
        PS260714AssetRenameCommand command =
            PS260714EditorAssetUtility.DrawRenameRow(
                "SO File Name",
                RenameControlName,
                ref _renameAssetName,
                ref _focusRenameField);
        if (command == PS260714AssetRenameCommand.None)
            return;
        if (command == PS260714AssetRenameCommand.Apply)
            RenameSelected();
        else
            CancelRename();
        GUIUtility.ExitGUI();
    }

    private void DrawAssetList()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(PS260714AssetEditorList.Width),
                   GUILayout.ExpandHeight(true)))
        {
            _searchText =
                PS260714AssetEditorList.DrawSearchField(_searchText);

            int visibleCount = 0;
            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_listScroll))
            {
                _listScroll = scroll.scrollPosition;
                foreach (BattleVfxCueSO cue in _cues)
                {
                    if (cue == null || !MatchesSearch(cue))
                        continue;

                    visibleCount++;
                    bool selected = ReferenceEquals(cue, _selected);
                    if (PS260714AssetEditorList.DrawAssetRow(
                            selected,
                            cue,
                            cue.Prefab,
                            cue.name,
                            cue.CueId,
                            AssetDatabase.GetAssetPath(cue)))
                    {
                        SelectCue(cue);
                    }
                }
            }

            if (visibleCount == 0)
            {
                EditorGUILayout.HelpBox(
                    _cues.Count == 0
                        ? "Battle VFX Cue가 없습니다."
                        : "검색 결과가 없습니다.",
                    MessageType.Info);
            }

            PS260714AssetEditorList.DrawCountFooter(
                visibleCount,
                _cues.Count);
        }
    }

    private void DrawEditor()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.ExpandWidth(true)))
        {
            if (_selected == null || _serialized == null)
            {
                EditorGUILayout.HelpBox(
                    "편집할 Battle VFX Cue를 선택하거나 New로 생성하세요.",
                    MessageType.Info);
                return;
            }

            _serialized.UpdateIfRequiredOrScript();
            using (EditorGUILayout.ScrollViewScope scroll =
                   new(_editorScroll))
            {
                _editorScroll = scroll.scrollPosition;
                DrawValidation();
                DrawIdentity();
                DrawTimeline();
                if (!HasTimelineClips())
                {
                    DrawAnchor();
                    DrawMotion();
                    DrawLifetime();
                }
                DrawPoolAndQuality();
                EditorGUILayout.Space(12f);
            }

            if (_serialized.ApplyModifiedProperties())
            {
                _selected.ValidateDefinition();
                EditorUtility.SetDirty(_selected);
            }
        }
    }

    private void DrawValidation()
    {
        BattleVfxCueValidationResult validation =
            BattleVfxCueValidator.Validate(_selected, _cues);
        if (validation.Diagnostics.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "3D VFX Cue 검증을 통과했습니다.",
                MessageType.Info);
            return;
        }

        foreach (BattleVfxCueDiagnostic diagnostic in
                 validation.Diagnostics)
        {
            MessageType messageType = diagnostic.Severity ==
                                      BattleVfxCueDiagnosticSeverity.Error
                ? MessageType.Error
                : MessageType.Warning;
            string path = string.IsNullOrWhiteSpace(diagnostic.Path)
                ? "<root>"
                : diagnostic.Path;
            EditorGUILayout.HelpBox(
                $"[{diagnostic.Code}] {path}\n{diagnostic.Message}",
                messageType);
        }
    }

    private void DrawIdentity()
    {
        if (!BeginFoldout(ref _identityExpanded, "1. 출력 및 식별자"))
            return;

        SerializedProperty cueId = Find("cueId");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(cueId, new GUIContent("Cue ID"));
            if (GUILayout.Button("ID 재생성", GUILayout.Width(82f)))
            {
                if (EditorUtility.DisplayDialog(
                        "Cue ID 재생성",
                        "이 Cue를 참조하는 외부 저장 데이터가 있다면 연결이 끊어질 수 있습니다.",
                        "재생성",
                        "취소"))
                {
                    Undo.RecordObject(_selected, "Regenerate Battle VFX Cue ID");
                    _selected.RegenerateCueId();
                    EditorUtility.SetDirty(_selected);
                    _serialized.Update();
                }
            }
        }

        EditorGUILayout.PropertyField(
            Find("audioClip"),
            new GUIContent(
                "오디오 클립",
                "Cue 재생 시 함께 출력합니다. 프리팹 없이 오디오 전용 Cue도 가능합니다."));
        if (!HasTimelineClips())
        {
            EditorGUILayout.PropertyField(
                Find("prefab"),
                new GUIContent(
                    "기존 단일 3D 프리팹",
                    "기존 Cue 호환용입니다. 새 Cue는 아래 타임라인 클립을 사용하세요."));
            if (Find("prefab").objectReferenceValue != null &&
                GUILayout.Button("단일 프리팹을 타임라인 클립으로 변환"))
            {
                _serialized.ApplyModifiedProperties();
                Undo.RecordObject(
                    _selected,
                    "Migrate Battle VFX Cue To Timeline");
                if (_selected.MigrateLegacyPrefabToTimeline())
                {
                    EditorUtility.SetDirty(_selected);
                    _serialized.Update();
                    _selectedClipIndex = 0;
                }
                GUIUtility.ExitGUI();
            }
        }
        EndFoldout();
    }

    private void DrawTimeline()
    {
        if (!BeginFoldout(
                ref _timelineExpanded,
                "2. 다중 프리팹 · 10×10 타일 타임라인"))
        {
            return;
        }

        SerializedProperty clips = Find("clips");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                $"클립 {clips.arraySize}개",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("클립 추가", GUILayout.Width(80f)))
            {
                AddTimelineClip(clips);
                GUIUtility.ExitGUI();
            }
        }

        EditorGUILayout.HelpBox(
            "시전자와 대상 모두 10×10이며 전체 사각형은 현재 던전 타일 한 칸입니다. " +
            "Tile Relative 배율은 그리드 크기와 해상도에 따라 자동으로 바뀝니다.",
            MessageType.Info);

        if (clips.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "클립을 추가하면 다중 프리팹 타임라인 방식으로 전환됩니다. " +
                "기존 단일 프리팹 Cue는 그대로 호환됩니다.",
                MessageType.None);
            EndFoldout();
            return;
        }

        _selectedClipIndex = Mathf.Clamp(
            _selectedClipIndex < 0 ? 0 : _selectedClipIndex,
            0,
            clips.arraySize - 1);
        DrawTimelineOverview(clips);
        DrawClipList(clips);

        SerializedProperty selectedClip =
            clips.GetArrayElementAtIndex(_selectedClipIndex);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            $"선택 클립 {_selectedClipIndex + 1}",
            EditorStyles.boldLabel);
        DrawPlacementGrids(selectedClip);
        DrawSelectedClipInspector(selectedClip);
        EndFoldout();
    }

    private void DrawTimelineOverview(SerializedProperty clips)
    {
        float timelineLength = 0.1f;
        for (int index = 0; index < clips.arraySize; index++)
        {
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            float start = clip.FindPropertyRelative("startTime").floatValue;
            float duration = clip.FindPropertyRelative("duration").floatValue;
            timelineLength = Mathf.Max(
                timelineLength,
                start + Mathf.Max(0.01f, duration));
        }

        Rect headerRect = GUILayoutUtility.GetRect(
            100f,
            18f,
            GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, new Color(0.12f, 0.12f, 0.12f));
        GUI.Label(
            headerRect,
            new GUIContent(
                $"0.00초                              {timelineLength:0.00}초"),
            EditorStyles.centeredGreyMiniLabel);

        for (int index = 0; index < clips.arraySize; index++)
        {
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            float start = Mathf.Max(
                0f,
                clip.FindPropertyRelative("startTime").floatValue);
            float duration = Mathf.Max(
                0.01f,
                clip.FindPropertyRelative("duration").floatValue);
            Rect rowRect = GUILayoutUtility.GetRect(
                100f,
                22f,
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(
                rowRect,
                index == _selectedClipIndex
                    ? new Color(0.18f, 0.22f, 0.28f)
                    : new Color(0.15f, 0.15f, 0.15f));
            Rect barRect = new(
                rowRect.x + rowRect.width * start / timelineLength,
                rowRect.y + 3f,
                Mathf.Max(
                    4f,
                    rowRect.width * duration / timelineLength),
                rowRect.height - 6f);
            EditorGUI.DrawRect(
                barRect,
                index == _selectedClipIndex
                    ? new Color(0.25f, 0.65f, 1f)
                    : new Color(0.25f, 0.45f, 0.7f));
            GUI.Label(
                rowRect,
                new GUIContent($"{index + 1}"),
                EditorStyles.miniLabel);
            if (Event.current.type == EventType.MouseDown &&
                rowRect.Contains(Event.current.mousePosition))
            {
                _selectedClipIndex = index;
                Event.current.Use();
                Repaint();
            }
        }
    }

    private void DrawClipList(SerializedProperty clips)
    {
        for (int index = 0; index < clips.arraySize; index++)
        {
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.HorizontalScope(
                       index == _selectedClipIndex
                           ? EditorStyles.helpBox
                           : GUIStyle.none))
            {
                if (GUILayout.Toggle(
                        index == _selectedClipIndex,
                        $"{index + 1}",
                        "Button",
                        GUILayout.Width(28f)))
                {
                    _selectedClipIndex = index;
                }
                EditorGUILayout.PropertyField(
                    clip.FindPropertyRelative("prefab"),
                    GUIContent.none);
                EditorGUILayout.PropertyField(
                    clip.FindPropertyRelative("startTime"),
                    GUIContent.none,
                    GUILayout.Width(55f));
                EditorGUILayout.LabelField("초", GUILayout.Width(14f));
                EditorGUILayout.PropertyField(
                    clip.FindPropertyRelative("duration"),
                    GUIContent.none,
                    GUILayout.Width(55f));
                if (GUILayout.Button("삭제", GUILayout.Width(42f)))
                {
                    DeleteTimelineClip(index);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private void DeleteTimelineClip(int index)
    {
        if (_selected == null || _serialized == null)
            return;

        SerializedProperty clips = Find("clips");
        if (clips == null || index < 0 || index >= clips.arraySize)
            return;

        clips.DeleteArrayElementAtIndex(index);
        int remainingCount = clips.arraySize;
        _serialized.ApplyModifiedProperties();
        _selected.ValidateDefinition();
        EditorUtility.SetDirty(_selected);
        _serialized.Update();

        if (remainingCount == 0)
        {
            _selectedClipIndex = -1;
        }
        else if (_selectedClipIndex > index)
        {
            _selectedClipIndex--;
        }
        else if (_selectedClipIndex == index)
        {
            _selectedClipIndex = Mathf.Min(index, remainingCount - 1);
        }
    }

    private void DrawPlacementGrids(SerializedProperty clip)
    {
        BattleVfxMotionMode motionMode =
            (BattleVfxMotionMode)clip
                .FindPropertyRelative("motionMode")
                .enumValueIndex;
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPlacementGrid(
                clip,
                "시전자 10×10",
                BattleVfxPlacementArea.Caster,
                motionMode != BattleVfxMotionMode.Stationary
                    ? "motionSourceGridPosition"
                    : "gridPosition");
            DrawPlacementGrid(
                clip,
                "대상 10×10",
                BattleVfxPlacementArea.Target,
                "gridPosition");
        }
    }

    private void DrawPlacementGrid(
        SerializedProperty clip,
        string label,
        BattleVfxPlacementArea area,
        string positionPropertyName)
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(GridPreviewSize + 8f)))
        {
            EditorGUILayout.LabelField(
                label,
                EditorStyles.centeredGreyMiniLabel);
            Rect rect = GUILayoutUtility.GetRect(
                GridPreviewSize,
                GridPreviewSize,
                GUILayout.Width(GridPreviewSize),
                GUILayout.Height(GridPreviewSize));
            EditorGUI.DrawRect(rect, new Color(0.09f, 0.1f, 0.12f));
            float cellSize = rect.width / VfxGridDimension;
            Handles.BeginGUI();
            Color previousColor = Handles.color;
            Handles.color = new Color(0.35f, 0.38f, 0.42f, 0.75f);
            for (int index = 0; index <= VfxGridDimension; index++)
            {
                float x = rect.x + cellSize * index;
                float y = rect.y + cellSize * index;
                Handles.DrawLine(
                    new Vector3(x, rect.y),
                    new Vector3(x, rect.yMax));
                Handles.DrawLine(
                    new Vector3(rect.x, y),
                    new Vector3(rect.xMax, y));
            }
            Handles.color = previousColor;
            Handles.EndGUI();

            SerializedProperty position =
                clip.FindPropertyRelative(positionPropertyName);
            Vector2 value = position.vector2Value;
            float markerX = rect.x +
                            Mathf.Clamp01(value.x / VfxGridDimension) *
                            rect.width;
            float markerY = rect.yMax -
                            Mathf.Clamp01(value.y / VfxGridDimension) *
                            rect.height;
            Rect marker = new(markerX - 5f, markerY - 5f, 10f, 10f);
            EditorGUI.DrawRect(marker, new Color(0.2f, 0.75f, 1f));

            Event current = Event.current;
            if (current.type == EventType.MouseDown &&
                current.button == 0 &&
                rect.Contains(current.mousePosition))
            {
                int column = Mathf.Clamp(
                    Mathf.FloorToInt(
                        (current.mousePosition.x - rect.x) / cellSize),
                    0,
                    VfxGridDimension - 1);
                int visualRow = Mathf.Clamp(
                    Mathf.FloorToInt(
                        (current.mousePosition.y - rect.y) / cellSize),
                    0,
                    VfxGridDimension - 1);
                int row = VfxGridDimension - 1 - visualRow;
                position.vector2Value = new Vector2(
                    column + 0.5f,
                    row + 0.5f);
                if (positionPropertyName == "gridPosition" &&
                    (BattleVfxMotionMode)clip
                        .FindPropertyRelative("motionMode")
                        .enumValueIndex ==
                    BattleVfxMotionMode.Stationary)
                {
                    clip.FindPropertyRelative("placementArea")
                        .enumValueIndex = (int)area;
                }
                current.Use();
                Repaint();
            }
        }
    }

    private static void DrawSelectedClipInspector(
        SerializedProperty clip)
    {
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("prefab"),
            new GUIContent("3D 프리팹"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("audioClip"),
            new GUIContent("클립 오디오"));
        SerializedProperty audioVolume =
            clip.FindPropertyRelative("audioVolumePercent");
        audioVolume.intValue = EditorGUILayout.IntSlider(
            new GUIContent(
                "클립 사운드 크기 (0~100)",
                "이 타임라인 클립의 오디오 재생 크기를 설정합니다."),
            audioVolume.intValue,
            0,
            100);
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("required"),
            new GUIContent("필수 출력"));

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(
                clip.FindPropertyRelative("startTime"),
                new GUIContent("시작 시간"));
            EditorGUILayout.PropertyField(
                clip.FindPropertyRelative("duration"),
                new GUIContent("재생 길이"));
        }
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("playbackFit"),
            new GUIContent("길이 맞춤"));

        BattleVfxMotionMode motionMode =
            (BattleVfxMotionMode)clip.FindPropertyRelative("motionMode")
                .enumValueIndex;
        if (motionMode == BattleVfxMotionMode.Stationary)
        {
            EditorGUILayout.PropertyField(
                clip.FindPropertyRelative("placementArea"),
                new GUIContent("배치 영역"));
        }
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("anchorType"),
            new GUIContent("앵커"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("attachMode"),
            new GUIContent("부착 방식"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("gridPosition"),
            new GUIContent("10×10 위치"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("localPosition"),
            new GUIContent("세부 위치"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("localEulerAngles"),
            new GUIContent("회전"));

        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("scaleMode"),
            new GUIContent("스케일 방식"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("uniformScale"),
            new GUIContent("전체 배율"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("localScale"),
            new GUIContent("축별 배율"));

        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("motionMode"),
            new GUIContent("이동 방식"));
        motionMode =
            (BattleVfxMotionMode)clip.FindPropertyRelative("motionMode")
                .enumValueIndex;
        if (motionMode != BattleVfxMotionMode.Stationary)
        {
            EditorGUILayout.PropertyField(
                clip.FindPropertyRelative("motionSourceGridPosition"),
                new GUIContent("시전자 출발 위치"));
            EditorGUILayout.PropertyField(
                clip.FindPropertyRelative("travelDuration"),
                new GUIContent("이동 시간"));
            if (motionMode == BattleVfxMotionMode.Arc)
            {
                EditorGUILayout.PropertyField(
                    clip.FindPropertyRelative("arcHeight"),
                    new GUIContent("포물선 높이"));
            }
            EditorGUILayout.PropertyField(
                clip.FindPropertyRelative("faceMotionDirection"),
                new GUIContent("진행 방향 바라보기"));
        }

        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("lifetimeMode"),
            new GUIContent("수명 방식"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("stopMode"),
            new GUIContent("종료 방식"));
        EditorGUILayout.PropertyField(
            clip.FindPropertyRelative("useBattleTime"),
            new GUIContent("전투 시간 사용"));
    }

    private void AddTimelineClip(SerializedProperty clips)
    {
        _serialized.ApplyModifiedProperties();
        Undo.RecordObject(_selected, "Add Battle VFX Timeline Clip");
        clips = Find("clips");
        int index = clips.arraySize;
        clips.InsertArrayElementAtIndex(index);
        SerializedProperty clip = clips.GetArrayElementAtIndex(index);
        clip.FindPropertyRelative("clipId").stringValue =
            Guid.NewGuid().ToString("N");
        clip.FindPropertyRelative("prefab").objectReferenceValue = null;
        clip.FindPropertyRelative("audioClip").objectReferenceValue = null;
        clip.FindPropertyRelative("audioVolumePercent").intValue = 100;
        clip.FindPropertyRelative("required").boolValue = true;
        clip.FindPropertyRelative("startTime").floatValue = 0f;
        clip.FindPropertyRelative("duration").floatValue = 1f;
        clip.FindPropertyRelative("playbackFit").enumValueIndex =
            (int)BattleVfxPlaybackFit.Natural;
        clip.FindPropertyRelative("placementArea").enumValueIndex =
            (int)BattleVfxPlacementArea.Target;
        clip.FindPropertyRelative("anchorType").enumValueIndex =
            (int)BattleVfxAnchorType.Center;
        clip.FindPropertyRelative("attachMode").enumValueIndex =
            (int)BattleVfxAttachMode.SpawnAtAnchor;
        clip.FindPropertyRelative("gridPosition").vector2Value =
            new Vector2(5f, 5f);
        clip.FindPropertyRelative("localPosition").vector3Value =
            Vector3.zero;
        clip.FindPropertyRelative("localEulerAngles").vector3Value =
            Vector3.zero;
        clip.FindPropertyRelative("scaleMode").enumValueIndex =
            (int)BattleVfxScaleMode.TileRelative;
        clip.FindPropertyRelative("uniformScale").floatValue = 1f;
        clip.FindPropertyRelative("localScale").vector3Value =
            Vector3.one;
        clip.FindPropertyRelative("motionMode").enumValueIndex =
            (int)BattleVfxMotionMode.Stationary;
        clip.FindPropertyRelative("motionSourceGridPosition")
            .vector2Value = new Vector2(5f, 5f);
        clip.FindPropertyRelative("travelDuration").floatValue = 0.25f;
        clip.FindPropertyRelative("arcHeight").floatValue = 0.5f;
        clip.FindPropertyRelative("faceMotionDirection").boolValue = true;
        clip.FindPropertyRelative("lifetimeMode").enumValueIndex =
            (int)BattleVfxLifetimeMode.Timed;
        clip.FindPropertyRelative("stopMode").enumValueIndex =
            (int)BattleVfxStopMode.StopEmission;
        clip.FindPropertyRelative("useBattleTime").boolValue = true;
        _serialized.ApplyModifiedProperties();
        _selected.ValidateDefinition();
        EditorUtility.SetDirty(_selected);
        _serialized.Update();
        _selectedClipIndex = index;
    }

    private bool HasTimelineClips()
    {
        SerializedProperty clips = Find("clips");
        return clips != null && clips.arraySize > 0;
    }

    private void DrawAnchor()
    {
        if (!BeginFoldout(ref _anchorExpanded, "3. 기존 대상 앵커 및 변환"))
            return;

        EditorGUILayout.PropertyField(
            Find("anchorType"),
            new GUIContent("앵커"));
        EditorGUILayout.PropertyField(
            Find("attachMode"),
            new GUIContent(
                "부착 방식",
                "Follow Target은 대상이 이동할 때 매 프레임 앵커를 다시 계산합니다."));
        EditorGUILayout.PropertyField(
            Find("localPosition"),
            new GUIContent("로컬 위치"));
        EditorGUILayout.PropertyField(
            Find("localEulerAngles"),
            new GUIContent("로컬 회전"));
        EditorGUILayout.PropertyField(
            Find("localScale"),
            new GUIContent("로컬 배율"));
        EndFoldout();
    }

    private void DrawLifetime()
    {
        if (!BeginFoldout(ref _lifetimeExpanded, "5. 기존 수명 및 종료"))
            return;

        SerializedProperty lifetimeMode = Find("lifetimeMode");
        EditorGUILayout.PropertyField(
            lifetimeMode,
            new GUIContent("수명 방식"));

        BattleVfxLifetimeMode mode =
            (BattleVfxLifetimeMode)lifetimeMode.enumValueIndex;
        string durationLabel = mode switch
        {
            BattleVfxLifetimeMode.ParticleSystem => "최소 유지 시간",
            BattleVfxLifetimeMode.Persistent => "종료 잔류 시간",
            _ => "유지 시간"
        };
        EditorGUILayout.PropertyField(
            Find("duration"),
            new GUIContent(durationLabel));

        EditorGUILayout.PropertyField(
            Find("stopMode"),
            new GUIContent(
                "종료 방식",
                "Stop Emission은 파티클 방출을 멈춘 뒤 잔류 시간 동안 기다립니다."));
        EditorGUILayout.PropertyField(
            Find("useBattleTime"),
            new GUIContent(
                "전투 시간 사용",
                "끄면 일시정지와 무관한 unscaled time을 사용합니다."));

        if (mode == BattleVfxLifetimeMode.Persistent)
        {
            EditorGUILayout.HelpBox(
                "Persistent Cue는 같은 Cue와 대상 조합을 한 개만 유지하며, " +
                "Status Loop Stop 요청에서 종료됩니다.",
                MessageType.Info);
        }
        EndFoldout();
    }

    private void DrawPoolAndQuality()
    {
        if (!BeginFoldout(ref _poolExpanded, "6. 풀 및 품질"))
            return;

        EditorGUILayout.PropertyField(
            Find("prewarmCount"),
            new GUIContent("사전 생성 수"));
        EditorGUILayout.PropertyField(
            Find("maximumConcurrent"),
            new GUIContent("동시 재생 제한"));
        EditorGUILayout.PropertyField(
            Find("importance"),
            new GUIContent(
                "중요도",
                "후속 품질 단계에서 이펙트 생략 우선순위를 결정하는 값입니다."));
        EndFoldout();
    }

    private void DrawMotion()
    {
        if (!BeginFoldout(ref _motionExpanded, "4. 기존 투사체 이동"))
            return;

        SerializedProperty motionMode = Find("motionMode");
        EditorGUILayout.PropertyField(
            motionMode,
            new GUIContent(
                "이동 방식",
                "Stationary는 대상 앵커에 고정하고 Linear/Arc는 소스에서 대상으로 이동합니다."));

        BattleVfxMotionMode mode =
            (BattleVfxMotionMode)motionMode.enumValueIndex;
        if (mode == BattleVfxMotionMode.Stationary)
        {
            EditorGUILayout.HelpBox(
                "시전·적중·상태 이펙트는 일반적으로 Stationary를 사용합니다.",
                MessageType.Info);
            EndFoldout();
            return;
        }

        EditorGUILayout.PropertyField(
            Find("motionSourceAnchorType"),
            new GUIContent("출발 앵커"));
        EditorGUILayout.PropertyField(
            Find("travelDuration"),
            new GUIContent("이동 시간"));
        if (mode == BattleVfxMotionMode.Arc)
        {
            EditorGUILayout.PropertyField(
                Find("arcHeight"),
                new GUIContent("포물선 높이"));
        }
        EditorGUILayout.PropertyField(
            Find("faceMotionDirection"),
            new GUIContent("진행 방향 바라보기"));
        EndFoldout();
    }

    private static bool BeginFoldout(ref bool expanded, string label)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        expanded = EditorGUILayout.Foldout(
            expanded,
            label,
            true,
            EditorStyles.foldoutHeader);
        if (!expanded)
        {
            EditorGUILayout.EndVertical();
            return false;
        }

        return true;
    }

    private static void EndFoldout()
    {
        EditorGUILayout.EndVertical();
    }

    private SerializedProperty Find(string propertyName)
    {
        return _serialized.FindProperty(propertyName);
    }

    private bool MatchesSearch(BattleVfxCueSO cue)
    {
        string search = (_searchText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(search))
            return true;

        return cue.name.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               cue.CueId.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               (cue.Prefab != null &&
                cue.Prefab.name.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static Texture GetPreviewTexture(BattleVfxCueSO cue)
    {
        if (cue == null || cue.Prefab == null)
            return null;
        return PS260714AssetEditorList.GetAssetPreview(
            cue.Prefab);
    }

    private void SelectCue(BattleVfxCueSO cue)
    {
        if (cue == null)
            return;

        if (!ReferenceEquals(_selected, cue))
            CancelRename();
        _selected = cue;
        _serialized = new SerializedObject(cue);
        _selectedClipIndex = -1;
        _editorScroll = Vector2.zero;
        Repaint();
    }

    private void RefreshList()
    {
        string selectedPath =
            PS260714EditorAssetUtility.CapturePath(_selected);
        PS260714EditorAssetUtility.LoadAssets(
            _cues,
            "t:BattleVfxCueSO");
        BattleVfxCueSO next =
            PS260714EditorAssetUtility.RestoreSelection(
                selectedPath,
                _cues);
        if (next != null)
            SelectCue(next);
        else
            ClearSelection();
    }

    private static List<TAsset> LoadAllAssets<TAsset>()
        where TAsset : UnityEngine.Object
    {
        List<TAsset> assets = new();
        PS260714EditorAssetUtility.LoadAssets(
            assets,
            $"t:{typeof(TAsset).Name}");
        return assets;
    }

    private void CreateCue()
    {
        EnsureFolder(AssetFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Battle VFX Cue",
            "NewBattleVfxCue",
            "asset",
            "Battle VFX Cue SO를 생성할 위치를 선택하세요.",
            AssetFolder);
        if (string.IsNullOrEmpty(path))
            return;

        BattleVfxCueSO cue = CreateInstance<BattleVfxCueSO>();
        cue.name = Path.GetFileNameWithoutExtension(path);
        cue.RegenerateCueId();
        cue.ValidateDefinition();
        AssetDatabase.CreateAsset(cue, path);
        AssetDatabase.SaveAssetIfDirty(cue);
        RefreshList();
        SelectCue(cue);
        EditorGUIUtility.PingObject(cue);
    }

    private void SaveSelected()
    {
        if (_selected == null)
            return;

        _serialized?.ApplyModifiedProperties();
        _selected.ValidateDefinition();
        EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssetIfDirty(_selected);
        ShowNotification(new GUIContent($"Saved {_selected.name}.asset"));
    }

    private void DuplicateSelected()
    {
        if (_selected == null)
            return;

        if (!PS260714EditorAssetUtility.TryDuplicate(
                _selected,
                null,
                " Copy",
                out BattleVfxCueSO duplicate,
                out string duplicateError))
        {
            EditorUtility.DisplayDialog(
                "Duplicate Battle VFX Cue",
                duplicateError,
                "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        if (duplicate != null)
        {
            duplicate.RegenerateCueId();
            duplicate.ValidateDefinition();
            EditorUtility.SetDirty(duplicate);
            AssetDatabase.SaveAssetIfDirty(duplicate);
        }

        RefreshList();
        if (duplicate != null)
            SelectCue(duplicate);
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        string assetName = _selected.name;
        if (!PS260714SafeAssetDelete.TryMoveToTrash(
                _selected,
                "Battle VFX Cue"))
            return;

        ClearSelection();
        CancelRename();
        RefreshList();
        ShowNotification(new GUIContent(
            $"Moved {assetName}.asset to Trash"));
    }

    private void BeginRename()
    {
        if (_selected == null)
            return;

        _renameAssetName = Path.GetFileNameWithoutExtension(
            AssetDatabase.GetAssetPath(_selected));
        _isRenaming = true;
        _focusRenameField = true;
        Repaint();
    }

    private void CancelRename()
    {
        _isRenaming = false;
        _focusRenameField = false;
        _renameAssetName = string.Empty;
    }

    private void RenameSelected()
    {
        if (_selected == null)
        {
            CancelRename();
            return;
        }

        if (!PS260714EditorAssetUtility.TryRename(
                _selected,
                _renameAssetName,
                out string renameError))
        {
            EditorUtility.DisplayDialog(
                "Rename Battle VFX Cue",
                renameError,
                "확인");
            _focusRenameField = true;
            return;
        }

        CancelRename();
        RefreshList();
        EditorGUIUtility.PingObject(_selected);
    }

    private void ClearSelection()
    {
        _selected = null;
        _serialized = null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = $"{current}/{parts[index]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}
