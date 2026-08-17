using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DynamicUiScenePreviewInstaller
{
    private const string ScenePath = "Assets/04_Scenes/ClientScene.unity";
    private const string AttendanceCellPath =
        "Assets/06_Runtime/Resources/Presentation/AttendanceRewardCell.prefab";
    private const string BuffIconPath =
        "Assets/06_Runtime/Resources/Presentation/CharacterBuffIcon.prefab";
    private const string OperatorRosterCardPath =
        "Assets/06_Runtime/Resources/Presentation/OperatorRosterCard.prefab";
    private const string RestCharacterSdPath =
        "Assets/06_Runtime/Resources/Presentation/DungeonRestCharacterSd.prefab";
    private const string TogglePath =
        "Assets/07_Prefabs/UI/Util/btnToggle.prefab";

    [MenuItem(
        PS260714EditorMenu.InstallDynamicUiPreviews,
        false,
        PS260714EditorMenu.InstallDynamicUiPreviewsPriority)]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);

        InstallAttendanceCells(scene);
        InstallCodexCards(scene);
        InstallOperatorPreviews(scene);
        InstallDungeonPreviews(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save ClientScene.");

        AssetDatabase.SaveAssets();
        Debug.Log("Installed dynamic UI prefab previews in ClientScene.");
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void InstallOperatorUiFromCommandLine()
    {
        try
        {
            InstallOperatorRosterCardLayout();
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            InstallOperatorDetailToggle(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    "Failed to save operator UI changes in ClientScene.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Installed operator role and representative UI.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void InstallOperatorRosterCardLayout()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(
            OperatorRosterCardPath);
        Require(contents, "Operator roster card prefab contents");
        try
        {
            Transform root = contents.transform;
            Transform namePlate = root.Find("imgOperatorNamePlate");
            Require(namePlate, "Operator roster name plate");

            Transform mark = root.Find("grpOperatorMark");
            if (mark != null)
                UnityEngine.Object.DestroyImmediate(mark.gameObject);

            Transform roleRoot = namePlate.Find("grpOperatorRole") ??
                                 namePlate.Find("grpOperatorSkill");
            Require(roleRoot, "Operator role icon root");
            roleRoot.name = "grpOperatorRole";
            Transform roleIcon = roleRoot.Find("imgOperatorRoleIcon") ??
                                 roleRoot.Find("imgOperatorSkillIcon");
            Require(roleIcon, "Operator role icon");
            roleIcon.name = "imgOperatorRoleIcon";

            Transform gradeRoot = root.Find("grpOperatorGradeIcons") ??
                                  namePlate.Find("grpOperatorGradeIcons");
            Require(gradeRoot, "Operator grade icon root");
            gradeRoot.SetParent(root, false);
            RectTransform gradeRect = gradeRoot as RectTransform;
            Require(gradeRect, "Operator grade icon RectTransform");
            gradeRect.anchorMin = new Vector2(1f, 0f);
            gradeRect.anchorMax = new Vector2(1f, 0f);
            gradeRect.pivot = new Vector2(1f, 0f);
            gradeRect.anchoredPosition = new Vector2(-10f, 72f);
            gradeRect.sizeDelta = new Vector2(54f, 18f);

            HorizontalLayoutGroup layout =
                gradeRoot.GetComponent<HorizontalLayoutGroup>();
            Require(layout, "Operator grade icon layout");
            layout.childAlignment = TextAnchor.MiddleRight;
            SerializedObject layoutSerialized = new(layout);
            SerializedProperty reverse = layoutSerialized.FindProperty(
                "m_ReverseArrangement");
            if (reverse != null)
                reverse.boolValue = true;
            layoutSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                contents,
                OperatorRosterCardPath);
            Require(saved, "Saved operator roster card prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void InstallOperatorDetailToggle(Scene scene)
    {
        OperatorDetailDesignerSettings detail =
            FindOne<OperatorDetailDesignerSettings>(scene);
        Transform row = detail.transform.Find(
            "grpOperatorDetailVisual/tglLobbyRepresentative");
        Require(row, "Lobby representative row");

        Toggle legacyToggle = row.GetComponent<Toggle>();
        if (legacyToggle != null)
            UnityEngine.Object.DestroyImmediate(legacyToggle);
        Image legacyBackground = row.GetComponent<Image>();
        if (legacyBackground != null)
            UnityEngine.Object.DestroyImmediate(legacyBackground);
        CanvasRenderer legacyRenderer = row.GetComponent<CanvasRenderer>();
        if (legacyRenderer != null)
            UnityEngine.Object.DestroyImmediate(legacyRenderer);

        GameObject togglePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            TogglePath);
        Require(togglePrefab, "btnToggle prefab");
        GameObject toggle = EnsureSinglePrefab(
            row,
            togglePrefab,
            "btnToggle",
            item => item.GetComponent<ToggleSliderController>() != null);
        RectTransform toggleRect = toggle.transform as RectTransform;
        Require(toggleRect, "btnToggle RectTransform");
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-8f, 0f);
        toggleRect.sizeDelta = new Vector2(90f, 45f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(toggleRect);

        RectTransform label = row.Find("txtLabel") as RectTransform;
        Require(label, "Lobby representative label");
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(12f, 4f);
        label.offsetMax = new Vector2(-110f, -4f);
    }

    private static void InstallAttendanceCells(Scene scene)
    {
        MonthlyAttendancePopupView popup = FindOne<MonthlyAttendancePopupView>(
            scene);
        SerializedObject popupSerialized = new(popup);
        RectTransform calendarRoot = popupSerialized
            .FindProperty("calendarRoot").objectReferenceValue as RectTransform;
        AttendanceRewardCellView prefab = AssetDatabase.LoadAssetAtPath<
            AttendanceRewardCellView>(AttendanceCellPath);
        Require(calendarRoot, "Attendance calendar root");
        Require(prefab, "Attendance reward cell prefab");

        List<GameObject> authored = DirectChildren(calendarRoot)
            .Where(item => item.GetComponent<AttendanceRewardCellView>() != null)
            .ToList();
        foreach (GameObject item in authored)
            UnityEngine.Object.DestroyImmediate(item);

        for (int index = 0;
             index < AttendanceRewardScheduleSO.CycleRewardCount;
             index++)
        {
            GameObject cell = InstantiatePrefab(prefab.gameObject, calendarRoot);
            cell.name = $"grpAttendanceReward{index + 1:00}";
            cell.SetActive(true);
        }
    }

    private static void InstallCodexCards(Scene scene)
    {
        foreach (CodexBrowserDesignerSettings settings in
                 FindAll<CodexBrowserDesignerSettings>(scene))
        {
            Require(settings.CardContent, "Codex card content");
            Require(settings.CardTemplate, "Codex card prefab");
            EnsureSinglePrefab(
                settings.CardContent,
                settings.CardTemplate,
                "btnCodexCard_0",
                item => item.name.StartsWith(
                    "btnCodexCard_",
                    StringComparison.Ordinal));
        }
    }

    private static void InstallOperatorPreviews(Scene scene)
    {
        OperatorRosterDesignerSettings roster =
            FindOne<OperatorRosterDesignerSettings>(scene);
        Transform rosterContent = roster.transform.Find(
            "scrRosterList/vptRosterList/grpRosterCardContent");
        Require(rosterContent, "Operator roster card content");
        Require(roster.CardPrefab, "Operator roster card prefab");
        EnsureSinglePrefab(
            rosterContent,
            roster.CardPrefab,
            "btnOperatorCard_0",
            item => item.name.StartsWith(
                "btnOperatorCard_",
                StringComparison.Ordinal));

        OperatorDetailDesignerSettings detail =
            FindOne<OperatorDetailDesignerSettings>(scene);
        Require(detail.AbilityIconPrefab, "Operator ability icon prefab");
        Transform passiveRoot = detail.transform.Find(
            "grpOperatorDetailRight/grpOperatorPassives/grpPassiveIconRoot");
        Transform skillRoot = detail.transform.Find(
            "grpOperatorDetailRight/grpOperatorSkills/grpSkillIconRoot");
        Require(passiveRoot, "Operator passive icon root");
        Require(skillRoot, "Operator skill icon root");
        EnsureSinglePrefab(
            passiveRoot,
            detail.AbilityIconPrefab,
            "grpPassiveIcon_0",
            item => item.name.StartsWith(
                "grpPassiveIcon_",
                StringComparison.Ordinal));
        EnsureSinglePrefab(
            skillRoot,
            detail.AbilityIconPrefab,
            "grpSkillIcon_0",
            item => item.name.StartsWith(
                "grpSkillIcon_",
                StringComparison.Ordinal));
    }

    private static void InstallDungeonPreviews(Scene scene)
    {
        DungeonPage page = FindOne<DungeonPage>(scene);
        SerializedObject pageSerialized = new(page);
        CharacterRuntime characterPrefab = pageSerialized
            .FindProperty("characterInfoPrefab").objectReferenceValue
            as CharacterRuntime;
        DungeonRewardCardView rewardPrefab = pageSerialized
            .FindProperty("rewardCardPrefab").objectReferenceValue
            as DungeonRewardCardView;
        DungeonDynamicChoiceButtonView choicePrefab = pageSerialized
            .FindProperty("choiceButtonPrefab").objectReferenceValue
            as DungeonDynamicChoiceButtonView;
        DungeonStartingItemSlotView startingItemPrefab = pageSerialized
            .FindProperty("startingItemSlotPrefab").objectReferenceValue
            as DungeonStartingItemSlotView;
        Image restCharacterSdPrefab = AssetDatabase.LoadAssetAtPath<
                GameObject>(RestCharacterSdPath)
            ?.GetComponent<Image>();
        Require(characterPrefab, "Character info prefab");
        Require(rewardPrefab, "Dungeon reward card prefab");
        Require(choicePrefab, "Dungeon choice button prefab");
        Require(startingItemPrefab, "Dungeon starting item slot prefab");
        Require(restCharacterSdPrefab, "Dungeon rest character SD prefab");
        pageSerialized.FindProperty("restCharacterSdPrefab")
            .objectReferenceValue = restCharacterSdPrefab;
        pageSerialized.ApplyModifiedPropertiesWithoutUndo();

        InstallCharacterPreview(scene, page, pageSerialized, characterPrefab);
        InstallBattleItemPreviews(scene);
        InstallSpawnQueuePreviews(scene);

        foreach (Transform root in FindTransforms(scene, "grpEventButtons"))
        {
            EnsureSingleComponentPrefab(
                root,
                choicePrefab,
                "btnEventChoice_Preview");
        }
        foreach (Transform root in FindTransforms(scene, "grpRoomChoices"))
        {
            EnsureSingleComponentPrefab(
                root,
                choicePrefab,
                "btnRoomChoice_Preview");
        }

        CharacterSO restSample = CharacterDefinitionCatalog.GetAll()
            .FirstOrDefault(item => item != null &&
                                    item.SittingSdSprite != null) ??
            CharacterDefinitionCatalog.GetAll().FirstOrDefault();
        foreach (Transform root in FindTransforms(
                     scene,
                     "grpRestCharacterSds"))
        {
            Image preview = EnsureSingleComponentPrefab(
                root,
                restCharacterSdPrefab,
                "imgRestCharacterSd_Preview");
            preview.sprite = restSample != null
                ? restSample.SittingSdSprite != null
                    ? restSample.SittingSdSprite
                    : restSample.WaitingSdSprite
                : null;
            preview.color = preview.sprite != null
                ? Color.white
                : new Color(1f, 1f, 1f, 0.15f);
            preview.enabled = true;
        }

        foreach (Transform root in FindTransforms(scene, "grpRewardCards"))
        {
            DungeonRewardCardView reward = EnsureSingleComponentPrefab(
                root,
                rewardPrefab,
                "btnRewardCard_Preview");
            reward.Bind(
                "REWARD",
                "PREVIEW",
                "Runtime reward description",
                "SELECT",
                new Color(0.3f, 0.68f, 0.4f, 1f),
                null);

            if (!HasAncestor(root, "grpBattleRewardOverlay"))
            {
                EnsureSingleComponentPrefab(
                    root,
                    startingItemPrefab,
                    "grpStartingItem_Preview");
            }
        }
    }

    private static void InstallCharacterPreview(
        Scene scene,
        DungeonPage page,
        SerializedObject pageSerialized,
        CharacterRuntime prefab)
    {
        DungeonBattleTab battleTab = pageSerialized
            .FindProperty("battleTab").objectReferenceValue as DungeonBattleTab;
        RectTransform root = battleTab != null
            ? battleTab.transform.Find(
                "grpPlayerPartyInfo/grpPlayerPartySlots") as RectTransform
            : null;
        Require(root, "Player character info root");

        CharacterSO sample = DirectChildren(root)
            .Select(item => item.GetComponent<CharacterRuntime>())
            .Where(item => item != null && item.Definition != null)
            .Select(item => item.Definition)
            .FirstOrDefault();
        sample ??= CharacterDefinitionCatalog.GetAll().FirstOrDefault();

        CharacterRuntime preview = EnsureSingleComponentPrefab(
            root,
            prefab,
            "grpPlayerCharacterSlot_1");
        SerializedObject previewSerialized = new(preview);
        previewSerialized.FindProperty("original").objectReferenceValue = sample;
        previewSerialized.ApplyModifiedPropertiesWithoutUndo();
        if (sample != null)
        {
            Image standingImage = previewSerialized.FindProperty("standingImage")
                .objectReferenceValue as Image;
            Sprite standingSprite = sample.StandingSprite != null
                ? sample.StandingSprite
                : sample.IconSprite;
            if (standingImage != null)
            {
                standingImage.sprite = standingSprite;
                standingImage.enabled = standingSprite != null;
            }

            CharacterStandingPortraitView portraitView = previewSerialized
                .FindProperty("standingPortraitView").objectReferenceValue
                as CharacterStandingPortraitView;
            Require(portraitView, "Character standing portrait view");
            portraitView.Configure(
                standingSprite,
                sample.DungeonHudStandingFocus,
                sample.DungeonHudStandingZoom);
            EditorUtility.SetDirty(portraitView);
        }

        CharacterBuffIconView buffPrefab = AssetDatabase.LoadAssetAtPath<
            CharacterBuffIconView>(BuffIconPath);
        RectTransform buffRoot = previewSerialized
            .FindProperty("buffIconContainer").objectReferenceValue
            as RectTransform;
        Require(buffPrefab, "Character buff icon prefab");
        Require(buffRoot, "Character buff icon root");
        CharacterBuffIconView buffPreview = EnsureSingleComponentPrefab(
            buffRoot,
            buffPrefab,
            "icoBuff_Preview");
        buffPreview.Refresh(
            sample != null ? sample.IconSprite : null,
            1,
            true,
            6f,
            10f);

        pageSerialized.FindProperty("playerCharacterRoot")
            .objectReferenceValue = root;
        SerializedProperty characters = pageSerialized
            .FindProperty("playerCharacters");
        characters.arraySize = DungeonPage.MaximumPartySize;
        for (int index = 0; index < characters.arraySize; index++)
        {
            characters.GetArrayElementAtIndex(index).objectReferenceValue =
                index == 0 ? preview : null;
        }
        pageSerialized.ApplyModifiedPropertiesWithoutUndo();

        DungeonFieldView fieldView = FindOne<DungeonFieldView>(scene);
        SerializedObject fieldSerialized = new(fieldView);
        SerializedProperty fieldCharacters = fieldSerialized
            .FindProperty("playerCharacters");
        fieldCharacters.arraySize = DungeonPage.MaximumPartySize;
        for (int index = 0; index < fieldCharacters.arraySize; index++)
        {
            fieldCharacters.GetArrayElementAtIndex(index).objectReferenceValue =
                index == 0 ? preview : null;
        }
        fieldSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void InstallBattleItemPreviews(Scene scene)
    {
        foreach (DungeonItemHandView hand in FindAll<DungeonItemHandView>(scene))
        {
            SerializedObject serialized = new(hand);
            DungeonItemCardView prefab = serialized.FindProperty("cardPrefab")
                .objectReferenceValue as DungeonItemCardView;
            Require(prefab, "Battle item card prefab");
            EnsureSingleComponentPrefab(
                hand.transform,
                prefab,
                "crdBattleItem_Preview");
        }
    }

    private static void InstallSpawnQueuePreviews(Scene scene)
    {
        foreach (DungeonSpawnQueueView queue in
                 FindAll<DungeonSpawnQueueView>(scene))
        {
            SerializedObject serialized = new(queue);
            RectTransform content = serialized.FindProperty("content")
                .objectReferenceValue as RectTransform;
            DungeonSpawnQueueItemView prefab = serialized
                .FindProperty("itemPrefab").objectReferenceValue
                as DungeonSpawnQueueItemView;
            Require(content, "Spawn queue content");
            Require(prefab, "Spawn queue item prefab");
            EnsureSingleComponentPrefab(
                content,
                prefab,
                "grpSpawnQueueItem_Preview");
        }
    }

    private static T EnsureSingleComponentPrefab<T>(
        Transform parent,
        T prefab,
        string objectName)
        where T : Component
    {
        GameObject instance = EnsureSinglePrefab(
            parent,
            prefab.gameObject,
            objectName,
            item => item.GetComponent<T>() != null);
        return instance.GetComponent<T>();
    }

    private static GameObject EnsureSinglePrefab(
        Transform parent,
        GameObject prefab,
        string objectName,
        Func<GameObject, bool> candidateFilter)
    {
        Require(parent, $"{objectName} parent");
        Require(prefab, $"{objectName} prefab");
        GameObject keep = null;
        foreach (GameObject candidate in DirectChildren(parent)
                     .Where(candidateFilter)
                     .ToArray())
        {
            GameObject source = PrefabUtility
                .GetCorrespondingObjectFromSource(candidate);
            if (keep == null && source == prefab)
            {
                keep = candidate;
                continue;
            }

            UnityEngine.Object.DestroyImmediate(candidate);
        }

        keep ??= InstantiatePrefab(prefab, parent);
        keep.name = objectName;
        keep.SetActive(true);
        keep.transform.SetAsLastSibling();
        PrefabUtility.RecordPrefabInstancePropertyModifications(keep);
        return keep;
    }

    private static GameObject InstantiatePrefab(
        GameObject prefab,
        Transform parent)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(
            prefab,
            parent) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException(
                $"Could not instantiate prefab '{prefab.name}'.");
        }

        return instance;
    }

    private static void RemoveDirectComponents<T>(Transform parent)
        where T : Component
    {
        foreach (GameObject child in DirectChildren(parent).ToArray())
        {
            if (child.GetComponent<T>() != null)
                UnityEngine.Object.DestroyImmediate(child);
        }
    }

    private static IEnumerable<GameObject> DirectChildren(Transform parent)
    {
        for (int index = 0; index < parent.childCount; index++)
            yield return parent.GetChild(index).gameObject;
    }

    private static T FindOne<T>(Scene scene)
        where T : Component
    {
        T[] found = FindAll<T>(scene).ToArray();
        if (found.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one {typeof(T).Name} in ClientScene, found " +
                $"{found.Length}.");
        }

        return found[0];
    }

    private static IEnumerable<T> FindAll<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static IEnumerable<Transform> FindTransforms(
        Scene scene,
        string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(item => item.name == objectName);
    }

    private static bool HasAncestor(Transform transform, string objectName)
    {
        for (Transform current = transform; current != null;
             current = current.parent)
        {
            if (current.name == objectName)
                return true;
        }

        return false;
    }

    private static void Require(UnityEngine.Object value, string label)
    {
        if (value == null)
            throw new InvalidOperationException($"{label} is missing.");
    }
}
