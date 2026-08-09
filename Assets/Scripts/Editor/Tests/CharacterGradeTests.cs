using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CharacterGradeTests
{
    [Test]
    public void CharacterDefinition_StoresGradeThree()
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            SerializedObject serialized = new(definition);
            serialized.FindProperty("grade").enumValueIndex =
                (int)CharacterGrade.Grade3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                definition.Grade,
                Is.EqualTo(CharacterGrade.Grade3));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void SharedPalette_ProvidesStyleForEveryGrade()
    {
        CharacterGradePresentation.Invalidate();
        Assert.That(CharacterGradePresentation.Palette, Is.Not.Null);

        for (int value = 0; value <= 3; value++)
        {
            CharacterGrade grade = (CharacterGrade)value;
            CharacterGradeStyle style =
                CharacterGradePresentation.GetStyle(grade);
            Assert.That(style, Is.Not.Null);
            Assert.That(style.PrimaryColor.a, Is.GreaterThan(0f));
            Assert.That(style.BackgroundColor.a, Is.GreaterThan(0f));
            Assert.That(style.OutlineColor.a, Is.GreaterThan(0f));
            Assert.That(style.TextColor.a, Is.GreaterThan(0f));
            Assert.That(
                CharacterGradePresentation.GetIcon(grade),
                Is.SameAs(style.GradeIcon));
            Assert.That(
                CharacterGradePresentation.GetIconCount(grade),
                Is.EqualTo(value));
            Assert.That(
                CharacterGradePresentation.GradeIconColor,
                Is.EqualTo(Color.white));
        }
    }

    [Test]
    public void DummyPoolEntry_UsesSharedGradeColor()
    {
        RecruitDummyPoolEntry entry =
            JsonUtility.FromJson<RecruitDummyPoolEntry>(
                "{\"grade\":2,\"rate\":1}");

        Assert.That(
            entry.Grade,
            Is.EqualTo(CharacterGrade.Grade2));
        Assert.That(
            entry.DisplayColor,
            Is.EqualTo(
                CharacterGradePresentation.GetPrimaryColor(
                    CharacterGrade.Grade2)));
    }

    [Test]
    public void OperatorRoster_PreservesAuthoredGridAndUsesGradeColors()
    {
        Scene scene = OpenClientScene(out bool opened);
        try
        {
            Transform roster = FindSceneTransform(
                scene,
                "grpOperatorRoster");
            Assert.That(roster, Is.Not.Null);
            CharacterGradeStyle style =
                CharacterGradePresentation.GetStyle(
                    CharacterGrade.Grade2);
            CharacterRoleSO role = AssetDatabase.LoadAssetAtPath<
                CharacterRoleSO>(
                "Assets/Resources/Presentation/Roles/RoleScout.asset");
            Assert.That(role, Is.Not.Null);
            Assert.That(role.IconSprite, Is.Not.Null);
            Transform content = roster.Find(
                "scrRosterList/vptRosterList/" +
                "grpRosterCardContent");
            GridLayoutGroup grid =
                content?.GetComponent<GridLayoutGroup>();
            Assert.That(grid, Is.Not.Null);
            GridLayoutGroup.Corner authoredCorner = grid.startCorner;
            GridLayoutGroup.Axis authoredAxis = grid.startAxis;
            TextAnchor authoredAlignment = grid.childAlignment;
            OperatorRosterView view =
                OperatorRosterView.Build(roster.parent);
            view.SetItems(new[]
            {
                new OperatorRosterItemModel(
                    "operator-grade-2",
                    "GRADE 2",
                    null,
                    role.IconSprite,
                    CharacterGrade.Grade2,
                    style.BackgroundColor,
                    style.PrimaryColor,
                    style.OutlineColor,
                    style.TextColor),
            });

            Transform card = content.Find("btnOperatorCard_0");
            Assert.That(
                card.GetComponent<Image>().color,
                Is.EqualTo(style.BackgroundColor));
            Assert.That(
                card.Find("imgOperatorNamePlate/imgOperatorAccent")
                    .GetComponent<Image>().color,
                Is.EqualTo(style.PrimaryColor));
            Assert.That(
                card.GetComponent<Outline>().effectColor,
                Is.EqualTo(style.OutlineColor));
            Assert.That(card.Find("grpOperatorMark"), Is.Null);
            Transform roleRoot = card.Find(
                "imgOperatorNamePlate/grpOperatorRole");
            Assert.That(roleRoot, Is.Not.Null);
            Assert.That(roleRoot.gameObject.activeSelf, Is.True);
            Assert.That(
                roleRoot.Find("imgOperatorRoleIcon")
                    ?.GetComponent<Image>()?.sprite,
                Is.SameAs(role.IconSprite));
            Transform gradeIcons = card.Find("grpOperatorGradeIcons");
            Assert.That(gradeIcons, Is.Not.Null);
            Assert.That(gradeIcons.parent, Is.SameAs(card));
            Assert.That(gradeIcons.gameObject.activeSelf, Is.True);
            RectTransform gradeRect = gradeIcons as RectTransform;
            Assert.That(gradeRect.anchorMin, Is.EqualTo(Vector2.right));
            Assert.That(gradeRect.anchorMax, Is.EqualTo(Vector2.right));
            Assert.That(gradeRect.pivot, Is.EqualTo(Vector2.right));
            HorizontalLayoutGroup gradeLayout =
                gradeIcons.GetComponent<HorizontalLayoutGroup>();
            Assert.That(
                gradeLayout.childAlignment,
                Is.EqualTo(TextAnchor.MiddleRight));
            Assert.That(gradeLayout.reverseArrangement, Is.True);
            Assert.That(gradeIcons.childCount, Is.EqualTo(3));
            Assert.That(
                gradeIcons.Cast<Transform>().Count(icon =>
                    icon.gameObject.activeSelf),
                Is.EqualTo(2));
            for (int index = 0;
                 index < gradeIcons.childCount;
                 index++)
            {
                Assert.That(
                    gradeIcons.GetChild(index)
                        .GetComponent<Image>().color,
                    Is.EqualTo(Color.white));
            }
            Assert.That(grid.startCorner, Is.EqualTo(authoredCorner));
            Assert.That(grid.startAxis, Is.EqualTo(authoredAxis));
            Assert.That(grid.childAlignment, Is.EqualTo(authoredAlignment));
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void OperatorDetail_UsesRequestedThreeColumnLayout()
    {
        Scene scene = OpenClientScene(out bool opened);
        try
        {
            Transform detail = FindSceneTransform(
                scene,
                "grpOperatorDetail");
            Assert.That(detail, Is.Not.Null);
            OperatorDetailView view =
                OperatorDetailView.Build(detail.parent);
            bool? toggleRequest = null;
            view.SetCallbacks(
                null,
                null,
                selected => toggleRequest = selected);
            view.SetData(new OperatorDetailModel(
                "GRADE 3 OPERATOR",
                "operator-grade-3",
                null,
                CharacterGrade.Grade3,
                string.Empty,
                Array.Empty<OperatorStatModel>(),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<OperatorAbilityIconModel>(),
                string.Empty,
                string.Empty,
                Array.Empty<OperatorAbilityIconModel>(),
                string.Empty,
                string.Empty,
                true,
                string.Empty));

            Transform visual = detail.Find("grpOperatorDetailVisual");
            Transform center = detail.Find("grpOperatorDetailCenter");
            Transform right = detail.Find("grpOperatorDetailRight");
            Assert.That(visual, Is.Not.Null);
            Assert.That(center, Is.Not.Null);
            Assert.That(right, Is.Not.Null);

            Transform representative = visual.Find(
                "tglLobbyRepresentative");
            Assert.That(representative, Is.Not.Null);
            Assert.That(representative.GetComponent<Toggle>(), Is.Null);
            Transform toggle = representative.Find("btnToggle");
            Assert.That(toggle, Is.Not.Null);
            ToggleSliderController toggleController =
                toggle.GetComponent<ToggleSliderController>();
            Assert.That(toggleController, Is.Not.Null);
            Assert.That(toggleController.Value, Is.True);
            GameObject toggleSource = PrefabUtility
                .GetCorrespondingObjectFromSource(toggle.gameObject);
            Assert.That(toggleSource, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(toggleSource),
                Is.EqualTo("Assets/Prefabs/UI/Util/btnToggle.prefab"));
            toggleController.OnClick();
            Assert.That(toggleController.Value, Is.False);
            Assert.That(toggleRequest, Is.False);

            RectTransform gradeIcons = visual.Find(
                    "grpOperatorDetailGradeIcons")
                as RectTransform;
            Assert.That(gradeIcons, Is.Not.Null);
            Assert.That(gradeIcons.gameObject.activeSelf, Is.True);
            Assert.That(gradeIcons.anchorMin, Is.EqualTo(Vector2.up));
            Assert.That(gradeIcons.anchorMax, Is.EqualTo(Vector2.up));
            Assert.That(gradeIcons.childCount, Is.EqualTo(3));
            for (int index = 0;
                 index < gradeIcons.childCount;
                 index++)
            {
                Assert.That(
                    gradeIcons.GetChild(index)
                        .GetComponent<Image>().color,
                    Is.EqualTo(Color.white));
            }

            RectTransform stat0 = center.Find("grpOperatorStat_0")
                as RectTransform;
            RectTransform stat2 = center.Find("grpOperatorStat_2")
                as RectTransform;
            RectTransform stat4 = center.Find("grpOperatorStat_4")
                as RectTransform;
            Assert.That(stat0, Is.Not.Null);
            Assert.That(stat2, Is.Not.Null);
            Assert.That(stat4, Is.Not.Null);
            Assert.That(
                stat0.anchoredPosition.y - stat0.rect.height,
                Is.GreaterThan(stat2.anchoredPosition.y));
            Assert.That(
                stat2.anchoredPosition.y - stat2.rect.height,
                Is.GreaterThan(stat4.anchoredPosition.y));

            RectTransform equipmentTitle = center.Find(
                    "txtEquipmentTitle")
                as RectTransform;
            Assert.That(equipmentTitle, Is.Not.Null);
            Assert.That(
                stat4.anchoredPosition.y - stat4.rect.height,
                Is.GreaterThan(equipmentTitle.anchoredPosition.y));
            for (int index = 0; index < 6; index++)
            {
                Assert.That(
                    center.Find($"grpEquipmentSlot_{index}"),
                    Is.Not.Null);
            }

            RectTransform attack = right.Find(
                    "grpOperatorBasicAttack")
                as RectTransform;
            RectTransform passives = right.Find(
                    "grpOperatorPassives")
                as RectTransform;
            RectTransform skills = right.Find("grpOperatorSkills")
                as RectTransform;
            Assert.That(attack, Is.Not.Null);
            Assert.That(passives, Is.Not.Null);
            Assert.That(skills, Is.Not.Null);
            Assert.That(
                attack.anchorMin.y,
                Is.GreaterThan(passives.anchorMax.y));
            Assert.That(
                passives.anchorMin.y,
                Is.GreaterThan(skills.anchorMax.y));
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ClientScene_FixedUiComponentsSurviveSceneReload()
    {
        Scene scene = OpenClientScene(out bool opened);
        try
        {
            List<string> missingScripts = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform item in
                         root.GetComponentsInChildren<Transform>(true))
                {
                    if (item.GetComponents<Component>()
                        .Any(component => component == null))
                    {
                        missingScripts.Add(item.name);
                    }
                }
            }

            Assert.That(
                missingScripts,
                Is.Empty,
                "ClientScene contains missing scripts: " +
                string.Join(", ", missingScripts));

            MainSubPage rosterPage = FindSceneTransform(scene, "pagRoster")
                ?.GetComponent<MainSubPage>();
            Assert.That(rosterPage, Is.Not.Null);
            SerializedObject rosterSerialized = new(rosterPage);
            RectTransform rosterPanel = rosterSerialized
                .FindProperty("_panel").objectReferenceValue as RectTransform;
            Assert.That(rosterPanel, Is.Not.Null);
            Assert.That(rosterPanel.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rosterPanel.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rosterPanel.sizeDelta, Is.EqualTo(Vector2.zero));

            Transform rosterContent = FindSceneTransform(
                scene,
                "grpRosterCardContent");
            Assert.That(
                rosterContent?.GetComponent<ResponsiveGridConstraint>(),
                Is.Not.Null);

            CharacterCodexPage characterPage = FindSceneTransform(
                    scene,
                    "pagCharacterCodex")
                ?.GetComponent<CharacterCodexPage>();
            Assert.That(characterPage, Is.Not.Null);
            SerializedObject characterSerialized = new(characterPage);
            RectTransform characterRoot = characterSerialized
                .FindProperty("_runtimeRoot")
                .objectReferenceValue as RectTransform;
            Assert.That(characterRoot, Is.Not.Null);
            Transform operatorDetail = characterRoot.Find(
                "grpOperatorDetail");
            Transform characterBackButton = characterRoot.Find(
                "btnBACKTOCODEX");
            Assert.That(operatorDetail, Is.Not.Null);
            Assert.That(characterBackButton, Is.Not.Null);
            Assert.That(characterBackButton.gameObject.activeSelf, Is.True);
            Assert.That(
                characterBackButton.GetComponent<Button>(),
                Is.Not.Null);
            Assert.That(
                characterBackButton.Find("txtLabel")
                    ?.GetComponent<TMPro.TextMeshProUGUI>(),
                Is.Not.Null);
            Assert.That(
                characterBackButton.GetSiblingIndex(),
                Is.GreaterThan(operatorDetail.GetSiblingIndex()),
                "Character detail back button must render above the " +
                "full-screen operator detail layout.");

            DungeonPage dungeonPage = FindSceneTransform(scene, "pagDungeon")
                ?.GetComponent<DungeonPage>();
            Assert.That(dungeonPage, Is.Not.Null);
            SerializedObject dungeonSerialized = new(dungeonPage);
            Assert.That(
                dungeonSerialized.FindProperty("characterInfoPrefab")
                    .objectReferenceValue,
                Is.Not.Null);

            DungeonBattleTab battleTab = dungeonSerialized
                .FindProperty("battleTab").objectReferenceValue
                as DungeonBattleTab;
            Assert.That(battleTab, Is.Not.Null);
            SerializedObject battleSerialized = new(battleTab);
            Assert.That(
                battleSerialized.FindProperty("_pausePanelFitter")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                battleSerialized.FindProperty("_activeSkillResourceView")
                    .objectReferenceValue,
                Is.Not.Null);
            DungeonActiveSkillResourceView activeSkillResourceView =
                battleSerialized.FindProperty("_activeSkillResourceView")
                    .objectReferenceValue
                as DungeonActiveSkillResourceView;
            Assert.That(activeSkillResourceView, Is.Not.Null);
            SerializedObject resourceSerialized =
                new(activeSkillResourceView);
            string[] requiredResourceReferences =
            {
                "_iconImage",
                "_cooldownOverlay",
                "_fallbackIconText",
                "_amountText",
                "_tooltipText",
                "_tooltip",
            };
            foreach (string propertyName in requiredResourceReferences)
            {
                Assert.That(
                    resourceSerialized.FindProperty(propertyName)
                        .objectReferenceValue,
                    Is.Not.Null,
                    $"Active skill resource reference is missing: " +
                    propertyName);
            }
            Assert.That(
                battleSerialized.FindProperty("_itemHandView")
                    .objectReferenceValue,
                Is.Not.Null);

            MethodInfo validateReferences = typeof(DungeonBattleTab)
                .GetMethod(
                    "ValidateReferences",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(validateReferences, Is.Not.Null);
            Assert.That(
                (bool)validateReferences.Invoke(battleTab, null),
                Is.True);

            Transform attendancePanel = FindSceneTransform(
                scene,
                "grpAttendancePanel");
            Assert.That(
                attendancePanel?.GetComponent<ResponsivePanelFitter>(),
                Is.Not.Null);

            GameObject rewardCard = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Presentation/DungeonRewardCard.prefab");
            Assert.That(rewardCard, Is.Not.Null);
            Assert.That(
                rewardCard.GetComponent<DungeonRewardCardHoverView>(),
                Is.Not.Null);
            Assert.That(
                rewardCard.GetComponents<Component>()
                    .Any(component => component == null),
                Is.False);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Scene OpenClientScene(out bool opened)
    {
        const string path = "Assets/Scenes/ClientScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        opened = !scene.IsValid() || !scene.isLoaded;
        return opened
            ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
            : scene;
    }

    private static Transform FindSceneTransform(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == name);
            if (match != null)
                return match;
        }
        return null;
    }
}

public sealed class LocalDataBuildResetTests
{
    private static readonly MethodInfo ShouldResetForBuildMethod =
        typeof(LocalDataResetService).GetMethod(
            "ShouldResetForBuild",
            BindingFlags.Static | BindingFlags.NonPublic);

    [Test]
    public void BuildChangeReset_RequiresEnabledValidDifferentGuid()
    {
        string current = Guid.NewGuid().ToString("N");
        string previous = Guid.NewGuid().ToString("N");

        Assert.That(ShouldReset(false, previous, current), Is.False);
        Assert.That(ShouldReset(true, current, current), Is.False);
        Assert.That(
            ShouldReset(
                true,
                new Guid(current).ToString("D").ToUpperInvariant(),
                current),
            Is.False);
        Assert.That(ShouldReset(true, previous, current), Is.True);
        Assert.That(ShouldReset(true, string.Empty, current), Is.True);
    }

    [TestCase("")]
    [TestCase("not-a-guid")]
    [TestCase("00000000000000000000000000000000")]
    public void BuildChangeReset_RejectsInvalidCurrentGuid(string current)
    {
        Assert.That(
            ShouldReset(true, Guid.NewGuid().ToString("N"), current),
            Is.False);
    }

    private static bool ShouldReset(
        bool enabled,
        string previous,
        string current)
    {
        Assert.That(ShouldResetForBuildMethod, Is.Not.Null);
        return (bool)ShouldResetForBuildMethod.Invoke(
            null,
            new object[] { enabled, previous, current });
    }
}

public sealed class CharacterRoleTests
{
    [Test]
    public void CommonSettingsProvider_UsesProjectSettingsScope()
    {
        SettingsProvider provider =
            CommonSettingsProjectProvider.CreateSettingsProvider();

        Assert.That(provider, Is.Not.Null);
        Assert.That(
            provider.settingsPath,
            Is.EqualTo(CommonSettingsProjectProvider.SettingsPath));
        Assert.That(
            provider.scope,
            Is.EqualTo(SettingsScope.Project));
    }

    [Test]
    public void RoleLocalization_UsesKeyWithSingleFallbackFields()
    {
        CharacterRoleSO role =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterArchetypeSO archetype =
            ScriptableObject.CreateInstance<CharacterArchetypeSO>();
        try
        {
            SerializedObject roleSerialized = new(role);
            Assert.That(
                roleSerialized.FindProperty("koreanName"),
                Is.Null);
            Assert.That(
                roleSerialized.FindProperty("englishName"),
                Is.Null);
            roleSerialized.FindProperty("nameLocalizationKey")
                .stringValue = "test.missing.role.name";
            roleSerialized.FindProperty("descriptionLocalizationKey")
                .stringValue = "test.missing.role.description";
            roleSerialized.FindProperty("fallbackName").stringValue =
                "Fallback Role";
            roleSerialized.FindProperty("fallbackDescription")
                .stringValue = "Fallback Role Description";
            SerializedProperty rolePassives =
                roleSerialized.FindProperty("passiveDefinitions");
            rolePassives.arraySize = 1;
            SerializedProperty passive =
                rolePassives.GetArrayElementAtIndex(0);
            Assert.That(
                passive.FindPropertyRelative("koreanDescription"),
                Is.Null);
            Assert.That(
                passive.FindPropertyRelative("englishDescription"),
                Is.Null);
            passive.FindPropertyRelative("nameLocalizationKey")
                .stringValue = "test.missing.passive.name";
            passive.FindPropertyRelative("descriptionLocalizationKey")
                .stringValue = "test.missing.passive.description";
            passive.FindPropertyRelative("fallbackName").stringValue =
                "Fallback Passive";
            passive.FindPropertyRelative("fallbackDescription")
                .stringValue = "Fallback Description";
            roleSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject archetypeSerialized = new(archetype);
            Assert.That(
                archetypeSerialized.FindProperty("koreanName"),
                Is.Null);
            Assert.That(
                archetypeSerialized.FindProperty("englishName"),
                Is.Null);
            archetypeSerialized.FindProperty("nameLocalizationKey")
                .stringValue = "test.missing.archetype.name";
            archetypeSerialized.FindProperty("descriptionLocalizationKey")
                .stringValue = "test.missing.archetype.description";
            archetypeSerialized.FindProperty("fallbackName")
                .stringValue = "Fallback Archetype";
            archetypeSerialized.FindProperty("fallbackDescription")
                .stringValue = "Fallback Archetype Description";
            archetypeSerialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                role.GetDisplayName(),
                Is.EqualTo("Fallback Role"));
            Assert.That(
                role.GetDescription(),
                Is.EqualTo("Fallback Role Description"));
            Assert.That(
                role.PassiveDefinitions[0].GetDisplayName(),
                Is.EqualTo("Fallback Passive"));
            Assert.That(
                role.PassiveDefinitions[0].GetDescription(),
                Is.EqualTo("Fallback Description"));
            Assert.That(
                archetype.GetDisplayName(),
                Is.EqualTo("Fallback Archetype"));
            Assert.That(
                archetype.GetDescription(),
                Is.EqualTo("Fallback Archetype Description"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(archetype);
            UnityEngine.Object.DestroyImmediate(role);
        }
    }

    [Test]
    public void RoleAndArchetypeIds_CanBeRegeneratedForDuplication()
    {
        CharacterRoleSO role =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterArchetypeSO archetype =
            ScriptableObject.CreateInstance<CharacterArchetypeSO>();
        try
        {
            string originalRoleId = role.RoleId;
            string originalArchetypeId = archetype.ArchetypeId;

            role.RegenerateRoleId();
            archetype.RegenerateArchetypeId();

            Assert.That(role.RoleId, Is.Not.Empty);
            Assert.That(role.RoleId, Is.Not.EqualTo(originalRoleId));
            Assert.That(archetype.ArchetypeId, Is.Not.Empty);
            Assert.That(
                archetype.ArchetypeId,
                Is.Not.EqualTo(originalArchetypeId));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(archetype);
            UnityEngine.Object.DestroyImmediate(role);
        }
    }

    [Test]
    public void LocalizationPicker_UsesAllKeysAndHierarchicalMenuPaths()
    {
        PS260714LocalizationKeyField.Refresh();
        var keys = PS260714LocalizationKeyField.GetKeys();

        Assert.That(keys, Does.Contain("roll.main.vanguard.name"));
        Assert.That(keys, Does.Contain("character.suiren.name"));
        Assert.That(keys, Does.Contain("ui.title.notice"));
        Assert.That(
            PS260714LocalizationKeyField.GetMenuPath(
                "character.suiren.name"),
            Is.EqualTo("character/suiren/name"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SharedPassiveEditor_AddsSectionsToNestedRoleAbility(
        bool useArchetype)
    {
        ScriptableObject owner = useArchetype
            ? ScriptableObject.CreateInstance<CharacterArchetypeSO>()
            : ScriptableObject.CreateInstance<CharacterRoleSO>();
        try
        {
            SerializedObject serialized = new(owner);
            SerializedProperty passives =
                serialized.FindProperty("passiveDefinitions");
            passives.arraySize = 1;
            SerializedProperty ability = passives.GetArrayElementAtIndex(0)
                .FindPropertyRelative("ability");
            ability.FindPropertyRelative("sections").ClearArray();
            string abilityPath = ability.propertyPath;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                CharacterEditorWindow.AddPassiveSectionForEditor(
                    owner,
                    abilityPath,
                    CharacterPassiveSectionType.Ability),
                Is.True);
            Assert.That(
                CharacterEditorWindow.AddPassiveSectionForEditor(
                    owner,
                    abilityPath,
                    CharacterPassiveSectionType.Ability),
                Is.False,
                "동일 구성 블록은 중복 추가되면 안 됩니다.");

            SerializedObject result = new(owner);
            SerializedProperty resultAbility = result.FindProperty(
                abilityPath);
            SerializedProperty sections = resultAbility
                .FindPropertyRelative("sections");
            Assert.That(sections.arraySize, Is.EqualTo(1));
            Assert.That(
                sections.GetArrayElementAtIndex(0).enumValueIndex,
                Is.EqualTo((int)CharacterPassiveSectionType.Ability));
            Assert.That(
                resultAbility.FindPropertyRelative("effects").arraySize,
                Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void SharedPassiveEditor_InitializesClonedAbilityAsNew()
    {
        CharacterRoleSO role =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        try
        {
            SerializedObject serialized = new(role);
            SerializedProperty passives =
                serialized.FindProperty("passiveDefinitions");
            passives.arraySize = 1;
            SerializedProperty originalAbility = passives
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("ability");
            originalAbility.FindPropertyRelative("actionId").stringValue =
                "original";
            SerializedProperty originalSections = originalAbility
                .FindPropertyRelative("sections");
            originalSections.arraySize = 1;
            originalSections.GetArrayElementAtIndex(0).enumValueIndex =
                (int)CharacterPassiveSectionType.Linkage;

            passives.arraySize = 2;
            SerializedProperty addedAbility = passives
                .GetArrayElementAtIndex(1)
                .FindPropertyRelative("ability");
            CharacterEditorWindow.InitializeEmbeddedPassiveDefinition(
                addedAbility,
                "passive_2");

            Assert.That(
                addedAbility.FindPropertyRelative("actionId").stringValue,
                Is.EqualTo("passive_2"));
            Assert.That(
                addedAbility.FindPropertyRelative("sections").arraySize,
                Is.Zero);
            Assert.That(
                addedAbility.FindPropertyRelative("effects").arraySize,
                Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(role);
        }
    }

    [Test]
    public void ExplorationArchetype_GainsAttackPowerPerCompletedStage()
    {
        CharacterArchetypeSO exploration =
            AssetDatabase.LoadAssetAtPath<CharacterArchetypeSO>(
                "Assets/Resources/Presentation/Archetypes/" +
                "RoleExploration.asset");

        Assert.That(exploration, Is.Not.Null);
        Assert.That(exploration.PassiveDefinitions, Has.Count.GreaterThan(0));
        CharacterPassiveDefinition passive =
            exploration.PassiveDefinitions[0].Ability;
        Assert.That(passive.HasStatModifierSection, Is.True);
        Assert.That(passive.StatModifiers, Has.Count.EqualTo(1));
        CharacterPassiveStatModifierDefinition modifier =
            passive.StatModifiers[0];
        Assert.That(
            modifier.StatType,
            Is.EqualTo(StatusEffectStatType.AttackPower));
        Assert.That(
            modifier.Mode,
            Is.EqualTo(StatusEffectStatModifierMode.Flat));
        Assert.That(modifier.BaseValue, Is.EqualTo(0f));
        Assert.That(modifier.DungeonStageProgressScale, Is.EqualTo(1f));
    }

    [Test]
    public void CharacterData_MergesRolePassiveBeforePersonalPassive()
    {
        CharacterRoleSO role =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterSO character =
            ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            SerializedObject roleSerialized = new(role);
            SerializedProperty rolePassives =
                roleSerialized.FindProperty("passiveDefinitions");
            rolePassives.arraySize = 1;
            SerializedProperty rolePassive =
                rolePassives.GetArrayElementAtIndex(0);
            rolePassive.FindPropertyRelative("passiveId").stringValue =
                "role.passive";
            ConfigurePassive(
                rolePassive.FindPropertyRelative("ability"));
            roleSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject characterSerialized = new(character);
            characterSerialized.FindProperty("role").objectReferenceValue =
                role;
            SerializedProperty personalPassives =
                characterSerialized.FindProperty("passiveDefinitions");
            personalPassives.arraySize = 1;
            ConfigurePassive(
                personalPassives.GetArrayElementAtIndex(0));
            characterSerialized.ApplyModifiedPropertiesWithoutUndo();

            CharacterData data = character.CreateData();

            Assert.That(data.PassiveDefinitions, Has.Count.EqualTo(2));
            Assert.That(data.ResolvedPassives, Has.Count.EqualTo(2));
            Assert.That(
                data.ResolvedPassives[0].Origin,
                Is.EqualTo(CharacterPassiveOrigin.Role));
            Assert.That(
                data.ResolvedPassives[0].Role,
                Is.SameAs(role));
            Assert.That(
                data.ResolvedPassives[1].Origin,
                Is.EqualTo(CharacterPassiveOrigin.Character));
            Assert.That(data.HasCustomPassiveDefinitions, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(character);
            UnityEngine.Object.DestroyImmediate(role);
        }
    }

    [Test]
    public void CharacterData_MergesArchetypePassiveBetweenRoleAndCharacter()
    {
        CharacterRoleSO role =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterArchetypeSO archetype =
            ScriptableObject.CreateInstance<CharacterArchetypeSO>();
        CharacterSO character =
            ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            SerializedObject roleSerialized = new(role);
            SerializedProperty rolePassives =
                roleSerialized.FindProperty("passiveDefinitions");
            rolePassives.arraySize = 1;
            SerializedProperty rolePassive =
                rolePassives.GetArrayElementAtIndex(0);
            rolePassive.FindPropertyRelative("passiveId").stringValue =
                "role.passive";
            ConfigurePassive(
                rolePassive.FindPropertyRelative("ability"));
            roleSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject archetypeSerialized = new(archetype);
            archetypeSerialized.FindProperty("parentRole")
                .objectReferenceValue = role;
            SerializedProperty archetypePassives =
                archetypeSerialized.FindProperty("passiveDefinitions");
            archetypePassives.arraySize = 1;
            SerializedProperty archetypePassive =
                archetypePassives.GetArrayElementAtIndex(0);
            archetypePassive.FindPropertyRelative("passiveId").stringValue =
                "archetype.passive";
            ConfigurePassive(
                archetypePassive.FindPropertyRelative("ability"));
            archetypeSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject characterSerialized = new(character);
            characterSerialized.FindProperty("role").objectReferenceValue =
                role;
            characterSerialized.FindProperty("archetype")
                .objectReferenceValue = archetype;
            SerializedProperty personalPassives =
                characterSerialized.FindProperty("passiveDefinitions");
            personalPassives.arraySize = 1;
            ConfigurePassive(
                personalPassives.GetArrayElementAtIndex(0));
            characterSerialized.ApplyModifiedPropertiesWithoutUndo();

            CharacterData data = character.CreateData();

            Assert.That(data.PassiveDefinitions, Has.Count.EqualTo(3));
            Assert.That(data.ResolvedPassives, Has.Count.EqualTo(3));
            Assert.That(
                data.ResolvedPassives[0].Origin,
                Is.EqualTo(CharacterPassiveOrigin.Role));
            Assert.That(
                data.ResolvedPassives[1].Origin,
                Is.EqualTo(CharacterPassiveOrigin.Archetype));
            Assert.That(
                data.ResolvedPassives[1].Archetype,
                Is.SameAs(archetype));
            Assert.That(
                data.ResolvedPassives[1].ArchetypePassive,
                Is.SameAs(archetype.PassiveDefinitions[0]));
            Assert.That(
                data.ResolvedPassives[2].Origin,
                Is.EqualTo(CharacterPassiveOrigin.Character));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(character);
            UnityEngine.Object.DestroyImmediate(archetype);
            UnityEngine.Object.DestroyImmediate(role);
        }
    }

    [Test]
    public void CharacterValidation_PreservesArchetypeFromDifferentRole()
    {
        CharacterRoleSO roleA =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterRoleSO roleB =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterArchetypeSO archetype =
            ScriptableObject.CreateInstance<CharacterArchetypeSO>();
        CharacterSO character =
            ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            SerializedObject archetypeSerialized = new(archetype);
            archetypeSerialized.FindProperty("parentRole")
                .objectReferenceValue = roleA;
            archetypeSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject characterSerialized = new(character);
            characterSerialized.FindProperty("role").objectReferenceValue =
                roleB;
            characterSerialized.FindProperty("archetype")
                .objectReferenceValue = archetype;
            characterSerialized.ApplyModifiedPropertiesWithoutUndo();

            MethodInfo onValidate = typeof(CharacterSO).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onValidate, Is.Not.Null);
            onValidate.Invoke(character, null);

            Assert.That(character.Role, Is.SameAs(roleB));
            Assert.That(character.Archetype, Is.SameAs(archetype));
            Assert.That(
                CharacterRolePresentation.IsValidCombination(
                    roleB,
                    archetype),
                Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(character);
            UnityEngine.Object.DestroyImmediate(archetype);
            UnityEngine.Object.DestroyImmediate(roleB);
            UnityEngine.Object.DestroyImmediate(roleA);
        }
    }

    [Test]
    public void RoleCatalogValidation_ReportsUnregisteredParentRole()
    {
        CharacterRoleSO registeredRole =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterRoleSO unregisteredRole =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterArchetypeSO archetype =
            ScriptableObject.CreateInstance<CharacterArchetypeSO>();
        CharacterRoleCatalogSO catalog =
            ScriptableObject.CreateInstance<CharacterRoleCatalogSO>();
        try
        {
            SerializedObject archetypeSerialized = new(archetype);
            archetypeSerialized.FindProperty("parentRole")
                .objectReferenceValue = unregisteredRole;
            archetypeSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject catalogSerialized = new(catalog);
            SerializedProperty roles =
                catalogSerialized.FindProperty("roles");
            roles.arraySize = 1;
            roles.GetArrayElementAtIndex(0).objectReferenceValue =
                registeredRole;
            SerializedProperty archetypes =
                catalogSerialized.FindProperty("archetypes");
            archetypes.arraySize = 1;
            archetypes.GetArrayElementAtIndex(0).objectReferenceValue =
                archetype;
            catalogSerialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(catalog.GetValidationIssues(), Is.Not.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(archetype);
            UnityEngine.Object.DestroyImmediate(unregisteredRole);
            UnityEngine.Object.DestroyImmediate(registeredRole);
        }
    }

    private static void ConfigurePassive(SerializedProperty passive)
    {
        SerializedProperty sections =
            passive.FindPropertyRelative("sections");
        sections.arraySize = 1;
        sections.GetArrayElementAtIndex(0).enumValueIndex =
            (int)CharacterPassiveSectionType.Ability;
    }
}
