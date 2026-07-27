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

    private const string AssetFolder = "Assets/Resources/BattleVfx";
    private const string RenameControlName = "BattleVfxRenameField";
    private const float ListWidth = 240f;

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
    private bool _anchorExpanded = true;
    private bool _motionExpanded = true;
    private bool _lifetimeExpanded = true;
    private bool _poolExpanded = true;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        BattleVfxEditorWindow window =
            GetWindow<BattleVfxEditorWindow>();
        window.titleContent = new GUIContent("Battle VFX");
        window.minSize = new Vector2(820f, 520f);
        window.Show();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(ValidateMenuPath)]
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

    [MenuItem(ValidateMenuPath, true)]
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
            RefreshList);
    }

    private void DrawRenameRow()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("SO 파일 이름", GUILayout.Width(90f));
            GUI.SetNextControlName(RenameControlName);
            _renameAssetName = EditorGUILayout.TextField(_renameAssetName);
            bool apply = GUILayout.Button("확인", GUILayout.Width(44f));
            bool cancel = GUILayout.Button("취소", GUILayout.Width(48f));

            if (_focusRenameField)
            {
                EditorGUI.FocusTextInControl(RenameControlName);
                _focusRenameField = false;
            }

            Event current = Event.current;
            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Return ||
                    current.keyCode == KeyCode.KeypadEnter)
                {
                    apply = true;
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Escape)
                {
                    cancel = true;
                    current.Use();
                }
            }

            if (cancel)
            {
                CancelRename();
                GUIUtility.ExitGUI();
            }
            if (apply)
            {
                RenameSelected();
                GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawAssetList()
    {
        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox,
                   GUILayout.Width(ListWidth),
                   GUILayout.ExpandHeight(true)))
        {
            _searchText = EditorGUILayout.TextField(
                _searchText,
                EditorStyles.toolbarSearchField);

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
                    GUIStyle style = selected
                        ? EditorStyles.miniButtonMid
                        : EditorStyles.miniButton;
                    GUIContent content = new(
                        cue.name,
                        GetPreviewTexture(cue),
                        cue.CueId);
                    if (GUILayout.Button(
                            content,
                            style,
                            GUILayout.Height(30f)))
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

            EditorGUILayout.LabelField(
                $"{visibleCount} / {_cues.Count}",
                EditorStyles.centeredGreyMiniLabel);
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
                DrawAnchor();
                DrawMotion();
                DrawLifetime();
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
            Find("prefab"),
            new GUIContent(
                "3D 프리팹",
                "월드 공간에 풀링되어 재생되는 3D 프리팹입니다."));
        EditorGUILayout.PropertyField(
            Find("audioClip"),
            new GUIContent(
                "오디오 클립",
                "Cue 재생 시 함께 출력합니다. 프리팹 없이 오디오 전용 Cue도 가능합니다."));
        EndFoldout();
    }

    private void DrawAnchor()
    {
        if (!BeginFoldout(ref _anchorExpanded, "2. 대상 앵커 및 변환"))
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
        if (!BeginFoldout(ref _lifetimeExpanded, "4. 수명 및 종료"))
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
        if (!BeginFoldout(ref _poolExpanded, "5. 풀 및 품질"))
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
        if (!BeginFoldout(ref _motionExpanded, "3. 투사체 이동"))
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
        return AssetPreview.GetMiniThumbnail(cue.Prefab);
    }

    private void SelectCue(BattleVfxCueSO cue)
    {
        if (cue == null)
            return;

        if (!ReferenceEquals(_selected, cue))
            CancelRename();
        _selected = cue;
        _serialized = new SerializedObject(cue);
        _editorScroll = Vector2.zero;
        Repaint();
    }

    private void RefreshList()
    {
        string selectedPath = _selected != null
            ? AssetDatabase.GetAssetPath(_selected)
            : string.Empty;
        _cues.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:BattleVfxCueSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleVfxCueSO cue =
                AssetDatabase.LoadAssetAtPath<BattleVfxCueSO>(path);
            if (cue != null)
                _cues.Add(cue);
        }

        _cues.Sort((left, right) => string.Compare(
            left.name,
            right.name,
            StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(selectedPath))
        {
            BattleVfxCueSO restored =
                AssetDatabase.LoadAssetAtPath<BattleVfxCueSO>(selectedPath);
            if (restored != null)
                SelectCue(restored);
            else
                ClearSelection();
        }
        else if (_selected == null && _cues.Count > 0)
        {
            SelectCue(_cues[0]);
        }
    }

    private static List<TAsset> LoadAllAssets<TAsset>()
        where TAsset : UnityEngine.Object
    {
        List<TAsset> assets = new();
        foreach (string guid in
                 AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
            if (asset != null)
                assets.Add(asset);
        }

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

        string sourcePath = AssetDatabase.GetAssetPath(_selected);
        string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{fileName} Copy.asset");
        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            return;

        AssetDatabase.SaveAssets();
        BattleVfxCueSO duplicate =
            AssetDatabase.LoadAssetAtPath<BattleVfxCueSO>(destinationPath);
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

        string path = AssetDatabase.GetAssetPath(_selected);
        string assetName = _selected.name;
        if (!EditorUtility.DisplayDialog(
                "Delete Battle VFX Cue",
                $"'{assetName}' SO 파일을 삭제합니다.\n\n{path}\n\n" +
                "이 작업은 Unity Undo로 복구할 수 없습니다.",
                "삭제",
                "취소"))
        {
            return;
        }

        if (!AssetDatabase.DeleteAsset(path))
        {
            EditorUtility.DisplayDialog(
                "Delete Battle VFX Cue",
                "SO 파일을 삭제하지 못했습니다.",
                "확인");
            return;
        }

        ClearSelection();
        CancelRename();
        RefreshList();
        ShowNotification(new GUIContent($"Deleted {assetName}.asset"));
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

        string newName = (_renameAssetName ?? string.Empty).Trim();
        if (newName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            newName = newName.Substring(0, newName.Length - 6).Trim();
        if (!IsValidFileName(newName, out string error))
        {
            EditorUtility.DisplayDialog(
                "Rename Battle VFX Cue",
                error,
                "확인");
            _focusRenameField = true;
            return;
        }

        string path = AssetDatabase.GetAssetPath(_selected);
        string renameError = AssetDatabase.RenameAsset(path, newName);
        if (!string.IsNullOrEmpty(renameError))
        {
            EditorUtility.DisplayDialog(
                "Rename Battle VFX Cue",
                renameError,
                "확인");
            _focusRenameField = true;
            return;
        }

        CancelRename();
        AssetDatabase.SaveAssets();
        RefreshList();
        EditorGUIUtility.PingObject(_selected);
    }

    private void ClearSelection()
    {
        _selected = null;
        _serialized = null;
    }

    private static bool IsValidFileName(
        string fileName,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            error = "파일 이름을 입력하세요.";
            return false;
        }
        if (fileName == "." || fileName == ".." ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.IndexOf('/') >= 0 ||
            fileName.IndexOf('\\') >= 0 ||
            fileName.EndsWith(".", StringComparison.Ordinal) ||
            fileName.EndsWith(" ", StringComparison.Ordinal))
        {
            error = "사용할 수 없는 파일 이름입니다.";
            return false;
        }

        error = string.Empty;
        return true;
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
