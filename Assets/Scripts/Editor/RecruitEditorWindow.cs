using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RecruitEditorWindow : EditorWindow
{
    private const float BannerListWidth = 230f;
    private const float BannerRatioTolerance = 0.01f;
    private const int MaximumSimulationCount = 1000000;

    private MainSubPage _targetPage;
    private SerializedObject _serializedTarget;
    private int _selectedBannerIndex;
    private Vector2 _bannerListScroll;
    private Vector2 _detailsScroll;
    private bool _basicExpanded = true;
    private bool _poolExpanded = true;
    private readonly bool[] _gradePoolExpanded =
        { true, true, true, true };
    private bool _paymentExpanded = true;
    private bool _validationExpanded = true;
    private bool _useFixedSeed = true;
    private int _simulationSeed = 260714;
    private int _simulationCount = 10000;
    private string _simulationSummary = string.Empty;
    private readonly List<SimulationResult> _simulationResults = new();
    [SerializeField] private int _revealPreviewCount;

    [MenuItem(PS260714EditorMenu.RecruitEditor)]
    public static void Open()
    {
        RecruitEditorWindow window = GetWindow<RecruitEditorWindow>();
        window.titleContent = new GUIContent("Recruit Editor");
        window.minSize = new Vector2(980f, 640f);
        window.Show();
        window.Focus();
    }

    [MenuItem(PS260714EditorMenu.RecruitEditor, true)]
    private static bool ValidateOpen()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        FindRecruitPage();
        EditorApplication.delayCall += SyncPreviewAfterReload;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        EditorApplication.delayCall -= SyncPreviewAfterReload;
    }

    private void SyncPreviewAfterReload()
    {
        if (this == null ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            _targetPage == null)
        {
            return;
        }
        SyncTargetPreview(_revealPreviewCount, false);
    }

    private void HandleHierarchyChanged()
    {
        if (_targetPage == null)
            FindRecruitPage();
        Repaint();
    }

    private void OnGUI()
    {
        DrawTargetToolbar();
        if (_targetPage == null)
        {
            EditorGUILayout.HelpBox(
                "현재 열린 씬에서 모집 페이지를 찾지 못했습니다. " +
                "ClientScene을 연 뒤 '자동 찾기'를 눌러 주세요.",
                MessageType.Info);
            return;
        }

        DrawScenePreviewToolbar();
        if (_targetPage.EnsureRecruitRewardPoolData())
        {
            EditorUtility.SetDirty(_targetPage);
            EditorSceneManager.MarkSceneDirty(
                _targetPage.gameObject.scene);
            _serializedTarget =
                new SerializedObject(_targetPage);
        }
        EnsureSerializedTarget();
        if (_serializedTarget == null || !IsRecruitPage(_targetPage))
        {
            EditorGUILayout.HelpBox(
                "선택한 MainSubPage가 모집 페이지가 아닙니다.",
                MessageType.Error);
            return;
        }

        _serializedTarget.UpdateIfRequiredOrScript();
        SerializedProperty pages =
            _serializedTarget.FindProperty("recruitBannerPages");
        if (pages == null || !pages.isArray)
        {
            EditorGUILayout.HelpBox(
                "recruitBannerPages 직렬화 필드를 찾지 못했습니다.",
                MessageType.Error);
            return;
        }

        if (pages.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "등록된 모집 배너가 없습니다.",
                MessageType.Warning);
            if (GUILayout.Button("기본 배너 생성", GUILayout.Height(32f)))
                AddBanner(pages);
            return;
        }

        _selectedBannerIndex = Mathf.Clamp(
            _selectedBannerIndex,
            0,
            pages.arraySize - 1);

        EditorGUI.BeginChangeCheck();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawBannerList(pages);
            DrawSelectedBanner(pages);
        }

        bool guiChanged = EditorGUI.EndChangeCheck();
        bool applied = _serializedTarget.ApplyModifiedProperties();
        if (guiChanged || applied)
            MarkTargetDirty();
    }

    private void DrawTargetToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            MainSubPage selected = EditorGUILayout.ObjectField(
                _targetPage,
                typeof(MainSubPage),
                true,
                GUILayout.MinWidth(300f)) as MainSubPage;
            if (EditorGUI.EndChangeCheck())
                SetTarget(selected);

            if (GUILayout.Button(
                    "자동 찾기",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(72f)))
            {
                FindRecruitPage();
            }

            using (new EditorGUI.DisabledScope(_targetPage == null))
            {
                if (GUILayout.Button(
                        "선택",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(52f)))
                {
                    Selection.activeObject = _targetPage;
                    EditorGUIUtility.PingObject(_targetPage);
                }

                if (GUILayout.Button(
                        "씬 저장",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(64f)))
                {
                    SaveTargetScene();
                }
            }
        }
    }

    private void DrawScenePreviewToolbar()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope(
                   EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "씬 프리뷰",
                EditorStyles.boldLabel,
                GUILayout.Width(72f));
            if (GUILayout.Button("선택 배너 반영"))
                SyncTargetPreview(_revealPreviewCount, true);
            if (GUILayout.Button("결과 1회"))
                SyncTargetPreview(1, true);
            if (GUILayout.Button("결과 10회"))
                SyncTargetPreview(10, true);
            if (GUILayout.Button("결과 숨김"))
                SyncTargetPreview(0, true);
        }

        EditorGUILayout.HelpBox(
            "고정 UI는 씬 오브젝트로 저장됩니다. 위치·크기·색상은 " +
            "Hierarchy에서 수정하고, 이 창에서는 표시 데이터만 " +
            "동기화합니다.",
            MessageType.Info);
    }

    private void DrawBannerList(SerializedProperty pages)
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(BannerListWidth)))
        {
            EditorGUILayout.LabelField(
                $"모집 배너 ({pages.arraySize})",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+", GUILayout.Width(34f)))
                    AddBanner(pages);
                if (GUILayout.Button("복제"))
                    DuplicateBanner(pages);
                if (GUILayout.Button("삭제"))
                    DeleteBanner(pages);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           _selectedBannerIndex <= 0))
                {
                    if (GUILayout.Button("위로"))
                        MoveBanner(pages, -1);
                }

                using (new EditorGUI.DisabledScope(
                           _selectedBannerIndex >= pages.arraySize - 1))
                {
                    if (GUILayout.Button("아래로"))
                        MoveBanner(pages, 1);
                }
            }

            _bannerListScroll = EditorGUILayout.BeginScrollView(
                _bannerListScroll,
                GUI.skin.box);
            for (int index = 0; index < pages.arraySize; index++)
            {
                SerializedProperty banner =
                    pages.GetArrayElementAtIndex(index);
                string bannerId = GetTrimmedString(banner, "bannerId");
                string label =
                    $"{index + 1:00}  {Fallback(bannerId, "banner")}";

                Color previous = GUI.backgroundColor;
                if (index == _selectedBannerIndex)
                {
                    GUI.backgroundColor =
                        new Color(0.42f, 0.86f, 0.76f, 1f);
                }

                if (GUILayout.Button(
                        label,
                        GUILayout.Height(36f),
                        GUILayout.ExpandWidth(true)))
                {
                    _selectedBannerIndex = index;
                    ClearSimulation();
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = previous;
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSelectedBanner(SerializedProperty pages)
    {
        SerializedProperty banner =
            pages.GetArrayElementAtIndex(_selectedBannerIndex);
        using (new EditorGUILayout.VerticalScope())
        {
            _detailsScroll = EditorGUILayout.BeginScrollView(
                _detailsScroll);
            DrawBasicSettings(banner);
            EditorGUILayout.Space(8f);
            DrawDummyPool(banner);
            EditorGUILayout.Space(8f);
            DrawPaymentSettings(banner);
            EditorGUILayout.Space(8f);
            DrawValidation(pages, banner);
            EditorGUILayout.Space(16f);
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawBasicSettings(SerializedProperty banner)
    {
        _basicExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _basicExpanded,
            "1. 배너 기본 설정 및 이미지");
        if (_basicExpanded)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawProperty(banner, "bannerId", "배너 ID");
                DrawProperty(banner, "ticketGroupId", "모집권 그룹 ID");
                DrawProperty(banner, "bannerArt", "배경 배너 이미지");
                DrawProperty(banner, "totalRecruitCount", "누적 모집 횟수");
                DrawProperty(banner, "currentStack", "현재 스택");
                DrawProperty(banner, "maximumStack", "최대 스택");
                DrawProperty(banner, "interactionEnabled", "버튼 상호작용");

                EditorGUILayout.Space(6f);
                DrawBannerImagePreview(
                    banner.FindPropertyRelative("bannerArt"));
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawBannerImagePreview(
        SerializedProperty artProperty)
    {
        const float targetRatio = RecruitBannerView.BannerArtAspectRatio;
        Sprite sprite = artProperty?.objectReferenceValue as Sprite;

        EditorGUILayout.LabelField(
            "목표 비율",
            $"A계열 가로형 √2:1 ({targetRatio:0.###}:1)");
        EditorGUILayout.HelpBox(
            "런타임에서는 모집 영역 전체를 채우는 배경형 Cover 방식으로 표시됩니다. " +
            "화면 비율에 따라 이미지 위·아래 일부가 잘릴 수 있습니다.",
            MessageType.Info);
        if (sprite == null)
        {
            EditorGUILayout.HelpBox(
                "이미지를 지정하지 않았습니다. 런타임에는 빈 배너 영역이 표시됩니다.",
                MessageType.Info);
            DrawPreviewFrame(null, targetRatio);
            return;
        }

        float width = sprite.rect.width;
        float height = sprite.rect.height;
        float ratio = height > 0f ? width / height : 0f;
        float relativeDifference = targetRatio > 0f
            ? Mathf.Abs(ratio - targetRatio) / targetRatio
            : 0f;

        EditorGUILayout.LabelField(
            "원본 이미지",
            $"{width:0} × {height:0}px  ({ratio:0.###}:1)");
        if (relativeDifference > BannerRatioTolerance)
        {
            EditorGUILayout.HelpBox(
                $"원본 비율이 목표와 {relativeDifference * 100f:0.#}% 다릅니다. " +
                "이미지 전체를 유지하면 여백이 생깁니다.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "원본 이미지가 A계열 목표 비율과 일치합니다.",
                MessageType.Info);
        }

        Texture preview = AssetPreview.GetAssetPreview(sprite);
        if (preview == null)
            preview = AssetPreview.GetMiniThumbnail(sprite);
        DrawPreviewFrame(preview, targetRatio);
    }

    private static void DrawPreviewFrame(Texture texture, float aspect)
    {
        Rect available = GUILayoutUtility.GetRect(
            120f,
            330f,
            GUILayout.ExpandWidth(true));
        float width = Mathf.Min(
            available.width,
            available.height * aspect);
        float height = width / aspect;
        Rect frame = new(
            available.x + (available.width - width) * 0.5f,
            available.y + (available.height - height) * 0.5f,
            width,
            height);

        EditorGUI.DrawRect(frame, new Color(0.06f, 0.075f, 0.07f, 1f));
        if (texture != null)
            GUI.DrawTexture(frame, texture, ScaleMode.ScaleToFit, true);
        Handles.DrawSolidRectangleWithOutline(
            frame,
            Color.clear,
            new Color(0.25f, 0.76f, 0.68f, 1f));
    }

    private void DrawDummyPool(SerializedProperty banner)
    {
        _poolExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _poolExpanded,
            "2. 등급별 모집 보상 풀 및 확률 시뮬레이션");
        if (!_poolExpanded)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "먼저 0~3등급 확률을 결정한 뒤, 선택된 등급 안에서 실제 보상을 추첨합니다. " +
                "각 등급에는 캐릭터와 아이템을 여러 개 넣을 수 있습니다.",
                MessageType.Info);

            SerializedProperty mode =
                banner.FindPropertyRelative("rateInputMode");
            SerializedProperty gradePools =
                banner.FindPropertyRelative("gradePools");
            EditorGUILayout.PropertyField(
                mode,
                new GUIContent("등급 확률 입력 방식"));

            bool valid = TryBuildProbabilityRows(
                banner,
                out List<ProbabilityRow> probabilityRows,
                out string probabilityError,
                out double inputTotal);
            string totalLabel = mode.enumValueIndex ==
                                (int)RecruitRateInputMode.Percentage
                ? $"{inputTotal:0.####}%"
                : $"{inputTotal:0.####}";
            EditorGUILayout.LabelField(
                "등급 확률 합계",
                totalLabel,
                EditorStyles.boldLabel);

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(
                "등급 확률",
                EditorStyles.boldLabel);
            for (int poolIndex = 0;
                 poolIndex < gradePools.arraySize;
                 poolIndex++)
            {
                SerializedProperty gradePool =
                    gradePools.GetArrayElementAtIndex(poolIndex);
                CharacterGrade grade = GetGradePoolGrade(
                    gradePool,
                    poolIndex);
                using (new EditorGUILayout.HorizontalScope(
                           EditorStyles.helpBox))
                {
                    DrawGradeSwatch(grade);
                    EditorGUILayout.LabelField(
                        $"{(int)grade}등급",
                        EditorStyles.boldLabel,
                        GUILayout.Width(70f));
                    EditorGUILayout.PropertyField(
                        gradePool.FindPropertyRelative("rate"),
                        GUIContent.none);
                    float gradeRate = Mathf.Max(
                        0f,
                        gradePool.FindPropertyRelative("rate").floatValue);
                    EditorGUILayout.LabelField(
                        inputTotal > 0d
                            ? $"실제 {gradeRate / inputTotal * 100d:0.####}%"
                            : "실제 0%",
                        GUILayout.Width(105f));
                    SerializedProperty rewards =
                        gradePool.FindPropertyRelative("rewards");
                    EditorGUILayout.LabelField(
                        $"{rewards.arraySize}개 보상",
                        GUILayout.Width(80f));
                }
            }

            int probabilityCursor = 0;
            for (int poolIndex = 0;
                 poolIndex < gradePools.arraySize;
                 poolIndex++)
            {
                SerializedProperty gradePool =
                    gradePools.GetArrayElementAtIndex(poolIndex);
                CharacterGrade grade = GetGradePoolGrade(
                    gradePool,
                    poolIndex);
                SerializedProperty rewards =
                    gradePool.FindPropertyRelative("rewards");
                int gradeIndex = Mathf.Clamp((int)grade, 0, 3);

                EditorGUILayout.Space(7f);
                CharacterGradeStyle style =
                    CharacterGradePresentation.GetStyle(grade);
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = style.PrimaryColor;
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.backgroundColor = previousColor;
                    _gradePoolExpanded[gradeIndex] =
                        EditorGUILayout.Foldout(
                            _gradePoolExpanded[gradeIndex],
                            $"{gradeIndex}등급 보상 ({rewards.arraySize})",
                            true,
                            EditorStyles.foldoutHeader);
                    if (_gradePoolExpanded[gradeIndex])
                    {
                        DrawGradePalettePreview(grade);
                        EditorGUILayout.PropertyField(
                            gradePool.FindPropertyRelative(
                                "selectionMode"),
                            new GUIContent("등급 내부 선택 방식"));

                        RecruitRewardSelectionMode selectionMode =
                            (RecruitRewardSelectionMode)gradePool
                                .FindPropertyRelative("selectionMode")
                                .enumValueIndex;
                        EditorGUILayout.HelpBox(
                            selectionMode ==
                            RecruitRewardSelectionMode.Equal
                                ? "이 등급의 모든 보상을 같은 확률로 추첨합니다."
                                : "각 보상의 개별 가중치 비율로 추첨합니다.",
                            MessageType.None);

                        DrawGradePoolRewards(
                            gradePools,
                            poolIndex,
                            grade,
                            selectionMode,
                            valid,
                            probabilityRows,
                            ref probabilityCursor);
                        DrawBulkRewardControls(
                            gradePools,
                            poolIndex,
                            grade);
                    }
                    else
                    {
                        probabilityCursor += rewards.arraySize;
                    }
                }
            }

            if (!valid)
            {
                EditorGUILayout.HelpBox(
                    probabilityError,
                    MessageType.Error);
            }

            DrawSimulationControls(
                valid,
                probabilityRows,
                probabilityError);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawGradePoolRewards(
        SerializedProperty gradePools,
        int poolIndex,
        CharacterGrade grade,
        RecruitRewardSelectionMode selectionMode,
        bool probabilitiesValid,
        IReadOnlyList<ProbabilityRow> probabilityRows,
        ref int probabilityCursor)
    {
        SerializedProperty gradePool =
            gradePools.GetArrayElementAtIndex(poolIndex);
        SerializedProperty rewards =
            gradePool.FindPropertyRelative("rewards");
        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;

        if (rewards.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "등록된 보상이 없습니다. 아래에서 여러 SO를 한 번에 추가할 수 있습니다.",
                MessageType.Warning);
        }

        for (int rewardIndex = 0;
             rewardIndex < rewards.arraySize;
             rewardIndex++)
        {
            SerializedProperty reward =
                rewards.GetArrayElementAtIndex(rewardIndex);
            reward.FindPropertyRelative("grade").enumValueIndex =
                (int)grade;
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        GetPoolEntryLabel(reward, rewardIndex),
                        EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(rewardIndex == 0))
                    {
                        if (GUILayout.Button("↑", GUILayout.Width(28f)))
                        {
                            moveFrom = rewardIndex;
                            moveTo = rewardIndex - 1;
                        }
                    }
                    using (new EditorGUI.DisabledScope(
                               rewardIndex >= rewards.arraySize - 1))
                    {
                        if (GUILayout.Button("↓", GUILayout.Width(28f)))
                        {
                            moveFrom = rewardIndex;
                            moveTo = rewardIndex + 1;
                        }
                    }
                    if (GUILayout.Button("삭제", GUILayout.Width(48f)))
                        removeIndex = rewardIndex;
                }

                DrawRewardEntryFields(reward, grade);
                if (selectionMode ==
                    RecruitRewardSelectionMode.IndividualWeight)
                {
                    DrawProperty(reward, "rate", "등급 내부 가중치");
                }
                DrawProperty(reward, "pickup", "픽업");

                if (probabilitiesValid &&
                    probabilityCursor < probabilityRows.Count)
                {
                    EditorGUILayout.LabelField(
                        "최종 개별 확률",
                        $"{probabilityRows[probabilityCursor].Probability * 100d:0.####}%");
                }
            }
            probabilityCursor++;
        }

        if (removeIndex >= 0)
            RemovePoolEntry(rewards, removeIndex);
        if (moveFrom >= 0)
        {
            MoveArrayElement(
                rewards,
                moveFrom,
                moveTo,
                $"{(int)grade}등급 보상 순서 변경");
        }
    }

    private static void DrawRewardEntryFields(
        SerializedProperty reward,
        CharacterGrade poolGrade)
    {
        SerializedProperty rewardType =
            reward.FindPropertyRelative("rewardType");
        EditorGUILayout.PropertyField(
            rewardType,
            new GUIContent("보상 종류"));
        RecruitRewardType type =
            (RecruitRewardType)rewardType.enumValueIndex;

        switch (type)
        {
            case RecruitRewardType.Character:
            {
                DrawProperty(reward, "character", "캐릭터 SO");
                CharacterSO character = reward
                    .FindPropertyRelative("character")
                    .objectReferenceValue as CharacterSO;
                if (character != null)
                {
                    EditorGUILayout.LabelField(
                        "캐릭터 정보",
                        $"{character.CharacterName} · " +
                        $"{(int)character.Grade}등급 · " +
                        $"{character.CharacterId}");
                    if (character.Grade != poolGrade)
                    {
                        EditorGUILayout.HelpBox(
                            $"이 캐릭터는 {(int)character.Grade}등급이므로 " +
                            $"{(int)poolGrade}등급 풀에서 추첨할 수 없습니다.",
                            MessageType.Error);
                    }
                }
                break;
            }

            case RecruitRewardType.Item:
            {
                DrawProperty(reward, "item", "아이템 SO");
                DrawProperty(reward, "itemAmount", "지급 수량");
                ItemDefinitionSO item = reward
                    .FindPropertyRelative("item")
                    .objectReferenceValue as ItemDefinitionSO;
                if (item != null)
                {
                    EditorGUILayout.LabelField(
                        "아이템 정보",
                        $"{item.GetDisplayName(true)} · " +
                        $"{item.Category} · {item.ItemId}");
                }
                break;
            }

            default:
                DrawProperty(reward, "displayName", "표시 이름");
                break;
        }
    }

    private void DrawBulkRewardControls(
        SerializedProperty gradePools,
        int poolIndex,
        CharacterGrade grade)
    {
        EditorGUILayout.Space(5f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("캐릭터 다중 선택"))
            {
                RecruitRewardMultiSelectWindow.Open(
                    grade,
                    RecruitRewardType.Character,
                    selected => AddRewardsToGradePool(
                        poolIndex,
                        grade,
                        selected));
            }
            if (GUILayout.Button("아이템 다중 선택"))
            {
                RecruitRewardMultiSelectWindow.Open(
                    grade,
                    RecruitRewardType.Item,
                    selected => AddRewardsToGradePool(
                        poolIndex,
                        grade,
                        selected));
            }
            if (GUILayout.Button("Project 선택 추가"))
            {
                AddRewardsToGradePool(
                    poolIndex,
                    grade,
                    Selection.objects);
            }
        }

        Rect dropArea = GUILayoutUtility.GetRect(
            0f,
            42f,
            GUILayout.ExpandWidth(true));
        GUI.Box(
            dropArea,
            "여기에 여러 CharacterSO / ItemDefinitionSO 드래그",
            EditorStyles.helpBox);
        Event current = Event.current;
        if (!dropArea.Contains(current.mousePosition) ||
            (current.type != EventType.DragUpdated &&
             current.type != EventType.DragPerform))
        {
            return;
        }

        bool hasSupportedObject = false;
        foreach (UnityEngine.Object dragged in
                 DragAndDrop.objectReferences)
        {
            if (dragged is CharacterSO || dragged is ItemDefinitionSO)
            {
                hasSupportedObject = true;
                break;
            }
        }
        DragAndDrop.visualMode = hasSupportedObject
            ? DragAndDropVisualMode.Copy
            : DragAndDropVisualMode.Rejected;
        if (current.type == EventType.DragPerform &&
            hasSupportedObject)
        {
            DragAndDrop.AcceptDrag();
            AddRewardsToGradePool(
                poolIndex,
                grade,
                DragAndDrop.objectReferences);
        }
        current.Use();
    }

    private static CharacterGrade GetGradePoolGrade(
        SerializedProperty gradePool,
        int fallbackIndex)
    {
        SerializedProperty grade =
            gradePool.FindPropertyRelative("grade");
        return CharacterGradePresentation.Clamp(
            grade != null
                ? (CharacterGrade)grade.enumValueIndex
                : (CharacterGrade)Mathf.Clamp(fallbackIndex, 0, 3));
    }

    private static void DrawGradeSwatch(CharacterGrade grade)
    {
        CharacterGradeStyle style =
            CharacterGradePresentation.GetStyle(grade);
        Rect rect = GUILayoutUtility.GetRect(
            18f,
            18f,
            GUILayout.Width(18f));
        EditorGUI.DrawRect(rect, style.PrimaryColor);
    }

    private void DrawSimulationControls(
        bool valid,
        IReadOnlyList<ProbabilityRow> rows,
        string error)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "확률 시뮬레이션",
            EditorStyles.boldLabel);
        _useFixedSeed = EditorGUILayout.Toggle(
            "고정 시드 사용",
            _useFixedSeed);
        using (new EditorGUI.DisabledScope(!_useFixedSeed))
        {
            _simulationSeed = EditorGUILayout.IntField(
                "시드",
                _simulationSeed);
        }

        _simulationCount = Mathf.Clamp(
            EditorGUILayout.IntField(
                "시뮬레이션 횟수",
                _simulationCount),
            1,
            MaximumSimulationCount);

        using (new EditorGUI.DisabledScope(!valid))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawQuickSimulationButton("1회", 1, rows);
                DrawQuickSimulationButton("10회", 10, rows);
                DrawQuickSimulationButton("1,000회", 1000, rows);
                DrawQuickSimulationButton("10,000회", 10000, rows);
                if (GUILayout.Button("입력 횟수 실행"))
                    RunSimulation(rows, _simulationCount);
            }
        }

        if (!valid && !string.IsNullOrWhiteSpace(error))
            return;

        if (!string.IsNullOrWhiteSpace(_simulationSummary))
        {
            EditorGUILayout.LabelField(
                _simulationSummary,
                EditorStyles.miniBoldLabel);
        }

        foreach (SimulationResult result in _simulationResults)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    result.Label,
                    GUILayout.MinWidth(140f));
                EditorGUILayout.LabelField(
                    $"{result.Count:N0}회",
                    GUILayout.Width(80f));
                EditorGUILayout.LabelField(
                    $"관측 {result.ObservedPercent:0.####}%",
                    GUILayout.Width(130f));
                EditorGUILayout.LabelField(
                    $"기대 {result.ExpectedPercent:0.####}%",
                    GUILayout.Width(130f));
                EditorGUILayout.LabelField(
                    $"편차 {result.DeviationPercent:+0.####;-0.####;0}%",
                    GUILayout.Width(130f));
            }
        }
    }

    private void DrawQuickSimulationButton(
        string label,
        int count,
        IReadOnlyList<ProbabilityRow> rows)
    {
        if (!GUILayout.Button(label))
            return;
        _simulationCount = count;
        RunSimulation(rows, count);
    }

    private void RunSimulation(
        IReadOnlyList<ProbabilityRow> rows,
        int count)
    {
        ClearSimulation();
        if (rows == null || rows.Count == 0 || count <= 0)
            return;

        int seed = _useFixedSeed
            ? _simulationSeed
            : Guid.NewGuid().GetHashCode();
        System.Random random = new(seed);
        int[] counts = new int[rows.Count];
        for (int draw = 0; draw < count; draw++)
        {
            double roll = random.NextDouble();
            double cumulative = 0d;
            int selected = rows.Count - 1;
            for (int index = 0; index < rows.Count; index++)
            {
                cumulative += rows[index].Probability;
                if (roll < cumulative)
                {
                    selected = index;
                    break;
                }
            }
            counts[selected]++;
        }

        int[] gradeCounts = new int[4];
        double[] gradeProbabilities = new double[4];
        for (int index = 0; index < rows.Count; index++)
        {
            int grade = Mathf.Clamp((int)rows[index].Grade, 0, 3);
            gradeCounts[grade] += counts[index];
            gradeProbabilities[grade] += rows[index].Probability;
        }
        for (int grade = 0; grade < gradeCounts.Length; grade++)
        {
            if (gradeProbabilities[grade] <= 0d)
                continue;
            _simulationResults.Add(new SimulationResult(
                $"[{grade}등급 합계]",
                gradeCounts[grade],
                gradeCounts[grade] * 100d / count,
                gradeProbabilities[grade] * 100d));
        }

        for (int index = 0; index < rows.Count; index++)
        {
            double observed = counts[index] * 100d / count;
            double expected = rows[index].Probability * 100d;
            _simulationResults.Add(new SimulationResult(
                rows[index].Label,
                counts[index],
                observed,
                expected));
        }

        _simulationSummary =
            $"{count:N0}회 완료 · 시드 {seed} · 인벤토리 변경 없음";
    }

    private void DrawPaymentSettings(SerializedProperty banner)
    {
        _paymentExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _paymentExpanded,
            "3. 모집 재화");
        if (!_paymentExpanded)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "각 항목은 서로 대체 가능한 결제 경로입니다. " +
                "런타임에서는 결제 가능한 보유 자원을 먼저 찾고, " +
                "여러 경로가 가능하면 숫자가 낮은 우선순위를 표시합니다.",
                MessageType.Info);

            SerializedProperty routes =
                banner.FindPropertyRelative("paymentRoutes");
            SerializedProperty defaultIndex =
                banner.FindPropertyRelative("defaultPaymentRouteIndex");

            if (routes.arraySize > 0)
            {
                string[] routeNames = BuildPaymentRouteNames(routes);
                defaultIndex.intValue = Mathf.Clamp(
                    defaultIndex.intValue,
                    0,
                    routes.arraySize - 1);
                defaultIndex.intValue = EditorGUILayout.Popup(
                    "동률 시 기본 결제 경로",
                    defaultIndex.intValue,
                    routeNames);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "결제 경로가 없습니다.",
                    MessageType.Warning);
            }

            int removeIndex = -1;
            int moveFrom = -1;
            int moveTo = -1;
            for (int index = 0; index < routes.arraySize; index++)
            {
                SerializedProperty route =
                    routes.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            $"결제 경로 {index + 1}",
                            EditorStyles.boldLabel);
                        if (index == defaultIndex.intValue)
                            GUILayout.Label("동률 기본", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        using (new EditorGUI.DisabledScope(index == 0))
                        {
                            if (GUILayout.Button("↑", GUILayout.Width(28f)))
                            {
                                moveFrom = index;
                                moveTo = index - 1;
                            }
                        }
                        using (new EditorGUI.DisabledScope(
                                   index >= routes.arraySize - 1))
                        {
                            if (GUILayout.Button("↓", GUILayout.Width(28f)))
                            {
                                moveFrom = index;
                                moveTo = index + 1;
                            }
                        }
                        if (GUILayout.Button("삭제", GUILayout.Width(48f)))
                            removeIndex = index;
                    }

                    DrawProperty(route, "routeId", "경로 ID");
                    DrawProperty(route, "item", "소모 아이템");
                    ItemDefinitionSO item = route
                        .FindPropertyRelative("item")
                        .objectReferenceValue as ItemDefinitionSO;
                    if (item != null)
                    {
                        EditorGUILayout.LabelField(
                            "아이템 정보",
                            $"{item.GetDisplayName(true)} · " +
                            $"{item.Category} · {item.ItemId}");
                    }

                    DrawProperty(
                        route,
                        "priority",
                        "결제 우선순위 (낮을수록 먼저)");
                    DrawProperty(
                        route,
                        "singleRecruitEnabled",
                        "1회 모집 사용");
                    DrawProperty(route, "singleCost", "1회 소모량");
                    DrawProperty(
                        route,
                        "tenRecruitEnabled",
                        "10회 모집 사용");
                    DrawProperty(
                        route,
                        "automaticTenCost",
                        "10회 비용 자동 계산");

                    SerializedProperty automatic =
                        route.FindPropertyRelative("automaticTenCost");
                    if (automatic.boolValue)
                    {
                        long single = Math.Max(
                            0L,
                            route.FindPropertyRelative("singleCost").longValue);
                        long ten = single > long.MaxValue / 10L
                            ? long.MaxValue
                            : single * 10L;
                        EditorGUILayout.LabelField(
                            "10회 소모량",
                            $"{ten:N0} (1회 비용 × 10)");
                    }
                    else
                    {
                        DrawProperty(
                            route,
                            "tenCostOverride",
                            "10회 소모량");
                    }

                    if (item is RecruitTicketItemSO ticket)
                    {
                        string bannerGroup =
                            GetTrimmedString(banner, "ticketGroupId");
                        MessageType type = string.IsNullOrWhiteSpace(bannerGroup) ||
                                           string.Equals(
                                               bannerGroup,
                                               ticket.BannerGroupId,
                                               StringComparison.Ordinal)
                            ? MessageType.Info
                            : MessageType.Warning;
                        EditorGUILayout.HelpBox(
                            $"모집권 그룹: {ticket.BannerGroupId} · " +
                            $"1장당 {ticket.RecruitsPerItem}회",
                            type);
                    }
                }
            }

            if (removeIndex >= 0)
                RemovePaymentRoute(routes, defaultIndex, removeIndex);
            if (moveFrom >= 0)
            {
                MovePaymentRoute(
                    routes,
                    defaultIndex,
                    moveFrom,
                    moveTo);
            }

            if (GUILayout.Button("결제 경로 추가"))
                AddPaymentRoute(routes, defaultIndex);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawValidation(
        SerializedProperty pages,
        SerializedProperty banner)
    {
        _validationExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            _validationExpanded,
            "검증 결과");
        if (_validationExpanded)
        {
            List<ValidationIssue> issues =
                CollectValidationIssues(pages, banner);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (issues.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "현재 배너 설정에서 문제를 찾지 못했습니다.",
                        MessageType.Info);
                }
                else
                {
                    foreach (ValidationIssue issue in issues)
                        EditorGUILayout.HelpBox(issue.Message, issue.Type);
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static List<ValidationIssue> CollectValidationIssues(
        SerializedProperty pages,
        SerializedProperty banner)
    {
        List<ValidationIssue> issues = new();
        string bannerId = GetTrimmedString(banner, "bannerId");
        if (string.IsNullOrWhiteSpace(bannerId))
        {
            issues.Add(new ValidationIssue(
                "배너 ID가 비어 있습니다.",
                MessageType.Error));
        }
        else
        {
            int duplicateCount = 0;
            for (int index = 0; index < pages.arraySize; index++)
            {
                string otherId = GetTrimmedString(
                    pages.GetArrayElementAtIndex(index),
                    "bannerId");
                if (string.Equals(
                        bannerId,
                        otherId,
                        StringComparison.Ordinal))
                {
                    duplicateCount++;
                }
            }

            if (duplicateCount > 1)
            {
                issues.Add(new ValidationIssue(
                    $"배너 ID '{bannerId}'가 중복되었습니다.",
                    MessageType.Error));
            }
        }

        Sprite art = banner.FindPropertyRelative("bannerArt")
            .objectReferenceValue as Sprite;
        if (art == null)
        {
            issues.Add(new ValidationIssue(
                "배너 이미지가 비어 있습니다.",
                MessageType.Warning));
        }
        else if (art.rect.height > 0f)
        {
            float ratio = art.rect.width / art.rect.height;
            float difference = Mathf.Abs(
                ratio - RecruitBannerView.BannerArtAspectRatio) /
                RecruitBannerView.BannerArtAspectRatio;
            if (difference > BannerRatioTolerance)
            {
                issues.Add(new ValidationIssue(
                    "배너 이미지가 A계열 가로형 비율(1.414:1)과 다릅니다.",
                    MessageType.Warning));
            }
        }

        if (!TryBuildProbabilityRows(
                banner,
                out _,
                out string poolError,
                out _))
        {
            issues.Add(new ValidationIssue(poolError, MessageType.Error));
        }

        ValidatePaymentRoutes(banner, issues);
        return issues;
    }

    private static void ValidatePaymentRoutes(
        SerializedProperty banner,
        ICollection<ValidationIssue> issues)
    {
        SerializedProperty routes =
            banner.FindPropertyRelative("paymentRoutes");
        if (routes.arraySize == 0)
        {
            issues.Add(new ValidationIssue(
                "모집 재화 결제 경로가 없습니다.",
                MessageType.Error));
            return;
        }

        SerializedProperty defaultIndex =
            banner.FindPropertyRelative("defaultPaymentRouteIndex");
        if (defaultIndex.intValue < 0 ||
            defaultIndex.intValue >= routes.arraySize)
        {
            issues.Add(new ValidationIssue(
                "기본 표시 결제 경로가 올바르지 않습니다.",
                MessageType.Error));
        }

        string ticketGroup = GetTrimmedString(banner, "ticketGroupId");
        HashSet<string> routeIds = new(StringComparer.Ordinal);
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        bool supportsSingle = false;
        bool supportsTen = false;

        for (int index = 0; index < routes.arraySize; index++)
        {
            SerializedProperty route =
                routes.GetArrayElementAtIndex(index);
            string routeId = GetTrimmedString(route, "routeId");
            if (string.IsNullOrWhiteSpace(routeId))
            {
                issues.Add(new ValidationIssue(
                    $"{index + 1}번 결제 경로 ID가 비어 있습니다.",
                    MessageType.Error));
            }
            else if (!routeIds.Add(routeId))
            {
                issues.Add(new ValidationIssue(
                    $"결제 경로 ID '{routeId}'가 중복되었습니다.",
                    MessageType.Error));
            }

            ItemDefinitionSO item = route.FindPropertyRelative("item")
                .objectReferenceValue as ItemDefinitionSO;
            if (item == null)
            {
                issues.Add(new ValidationIssue(
                    $"{index + 1}번 결제 경로의 아이템이 비어 있습니다.",
                    MessageType.Error));
            }
            else
            {
                string key = string.IsNullOrWhiteSpace(item.ItemId)
                    ? item.GetInstanceID().ToString()
                    : item.ItemId;
                if (!itemIds.Add(key))
                {
                    issues.Add(new ValidationIssue(
                        $"아이템 '{item.GetDisplayName(true)}'이 중복 등록되었습니다.",
                        MessageType.Error));
                }

                if (item is RecruitTicketItemSO ticket &&
                    !string.IsNullOrWhiteSpace(ticketGroup) &&
                    !string.IsNullOrWhiteSpace(ticket.BannerGroupId) &&
                    !string.Equals(
                        ticketGroup,
                        ticket.BannerGroupId,
                        StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        $"모집권 '{item.GetDisplayName(true)}'의 그룹 " +
                        $"'{ticket.BannerGroupId}'이 배너 그룹 " +
                        $"'{ticketGroup}'과 다릅니다.",
                        MessageType.Error));
                }
            }

            bool singleEnabled = route
                .FindPropertyRelative("singleRecruitEnabled")
                .boolValue;
            bool tenEnabled = route
                .FindPropertyRelative("tenRecruitEnabled")
                .boolValue;
            long singleCost = route
                .FindPropertyRelative("singleCost")
                .longValue;
            bool automatic = route
                .FindPropertyRelative("automaticTenCost")
                .boolValue;
            long tenCost = automatic
                ? singleCost
                : route.FindPropertyRelative("tenCostOverride").longValue;

            if (singleEnabled && singleCost <= 0L)
            {
                issues.Add(new ValidationIssue(
                    $"{index + 1}번 결제 경로의 1회 소모량은 1 이상이어야 합니다.",
                    MessageType.Error));
            }
            if (tenEnabled && tenCost <= 0L)
            {
                issues.Add(new ValidationIssue(
                    $"{index + 1}번 결제 경로의 10회 소모량은 1 이상이어야 합니다.",
                    MessageType.Error));
            }

            supportsSingle |= singleEnabled && item != null && singleCost > 0L;
            supportsTen |= tenEnabled && item != null && tenCost > 0L;
        }

        if (!supportsSingle)
        {
            issues.Add(new ValidationIssue(
                "사용 가능한 1회 모집 결제 경로가 없습니다.",
                MessageType.Error));
        }
        if (!supportsTen)
        {
            issues.Add(new ValidationIssue(
                "사용 가능한 10회 모집 결제 경로가 없습니다.",
                MessageType.Error));
        }
    }

    private static bool TryBuildProbabilityRows(
        SerializedProperty banner,
        out List<ProbabilityRow> rows,
        out string error,
        out double inputTotal)
    {
        rows = new List<ProbabilityRow>();
        error = string.Empty;
        inputTotal = 0d;
        SerializedProperty gradePools =
            banner.FindPropertyRelative("gradePools");
        if (gradePools == null || gradePools.arraySize == 0)
        {
            error = "등급별 모집 보상 풀이 비어 있습니다.";
            return false;
        }

        bool[] registeredGrades = new bool[4];
        HashSet<string> registeredRewards =
            new(StringComparer.Ordinal);
        double[] gradeRates =
            new double[gradePools.arraySize];
        List<double[]> innerRates = new();
        List<double> innerTotals = new();
        for (int poolIndex = 0;
             poolIndex < gradePools.arraySize;
             poolIndex++)
        {
            SerializedProperty gradePool =
                gradePools.GetArrayElementAtIndex(poolIndex);
            CharacterGrade grade = GetGradePoolGrade(
                gradePool,
                poolIndex);
            int gradeIndex = Mathf.Clamp((int)grade, 0, 3);
            if (registeredGrades[gradeIndex])
            {
                error = $"{gradeIndex}등급 풀이 중복되었습니다.";
                return false;
            }
            registeredGrades[gradeIndex] = true;

            float gradeRate = gradePool
                .FindPropertyRelative("rate")
                .floatValue;
            if (float.IsNaN(gradeRate) ||
                float.IsInfinity(gradeRate) ||
                gradeRate < 0f)
            {
                error = $"{gradeIndex}등급 확률 값이 올바르지 않습니다.";
                return false;
            }
            gradeRates[poolIndex] = gradeRate;
            inputTotal += gradeRate;

            SerializedProperty rewards =
                gradePool.FindPropertyRelative("rewards");
            if (gradeRate > 0f && rewards.arraySize == 0)
            {
                error =
                    $"{gradeIndex}등급 확률이 설정되었지만 보상이 없습니다.";
                return false;
            }

            RecruitRewardSelectionMode selectionMode =
                (RecruitRewardSelectionMode)gradePool
                    .FindPropertyRelative("selectionMode")
                    .enumValueIndex;
            double[] rewardRates = new double[rewards.arraySize];
            double innerTotal = 0d;
            for (int rewardIndex = 0;
                 rewardIndex < rewards.arraySize;
                 rewardIndex++)
            {
                SerializedProperty reward =
                    rewards.GetArrayElementAtIndex(rewardIndex);
                if (!TryValidatePoolEntry(
                        reward,
                        rewardIndex,
                        out error))
                {
                    error =
                        $"{gradeIndex}등급: {error}";
                    return false;
                }

                RecruitRewardType type =
                    (RecruitRewardType)reward
                        .FindPropertyRelative("rewardType")
                        .enumValueIndex;
                if (type == RecruitRewardType.Character)
                {
                    CharacterSO character = reward
                        .FindPropertyRelative("character")
                        .objectReferenceValue as CharacterSO;
                    if (character != null &&
                        character.Grade != grade)
                    {
                        error =
                            $"{character.CharacterName} 캐릭터의 등급이 " +
                            $"{gradeIndex}등급 풀과 일치하지 않습니다.";
                        return false;
                    }
                }

                string rewardKey = GetRewardKey(reward);
                if (!string.IsNullOrWhiteSpace(rewardKey) &&
                    !registeredRewards.Add(rewardKey))
                {
                    error =
                        $"{GetPoolEntryLabel(reward, rewardIndex)} 보상이 중복 등록되었습니다.";
                    return false;
                }

                float innerRate = selectionMode ==
                                  RecruitRewardSelectionMode.Equal
                    ? 1f
                    : reward.FindPropertyRelative("rate").floatValue;
                if (float.IsNaN(innerRate) ||
                    float.IsInfinity(innerRate) ||
                    innerRate < 0f)
                {
                    error =
                        $"{gradeIndex}등급 {rewardIndex + 1}번 보상의 가중치가 올바르지 않습니다.";
                    return false;
                }
                rewardRates[rewardIndex] = innerRate;
                innerTotal += innerRate;
            }
            if (gradeRate > 0f &&
                rewards.arraySize > 0 &&
                innerTotal <= 0d)
            {
                error =
                    $"{gradeIndex}등급 내부 가중치 합계는 0보다 커야 합니다.";
                return false;
            }
            innerRates.Add(rewardRates);
            innerTotals.Add(innerTotal);
        }

        if (inputTotal <= 0d)
        {
            error = "등급 확률 중 하나 이상은 0보다 커야 합니다.";
            return false;
        }

        RecruitRateInputMode mode = (RecruitRateInputMode)banner
            .FindPropertyRelative("rateInputMode")
            .enumValueIndex;
        if (mode == RecruitRateInputMode.Percentage &&
            Math.Abs(inputTotal - 100d) > 0.01d)
        {
            error =
                $"등급 확률 합계가 100%가 아닙니다. 현재 {inputTotal:0.####}%입니다.";
            return false;
        }

        for (int poolIndex = 0;
             poolIndex < gradePools.arraySize;
             poolIndex++)
        {
            SerializedProperty gradePool =
                gradePools.GetArrayElementAtIndex(poolIndex);
            CharacterGrade grade = GetGradePoolGrade(
                gradePool,
                poolIndex);
            SerializedProperty rewards =
                gradePool.FindPropertyRelative("rewards");
            for (int rewardIndex = 0;
                 rewardIndex < rewards.arraySize;
                 rewardIndex++)
            {
                double innerProbability =
                    innerTotals[poolIndex] > 0d
                        ? innerRates[poolIndex][rewardIndex] /
                          innerTotals[poolIndex]
                        : 0d;
                SerializedProperty reward =
                    rewards.GetArrayElementAtIndex(rewardIndex);
                rows.Add(new ProbabilityRow(
                    $"{(int)grade}등급 · " +
                    GetPoolEntryLabel(reward, rewardIndex),
                    grade,
                    gradeRates[poolIndex] /
                    inputTotal *
                    innerProbability));
            }
        }
        return true;
    }

    private static bool TryValidatePoolEntry(
        SerializedProperty entry,
        int index,
        out string error)
    {
        error = string.Empty;
        RecruitRewardType type = (RecruitRewardType)entry
            .FindPropertyRelative("rewardType")
            .enumValueIndex;
        switch (type)
        {
            case RecruitRewardType.Character:
            {
                CharacterSO character = entry
                    .FindPropertyRelative("character")
                    .objectReferenceValue as CharacterSO;
                if (character == null)
                {
                    error =
                        $"{index + 1}번 캐릭터 보상에 CharacterSO가 지정되지 않았습니다.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(character.CharacterId))
                {
                    error =
                        $"{index + 1}번 캐릭터 보상의 ID가 비어 있습니다.";
                    return false;
                }
                break;
            }

            case RecruitRewardType.Item:
            {
                ItemDefinitionSO item = entry
                    .FindPropertyRelative("item")
                    .objectReferenceValue as ItemDefinitionSO;
                if (item == null)
                {
                    error =
                        $"{index + 1}번 아이템 보상에 ItemDefinitionSO가 지정되지 않았습니다.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(item.ItemId))
                {
                    error =
                        $"{index + 1}번 아이템 보상의 ID가 비어 있습니다.";
                    return false;
                }
                if (entry.FindPropertyRelative("itemAmount").longValue <= 0L)
                {
                    error =
                        $"{index + 1}번 아이템 보상 수량은 1 이상이어야 합니다.";
                    return false;
                }
                break;
            }
        }
        return true;
    }

    private static string GetRewardKey(SerializedProperty entry)
    {
        RecruitRewardType type = (RecruitRewardType)entry
            .FindPropertyRelative("rewardType")
            .enumValueIndex;
        return type switch
        {
            RecruitRewardType.Character =>
                entry.FindPropertyRelative("character")
                    .objectReferenceValue is CharacterSO character
                    ? $"character:{character.CharacterId}"
                    : string.Empty,
            RecruitRewardType.Item =>
                entry.FindPropertyRelative("item")
                    .objectReferenceValue is ItemDefinitionSO item
                    ? $"item:{item.ItemId}"
                    : string.Empty,
            _ => string.Empty,
        };
    }

    private static string GetPoolEntryLabel(
        SerializedProperty entry,
        int index)
    {
        RecruitRewardType type = (RecruitRewardType)entry
            .FindPropertyRelative("rewardType")
            .enumValueIndex;
        switch (type)
        {
            case RecruitRewardType.Character:
            {
                CharacterSO character = entry
                    .FindPropertyRelative("character")
                    .objectReferenceValue as CharacterSO;
                if (character == null)
                    return $"미지정 캐릭터 {index + 1}";
                return !string.IsNullOrWhiteSpace(character.CharacterName)
                    ? character.CharacterName
                    : character.name;
            }

            case RecruitRewardType.Item:
            {
                ItemDefinitionSO item = entry
                    .FindPropertyRelative("item")
                    .objectReferenceValue as ItemDefinitionSO;
                if (item == null)
                    return $"미지정 아이템 {index + 1}";
                long amount = Math.Max(
                    1L,
                    entry.FindPropertyRelative("itemAmount").longValue);
                return $"{item.GetDisplayName(true)} ×{amount:N0}";
            }

            default:
                return Fallback(
                    GetTrimmedString(entry, "displayName"),
                    $"더미 항목 {index + 1}");
        }
    }

    private void AddBanner(SerializedProperty pages)
    {
        RecordUndo("모집 배너 추가");
        int index = pages.arraySize;
        pages.InsertArrayElementAtIndex(index);
        SerializedProperty banner = pages.GetArrayElementAtIndex(index);
        InitializeBanner(banner, index);
        _selectedBannerIndex = index;
        ClearSimulation();
        ApplyAndExit();
    }

    private void DuplicateBanner(SerializedProperty pages)
    {
        if (pages.arraySize == 0)
            return;

        RecordUndo("모집 배너 복제");
        int source = Mathf.Clamp(
            _selectedBannerIndex,
            0,
            pages.arraySize - 1);
        pages.InsertArrayElementAtIndex(source);
        int duplicateIndex = source + 1;
        SerializedProperty duplicate =
            pages.GetArrayElementAtIndex(duplicateIndex);
        string sourceId = GetTrimmedString(duplicate, "bannerId");
        duplicate.FindPropertyRelative("bannerId").stringValue =
            Fallback(sourceId, "banner") + "_copy";
        _selectedBannerIndex = duplicateIndex;
        ClearSimulation();
        ApplyAndExit();
    }

    private void DeleteBanner(SerializedProperty pages)
    {
        if (pages.arraySize <= 0)
            return;
        if (!EditorUtility.DisplayDialog(
                "모집 배너 삭제",
                "선택한 모집 배너를 삭제하시겠습니까?",
                "삭제",
                "취소"))
        {
            return;
        }

        RecordUndo("모집 배너 삭제");
        pages.DeleteArrayElementAtIndex(_selectedBannerIndex);
        _selectedBannerIndex = Mathf.Clamp(
            _selectedBannerIndex,
            0,
            Mathf.Max(0, pages.arraySize - 1));
        ClearSimulation();
        ApplyAndExit();
    }

    private void MoveBanner(SerializedProperty pages, int direction)
    {
        int target = _selectedBannerIndex + direction;
        if (target < 0 || target >= pages.arraySize)
            return;
        RecordUndo("모집 배너 순서 변경");
        pages.MoveArrayElement(_selectedBannerIndex, target);
        _selectedBannerIndex = target;
        ClearSimulation();
        ApplyAndExit();
    }

    private static void InitializeBanner(
        SerializedProperty banner,
        int index)
    {
        SetString(banner, "bannerId", index == 0 ? "main" : $"banner_{index + 1}");
        SetString(banner, "ticketGroupId", "standard");
        SetString(banner, "koreanTitle", "상시 모집");
        SetString(banner, "englishTitle", "STANDARD RECRUITMENT");
        SetString(banner, "koreanDescription", "새로운 대원을 모집합니다");
        SetString(banner, "englishDescription", "RECRUIT NEW OPERATORS");
        SetString(banner, "koreanPeriod", "상시");
        SetString(banner, "englishPeriod", "PERMANENT");
        banner.FindPropertyRelative("bannerArt").objectReferenceValue = null;
        banner.FindPropertyRelative("currencyIcon").objectReferenceValue = null;
        banner.FindPropertyRelative("totalRecruitCount").intValue = 0;
        banner.FindPropertyRelative("currentStack").intValue = 0;
        banner.FindPropertyRelative("maximumStack").intValue = 0;
        banner.FindPropertyRelative("singleCost").intValue = 0;
        banner.FindPropertyRelative("tenCost").intValue = 0;
        banner.FindPropertyRelative("rateInputMode").enumValueIndex =
            (int)RecruitRateInputMode.Percentage;
        banner.FindPropertyRelative("dummyPool").arraySize = 0;
        SerializedProperty gradePools =
            banner.FindPropertyRelative("gradePools");
        gradePools.arraySize = 4;
        float[] defaultRates = { 40f, 50f, 8f, 2f };
        for (int grade = 0; grade < gradePools.arraySize; grade++)
        {
            SerializedProperty gradePool =
                gradePools.GetArrayElementAtIndex(grade);
            gradePool.FindPropertyRelative("grade").enumValueIndex = grade;
            gradePool.FindPropertyRelative("rate").floatValue =
                defaultRates[grade];
            gradePool.FindPropertyRelative("selectionMode").enumValueIndex =
                (int)RecruitRewardSelectionMode.Equal;
            gradePool.FindPropertyRelative("rewards").arraySize = 0;
        }
        banner.FindPropertyRelative("rewardPoolDataVersion").intValue = 1;
        banner.FindPropertyRelative("paymentRoutes").arraySize = 0;
        banner.FindPropertyRelative("defaultPaymentRouteIndex").intValue = 0;
        banner.FindPropertyRelative("interactionEnabled").boolValue = true;
    }

    private void AddRewardsToGradePool(
        int requestedPoolIndex,
        CharacterGrade grade,
        IEnumerable<UnityEngine.Object> assets)
    {
        List<UnityEngine.Object> queued = new();
        if (assets != null)
        {
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is CharacterSO || asset is ItemDefinitionSO)
                    queued.Add(asset);
            }
        }

        EditorApplication.delayCall += () =>
        {
            if (this == null || _targetPage == null)
                return;
            EnsureSerializedTarget();
            _serializedTarget.UpdateIfRequiredOrScript();
            SerializedProperty pages =
                _serializedTarget.FindProperty("recruitBannerPages");
            if (pages == null || pages.arraySize == 0)
                return;

            int bannerIndex = Mathf.Clamp(
                _selectedBannerIndex,
                0,
                pages.arraySize - 1);
            SerializedProperty banner =
                pages.GetArrayElementAtIndex(bannerIndex);
            SerializedProperty gradePools =
                banner.FindPropertyRelative("gradePools");
            int poolIndex = FindGradePoolIndex(
                gradePools,
                grade,
                requestedPoolIndex);
            if (poolIndex < 0)
                return;

            SerializedProperty rewards = gradePools
                .GetArrayElementAtIndex(poolIndex)
                .FindPropertyRelative("rewards");
            HashSet<string> registered = new(StringComparer.Ordinal);
            for (int existingPoolIndex = 0;
                 existingPoolIndex < gradePools.arraySize;
                 existingPoolIndex++)
            {
                SerializedProperty existingRewards = gradePools
                    .GetArrayElementAtIndex(existingPoolIndex)
                    .FindPropertyRelative("rewards");
                for (int rewardIndex = 0;
                     rewardIndex < existingRewards.arraySize;
                     rewardIndex++)
                {
                    string key = GetRewardKey(
                        existingRewards.GetArrayElementAtIndex(
                            rewardIndex));
                    if (!string.IsNullOrWhiteSpace(key))
                        registered.Add(key);
                }
            }

            int added = 0;
            int skipped = 0;
            RecordUndo($"{(int)grade}등급 보상 일괄 추가");
            foreach (UnityEngine.Object asset in queued)
            {
                if (asset is CharacterSO character)
                {
                    if (character.Grade != grade)
                    {
                        skipped++;
                        continue;
                    }

                    string key =
                        $"character:{character.CharacterId}";
                    if (!registered.Add(key))
                    {
                        skipped++;
                        continue;
                    }

                    int index = rewards.arraySize;
                    rewards.InsertArrayElementAtIndex(index);
                    SerializedProperty reward =
                        rewards.GetArrayElementAtIndex(index);
                    InitializePoolEntry(
                        reward,
                        character.CharacterName,
                        grade,
                        1f);
                    reward.FindPropertyRelative("rewardType")
                        .enumValueIndex =
                        (int)RecruitRewardType.Character;
                    reward.FindPropertyRelative("character")
                        .objectReferenceValue = character;
                    added++;
                }
                else if (asset is ItemDefinitionSO item)
                {
                    string key = $"item:{item.ItemId}";
                    if (!registered.Add(key))
                    {
                        skipped++;
                        continue;
                    }

                    int index = rewards.arraySize;
                    rewards.InsertArrayElementAtIndex(index);
                    SerializedProperty reward =
                        rewards.GetArrayElementAtIndex(index);
                    InitializePoolEntry(
                        reward,
                        item.GetDisplayName(true),
                        grade,
                        1f);
                    reward.FindPropertyRelative("rewardType")
                        .enumValueIndex =
                        (int)RecruitRewardType.Item;
                    reward.FindPropertyRelative("item")
                        .objectReferenceValue = item;
                    added++;
                }
            }

            _serializedTarget.ApplyModifiedProperties();
            ClearSimulation();
            MarkTargetDirty();
            Repaint();
            string message = added > 0
                ? $"{(int)grade}등급에 {added}개 보상을 추가했습니다."
                : "추가할 수 있는 새 보상이 없습니다.";
            if (skipped > 0)
                message += $" 중복/등급 불일치 {skipped}개 제외.";
            ShowNotification(new GUIContent(message));
        };
    }

    private static int FindGradePoolIndex(
        SerializedProperty gradePools,
        CharacterGrade grade,
        int fallbackIndex)
    {
        if (gradePools == null)
            return -1;
        for (int index = 0;
             index < gradePools.arraySize;
             index++)
        {
            if (GetGradePoolGrade(
                    gradePools.GetArrayElementAtIndex(index),
                    index) == grade)
            {
                return index;
            }
        }
        return fallbackIndex >= 0 &&
               fallbackIndex < gradePools.arraySize
            ? fallbackIndex
            : -1;
    }

    private void RemovePoolEntry(
        SerializedProperty pool,
        int index)
    {
        RecordUndo("더미 모집 항목 삭제");
        pool.DeleteArrayElementAtIndex(index);
        ClearSimulation();
        ApplyAndExit();
    }

    private static void InitializePoolEntry(
        SerializedProperty entry,
        string displayName,
        CharacterGrade grade,
        float rate)
    {
        SetString(entry, "displayName", displayName);
        entry.FindPropertyRelative("rewardType").enumValueIndex =
            (int)RecruitRewardType.Dummy;
        entry.FindPropertyRelative("character").objectReferenceValue = null;
        entry.FindPropertyRelative("item").objectReferenceValue = null;
        entry.FindPropertyRelative("itemAmount").longValue = 1L;
        entry.FindPropertyRelative("grade").enumValueIndex =
            (int)CharacterGradePresentation.Clamp(grade);
        entry.FindPropertyRelative("rate").floatValue =
            Mathf.Max(0f, rate);
        entry.FindPropertyRelative("pickup").boolValue = false;
    }

    private void AddPaymentRoute(
        SerializedProperty routes,
        SerializedProperty defaultIndex)
    {
        RecordUndo("모집 결제 경로 추가");
        int index = routes.arraySize;
        routes.InsertArrayElementAtIndex(index);
        SerializedProperty route = routes.GetArrayElementAtIndex(index);
        SetString(route, "routeId", $"payment_{index + 1}");
        route.FindPropertyRelative("item").objectReferenceValue = null;
        route.FindPropertyRelative("singleCost").longValue = 1L;
        route.FindPropertyRelative("singleRecruitEnabled").boolValue = true;
        route.FindPropertyRelative("tenRecruitEnabled").boolValue = true;
        route.FindPropertyRelative("automaticTenCost").boolValue = true;
        route.FindPropertyRelative("tenCostOverride").longValue = 10L;
        route.FindPropertyRelative("priority").intValue = index;
        if (routes.arraySize == 1)
            defaultIndex.intValue = 0;
        ApplyAndExit();
    }

    private void RemovePaymentRoute(
        SerializedProperty routes,
        SerializedProperty defaultIndex,
        int index)
    {
        RecordUndo("모집 결제 경로 삭제");
        routes.DeleteArrayElementAtIndex(index);
        if (routes.arraySize == 0)
        {
            defaultIndex.intValue = 0;
        }
        else if (defaultIndex.intValue > index)
        {
            defaultIndex.intValue--;
        }
        else if (defaultIndex.intValue == index)
        {
            defaultIndex.intValue = Mathf.Clamp(
                index,
                0,
                routes.arraySize - 1);
        }
        ApplyAndExit();
    }

    private void MovePaymentRoute(
        SerializedProperty routes,
        SerializedProperty defaultIndex,
        int from,
        int to)
    {
        RecordUndo("모집 결제 경로 순서 변경");
        int selected = defaultIndex.intValue;
        routes.MoveArrayElement(from, to);
        if (selected == from)
            defaultIndex.intValue = to;
        else if (selected == to)
            defaultIndex.intValue = from;
        ApplyAndExit();
    }

    private void MoveArrayElement(
        SerializedProperty array,
        int from,
        int to,
        string undoName)
    {
        RecordUndo(undoName);
        array.MoveArrayElement(from, to);
        ClearSimulation();
        ApplyAndExit();
    }

    private static string[] BuildPaymentRouteNames(
        SerializedProperty routes)
    {
        string[] names = new string[routes.arraySize];
        for (int index = 0; index < routes.arraySize; index++)
        {
            SerializedProperty route =
                routes.GetArrayElementAtIndex(index);
            string routeId = GetTrimmedString(route, "routeId");
            ItemDefinitionSO item = route.FindPropertyRelative("item")
                .objectReferenceValue as ItemDefinitionSO;
            names[index] = item != null
                ? $"{index + 1}. {item.GetDisplayName(true)} ({Fallback(routeId, "payment")})"
                : $"{index + 1}. {Fallback(routeId, "payment")} (아이템 없음)";
        }
        return names;
    }

    private static void DrawGradePalettePreview(CharacterGrade grade)
    {
        CharacterGradeStyle style =
            CharacterGradePresentation.GetStyle(grade);
        Rect preview = EditorGUILayout.GetControlRect(false, 24f);
        EditorGUI.DrawRect(preview, style.BackgroundColor);
        EditorGUI.DrawRect(
            new Rect(preview.x, preview.y, 8f, preview.height),
            style.PrimaryColor);
        Handles.DrawSolidRectangleWithOutline(
            preview,
            Color.clear,
            style.OutlineColor);

        float labelOffset = 14f;
        if (style.GradeIcon != null)
        {
            Rect iconRect = new(
                preview.x + 10f,
                preview.y + 3f,
                18f,
                18f);
            Texture icon =
                AssetPreview.GetAssetPreview(style.GradeIcon) ??
                AssetPreview.GetMiniThumbnail(style.GradeIcon);
            if (icon != null)
            {
                GUI.DrawTexture(
                    iconRect,
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
                labelOffset = 34f;
            }
        }

        GUIStyle labelStyle = new(EditorStyles.miniBoldLabel);
        labelStyle.normal.textColor = style.TextColor;
        EditorGUI.LabelField(
            new Rect(
                preview.x + labelOffset,
                preview.y,
                preview.width - labelOffset - 4f,
                preview.height),
            $"공통 팔레트 · {CharacterGradePresentation.GetLabel(grade)}",
            labelStyle);
    }

    private static void DrawProperty(
        SerializedProperty parent,
        string propertyName,
        string label)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void RecordUndo(string action)
    {
        if (_targetPage != null)
            Undo.RecordObject(_targetPage, action);
    }

    private void ApplyAndExit()
    {
        _serializedTarget?.ApplyModifiedProperties();
        MarkTargetDirty();
        GUIUtility.ExitGUI();
    }

    private void MarkTargetDirty()
    {
        if (_targetPage == null)
            return;
        EditorUtility.SetDirty(_targetPage);
        Scene scene = _targetPage.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private void SaveTargetScene()
    {
        if (_targetPage == null)
            return;
        _serializedTarget?.ApplyModifiedProperties();
        if (!SyncTargetPreview(_revealPreviewCount, false))
            return;
        Scene scene = _targetPage.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log(
                "모집 데이터와 디자이너 씬 프리뷰를 저장했습니다.",
                _targetPage);
        }
    }

    private bool SyncTargetPreview(
        int revealPreviewCount,
        bool exitGui)
    {
        if (_targetPage == null)
            return false;
        _serializedTarget?.ApplyModifiedProperties();
        _revealPreviewCount = revealPreviewCount == 1
            ? 1
            : revealPreviewCount == 10
                ? 10
                : 0;

        bool synchronized =
            _targetPage.SyncRecruitEditorPreview(
                _selectedBannerIndex,
                _revealPreviewCount,
                out string error);
        if (!synchronized)
        {
            Debug.LogError(
                string.IsNullOrWhiteSpace(error)
                    ? "모집 씬 프리뷰 동기화에 실패했습니다."
                    : error,
                _targetPage);
            return false;
        }

        MarkTargetDirty();
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
        if (exitGui)
            GUIUtility.ExitGUI();
        return true;
    }

    private void FindRecruitPage()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        MainSubPage fallback = null;
        foreach (MainSubPage page in
                 Resources.FindObjectsOfTypeAll<MainSubPage>())
        {
            if (page == null ||
                EditorUtility.IsPersistent(page) ||
                !page.gameObject.scene.IsValid() ||
                !page.gameObject.scene.isLoaded ||
                !IsRecruitPage(page))
            {
                continue;
            }

            fallback ??= page;
            if (page.gameObject.scene == activeScene)
            {
                SetTarget(page);
                return;
            }
        }

        SetTarget(fallback);
    }

    private void SetTarget(MainSubPage page)
    {
        _targetPage = page;
        _serializedTarget = page != null
            ? new SerializedObject(page)
            : null;
        _selectedBannerIndex = 0;
        ClearSimulation();
        Repaint();
    }

    private void EnsureSerializedTarget()
    {
        if (_targetPage != null &&
            (_serializedTarget == null ||
             _serializedTarget.targetObject != _targetPage))
        {
            _serializedTarget = new SerializedObject(_targetPage);
        }
    }

    private static bool IsRecruitPage(MainSubPage page)
    {
        if (page == null)
            return false;
        SerializedObject serialized = new(page);
        SerializedProperty pageType = serialized.FindProperty("pageType");
        return pageType != null &&
               pageType.enumValueIndex ==
               (int)EMainSubPageType.Recruit;
    }

    private void ClearSimulation()
    {
        _simulationSummary = string.Empty;
        _simulationResults.Clear();
    }

    private static string GetTrimmedString(
        SerializedProperty parent,
        string propertyName)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        return property?.stringValue?.Trim() ?? string.Empty;
    }

    private static void SetString(
        SerializedProperty parent,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);
        if (property != null)
            property.stringValue = value ?? string.Empty;
    }

    private static string Fallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private readonly struct ProbabilityRow
    {
        public string Label { get; }
        public CharacterGrade Grade { get; }
        public double Probability { get; }

        public ProbabilityRow(
            string label,
            CharacterGrade grade,
            double probability)
        {
            Label = label ?? string.Empty;
            Grade = CharacterGradePresentation.Clamp(grade);
            Probability = Math.Max(0d, probability);
        }
    }

    private readonly struct SimulationResult
    {
        public string Label { get; }
        public int Count { get; }
        public double ObservedPercent { get; }
        public double ExpectedPercent { get; }
        public double DeviationPercent =>
            ObservedPercent - ExpectedPercent;

        public SimulationResult(
            string label,
            int count,
            double observedPercent,
            double expectedPercent)
        {
            Label = label ?? string.Empty;
            Count = Mathf.Max(0, count);
            ObservedPercent = observedPercent;
            ExpectedPercent = expectedPercent;
        }
    }

    private readonly struct ValidationIssue
    {
        public string Message { get; }
        public MessageType Type { get; }

        public ValidationIssue(string message, MessageType type)
        {
            Message = message ?? string.Empty;
            Type = type;
        }
    }
}

internal sealed class RecruitRewardMultiSelectWindow : EditorWindow
{
    private readonly List<UnityEngine.Object> _candidates = new();
    private readonly HashSet<int> _selectedIds = new();
    private CharacterGrade _grade;
    private RecruitRewardType _rewardType;
    private Action<IReadOnlyList<UnityEngine.Object>> _completed;
    private string _search = string.Empty;
    private Vector2 _scroll;

    public static void Open(
        CharacterGrade grade,
        RecruitRewardType rewardType,
        Action<IReadOnlyList<UnityEngine.Object>> completed)
    {
        RecruitRewardMultiSelectWindow window =
            CreateInstance<RecruitRewardMultiSelectWindow>();
        window.titleContent = new GUIContent(
            rewardType == RecruitRewardType.Character
                ? $"{(int)grade}등급 캐릭터 선택"
                : $"{(int)grade}등급 아이템 선택");
        window._grade = grade;
        window._rewardType = rewardType;
        window._completed = completed;
        window.BuildCandidates();
        window.minSize = new Vector2(520f, 520f);
        window.position = new Rect(
            GUIUtility.GUIToScreenPoint(new Vector2(120f, 100f)),
            new Vector2(600f, 650f));
        window.ShowUtility();
        window.Focus();
    }

    private void BuildCandidates()
    {
        _candidates.Clear();
        if (_rewardType == RecruitRewardType.Character)
        {
            foreach (CharacterSO character in
                     CharacterDefinitionCatalog.GetAll())
            {
                if (character != null && character.Grade == _grade)
                    _candidates.Add(character);
            }
        }
        else
        {
            foreach (ItemDefinitionSO item in
                     ItemDefinitionCatalog.GetAll())
            {
                if (item != null)
                    _candidates.Add(item);
            }
        }

        _candidates.Sort((left, right) =>
            string.Compare(
                GetName(left),
                GetName(right),
                StringComparison.OrdinalIgnoreCase));
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            $"{(int)_grade}등급에 추가할 " +
            (_rewardType == RecruitRewardType.Character
                ? "캐릭터"
                : "아이템"),
            EditorStyles.boldLabel);
        _search = EditorGUILayout.TextField("검색", _search);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("검색 결과 전체 선택"))
            {
                foreach (UnityEngine.Object candidate in
                         GetFilteredCandidates())
                {
                    _selectedIds.Add(candidate.GetInstanceID());
                }
            }
            if (GUILayout.Button("선택 해제"))
                _selectedIds.Clear();
        }

        EditorGUILayout.Space(4f);
        _scroll = EditorGUILayout.BeginScrollView(
            _scroll,
            EditorStyles.helpBox);
        foreach (UnityEngine.Object candidate in GetFilteredCandidates())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int id = candidate.GetInstanceID();
                bool selected = _selectedIds.Contains(id);
                bool next = EditorGUILayout.Toggle(
                    selected,
                    GUILayout.Width(22f));
                if (next != selected)
                {
                    if (next)
                        _selectedIds.Add(id);
                    else
                        _selectedIds.Remove(id);
                }

                Texture icon = AssetPreview.GetMiniThumbnail(candidate);
                GUILayout.Label(
                    icon,
                    GUILayout.Width(24f),
                    GUILayout.Height(24f));
                EditorGUILayout.LabelField(
                    GetName(candidate),
                    GUILayout.MinWidth(180f));
                EditorGUILayout.ObjectField(
                    candidate,
                    candidate.GetType(),
                    false,
                    GUILayout.Width(220f));
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                $"{_selectedIds.Count}개 선택",
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_selectedIds.Count == 0))
            {
                if (GUILayout.Button("선택 항목 추가", GUILayout.Height(30f)))
                    Complete();
            }
            if (GUILayout.Button("취소", GUILayout.Height(30f)))
                Close();
        }
    }

    private IEnumerable<UnityEngine.Object> GetFilteredCandidates()
    {
        string query = _search?.Trim() ?? string.Empty;
        for (int index = 0; index < _candidates.Count; index++)
        {
            UnityEngine.Object candidate = _candidates[index];
            if (candidate == null)
                continue;
            if (query.Length == 0 ||
                GetName(candidate).IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                yield return candidate;
            }
        }
    }

    private void Complete()
    {
        List<UnityEngine.Object> selected = new();
        for (int index = 0; index < _candidates.Count; index++)
        {
            UnityEngine.Object candidate = _candidates[index];
            if (candidate != null &&
                _selectedIds.Contains(candidate.GetInstanceID()))
            {
                selected.Add(candidate);
            }
        }

        Action<IReadOnlyList<UnityEngine.Object>> completed =
            _completed;
        Close();
        completed?.Invoke(selected);
    }

    private static string GetName(UnityEngine.Object candidate)
    {
        return candidate switch
        {
            CharacterSO character =>
                !string.IsNullOrWhiteSpace(character.CharacterName)
                    ? character.CharacterName
                    : character.name,
            ItemDefinitionSO item => item.GetDisplayName(true),
            _ => candidate != null ? candidate.name : string.Empty,
        };
    }
}
