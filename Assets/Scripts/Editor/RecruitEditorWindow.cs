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
            "2. 더미 모집 풀 및 확률 시뮬레이션");
        if (!_poolExpanded)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "시뮬레이션 전용 데이터입니다. 캐릭터를 지급하거나 재화를 차감하지 않습니다.",
                MessageType.Info);

            SerializedProperty mode =
                banner.FindPropertyRelative("rateInputMode");
            SerializedProperty pool =
                banner.FindPropertyRelative("dummyPool");
            EditorGUILayout.PropertyField(
                mode,
                new GUIContent("입력 방식"));

            bool valid = TryBuildProbabilityRows(
                banner,
                out List<ProbabilityRow> probabilityRows,
                out string probabilityError,
                out double inputTotal);
            string totalLabel = mode.enumValueIndex ==
                                (int)RecruitRateInputMode.Percentage
                ? $"{inputTotal:0.####}%"
                : $"{inputTotal:0.####}";
            EditorGUILayout.LabelField("입력 합계", totalLabel);

            int removeIndex = -1;
            int moveFrom = -1;
            int moveTo = -1;
            for (int index = 0; index < pool.arraySize; index++)
            {
                SerializedProperty entry =
                    pool.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            $"더미 항목 {index + 1}",
                            EditorStyles.boldLabel);
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
                                   index >= pool.arraySize - 1))
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

                    DrawProperty(entry, "displayName", "표시 이름");
                    SerializedProperty grade =
                        entry.FindPropertyRelative("grade");
                    grade.enumValueIndex = EditorGUILayout.IntSlider(
                        "등급",
                        grade.enumValueIndex,
                        0,
                        3);
                    DrawGradePalettePreview(
                        (CharacterGrade)grade.enumValueIndex);
                    DrawProperty(
                        entry,
                        "rate",
                        mode.enumValueIndex ==
                        (int)RecruitRateInputMode.Percentage
                            ? "확률 (%)"
                            : "가중치");
                    DrawProperty(entry, "pickup", "픽업");

                    if (valid && index < probabilityRows.Count)
                    {
                        EditorGUILayout.LabelField(
                            "정규화 확률",
                            $"{probabilityRows[index].Probability * 100d:0.####}%");
                    }
                }
            }

            if (removeIndex >= 0)
                RemovePoolEntry(pool, removeIndex);
            if (moveFrom >= 0)
                MoveArrayElement(pool, moveFrom, moveTo, "더미 항목 순서 변경");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("더미 항목 추가"))
                {
                    AddPoolEntry(
                        pool,
                        "더미 항목",
                        CharacterGrade.Grade0,
                        1f);
                }
                if (GUILayout.Button("샘플 확률 4종 채우기"))
                    FillSamplePool(pool);
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
        SerializedProperty pool =
            banner.FindPropertyRelative("dummyPool");
        if (pool == null || pool.arraySize == 0)
        {
            error = "더미 모집 풀이 비어 있습니다.";
            return false;
        }

        double[] rates = new double[pool.arraySize];
        for (int index = 0; index < pool.arraySize; index++)
        {
            SerializedProperty entry =
                pool.GetArrayElementAtIndex(index);
            float rate = entry.FindPropertyRelative("rate").floatValue;
            if (float.IsNaN(rate) ||
                float.IsInfinity(rate) ||
                rate < 0f)
            {
                error = $"{index + 1}번 더미 항목의 확률 값이 올바르지 않습니다.";
                return false;
            }
            rates[index] = rate;
            inputTotal += rate;
        }

        if (inputTotal <= 0d)
        {
            error = "확률 값 중 하나 이상은 0보다 커야 합니다.";
            return false;
        }

        RecruitRateInputMode mode = (RecruitRateInputMode)banner
            .FindPropertyRelative("rateInputMode")
            .enumValueIndex;
        if (mode == RecruitRateInputMode.Percentage &&
            Math.Abs(inputTotal - 100d) > 0.01d)
        {
            error =
                $"직접 확률의 합계가 100%가 아닙니다. 현재 {inputTotal:0.####}%입니다.";
            return false;
        }

        for (int index = 0; index < pool.arraySize; index++)
        {
            SerializedProperty entry =
                pool.GetArrayElementAtIndex(index);
            string label = GetTrimmedString(entry, "displayName");
            rows.Add(new ProbabilityRow(
                Fallback(label, $"더미 항목 {index + 1}"),
                rates[index] / inputTotal));
        }
        return true;
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
        banner.FindPropertyRelative("paymentRoutes").arraySize = 0;
        banner.FindPropertyRelative("defaultPaymentRouteIndex").intValue = 0;
        banner.FindPropertyRelative("interactionEnabled").boolValue = true;
    }

    private void AddPoolEntry(
        SerializedProperty pool,
        string displayName,
        CharacterGrade grade,
        float rate)
    {
        RecordUndo("더미 모집 항목 추가");
        int index = pool.arraySize;
        pool.InsertArrayElementAtIndex(index);
        InitializePoolEntry(
            pool.GetArrayElementAtIndex(index),
            displayName,
            grade,
            rate);
        ClearSimulation();
        ApplyAndExit();
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

    private void FillSamplePool(SerializedProperty pool)
    {
        if (pool.arraySize > 0 &&
            !EditorUtility.DisplayDialog(
                "샘플 확률 채우기",
                "현재 더미 풀을 0등급 40%, 1등급 50%, " +
                "2등급 8%, 3등급 2%로 교체하시겠습니까?",
                "교체",
                "취소"))
        {
            return;
        }

        RecordUndo("더미 모집 샘플 확률 채우기");
        pool.arraySize = 4;
        InitializePoolEntry(
            pool.GetArrayElementAtIndex(0),
            "더미 0등급",
            CharacterGrade.Grade0,
            40f);
        InitializePoolEntry(
            pool.GetArrayElementAtIndex(1),
            "더미 1등급",
            CharacterGrade.Grade1,
            50f);
        InitializePoolEntry(
            pool.GetArrayElementAtIndex(2),
            "더미 2등급",
            CharacterGrade.Grade2,
            8f);
        InitializePoolEntry(
            pool.GetArrayElementAtIndex(3),
            "더미 3등급",
            CharacterGrade.Grade3,
            2f);
        SerializedProperty mode = pool.serializedObject
            .FindProperty("recruitBannerPages")
            .GetArrayElementAtIndex(_selectedBannerIndex)
            .FindPropertyRelative("rateInputMode");
        mode.enumValueIndex = (int)RecruitRateInputMode.Percentage;
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
        Rect preview = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.DrawRect(preview, style.BackgroundColor);
        EditorGUI.DrawRect(
            new Rect(preview.x, preview.y, 8f, preview.height),
            style.PrimaryColor);
        Handles.DrawSolidRectangleWithOutline(
            preview,
            Color.clear,
            style.OutlineColor);

        GUIStyle labelStyle = new(EditorStyles.miniBoldLabel);
        labelStyle.normal.textColor = style.TextColor;
        EditorGUI.LabelField(
            new Rect(
                preview.x + 14f,
                preview.y,
                preview.width - 18f,
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
        public double Probability { get; }

        public ProbabilityRow(string label, double probability)
        {
            Label = label ?? string.Empty;
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
