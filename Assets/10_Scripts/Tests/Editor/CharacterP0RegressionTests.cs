using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PS260714.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static TestReflection;

public sealed class CharacterP0RegressionTests
{
    private const string SuirenAssetPath =
        "fixture:cooldown-cleanse";
    private const string AislingAssetPath =
        "fixture:previous-target-status";
    private const string IsoldeAssetPath =
        "Assets/06_Runtime/Resources/Characters/2_Isolde.asset";
    private const string CalistaAssetPath =
        "Assets/06_Runtime/Resources/Characters/2_Calista.asset";
    private const string EmergencyKitAssetPath =
        "Assets/06_Runtime/Resources/StatusEffects/EmergencyKit.asset";
    private const string FireAssetPath =
        "Assets/06_Runtime/Resources/StatusEffects/Fire.asset";
    private const string OpeningAssetPath =
        "Assets/06_Runtime/Resources/StatusEffects/Opening.asset";
    private const string ComboAssetPath =
        "Assets/06_Runtime/Resources/StatusEffects/Combo.asset";
    private const string StarPowderAssetPath =
        "Assets/06_Runtime/Resources/StatusEffects/StarPowder.asset";
    private const string StunAssetPath =
        "Assets/06_Runtime/Resources/StatusEffects/Stun.asset";

    private readonly List<CharacterRuntime> _characters = new();
    private readonly List<UnityEngine.Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (CharacterRuntime character in _characters)
        {
            if (character != null)
                character.BindBattle(null, null);
        }

        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
        }

        _characters.Clear();
        _createdObjects.Clear();
    }

    [Test]
    public void BattleAreaGeometry_CircleUsesRadiusBoundary()
    {
        Assert.That(
            BattleAreaGeometry.Contains(
                new Vector2(1.5f, 0f),
                Vector2.zero,
                Vector2.up,
                1.5f,
                360f),
            Is.True);
        Assert.That(
            BattleAreaGeometry.Contains(
                new Vector2(1.51f, 0f),
                Vector2.zero,
                Vector2.up,
                1.5f,
                360f),
            Is.False);
        Assert.That(
            BattleAreaGeometry.Contains(
                Vector2.down,
                Vector2.zero,
                Vector2.up,
                1.5f,
                360f),
            Is.True);
    }

    [Test]
    public void BattleAreaGeometry_180DegreeSectorExcludesRearHalf()
    {
        Assert.That(
            BattleAreaGeometry.Contains(
                Vector2.up,
                Vector2.zero,
                Vector2.up,
                2f,
                180f),
            Is.True);
        Assert.That(
            BattleAreaGeometry.Contains(
                Vector2.down,
                Vector2.zero,
                Vector2.up,
                2f,
                180f),
            Is.False);
    }

    [Test]
    public void BattleAreaGeometry_SectorUsesConfiguredFullAngle()
    {
        Vector2 inside = Quaternion.Euler(0f, 0f, -29f) * Vector2.up;
        Vector2 outside = Quaternion.Euler(0f, 0f, -31f) * Vector2.up;

        Assert.That(
            BattleAreaGeometry.Contains(
                inside,
                Vector2.zero,
                Vector2.up,
                2f,
                60f),
            Is.True);
        Assert.That(
            BattleAreaGeometry.Contains(
                outside,
                Vector2.zero,
                Vector2.up,
                2f,
                60f),
            Is.False);
    }

    [Test]
    public void BattleAreaGeometry_ZeroDegreeSectorUsesAimRay()
    {
        Assert.That(
            BattleAreaGeometry.Contains(
                Vector2.up,
                Vector2.zero,
                Vector2.up,
                2f,
                0f),
            Is.True);
        Assert.That(
            BattleAreaGeometry.Contains(
                new Vector2(0.01f, 1f),
                Vector2.zero,
                Vector2.up,
                2f,
                0f),
            Is.False);
    }

    [Test]
    public void ManualWorldAreaRequest_ZeroTargetCountRequiresOnlyPoint()
    {
        BattleAreaDefinition area = new();
        SetPrivateField(
            area,
            "shapeType",
            CharacterAreaShapeType.CircleSector);
        BattleManualTargetSelectionRequest request = new(
            null,
            CharacterTargetFaction.Enemy,
            0,
            new EnemyRuntime[] { null, null, null },
            null,
            true,
            _ => { },
            area);

        Assert.That(request.TargetCount, Is.Zero);
        Assert.That(request.CandidateCount, Is.EqualTo(3));
        Assert.That(request.RequiredCount, Is.Zero);
    }

    [Test]
    public void BattleAreaGeometry_ClampKeepsDestinationInsideWall()
    {
        Vector2 clamped = BattleAreaGeometry.ClampToRadius(
            new Vector2(4f, 0f),
            Vector2.zero,
            2f);

        Assert.That(clamped, Is.EqualTo(new Vector2(2f, 0f)));
    }

    [Test]
    public void CharacterDesignatedArea_UsesRangeLimitedPointerInsteadOfCasterOrigin()
    {
        BattleAreaDefinition area = new();
        SetPrivateField(
            area,
            "originMode",
            CharacterAreaOriginMode.DesignatedPoint);
        SetPrivateField(area, "maxCastDistance", 2f);

        Vector2 source = new(5f, 1f);
        Vector2 resolved = BattleAreaGeometry.ResolveManualOrigin(
            new Vector2(9f, 1f),
            source,
            area,
            100f,
            BattleManualAreaPlacementMode.AbilityConstrained);

        Assert.That(area.OriginMode,
            Is.EqualTo(CharacterAreaOriginMode.DesignatedPoint));
        Assert.That(resolved, Is.EqualTo(new Vector2(7f, 1f)));
        Assert.That(resolved, Is.Not.EqualTo(source));
    }

    [Test]
    public void BattleArenaRingMeshBuilder_CreatesClosedAnnularPrism()
    {
        Mesh mesh = new();
        _createdObjects.Add(mesh);

        BattleArenaRingMeshBuilder.Populate(mesh, 2.14f, 2.34f, 0.08f, 96);

        Assert.That(mesh.vertexCount, Is.EqualTo(776));
        Assert.That(mesh.triangles.Length, Is.EqualTo(2304));
        Assert.That(mesh.bounds.size.x, Is.EqualTo(4.68f).Within(0.001f));
        Assert.That(mesh.bounds.size.y, Is.EqualTo(0.08f).Within(0.001f));
        Assert.That(mesh.bounds.size.z, Is.EqualTo(4.68f).Within(0.001f));
        Assert.That(mesh.bounds.center.y, Is.EqualTo(0.04f).Within(0.001f));
    }

    [Test]
    public void CharacterEditorStandingPreview_FitsOneByTwoWithoutStretching()
    {
        Rect fitted = CharacterEditorWindow.CalculateAspectFitRect(
            new Rect(0f, 0f, 200f, 200f),
            1024f / 2048f);

        Assert.That(fitted.x, Is.EqualTo(50f).Within(0.001f));
        Assert.That(fitted.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(fitted.width, Is.EqualTo(100f).Within(0.001f));
        Assert.That(fitted.height, Is.EqualTo(200f).Within(0.001f));
    }

    [Test]
    public void CharacterEditorIconAndSdPreview_FitSquareWithoutStretching()
    {
        Rect fitted = CharacterEditorWindow.CalculateAspectFitRect(
            new Rect(0f, 0f, 240f, 120f),
            1024f / 1024f);

        Assert.That(fitted.x, Is.EqualTo(60f).Within(0.001f));
        Assert.That(fitted.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(fitted.width, Is.EqualTo(120f).Within(0.001f));
        Assert.That(fitted.height, Is.EqualTo(120f).Within(0.001f));
    }

    [Test]
    public void StandingPortrait_CoverLayoutPreservesOneByTwoArtwork()
    {
        Vector2 rendered = CharacterStandingPortraitView.CalculateRenderedSize(
            new Vector2(152f, 140f),
            new Vector2(1024f, 2048f),
            1f);

        Assert.That(rendered.x, Is.EqualTo(152f).Within(0.001f));
        Assert.That(rendered.y, Is.EqualTo(304f).Within(0.001f));
    }

    [Test]
    public void StandingPortrait_DefaultFocusMovesTallArtworkIntoMask()
    {
        Vector2 viewport = new(152f, 140f);
        Vector2 rendered = new(152f, 304f);
        Vector2 anchored = CharacterStandingPortraitView
            .CalculateAnchoredPosition(
                viewport,
                rendered,
                CharacterStandingFraming.DefaultFocus);
        Rect visible = CharacterStandingPortraitView.CalculateVisibleSourceRect(
            viewport,
            rendered,
            anchored);

        Assert.That(anchored.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(anchored.y, Is.EqualTo(-30.4f).Within(0.001f));
        Assert.That(visible.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(visible.width, Is.EqualTo(1f).Within(0.001f));
        Assert.That(visible.height, Is.EqualTo(140f / 304f).Within(0.001f));
        Assert.That(visible.y, Is.EqualTo(0.3697368f).Within(0.001f));
    }

    [Test]
    public void StandingPortrait_OnValidateDoesNotMutateRectTransformLayout()
    {
        GameObject viewportObject = new(
            "StandingViewport",
            typeof(RectTransform),
            typeof(CharacterStandingPortraitView));
        GameObject artworkObject = new(
            "StandingArtwork",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        _createdObjects.Add(viewportObject);
        artworkObject.transform.SetParent(
            viewportObject.transform,
            false);

        CharacterStandingPortraitView view =
            viewportObject.GetComponent<CharacterStandingPortraitView>();
        RectTransform viewport =
            viewportObject.GetComponent<RectTransform>();
        Image artwork = artworkObject.GetComponent<Image>();
        Texture2D texture = new(10, 20);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 10f, 20f),
            new Vector2(0.5f, 0.5f));
        _createdObjects.Add(texture);
        _createdObjects.Add(sprite);
        SetPrivateField(view, "viewport", viewport);
        SetPrivateField(view, "artwork", artwork);
        viewport.sizeDelta = new Vector2(100f, 100f);
        artwork.sprite = sprite;
        artwork.rectTransform.sizeDelta = new Vector2(17f, 19f);
        artwork.enabled = true;

        MethodInfo onValidate =
            typeof(CharacterStandingPortraitView).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(onValidate, Is.Not.Null);
        onValidate.Invoke(view, null);

        Assert.That(artwork.enabled, Is.True);
        Assert.That(
            artwork.rectTransform.sizeDelta,
            Is.EqualTo(new Vector2(17f, 19f)));
    }

    [Test]
    public void ResponsivePanelFitter_ShrinksOversizedContentOnly()
    {
        float reduced = ResponsivePanelFitter.CalculateFitScale(
            new Vector2(1280f, 591f),
            new Vector2(980f, 900f),
            false);
        float unchanged = ResponsivePanelFitter.CalculateFitScale(
            new Vector2(1920f, 1080f),
            new Vector2(980f, 900f),
            false);

        Assert.That(reduced, Is.EqualTo(591f / 900f).Within(0.001f));
        Assert.That(unchanged, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ResponsiveGridConstraint_DerivesColumnsFromViewportWidth()
    {
        int wide = ResponsiveGridConstraint.CalculateColumnCount(
            1180f,
            160f,
            8f,
            4,
            4);
        int narrow = ResponsiveGridConstraint.CalculateColumnCount(
            500f,
            160f,
            8f,
            4,
            4);

        Assert.That(wide, Is.EqualTo(7));
        Assert.That(narrow, Is.EqualTo(2));
    }

    [Test]
    public void ResponsiveCanvasUtility_UsesExpandForPcAspectChanges()
    {
        GameObject canvasObject = new(
            "ResponsiveCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        _createdObjects.Add(canvasObject);
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        ResponsiveCanvasUtility.Configure(scaler);

        Assert.That(
            scaler.screenMatchMode,
            Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
        Assert.That(
            scaler.referenceResolution,
            Is.EqualTo(new Vector2(1920f, 1080f)));
    }

    [Test]
    public void CharacterEditorPreview_TightSpriteUsesFullSourceCanvas()
    {
        Texture2D texture = new(8, 16, TextureFormat.RGBA32, false);
        _createdObjects.Add(texture);
        Color32[] pixels = new Color32[texture.width * texture.height];
        for (int y = 4; y < 12; y++)
        {
            for (int x = 2; x < 6; x++)
                pixels[y * texture.width + x] = Color.white;
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.Tight);
        _createdObjects.Add(sprite);

        bool found = CharacterEditorWindow.TryGetSpriteTextureCoordinates(
            sprite,
            out Texture2D previewTexture,
            out Rect textureCoordinates);

        Assert.That(found, Is.True);
        Assert.That(previewTexture, Is.SameAs(texture));
        Assert.That(textureCoordinates.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(textureCoordinates.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(textureCoordinates.width, Is.EqualTo(1f).Within(0.001f));
        Assert.That(textureCoordinates.height, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void CharacterEditor_AddPassiveCreatesPersistedEmptyDraft()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "EmptyPassiveDraftFixture");
        SerializedObject serialized = new(definition);
        serialized.FindProperty("passiveDefinitions").ClearArray();
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterEditorWindow window =
            ScriptableObject.CreateInstance<CharacterEditorWindow>();
        window.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(window);
        FieldInfo selectedCharacterField =
            typeof(CharacterEditorWindow).GetField(
                "_selectedCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo serializedCharacterField =
            typeof(CharacterEditorWindow).GetField(
                "_serializedCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo addPassive =
            typeof(CharacterEditorWindow).GetMethod(
                "AddPassiveDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(selectedCharacterField, Is.Not.Null);
        Assert.That(serializedCharacterField, Is.Not.Null);
        Assert.That(addPassive, Is.Not.Null);
        selectedCharacterField.SetValue(window, definition);
        serializedCharacterField.SetValue(
            window,
            new SerializedObject(definition));

        addPassive.Invoke(window, null);

        Assert.That(definition.PassiveDefinitions, Has.Count.EqualTo(1));
        Assert.That(
            definition.PassiveDefinitions[0].IsEmptyPlaceholder,
            Is.True);

        MethodInfo onValidate = typeof(CharacterSO).GetMethod(
            "OnValidate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(onValidate, Is.Not.Null);
        onValidate.Invoke(definition, null);

        Assert.That(definition.PassiveDefinitions, Has.Count.EqualTo(1));
        Assert.That(definition.PassiveDefinitions[0].Sections, Is.Empty);

        CharacterData data = definition.CreateData();
        Assert.That(data.ConfiguredPassiveDefinitionCount, Is.Zero);
        Assert.That(data.HasCustomPassiveDefinitions, Is.False);
        Assert.That(
            CharacterLocalization.GetPassiveDescription(data),
            Is.Empty);

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(validation.IsValid, Is.True);
        Assert.That(
            HasDiagnostic(validation, "passive.empty_placeholder"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void CharacterEditor_ChangingCharacterClearsTextEditingFocus()
    {
        CharacterSO first = CreateBaseCharacterFixture(
            "FocusReleaseFirstFixture");
        CharacterSO second = CreateBaseCharacterFixture(
            "FocusReleaseSecondFixture");
        CharacterEditorWindow window =
            ScriptableObject.CreateInstance<CharacterEditorWindow>();
        window.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(window);
        FieldInfo selectedCharacterField =
            typeof(CharacterEditorWindow).GetField(
                "_selectedCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo serializedCharacterField =
            typeof(CharacterEditorWindow).GetField(
                "_serializedCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo selectCharacter =
            typeof(CharacterEditorWindow).GetMethod(
                "SelectCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(selectedCharacterField, Is.Not.Null);
        Assert.That(serializedCharacterField, Is.Not.Null);
        Assert.That(selectCharacter, Is.Not.Null);
        selectedCharacterField.SetValue(window, first);
        serializedCharacterField.SetValue(
            window,
            new SerializedObject(first));

        try
        {
            EditorGUIUtility.editingTextField = true;
            selectCharacter.Invoke(window, new object[] { second, false });

            Assert.That(
                EditorGUIUtility.editingTextField,
                Is.False);
            Assert.That(
                selectedCharacterField.GetValue(window),
                Is.SameAs(second));
        }
        finally
        {
            EditorGUIUtility.editingTextField = false;
        }
    }

    [Test]
    public void LegacyCharacterInfo_DisablesWithMissingTooltipReference()
    {
        GameObject root = new(
            "LegacyCharacterInfoSlot",
            typeof(RectTransform),
            typeof(AudioSource));
        _createdObjects.Add(root);
        CharacterRuntime character =
            root.AddComponent<CharacterRuntime>();
        GameObject destroyedTooltip = new("DestroyedSkillTooltip");
        SerializedObject serialized = new(character);
        serialized.FindProperty("skillTooltip").objectReferenceValue =
            destroyedTooltip;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Object.DestroyImmediate(destroyedTooltip);

        Assert.DoesNotThrow(() => root.SetActive(false));
    }

    [Test]
    public void DungeonCharacterInfo_UsesDesignerEditablePrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(
            "Presentation/CharacterInfo");

        Assert.That(prefab, Is.Not.Null);
        Assert.That(
            prefab.GetComponent<CharacterRuntime>(),
            Is.Not.Null);
        Assert.That(
            ((RectTransform)prefab.transform).sizeDelta,
            Is.EqualTo(new Vector2(270f, 144f)));

        SerializedObject serialized = new(
            prefab.GetComponent<CharacterRuntime>());
        string[] designerReferences =
        {
            "nameText",
            "standingPortraitView",
            "standingImage",
            "healthFill",
            "healthText",
            "selectionHighlight",
            "passiveIconFrame",
            "passiveIconImage",
            "activeSkillIconFrame",
            "activeSkillIconImage",
            "skillTooltip",
            "skillTooltipText",
            "buffIconContainer",
        };
        foreach (string propertyName in designerReferences)
        {
            Assert.That(
                serialized.FindProperty(propertyName)
                    .objectReferenceValue,
                Is.Not.Null,
                $"CharacterInfo prefab reference '{propertyName}' is missing.");
        }
    }

    [Test]
    public void CharacterEditor_PermanentModifierUsesCharacterStatChildOnly()
    {
        FieldInfo valuesField = typeof(CharacterEditorWindow).GetField(
            "PassiveStatModifierStatTypeValues",
            BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo optionsField = typeof(CharacterEditorWindow).GetField(
            "PassiveStatModifierStatTypeOptions",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(valuesField, Is.Not.Null);
        Assert.That(optionsField, Is.Not.Null);

        int[] values = valuesField.GetValue(null) as int[];
        string[] options = optionsField.GetValue(null) as string[];
        Assert.That(values, Is.EqualTo(new[]
        {
            (int)StatusEffectStatType.AttackPower,
            (int)StatusEffectStatType.AttackSpeed,
        }));
        Assert.That(options, Is.EqualTo(new[]
        {
            "공격력",
            "공격 속도",
        }));
        Assert.That(
            CharacterPassiveStatModifierRules.IsSupportedCharacterStat(
                StatusEffectStatType.IncomingDamage),
            Is.False);
    }

    [Test]
    public void CharacterSdSprites_UseDefeatAtZeroAndSittingInRest()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "CharacterSdStateFixture");
        Sprite defeat = CreateTestSprite(Color.red);
        Sprite sitting = CreateTestSprite(Color.green);
        SerializedObject definitionSerialized = new(definition);
        definitionSerialized.FindProperty("defeatSdSprite")
            .objectReferenceValue = defeat;
        definitionSerialized.FindProperty("sittingSdSprite")
            .objectReferenceValue = sitting;
        definitionSerialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(character.Data.DefeatSdSprite, Is.SameAs(defeat));
        Assert.That(character.Data.SittingSdSprite, Is.SameAs(sitting));
        Assert.That(character.ResolveRestSdSprite(), Is.SameAs(sitting));

        Assert.That(
            character.ApplyRunHealthLoss(character.MaximumHealth),
            Is.EqualTo(character.MaximumHealth));
        Assert.That(character.CurrentHealth, Is.Zero);
        Assert.That(character.ResolveCurrentBattleSdSprite(), Is.SameAs(defeat));
        Assert.That(character.ResolveRestSdSprite(), Is.SameAs(defeat));

        Assert.That(character.RestoreHealth(1, true), Is.EqualTo(1));
        Assert.That(character.ResolveRestSdSprite(), Is.SameAs(sitting));
    }

    [Test]
    public void DungeonCharacterInfo_ShowsBuffValueAndTimedRadialOverlay()
    {
        Texture2D texture = new(4, 4, TextureFormat.RGBA32, false);
        Sprite icon = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0.5f));
        _createdObjects.Add(texture);
        _createdObjects.Add(icon);

        StatusEffectSO buff = CreateRuntimeStatus(
            "character-info-visible-buff",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        StatusEffectSO debuff = CreateRuntimeStatus(
            "character-info-hidden-debuff",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        ConfigureStatusRemovalMetadata(
            buff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            debuff,
            StatusEffectAlignment.Debuff,
            true);
        SerializedObject serializedBuff = new(buff);
        serializedBuff.FindProperty("icon").objectReferenceValue = icon;
        serializedBuff.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(
            CreateBaseCharacterFixture("CharacterInfoBuffFixture"));
        Assert.That(
            character.ApplyStatusEffect(buff, 5f, 3),
            Is.True);
        Assert.That(
            character.ApplyStatusEffect(debuff, 5f, 2),
            Is.True);

        Transform container = character.transform.Find("grpBuffIcons");
        Assert.That(container, Is.Not.Null);
        Assert.That(
            container.GetComponent<HorizontalLayoutGroup>()
                .reverseArrangement,
            Is.True);
        Assert.That(container.childCount, Is.EqualTo(1));
        Transform buffIcon = container.GetChild(0);
        CharacterBuffIconView view =
            buffIcon.GetComponent<CharacterBuffIconView>();
        Image overlay = buffIcon.Find("imgBuffDurationOverlay")
            .GetComponent<Image>();
        RectTransform valueRect = buffIcon.Find("txtBuffValue")
            as RectTransform;
        TextMeshProUGUI valueText =
            valueRect.GetComponent<TextMeshProUGUI>();

        Assert.That(view, Is.Not.Null);
        Assert.That(view.HasRequiredPrefabReferences(), Is.True);
        Assert.That(valueText.text, Is.EqualTo("3"));
        Assert.That(valueRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
        Assert.That(valueRect.anchorMax, Is.EqualTo(new Vector2(1f, 0.5f)));
        Assert.That(overlay.sprite, Is.SameAs(icon));
        Assert.That(overlay.type, Is.EqualTo(Image.Type.Filled));
        Assert.That(
            overlay.fillMethod,
            Is.EqualTo(Image.FillMethod.Radial360));
        Assert.That(
            overlay.fillOrigin,
            Is.EqualTo((int)Image.Origin360.Top));
        Assert.That(overlay.fillClockwise, Is.True);
        Assert.That(overlay.fillAmount, Is.EqualTo(1f));

        character.TickBattle(1f, new FakeBattleBoard());
        Assert.That(overlay.fillAmount, Is.EqualTo(0.8f).Within(0.001f));
    }

    [Test]
    public void DungeonCharacterInfo_AbilityIconsTooltipAndAvailabilityRefresh()
    {
        CharacterRuntime character = CreateCharacter(
            CreateBaseCharacterFixture("CharacterInfoAbilityFixture"));
        Transform passiveFrame =
            character.transform.Find("grpPassiveAbilityIcon");
        Transform activeFrame =
            character.transform.Find("grpActiveAbilityIcon");
        Image passiveImage = passiveFrame
            .Find("imgPassiveAbilityIcon")
            .GetComponent<Image>();
        Image activeImage = activeFrame
            .Find("imgActiveAbilityIcon")
            .GetComponent<Image>();

        Assert.That(
            passiveImage.sprite,
            Is.SameAs(character.Data.PassiveSdSprite));
        Assert.That(
            activeImage.sprite,
            Is.SameAs(character.Data.SkillSdSprite));
        Assert.That(activeImage.color.r, Is.EqualTo(0.28f).Within(0.001f));

        FakeActiveSkillResource resource = new(10, 10);
        character.BindBattle(resource, new FakeBattleBoard
        {
            LivingEnemyCountValue = 1,
        });
        Assert.That(character.CanActivateActiveSkill(), Is.True);
        Assert.That(activeImage.color, Is.EqualTo(Color.white));

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new(
                "CharacterInfoHoverEventSystem",
                typeof(EventSystem));
            _createdObjects.Add(eventSystemObject);
            eventSystem =
                eventSystemObject.GetComponent<EventSystem>();
        }
        PointerEventData pointerEvent = new(eventSystem);
        ExecuteEvents.Execute(
            passiveFrame.gameObject,
            pointerEvent,
            ExecuteEvents.pointerEnterHandler);
        Transform tooltip = character.transform.Find("grpSkillTooltip");
        TextMeshProUGUI tooltipText = tooltip
            .Find("txtSkillTooltip")
            .GetComponent<TextMeshProUGUI>();
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        StringAssert.Contains(
            LocalizationService.Get(
                LocalizationKeys.CodexCharacterPassive),
            tooltipText.text);

        ExecuteEvents.Execute(
            passiveFrame.gameObject,
            pointerEvent,
            ExecuteEvents.pointerExitHandler);
        Transform standingImage =
            character.transform.Find("grpStandingViewport/imgCharacterStanding");
        ExecuteEvents.Execute(
            standingImage.gameObject,
            pointerEvent,
            ExecuteEvents.pointerEnterHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        StringAssert.Contains(
            CharacterLocalization.GetName(character.Data),
            tooltipText.text);
        StringAssert.Contains(
            LocalizationService.Get(
                LocalizationKeys.CodexCharacterNormalAttack),
            tooltipText.text);
        StringAssert.Contains(
            LocalizationService.Get(
                LocalizationKeys.CodexCharacterPassive),
            tooltipText.text);
        StringAssert.Contains(
            CharacterLocalization.GetActiveSkillTitle(
                character.Data.ActiveSkillCost),
            tooltipText.text);

        int amountToLeaveBelowSkillCost =
            resource.Current -
            Mathf.Max(0, character.Data.ActiveSkillCost - 1);
        Assert.That(
            resource.TrySpend(amountToLeaveBelowSkillCost),
            Is.True);
        Assert.That(character.CanActivateActiveSkill(), Is.False);
        Assert.That(activeImage.color.r, Is.EqualTo(0.28f).Within(0.001f));
    }

    [Test]
    public void DungeonCharacterInfo_UsesDefinitionAbilityIconSprites()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "CharacterInfoIconFixture");
        Texture2D passiveTexture =
            new(8, 8, TextureFormat.RGBA32, false);
        Texture2D activeTexture =
            new(8, 8, TextureFormat.RGBA32, false);
        Sprite passiveIcon = Sprite.Create(
            passiveTexture,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f));
        Sprite activeIcon = Sprite.Create(
            activeTexture,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f));
        _createdObjects.Add(passiveTexture);
        _createdObjects.Add(activeTexture);
        _createdObjects.Add(passiveIcon);
        _createdObjects.Add(activeIcon);

        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        Assert.That(passives.arraySize, Is.GreaterThan(0));
        Assert.That(skills.arraySize, Is.GreaterThan(0));
        passives.GetArrayElementAtIndex(0)
            .FindPropertyRelative("iconSprite")
            .objectReferenceValue = passiveIcon;
        skills.GetArrayElementAtIndex(0)
            .FindPropertyRelative("iconSprite")
            .objectReferenceValue = activeIcon;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        Image passiveImage = character.transform
            .Find("grpPassiveAbilityIcon/imgPassiveAbilityIcon")
            .GetComponent<Image>();
        Image activeImage = character.transform
            .Find("grpActiveAbilityIcon/imgActiveAbilityIcon")
            .GetComponent<Image>();

        Assert.That(
            character.Data.PassiveAbilityIconSprite,
            Is.SameAs(passiveIcon));
        Assert.That(
            character.Data.ActiveAbilityIconSprite,
            Is.SameAs(activeIcon));
        Assert.That(passiveImage.sprite, Is.SameAs(passiveIcon));
        Assert.That(activeImage.sprite, Is.SameAs(activeIcon));
    }

    [Test]
    public void DungeonCharacterInfo_HidesRoleAndArchetypeIcons()
    {
        CharacterRoleSO role =
            ScriptableObject.CreateInstance<CharacterRoleSO>();
        CharacterArchetypeSO archetype =
            ScriptableObject.CreateInstance<CharacterArchetypeSO>();
        _createdObjects.Add(role);
        _createdObjects.Add(archetype);
        Sprite roleIcon = CreateTestSprite(Color.blue);
        Sprite archetypeIcon = CreateTestSprite(Color.cyan);

        SerializedObject roleSerialized = new(role);
        roleSerialized.FindProperty("iconSprite").objectReferenceValue =
            roleIcon;
        roleSerialized.FindProperty("fallbackName").stringValue =
            "Test Role";
        roleSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject archetypeSerialized = new(archetype);
        archetypeSerialized.FindProperty("parentRole")
            .objectReferenceValue = role;
        archetypeSerialized.FindProperty("iconSprite")
            .objectReferenceValue = archetypeIcon;
        archetypeSerialized.FindProperty("fallbackName").stringValue =
            "Test Archetype";
        archetypeSerialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterSO definition = CreateBaseCharacterFixture(
            "CharacterInfoClassificationIconFixture");
        SerializedObject characterSerialized = new(definition);
        characterSerialized.FindProperty("role").objectReferenceValue =
            role;
        characterSerialized.FindProperty("archetype")
            .objectReferenceValue = archetype;
        characterSerialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        Transform roleFrame = character.transform.Find("grpRoleIcon");
        Transform archetypeFrame =
            character.transform.Find("grpArchetypeIcon");

        Assert.That(
            roleFrame == null || !roleFrame.gameObject.activeSelf,
            Is.True);
        Assert.That(
            archetypeFrame == null || !archetypeFrame.gameObject.activeSelf,
            Is.True);
    }

    [Test]
    public void DungeonCharacterInfo_UsesIndexZeroIconsUntilVariantConditionMatches()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "CharacterInfoConditionalIconFixture");
        Sprite basePassiveIcon = CreateTestSprite(Color.red);
        Sprite variantPassiveIcon = CreateTestSprite(Color.yellow);
        Sprite baseSkillIcon = CreateTestSprite(Color.green);
        Sprite variantSkillIcon = CreateTestSprite(Color.magenta);

        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 2;
        passives.GetArrayElementAtIndex(0)
            .FindPropertyRelative("iconSprite")
            .objectReferenceValue = basePassiveIcon;
        SerializedProperty passiveVariant =
            passives.GetArrayElementAtIndex(1);
        passiveVariant.FindPropertyRelative("iconSprite")
            .objectReferenceValue = variantPassiveIcon;
        SetSections(
            passiveVariant.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Condition,
            (int)CharacterPassiveSectionType.Ability);
        ConfigureSourceHealthPercentageCondition(passiveVariant, 50f);

        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 2;
        skills.GetArrayElementAtIndex(0)
            .FindPropertyRelative("iconSprite")
            .objectReferenceValue = baseSkillIcon;
        SerializedProperty skillVariant = skills.GetArrayElementAtIndex(1);
        skillVariant.FindPropertyRelative("iconSprite")
            .objectReferenceValue = variantSkillIcon;
        SetSections(
            skillVariant.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Condition,
            (int)CharacterSkillSectionType.Ability);
        ConfigureSourceHealthPercentageCondition(skillVariant, 50f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        Image passiveImage = character.transform
            .Find("grpPassiveAbilityIcon/imgPassiveAbilityIcon")
            .GetComponent<Image>();
        Image skillImage = character.transform
            .Find("grpActiveAbilityIcon/imgActiveAbilityIcon")
            .GetComponent<Image>();

        Assert.That(
            character.Data.PassiveAbilityIconSprite,
            Is.SameAs(basePassiveIcon));
        Assert.That(
            character.Data.ActiveAbilityIconSprite,
            Is.SameAs(baseSkillIcon));
        Assert.That(passiveImage.sprite, Is.SameAs(basePassiveIcon));
        Assert.That(skillImage.sprite, Is.SameAs(baseSkillIcon));

        Assert.That(character.TakeDamage(60), Is.EqualTo(60));

        Assert.That(passiveImage.sprite, Is.SameAs(variantPassiveIcon));
        Assert.That(skillImage.sprite, Is.SameAs(variantSkillIcon));
        Assert.That(
            character.Data.PassiveAbilityIconSprite,
            Is.SameAs(basePassiveIcon));
        Assert.That(
            character.Data.ActiveAbilityIconSprite,
            Is.SameAs(baseSkillIcon));
    }

    [Test]
    public void DungeonPauseMenu_DoesNotCreateMissingFixedSceneUi()
    {
        GameObject root = new(
            "DungeonPauseMenuTest",
            typeof(RectTransform));
        _createdObjects.Add(root);
        root.AddComponent<DungeonBattleTab>();

        Assert.That(
            typeof(DungeonBattleTab).GetMethod(
                "EnsurePauseNavigationButtons",
                BindingFlags.Instance | BindingFlags.NonPublic),
            Is.Null);
        Assert.That(root.transform.Find("grpPauseOverlay"), Is.Null);
        Assert.That(root.transform.Find("grpPauseMenuPanel"), Is.Null);
    }
    [Test]
    public void CodexCard_IsDesignerEditablePrefab()
    {
        const string prefabPath =
            "Assets/06_Runtime/Resources/Presentation/CodexCard.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<Button>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Outline>(), Is.Not.Null);
        Assert.That(
            prefab.transform.Find("imgCodexCardIcon")
                ?.GetComponent<Image>(),
            Is.Not.Null);
        Assert.That(
            prefab.transform.Find(
                    "grpCodexCardNamePlate/txtCodexCardName")
                ?.GetComponent<TextMeshProUGUI>(),
            Is.Not.Null);

        RectTransform rect = prefab.transform as RectTransform;
        Assert.That(rect, Is.Not.Null);
        Assert.That(
            rect.rect.height / rect.rect.width,
            Is.EqualTo(1.4f).Within(0.0001f));
    }

    [Test]
    public void CodexBrowser_DoesNotCreateMissingFixedHierarchy()
    {
        GameObject root = new(
            "MissingCodexDesignerLayout",
            typeof(RectTransform));
        _createdObjects.Add(root);

        LogAssert.Expect(
            LogType.Error,
            new System.Text.RegularExpressions.Regex(
                "Codex browser fixed UI is missing"));
        CodexBrowserView.Build(root.transform);

        Assert.That(root.transform.childCount, Is.Zero);
    }

    [Test]
    public void StageSelectSync_DoesNotCreateMissingFixedHierarchy()
    {
        GameObject pageObject = new(
            "StageSelectValidationTest",
            typeof(RectTransform),
            typeof(StageSelectPage));
        _createdObjects.Add(pageObject);
        StageSelectPage page = pageObject.GetComponent<StageSelectPage>();

        Assert.That(page.SyncEditorUi(out string error), Is.False);
        Assert.That(error, Does.Contain("Dungeon Select designer references"));
        Assert.That(pageObject.transform.childCount, Is.Zero);
    }

    [Test]
    public void DungeonProgressData_RoundTripsClearStateAndCount()
    {
        DungeonProgressData source = new();
        Assert.That(
            source.MarkCleared("test_field", 2, false),
            Is.True);
        Assert.That(
            source.MarkCleared("test_field", 3, false),
            Is.True);

        DungeonProgressData restored = new();
        Assert.That(restored.ImportJson(source.ExportJson()), Is.True);
        Assert.That(restored.IsCleared("test_field"), Is.True);
        Assert.That(restored.GetClearCount("test_field"), Is.EqualTo(2));
        Assert.That(restored.IsCleared("free_battle"), Is.False);
    }

    [Test]
    public void TutorialBattle_TimeoutFinishesRunAsClear()
    {
        DungeonPage page = CreateTutorialDungeonPageForBattleResult();
        EDungeonRunResult notifiedResult = EDungeonRunResult.None;
        page.RunEnded += result => notifiedResult = result;
        MethodInfo handleBattleEnded = typeof(DungeonPage).GetMethod(
            "HandleBattleEnded",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(handleBattleEnded, Is.Not.Null);

        handleBattleEnded.Invoke(
            page,
            new object[] { EBattleResult.Timeout });

        Assert.That(page.RunResult, Is.EqualTo(EDungeonRunResult.Clear));
        Assert.That(notifiedResult, Is.EqualTo(EDungeonRunResult.Clear));
        Assert.That(
            page.RunSession.Activity,
            Is.EqualTo(EDungeonRunActivity.Result));
        Assert.That(
            page.RunSession.Pause.Reasons.HasFlag(
                EDungeonPauseReason.Result),
            Is.True);
    }

    [Test]
    public void TutorialBattle_VictoryFinishesRunAsClear()
    {
        DungeonPage page = CreateTutorialDungeonPageForBattleResult();
        MethodInfo handleBattleEnded = typeof(DungeonPage).GetMethod(
            "HandleBattleEnded",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo handleBattleCompleted = typeof(DungeonPage).GetMethod(
            "HandleBattleCompleted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(handleBattleEnded, Is.Not.Null);
        Assert.That(handleBattleCompleted, Is.Not.Null);

        handleBattleEnded.Invoke(
            page,
            new object[] { EBattleResult.Victory });
        Assert.That(page.RunResult, Is.EqualTo(EDungeonRunResult.None));

        handleBattleCompleted.Invoke(page, null);

        Assert.That(page.RunResult, Is.EqualTo(EDungeonRunResult.Clear));
        Assert.That(
            page.RunSession.Activity,
            Is.EqualTo(EDungeonRunActivity.Result));
    }

    [Test]
    public void OnKillPassive_SelfOtherAndAllFilterExpectedKillers()
    {
        StatusEffectSO selfReward = CreateRuntimeStatus(
            "test_kill_self_reward",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO otherReward = CreateRuntimeStatus(
            "test_kill_other_reward",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO allReward = CreateRuntimeStatus(
            "test_kill_all_reward",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterRuntime selfOwner = CreateCharacter(
            CreateKillPassiveCharacter(
                CharacterPassiveKillSource.Self,
                selfReward));
        CharacterRuntime otherOwner = CreateCharacter(
            CreateKillPassiveCharacter(
                CharacterPassiveKillSource.Other,
                otherReward));
        CharacterRuntime allOwner = CreateCharacter(
            CreateKillPassiveCharacter(
                CharacterPassiveKillSource.All,
                allReward));
        FakeBattleBoard board = new();
        selfOwner.BindBattle(null, board);
        otherOwner.BindBattle(null, board);
        allOwner.BindBattle(null, board);
        EnemyRuntime defeatedEnemy = CreateEnemyRuntime();

        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            defeatedEnemy,
            selfOwner));

        Assert.That(selfOwner.GetStatusStackCount(selfReward), Is.EqualTo(1));
        Assert.That(otherOwner.GetStatusStackCount(otherReward), Is.EqualTo(1));
        Assert.That(allOwner.GetStatusStackCount(allReward), Is.EqualTo(1));

        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            defeatedEnemy,
            otherOwner));

        Assert.That(selfOwner.GetStatusStackCount(selfReward), Is.EqualTo(1));
        Assert.That(otherOwner.GetStatusStackCount(otherReward), Is.EqualTo(1));
        Assert.That(allOwner.GetStatusStackCount(allReward), Is.EqualTo(2));

        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            defeatedEnemy,
            null));

        Assert.That(selfOwner.GetStatusStackCount(selfReward), Is.EqualTo(1));
        Assert.That(otherOwner.GetStatusStackCount(otherReward), Is.EqualTo(1));
        Assert.That(allOwner.GetStatusStackCount(allReward), Is.EqualTo(2));
    }

    [Test]
    public void OnKillPassive_SpecificCharacterMatchesDefinition()
    {
        StatusEffectSO reward = CreateRuntimeStatus(
            "test_kill_specific_reward",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterSO designatedDefinition =
            CreateBaseCharacterFixture("DesignatedKillerFixture");
        CharacterRuntime owner = CreateCharacter(
            CreateKillPassiveCharacter(
                CharacterPassiveKillSource.SpecificCharacter,
                reward,
                designatedDefinition));
        CharacterRuntime designatedKiller =
            CreateCharacter(designatedDefinition);
        CharacterRuntime otherKiller = CreateCharacter(SuirenAssetPath);
        FakeBattleBoard board = new();
        owner.BindBattle(null, board);
        EnemyRuntime defeatedEnemy = CreateEnemyRuntime();

        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            defeatedEnemy,
            otherKiller));
        Assert.That(owner.GetStatusStackCount(reward), Is.Zero);

        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            defeatedEnemy,
            designatedKiller));
        Assert.That(owner.GetStatusStackCount(reward), Is.EqualTo(1));
    }

    [Test]
    public void OnKillPassive_RebindingDoesNotDuplicateSubscription()
    {
        StatusEffectSO reward = CreateRuntimeStatus(
            "test_kill_rebind_reward",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterRuntime owner = CreateCharacter(
            CreateKillPassiveCharacter(
                CharacterPassiveKillSource.Self,
                reward));
        FakeBattleBoard board = new();
        owner.BindBattle(null, board);
        owner.BindBattle(null, board);
        EnemyRuntime defeatedEnemy = CreateEnemyRuntime();

        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            defeatedEnemy,
            owner));
        Assert.That(owner.GetStatusStackCount(reward), Is.EqualTo(1));

        owner.BindBattle(null, null);
        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            defeatedEnemy,
            owner));
        Assert.That(owner.GetStatusStackCount(reward), Is.EqualTo(1));
    }

    [Test]
    public void KillRewardPassive_GainsResourceAndSourceStatus()
    {
        CharacterRuntime mirinae = CreateCharacter(
            CreateMirinaeFeatureFixture());
        CharacterRuntime killer = CreateCharacter(SuirenAssetPath);
        StatusEffectSO starPowder =
            LoadAsset<StatusEffectSO>(StarPowderAssetPath);
        FakeActiveSkillResource resource = new(0);
        FakeBattleBoard board = new();
        mirinae.BindBattle(resource, board);

        board.RaiseEnemyDefeated(new BattleEnemyDefeatedEvent(
            CreateEnemyRuntime(),
            killer));

        Assert.That(resource.Current, Is.EqualTo(1));
        Assert.That(resource.TryGainCallCount, Is.EqualTo(1));
        Assert.That(
            mirinae.GetStatusStackCount(starPowder),
            Is.EqualTo(1));
    }

    [Test]
    public void LockedAttackTarget_IsReusedUntilInvalid_AndGainsComboPerAttack()
    {
        StatusEffectSO combo = LoadAsset<StatusEffectSO>(ComboAssetPath);
        CharacterSO definition = CreateBaseCharacterFixture(
            "LockedAttackTargetFixture");
        SerializedObject serialized = new(definition);
        SerializedProperty attack = serialized
            .FindProperty("attackDefinitions")
            .GetArrayElementAtIndex(0);
        attack.FindPropertyRelative("targetRetentionMode").enumValueIndex =
            (int)CharacterAttackTargetRetentionMode.LockUntilInvalid;

        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnAttack;
        passive.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.PreviousAttackSucceeded;
        passive.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty passiveEffects =
            passive.FindPropertyRelative("effects");
        passiveEffects.arraySize = 1;
        ConfigureApplyStatusEffect(
            passiveEffects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.Source,
            combo,
            1f,
            1f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        EnemyRuntime firstTarget = CreateEnemyRuntime();
        EnemyRuntime secondTarget = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
        };
        board.PlannedEnemySelections.Enqueue(new[] { firstTarget });
        board.PlannedEnemySelections.Enqueue(new[] { secondTarget });
        CharacterRuntime byeolha = CreateCharacter(definition);
        byeolha.BindBattle(null, board);

        byeolha.TickBattle(byeolha.Data.AttackCooldown, board);
        byeolha.TickBattle(byeolha.Data.AttackCooldown, board);

        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(1));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots[0], Does.Contain(firstTarget));
        Assert.That(board.DamageTargetSnapshots[1], Does.Contain(firstTarget));
        Assert.That(byeolha.GetStatusStackCount(combo), Is.EqualTo(2));

        board.InvalidEnemyTargets.Add(firstTarget);
        byeolha.TickBattle(byeolha.Data.AttackCooldown, board);

        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(3));
        Assert.That(board.DamageTargetSnapshots[2], Does.Contain(secondTarget));
        Assert.That(byeolha.GetStatusStackCount(combo), Is.EqualTo(3));

        EnemyRuntime resetTarget = CreateEnemyRuntime();
        board.PlannedEnemySelections.Enqueue(new[] { resetTarget });
        byeolha.ResetRuntime();
        byeolha.TickBattle(byeolha.Data.AttackCooldown, board);

        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(3));
        Assert.That(board.DamageTargetSnapshots[3], Does.Contain(resetTarget));
        Assert.That(byeolha.GetStatusStackCount(combo), Is.EqualTo(1));
    }

    [Test]
    public void DefaultAttackTarget_IsReselectedEveryAttack()
    {
        CharacterRuntime character = CreateCharacter(
            CreateBaseCharacterFixture("ReselectAttackTargetFixture"));
        EnemyRuntime firstTarget = CreateEnemyRuntime();
        EnemyRuntime secondTarget = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
        };
        board.PlannedEnemySelections.Enqueue(new[] { firstTarget });
        board.PlannedEnemySelections.Enqueue(new[] { secondTarget });
        character.BindBattle(null, board);

        character.TickBattle(character.Data.AttackCooldown, board);
        character.TickBattle(character.Data.AttackCooldown, board);

        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots[0], Does.Contain(firstTarget));
        Assert.That(board.DamageTargetSnapshots[1], Does.Contain(secondTarget));
    }

    [Test]
    public void FailedAttackLinkage_UsesFallbackOnlyAfterPreviousFailure()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "FailedAttackFallbackFixture");
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 2;
        SerializedProperty fallback = attacks.GetArrayElementAtIndex(1);
        SetSections(
            fallback.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Linkage,
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        fallback.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.PreviousAttackFailed;
        fallback.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        fallback.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        fallback.FindPropertyRelative("subjectCount").intValue = 1;
        fallback.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        fallback.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        fallback.FindPropertyRelative("damageAmount").floatValue = 2f;
        SerializedProperty effects =
            fallback.FindPropertyRelative("effects");
        effects.arraySize = 1;
        ConfigureFixedDamageEffect(
            effects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        EnemyRuntime fallbackTarget = CreateEnemyRuntime();
        FakeBattleBoard fallbackBoard = new()
        {
            LivingEnemyCountValue = 1,
        };
        fallbackBoard.PlannedEnemySelections.Enqueue(
            Array.Empty<EnemyRuntime>());
        fallbackBoard.PlannedEnemySelections.Enqueue(
            new[] { fallbackTarget });
        CharacterRuntime fallbackCharacter = CreateCharacter(definition);
        fallbackCharacter.BindBattle(null, fallbackBoard);

        fallbackCharacter.TickBattle(
            fallbackCharacter.Data.AttackCooldown,
            fallbackBoard);

        Assert.That(
            fallbackBoard.CharacterTargetSelectionCallCount,
            Is.EqualTo(2));
        Assert.That(fallbackBoard.DamageAmounts, Is.EqualTo(new[] { 2 }));
        Assert.That(
            fallbackBoard.DamageTargetSnapshots[0],
            Does.Contain(fallbackTarget));

        EnemyRuntime primaryTarget = CreateEnemyRuntime();
        EnemyRuntime unusedFallbackTarget = CreateEnemyRuntime();
        FakeBattleBoard primaryBoard = new()
        {
            LivingEnemyCountValue = 2,
        };
        primaryBoard.PlannedEnemySelections.Enqueue(
            new[] { primaryTarget });
        primaryBoard.PlannedEnemySelections.Enqueue(
            new[] { unusedFallbackTarget });
        CharacterRuntime primaryCharacter = CreateCharacter(definition);
        primaryCharacter.BindBattle(null, primaryBoard);

        primaryCharacter.TickBattle(
            primaryCharacter.Data.AttackCooldown,
            primaryBoard);

        Assert.That(
            primaryBoard.CharacterTargetSelectionCallCount,
            Is.EqualTo(1));
        Assert.That(primaryBoard.DamageAmounts, Is.EqualTo(new[] { 1 }));
        Assert.That(
            primaryBoard.DamageTargetSnapshots[0],
            Does.Contain(primaryTarget));
        Assert.That(primaryBoard.PlannedEnemySelections, Has.Count.EqualTo(1));
    }

    [Test]
    public void IsoldeAsset_UsesRandomBloodFallbackAfterPrimaryFailure()
    {
        CharacterSO definition =
            LoadAsset<CharacterSO>(IsoldeAssetPath);

        Assert.That(definition.PassiveDefinitions, Is.Not.Empty);
        Assert.That(
            definition.PassiveDefinitions[0].Linkage,
            Is.EqualTo(CharacterActionLinkage.None));
        Assert.That(definition.AttackDefinitions, Has.Count.EqualTo(2));
        CharacterAttackDefinition primary =
            definition.AttackDefinitions[0];
        CharacterAttackDefinition fallback =
            definition.AttackDefinitions[1];
        Assert.That(
            primary.HasSection(CharacterAttackSectionType.Linkage),
            Is.False);
        Assert.That(
            fallback.HasSection(CharacterAttackSectionType.Linkage),
            Is.True);
        Assert.That(
            fallback.Linkage,
            Is.EqualTo(CharacterActionLinkage.PreviousAttackFailed));
        Assert.That(
            fallback.Subject,
            Is.EqualTo(CharacterAttackSubject.Random));
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            HasDiagnostic(validation, "passive.linkage_ignored"),
            Is.False,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void ActionDefinitions_DefaultLinkageToNone()
    {
        Assert.That(
            new CharacterAttackDefinition().Linkage,
            Is.EqualTo(CharacterActionLinkage.None));
        Assert.That(
            new CharacterPassiveDefinition().Linkage,
            Is.EqualTo(CharacterActionLinkage.None));
        Assert.That(
            new CharacterSkillDefinition().Linkage,
            Is.EqualTo(CharacterActionLinkage.None));
        Assert.That(
            new CharacterPassiveDefinition().MotionMode,
            Is.EqualTo(CharacterPassiveMotionMode.PlayPassiveMotion));
    }

    [Test]
    public void Calista_PassiveMotionPlaysOnlyOnFourStackRelease()
    {
        CharacterSO definition = LoadAsset<CharacterSO>(CalistaAssetPath);
        Assert.That(definition.PassiveDefinitions, Has.Count.EqualTo(2));
        Assert.That(
            definition.PassiveDefinitions[0].MotionMode,
            Is.EqualTo(CharacterPassiveMotionMode.None));
        Assert.That(
            definition.PassiveDefinitions[1].MotionMode,
            Is.EqualTo(CharacterPassiveMotionMode.PlayPassiveMotion));

        StatusEffectSO ready = LoadAsset<StatusEffectSO>(
            "Assets/06_Runtime/Resources/StatusEffects/Ready_4.asset");
        CharacterRuntime calista = CreateCharacter(definition);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { CreateEnemyRuntime() },
        };
        calista.BindBattle(null, board);
        float attackCycle = calista.Data.AttackCooldown +
                            calista.Data.AttackRecoveryDuration;

        for (int attackIndex = 1; attackIndex <= 3; attackIndex++)
        {
            calista.TickBattle(attackCycle, board);
            Assert.That(
                calista.GetStatusStackCount(ready),
                Is.EqualTo(attackIndex));
            Assert.That(
                GetPrivateField<float>(
                    calista,
                    "_passiveSdTimeRemaining"),
                Is.Zero);
        }

        calista.TickBattle(attackCycle, board);

        Assert.That(calista.GetStatusStackCount(ready), Is.Zero);
        Assert.That(
            GetPrivateField<float>(calista, "_passiveSdTimeRemaining"),
            Is.GreaterThan(0f));
    }

    [Test]
    public void NonAttackPassive_NormalizesLinkageToNone()
    {
        CharacterPassiveDefinition passive = new();
        SetPrivateField(
            passive,
            "trigger",
            CharacterPassiveTrigger.OnCooldown);
        SetPrivateField(
            passive,
            "linkage",
            CharacterActionLinkage.PreviousAttackSucceeded);

        passive.Validate();

        Assert.That(
            passive.Linkage,
            Is.EqualTo(CharacterActionLinkage.None));
    }

    [Test]
    public void AttackTargetSelectedPassive_ReusesSelectionAndRunsBeforeAttack()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "AttackTargetSelectedPassiveFixture");
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnAttackTargetSelected;
        passive.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.None;
        passive.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        SerializedProperty passiveEffects =
            passive.FindPropertyRelative("effects");
        passiveEffects.arraySize = 1;
        ConfigureFixedDamageEffect(
            passiveEffects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        EnemyRuntime target = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        CharacterRuntime character = CreateCharacter(definition);
        character.BindBattle(null, board);

        character.TickBattle(character.Data.AttackCooldown, board);

        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(1),
            "The passive must inherit the selected attack target.");
        Assert.That(
            board.DamageAmounts,
            Is.EqualTo(new[] { 2, 1 }),
            "The target-selection passive must execute before the attack.");
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots[0], Does.Contain(target));
        Assert.That(board.DamageTargetSnapshots[1], Does.Contain(target));
    }

    [Test]
    public void AttackTargetRelationPassives_DistinguishSameAndDifferentTargets()
    {
        StatusEffectSO sameTargetReward = CreateRuntimeStatus(
            "same-target-reward",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO differentTargetReward = CreateRuntimeStatus(
            "different-target-reward",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterSO definition = CreateBaseCharacterFixture(
            "AttackTargetRelationFixture");
        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 2;
        ConfigureAttackTargetRelationPassive(
            passives.GetArrayElementAtIndex(0),
            CharacterPassiveAttackTargetRelation.SameAsPreviousAttack,
            sameTargetReward);
        ConfigureAttackTargetRelationPassive(
            passives.GetArrayElementAtIndex(1),
            CharacterPassiveAttackTargetRelation.DifferentFromPreviousAttack,
            differentTargetReward);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        EnemyRuntime firstTarget = CreateEnemyRuntime();
        EnemyRuntime secondTarget = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
        };
        board.PlannedEnemySelections.Enqueue(new[] { firstTarget });
        board.PlannedEnemySelections.Enqueue(new[] { firstTarget });
        board.PlannedEnemySelections.Enqueue(new[] { secondTarget });
        board.PlannedEnemySelections.Enqueue(new[] { secondTarget });
        CharacterRuntime character = CreateCharacter(definition);
        character.BindBattle(null, board);

        for (int attackIndex = 0; attackIndex < 4; attackIndex++)
            character.TickBattle(character.Data.AttackCooldown, board);

        Assert.That(
            character.GetStatusStackCount(sameTargetReward),
            Is.EqualTo(2),
            "The second and fourth attempts repeat their previous target.");
        Assert.That(
            character.GetStatusStackCount(differentTargetReward),
            Is.EqualTo(1),
            "Only the third attempt changes away from the previous target.");

        board.PlannedEnemySelections.Enqueue(new[] { secondTarget });
        character.ResetRuntime();
        character.TickBattle(character.Data.AttackCooldown, board);

        Assert.That(
            character.GetStatusStackCount(sameTargetReward),
            Is.Zero,
            "The first attempt after reset must only establish a baseline.");
        Assert.That(
            character.GetStatusStackCount(differentTargetReward),
            Is.Zero,
            "The first attempt after reset has no previous target.");
    }

    [Test]
    public void LockedAttackTarget_RejectsMultipleTargetSelectors()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "InvalidLockedAttackTargetFixture");
        SerializedObject serialized = new(definition);
        SerializedProperty attack = serialized
            .FindProperty("attackDefinitions")
            .GetArrayElementAtIndex(0);
        attack.FindPropertyRelative("targetRetentionMode").enumValueIndex =
            (int)CharacterAttackTargetRetentionMode.LockUntilInvalid;
        attack.FindPropertyRelative("subjectCount").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                validation,
                "attack.target_retention_unsupported"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void SourceStatusSequence_ConditionFiltersReusedTarget()
    {
        CharacterRuntime mirinae = CreateCharacter(
            CreateMirinaeFeatureFixture());
        StatusEffectSO starPowder =
            LoadAsset<StatusEffectSO>(StarPowderAssetPath);
        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
            ReturnCenterTargetsForAreaExpansion = true,
        };
        mirinae.BindBattle(resource, board);
        mirinae.TickBattle(3f, board);
        Assert.That(
            board.DamageTargetSnapshots,
            Has.Count.EqualTo(1),
            "A normal attack must establish the reusable skill target.");

        Assert.That(
            mirinae.ApplyStatusEffect(starPowder, 1f, 11),
            Is.True);
        Assert.That(mirinae.TryActivateActiveSkill(), Is.True);

        Assert.That(
            board.FilterCharacterTargetCallCount,
            Is.EqualTo(2),
            "Both linked sequence steps must validate their inherited " +
            "target before the source-gated step is rejected.");
        Assert.That(
            board.DamageTargetSnapshots,
            Has.Count.EqualTo(2),
            "At 11 stacks only the first sequence step may execute.");
        Assert.That(
            mirinae.GetStatusStackCount(starPowder),
            Is.EqualTo(11));

        Assert.That(
            mirinae.ApplyStatusEffect(starPowder, 1f, 1),
            Is.True);
        Assert.That(mirinae.TryActivateActiveSkill(), Is.True);

        Assert.That(
            board.FilterCharacterTargetCallCount,
            Is.EqualTo(4),
            "Each activation must validate the inherited target for both " +
            "linked sequence steps.");
        Assert.That(
            board.DamageTargetSnapshots,
            Has.Count.EqualTo(4),
            "At 12 stacks the source-gated second sequence step must run.");
        Assert.That(
            mirinae.GetStatusStackCount(starPowder),
            Is.Zero,
            "The second step must consume all 12 StarPowder stacks.");
    }

    [Test]
    public void CooldownPassive_ChargesStatusUpToConfiguredMaximum()
    {
        CharacterRuntime suiren = CreateCharacter(
            CreateSuirenFeatureFixture());
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        FakeBattleBoard board = new();
        suiren.BindBattle(null, board);

        for (int expectedStacks = 1; expectedStacks <= 3; expectedStacks++)
        {
            suiren.TickBattle(10f, board);
            Assert.That(
                suiren.GetStatusStackCount(emergencyKit),
                Is.EqualTo(expectedStacks));
        }

        suiren.TickBattle(10f, board);

        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(3));
    }

    [Test]
    public void StatusPassive_CleansExactTarget_AndConsumesOneStack()
    {
        CharacterRuntime suiren = CreateCharacter(
            CreateSuirenFeatureFixture());
        CharacterRuntime ally = CreateCharacter(
            CreateBaseCharacterFixture("CleanseTargetFixture"));
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        FakeBattleBoard board = new();
        suiren.BindBattle(null, board);
        ally.BindBattle(null, board);

        Assert.That(
            suiren.ApplyStatusEffect(emergencyKit, 1f, 2),
            Is.True);
        Assert.That(ally.ApplyStatusEffect(stun, 5f, 1), Is.True);

        Assert.That(ally.DisabledTimeRemaining, Is.Zero);
        Assert.That(ally.HasStatusEffect(stun), Is.False);
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(
            board.LastAlliedStatusRemovalTargets,
            Is.EquivalentTo(new IBattleCharacter[] { ally }));
    }

    [Test]
    public void StatusPassive_SelfTrigger_IgnoresOtherAllies()
    {
        CharacterSO definition = CreateSuirenFeatureFixture();
        SerializedObject serialized = new(definition);
        SerializedProperty cleanse = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(1);
        cleanse.FindPropertyRelative("statusTarget").enumValueIndex =
            (int)CharacterPassiveStatusTarget.Self;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime owner = CreateCharacter(definition);
        CharacterRuntime ally = CreateCharacter(
            CreateBaseCharacterFixture("SelfTriggerOtherAllyFixture"));
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        FakeBattleBoard board = new();
        owner.BindBattle(null, board);
        ally.BindBattle(null, board);

        Assert.That(
            owner.ApplyStatusEffect(emergencyKit, 1f, 2),
            Is.True);
        Assert.That(ally.ApplyStatusEffect(stun, 5f, 1), Is.True);

        Assert.That(ally.HasStatusEffect(stun), Is.True);
        Assert.That(
            owner.GetStatusStackCount(emergencyKit),
            Is.EqualTo(2),
            "Another ally acquiring the status must not trigger Self.");

        Assert.That(owner.ApplyStatusEffect(stun, 5f, 1), Is.True);

        Assert.That(owner.HasStatusEffect(stun), Is.False);
        Assert.That(ally.HasStatusEffect(stun), Is.True);
        Assert.That(
            owner.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(
            board.LastAlliedStatusRemovalTargets,
            Is.EquivalentTo(new IBattleCharacter[] { owner }));
    }

    [Test]
    public void StatusPassive_MultiFilterTriggersForEitherSelectedStatus()
    {
        CharacterSO definition = CreateSuirenFeatureFixture();
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        StatusEffectSO combo = LoadAsset<StatusEffectSO>(ComboAssetPath);
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(1);
        passive.FindPropertyRelative(
            "triggerStatusEffect").objectReferenceValue = null;
        SerializedProperty triggerStatuses =
            passive.FindPropertyRelative("triggerStatusEffects");
        triggerStatuses.arraySize = 2;
        triggerStatuses.GetArrayElementAtIndex(0)
            .objectReferenceValue = stun;
        triggerStatuses.GetArrayElementAtIndex(1)
            .objectReferenceValue = combo;
        SerializedProperty removeEffect = passive
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        removeEffect.FindPropertyRelative(
            "statusEffect").objectReferenceValue = combo;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime suiren = CreateCharacter(definition);
        CharacterRuntime ally = CreateCharacter(
            CreateBaseCharacterFixture("MultiStatusTargetFixture"));
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        FakeBattleBoard board = new();
        suiren.BindBattle(null, board);
        ally.BindBattle(null, board);

        Assert.That(
            suiren.ApplyStatusEffect(emergencyKit, 1f, 2),
            Is.True);
        Assert.That(ally.ApplyStatusEffect(combo, 5f, 1), Is.True);

        Assert.That(ally.HasStatusEffect(combo), Is.False);
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(
            board.LastAlliedStatusRemovalTargets,
            Is.EquivalentTo(new IBattleCharacter[] { ally }));
    }

    [Test]
    public void StatusPassive_AllDebuffsScopeIgnoresBuffsAndTriggersForDebuff()
    {
        CharacterSO definition = CreateSuirenFeatureFixture();
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(1);
        passive.FindPropertyRelative("triggerStatusScope").enumValueIndex =
            (int)CharacterStatusSelectionScope.AllDebuffs;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime owner = CreateCharacter(definition);
        CharacterRuntime ally = CreateCharacter(
            CreateBaseCharacterFixture("StatusScopeTargetFixture"));
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        Assert.That(
            emergencyKit.Alignment,
            Is.EqualTo(StatusEffectAlignment.Buff));
        Assert.That(
            stun.Alignment,
            Is.EqualTo(StatusEffectAlignment.Debuff));

        FakeBattleBoard board = new();
        owner.BindBattle(null, board);
        ally.BindBattle(null, board);
        Assert.That(
            owner.ApplyStatusEffect(emergencyKit, 1f, 2),
            Is.True);

        Assert.That(
            ally.ApplyStatusEffect(emergencyKit, 5f, 1),
            Is.True);
        Assert.That(
            owner.GetStatusStackCount(emergencyKit),
            Is.EqualTo(2),
            "A buff must not trigger an AllDebuffs passive.");

        Assert.That(
            ally.ApplyStatusEffect(stun, 5f, 1),
            Is.True);
        Assert.That(
            owner.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(ally.HasStatusEffect(stun), Is.False);
    }

    [Test]
    public void StatusPassive_IgnoresWrongTargetStatusAndMissingCost()
    {
        CharacterRuntime suiren = CreateCharacter(
            CreateSuirenFeatureFixture());
        CharacterRuntime ally = CreateCharacter(
            CreateBaseCharacterFixture("IgnoredStatusTargetFixture"));
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        FakeBattleBoard board = new();
        suiren.BindBattle(null, board);
        ally.BindBattle(null, board);

        Assert.That(
            suiren.ApplyStatusEffect(emergencyKit, 1f, 1),
            Is.True);

        EnemyRuntime enemy = CreateEnemyRuntime();
        board.RaiseStatusApplied(
            new BattleStatusAppliedEvent(
                BattleStatusTarget.FromEnemy(enemy),
                stun,
                0,
                1));
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));

        board.RaiseStatusApplied(
            new BattleStatusAppliedEvent(
                BattleStatusTarget.FromAlly(ally),
                emergencyKit,
                0,
                1));
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(board.AlliedStatusRemovalCallCount, Is.Zero);

        Assert.That(
            suiren.TryConsumeStatusStacks(emergencyKit, 1),
            Is.True);
        Assert.That(ally.ApplyStatusEffect(stun, 5f, 1), Is.True);

        Assert.That(ally.HasStatusEffect(stun), Is.True);
        Assert.That(ally.DisabledTimeRemaining, Is.EqualTo(5f));
        Assert.That(board.AlliedStatusRemovalCallCount, Is.Zero);
    }

    [Test]
    public void MultipleCleansers_OnlySuccessfulOneConsumesStatusCost()
    {
        CharacterRuntime firstSuiren = CreateCharacter(
            CreateSuirenFeatureFixture());
        CharacterRuntime secondSuiren = CreateCharacter(
            CreateSuirenFeatureFixture());
        CharacterRuntime ally = CreateCharacter(
            CreateBaseCharacterFixture("SharedCleanseTargetFixture"));
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        FakeBattleBoard board = new();
        firstSuiren.BindBattle(null, board);
        secondSuiren.BindBattle(null, board);
        ally.BindBattle(null, board);

        Assert.That(
            firstSuiren.ApplyStatusEffect(emergencyKit, 1f, 1),
            Is.True);
        Assert.That(
            secondSuiren.ApplyStatusEffect(emergencyKit, 1f, 1),
            Is.True);
        Assert.That(ally.ApplyStatusEffect(stun, 5f, 1), Is.True);

        int remainingKits =
            firstSuiren.GetStatusStackCount(emergencyKit) +
            secondSuiren.GetStatusStackCount(emergencyKit);
        Assert.That(ally.HasStatusEffect(stun), Is.False);
        Assert.That(remainingKits, Is.EqualTo(1));
        Assert.That(
            board.AlliedStatusRemovalCallCount,
            Is.EqualTo(2),
            "The second passive may try, but its failed removal must " +
            "not consume a kit.");
    }

    [Test]
    public void ConfiguredActiveSkill_UsesFixtureDefinition()
    {
        CharacterRuntime suiren = CreateCharacter(
            CreateSuirenFeatureFixture());
        CharacterData data = suiren.Data;

        Assert.That(data.HasCustomSkillDefinitions, Is.True);
        Assert.That(data.SkillDefinitions, Has.Count.GreaterThan(0));

        CharacterSkillDefinition definition = data.SkillDefinitions[0];
        Assert.That(
            definition.HasSection(CharacterSkillSectionType.Cost),
            Is.True);
        Assert.That(
            definition.HasSection(CharacterSkillSectionType.Subject),
            Is.True);
        Assert.That(
            definition.HasSection(CharacterSkillSectionType.Ability),
            Is.True);
        Assert.That(definition.TargetFaction, Is.EqualTo(
            CharacterTargetFaction.Enemy));
        Assert.That(definition.Subject, Is.EqualTo(
            CharacterAttackSubject.LowestValue));
        Assert.That(definition.SubjectMetric, Is.EqualTo(
            CharacterAttackSubjectMetric.Health));
        Assert.That(data.ActiveSkillCost, Is.EqualTo(1));
        Assert.That(data.SkillAttackDamage, Is.EqualTo(2));

        string description =
            CharacterLocalization.GetActiveSkillDescription(data);
        Assert.That(description, Is.Not.Empty);

        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        suiren.BindBattle(resource, board);

        bool activated = suiren.TryActivateActiveSkill();

        Assert.That(activated, Is.True);
        Assert.That(resource.Current, Is.EqualTo(9));
        Assert.That(suiren.TotalDamageDealt, Is.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(board.DamageTargetSnapshots[0], Does.Contain(target));
    }

    [Test]
    public void ScalingValue_FromLegacyPreservesFixedAndRatioSemantics()
    {
        EffectContext context = EffectContext.ForPreview(
            CharacterActionKind.Skill,
            8f);
        ScalingValue fixedValue = ScalingValue.FromLegacy(
            CharacterDamageAmountMode.Fixed,
            3f);
        ScalingValue ratioValue = ScalingValue.FromLegacy(
            CharacterDamageAmountMode.Ratio,
            1.5f);

        Assert.That(fixedValue.FixedAmount, Is.EqualTo(3f));
        Assert.That(fixedValue.SourceAttackPowerScale, Is.Zero);
        Assert.That(
            fixedValue.Evaluate(default),
            Is.EqualTo(3f).Within(0.0001f));
        Assert.That(ratioValue.FixedAmount, Is.Zero);
        Assert.That(
            ratioValue.SourceAttackPowerScale,
            Is.EqualTo(1.5f));
        Assert.That(
            ratioValue.Evaluate(context),
            Is.EqualTo(12f).Within(0.0001f));
        Assert.That(
            (ScalingValue.Fixed(2f) +
             ScalingValue.SourceAttackPower(0.5f)).Evaluate(context),
            Is.EqualTo(6f).Within(0.0001f));

        ScalingValue unsupported = ScalingValue.FromLegacy(
            (CharacterDamageAmountMode)999,
            1f);
        Assert.That(unsupported.IsFinite, Is.False);
        Assert.That(unsupported.Evaluate(context), Is.Zero);
    }

    [Test]
    public void ScalingValue_SourceResourceScale_EvaluatesCombinedFormula()
    {
        EffectContext context = EffectContext.ForPreview(
            CharacterActionKind.Skill,
            8f,
            4,
            10);
        ScalingValue value =
            ScalingValue.Fixed(2f) +
            ScalingValue.SourceAttackPower(0.5f) +
            ScalingValue.SourceResource(1.5f);

        Assert.That(context.SourceResource, Is.EqualTo(4));
        Assert.That(context.SourceResourceMaximum, Is.EqualTo(10));
        Assert.That(value.SourceResourceScale, Is.EqualTo(1.5f));
        Assert.That(value.IsFinite, Is.True);
        Assert.That(value.HasPositiveTerm, Is.True);
        Assert.That(
            value.Evaluate(context),
            Is.EqualTo(12f).Within(0.0001f));
    }

    [Test]
    public void RatioSkill_UsesSourceAttackPowerContextAndPreservesDamage()
    {
        CharacterRuntime saena = CreateCharacter(
            CreateSaenaFeatureFixture());
        CharacterSkillDefinition skill = saena.Data.SkillDefinitions[0];
        CharacterEffectDefinition damageEffect = skill.Effects[0];
        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        saena.BindBattle(resource, board);

        Assert.That(saena.Data.AttackPower, Is.EqualTo(5f));
        Assert.That(saena.Data.ActiveSkillCost, Is.EqualTo(3));
        Assert.That(damageEffect.Type, Is.EqualTo(
            CharacterEffectType.Damage));
        Assert.That(damageEffect.DamageType, Is.EqualTo(
            CharacterAttackDamageType.Fixed));
        Assert.That(damageEffect.DamageAmountMode, Is.EqualTo(
            CharacterDamageAmountMode.Ratio));
        Assert.That(damageEffect.DamageAmount, Is.EqualTo(3f));

        saena.TickBattle(saena.Data.AttackCooldown, board);
        Assert.That(
            board.DamageTargetSnapshots,
            Has.Count.EqualTo(1),
            "A normal attack must establish Saena's reusable skill target.");
        int damageBeforeSkill = saena.TotalDamageDealt;
        int selectionCountBeforeSkill =
            board.CharacterTargetSelectionCallCount;

        Assert.That(saena.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(7));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(
            saena.TotalDamageDealt - damageBeforeSkill,
            Is.EqualTo(15));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(selectionCountBeforeSkill),
            "Subject None must reuse the normal-attack target without " +
            "selecting a new target.");
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots[1], Does.Contain(target));
    }

    [Test]
    public void GainResourceSkill_UsesPostCostSnapshot_AndRunsOnce()
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 0f,
            sourceResourceScale: 1f,
            targetCount: 2);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(
            CharacterLocalization.GetActiveSkillDescription(character.Data),
            Does.Contain("× 1"));
        EnemyRuntime firstTarget = CreateEnemyRuntime();
        EnemyRuntime secondTarget = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
            SelectedEnemyTargets = new[] { firstTarget, secondTarget },
        };
        character.BindBattle(resource, board);

        bool activated = character.TryActivateActiveSkill();

        // Cost 2 is paid before the context snapshot: 6 - 2 = 4,
        // then CurrentResource x 1 restores 4 exactly once.
        Assert.That(activated, Is.True);
        Assert.That(resource.Current, Is.EqualTo(8));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(resource.TryGainCallCount, Is.EqualTo(1));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.Zero,
            "Targetless resource gain must not request action targets.");
        Assert.That(board.DamageTargetSnapshots, Is.Empty);
        Assert.That(character.TotalDamageDealt, Is.Zero);

        // A second activation snapshots 6 after paying its cost, requests
        // another gain of 6, and the resource contract clamps that to 10.
        Assert.That(character.TryActivateActiveSkill(), Is.True);
        Assert.That(resource.Current, Is.EqualTo(10));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(2));
        Assert.That(resource.TryGainCallCount, Is.EqualTo(2));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.Zero,
            "Targetless resource gain must remain targetless.");
    }

    [Test]
    public void GainResourceValidator_AcceptsResourceScale_AndRejectsEmptyFormula()
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 0f,
            sourceResourceScale: 1f,
            targetCount: 1);

        CharacterDefinitionValidationResult validResult =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validResult.IsValid,
            Is.True,
            string.Join("\n", validResult.Diagnostics));

        SerializedObject serialized = new(definition);
        SerializedProperty effect = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("damageAmount").floatValue = 0f;
        effect.FindPropertyRelative("sourceResourceScale").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult invalidResult =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(invalidResult.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                invalidResult,
                "effect.resource_gain_invalid"),
            Is.True,
            string.Join("\n", invalidResult.Diagnostics));
    }

    [Test]
    public void TargetScaledDamage_UsesPerEffectSnapshots_AndSeesPriorStatus()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition = CreateTargetScalingCharacter(
            fire,
            emergencyKit);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(
            character.ApplyStatusEffect(emergencyKit, 1f, 2),
            Is.True);
        Assert.That(
            CharacterLocalization.GetActiveSkillDescription(character.Data),
            Does.Contain(" - "));

        EnemyRuntime firstTarget = CreateEnemyRuntime(20);
        EnemyRuntime secondTarget = CreateEnemyRuntime(40);
        SetEnemyHealth(firstTarget, 10);
        SetEnemyHealth(secondTarget, 20);
        Assert.That(
            ApplyEnemyStatus(
                firstTarget,
                fire,
                3f,
                1,
                character,
                fire.TickInterval),
            Is.True);

        bool mutatedAfterFirstDamage = false;
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
            SelectedEnemyTargets =
                new[] { firstTarget, secondTarget },
            ApplyEffectsToEnemyRuntime = true,
        };
        board.TargetDamageApplied = (_, _) =>
        {
            if (mutatedAfterFirstDamage)
                return;

            mutatedAfterFirstDamage = true;
            ApplyEnemyStatus(
                secondTarget,
                fire,
                3f,
                5,
                character,
                fire.TickInterval);
        };
        FakeActiveSkillResource resource = new(10);
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        // ApplyStatus adds 2 stacks before Damage. The damage formula is:
        // missing HP x 0.5 + source EmergencyKit stacks x 1
        // + target Fire stacks x 1.
        // First: 5 + 2 + 3 = 10. Second: 10 + 2 + 2 = 14.
        // The callback adds 5 Fire stacks to the second target after the
        // first damage is applied, but its snapshotted damage stays 14.
        Assert.That(resource.Current, Is.EqualTo(9));
        Assert.That(board.StatusApplyCallCount, Is.EqualTo(1));
        Assert.That(
            board.DamageAmounts,
            Is.EqualTo(new[] { 10, 14 }));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(24));
        Assert.That(GetEnemyStatusStacks(secondTarget, fire), Is.EqualTo(7));
    }

    [Test]
    public void TargetOnlyDamageScaling_RemainsUsableWithoutPreviewTarget()
    {
        CharacterSO definition = CreateTargetOnlyScalingCharacter();
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime target = CreateEnemyRuntime(20);
        SetEnemyHealth(target, 12);
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
            ApplyEffectsToEnemyRuntime = true,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(9));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 3 }));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(3));
    }

    [Test]
    public void TargetScalingValidator_RejectsMissingStatusAndOneShotTargetTerms()
    {
        CharacterSO definition = CreateTargetOnlyScalingCharacter();
        Assert.That(
            CharacterDefinitionValidator.Validate(definition).IsValid,
            Is.True);

        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        SerializedProperty effect = skill
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        effect.FindPropertyRelative(
            "targetStatusStacksScale").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult missingStatusResult =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            HasDiagnostic(
                missingStatusResult,
                "effect.target_status_scaling_status_required"),
            Is.True,
            string.Join("\n", missingStatusResult.Diagnostics));

        effect.FindPropertyRelative(
            "targetStatusStacksScale").floatValue = 0f;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult allyHealthResult =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            HasDiagnostic(
                allyHealthResult,
                "effect.target_health_scaling_unsupported"),
            Is.False,
            string.Join("\n", allyHealthResult.Diagnostics));
        Assert.That(
            HasDiagnostic(
                allyHealthResult,
                "effect.ally_damage_unsupported"),
            Is.True,
            string.Join("\n", allyHealthResult.Diagnostics));

        CharacterSO resourceDefinition = CreateResourceGainCharacter(
            fixedAmount: 1f,
            sourceResourceScale: 0f,
            targetCount: 1);
        SerializedObject serializedResource = new(resourceDefinition);
        serializedResource
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("targetMaxHealthScale")
            .floatValue = 0.5f;
        serializedResource.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult resourceResult =
            CharacterDefinitionValidator.Validate(resourceDefinition);
        Assert.That(
            HasDiagnostic(
                resourceResult,
                "effect.target_scaling_unsupported"),
            Is.True,
            string.Join("\n", resourceResult.Diagnostics));
    }

    [Test]
    public void ExplicitEffects_PreserveDefaultOrUseSourceStatusOverride()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_explicit_target_default",
            false,
            true,
            StatusEffectStackMode.Replace,
            0);
        CharacterSO[] definitions =
        {
            CreateExplicitDamageAndStatusCharacter(status),
            CreateSourceRetargetCharacter(status),
        };

        int explicitEffectCount = 0;
        foreach (CharacterSO definition in definitions)
        {
            string fixtureName = definition.name;

            foreach (CharacterAttackDefinition attack in
                     definition.AttackDefinitions)
            {
                Assert.That(
                    attack,
                    Is.Not.Null,
                    $"{fixtureName} contains a null attack definition.");
                AssertEffectsPreserveTargetDefault(
                    attack.Effects,
                    $"{fixtureName}.attack",
                    ref explicitEffectCount);
            }

            foreach (CharacterPassiveDefinition passive in
                     definition.PassiveDefinitions)
            {
                Assert.That(
                    passive,
                    Is.Not.Null,
                    $"{fixtureName} contains a null passive definition.");
                AssertEffectsPreserveTargetDefault(
                    passive.Effects,
                    $"{fixtureName}.passive",
                    ref explicitEffectCount);
            }

            foreach (CharacterSkillDefinition skill in
                     definition.SkillDefinitions)
            {
                Assert.That(
                    skill,
                    Is.Not.Null,
                    $"{fixtureName} contains a null skill definition.");
                AssertEffectsPreserveTargetDefault(
                    skill.Effects,
                    $"{fixtureName}.skill",
                    ref explicitEffectCount);
            }
        }

        Assert.That(
            explicitEffectCount,
            Is.GreaterThan(0),
            "At least one fixture effect must protect the " +
            "serialized target defaults.");
    }

    [Test]
    public void ManualSkill_WaitsForSelectionThenResumesWithChosenTarget()
    {
        CharacterSO definition =
            CreateExplicitDamageAndStatusCharacter(null);
        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Manual;
        skill.FindPropertyRelative("subjectCount").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime enemy = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { enemy },
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);
        Assert.That(board.IsManualTargetSelectionPending, Is.True);
        Assert.That(
            board.CharacterTargetSelectionCounts,
            Is.EqualTo(new[] { 1 }),
            "Collecting all manual candidates must not request an " +
            "unbounded target-list capacity.");
        Assert.That(
            board.CurrentManualTargetRequest.RequiredCount,
            Is.EqualTo(1));
        Assert.That(resource.Current, Is.EqualTo(6));
        Assert.That(resource.TrySpendCallCount, Is.Zero);
        Assert.That(board.DamageTargetSnapshots, Is.Empty);

        board.CompleteManualEnemyTargets(enemy);
        character.TickBattle(0.1f, board);

        Assert.That(board.IsManualTargetSelectionPending, Is.False);
        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(new[] { enemy }));
        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 4 }));
    }

    [Test]
    public void ManualBasicAttack_WaitsForSelectionThenUsesChosenTarget()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "ManualBasicAttackFixture",
            attackPower: 10f,
            attackCooldown: 0.1f);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("attackDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("subject")
            .enumValueIndex = (int)CharacterAttackSubject.Manual;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime enemy = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { enemy },
        };
        character.BindBattle(null, board);

        character.TickBattle(0.2f, board);

        Assert.That(board.IsManualTargetSelectionPending, Is.True);
        Assert.That(board.DamageTargetSnapshots, Is.Empty);

        board.CompleteManualEnemyTargets(enemy);
        character.TickBattle(0.1f, board);

        Assert.That(board.IsManualTargetSelectionPending, Is.False);
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(new[] { enemy }));
        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void TargetlessCooldownPassive_IgnoresManualSubjectAndExecutes()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "ManualCooldownPassiveFixture");
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnCooldown;
        passive.FindPropertyRelative("cooldown").floatValue = 0.1f;
        passive.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Manual;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime enemy = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(0, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { enemy },
        };
        character.BindBattle(resource, board);

        character.TickBattle(0.2f, board);

        Assert.That(board.IsManualTargetSelectionPending, Is.False);
        Assert.That(resource.Current, Is.EqualTo(1));
        Assert.That(resource.TryGainCallCount, Is.EqualTo(1));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.Zero,
            "GainResource does not consume action targets even when the " +
            "stored subject is Manual.");
    }

    [Test]
    public void SourceStatusThenInheritedDamage_RetargetsOnlyFirstEffect()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition =
            CreateSourceRetargetCharacter(emergencyKit);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime enemy = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { enemy },
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        // Cost 2 is paid before the shared action context is snapshotted.
        // Source ApplyStatus must not replace that context or consume the
        // inherited enemy range visualization.
        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(
            character.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(GetEnemyStatusStacks(enemy, emergencyKit), Is.Zero);
        Assert.That(board.StatusApplyCallCount, Is.Zero);
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(new[] { enemy }));
        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 4 }));
        Assert.That(
            board.DamageShowAttackRangeSnapshots,
            Is.EqualTo(new[] { true }));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(4));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(1));
    }

    [Test]
    public void EffectTargetModeValidator_RejectsInvalidAndUnsupportedSourceEffects()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);

        CharacterSO invalidModeDefinition =
            CreateSourceRetargetCharacter(emergencyKit);
        SerializedObject invalidModeSerialized =
            new(invalidModeDefinition);
        SerializedProperty invalidModeEffect = invalidModeSerialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        invalidModeEffect.FindPropertyRelative("targetMode").intValue = 999;
        invalidModeSerialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult invalidModeResult =
            CharacterDefinitionValidator.Validate(invalidModeDefinition);
        Assert.That(invalidModeResult.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                invalidModeResult,
                "effect.target_mode_invalid"),
            Is.True,
            string.Join("\n", invalidModeResult.Diagnostics));

        CharacterSO sourceDamageDefinition =
            CreateSourceRetargetCharacter(emergencyKit);
        SerializedObject sourceDamageSerialized =
            new(sourceDamageDefinition);
        SerializedProperty sourceDamageEffect = sourceDamageSerialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        sourceDamageEffect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        sourceDamageEffect.FindPropertyRelative(
            "damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        sourceDamageEffect.FindPropertyRelative(
            "damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        sourceDamageEffect.FindPropertyRelative(
            "damageAmount").floatValue = 1f;
        sourceDamageSerialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult sourceDamageResult =
            CharacterDefinitionValidator.Validate(sourceDamageDefinition);
        Assert.That(sourceDamageResult.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                sourceDamageResult,
                "effect.ally_damage_unsupported"),
            Is.True,
            string.Join("\n", sourceDamageResult.Diagnostics));

        CharacterSO incompatibleStatusDefinition =
            CreateSourceRetargetCharacter(fire);
        CharacterDefinitionValidationResult incompatibleStatusResult =
            CharacterDefinitionValidator.Validate(
                incompatibleStatusDefinition);
        Assert.That(incompatibleStatusResult.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                incompatibleStatusResult,
                "effect.status_faction_mismatch"),
            Is.True,
            string.Join("\n", incompatibleStatusResult.Diagnostics));
    }

    [Test]
    public void TargetlessSourceSkill_ActivatesWithoutEnemiesOrSelection()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition =
            CreateTargetlessSourceCharacter(emergencyKit);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        string description =
            CharacterLocalization.GetActiveSkillDescription(character.Data);
        Assert.That(
            description,
            Does.Contain("행동 대상 불필요")
                .Or.Contain("No action target required"));
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(
            character.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.Zero);
        Assert.That(
            board.AlliedCharacterTargetSelectionCallCount,
            Is.Zero);
        Assert.That(board.DamageTargetSnapshots, Is.Empty);
    }

    [Test]
    public void OptionalInheritedEffect_StillRequiresActionTargetsInDescription()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition =
            CreateSourceRetargetCharacter(emergencyKit);
        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        SerializedProperty effects = skill.FindPropertyRelative("effects");
        effects.DeleteArrayElementAtIndex(0);
        effects.GetArrayElementAtIndex(0)
            .FindPropertyRelative("preconditionFailurePolicy")
            .enumValueIndex =
            (int)CharacterEffectPreconditionFailurePolicy.SkipEffect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterSkillDefinition definitionSkill =
            definition.SkillDefinitions[0];
        string description = CharacterLocalization
            .GetActiveSkillDescription(definition.CreateData());

        Assert.That(
            BattleAbilityRules.RequiresActionTargets(definitionSkill),
            Is.True);
        Assert.That(description, Does.Not.Contain("행동 대상 불필요"));
        Assert.That(
            description,
            Does.Not.Contain("No action target required"));
    }

    [Test]
    public void TargetlessGainResourceSkill_UsesPostCostSnapshotOnce()
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 0f,
            sourceResourceScale: 1f,
            targetCount: 1);
        SetFirstSkillSubject(
            definition,
            CharacterAttackSubject.None);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        // Cost 2 is paid first, then the targetless resource effect uses the
        // post-cost action snapshot (4) exactly once: 6 - 2 + 4 = 8.
        Assert.That(resource.Current, Is.EqualTo(8));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(resource.TryGainCallCount, Is.EqualTo(1));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.Zero);
        Assert.That(board.DamageTargetSnapshots, Is.Empty);
    }

    [Test]
    public void MixedSourceAndInheritedEffects_NoTargets_DoesNotPayPartially()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition =
            CreateSourceRetargetCharacter(emergencyKit);
        SetFirstSkillSubject(
            definition,
            CharacterAttackSubject.None);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.False);

        Assert.That(resource.Current, Is.EqualTo(6));
        Assert.That(resource.TrySpendCallCount, Is.Zero);
        Assert.That(
            character.GetStatusStackCount(emergencyKit),
            Is.Zero);
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.Zero,
            "Without a basic-attack selector there is no valid fallback " +
            "search policy.");
        Assert.That(board.DamageTargetSnapshots, Is.Empty);
    }

    [Test]
    public void AllySelfSkill_ActivatesWithoutLivingEnemies()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition =
            CreateAllySelfCharacter(emergencyKit);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(
            character.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.Zero);
        Assert.That(
            board.AlliedCharacterTargetSelectionCallCount,
            Is.EqualTo(1));
    }

    [Test]
    public void FreshSelectionSkill_PreparesOnceAndKeepsDistinctTargets()
    {
        CharacterSO definition = CreateFreshSelectionCharacter();
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        string description =
            CharacterLocalization.GetActiveSkillDescription(character.Data);
        Assert.That(
            description,
            Does.Contain("별도 선택")
                .Or.Contain("Fresh selection"));

        EnemyRuntime actionTarget = CreateEnemyRuntime();
        EnemyRuntime freshTarget = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
        };
        board.PlannedEnemySelections.Enqueue(
            new[] { actionTarget });
        board.PlannedEnemySelections.Enqueue(
            new[] { freshTarget });
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(new[] { actionTarget }));
        Assert.That(
            board.DamageTargetSnapshots[1],
            Is.EqualTo(new[] { freshTarget }));
        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 2, 3 }));
        Assert.That(
            board.DamageShowAttackRangeSnapshots,
            Is.EqualTo(new[] { true, true }));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(5));
    }

    [Test]
    public void FreshSelectionSkill_ExecutesWithoutActionTarget()
    {
        CharacterSO definition = CreateFreshSelectionCharacter();
        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        skill.FindPropertyRelative("effects")
            .DeleteArrayElementAtIndex(0);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime freshTarget = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
        };
        board.PlannedEnemySelections.Enqueue(
            new[] { freshTarget });
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(1));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(new[] { freshTarget }));
        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void FreshSelectionConditions_DoNotExpandLegacyTiles()
    {
        CharacterSO definition = CreateFreshSelectionCharacter();
        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        SerializedProperty effects = skill.FindPropertyRelative("effects");
        effects.DeleteArrayElementAtIndex(0);
        SerializedProperty selector = effects
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("targetSelector");
        SerializedProperty conditions = selector.FindPropertyRelative(
            "numericConditions");
        conditions.arraySize = 1;
        SerializedProperty condition =
            conditions.GetArrayElementAtIndex(0);
        condition.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterConditionType.Numeric;
        condition.FindPropertyRelative("metric").enumValueIndex =
            (int)CharacterNumericConditionMetric.Health;
        condition.FindPropertyRelative("comparison").enumValueIndex =
            (int)CharacterNumericComparison.GreaterThanOrEqual;
        condition.FindPropertyRelative("threshold").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        EnemyRuntime center = CreateEnemyRuntime();
        EnemyRuntime cross = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
            SimulateAislingAreaSequence = true,
        };
        board.ConfigureAislingTargets(
            center,
            cross,
            CreateEnemyRuntime(),
            CreateEnemyRuntime());
        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(1));
        Assert.That(
            board.SelectionNumericConditionCounts,
            Is.EqualTo(new[] { 1 }));
        Assert.That(board.AreaExpansionCallCount, Is.Zero);
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(new[] { center }));
        Assert.That(
            board.DamageShowAttackRangeSnapshots,
            Is.EqualTo(new[] { true }));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(3));
    }

    [Test]
    public void FreshSelectionValidator_RejectsNoneAndUsesSelectorFaction()
    {
        CharacterSO noneDefinition = CreateFreshSelectionCharacter();
        SerializedObject noneSerialized = new(noneDefinition);
        SerializedProperty noneSelector = noneSerialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(1)
            .FindPropertyRelative("targetSelector");
        noneSelector.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        noneSerialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult noneResult =
            CharacterDefinitionValidator.Validate(noneDefinition);
        Assert.That(noneResult.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                noneResult,
                "effect.fresh_subject_none"),
            Is.True,
            string.Join("\n", noneResult.Diagnostics));

        CharacterSO allyDefinition = CreateFreshSelectionCharacter();
        SerializedObject allySerialized = new(allyDefinition);
        SerializedProperty allySelector = allySerialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(1)
            .FindPropertyRelative("targetSelector");
        allySelector.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        allySelector.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        allySerialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult allyResult =
            CharacterDefinitionValidator.Validate(allyDefinition);
        Assert.That(allyResult.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                allyResult,
                "effect.ally_damage_unsupported"),
            Is.True,
            string.Join("\n", allyResult.Diagnostics));
    }

    [Test]
    public void OptionalEffect_MissingTargetSkipsWithoutBlockingAction()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition =
            CreateSourceRetargetCharacter(emergencyKit);
        SetFirstSkillSubject(
            definition,
            CharacterAttackSubject.None);

        SerializedObject serialized = new(definition);
        SerializedProperty optionalDamage = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(1);
        optionalDamage.FindPropertyRelative(
            "preconditionFailurePolicy").enumValueIndex =
            (int)CharacterEffectPreconditionFailurePolicy.SkipEffect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(
            CharacterLocalization.GetActiveSkillDescription(character.Data),
            Does.Contain("선택 효과")
                .Or.Contain("Optional"));
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(
            character.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
        Assert.That(board.DamageTargetSnapshots, Is.Empty);
    }

    [Test]
    public void StopOnEffectFailure_DoesNotExecuteRemainingEffects()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        CharacterSO definition =
            CreateExplicitDamageAndStatusCharacter(fire);
        SerializedObject serialized = new(definition);
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        effects.MoveArrayElement(1, 0);
        effects.GetArrayElementAtIndex(0)
            .FindPropertyRelative("failurePolicy").enumValueIndex =
            (int)CharacterEffectFailurePolicy.StopRemainingEffects;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
            ForceStatusApplyFailure = true,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(board.StatusApplyCallCount, Is.EqualTo(1));
        Assert.That(board.DamageTargetSnapshots, Is.Empty);
        Assert.That(character.TotalDamageDealt, Is.Zero);
    }

    [Test]
    public void FirstSuccessful_SkipsUnaffordableCandidateBeforeTargeting()
    {
        CharacterSO definition = CreateFreshSelectionCharacter();
        SerializedObject serialized = new(definition);
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.InsertArrayElementAtIndex(0);
        skills.GetArrayElementAtIndex(0)
            .FindPropertyRelative("cost").intValue = 9;
        skills.GetArrayElementAtIndex(1)
            .FindPropertyRelative("cost").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime actionTarget = CreateEnemyRuntime();
        EnemyRuntime freshTarget = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 2,
        };
        board.PlannedEnemySelections.Enqueue(
            new[] { actionTarget });
        board.PlannedEnemySelections.Enqueue(
            new[] { freshTarget });
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(5));
    }

    [Test]
    public void FreeSkill_CommitsWithoutCallingResourceSpend()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        CharacterSO definition =
            CreateTargetlessSourceCharacter(emergencyKit);
        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(0, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.Zero);
        Assert.That(resource.TrySpendCallCount, Is.Zero);
        Assert.That(
            character.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));
    }

    [Test]
    public void EffectFailurePolicies_RejectUnknownSerializedValues()
    {
        CharacterSO definition = CreateFreshSelectionCharacter();
        SerializedObject serialized = new(definition);
        SerializedProperty effect = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        effect.FindPropertyRelative(
            "preconditionFailurePolicy").intValue = 99;
        effect.FindPropertyRelative("failurePolicy").intValue = 99;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                validation,
                "effect.precondition_policy_invalid"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
        Assert.That(
            HasDiagnostic(
                validation,
                "effect.failure_policy_invalid"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void SpendResourceSkill_ReservesBaseAndMultipleEffects()
    {
        CharacterSO definition =
            CreateResourceSpendCharacter(2f, 1f);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(
            CharacterLocalization.GetActiveSkillDescription(character.Data),
            Does.Contain("자원 소비")
                .Or.Contain("Spend Resource"));
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(1));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(3));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.Zero);
        Assert.That(board.DamageTargetSnapshots, Is.Empty);
    }

    [Test]
    public void SpendResourceSkill_CombinedShortageDoesNotPayPartially()
    {
        CharacterSO definition =
            CreateResourceSpendCharacter(2f, 1f);
        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(4, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.False);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.Zero);
        Assert.That(board.CharacterTargetSelectionCallCount, Is.Zero);
    }

    [Test]
    public void OptionalSpendResource_ShortageSkipsOnlyOptionalEffect()
    {
        CharacterSO definition =
            CreateResourceSpendCharacter(2f, 3f);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(1)
            .FindPropertyRelative(
                "preconditionFailurePolicy").enumValueIndex =
            (int)CharacterEffectPreconditionFailurePolicy.SkipEffect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(5, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(1));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(2));
    }

    [Test]
    public void StopBeforeSpend_DoesNotChargeUnreachedReservation()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        CharacterSO definition =
            CreateExplicitDamageAndStatusCharacter(fire);
        SerializedObject serialized = new(definition);
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        effects.MoveArrayElement(1, 0);
        effects.GetArrayElementAtIndex(0)
            .FindPropertyRelative("failurePolicy").enumValueIndex =
            (int)CharacterEffectFailurePolicy.StopRemainingEffects;
        ConfigureFixedResourceSpendEffect(
            effects.GetArrayElementAtIndex(1),
            2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { CreateEnemyRuntime() },
            ForceStatusApplyFailure = true,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(board.StatusApplyCallCount, Is.EqualTo(1));
    }

    [Test]
    public void GainInSameAction_DoesNotFundSpendReservation()
    {
        CharacterSO definition =
            CreateResourceSpendCharacter(1f);
        SerializedObject serialized = new(definition);
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        effects.InsertArrayElementAtIndex(0);
        SerializedProperty gain = effects.GetArrayElementAtIndex(0);
        gain.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.GainResource;
        gain.FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.InheritAction;
        gain.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        gain.FindPropertyRelative("damageAmount").floatValue = 5f;
        gain.FindPropertyRelative("sourceResourceScale").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(2, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.False);

        Assert.That(resource.Current, Is.EqualTo(2));
        Assert.That(resource.TrySpendCallCount, Is.Zero);
        Assert.That(resource.TryGainCallCount, Is.Zero);
    }

    [Test]
    public void SpendResourceValidator_RejectsScalingAndSubUnitAmount()
    {
        CharacterSO definition =
            CreateResourceSpendCharacter(0.5f);
        SerializedObject serialized = new(definition);
        SerializedProperty spend = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        spend.FindPropertyRelative("sourceResourceScale").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                validation,
                "effect.resource_spend_invalid"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void AllyHeal_RestoresCharacterHealthAndResetsWithRuntime()
    {
        CharacterSO definition = CreateHealCharacter(
            CharacterTargetFaction.Ally,
            CharacterAttackSubject.Self,
            3f);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(character.MaximumHealth, Is.EqualTo(10));
        Assert.That(character.TrySpendHealth(4), Is.True);
        Assert.That(character.CurrentHealth, Is.EqualTo(6));
        Assert.That(
            CharacterLocalization.GetActiveSkillDescription(character.Data),
            Does.Contain("체력 회복")
                .Or.Contain("Heal"));
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(character.CurrentHealth, Is.EqualTo(9));
        Assert.That(resource.Current, Is.EqualTo(4));
        character.ResetRuntime();
        Assert.That(character.CurrentHealth, Is.EqualTo(10));
    }

    [Test]
    public void DungeonBattlePreparation_PreservesHealthAndAppliesEfficiency()
    {
        CharacterSO definition = CreateHealCharacter(
            CharacterTargetFaction.Ally,
            CharacterAttackSubject.Self,
            3f);
        CharacterRuntime character = CreateCharacter(definition);
        float baseAttackPower = character.Data.AttackPower;

        Assert.That(character.TrySpendHealth(4), Is.True);
        Assert.That(character.CurrentHealth, Is.EqualTo(6));
        character.GainShield(5);

        character.PrepareForNextBattle();

        Assert.That(character.CurrentHealth, Is.EqualTo(6));
        Assert.That(character.CurrentShield, Is.Zero);
        Assert.That(character.HealthPerformancePercentage, Is.EqualTo(6f));
        Assert.That(character.HealthPerformanceMultiplier, Is.EqualTo(0.06f));
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(baseAttackPower * 0.06f).Within(0.001f));

        character.ResetRuntime();
        Assert.That(character.CurrentHealth, Is.EqualTo(10));
        Assert.That(character.HealthPerformanceMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void DungeonRunHealthLoss_CanExhaustAndRoomRecoveryCanRevive()
    {
        CharacterSO definition = CreateHealCharacter(
            CharacterTargetFaction.Ally,
            CharacterAttackSubject.Self,
            3f);
        CharacterRuntime character = CreateCharacter(definition);
        character.BeginDungeonRun();

        Assert.That(character.ApplyRunHealthLoss(10), Is.EqualTo(10));
        Assert.That(character.CurrentHealth, Is.Zero);
        Assert.That(character.CanParticipate, Is.False);
        Assert.That(character.Heal(3), Is.Zero);
        Assert.That(character.RestoreHealth(3, true), Is.EqualTo(3));
        Assert.That(character.CurrentHealth, Is.EqualTo(3));
        Assert.That(character.CanParticipate, Is.True);
    }

    [Test]
    public void EnemyHeal_UsesPerTargetMaximumHealthScaling()
    {
        CharacterSO definition = CreateHealCharacter(
            CharacterTargetFaction.Enemy,
            CharacterAttackSubject.Random,
            0f);
        SerializedObject serialized = new(definition);
        SerializedProperty heal = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        heal.FindPropertyRelative("targetMaxHealthScale").floatValue =
            0.25f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        EnemyRuntime target = CreateEnemyRuntime(20);
        SetEnemyHealth(target, 10);
        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(target.Health, Is.EqualTo(15));
        Assert.That(resource.Current, Is.EqualTo(4));
    }

    [Test]
    public void SpendHealthSkill_ReservesMultipleCostsAndLeavesOneHealth()
    {
        CharacterSO definition =
            CreateHealthSpendCharacter(3f, 6f);
        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(character.CurrentHealth, Is.EqualTo(1));
        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
    }

    [Test]
    public void SpendHealthSkill_LethalCombinedCostDoesNotPayResource()
    {
        CharacterSO definition =
            CreateHealthSpendCharacter(3f, 7f);
        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.False);

        Assert.That(character.CurrentHealth, Is.EqualTo(10));
        Assert.That(resource.Current, Is.EqualTo(6));
        Assert.That(resource.TrySpendCallCount, Is.Zero);
    }

    [Test]
    public void OptionalSpendHealth_LethalCostSkipsOnlyOptionalEffect()
    {
        CharacterSO definition =
            CreateHealthSpendCharacter(6f, 4f);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(1)
            .FindPropertyRelative(
                "preconditionFailurePolicy").enumValueIndex =
            (int)CharacterEffectPreconditionFailurePolicy.SkipEffect;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(character.CurrentHealth, Is.EqualTo(4));
        Assert.That(resource.Current, Is.EqualTo(4));
    }

    [Test]
    public void StopBeforeSpendHealth_DoesNotChargeUnreachedCost()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        CharacterSO definition =
            CreateExplicitDamageAndStatusCharacter(fire);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("maximumHealth").intValue = 10;
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        effects.MoveArrayElement(1, 0);
        effects.GetArrayElementAtIndex(0)
            .FindPropertyRelative("failurePolicy").enumValueIndex =
            (int)CharacterEffectFailurePolicy.StopRemainingEffects;
        ConfigureFixedHealthSpendEffect(
            effects.GetArrayElementAtIndex(1),
            3f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { CreateEnemyRuntime() },
            ForceStatusApplyFailure = true,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(character.CurrentHealth, Is.EqualTo(10));
        Assert.That(resource.Current, Is.EqualTo(4));
        Assert.That(board.StatusApplyCallCount, Is.EqualTo(1));
    }

    [Test]
    public void HealInSameAction_DoesNotFundHealthSpendReservation()
    {
        CharacterSO definition =
            CreateHealthSpendCharacter(2f);
        SerializedObject serialized = new(definition);
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        effects.InsertArrayElementAtIndex(0);
        ConfigureHealEffect(
            effects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.Source,
            5f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(character.TrySpendHealth(8), Is.True);
        Assert.That(character.CurrentHealth, Is.EqualTo(2));
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.False);

        Assert.That(character.CurrentHealth, Is.EqualTo(2));
        Assert.That(resource.Current, Is.EqualTo(6));
    }

    [Test]
    public void SpendHealthValidator_RejectsScalingAndSubUnitAmount()
    {
        CharacterSO definition =
            CreateHealthSpendCharacter(0.5f);
        SerializedObject serialized = new(definition);
        SerializedProperty spend = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        spend.FindPropertyRelative("targetMaxHealthScale").floatValue =
            1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                validation,
                "effect.health_spend_invalid"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void AllyShield_StacksSurvivesHealthSpendAndResetsWithRuntime()
    {
        CharacterSO definition = CreateShieldCharacter(
            CharacterTargetFaction.Ally,
            CharacterAttackSubject.Self,
            3f,
            4f);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 0,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(character.CurrentShield, Is.EqualTo(7));
        Assert.That(character.TrySpendHealth(3), Is.True);
        Assert.That(character.CurrentShield, Is.EqualTo(7));
        Assert.That(
            CharacterLocalization.GetActiveSkillDescription(character.Data),
            Does.Contain("보호막").Or.Contain("Shield"));
        character.ResetRuntime();
        Assert.That(character.CurrentShield, Is.Zero);
    }

    [Test]
    public void EnemyShield_UsesPerTargetMaximumHealthScaling()
    {
        CharacterSO definition = CreateShieldCharacter(
            CharacterTargetFaction.Enemy,
            CharacterAttackSubject.Random,
            0f);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("targetMaxHealthScale").floatValue =
            0.25f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyRuntime target = CreateEnemyRuntime(20);
        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(target.CurrentShield, Is.EqualTo(5));
        Assert.That(resource.Current, Is.EqualTo(4));
    }

    [Test]
    public void EnemyShield_AbsorbsPhysicalDamageBeforeArmorAndHealth()
    {
        EnemyRuntime target = CreateEnemyRuntime(
            maximumHealth: 20,
            initialArmorMultiplier: 0.5f);
        Assert.That(target.GainShield(4), Is.EqualTo(4));

        int applied = TakeEnemyDamage(
            target,
            7,
            CharacterAttackDamageType.Physical);

        Assert.That(applied, Is.EqualTo(7));
        Assert.That(target.CurrentShield, Is.Zero);
        Assert.That(target.Armor, Is.EqualTo(7));
        Assert.That(target.Health, Is.EqualTo(20));
    }

    [Test]
    public void AllyShield_AbsorbsDamageBeforeHealth()
    {
        CharacterSO definition = CreateShieldCharacter(
            CharacterTargetFaction.Ally,
            CharacterAttackSubject.Self,
            1f);
        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(character.GainShield(4), Is.EqualTo(4));

        int applied = character.TakeDamage(6);

        Assert.That(applied, Is.EqualTo(6));
        Assert.That(character.CurrentShield, Is.Zero);
        Assert.That(character.CurrentHealth, Is.EqualTo(8));
    }

    [Test]
    public void EnemyShield_IsBypassedByFixedDamage()
    {
        EnemyRuntime target = CreateEnemyRuntime(20);
        Assert.That(target.GainShield(4), Is.EqualTo(4));

        int applied = TakeEnemyDamage(
            target,
            6,
            CharacterAttackDamageType.Fixed);

        Assert.That(applied, Is.EqualTo(6));
        Assert.That(target.CurrentShield, Is.EqualTo(4));
        Assert.That(target.Health, Is.EqualTo(14));
    }

    [Test]
    public void FixedDamageAfterShield_BypassesNewShield()
    {
        CharacterSO definition = CreateShieldCharacter(
            CharacterTargetFaction.Enemy,
            CharacterAttackSubject.Random,
            5f);
        SerializedObject serialized = new(definition);
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        effects.InsertArrayElementAtIndex(1);
        SerializedProperty damage = effects.GetArrayElementAtIndex(1);
        damage.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        damage.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        damage.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        damage.FindPropertyRelative("damageAmount").floatValue = 7f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyRuntime target = CreateEnemyRuntime(20);
        CharacterRuntime character = CreateCharacter(definition);
        FakeActiveSkillResource resource = new(6, 10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
            ApplyEffectsToEnemyRuntime = true,
        };
        character.BindBattle(resource, board);

        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(target.CurrentShield, Is.EqualTo(5));
        Assert.That(target.Health, Is.EqualTo(13));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(7));
    }

    [Test]
    public void ShieldValidator_RejectsEmptyFormula()
    {
        CharacterSO definition = CreateShieldCharacter(
            CharacterTargetFaction.Ally,
            CharacterAttackSubject.Self,
            0f);

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(validation, "effect.shield_invalid"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void ShieldMetrics_AreValidForEnemyAndAllyTargets()
    {
        CharacterSO definition = CreateShieldCharacter(
            CharacterTargetFaction.Enemy,
            CharacterAttackSubject.HighestValue,
            2f);
        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Condition,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("subjectMetric").enumValueIndex =
            (int)CharacterAttackSubjectMetric.Shield;
        SerializedProperty conditions =
            skill.FindPropertyRelative("numericConditions");
        conditions.arraySize = 1;
        SerializedProperty condition =
            conditions.GetArrayElementAtIndex(0);
        condition.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterConditionType.Numeric;
        condition.FindPropertyRelative("metric").enumValueIndex =
            (int)CharacterNumericConditionMetric.Shield;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult enemyValidation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            enemyValidation.IsValid,
            Is.True,
            string.Join("\n", enemyValidation.Diagnostics));

        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        CharacterDefinitionValidationResult allyValidation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            allyValidation.IsValid,
            Is.True,
            string.Join("\n", allyValidation.Diagnostics));
    }

    [Test]
    public void CharacterModifier_ModulesResolveByActionAndEffect()
    {
        CharacterModifierCollection modifiers = new();
        CharacterModifierModule allSkillDamage = new(
            "all_skill_damage",
            new CharacterModifierTarget(
                CharacterModifierTargetScope.ActionKind,
                CharacterActionKind.Skill),
            CharacterModifierStat.Damage,
            CharacterModifierOperation.AddPercent,
            0.2f);
        CharacterModifierModule selectedSkillDamage = new(
            "selected_skill_damage",
            new CharacterModifierTarget(
                CharacterModifierTargetScope.Action,
                CharacterActionKind.Skill,
                "skill_a"),
            CharacterModifierStat.Damage,
            CharacterModifierOperation.AddFlat,
            5f);
        CharacterModifierModule selectedEffectDuration = new(
            "burn_duration",
            new CharacterModifierTarget(
                CharacterModifierTargetScope.Effect,
                CharacterActionKind.Skill,
                "skill_a",
                "burn"),
            CharacterModifierStat.StatusDuration,
            CharacterModifierOperation.AddFlat,
            1f);

        Assert.That(modifiers.ReplaceSource(
            "test",
            new[]
            {
                allSkillDamage,
                selectedSkillDamage,
                selectedEffectDuration,
            },
            1,
            CharacterModifierLifetimeScope.Dungeon), Is.True);

        Assert.That(
            modifiers.Resolve(
                10f,
                CharacterModifierStat.Damage,
                CharacterActionKind.Skill,
                "skill_a"),
            Is.EqualTo(18f).Within(0.001f));
        Assert.That(
            modifiers.Resolve(
                10f,
                CharacterModifierStat.Damage,
                CharacterActionKind.Skill,
                "skill_b"),
            Is.EqualTo(12f).Within(0.001f));
        Assert.That(
            modifiers.Resolve(
                2f,
                CharacterModifierStat.StatusDuration,
                CharacterActionKind.Skill,
                "skill_a",
                "burn"),
            Is.EqualTo(3f).Within(0.001f));
        Assert.That(
            modifiers.Resolve(
                2f,
                CharacterModifierStat.StatusDuration,
                CharacterActionKind.Skill,
                "skill_a",
                "stun"),
            Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void CharacterModifier_TimedAndScopedSourcesExpireIndependently()
    {
        CharacterModifierCollection modifiers = new();
        CharacterModifierModule attackPower = new(
            "power",
            new CharacterModifierTarget(
                CharacterModifierTargetScope.Character),
            CharacterModifierStat.AttackPower,
            CharacterModifierOperation.AddFlat,
            2f);
        modifiers.ReplaceSource(
            "battle",
            new[] { attackPower },
            1,
            CharacterModifierLifetimeScope.Battle,
            1f);
        modifiers.ReplaceSource(
            "dungeon",
            new[] { attackPower },
            1,
            CharacterModifierLifetimeScope.Dungeon);

        Assert.That(
            modifiers.Resolve(10f, CharacterModifierStat.AttackPower),
            Is.EqualTo(14f).Within(0.001f));
        Assert.That(modifiers.Tick(1f), Is.True);
        Assert.That(
            modifiers.Resolve(10f, CharacterModifierStat.AttackPower),
            Is.EqualTo(12f).Within(0.001f));
        Assert.That(
            modifiers.ClearScope(CharacterModifierLifetimeScope.Battle),
            Is.False);
        Assert.That(
            modifiers.ClearScope(CharacterModifierLifetimeScope.Dungeon),
            Is.True);
        Assert.That(
            modifiers.Resolve(10f, CharacterModifierStat.AttackPower),
            Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void CharacterData_DungeonModifierPersistsUntilScopeIsCleared()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        CharacterData data = definition.CreateData(
            new CharacterProgressData(definition.CharacterId, true));
        CharacterModifierModule attackPower = new(
            "item_power",
            new CharacterModifierTarget(
                CharacterModifierTargetScope.Character),
            CharacterModifierStat.AttackPower,
            CharacterModifierOperation.AddPercent,
            0.5f);

        Assert.That(data.ReplaceModifierSource(
            "item:test",
            new[] { attackPower },
            1,
            CharacterModifierLifetimeScope.Dungeon), Is.True);
        Assert.That(data.AttackPower, Is.EqualTo(15f).Within(0.001f));
        Assert.That(
            data.ClearModifierScope(CharacterModifierLifetimeScope.Battle),
            Is.False);
        Assert.That(data.AttackPower, Is.EqualTo(15f).Within(0.001f));
        Assert.That(
            data.ClearModifierScope(CharacterModifierLifetimeScope.Dungeon),
            Is.True);
        Assert.That(data.AttackPower, Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void DungeonUpgrade_ModularOptionUsesStableIdAndConfiguredValue()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        SerializedObject serialized = new(definition);
        SerializedProperty pools = serialized.FindProperty(
            "dungeonUpgradeDefinitions");
        pools.arraySize = 1;
        SerializedProperty entries = pools.GetArrayElementAtIndex(0)
            .FindPropertyRelative("entries");
        entries.arraySize = 1;
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("upgradeId").stringValue =
            "skill_mastery";
        entry.FindPropertyRelative("probability").floatValue = 2f;
        entry.FindPropertyRelative("limit").intValue = 2;
        SerializedProperty modules = entry.FindPropertyRelative(
            "modifierModules");
        modules.arraySize = 1;
        SerializedProperty module = modules.GetArrayElementAtIndex(0);
        module.FindPropertyRelative("moduleId").stringValue = "power";
        module.FindPropertyRelative("stat").enumValueIndex =
            (int)CharacterModifierStat.AttackPower;
        module.FindPropertyRelative("operation").enumValueIndex =
            (int)CharacterModifierOperation.AddFlat;
        module.FindPropertyRelative("valuePerStack").floatValue = 1.5f;
        module.FindPropertyRelative("target")
            .FindPropertyRelative("scope").enumValueIndex =
            (int)CharacterModifierTargetScope.Character;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterData data = definition.CreateData(
            new CharacterProgressData(definition.CharacterId, true));

        Assert.That(
            data.ApplyDungeonUpgrade(0, "skill_mastery"),
            Is.True);
        Assert.That(data.AttackPower, Is.EqualTo(11.5f).Within(0.001f));
        Assert.That(
            data.ApplyDungeonUpgrade(0, "skill_mastery"),
            Is.True);
        Assert.That(data.AttackPower, Is.EqualTo(13f).Within(0.001f));
        Assert.That(
            data.ApplyDungeonUpgrade(0, "skill_mastery"),
            Is.False);
        Assert.That(
            data.GetDungeonUpgradeAppliedCount(0, "skill_mastery"),
            Is.EqualTo(2));
    }

    [Test]
    public void UpgradeLocalizationPreset_ResolvesDungeonAndCumulativeTitles()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        SerializedObject serialized = new(definition);

        SerializedProperty pools = serialized.FindProperty(
            "dungeonUpgradeDefinitions");
        pools.arraySize = 1;
        SerializedProperty entries = pools.GetArrayElementAtIndex(0)
            .FindPropertyRelative("entries");
        entries.arraySize = 1;
        SerializedProperty dungeonEntry =
            entries.GetArrayElementAtIndex(0);
        dungeonEntry.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterDungeonUpgradeType.AttackPower;
        dungeonEntry.FindPropertyRelative("localizationPreset").intValue =
            (int)CharacterUpgradeLocalizationPreset.SkillCost;

        SerializedProperty cumulative = serialized.FindProperty(
            "cumulativeUpgradeDefinitions");
        cumulative.arraySize = 1;
        SerializedProperty cumulativeEntry =
            cumulative.GetArrayElementAtIndex(0);
        cumulativeEntry.FindPropertyRelative("upgradeId").stringValue =
            "speed_mastery";
        cumulativeEntry.FindPropertyRelative("localizationPreset").intValue =
            (int)CharacterUpgradeLocalizationPreset.AttackSpeed;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(
            CharacterLocalization.GetDungeonUpgradeTitle(
                definition.DungeonUpgradeDefinitions[0].Entries[0]),
            Is.EqualTo(CharacterLocalization.GetUpgradePresetTitle(
                CharacterUpgradeLocalizationPreset.SkillCost)));
        Assert.That(
            CharacterLocalization.GetCumulativeUpgradeTitle(
                definition.CumulativeUpgradeDefinitions[0]),
            Is.EqualTo(CharacterLocalization.GetUpgradePresetTitle(
                CharacterUpgradeLocalizationPreset.AttackSpeed)));
    }

    [Test]
    public void UpgradeLocalizationPreset_CustomUsesConfiguredKeys()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        SerializedObject serialized = new(definition);
        SerializedProperty pools = serialized.FindProperty(
            "dungeonUpgradeDefinitions");
        pools.arraySize = 1;
        SerializedProperty entries = pools.GetArrayElementAtIndex(0)
            .FindPropertyRelative("entries");
        entries.arraySize = 1;
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("localizationPreset").intValue =
            (int)CharacterUpgradeLocalizationPreset.Custom;
        entry.FindPropertyRelative("titleLocalizationKey").stringValue =
            LocalizationKeys.UiDungeonRewardUpgradeSkillPowerTitle;
        entry.FindPropertyRelative("descriptionLocalizationKey").stringValue =
            LocalizationKeys.UiDungeonRewardUpgradeGenericTitle;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDungeonUpgradeEntry configured =
            definition.DungeonUpgradeDefinitions[0].Entries[0];
        Assert.That(
            CharacterLocalization.GetDungeonUpgradeTitle(configured),
            Is.EqualTo(LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardUpgradeSkillPowerTitle)));
        Assert.That(
            CharacterLocalization.GetDungeonUpgradeDescription(
                definition.CreateData(new CharacterProgressData(
                    definition.CharacterId,
                    true)),
                configured),
            Is.EqualTo(LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardUpgradeGenericTitle)));
    }

    [Test]
    public void CumulativeUpgrade_AppliesCompoundModifiersFromSavedLevel()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "veteran",
            5,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 1.5f),
            (CharacterCumulativeUpgradeModifierType.MaximumHealth, 2f),
            (CharacterCumulativeUpgradeModifierType.AttackCooldown, -0.1f),
            (CharacterCumulativeUpgradeModifierType.PassiveDamage, 0.5f),
            (CharacterCumulativeUpgradeModifierType.AttackDamage, 1f),
            (CharacterCumulativeUpgradeModifierType.SkillDamage, 2f),
            (CharacterCumulativeUpgradeModifierType.SkillCostReduction, 1f));
        CharacterProgressData progress = new(
            definition.CharacterId,
            true);
        progress.SetCumulativeUpgradeLevel("veteran", 2);

        CharacterData data = definition.CreateData(progress);

        Assert.That(data.GetCumulativeUpgradeLevel("veteran"), Is.EqualTo(2));
        Assert.That(data.AttackPower, Is.EqualTo(13f).Within(0.001f));
        Assert.That(data.MaximumHealth, Is.EqualTo(14));
        Assert.That(data.AttackCooldown, Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(
            data.PassiveDamageAmountBonus,
            Is.EqualTo(1f).Within(0.001f));
        Assert.That(
            data.AttackDamageFlatBonus,
            Is.EqualTo(2f).Within(0.001f));
        Assert.That(
            data.SkillDamageFlatBonus,
            Is.EqualTo(4f).Within(0.001f));
        Assert.That(data.ActiveSkillCost, Is.EqualTo(1));
        Assert.That(
            CharacterLocalization.GetCumulativeUpgradeDescription(data),
            Does.Contain("veteran").And.Contain("Lv.2/5"));
    }

    [Test]
    public void CumulativeUpgrade_CanUnlockHealthPerformanceAboveOneHundred()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "health_performance_cap",
            5,
            (CharacterCumulativeUpgradeModifierType.HealthPerformanceCap,
                10f));
        CharacterProgressData progress = new(
            definition.CharacterId,
            true);
        progress.SetCumulativeUpgradeLevel(
            "health_performance_cap",
            2);

        CharacterData data = definition.CreateData(progress);

        Assert.That(data.HealthPerformanceCap, Is.EqualTo(120f));
    }

    [Test]
    public void CumulativeUpgrade_SetAndAddClampAndRecalculate()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "power",
            3,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 2f));
        CharacterData data = definition.CreateData(
            new CharacterProgressData(definition.CharacterId, true));
        int statsChangedCount = 0;
        data.StatsChanged += () => statsChangedCount++;

        Assert.That(
            data.AddCumulativeUpgradeLevel("power", 5),
            Is.EqualTo(3));
        Assert.That(data.AttackPower, Is.EqualTo(16f).Within(0.001f));

        Assert.That(
            data.AddCumulativeUpgradeLevel("power", -2),
            Is.EqualTo(1));
        Assert.That(data.AttackPower, Is.EqualTo(12f).Within(0.001f));

        data.SetCumulativeUpgradeLevel("power", 99);
        Assert.That(data.GetCumulativeUpgradeLevel("power"), Is.EqualTo(3));
        Assert.That(data.AttackPower, Is.EqualTo(16f).Within(0.001f));
        Assert.That(statsChangedCount, Is.EqualTo(3));
    }

    [Test]
    public void CumulativeUpgrade_UnlimitedDefinitionAcceptsHighLevel()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "unlimited",
            0,
            (CharacterCumulativeUpgradeModifierType.MaximumHealth, 1f));
        CharacterData data = definition.CreateData(
            new CharacterProgressData(definition.CharacterId, true));

        data.SetCumulativeUpgradeLevel("unlimited", 50);

        Assert.That(
            data.GetCumulativeUpgradeLevel("unlimited"),
            Is.EqualTo(50));
        Assert.That(data.MaximumHealth, Is.EqualTo(60));
    }

    [Test]
    public void CumulativeUpgrade_UnknownProgressIsPreservedButNotApplied()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "known",
            3,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 2f));
        CharacterProgressData progress = new(
            definition.CharacterId,
            true);
        progress.SetCumulativeUpgradeLevel("legacy_unknown", 4);

        CharacterData data = definition.CreateData(progress);

        Assert.That(
            data.GetCumulativeUpgradeLevel("legacy_unknown"),
            Is.EqualTo(4));
        Assert.That(data.CumulativeUpgrades, Has.Count.EqualTo(1));
        Assert.That(data.AttackPower, Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void CumulativeUpgrade_DuplicateIdIsRejectedAndAppliedOnce()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "duplicate",
            3,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 1f));
        ConfigureCumulativeUpgradeDefinition(
            definition,
            1,
            "duplicate",
            3,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 100f));
        CharacterProgressData progress = new(
            definition.CharacterId,
            true);
        progress.SetCumulativeUpgradeLevel("duplicate", 1);

        CharacterData data = definition.CreateData(progress);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(data.AttackPower, Is.EqualTo(11f).Within(0.001f));
        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(validation, "cumulative.id_duplicate"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void CumulativeUpgradeValidator_RejectsInvalidDefinitions()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            string.Empty,
            1,
            (CharacterCumulativeUpgradeModifierType.MaximumHealth, 0.5f),
            (CharacterCumulativeUpgradeModifierType.AttackPower, 0f));
        ConfigureCumulativeUpgradeDefinition(
            definition,
            1,
            "empty_modifiers",
            1);

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(validation, "cumulative.id_required"),
            Is.True);
        Assert.That(
            HasDiagnostic(
                validation,
                "cumulative.modifier_integer_required"),
            Is.True);
        Assert.That(
            HasDiagnostic(
                validation,
                "cumulative.modifier_value_invalid"),
            Is.True);
        Assert.That(
            HasDiagnostic(validation, "cumulative.modifier_required"),
            Is.True,
            string.Join("\n", validation.Diagnostics));
    }

    [Test]
    public void CumulativeUpgrade_ExtremeReductionUsesRuntimeFloors()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "floor",
            1,
            (CharacterCumulativeUpgradeModifierType.MaximumHealth, -100f),
            (CharacterCumulativeUpgradeModifierType.AttackCooldown, -10f),
            (CharacterCumulativeUpgradeModifierType
                .SkillCostReduction, -5f));
        CharacterProgressData progress = new(
            definition.CharacterId,
            true);
        progress.SetCumulativeUpgradeLevel("floor", 1);

        CharacterData data = definition.CreateData(progress);

        Assert.That(data.MaximumHealth, Is.EqualTo(1));
        Assert.That(
            data.AttackCooldown,
            Is.EqualTo(TimePrecision.Step).Within(0.001f));
        Assert.That(data.ActiveSkillCost, Is.EqualTo(2));
    }

    [Test]
    public void CollectionUpgrade_RefreshesAllIndependentRuntimeData()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "shared",
            5,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 2f));
        CharacterCollectionData collection = new();
        CharacterData first = collection.CreateRuntimeData(definition);
        CharacterData second = collection.CreateRuntimeData(definition);
        int firstChanged = 0;
        int secondChanged = 0;
        int collectionChanged = 0;
        first.StatsChanged += () => firstChanged++;
        second.StatsChanged += () => secondChanged++;
        collection.CharacterProgressChanged += changed =>
        {
            if (ReferenceEquals(changed, definition))
                collectionChanged++;
        };

        CharacterCumulativeUpgradeChangeResult result =
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "shared",
                2,
                out int newLevel,
                save: false);

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(
            result,
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));
        Assert.That(newLevel, Is.EqualTo(2));
        Assert.That(first.AttackPower, Is.EqualTo(14f).Within(0.001f));
        Assert.That(second.AttackPower, Is.EqualTo(14f).Within(0.001f));
        Assert.That(firstChanged, Is.EqualTo(1));
        Assert.That(secondChanged, Is.EqualTo(1));
        Assert.That(collectionChanged, Is.EqualTo(1));
    }

    [Test]
    public void CollectionUpgrade_RejectsUnownedCharacter()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        SetCharacterInitiallyOwned(definition, false);
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "locked",
            2,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 1f));
        CharacterCollectionData collection = new();

        CharacterCumulativeUpgradeChangeResult result =
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "locked",
                1,
                out int newLevel,
                save: false);

        Assert.That(
            result,
            Is.EqualTo(
                CharacterCumulativeUpgradeChangeResult.CharacterNotOwned));
        Assert.That(newLevel, Is.Zero);
        Assert.That(
            collection.GetOrCreate(definition).GetCumulativeUpgradeLevel(
                "locked"),
            Is.Zero);
    }

    [Test]
    public void CollectionOwnershipChange_EnablesUpgrade()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        SetCharacterInitiallyOwned(definition, false);
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "unlock_then_upgrade",
            2,
            (CharacterCumulativeUpgradeModifierType.MaximumHealth, 3f));
        CharacterCollectionData collection = new();
        CharacterData data = collection.CreateRuntimeData(definition);
        int progressChanged = 0;
        collection.CharacterProgressChanged += _ => progressChanged++;

        Assert.That(data.IsOwned, Is.False);
        Assert.That(
            collection.TrySetOwned(
                definition,
                true,
                save: false),
            Is.True);
        CharacterCumulativeUpgradeChangeResult result =
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "unlock_then_upgrade",
                1,
                out int newLevel,
                save: false);

        Assert.That(data.IsOwned, Is.True);
        Assert.That(
            result,
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));
        Assert.That(newLevel, Is.EqualTo(1));
        Assert.That(data.MaximumHealth, Is.EqualTo(13));
        Assert.That(progressChanged, Is.EqualTo(2));
    }

    [Test]
    public void CollectionTrust_IsClampedAndPreservedBySaveSnapshot()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        CharacterCollectionData source = new();
        CharacterData runtime = source.CreateRuntimeData(definition);

        Assert.That(
            source.TrySetTrust(
                definition,
                135,
                save: false),
            Is.True);
        Assert.That(runtime.Trust, Is.EqualTo(100));
        Assert.That(
            source.CreatePreviewData(definition).Trust,
            Is.EqualTo(100));

        CharacterCollectionData restored = new();
        Assert.That(
            restored.TryImportJson(source.ExportJson()),
            Is.True);
        Assert.That(
            restored.CreatePreviewData(definition).Trust,
            Is.EqualTo(100));
    }

    [Test]
    public void CollectionUpgrade_RejectsInvalidAmountAndUnknownId()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "known",
            2,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 1f));
        CharacterCollectionData collection = new();

        CharacterCumulativeUpgradeChangeResult invalidAmount =
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "known",
                0,
                out int invalidLevel,
                save: false);
        CharacterCumulativeUpgradeChangeResult unknownId =
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "missing",
                1,
                out int unknownLevel,
                save: false);

        Assert.That(
            invalidAmount,
            Is.EqualTo(
                CharacterCumulativeUpgradeChangeResult.InvalidAmount));
        Assert.That(
            unknownId,
            Is.EqualTo(
                CharacterCumulativeUpgradeChangeResult.UpgradeNotFound));
        Assert.That(invalidLevel, Is.Zero);
        Assert.That(unknownLevel, Is.Zero);
    }

    [Test]
    public void CollectionUpgrade_ClampsFirstIncreaseThenReportsMaximum()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "limited",
            2,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 1f));
        CharacterCollectionData collection = new();

        CharacterCumulativeUpgradeChangeResult first =
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "limited",
                5,
                out int firstLevel,
                save: false);
        CharacterCumulativeUpgradeChangeResult second =
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "limited",
                1,
                out int secondLevel,
                save: false);

        Assert.That(
            first,
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));
        Assert.That(firstLevel, Is.EqualTo(2));
        Assert.That(
            second,
            Is.EqualTo(
                CharacterCumulativeUpgradeChangeResult.MaxLevelReached));
        Assert.That(secondLevel, Is.EqualTo(2));
    }

    [Test]
    public void CollectionImport_RebindsAllExistingRuntimeData()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "shared",
            5,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 2f));
        CharacterCollectionData collection = new();
        CharacterData first = collection.CreateRuntimeData(definition);
        CharacterData second = collection.CreateRuntimeData(definition);
        CharacterProgressData previousProgress = first.Progress;
        int firstChanged = 0;
        int secondChanged = 0;
        int collectionChanged = 0;
        first.StatsChanged += () => firstChanged++;
        second.StatsChanged += () => secondChanged++;
        collection.CharacterProgressChanged += changed =>
        {
            if (ReferenceEquals(changed, definition))
                collectionChanged++;
        };

        CharacterCollectionData source = new();
        Assert.That(
            source.TryAddCumulativeUpgradeLevel(
                definition,
                "shared",
                2,
                out _,
                save: false),
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));

        Assert.That(
            collection.TryImportJson(source.ExportJson()),
            Is.True);
        Assert.That(first.Progress, Is.Not.SameAs(previousProgress));
        Assert.That(second.Progress, Is.SameAs(first.Progress));
        Assert.That(first.AttackPower, Is.EqualTo(14f).Within(0.001f));
        Assert.That(second.AttackPower, Is.EqualTo(14f).Within(0.001f));
        Assert.That(firstChanged, Is.EqualTo(1));
        Assert.That(secondChanged, Is.EqualTo(1));
        Assert.That(collectionChanged, Is.EqualTo(1));
    }

    [Test]
    public void CollectionImport_NormalizesDuplicateSaveRecords()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "shared",
            5,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 2f));
        ConfigureCumulativeUpgradeDefinition(
            definition,
            1,
            "vitality",
            5,
            (CharacterCumulativeUpgradeModifierType.MaximumHealth, 1f));
        SetCharacterInitiallyOwned(definition, false);
        CharacterCollectionData collection = new();
        CharacterData data = collection.CreateRuntimeData(definition);
        string characterId = definition.CharacterId;
        string json =
            "{\"characters\":[" +
            "{\"characterId\":\" " + characterId +
            " \",\"isOwned\":false,\"cumulativeUpgrades\":[" +
            "{\"upgradeId\":\" shared \",\"level\":1}," +
            "{\"upgradeId\":\"shared\",\"level\":3}," +
            "{\"upgradeId\":\" \",\"level\":99}]}," +
            "{\"characterId\":\"" + characterId +
            "\",\"isOwned\":true,\"cumulativeUpgrades\":[" +
            "{\"upgradeId\":\"shared\",\"level\":2}," +
            "{\"upgradeId\":\"vitality\",\"level\":4}]}," +
            "{\"characterId\":\" \",\"isOwned\":true}]}";

        Assert.That(collection.TryImportJson(json), Is.True);

        Assert.That(collection.Characters, Has.Count.EqualTo(1));
        Assert.That(
            collection.Characters[0].CumulativeUpgrades,
            Has.Count.EqualTo(2));
        Assert.That(data.IsOwned, Is.True);
        Assert.That(data.GetCumulativeUpgradeLevel("shared"), Is.EqualTo(3));
        Assert.That(data.GetCumulativeUpgradeLevel("vitality"), Is.EqualTo(4));
        Assert.That(data.AttackPower, Is.EqualTo(16f).Within(0.001f));
        Assert.That(data.MaximumHealth, Is.EqualTo(14));
    }

    [Test]
    public void CollectionImport_InvalidEmptyJsonPreservesRuntimeState()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "stable",
            3,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 1f));
        CharacterCollectionData collection = new();
        CharacterData data = collection.CreateRuntimeData(definition);
        Assert.That(
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "stable",
                1,
                out _,
                save: false),
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));
        CharacterProgressData previousProgress = data.Progress;
        int statsChanged = 0;
        int collectionChanged = 0;
        data.StatsChanged += () => statsChanged++;
        collection.CharacterProgressChanged += _ => collectionChanged++;

        Assert.That(collection.TryImportJson("   "), Is.False);
        Assert.That(collection.TryImportJson("{}"), Is.False);

        Assert.That(data.Progress, Is.SameAs(previousProgress));
        Assert.That(data.GetCumulativeUpgradeLevel("stable"), Is.EqualTo(1));
        Assert.That(data.AttackPower, Is.EqualTo(11f).Within(0.001f));
        Assert.That(statsChanged, Is.Zero);
        Assert.That(collectionChanged, Is.Zero);
    }

    [Test]
    public void CollectionImport_EmptySaveRebindsRuntimeToDefaults()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "reset",
            3,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 1f));
        CharacterCollectionData collection = new();
        CharacterData data = collection.CreateRuntimeData(definition);
        Assert.That(
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "reset",
                2,
                out _,
                save: false),
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));
        CharacterProgressData previousProgress = data.Progress;
        int statsChanged = 0;
        data.StatsChanged += () => statsChanged++;

        Assert.That(
            collection.TryImportJson(
                "{\"version\":1,\"characters\":[]}"),
            Is.True);

        Assert.That(data.Progress, Is.Not.SameAs(previousProgress));
        Assert.That(data.IsOwned, Is.True);
        Assert.That(data.GetCumulativeUpgradeLevel("reset"), Is.Zero);
        Assert.That(data.AttackPower, Is.EqualTo(10f).Within(0.001f));
        Assert.That(statsChanged, Is.EqualTo(1));
    }

    [Test]
    public void CollectionPreviewData_IsDetachedCurrentProgressSnapshot()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        ConfigureCumulativeUpgradeDefinition(
            definition,
            0,
            "preview",
            5,
            (CharacterCumulativeUpgradeModifierType.AttackPower, 2f));
        CharacterCollectionData collection = new();
        Assert.That(
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "preview",
                2,
                out _,
                save: false),
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));
        CharacterProgressData savedProgress =
            collection.GetOrCreate(definition);

        CharacterData preview = collection.CreatePreviewData(definition);

        Assert.That(preview.Progress, Is.Not.SameAs(savedProgress));
        Assert.That(preview.GetCumulativeUpgradeLevel("preview"), Is.EqualTo(2));
        Assert.That(preview.AttackPower, Is.EqualTo(14f).Within(0.001f));

        Assert.That(
            collection.TryAddCumulativeUpgradeLevel(
                definition,
                "preview",
                1,
                out _,
                save: false),
            Is.EqualTo(CharacterCumulativeUpgradeChangeResult.Success));
        Assert.That(preview.GetCumulativeUpgradeLevel("preview"), Is.EqualTo(2));
        Assert.That(preview.AttackPower, Is.EqualTo(14f).Within(0.001f));

        CharacterData refreshed =
            collection.CreatePreviewData(definition);
        Assert.That(
            refreshed.GetCumulativeUpgradeLevel("preview"),
            Is.EqualTo(3));
        Assert.That(refreshed.AttackPower, Is.EqualTo(16f).Within(0.001f));
    }

    [Test]
    public void DataManager_InitializesBeforeCharacterRuntime()
    {
        DefaultExecutionOrder executionOrder =
            (DefaultExecutionOrder)Attribute.GetCustomAttribute(
                typeof(DataManager),
                typeof(DefaultExecutionOrder));

        Assert.That(executionOrder, Is.Not.Null);
        Assert.That(executionOrder.order, Is.LessThan(0));
    }

    [Test]
    public void DefaultCharacterDataFactories_HonorInitialOwnership()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        SetCharacterInitiallyOwned(definition, true);
        CharacterCollectionData collection = new();

        CharacterData standalone = definition.CreateData();
        CharacterProgressData saved = collection.GetOrCreate(definition);

        Assert.That(standalone.IsOwned, Is.True);
        Assert.That(saved.IsOwned, Is.True);
    }

    [Test]
    public void DungeonCharacterAvailability_TracksCollectionOwnership()
    {
        CharacterSO definition = CreateCumulativeUpgradeCharacter();
        SetCharacterInitiallyOwned(definition, false);
        CharacterCollectionData collection = new();
        MethodInfo ownershipCheck = typeof(DungeonPage).GetMethod(
            "IsCharacterOwnedForDungeon",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(ownershipCheck, Is.Not.Null);
        Assert.That(
            (bool)ownershipCheck.Invoke(
                null,
                new object[] { definition, collection }),
            Is.False);
        Assert.That(
            collection.TrySetOwned(
                definition,
                true,
                save: false),
            Is.True);
        Assert.That(
            (bool)ownershipCheck.Invoke(
                null,
                new object[] { definition, collection }),
            Is.True);

        SetCharacterInitiallyOwned(definition, true);
        CharacterCollectionData freshCollection = new();
        Assert.That(
            (bool)ownershipCheck.Invoke(
                null,
                new object[] { definition, freshCollection }),
            Is.True);
    }

    [Test]
    public void DungeonCharacterRewards_ExcludeEveryCharacterAcquiredThisRun()
    {
        List<CharacterSO> definitions = new();
        CharacterRuntime[] slots =
            new CharacterRuntime[DungeonPage.MaximumPartySize];
        for (int index = 0; index < DungeonPage.MaximumPartySize + 1;
             index++)
        {
            CharacterSO definition = CreateCumulativeUpgradeCharacter();
            definition.name = $"RewardCharacter_{index + 1}";
            SetCharacterInitiallyOwned(definition, true);
            definitions.Add(definition);
            if (index < slots.Length)
                slots[index] = CreateCharacter(definition);
        }

        GameObject pageObject = new(
            "DungeonCharacterRewardTest",
            typeof(RectTransform));
        pageObject.SetActive(false);
        _createdObjects.Add(pageObject);
        DungeonPage page = pageObject.AddComponent<DungeonPage>();
        FieldInfo playerCharactersField = typeof(DungeonPage).GetField(
            "playerCharacters",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo availableTurretsField = typeof(DungeonPage).GetField(
            "_availableTurrets",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo battleRewardPendingField = typeof(DungeonPage).GetField(
            "_battleRewardPending",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(playerCharactersField, Is.Not.Null);
        Assert.That(availableTurretsField, Is.Not.Null);
        Assert.That(battleRewardPendingField, Is.Not.Null);
        playerCharactersField.SetValue(page, slots);
        List<CharacterSO> available =
            (List<CharacterSO>)availableTurretsField.GetValue(page);
        available.AddRange(definitions);

        for (int index = 0; index < DungeonPage.MaximumPartySize; index++)
        {
            battleRewardPendingField.SetValue(page, true);
            Assert.That(
                page.TryAcquireTurret(definitions[index]),
                Is.True);
        }

        IReadOnlyList<CharacterSO> beforeReplacement =
            page.GetAvailableCharacterRewardDefinitions();
        Assert.That(beforeReplacement.Count, Is.EqualTo(1));
        Assert.That(beforeReplacement[0], Is.SameAs(definitions[4]));

        battleRewardPendingField.SetValue(page, true);
        Assert.That(
            page.TryAcquireTurret(definitions[4], 0),
            Is.True);
        Assert.That(
            page.GetAvailableCharacterRewardDefinitions(),
            Is.Empty);

        battleRewardPendingField.SetValue(page, true);
        Assert.That(
            page.TryAcquireTurret(definitions[0], 0),
            Is.False);
        Assert.That(
            page.OwnedTurrets[0].Definition,
            Is.SameAs(definitions[4]));
    }

    [Test]
    public void ExplicitDamageAndStatusSkill_AppliesBothOnce_AndPaysOnce()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        CharacterSO definition = CreateExplicitDamageAndStatusCharacter(fire);
        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join(
                "\n",
                validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(
            CharacterLocalization.GetActiveSkillDescription(character.Data),
            Does.Contain(" + "));
        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        character.BindBattle(resource, board);

        bool activated = character.TryActivateActiveSkill();

        Assert.That(activated, Is.True);
        Assert.That(resource.Current, Is.EqualTo(8));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(character.TotalDamageDealt, Is.EqualTo(4));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(board.StatusTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(1));
        Assert.That(board.StatusApplyCallCount, Is.EqualTo(1));
        Assert.That(board.AppliedStatuses, Is.EqualTo(new[] { fire }));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(board.StatusTargetSnapshots[0]));
        Assert.That(board.DamageTargetSnapshots[0], Does.Contain(target));
    }

    [Test]
    public void RawStatBoosts_DoNotPublishStatusAppliedEvents()
    {
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        FakeBattleBoard board = new();
        int statusAppliedEventCount = 0;
        board.StatusApplied += _ => statusAppliedEventCount++;
        character.BindBattle(null, board);

        Assert.That(
            character.ApplyAttackSpeedBoost(1.5f, 3f),
            Is.True);
        Assert.That(
            character.ApplyPowerBoost(2f, 3f),
            Is.True);

        Assert.That(statusAppliedEventCount, Is.Zero);
    }

    [Test]
    public void PreviousTargetSkill_BeforeBasicAttack_UsesAttackSelector()
    {
        CharacterRuntime aisling = CreateCharacter(
            CreateAislingFeatureFixture());
        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
            ReturnCenterTargetsForAreaExpansion = true,
        };
        aisling.BindBattle(resource, board);

        bool activated = aisling.TryActivateActiveSkill();

        Assert.That(activated, Is.True);
        Assert.That(
            resource.Current,
            Is.EqualTo(10 - aisling.Data.ActiveSkillCost));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(1));
        Assert.That(
            board.CharacterTargetSelectionSubjects,
            Is.EqualTo(new[] { CharacterAttackSubject.LowestValue }));
        Assert.That(
            board.CharacterTargetSelectionMetrics,
            Is.EqualTo(new[] { CharacterAttackSubjectMetric.Health }));
        Assert.That(board.CharacterTargetSelectionCounts, Is.EqualTo(
            new[] { 1 }));
        Assert.That(board.FilterCharacterTargetCallCount, Is.Zero);
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(board.DamageTargetSnapshots[0], Does.Contain(target));
    }

    [Test]
    public void PreviousTargetSkill_InvalidPreviousTarget_ReselectsTarget()
    {
        CharacterRuntime aisling = CreateCharacter(
            CreateAislingFeatureFixture());
        EnemyRuntime previousTarget = CreateEnemyRuntime();
        EnemyRuntime replacementTarget = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { previousTarget },
            ReturnCenterTargetsForAreaExpansion = true,
        };
        aisling.BindBattle(resource, board);
        aisling.TickBattle(aisling.Data.AttackCooldown, board);
        board.InvalidEnemyTargets.Add(previousTarget);
        board.SelectedEnemyTargets = new[] { replacementTarget };

        bool activated = aisling.TryActivateActiveSkill();

        Assert.That(activated, Is.True);
        Assert.That(
            resource.Current,
            Is.EqualTo(10 - aisling.Data.ActiveSkillCost));
        Assert.That(resource.TrySpendCallCount, Is.EqualTo(1));
        Assert.That(board.FilterCharacterTargetCallCount, Is.EqualTo(1));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(
            board.DamageTargetSnapshots[1],
            Does.Contain(replacementTarget));
        Assert.That(
            board.DamageTargetSnapshots[1],
            Has.No.Member(previousTarget));
    }

    [Test]
    public void PreviousTargetSkill_NoReplacement_DoesNotSpendResource()
    {
        CharacterRuntime aisling = CreateCharacter(
            CreateAislingFeatureFixture());
        EnemyRuntime previousTarget = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { previousTarget },
            ReturnCenterTargetsForAreaExpansion = true,
        };
        aisling.BindBattle(resource, board);
        aisling.TickBattle(aisling.Data.AttackCooldown, board);
        board.InvalidEnemyTargets.Add(previousTarget);
        board.LivingEnemyCountValue = 0;
        board.SelectedEnemyTargets = Array.Empty<EnemyRuntime>();

        bool activated = aisling.TryActivateActiveSkill();

        Assert.That(activated, Is.False);
        Assert.That(resource.Current, Is.EqualTo(10));
        Assert.That(resource.TrySpendCallCount, Is.Zero);
        Assert.That(board.FilterCharacterTargetCallCount, Is.EqualTo(1));
        Assert.That(board.CharacterTargetSelectionCallCount, Is.EqualTo(2));
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));
    }

    [Test]
    public void PreviousTargetSkill_ReusesBasicAttackTarget_AndAppliesStatus()
    {
        CharacterRuntime aisling = CreateCharacter(
            CreateAislingFeatureFixture());
        StatusEffectSO opening =
            LoadAsset<StatusEffectSO>(OpeningAssetPath);
        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
            ReturnCenterTargetsForAreaExpansion = true,
        };
        aisling.BindBattle(resource, board);
        aisling.TickBattle(aisling.Data.AttackCooldown, board);

        Assert.That(
            board.DamageTargetSnapshots,
            Has.Count.EqualTo(1),
            "A normal attack must establish the reusable skill target.");
        Assert.That(
            board.DamageTargetSnapshots[0],
            Does.Contain(target));

        bool activated = aisling.TryActivateActiveSkill();

        Assert.That(activated, Is.True);
        Assert.That(
            resource.Current,
            Is.EqualTo(10 - aisling.Data.ActiveSkillCost));
        Assert.That(
            board.DamageTargetSnapshots,
            Has.Count.EqualTo(2),
            "The active skill must damage the target saved by the normal " +
            "attack.");
        Assert.That(
            board.DamageTargetSnapshots[1],
            Does.Contain(target));
        Assert.That(
            board.CharacterTargetSelectionCallCount,
            Is.EqualTo(1),
            "Subject None must reuse the normal-attack target without " +
            "selecting a new target.");
        Assert.That(
            board.FilterCharacterTargetCallCount,
            Is.EqualTo(1),
            "The inherited target must be validated against the current " +
            "board even when the skill has no numeric conditions.");
        Assert.That(board.StatusApplyCallCount, Is.EqualTo(1));
        Assert.That(board.AppliedStatuses, Does.Contain(opening));
        Assert.That(
            board.StatusTargetSnapshots[0],
            Does.Contain(target),
            "The active skill must apply its configured status to the " +
            "reused target.");
    }

    [Test]
    public void EnemyStatus_NonFirePeriodicDamage_ResolvesValueModesAndStacks()
    {
        StatusEffectSO status = CreateEnemyPeriodicDamageStatus(
            "test_periodic_damage",
            StatusEffectStackRemovalOrder.Oldest,
            2f,
            true,
            0.1f,
            false);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        int totalDamage = 0;
        List<IBattleCharacter> damageSources = new();

        Assert.That(status.StatusId, Is.Not.EqualTo(StatusEffectIds.Fire));
        Assert.That(
            ApplyEnemyStatus(enemy, status, 1f, 3, source, 1f),
            Is.True);

        bool changed = TickEnemyStatuses(
            enemy,
            1f,
            (damage, appliedSource) =>
            {
                totalDamage += damage;
                damageSources.Add(appliedSource);
                return true;
            });

        Assert.That(changed, Is.True);
        Assert.That(
            totalDamage,
            Is.EqualTo(8),
            "Fixed 2 x 3 stacks plus 10% of 20 max health must deal 8.");
        Assert.That(damageSources, Is.All.SameAs(source));
        Assert.That(HasEnemyStatus(enemy, status), Is.False);
    }

    [Test]
    public void EnemyStatus_IndependentDuration_UsesActiveBatchAndLastTick()
    {
        StatusEffectSO status = CreateEnemyPeriodicDamageStatus(
            "test_independent_duration",
            StatusEffectStackRemovalOrder.Oldest);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime firstSource = CreateCharacter(SuirenAssetPath);
        CharacterRuntime secondSource = CreateCharacter(AislingAssetPath);

        Assert.That(
            ApplyEnemyStatus(enemy, status, 2f, 2, firstSource, 1f),
            Is.True);
        Assert.That(
            ApplyEnemyStatus(enemy, status, 1f, 1, secondSource, 1f),
            Is.True);
        Assert.That(GetEnemyStatusStacks(enemy, status), Is.EqualTo(3));
        Assert.That(
            GetEnemyStatusRemainingDuration(enemy, status),
            Is.EqualTo(3f).Within(0.001f));

        int firstSourceDamage = 0;
        int secondSourceDamage = 0;
        bool changed = TickEnemyStatuses(
            enemy,
            3f,
            (damage, appliedSource) =>
            {
                if (ReferenceEquals(appliedSource, firstSource))
                    firstSourceDamage += damage;
                if (ReferenceEquals(appliedSource, secondSource))
                    secondSourceDamage += damage;
                return true;
            });

        Assert.That(changed, Is.True);
        Assert.That(firstSourceDamage, Is.EqualTo(4));
        Assert.That(
            secondSourceDamage,
            Is.EqualTo(1),
            "The final boundary tick must execute before the last batch " +
            "expires.");
        Assert.That(HasEnemyStatus(enemy, status), Is.False);
        Assert.That(GetEnemyStatusStacks(enemy, status), Is.Zero);
        Assert.That(GetEnemyStatusRemainingDuration(enemy, status), Is.Zero);
    }

    [Test]
    public void EnemyStatus_OldestRemoval_RemovesOldestIndependentBatch()
    {
        StatusEffectSO status = CreateEnemyPeriodicDamageStatus(
            "test_oldest_removal",
            StatusEffectStackRemovalOrder.Oldest);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime firstSource = CreateCharacter(SuirenAssetPath);
        CharacterRuntime secondSource = CreateCharacter(AislingAssetPath);

        Assert.That(
            ApplyEnemyStatus(enemy, status, 2f, 2, firstSource, 1f),
            Is.True);
        Assert.That(
            ApplyEnemyStatus(enemy, status, 1f, 1, secondSource, 1f),
            Is.True);

        int removed = RemoveEnemyStatus(
            enemy,
            CharacterStatusRemovalTarget.Single,
            status,
            2);

        Assert.That(removed, Is.EqualTo(2));
        Assert.That(GetEnemyStatusStacks(enemy, status), Is.EqualTo(1));
        Assert.That(
            GetEnemyStatusRemainingDuration(enemy, status),
            Is.EqualTo(1f).Within(0.001f));

        int firstSourceDamage = 0;
        int secondSourceDamage = 0;
        TickEnemyStatuses(
            enemy,
            1f,
            (damage, appliedSource) =>
            {
                if (ReferenceEquals(appliedSource, firstSource))
                    firstSourceDamage += damage;
                if (ReferenceEquals(appliedSource, secondSource))
                    secondSourceDamage += damage;
                return true;
            });

        Assert.That(firstSourceDamage, Is.Zero);
        Assert.That(secondSourceDamage, Is.EqualTo(1));
        Assert.That(HasEnemyStatus(enemy, status), Is.False);
    }

    [Test]
    public void FireCompatibilityWrapper_PreservesStacksDurationAndDamage()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        ApplyFire(enemy, 2f, 1f, 2, source);

        Assert.That(enemy.HasFire, Is.True);
        Assert.That(enemy.FireStackCount, Is.EqualTo(2));
        Assert.That(enemy.FireRemainingDuration, Is.EqualTo(2f));

        int totalDamage = 0;
        List<IBattleCharacter> damageSources = new();
        bool changed = TickEnemyStatuses(
            enemy,
            2f,
            (damage, appliedSource) =>
            {
                totalDamage += damage;
                damageSources.Add(appliedSource);
                return true;
            });

        Assert.That(changed, Is.True);
        Assert.That(totalDamage, Is.EqualTo(4));
        Assert.That(damageSources, Is.All.SameAs(source));
        Assert.That(enemy.HasFire, Is.False);
        Assert.That(enemy.FireStackCount, Is.Zero);
        Assert.That(enemy.FireRemainingDuration, Is.Zero);
    }

    [Test]
    public void MigratedStatusAssets_UseModularDefinitions()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);

        Assert.That(fire.Operations, Is.Empty);
        Assert.That(fire.TriggerBlocks.Count, Is.EqualTo(1));
        StatusEffectTriggerBlockDefinition fireTick =
            fire.TriggerBlocks[0];
        Assert.That(
            fireTick.Trigger,
            Is.EqualTo(StatusEffectLifecycleTrigger.OnTick));
        Assert.That(fireTick.ScaleWithCurrentStacks, Is.True);
        Assert.That(fireTick.ScaleWithOccurrences, Is.True);
        Assert.That(fireTick.Effects.Count, Is.EqualTo(1));
        Assert.That(
            fireTick.Effects[0].Type,
            Is.EqualTo(CharacterEffectType.Damage));
        Assert.That(
            fireTick.Effects[0].DamageType,
            Is.EqualTo(CharacterAttackDamageType.Fixed));
        Assert.That(
            fireTick.Effects[0].DamageAmountMode,
            Is.EqualTo(CharacterDamageAmountMode.Fixed));
        Assert.That(fireTick.Effects[0].DamageAmount, Is.EqualTo(1f));

        Assert.That(stun.Operations, Is.Empty);
        Assert.That(stun.ControlEffects.Count, Is.EqualTo(1));
        Assert.That(
            stun.ControlEffects[0].ControlType,
            Is.EqualTo(StatusEffectControlType.DisableAllActions));

        Assert.That(emergencyKit.Operations, Is.Empty);
        Assert.That(emergencyKit.TriggerBlocks, Is.Empty);
        Assert.That(emergencyKit.StatModifiers, Is.Empty);
        Assert.That(emergencyKit.ControlEffects, Is.Empty);
    }

    [Test]
    public void OpeningStatus_IncreasesEnemyIncomingDamageByTenPercent()
    {
        StatusEffectSO opening =
            LoadAsset<StatusEffectSO>(OpeningAssetPath);

        Assert.That(opening.StatusId, Is.EqualTo(StatusEffectIds.Opening));
        Assert.That(
            opening.Alignment,
            Is.EqualTo(StatusEffectAlignment.Debuff));
        Assert.That(opening.CanTargetEnemy, Is.True);
        Assert.That(opening.CanTargetAlly, Is.False);
        Assert.That(opening.DefaultDuration, Is.EqualTo(5f));
        Assert.That(
            opening.StackMode,
            Is.EqualTo(StatusEffectStackMode.Replace));
        Assert.That(opening.MaximumStacks, Is.EqualTo(1));
        Assert.That(opening.StatModifiers, Has.Count.EqualTo(1));

        StatusEffectStatModifierDefinition modifier =
            opening.StatModifiers[0];
        Assert.That(
            modifier.StatType,
            Is.EqualTo(StatusEffectStatType.IncomingDamage));
        Assert.That(
            modifier.Mode,
            Is.EqualTo(StatusEffectStatModifierMode.AdditiveRatio));
        Assert.That(modifier.Value, Is.EqualTo(0.1f));
        Assert.That(modifier.ScaleWithStacks, Is.False);

        CharacterAttackDamageType[] damageTypes =
        {
            CharacterAttackDamageType.Physical,
            CharacterAttackDamageType.Magical,
            CharacterAttackDamageType.Fixed,
        };
        foreach (CharacterAttackDamageType damageType in damageTypes)
        {
            EnemyRuntime enemy = CreateEnemyRuntime(100);
            Assert.That(
                ApplyEnemyStatus(
                    enemy,
                    opening,
                    5f,
                    1,
                    null,
                    opening.TickInterval),
                Is.True);
            Assert.That(
                TakeEnemyDamage(enemy, 10, damageType),
                Is.EqualTo(11),
                damageType.ToString());
        }

        EnemyRuntime expired = CreateEnemyRuntime(100);
        Assert.That(
            ApplyEnemyStatus(
                expired,
                opening,
                1f,
                1,
                null,
                opening.TickInterval),
            Is.True);
        Assert.That(
            TickEnemyStatuses(expired, 1f, null),
            Is.True);
        Assert.That(
            TakeEnemyDamage(
                expired,
                10,
                CharacterAttackDamageType.Physical),
            Is.EqualTo(10));
    }

    [Test]
    public void MigratedFireTick_UsesSharedBoardEffectExecutor()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        FakeBattleBoard board = new();
        source.BindBattle(null, board);
        ApplyFire(enemy, 1f, 1f, 2, source);
        int fallbackCalls = 0;

        bool changed = TickEnemyStatuses(
            enemy,
            1f,
            (_, _) =>
            {
                fallbackCalls++;
                return true;
            });

        Assert.That(changed, Is.True);
        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 2 }));
        Assert.That(fallbackCalls, Is.Zero);
        Assert.That(enemy.HasFire, Is.False);
    }

    [Test]
    public void AlliedOnApplyTrigger_ExecutesSharedHealEffect()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_on_apply_shared_heal",
            false,
            true,
            StatusEffectStackMode.Replace,
            0);
        SerializedObject serializedStatus = new(status);
        SerializedProperty blocks =
            serializedStatus.FindProperty("triggerBlocks");
        MethodInfo addTriggerBlock =
            typeof(StatusEffectEditorWindow).GetMethod(
                "AddTriggerBlock",
                BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(addTriggerBlock, Is.Not.Null);
        addTriggerBlock.Invoke(null, new object[] { blocks });
        SerializedProperty effect = blocks
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Heal;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.InheritAction;
        effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        effect.FindPropertyRelative("damageAmount").floatValue = 3f;
        serializedStatus.ApplyModifiedPropertiesWithoutUndo();
        status.ValidateDefinition();

        CharacterRuntime character = CreateCharacter(AislingAssetPath);
        FakeBattleBoard board = new();
        character.BindBattle(null, board);
        Assert.That(character.TrySpendHealth(5), Is.True);
        int damagedHealth = character.CurrentHealth;

        Assert.That(
            character.ApplyStatusEffect(
                status,
                2f,
                1,
                character),
            Is.True);
        Assert.That(
            character.CurrentHealth,
            Is.EqualTo(damagedHealth + 3));
    }

    [Test]
    public void EnemyStatus_LifecycleDamage_HasOrderedExclusiveRemovalPaths()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_lifecycle_damage",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            4);
        ConfigureRuntimeStatusOperation(
            status,
            0,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.InstantDamage,
            StatusEffectValueMode.Fixed,
            1f,
            false);
        ConfigureRuntimeStatusOperation(
            status,
            1,
            StatusEffectOperationTrigger.OnStackChanged,
            StatusEffectOperationType.InstantDamage,
            StatusEffectValueMode.Fixed,
            2f,
            false);
        ConfigureRuntimeStatusOperation(
            status,
            2,
            StatusEffectOperationTrigger.OnRemove,
            StatusEffectOperationType.InstantDamage,
            StatusEffectValueMode.Fixed,
            3f,
            false);
        ConfigureRuntimeStatusOperation(
            status,
            3,
            StatusEffectOperationTrigger.OnExpire,
            StatusEffectOperationType.InstantDamage,
            StatusEffectValueMode.Fixed,
            4f,
            false);

        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        EnemyRuntime manuallyRemoved = CreateEnemyRuntime();
        List<int> manualEvents = new();
        Func<int, IBattleCharacter, bool> recordManual =
            (damage, appliedSource) =>
            {
                Assert.That(appliedSource, Is.SameAs(source));
                manualEvents.Add(damage);
                return true;
            };

        Assert.That(
            ApplyEnemyStatus(
                manuallyRemoved,
                status,
                1f,
                1,
                source,
                1f,
                recordManual),
            Is.True);
        Assert.That(
            RemoveEnemyStatus(
                manuallyRemoved,
                CharacterStatusRemovalTarget.Single,
                status,
                1,
                recordManual),
            Is.EqualTo(1));
        TickEnemyStatuses(manuallyRemoved, 2f, recordManual);

        Assert.That(
            manualEvents,
            Is.EqualTo(new[] { 1, 2, 2, 3 }),
            "Manual removal must execute OnStackChanged then OnRemove, " +
            "without a later OnExpire.");

        EnemyRuntime naturallyExpired = CreateEnemyRuntime();
        List<int> expirationEvents = new();
        Func<int, IBattleCharacter, bool> recordExpiration =
            (damage, appliedSource) =>
            {
                Assert.That(appliedSource, Is.SameAs(source));
                expirationEvents.Add(damage);
                return true;
            };

        Assert.That(
            ApplyEnemyStatus(
                naturallyExpired,
                status,
                1f,
                1,
                source,
                1f,
                recordExpiration),
            Is.True);
        Assert.That(
            TickEnemyStatuses(
                naturallyExpired,
                1f,
                recordExpiration),
            Is.True);
        Assert.That(
            RemoveEnemyStatus(
                naturallyExpired,
                CharacterStatusRemovalTarget.Single,
                status,
                1,
                recordExpiration),
            Is.Zero);

        Assert.That(
            expirationEvents,
            Is.EqualTo(new[] { 1, 2, 2, 4 }),
            "Natural expiration must execute OnStackChanged then OnExpire, " +
            "without a later OnRemove.");
    }

    [Test]
    public void EnemyStatus_LifecycleDamage_StopsWhenCallbackRejectsTarget()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_lifecycle_callback_stop",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            2);
        for (int index = 0; index < 2; index++)
        {
            ConfigureRuntimeStatusOperation(
                status,
                index,
                StatusEffectOperationTrigger.OnApply,
                StatusEffectOperationType.InstantDamage,
                StatusEffectValueMode.Fixed,
                1f,
                false);
        }

        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        EnemyRuntime originalTarget = CreateEnemyRuntime();
        EnemyRuntime replacementTarget = CreateEnemyRuntime();
        EnemyRuntime currentTarget = originalTarget;
        List<EnemyRuntime> callbackTargets = new();

        bool applied = ApplyEnemyStatus(
            originalTarget,
            status,
            3f,
            1,
            source,
            1f,
            (damage, appliedSource) =>
            {
                Assert.That(damage, Is.EqualTo(1));
                Assert.That(appliedSource, Is.SameAs(source));
                callbackTargets.Add(currentTarget);
                currentTarget = replacementTarget;
                return false;
            });

        Assert.That(applied, Is.True);
        Assert.That(
            callbackTargets,
            Is.EqualTo(new[] { originalTarget }),
            "After the callback reports a defeated/replaced target, " +
            "remaining lifecycle damage must not spill to the new target.");
        Assert.That(HasEnemyStatus(originalTarget, status), Is.True);
        Assert.That(HasEnemyStatus(replacementTarget, status), Is.False);
    }

    [Test]
    public void AlliedStatus_ModifiersRestoreWithoutDriftAfterRemoveAndExpire()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_ally_modifiers",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            4);
        ConfigureRuntimeStatusOperation(
            status,
            0,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackPowerModifier,
            StatusEffectValueMode.Fixed,
            2f,
            true);
        ConfigureRuntimeStatusOperation(
            status,
            1,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackPowerModifier,
            StatusEffectValueMode.Ratio,
            0.5f,
            false);
        ConfigureRuntimeStatusOperation(
            status,
            2,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackSpeedModifier,
            StatusEffectValueMode.Fixed,
            0.1f,
            true);
        ConfigureRuntimeStatusOperation(
            status,
            3,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackSpeedModifier,
            StatusEffectValueMode.Ratio,
            0.5f,
            false);

        CharacterRuntime character = CreateCharacter(AislingAssetPath);
        FakeBattleBoard board = new();
        float basePower = character.CurrentAttackPower;
        float baseSpeed = character.CurrentAttackSpeed;
        float stackedPower = basePower + 4f + basePower * 0.5f;
        float stackedSpeed = baseSpeed + 0.2f + baseSpeed * 0.5f;
        float partialPower = basePower + 2f + basePower * 0.5f;
        float partialSpeed = baseSpeed + 0.1f + baseSpeed * 0.5f;

        Assert.That(character.ApplyStatusEffect(status, 1f, 2), Is.True);
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(stackedPower).Within(0.0001f));
        Assert.That(
            character.CurrentAttackSpeed,
            Is.EqualTo(stackedSpeed).Within(0.0001f));

        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                status,
                1),
            Is.EqualTo(1));
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(partialPower).Within(0.0001f));
        Assert.That(
            character.CurrentAttackSpeed,
            Is.EqualTo(partialSpeed).Within(0.0001f));

        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                status,
                1),
            Is.EqualTo(1));
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(basePower).Within(0.0001f));
        Assert.That(
            character.CurrentAttackSpeed,
            Is.EqualTo(baseSpeed).Within(0.0001f));

        for (int cycle = 0; cycle < 3; cycle++)
        {
            Assert.That(
                character.ApplyStatusEffect(status, 1f, 2),
                Is.True);
            Assert.That(
                character.CurrentAttackPower,
                Is.EqualTo(stackedPower).Within(0.0001f));
            Assert.That(
                character.CurrentAttackSpeed,
                Is.EqualTo(stackedSpeed).Within(0.0001f));

            character.TickBattle(1f, board);

            Assert.That(character.HasStatusEffect(status), Is.False);
            Assert.That(
                character.CurrentAttackPower,
                Is.EqualTo(basePower).Within(0.0001f));
            Assert.That(
                character.CurrentAttackSpeed,
                Is.EqualTo(baseSpeed).Within(0.0001f));
        }
    }

    [Test]
    public void ModularStatusModifiers_UseStableLayersAndRestoreOnRemoval()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_modular_stat_modifiers",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureRuntimeStatusModifier(
            status,
            0,
            StatusEffectStatType.AttackPower,
            StatusEffectStatModifierMode.Flat,
            2f,
            true);
        ConfigureRuntimeStatusModifier(
            status,
            1,
            StatusEffectStatType.AttackPower,
            StatusEffectStatModifierMode.AdditiveRatio,
            0.5f,
            false);
        ConfigureRuntimeStatusModifier(
            status,
            2,
            StatusEffectStatType.AttackPower,
            StatusEffectStatModifierMode.MultiplicativeRatio,
            0.1f,
            true);
        ConfigureRuntimeStatusModifier(
            status,
            3,
            StatusEffectStatType.AttackSpeed,
            StatusEffectStatModifierMode.Flat,
            0.1f,
            true);
        ConfigureRuntimeStatusModifier(
            status,
            4,
            StatusEffectStatType.AttackSpeed,
            StatusEffectStatModifierMode.MultiplicativeRatio,
            0.2f,
            false);

        CharacterRuntime character = CreateCharacter(AislingAssetPath);
        float basePower = character.CurrentAttackPower;
        float baseSpeed = character.CurrentAttackSpeed;
        float stackedPower =
            (basePower + 4f + basePower * 0.5f) * 1.21f;
        float stackedSpeed = (baseSpeed + 0.2f) * 1.2f;
        float partialPower =
            (basePower + 2f + basePower * 0.5f) * 1.1f;
        float partialSpeed = (baseSpeed + 0.1f) * 1.2f;

        Assert.That(character.ApplyStatusEffect(status, 2f, 2), Is.True);
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(stackedPower).Within(0.0001f));
        Assert.That(
            character.CurrentAttackSpeed,
            Is.EqualTo(stackedSpeed).Within(0.0001f));

        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                status,
                1),
            Is.EqualTo(1));
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(partialPower).Within(0.0001f));
        Assert.That(
            character.CurrentAttackSpeed,
            Is.EqualTo(partialSpeed).Within(0.0001f));

        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                status,
                1),
            Is.EqualTo(1));
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(basePower).Within(0.0001f));
        Assert.That(
            character.CurrentAttackSpeed,
            Is.EqualTo(baseSpeed).Within(0.0001f));
    }

    [Test]
    public void PassiveStatusContributionMultiplier_ScalesCommonBuffStat()
    {
        StatusEffectSO power = CreateRuntimeStatus(
            "test_common_power",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureRuntimeStatusModifier(
            power,
            0,
            StatusEffectStatType.AttackPower,
            StatusEffectStatModifierMode.Flat,
            1f,
            true);

        CharacterSO definition = CreateBaseCharacterFixture(
            "PassiveStatusContributionFixture",
            10f);
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.StatusContribution);
        passive.FindPropertyRelative("effects").ClearArray();
        ConfigureStatusContributionMultiplier(
            passive,
            0,
            power,
            StatusEffectStatType.AttackPower,
            1.5f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(character.CurrentAttackPower, Is.EqualTo(10f));
        Assert.That(
            character.ApplyStatusEffect(power, 5f, 2),
            Is.True);
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(13f).Within(0.0001f));
        Assert.That(
            CharacterLocalization.GetPassiveDescription(character.Data),
            Does.Contain("1.5"));
    }

    [Test]
    public void PassiveStatusContributionMultiplier_ScalesWithDungeonProgress()
    {
        StatusEffectSO power = CreateRuntimeStatus(
            "test_dungeon_progress_power",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureRuntimeStatusModifier(
            power,
            0,
            StatusEffectStatType.AttackPower,
            StatusEffectStatModifierMode.Flat,
            1f,
            true);

        CharacterSO definition = CreateBaseCharacterFixture(
            "DungeonProgressContributionFixture",
            10f);
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.StatusContribution);
        passive.FindPropertyRelative("effects").ClearArray();
        ConfigureStatusContributionMultiplier(
            passive,
            0,
            power,
            StatusEffectStatType.AttackPower,
            1f,
            2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeBattleBoard board = new()
        {
            DungeonStageProgress = 2f,
        };
        character.BindBattle(null, board);
        Assert.That(
            character.ApplyStatusEffect(power, 5f, 2),
            Is.True);
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(20f).Within(0.0001f),
            "10 base + (2 stacks × (1 fixed + 2 stages × 2)).");
        Assert.That(
            CharacterLocalization.GetPassiveDescription(character.Data),
            Does.Contain("2"));
    }

    [Test]
    public void PassiveStatModifier_AddsAttackPowerPerCompletedStage()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "DungeonStageAttackPowerFixture",
            10f);
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.StatModifier);
        passive.FindPropertyRelative("effects").ClearArray();
        SerializedProperty modifiers =
            passive.FindPropertyRelative("statModifiers");
        modifiers.arraySize = 1;
        SerializedProperty modifier = modifiers.GetArrayElementAtIndex(0);
        modifier.FindPropertyRelative("statType").enumValueIndex =
            (int)StatusEffectStatType.AttackPower;
        modifier.FindPropertyRelative("mode").enumValueIndex =
            (int)StatusEffectStatModifierMode.Flat;
        modifier.FindPropertyRelative("baseValue").floatValue = 0f;
        modifier.FindPropertyRelative("dungeonStageProgressScale")
            .floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        FakeBattleBoard board = new()
        {
            DungeonStageProgress = 2f,
        };
        character.BindBattle(null, board);

        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(12f).Within(0.0001f));
        Assert.That(
            CharacterLocalization.GetPassiveDescription(character.Data),
            Does.Contain("완료 스테이지"));
    }

    [Test]
    public void PassiveStatModifier_IncomingDamageIsRejectedAndIgnored()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "UnsupportedIncomingDamagePassiveFixture");
        SerializedObject serialized = new(definition);
        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.StatModifier);
        passive.FindPropertyRelative("effects").ClearArray();
        SerializedProperty modifiers =
            passive.FindPropertyRelative("statModifiers");
        modifiers.arraySize = 1;
        SerializedProperty modifier = modifiers.GetArrayElementAtIndex(0);
        modifier.FindPropertyRelative("statType").enumValueIndex =
            (int)StatusEffectStatType.IncomingDamage;
        modifier.FindPropertyRelative("mode").enumValueIndex =
            (int)StatusEffectStatModifierMode.AdditiveRatio;
        modifier.FindPropertyRelative("baseValue").floatValue = 1f;
        modifier.FindPropertyRelative("dungeonStageProgressScale")
            .floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(validation.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                validation,
                "passive_stat_modifier.stat_unsupported"),
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(character.TakeDamage(10), Is.EqualTo(10));
        Assert.That(character.CurrentHealth, Is.EqualTo(90));
        Assert.That(
            CharacterLocalization.GetPassiveDescription(character.Data),
            Does.Not.Contain("받는 피해").And.Not.Contain(
                "Incoming Damage"));
    }

    [Test]
    public void DungeonStageProgress_CountsEventStages()
    {
        GameObject root = new("DungeonStageProgressFixture");
        _createdObjects.Add(root);
        DungeonFlowController flow =
            root.AddComponent<DungeonFlowController>();
        GameObject battle = CreateFlowTab(root, "Battle");
        GameObject dungeonEvent = CreateFlowTab(root, "Event");
        GameObject rest = CreateFlowTab(root, "Rest");
        GameObject shop = CreateFlowTab(root, "Shop");

        SerializedObject serialized = new(flow);
        serialized.FindProperty("battleTab").objectReferenceValue = battle;
        serialized.FindProperty("eventTab").objectReferenceValue =
            dungeonEvent;
        serialized.FindProperty("restTab").objectReferenceValue = rest;
        serialized.FindProperty("shopTab").objectReferenceValue = shop;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(flow.Initialize(), Is.True);
        Assert.That(
            flow.StartRun(new[]
            {
                EDungeonPhase.Battle,
                EDungeonPhase.Event,
                EDungeonPhase.Battle,
            }),
            Is.True);
        Assert.That(flow.CurrentStageProgress, Is.EqualTo(0f));

        Assert.That(flow.TryAdvance(), Is.True);
        Assert.That(flow.CurrentPhase, Is.EqualTo(EDungeonPhase.Event));
        Assert.That(flow.CurrentStageProgress, Is.EqualTo(1f));

        Assert.That(flow.TryAdvance(), Is.True);
        Assert.That(flow.CurrentPhase, Is.EqualTo(EDungeonPhase.Battle));
        Assert.That(flow.CurrentStageProgress, Is.EqualTo(2f));
    }

    [Test]
    public void EffectStatusContributionMultiplier_IsLocalToThatEffect()
    {
        StatusEffectSO power = CreateRuntimeStatus(
            "test_heavy_blade_power",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureRuntimeStatusModifier(
            power,
            0,
            StatusEffectStatType.AttackPower,
            StatusEffectStatModifierMode.Flat,
            1f,
            true);

        CharacterSO definition = CreateBaseCharacterFixture(
            "EffectStatusContributionFixture",
            10f);
        SerializedObject serialized = new(definition);
        SerializedProperty damage = serialized
            .FindProperty("attackDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        ConfigureDamageEffect(
            damage,
            CharacterEffectTargetMode.InheritAction,
            CharacterDamageAmountMode.Ratio,
            1f);
        ConfigureStatusContributionMultiplier(
            damage,
            0,
            power,
            StatusEffectStatType.AttackPower,
            3f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult validation =
            CharacterDefinitionValidator.Validate(definition);
        Assert.That(
            validation.IsValid,
            Is.True,
            string.Join("\n", validation.Diagnostics));

        CharacterRuntime character = CreateCharacter(definition);
        Assert.That(
            character.ApplyStatusEffect(power, 5f, 2),
            Is.True);
        Assert.That(
            character.CurrentAttackPower,
            Is.EqualTo(12f).Within(0.0001f),
            "The effect-local multiplier must not alter the displayed stat.");

        EnemyRuntime target = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        character.BindBattle(null, board);
        character.TickBattle(character.Data.AttackCooldown, board);

        Assert.That(
            board.DamageAmounts,
            Is.EqualTo(new[] { 16 }),
            "Base 10 plus two Power stacks at three damage each.");
    }

    [Test]
    public void ModularStatusControls_BlockOnlyConfiguredActionGroups()
    {
        StatusEffectSO granular = CreateRuntimeStatus(
            "test_granular_controls",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureRuntimeStatusControl(
            granular,
            0,
            StatusEffectControlType.DisableBasicAttack);
        ConfigureRuntimeStatusControl(
            granular,
            1,
            StatusEffectControlType.DisableActiveSkill);
        ConfigureRuntimeStatusControl(
            granular,
            2,
            StatusEffectControlType.PausePassiveCooldowns);

        CharacterRuntime character = CreateCharacter(AislingAssetPath);
        Assert.That(
            character.ApplyStatusEffect(granular, 2f, 1),
            Is.True);
        Assert.That(character.AreAllActionsDisabled, Is.False);
        Assert.That(character.IsBasicAttackBlocked, Is.True);
        Assert.That(character.IsActiveSkillBlocked, Is.True);
        Assert.That(character.ArePassiveCooldownsPaused, Is.True);
        Assert.That(character.DisabledTimeRemaining, Is.Zero);

        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                granular,
                0),
            Is.EqualTo(1));
        Assert.That(character.IsBasicAttackBlocked, Is.False);
        Assert.That(character.IsActiveSkillBlocked, Is.False);
        Assert.That(character.ArePassiveCooldownsPaused, Is.False);

        StatusEffectSO fullDisable = CreateRuntimeStatus(
            "test_disable_all_control",
            false,
            true,
            StatusEffectStackMode.Replace,
            0);
        ConfigureRuntimeStatusControl(
            fullDisable,
            0,
            StatusEffectControlType.DisableAllActions);

        Assert.That(
            character.ApplyStatusEffect(fullDisable, 2f, 1),
            Is.True);
        Assert.That(character.AreAllActionsDisabled, Is.True);
        Assert.That(character.IsBasicAttackBlocked, Is.True);
        Assert.That(character.IsActiveSkillBlocked, Is.True);
        Assert.That(character.ArePassiveCooldownsPaused, Is.True);
        Assert.That(
            character.DisabledTimeRemaining,
            Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void DisableAllControl_PausesEnemyAbilityCooldown()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_enemy_disable_all",
            true,
            false,
            StatusEffectStackMode.Replace,
            0);
        ConfigureRuntimeStatusControl(
            status,
            0,
            StatusEffectControlType.DisableAllActions);
        EnemyRuntime enemy = CreateEnemyRuntime(
            maximumHealth: 20,
            abilityCooldown: 3f);

        Assert.That(
            ApplyEnemyStatus(
                enemy,
                status,
                2f,
                1,
                null,
                1f),
            Is.True);
        Assert.That(enemy.AreAllActionsDisabled, Is.True);
        Assert.That(TickEnemyAbilityCooldown(enemy, 1f), Is.False);
        Assert.That(
            enemy.AbilityCooldownRemaining,
            Is.EqualTo(3f).Within(0.0001f));

        Assert.That(
            RemoveEnemyStatus(
                enemy,
                CharacterStatusRemovalTarget.Single,
                status,
                0),
            Is.EqualTo(1));
        Assert.That(enemy.AreAllActionsDisabled, Is.False);
        Assert.That(TickEnemyAbilityCooldown(enemy, 1f), Is.False);
        Assert.That(
            enemy.AbilityCooldownRemaining,
            Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void AttackPowerModifier_AffectsScaledSkillDamage()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        CharacterSO definition = CreateExplicitDamageAndStatusCharacter(fire);
        SerializedObject serializedDefinition = new(definition);
        serializedDefinition.FindProperty("attackPower").intValue = 4;
        SerializedProperty damageEffect = serializedDefinition
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0);
        damageEffect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Ratio;
        damageEffect.FindPropertyRelative("damageAmount").floatValue = 1f;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

        StatusEffectSO modifier = CreateRuntimeStatus(
            "test_skill_power_modifier",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            2);
        ConfigureRuntimeStatusOperation(
            modifier,
            0,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackPowerModifier,
            StatusEffectValueMode.Fixed,
            2f,
            false);
        ConfigureRuntimeStatusOperation(
            modifier,
            1,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackPowerModifier,
            StatusEffectValueMode.Ratio,
            0.5f,
            false);

        CharacterRuntime character = CreateCharacter(definition);
        EnemyRuntime target = CreateEnemyRuntime();
        FakeActiveSkillResource resource = new(10);
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        character.BindBattle(resource, board);

        Assert.That(character.ApplyStatusEffect(modifier, 5f, 1), Is.True);
        Assert.That(character.CurrentAttackPower, Is.EqualTo(8f));
        Assert.That(character.TryActivateActiveSkill(), Is.True);

        Assert.That(character.TotalDamageDealt, Is.EqualTo(8));
        Assert.That(resource.Current, Is.EqualTo(8));
    }

    [Test]
    public void Stun_GenericStatusBlocksActionUntilExpiration()
    {
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        EnemyRuntime target = CreateEnemyRuntime();
        FakeBattleBoard board = new()
        {
            LivingEnemyCountValue = 1,
            SelectedEnemyTargets = new[] { target },
        };
        character.BindBattle(null, board);

        Assert.That(character.ApplyStatusEffect(stun, 3f, 1), Is.True);
        Assert.That(character.HasStatusEffect(stun), Is.True);
        Assert.That(character.GetStatusStackCount(stun), Is.EqualTo(1));
        Assert.That(character.DisabledStatusEffect, Is.SameAs(stun));
        Assert.That(character.DisabledTimeRemaining, Is.EqualTo(3f));

        character.TickBattle(3f, board);

        Assert.That(board.DamageTargetSnapshots, Is.Empty);
        Assert.That(character.HasStatusEffect(stun), Is.False);
        Assert.That(character.GetStatusStackCount(stun), Is.Zero);
        Assert.That(character.DisabledStatusEffect, Is.Null);
        Assert.That(character.DisabledTimeRemaining, Is.Zero);

        character.TickBattle(3f, board);

        Assert.That(
            board.DamageTargetSnapshots,
            Is.Not.Empty,
            "The character must resume acting after the generic Stun " +
            "status expires.");
    }

    [Test]
    public void AlliedStatus_ReadModelReportsOrderedLifecycleChanges()
    {
        StatusEffectSO laterStatus = CreateRuntimeStatus(
            "test_status_z",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO earlierStatus = CreateRuntimeStatus(
            "test_status_a",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        FakeBattleBoard board = new();
        character.BindBattle(null, board);
        List<BattleStatusChangedEvent> events = new();
        character.StatusChanged += events.Add;

        Assert.That(
            character.ApplyStatusEffect(laterStatus, 3f, 2),
            Is.True);
        Assert.That(
            character.ApplyStatusEffect(laterStatus, 4f, 1),
            Is.True);
        Assert.That(
            character.ApplyStatusEffect(earlierStatus, 1f, 1),
            Is.True);

        IReadOnlyList<BattleStatusSnapshot> snapshots =
            character.GetActiveStatusEffects();
        Assert.That(snapshots.Count, Is.EqualTo(2));
        Assert.That(
            snapshots[0].Definition,
            Is.SameAs(earlierStatus),
            "Snapshots must use stable StatusId ordering for presentation.");
        Assert.That(snapshots[1].Definition, Is.SameAs(laterStatus));
        Assert.That(snapshots[1].StackCount, Is.EqualTo(3));
        Assert.That(snapshots[1].RemainingDuration, Is.EqualTo(4f));

        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                laterStatus,
                1),
            Is.EqualTo(1));
        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                laterStatus,
                0),
            Is.EqualTo(2));
        character.TickBattle(1f, board);

        Assert.That(
            events.ConvertAll(eventData => eventData.ChangeType),
            Is.EqualTo(new[]
            {
                BattleStatusChangeType.Applied,
                BattleStatusChangeType.Reapplied,
                BattleStatusChangeType.Applied,
                BattleStatusChangeType.StackChanged,
                BattleStatusChangeType.Removed,
                BattleStatusChangeType.Expired,
            }));
        Assert.That(
            events.TrueForAll(eventData =>
                ReferenceEquals(eventData.Target.Ally, character) &&
                eventData.Target.Enemy == null),
            Is.True);
        Assert.That(events[0].PreviousStacks, Is.Zero);
        Assert.That(events[0].CurrentStacks, Is.EqualTo(2));
        Assert.That(events[1].PreviousStacks, Is.EqualTo(2));
        Assert.That(events[1].CurrentStacks, Is.EqualTo(3));
        Assert.That(events[3].PreviousStacks, Is.EqualTo(3));
        Assert.That(events[3].CurrentStacks, Is.EqualTo(2));
        Assert.That(events[4].PreviousStacks, Is.EqualTo(2));
        Assert.That(events[4].CurrentStacks, Is.Zero);
        Assert.That(events[5].StatusEffect, Is.SameAs(earlierStatus));
        Assert.That(events[5].CurrentStacks, Is.Zero);
        Assert.That(character.GetActiveStatusEffects(), Is.Empty);
    }

    [Test]
    public void AlliedStatus_RatioRemovalUsesCurrentStacksPerStatusType()
    {
        StatusEffectSO firstStatus = CreateRuntimeStatus(
            "test_ratio_removal_a",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO secondStatus = CreateRuntimeStatus(
            "test_ratio_removal_b",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);

        Assert.That(
            character.ApplyStatusEffect(firstStatus, 5f, 7),
            Is.True);
        Assert.That(
            character.ApplyStatusEffect(secondStatus, 5f, 3),
            Is.True);

        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.Single,
                firstStatus,
                CharacterStatusRemovalAmount.Ratio(0.5f)),
            Is.EqualTo(4),
            "7 current stacks at one half must round up to 4 removed.");
        Assert.That(
            character.GetStatusStackCount(firstStatus),
            Is.EqualTo(3));

        Assert.That(
            character.ApplyStatusEffect(firstStatus, 5f, 4),
            Is.True);
        Assert.That(
            character.RemoveStatusEffects(
                CharacterStatusRemovalTarget.All,
                null,
                CharacterStatusRemovalAmount.Ratio(0.25f)),
            Is.EqualTo(3),
            "All removal must resolve one quarter independently for each " +
            "status type: ceil(7/4) + ceil(3/4).");
        Assert.That(
            character.GetStatusStackCount(firstStatus),
            Is.EqualTo(5));
        Assert.That(
            character.GetStatusStackCount(secondStatus),
            Is.EqualTo(2));
    }

    [Test]
    public void AlliedStatus_ExplicitSelectionRemovesEveryChosenStatus()
    {
        StatusEffectSO firstStatus = CreateRuntimeStatus(
            "test_multi_removal_a",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO secondStatus = CreateRuntimeStatus(
            "test_multi_removal_b",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO untouchedStatus = CreateRuntimeStatus(
            "test_multi_removal_untouched",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        character.ApplyStatusEffect(firstStatus, 5f, 2);
        character.ApplyStatusEffect(secondStatus, 5f, 3);
        character.ApplyStatusEffect(untouchedStatus, 5f, 4);

        CharacterStatusRemovalSelection selection = new(
            CharacterStatusRemovalTarget.Single,
            null,
            new[] { firstStatus, secondStatus, firstStatus });
        int removed = character.RemoveStatusEffects(
            selection,
            CharacterStatusRemovalAmount.Fixed(0));

        Assert.That(removed, Is.EqualTo(5));
        Assert.That(character.GetStatusStackCount(firstStatus), Is.Zero);
        Assert.That(character.GetStatusStackCount(secondStatus), Is.Zero);
        Assert.That(
            character.GetStatusStackCount(untouchedStatus),
            Is.EqualTo(4));
    }

    [Test]
    public void AlliedStatus_ExplicitSelectionRandomCountRemovesExactlyNStatuses()
    {
        StatusEffectSO firstStatus = CreateRuntimeStatus(
            "test_multi_random_removal_a",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO secondStatus = CreateRuntimeStatus(
            "test_multi_random_removal_b",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO thirdStatus = CreateRuntimeStatus(
            "test_multi_random_removal_c",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        character.ApplyStatusEffect(firstStatus, 5f, 1);
        character.ApplyStatusEffect(secondStatus, 5f, 1);
        character.ApplyStatusEffect(thirdStatus, 5f, 1);

        CharacterStatusRemovalSelection selection = new(
            CharacterStatusRemovalTarget.Single,
            null,
            new[] { firstStatus, secondStatus, thirdStatus },
            CharacterStatusRemovalPickMode.RandomCount,
            2);
        int removed = character.RemoveStatusEffects(
            selection,
            CharacterStatusRemovalAmount.Fixed(0));

        Assert.That(removed, Is.EqualTo(2));
        Assert.That(
            character.GetStatusStackCount(firstStatus) +
            character.GetStatusStackCount(secondStatus) +
            character.GetStatusStackCount(thirdStatus),
            Is.EqualTo(1));
    }

    [Test]
    public void AlliedStatus_BuffRandomCountIgnoresDebuffsAndProtectedBuffs()
    {
        StatusEffectSO firstBuff = CreateRuntimeStatus(
            "test_random_buff_removal_a",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO secondBuff = CreateRuntimeStatus(
            "test_random_buff_removal_b",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO thirdBuff = CreateRuntimeStatus(
            "test_random_buff_removal_c",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO protectedBuff = CreateRuntimeStatus(
            "test_random_buff_removal_protected",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO debuff = CreateRuntimeStatus(
            "test_random_buff_removal_debuff",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureStatusRemovalMetadata(
            firstBuff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            secondBuff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            thirdBuff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            protectedBuff,
            StatusEffectAlignment.Buff,
            false);
        ConfigureStatusRemovalMetadata(
            debuff,
            StatusEffectAlignment.Debuff,
            true);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        character.ApplyStatusEffect(firstBuff, 5f, 1);
        character.ApplyStatusEffect(secondBuff, 5f, 1);
        character.ApplyStatusEffect(thirdBuff, 5f, 1);
        character.ApplyStatusEffect(protectedBuff, 5f, 1);
        character.ApplyStatusEffect(debuff, 5f, 1);

        int removed = character.RemoveStatusEffects(
            new CharacterStatusRemovalSelection(
                CharacterStatusRemovalTarget.Buff,
                null,
                null,
                CharacterStatusRemovalPickMode.RandomCount,
                2),
            CharacterStatusRemovalAmount.Fixed(0));

        Assert.That(removed, Is.EqualTo(2));
        Assert.That(
            character.GetStatusStackCount(firstBuff) +
            character.GetStatusStackCount(secondBuff) +
            character.GetStatusStackCount(thirdBuff),
            Is.EqualTo(1));
        Assert.That(
            character.GetStatusStackCount(protectedBuff),
            Is.EqualTo(1));
        Assert.That(character.GetStatusStackCount(debuff), Is.EqualTo(1));
    }

    [Test]
    public void AlliedStatus_AlignmentRemovalRespectsProtectedStatuses()
    {
        StatusEffectSO buff = CreateRuntimeStatus(
            "test_group_removal_buff",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO protectedBuff = CreateRuntimeStatus(
            "test_group_removal_protected_buff",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO debuff = CreateRuntimeStatus(
            "test_group_removal_debuff",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO neutral = CreateRuntimeStatus(
            "test_group_removal_neutral",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureStatusRemovalMetadata(
            buff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            protectedBuff,
            StatusEffectAlignment.Buff,
            false);
        ConfigureStatusRemovalMetadata(
            debuff,
            StatusEffectAlignment.Debuff,
            true);
        ConfigureStatusRemovalMetadata(
            neutral,
            StatusEffectAlignment.Neutral,
            true);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        character.ApplyStatusEffect(buff, 5f, 1);
        character.ApplyStatusEffect(protectedBuff, 5f, 1);
        character.ApplyStatusEffect(debuff, 5f, 1);
        character.ApplyStatusEffect(neutral, 5f, 1);

        Assert.That(
            character.RemoveStatusEffects(
                new CharacterStatusRemovalSelection(
                    CharacterStatusRemovalTarget.Buff,
                    null),
                CharacterStatusRemovalAmount.Fixed(0)),
            Is.EqualTo(1));
        Assert.That(character.GetStatusStackCount(buff), Is.Zero);
        Assert.That(
            character.GetStatusStackCount(protectedBuff),
            Is.EqualTo(1));
        Assert.That(character.GetStatusStackCount(debuff), Is.EqualTo(1));
        Assert.That(character.GetStatusStackCount(neutral), Is.EqualTo(1));

        Assert.That(
            character.RemoveStatusEffects(
                new CharacterStatusRemovalSelection(
                    CharacterStatusRemovalTarget.Debuff,
                    null),
                CharacterStatusRemovalAmount.Fixed(0)),
            Is.EqualTo(1));
        Assert.That(character.GetStatusStackCount(debuff), Is.Zero);

        Assert.That(
            character.RemoveStatusEffects(
                new CharacterStatusRemovalSelection(
                    CharacterStatusRemovalTarget.All,
                    null),
                CharacterStatusRemovalAmount.Fixed(0)),
            Is.EqualTo(1));
        Assert.That(character.GetStatusStackCount(neutral), Is.Zero);
        Assert.That(
            character.GetStatusStackCount(protectedBuff),
            Is.EqualTo(1));
    }

    [Test]
    public void EnemyStatus_DebuffRemovalKeepsBuffs()
    {
        StatusEffectSO buff = CreateRuntimeStatus(
            "test_enemy_group_removal_buff",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO debuff = CreateRuntimeStatus(
            "test_enemy_group_removal_debuff",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureStatusRemovalMetadata(
            buff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            debuff,
            StatusEffectAlignment.Debuff,
            true);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        Assert.That(
            ApplyEnemyStatus(enemy, buff, 5f, 2, source, 1f),
            Is.True);
        Assert.That(
            ApplyEnemyStatus(enemy, debuff, 5f, 3, source, 1f),
            Is.True);

        int removed = RemoveEnemyStatuses(
            enemy,
            new CharacterStatusRemovalSelection(
                CharacterStatusRemovalTarget.Debuff,
                null),
            CharacterStatusRemovalAmount.Fixed(0));

        Assert.That(removed, Is.EqualTo(3));
        Assert.That(GetEnemyStatusStacks(enemy, debuff), Is.Zero);
        Assert.That(GetEnemyStatusStacks(enemy, buff), Is.EqualTo(2));
    }

    [Test]
    public void EnemyStatus_DebuffRandomCountRemovesExactlyNStatuses()
    {
        StatusEffectSO firstDebuff = CreateRuntimeStatus(
            "test_enemy_random_debuff_removal_a",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO secondDebuff = CreateRuntimeStatus(
            "test_enemy_random_debuff_removal_b",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO thirdDebuff = CreateRuntimeStatus(
            "test_enemy_random_debuff_removal_c",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        StatusEffectSO buff = CreateRuntimeStatus(
            "test_enemy_random_debuff_removal_buff",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        ConfigureStatusRemovalMetadata(
            firstDebuff,
            StatusEffectAlignment.Debuff,
            true);
        ConfigureStatusRemovalMetadata(
            secondDebuff,
            StatusEffectAlignment.Debuff,
            true);
        ConfigureStatusRemovalMetadata(
            thirdDebuff,
            StatusEffectAlignment.Debuff,
            true);
        ConfigureStatusRemovalMetadata(
            buff,
            StatusEffectAlignment.Buff,
            true);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        ApplyEnemyStatus(enemy, firstDebuff, 5f, 1, source, 1f);
        ApplyEnemyStatus(enemy, secondDebuff, 5f, 1, source, 1f);
        ApplyEnemyStatus(enemy, thirdDebuff, 5f, 1, source, 1f);
        ApplyEnemyStatus(enemy, buff, 5f, 1, source, 1f);

        int removed = RemoveEnemyStatuses(
            enemy,
            new CharacterStatusRemovalSelection(
                CharacterStatusRemovalTarget.Debuff,
                null,
                null,
                CharacterStatusRemovalPickMode.RandomCount,
                2),
            CharacterStatusRemovalAmount.Fixed(0));

        Assert.That(removed, Is.EqualTo(2));
        Assert.That(
            GetEnemyStatusStacks(enemy, firstDebuff) +
            GetEnemyStatusStacks(enemy, secondDebuff) +
            GetEnemyStatusStacks(enemy, thirdDebuff),
            Is.EqualTo(1));
        Assert.That(GetEnemyStatusStacks(enemy, buff), Is.EqualTo(1));
    }

    [Test]
    public void AlliedStatus_ActualBoardPreservesApplyingSource()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_allied_status_source",
            false,
            true,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        CharacterRuntime target = CreateCharacter(AislingAssetPath);
        GameObject boardObject = new("Test_AlliedStatusSourceBoard");
        _createdObjects.Add(boardObject);
        DungeonBoardView board =
            boardObject.AddComponent<DungeonBoardView>();
        target.BindBattle(null, board);

        List<BattleStatusChangedEvent> lifecycleEvents = new();
        List<BattleStatusAppliedEvent> appliedEvents = new();
        target.StatusChanged += lifecycleEvents.Add;
        board.StatusApplied += appliedEvents.Add;

        Assert.That(
            board.TryApplyAlliedCharacterStatus(
                source,
                new IBattleCharacter[] { target },
                status,
                2f,
                1f),
            Is.True);

        IReadOnlyList<BattleStatusSnapshot> snapshots =
            target.GetActiveStatusEffects();
        Assert.That(snapshots.Count, Is.EqualTo(1));
        Assert.That(snapshots[0].ActiveSource, Is.SameAs(source));
        Assert.That(lifecycleEvents, Has.Count.EqualTo(1));
        Assert.That(
            lifecycleEvents[0].Current.ActiveSource,
            Is.SameAs(source));
        Assert.That(appliedEvents, Has.Count.EqualTo(1));
        Assert.That(appliedEvents[0].Source, Is.SameAs(source));
        Assert.That(appliedEvents[0].Target.Ally, Is.SameAs(target));
    }

    [Test]
    public void EnemyStatus_LifecycleDispatchCompletesWithoutSubscriber()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_enemy_no_lifecycle_subscriber",
            true,
            false,
            StatusEffectStackMode.AddAndRefreshDuration,
            0);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);

        Assert.That(
            ApplyEnemyStatus(enemy, status, 2f, 1, source, 1f),
            Is.True);
        Assert.That(GetEnemyStatusStacks(enemy, status), Is.EqualTo(1));
        Assert.That(
            RemoveEnemyStatus(
                enemy,
                CharacterStatusRemovalTarget.Single,
                status,
                0),
            Is.EqualTo(1));
        Assert.That(enemy.GetActiveStatusEffects(), Is.Empty);
    }

    [Test]
    public void EnemyStatus_ReadModelReportsPartialAndFinalExpiration()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_enemy_lifecycle",
            true,
            false,
            StatusEffectStackMode.IndependentDuration,
            0);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        List<BattleStatusChangedEvent> events = new();
        enemy.StatusChanged += events.Add;

        Assert.That(
            ApplyEnemyStatus(enemy, status, 2f, 2, source, 1f),
            Is.True);
        Assert.That(
            ApplyEnemyStatus(enemy, status, 1f, 1, source, 1f),
            Is.True);

        IReadOnlyList<BattleStatusSnapshot> snapshots =
            enemy.GetActiveStatusEffects();
        Assert.That(snapshots.Count, Is.EqualTo(1));
        Assert.That(snapshots[0].Definition, Is.SameAs(status));
        Assert.That(snapshots[0].StackCount, Is.EqualTo(3));
        Assert.That(snapshots[0].RemainingDuration, Is.EqualTo(3f));

        Assert.That(TickEnemyStatuses(enemy, 2f, null), Is.True);
        Assert.That(GetEnemyStatusStacks(enemy, status), Is.EqualTo(1));
        Assert.That(TickEnemyStatuses(enemy, 1f, null), Is.True);

        Assert.That(
            events.ConvertAll(eventData => eventData.ChangeType),
            Is.EqualTo(new[]
            {
                BattleStatusChangeType.Applied,
                BattleStatusChangeType.Reapplied,
                BattleStatusChangeType.StackChanged,
                BattleStatusChangeType.Expired,
            }));
        Assert.That(
            events.TrueForAll(eventData =>
                eventData.Target.Enemy == enemy &&
                eventData.Target.Ally == null),
            Is.True);
        Assert.That(events[2].PreviousStacks, Is.EqualTo(3));
        Assert.That(events[2].CurrentStacks, Is.EqualTo(1));
        Assert.That(events[3].PreviousStacks, Is.EqualTo(1));
        Assert.That(events[3].CurrentStacks, Is.Zero);
        Assert.That(enemy.GetActiveStatusEffects(), Is.Empty);
    }

    [Test]
    public void PermanentStatus_SnapshotPreservesInfiniteDuration()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_permanent_snapshot",
            false,
            true,
            StatusEffectStackMode.AddKeepDuration,
            0);
        SerializedObject serialized = new(status);
        serialized.FindProperty("durationMode").enumValueIndex =
            (int)StatusEffectDurationMode.Permanent;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        status.ValidateDefinition();
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        List<BattleStatusChangedEvent> events = new();
        character.StatusChanged += events.Add;

        Assert.That(character.ApplyStatusEffect(status, 0f, 1), Is.True);

        IReadOnlyList<BattleStatusSnapshot> snapshots =
            character.GetActiveStatusEffects();
        Assert.That(snapshots.Count, Is.EqualTo(1));
        Assert.That(snapshots[0].IsValid, Is.True);
        Assert.That(snapshots[0].IsPermanent, Is.True);
        Assert.That(
            snapshots[0].RemainingDuration,
            Is.EqualTo(float.PositiveInfinity));

        character.ResetRuntime();

        Assert.That(character.GetActiveStatusEffects(), Is.Empty);
        Assert.That(
            events.ConvertAll(eventData => eventData.ChangeType),
            Is.EqualTo(new[]
            {
                BattleStatusChangeType.Applied,
                BattleStatusChangeType.Removed,
            }));
        Assert.That(events[1].Current.IsValid, Is.False);
        Assert.That(events[1].Current.IsPermanent, Is.False);
        Assert.That(events[1].Current.RemainingDuration, Is.Zero);
    }

    [Test]
    public void BattleItemStatusDuration_PersistsUntilBattleReset()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_battle_item_persistent_status",
            false,
            true,
            StatusEffectStackMode.AddKeepDuration,
            0);
        CharacterRuntime character = CreateCharacter(SuirenAssetPath);
        FakeBattleBoard board = new();
        CharacterEffectDefinition effect = new();
        SetPrivateField(effect, "type", CharacterEffectType.ApplyStatus);
        SetPrivateField(
            effect,
            "targetMode",
            CharacterEffectTargetMode.InheritAction);
        SetPrivateField(effect, "statusEffect", status);
        SetPrivateField(effect, "statusDuration", 1f);
        SetPrivateField(effect, "statusStacks", 1f);
        BattleEffectContext context = BattleEffectContext.ForBattleItem(
            BattleStatusTarget.FromAlly(character),
            board,
            null,
            CharacterTargetFaction.Ally,
            Array.Empty<EnemyRuntime>(),
            new IBattleCharacter[] { character },
            character.CurrentAttackPower,
            statusEffectsLastUntilBattleEnd: true);

        BattleEffectResult result = BattleEffectExecutor.ExecuteSequence(
            context,
            new IBattleEffectDefinition[] { effect });

        Assert.That(result.Succeeded, Is.True);
        IReadOnlyList<BattleStatusSnapshot> activeStatusEffects =
            character.GetActiveStatusEffects();
        Assert.That(activeStatusEffects.Count, Is.EqualTo(1));
        Assert.That(
            activeStatusEffects[0].IsPermanent,
            Is.True);

        character.TickBattle(120f, board);
        Assert.That(character.HasStatusEffect(status), Is.True);

        Assert.That(character.ApplyStatusEffect(status, 1f, 1), Is.True);
        activeStatusEffects = character.GetActiveStatusEffects();
        Assert.That(
            activeStatusEffects[0].IsPermanent,
            Is.True,
            "A timed reapplication must not shorten a battle-long item buff.");

        character.ResetRuntime();
        Assert.That(character.HasStatusEffect(status), Is.False);
    }

    [Test]
    public void FireCompatibilityWrapper_PublishesGenericStatusLifecycle()
    {
        StatusEffectSO fire = LoadAsset<StatusEffectSO>(FireAssetPath);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        List<BattleStatusChangedEvent> events = new();
        enemy.StatusChanged += events.Add;

        ApplyFire(enemy, 2f, 1f, 2, source);

        IReadOnlyList<BattleStatusSnapshot> snapshots =
            enemy.GetActiveStatusEffects();
        Assert.That(snapshots.Count, Is.EqualTo(1));
        Assert.That(snapshots[0].Definition, Is.SameAs(fire));
        Assert.That(snapshots[0].StackCount, Is.EqualTo(2));
        Assert.That(enemy.HasFire, Is.True);

        Assert.That(
            RemoveEnemyStatus(
                enemy,
                CharacterStatusRemovalTarget.Single,
                fire,
                0),
            Is.EqualTo(2));

        Assert.That(
            events.ConvertAll(eventData => eventData.ChangeType),
            Is.EqualTo(new[]
            {
                BattleStatusChangeType.Applied,
                BattleStatusChangeType.Removed,
            }));
        Assert.That(events[0].StatusEffect, Is.SameAs(fire));
        Assert.That(events[1].StatusEffect, Is.SameAs(fire));
        Assert.That(enemy.HasFire, Is.False);
        Assert.That(enemy.GetActiveStatusEffects(), Is.Empty);
    }

    [Test]
    public void EnemyStatus_ReentrantLifecycleEventWaitsForOperations()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "test_reentrant_lifecycle",
            true,
            false,
            StatusEffectStackMode.Replace,
            2);
        ConfigureRuntimeStatusOperation(
            status,
            0,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.InstantDamage,
            StatusEffectValueMode.Fixed,
            1f,
            false);
        ConfigureRuntimeStatusOperation(
            status,
            1,
            StatusEffectOperationTrigger.OnRemove,
            StatusEffectOperationType.InstantDamage,
            StatusEffectValueMode.Fixed,
            2f,
            false);
        EnemyRuntime enemy = CreateEnemyRuntime();
        CharacterRuntime source = CreateCharacter(SuirenAssetPath);
        List<string> sequence = new();
        Func<int, IBattleCharacter, bool> applyDamage = (damage, _) =>
        {
            sequence.Add($"damage:{damage}");
            return true;
        };
        enemy.StatusChanged += eventData =>
        {
            sequence.Add(eventData.ChangeType.ToString());
            if (eventData.ChangeType == BattleStatusChangeType.Applied)
            {
                RemoveEnemyStatus(
                    enemy,
                    CharacterStatusRemovalTarget.Single,
                    status,
                    0,
                    applyDamage);
            }
        };

        Assert.That(
            ApplyEnemyStatus(
                enemy,
                status,
                3f,
                1,
                source,
                1f,
                applyDamage),
            Is.True);

        Assert.That(
            sequence,
            Is.EqualTo(new[]
            {
                "damage:1",
                "Applied",
                "damage:2",
                "Removed",
            }));
        Assert.That(enemy.GetActiveStatusEffects(), Is.Empty);
    }

    [Test]
    public void StatusStackCondition_MultiSelectionSupportsAnyAllAndCount()
    {
        StatusEffectSO first = CreateRuntimeStatus(
            "condition-first",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        StatusEffectSO second = CreateRuntimeStatus(
            "condition-second",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        CharacterRuntime character = CreateCharacter(
            CreateBaseCharacterFixture("status-condition-selection"));
        Assert.That(
            character.ApplyStatusEffect(first, 1f, 1),
            Is.True);

        CharacterNumericCondition condition = new();
        SetPrivateField(
            condition,
            "metric",
            CharacterNumericConditionMetric.StatusStackCount);
        SetPrivateField(
            condition,
            "comparison",
            CharacterNumericComparison.GreaterThanOrEqual);
        SetPrivateField(condition, "threshold", 1f);
        SetPrivateField(
            condition,
            "statusEffects",
            new List<StatusEffectSO> { first, second });

        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.Any);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True);

        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.All);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.False);

        Assert.That(
            character.ApplyStatusEffect(second, 1f, 1),
            Is.True);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True);

        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.AtLeastCount);
        SetPrivateField(condition, "statusMatchCount", 2);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True);
    }

    [Test]
    public void StatusStackCondition_BuffAndDebuffScopesSupportAllAndCount()
    {
        StatusEffectSO firstBuff = CreateRuntimeStatus(
            "condition-scope-buff-first",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        StatusEffectSO secondBuff = CreateRuntimeStatus(
            "condition-scope-buff-second",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        StatusEffectSO debuff = CreateRuntimeStatus(
            "condition-scope-debuff",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        ConfigureStatusRemovalMetadata(
            firstBuff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            secondBuff,
            StatusEffectAlignment.Buff,
            true);
        ConfigureStatusRemovalMetadata(
            debuff,
            StatusEffectAlignment.Debuff,
            true);

        CharacterRuntime character = CreateCharacter(
            CreateBaseCharacterFixture("status-condition-scope"));
        Assert.That(character.ApplyStatusEffect(firstBuff, 5f, 1), Is.True);
        Assert.That(character.ApplyStatusEffect(secondBuff, 5f, 2), Is.True);
        Assert.That(character.ApplyStatusEffect(debuff, 5f, 3), Is.True);

        CharacterNumericCondition condition = new();
        SetPrivateField(
            condition,
            "metric",
            CharacterNumericConditionMetric.StatusStackCount);
        SetPrivateField(
            condition,
            "comparison",
            CharacterNumericComparison.GreaterThanOrEqual);
        SetPrivateField(condition, "threshold", 1f);
        SetPrivateField(
            condition,
            "statusSelectionScope",
            CharacterStatusSelectionScope.AllBuffs);
        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.All);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True);

        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.AtLeastCount);
        SetPrivateField(condition, "statusMatchCount", 2);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True,
            "Both active buffs must count as distinct matches.");

        SetPrivateField(condition, "threshold", 2f);
        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.All);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.False,
            "All must apply the stack comparison to every active buff.");

        SetPrivateField(
            condition,
            "statusSelectionScope",
            CharacterStatusSelectionScope.AllDebuffs);
        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.AtLeastCount);
        SetPrivateField(condition, "statusMatchCount", 1);
        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True,
            "The debuff scope must exclude both buffs.");
    }

    [Test]
    public void StatusStackCondition_EmptyListUsesLegacyStatusField()
    {
        StatusEffectSO status = CreateRuntimeStatus(
            "condition-legacy",
            canTargetEnemy: false,
            canTargetAlly: true,
            StatusEffectStackMode.AddAndRefreshDuration,
            operationCount: 0);
        CharacterRuntime character = CreateCharacter(
            CreateBaseCharacterFixture("status-condition-legacy"));
        Assert.That(
            character.ApplyStatusEffect(status, 1f, 1),
            Is.True);

        CharacterNumericCondition condition = new();
        SetPrivateField(
            condition,
            "metric",
            CharacterNumericConditionMetric.StatusStackCount);
        SetPrivateField(
            condition,
            "comparison",
            CharacterNumericComparison.GreaterThanOrEqual);
        SetPrivateField(condition, "threshold", 1f);
        SetPrivateField(condition, "statusEffect", status);

        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True);
    }

    private StatusEffectSO CreateRuntimeStatus(
        string statusId,
        bool canTargetEnemy,
        bool canTargetAlly,
        StatusEffectStackMode stackMode,
        int operationCount)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        status.name = statusId;
        _createdObjects.Add(status);

        SerializedObject serialized = new(status);
        serialized.FindProperty("statusId").stringValue = statusId;
        serialized.FindProperty("canTargetEnemy").boolValue =
            canTargetEnemy;
        serialized.FindProperty("canTargetAlly").boolValue =
            canTargetAlly;
        serialized.FindProperty("durationMode").enumValueIndex =
            (int)StatusEffectDurationMode.Timed;
        serialized.FindProperty("defaultDuration").floatValue = 1f;
        serialized.FindProperty("refreshDurationOnReapply").boolValue = true;
        serialized.FindProperty("tickInterval").floatValue = 1f;
        serialized.FindProperty("stackMode").enumValueIndex =
            (int)stackMode;
        serialized.FindProperty("maximumStacks").intValue = 0;
        serialized.FindProperty("defaultAppliedStacks").intValue = 1;
        serialized.FindProperty("stackRemovalOrder").enumValueIndex =
            (int)StatusEffectStackRemovalOrder.Oldest;
        serialized.FindProperty("removable").boolValue = true;
        serialized.FindProperty("includedInRandomRemoval").boolValue = true;
        serialized.FindProperty("includedInAllRemoval").boolValue = true;
        serialized.FindProperty("operations").arraySize =
            Mathf.Max(0, operationCount);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return status;
    }

    private DungeonPage CreateTutorialDungeonPageForBattleResult()
    {
        DungeonTutorialDefinition tutorial =
            ScriptableObject.CreateInstance<DungeonTutorialDefinition>();
        DungeonDefinition definition =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        definition.name = "TutorialBattleResultDefinition";
        SetPrivateField(definition, "tutorial", tutorial);
        _createdObjects.Add(tutorial);
        _createdObjects.Add(definition);

        GameObject pageObject = new(
            "TutorialBattleResultPage",
            typeof(RectTransform));
        pageObject.SetActive(false);
        _createdObjects.Add(pageObject);
        DungeonPage page = pageObject.AddComponent<DungeonPage>();
        page.RunSession.Begin(
            definition,
            260714,
            1,
            new[] { EDungeonPhase.Battle });
        page.RunSession.SetActivity(EDungeonRunActivity.Battle);
        return page;
    }

    private static void ConfigureStatusRemovalMetadata(
        StatusEffectSO status,
        StatusEffectAlignment alignment,
        bool includedInAllRemoval)
    {
        SerializedObject serialized = new(status);
        serialized.FindProperty("alignment").enumValueIndex =
            (int)alignment;
        serialized.FindProperty("includedInAllRemoval").boolValue =
            includedInAllRemoval;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureRuntimeStatusOperation(
        StatusEffectSO status,
        int operationIndex,
        StatusEffectOperationTrigger trigger,
        StatusEffectOperationType operationType,
        StatusEffectValueMode valueMode,
        float value,
        bool scaleWithStacks)
    {
        SerializedObject serialized = new(status);
        SerializedProperty operation = serialized
            .FindProperty("operations")
            .GetArrayElementAtIndex(operationIndex);
        operation.FindPropertyRelative("trigger").enumValueIndex =
            (int)trigger;
        operation.FindPropertyRelative("operationType").enumValueIndex =
            (int)operationType;
        operation.FindPropertyRelative("valueMode").enumValueIndex =
            (int)valueMode;
        operation.FindPropertyRelative("value").floatValue = value;
        operation.FindPropertyRelative("scaleWithStacks").boolValue =
            scaleWithStacks;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        status.ValidateDefinition();
    }

    private static void ConfigureRuntimeStatusModifier(
        StatusEffectSO status,
        int modifierIndex,
        StatusEffectStatType statType,
        StatusEffectStatModifierMode mode,
        float value,
        bool scaleWithStacks)
    {
        SerializedObject serialized = new(status);
        SerializedProperty modifiers =
            serialized.FindProperty("statModifiers");
        if (modifiers.arraySize <= modifierIndex)
            modifiers.arraySize = modifierIndex + 1;
        SerializedProperty modifier =
            modifiers.GetArrayElementAtIndex(modifierIndex);
        modifier.FindPropertyRelative("statType").enumValueIndex =
            (int)statType;
        modifier.FindPropertyRelative("mode").enumValueIndex =
            (int)mode;
        modifier.FindPropertyRelative("value").floatValue = value;
        modifier.FindPropertyRelative("scaleWithStacks").boolValue =
            scaleWithStacks;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        status.ValidateDefinition();
    }

    private static void ConfigureStatusContributionMultiplier(
        SerializedProperty owner,
        int modifierIndex,
        StatusEffectSO status,
        StatusEffectStatType statType,
        float multiplier,
        float dungeonStageProgressScale = 0f)
    {
        SerializedProperty modifiers = owner.FindPropertyRelative(
            "statusContributionMultipliers");
        if (modifiers.arraySize <= modifierIndex)
            modifiers.arraySize = modifierIndex + 1;

        SerializedProperty modifier =
            modifiers.GetArrayElementAtIndex(modifierIndex);
        modifier.FindPropertyRelative("statusEffect").objectReferenceValue =
            status;
        modifier.FindPropertyRelative("statType").enumValueIndex =
            (int)statType;
        modifier.FindPropertyRelative("multiplier").floatValue =
            multiplier;
        modifier.FindPropertyRelative("dungeonStageProgressScale")
            .floatValue = dungeonStageProgressScale;
    }

    private static void ConfigureRuntimeStatusControl(
        StatusEffectSO status,
        int controlIndex,
        StatusEffectControlType controlType)
    {
        SerializedObject serialized = new(status);
        SerializedProperty controls =
            serialized.FindProperty("controlEffects");
        if (controls.arraySize <= controlIndex)
            controls.arraySize = controlIndex + 1;
        controls.GetArrayElementAtIndex(controlIndex)
            .FindPropertyRelative("controlType")
            .enumValueIndex = (int)controlType;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        status.ValidateDefinition();
    }

    private StatusEffectSO CreateEnemyPeriodicDamageStatus(
        string statusId,
        StatusEffectStackRemovalOrder removalOrder,
        float fixedDamage = 1f,
        bool fixedDamageScalesWithStacks = true,
        float ratioDamage = 0f,
        bool ratioDamageScalesWithStacks = false)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        status.name = statusId;
        _createdObjects.Add(status);

        SerializedObject serialized = new(status);
        serialized.FindProperty("statusId").stringValue = statusId;
        serialized.FindProperty("canTargetEnemy").boolValue = true;
        serialized.FindProperty("canTargetAlly").boolValue = false;
        serialized.FindProperty("durationMode").enumValueIndex =
            (int)StatusEffectDurationMode.Timed;
        serialized.FindProperty("defaultDuration").floatValue = 1f;
        serialized.FindProperty("refreshDurationOnReapply").boolValue = false;
        serialized.FindProperty("tickInterval").floatValue = 1f;
        serialized.FindProperty("stackMode").enumValueIndex =
            (int)StatusEffectStackMode.IndependentDuration;
        serialized.FindProperty("maximumStacks").intValue = 0;
        serialized.FindProperty("defaultAppliedStacks").intValue = 1;
        serialized.FindProperty("stackRemovalOrder").enumValueIndex =
            (int)removalOrder;
        serialized.FindProperty("removable").boolValue = true;
        serialized.FindProperty("includedInRandomRemoval").boolValue = true;
        serialized.FindProperty("includedInAllRemoval").boolValue = true;

        SerializedProperty operations = serialized.FindProperty("operations");
        operations.arraySize = ratioDamage > 0f ? 2 : 1;
        SetPeriodicDamageOperation(
            operations.GetArrayElementAtIndex(0),
            StatusEffectValueMode.Fixed,
            fixedDamage,
            fixedDamageScalesWithStacks);
        if (ratioDamage > 0f)
        {
            SetPeriodicDamageOperation(
                operations.GetArrayElementAtIndex(1),
                StatusEffectValueMode.Ratio,
                ratioDamage,
                ratioDamageScalesWithStacks);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        status.ValidateDefinition();
        return status;
    }

    private static void SetPeriodicDamageOperation(
        SerializedProperty operation,
        StatusEffectValueMode valueMode,
        float value,
        bool scaleWithStacks)
    {
        operation.FindPropertyRelative("trigger").enumValueIndex =
            (int)StatusEffectOperationTrigger.OnTick;
        operation.FindPropertyRelative("operationType").enumValueIndex =
            (int)StatusEffectOperationType.PeriodicDamage;
        operation.FindPropertyRelative("valueMode").enumValueIndex =
            (int)valueMode;
        operation.FindPropertyRelative("value").floatValue = value;
        operation.FindPropertyRelative("scaleWithStacks").boolValue =
            scaleWithStacks;
    }

    private static bool ApplyEnemyStatus(
        EnemyRuntime enemy,
        StatusEffectSO status,
        float duration,
        int stacks,
        IBattleCharacter source,
        float tickInterval)
    {
        return (bool)InvokeEnemyRuntime(
            enemy,
            "ApplyStatusEffect",
            new[]
            {
                typeof(StatusEffectSO),
                typeof(float),
                typeof(int),
                typeof(IBattleCharacter),
                typeof(float),
            },
            status,
            duration,
            stacks,
            source,
            tickInterval);
    }

    private static void SetEnemyHealth(
        EnemyRuntime enemy,
        int health)
    {
        InvokeEnemyRuntime(
            enemy,
            "SetHealth",
            new[] { typeof(int) },
            health);
    }

    private static int TakeEnemyDamage(
        EnemyRuntime enemy,
        int damage,
        CharacterAttackDamageType damageType)
    {
        return (int)InvokeEnemyRuntime(
            enemy,
            "TakeDamage",
            new[]
            {
                typeof(int),
                typeof(CharacterAttackDamageType),
            },
            damage,
            damageType);
    }

    private static bool ApplyEnemyStatus(
        EnemyRuntime enemy,
        StatusEffectSO status,
        float duration,
        int stacks,
        IBattleCharacter source,
        float tickInterval,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        return (bool)InvokeEnemyRuntime(
            enemy,
            "ApplyStatusEffect",
            new[]
            {
                typeof(StatusEffectSO),
                typeof(float),
                typeof(int),
                typeof(IBattleCharacter),
                typeof(float),
                typeof(Func<int, IBattleCharacter, bool>),
            },
            status,
            duration,
            stacks,
            source,
            tickInterval,
            applyDamage);
    }

    private static bool TickEnemyStatuses(
        EnemyRuntime enemy,
        float deltaTime,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        return (bool)InvokeEnemyRuntime(
            enemy,
            "TickStatusEffects",
            new[]
            {
                typeof(float),
                typeof(Func<int, IBattleCharacter, bool>),
            },
            deltaTime,
            applyDamage);
    }

    private static bool HasEnemyStatus(
        EnemyRuntime enemy,
        StatusEffectSO status)
    {
        return (bool)InvokeEnemyRuntime(
            enemy,
            "HasStatusEffect",
            new[] { typeof(StatusEffectSO) },
            status);
    }

    private static int GetEnemyStatusStacks(
        EnemyRuntime enemy,
        StatusEffectSO status)
    {
        return (int)InvokeEnemyRuntime(
            enemy,
            "GetStatusStackCount",
            new[] { typeof(StatusEffectSO) },
            status);
    }

    private static float GetEnemyStatusRemainingDuration(
        EnemyRuntime enemy,
        StatusEffectSO status)
    {
        return (float)InvokeEnemyRuntime(
            enemy,
            "GetStatusRemainingDuration",
            new[] { typeof(StatusEffectSO) },
            status);
    }

    private static int RemoveEnemyStatus(
        EnemyRuntime enemy,
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO status,
        int removalCount)
    {
        return (int)InvokeEnemyRuntime(
            enemy,
            "RemoveStatusEffects",
            new[]
            {
                typeof(CharacterStatusRemovalTarget),
                typeof(StatusEffectSO),
                typeof(int),
            },
            removalTarget,
            status,
            removalCount);
    }

    private static bool TickEnemyAbilityCooldown(
        EnemyRuntime enemy,
        float deltaTime)
    {
        return (bool)InvokeEnemyRuntime(
            enemy,
            "TickAbilityCooldown",
            new[] { typeof(float) },
            deltaTime);
    }

    private static int RemoveEnemyStatus(
        EnemyRuntime enemy,
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO status,
        int removalCount,
        Func<int, IBattleCharacter, bool> applyDamage)
    {
        return (int)InvokeEnemyRuntime(
            enemy,
            "RemoveStatusEffects",
            new[]
            {
                typeof(CharacterStatusRemovalTarget),
                typeof(StatusEffectSO),
                typeof(int),
                typeof(Func<int, IBattleCharacter, bool>),
            },
            removalTarget,
            status,
            removalCount,
            applyDamage);
    }

    private static int RemoveEnemyStatuses(
        EnemyRuntime enemy,
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount)
    {
        return (int)InvokeEnemyRuntime(
            enemy,
            "RemoveStatusEffects",
            new[]
            {
                typeof(CharacterStatusRemovalSelection),
                typeof(CharacterStatusRemovalAmount),
                typeof(Func<int, IBattleCharacter, bool>),
            },
            removalSelection,
            removalAmount,
            null);
    }

    private static void ApplyFire(
        EnemyRuntime enemy,
        float duration,
        float tickInterval,
        int stacks,
        IBattleCharacter source)
    {
        InvokeEnemyRuntime(
            enemy,
            "ApplyFire",
            new[]
            {
                typeof(float),
                typeof(float),
                typeof(int),
                typeof(IBattleCharacter),
            },
            duration,
            tickInterval,
            stacks,
            source);
    }

    private static object InvokeEnemyRuntime(
        EnemyRuntime enemy,
        string methodName,
        Type[] parameterTypes,
        params object[] arguments)
    {
        MethodInfo method = typeof(EnemyRuntime).GetMethod(
            methodName,
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic,
            null,
            parameterTypes,
            null);
        Assert.That(
            method,
            Is.Not.Null,
            $"Missing EnemyRuntime test contract: {methodName}.");
        return method.Invoke(enemy, arguments);
    }

    private CharacterRuntime CreateCharacter(string fixtureId)
    {
        // Runtime tests use an isolated definition. The string only provides
        // a readable fixture name and is never resolved through AssetDatabase.
        return CreateCharacter(CreateBaseCharacterFixture(fixtureId));
    }

    private Sprite CreateTestSprite(Color color)
    {
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixels(new[] { color, color, color, color });
        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(texture);
        _createdObjects.Add(sprite);
        return sprite;
    }

    private static void ConfigureSourceHealthPercentageCondition(
        SerializedProperty owner,
        float maximumPercentage)
    {
        owner.FindPropertyRelative("conditionMatchMode").enumValueIndex =
            (int)CharacterConditionMatchMode.All;
        SerializedProperty conditions =
            owner.FindPropertyRelative("numericConditions");
        conditions.arraySize = 1;
        SerializedProperty condition =
            conditions.GetArrayElementAtIndex(0);
        condition.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterConditionType.Numeric;
        condition.FindPropertyRelative("target").enumValueIndex =
            (int)CharacterConditionTarget.Source;
        condition.FindPropertyRelative("metric").enumValueIndex =
            (int)CharacterNumericConditionMetric.HealthPercentage;
        condition.FindPropertyRelative("comparison").enumValueIndex =
            (int)CharacterNumericComparison.LessThanOrEqual;
        condition.FindPropertyRelative("threshold").floatValue =
            maximumPercentage;
    }

    private CharacterSO CreateBaseCharacterFixture(
        string fixtureName,
        float attackPower = 10f,
        float attackCooldown = 1f)
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = fixtureName;
        _createdObjects.Add(definition);

        Texture2D fixtureTexture =
            new(1, 1, TextureFormat.RGBA32, false);
        fixtureTexture.hideFlags = HideFlags.HideAndDontSave;
        fixtureTexture.SetPixel(0, 0, Color.white);
        fixtureTexture.Apply();
        Sprite fixtureSprite = Sprite.Create(
            fixtureTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f));
        fixtureSprite.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(fixtureTexture);
        _createdObjects.Add(fixtureSprite);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("characterName").stringValue =
            fixtureName;
        serialized.FindProperty("characterDescription").stringValue =
            "Runtime composition test fixture.";
        serialized.FindProperty("maximumHealth").intValue = 100;
        serialized.FindProperty("attackPower").intValue =
            Mathf.RoundToInt(attackPower);
        serialized.FindProperty("attackCooldown").floatValue =
            attackCooldown;
        serialized.FindProperty("attackRecoveryDuration").floatValue = 0f;
        serialized.FindProperty("activeSkillRecoveryDuration").floatValue =
            0f;
        serialized.FindProperty("waitingSdSprite").objectReferenceValue =
            fixtureSprite;
        serialized.FindProperty("attackSdSprite").objectReferenceValue =
            fixtureSprite;
        serialized.FindProperty("damagedSdSprite").objectReferenceValue =
            fixtureSprite;
        serialized.FindProperty("skillSdSprite").objectReferenceValue =
            fixtureSprite;
        serialized.FindProperty("passiveSdSprite").objectReferenceValue =
            fixtureSprite;

        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 1;
        SerializedProperty passive = passives.GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnKill;
        passive.FindPropertyRelative("killSource").enumValueIndex =
            (int)CharacterPassiveKillSource.Self;
        passive.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.None;
        passive.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty passiveEffects =
            passive.FindPropertyRelative("effects");
        passiveEffects.arraySize = 1;
        SerializedProperty passiveEffect =
            passiveEffects.GetArrayElementAtIndex(0);
        passiveEffect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.GainResource;
        passiveEffect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.Source;
        passiveEffect.FindPropertyRelative(
            "damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        passiveEffect.FindPropertyRelative("damageAmount").floatValue = 1f;

        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        attack.FindPropertyRelative("subjectCount").intValue = 1;
        attack.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        attack.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        attack.FindPropertyRelative("damageAmount").floatValue = 1f;
        SerializedProperty attackEffects =
            attack.FindPropertyRelative("effects");
        attackEffects.arraySize = 1;
        ConfigureFixedDamageEffect(
            attackEffects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            1f);

        serialized.FindProperty("skillExecutionPolicy").enumValueIndex =
            (int)CharacterSkillExecutionPolicy.FirstSuccessful;
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = 1;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        skill.FindPropertyRelative("subjectCount").intValue = 1;
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        skill.FindPropertyRelative("damageAmount").floatValue = 1f;
        SerializedProperty skillEffects =
            skill.FindPropertyRelative("effects");
        skillEffects.arraySize = 1;
        ConfigureFixedDamageEffect(
            skillEffects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            1f);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateSuirenFeatureFixture()
    {
        StatusEffectSO emergencyKit =
            LoadAsset<StatusEffectSO>(EmergencyKitAssetPath);
        StatusEffectSO stun = LoadAsset<StatusEffectSO>(StunAssetPath);
        CharacterSO definition = CreateBaseCharacterFixture(
            "CooldownCleanseFeatureFixture",
            1f,
            3f);
        SerializedObject serialized = new(definition);

        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 2;

        SerializedProperty charge = passives.GetArrayElementAtIndex(0);
        SetSections(
            charge.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        charge.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnCooldown;
        charge.FindPropertyRelative("cooldown").floatValue = 10f;
        charge.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.None;
        charge.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        charge.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty chargeEffects =
            charge.FindPropertyRelative("effects");
        chargeEffects.arraySize = 1;
        ConfigureApplyStatusEffect(
            chargeEffects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            emergencyKit,
            0.1f,
            1f);

        SerializedProperty cleanse = passives.GetArrayElementAtIndex(1);
        SetSections(
            cleanse.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.SelfStatusCost,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        cleanse.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnStatusAcquired;
        cleanse.FindPropertyRelative("statusTarget").enumValueIndex =
            (int)CharacterPassiveStatusTarget.Ally;
        cleanse.FindPropertyRelative(
            "triggerStatusEffect").objectReferenceValue = stun;
        cleanse.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.None;
        cleanse.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        cleanse.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        SerializedProperty selfCost =
            cleanse.FindPropertyRelative("selfStatusCost");
        selfCost.FindPropertyRelative("statusEffect").objectReferenceValue =
            emergencyKit;
        selfCost.FindPropertyRelative("requiredStacks").intValue = 1;
        selfCost.FindPropertyRelative("consumedStacks").intValue = 1;
        SerializedProperty cleanseEffects =
            cleanse.FindPropertyRelative("effects");
        cleanseEffects.arraySize = 1;
        ConfigureRemoveStatusEffect(
            cleanseEffects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            stun,
            1);

        ConfigureFirstSkill(
            serialized,
            CharacterAttackSubject.LowestValue,
            CharacterAttackSubjectMetric.Health,
            1,
            CharacterDamageAmountMode.Fixed,
            2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateAislingFeatureFixture()
    {
        StatusEffectSO opening =
            LoadAsset<StatusEffectSO>(OpeningAssetPath);
        CharacterSO definition = CreateBaseCharacterFixture(
            "PreviousTargetStatusFeatureFixture",
            7f,
            4f);
        SerializedObject serialized = new(definition);
        SerializedProperty attack = serialized
            .FindProperty("attackDefinitions")
            .GetArrayElementAtIndex(0);
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.LowestValue;
        attack.FindPropertyRelative("subjectMetric").enumValueIndex =
            (int)CharacterAttackSubjectMetric.Health;
        ConfigureFirstSkill(
            serialized,
            CharacterAttackSubject.None,
            CharacterAttackSubjectMetric.Health,
            2,
            CharacterDamageAmountMode.Ratio,
            2.5f,
            opening);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateSaenaFeatureFixture()
    {
        CharacterSO definition = CreateBaseCharacterFixture(
            "SourceAttackPowerFeatureFixture",
            5f,
            2f);
        SerializedObject serialized = new(definition);
        ConfigureFirstSkill(
            serialized,
            CharacterAttackSubject.None,
            CharacterAttackSubjectMetric.Health,
            3,
            CharacterDamageAmountMode.Ratio,
            3f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateMirinaeFeatureFixture()
    {
        StatusEffectSO starPowder =
            LoadAsset<StatusEffectSO>(StarPowderAssetPath);
        CharacterSO definition = CreateBaseCharacterFixture(
            "KillRewardSequenceFeatureFixture",
            3f,
            3f);
        SerializedObject serialized = new(definition);

        SerializedProperty passive = serialized
            .FindProperty("passiveDefinitions")
            .GetArrayElementAtIndex(0);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnKill;
        passive.FindPropertyRelative("killSource").enumValueIndex =
            (int)CharacterPassiveKillSource.All;
        SerializedProperty passiveEffects =
            passive.FindPropertyRelative("effects");
        passiveEffects.arraySize = 2;
        SerializedProperty gain =
            passiveEffects.GetArrayElementAtIndex(0);
        gain.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.GainResource;
        gain.FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.Source;
        gain.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        gain.FindPropertyRelative("damageAmount").floatValue = 1f;
        ConfigureApplyStatusEffect(
            passiveEffects.GetArrayElementAtIndex(1),
            CharacterEffectTargetMode.Source,
            starPowder,
            1f,
            1f);

        serialized.FindProperty("skillExecutionPolicy").enumValueIndex =
            (int)CharacterSkillExecutionPolicy.SequenceAll;
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 2;
        ConfigureSkillStep(
            skills.GetArrayElementAtIndex(0),
            CharacterAttackSubject.None,
            1,
            CharacterActionLinkage.None,
            false,
            starPowder);
        ConfigureSkillStep(
            skills.GetArrayElementAtIndex(1),
            CharacterAttackSubject.None,
            1,
            CharacterActionLinkage.SimultaneousWithPreviousAttack,
            true,
            starPowder);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void ConfigureFirstSkill(
        SerializedObject serialized,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int cost,
        CharacterDamageAmountMode amountMode,
        float amount,
        StatusEffectSO appliedStatus = null)
    {
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = cost;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)subject;
        skill.FindPropertyRelative("subjectMetric").enumValueIndex =
            (int)metric;
        skill.FindPropertyRelative("subjectCount").intValue = 1;
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)amountMode;
        skill.FindPropertyRelative("damageAmount").floatValue = amount;
        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = appliedStatus == null ? 1 : 2;
        SerializedProperty damage = effects.GetArrayElementAtIndex(0);
        ConfigureDamageEffect(
            damage,
            CharacterEffectTargetMode.InheritAction,
            amountMode,
            amount);
        if (appliedStatus != null)
        {
            ConfigureApplyStatusEffect(
                effects.GetArrayElementAtIndex(1),
                CharacterEffectTargetMode.InheritAction,
                appliedStatus,
                10f,
                1f);
        }
    }

    private static void ConfigureSkillStep(
        SerializedProperty skill,
        CharacterAttackSubject subject,
        int cost,
        CharacterActionLinkage linkage,
        bool requiresTwelveStacks,
        StatusEffectSO sourceStatus)
    {
        if (requiresTwelveStacks)
        {
            SetSections(
                skill.FindPropertyRelative("sections"),
                (int)CharacterSkillSectionType.Cost,
                (int)CharacterSkillSectionType.Linkage,
                (int)CharacterSkillSectionType.Subject,
                (int)CharacterSkillSectionType.Ability,
                (int)CharacterSkillSectionType.Condition);
        }
        else
        {
            SetSections(
                skill.FindPropertyRelative("sections"),
                (int)CharacterSkillSectionType.Cost,
                (int)CharacterSkillSectionType.Linkage,
                (int)CharacterSkillSectionType.Subject,
                (int)CharacterSkillSectionType.Ability);
        }
        skill.FindPropertyRelative("cost").intValue = cost;
        skill.FindPropertyRelative("linkage").enumValueIndex =
            (int)linkage;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)subject;
        skill.FindPropertyRelative("subjectCount").intValue = 1;
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        skill.FindPropertyRelative("damageAmount").floatValue = 1f;

        SerializedProperty conditions =
            skill.FindPropertyRelative("numericConditions");
        conditions.arraySize = requiresTwelveStacks ? 1 : 0;
        if (requiresTwelveStacks)
        {
            SerializedProperty condition =
                conditions.GetArrayElementAtIndex(0);
            condition.FindPropertyRelative("type").enumValueIndex =
                (int)CharacterConditionType.Numeric;
            condition.FindPropertyRelative("target").enumValueIndex =
                (int)CharacterConditionTarget.Source;
            condition.FindPropertyRelative("metric").enumValueIndex =
                (int)CharacterNumericConditionMetric.StatusStackCount;
            condition.FindPropertyRelative("comparison").enumValueIndex =
                (int)CharacterNumericComparison.GreaterThanOrEqual;
            condition.FindPropertyRelative("threshold").floatValue = 12f;
            condition.FindPropertyRelative(
                "statusEffect").objectReferenceValue = sourceStatus;
        }

        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = requiresTwelveStacks ? 2 : 1;
        ConfigureFixedDamageEffect(
            effects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            1f);
        if (requiresTwelveStacks)
        {
            ConfigureRemoveStatusEffect(
                effects.GetArrayElementAtIndex(1),
                CharacterEffectTargetMode.Source,
                sourceStatus,
                12);
        }
    }

    private static void ConfigureDamageEffect(
        SerializedProperty effect,
        CharacterEffectTargetMode targetMode,
        CharacterDamageAmountMode amountMode,
        float amount)
    {
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)targetMode;
        effect.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)amountMode;
        effect.FindPropertyRelative("damageAmount").floatValue = amount;
    }

    private static void ConfigureApplyStatusEffect(
        SerializedProperty effect,
        CharacterEffectTargetMode targetMode,
        StatusEffectSO status,
        float duration,
        float stacks)
    {
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.ApplyStatus;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)targetMode;
        effect.FindPropertyRelative("statusEffect").objectReferenceValue =
            status;
        effect.FindPropertyRelative("statusDuration").floatValue = duration;
        effect.FindPropertyRelative("statusStacks").floatValue = stacks;
    }

    private static void ConfigureAttackTargetRelationPassive(
        SerializedProperty passive,
        CharacterPassiveAttackTargetRelation relation,
        StatusEffectSO reward)
    {
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Condition,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnAttack;
        passive.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.PreviousAttackSucceeded;
        passive.FindPropertyRelative(
            "attackTargetRelation").enumValueIndex = (int)relation;
        passive.FindPropertyRelative("numericConditions").ClearArray();
        passive.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty effects = passive.FindPropertyRelative("effects");
        effects.arraySize = 1;
        ConfigureApplyStatusEffect(
            effects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.Source,
            reward,
            100f,
            1f);
    }

    private static void ConfigureRemoveStatusEffect(
        SerializedProperty effect,
        CharacterEffectTargetMode targetMode,
        StatusEffectSO status,
        int removalCount)
    {
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.RemoveStatus;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)targetMode;
        effect.FindPropertyRelative("statusEffect").objectReferenceValue =
            status;
        effect.FindPropertyRelative("statusRemovalTarget").enumValueIndex =
            (int)CharacterStatusRemovalTarget.Single;
        effect.FindPropertyRelative("statusRemovalCount").intValue =
            removalCount;
    }

    private CharacterRuntime CreateCharacter(CharacterSO definition)
    {
        GameObject prefab = Resources.Load<GameObject>(
            "Presentation/CharacterInfo");
        Assert.That(
            prefab,
            Is.Not.Null,
            "Character info prefab is missing from Resources/Presentation.");
        GameObject root = UnityEngine.Object.Instantiate(prefab);
        root.name = $"Test_{definition.name}";
        _createdObjects.Add(root);

        CharacterRuntime character =
            root.GetComponent<CharacterRuntime>();
        Assert.That(character, Is.Not.Null);

        Assert.That(
            character.ConfigureDefinition(definition),
            Is.True,
            $"Failed to initialize {definition.name}.");
        _characters.Add(character);
        return character;
    }

    private CharacterSO CreateKillPassiveCharacter(
        CharacterPassiveKillSource killSource,
        StatusEffectSO rewardStatus,
        CharacterSO specifiedKiller = null)
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = $"KillPassive_{killSource}";
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("characterName").stringValue =
            definition.name;
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 1;
        SerializedProperty passive = passives.GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnKill;
        passive.FindPropertyRelative("killSource").enumValueIndex =
            (int)killSource;
        passive.FindPropertyRelative(
            "specifiedKillerCharacter").objectReferenceValue =
            specifiedKiller;
        passive.FindPropertyRelative("linkage").enumValueIndex =
            (int)CharacterActionLinkage.None;
        passive.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        passive.FindPropertyRelative("subjectCount").intValue = 1;
        passive.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.StatusEffect;
        passive.FindPropertyRelative("statusEffect").objectReferenceValue =
            rewardStatus;
        passive.FindPropertyRelative("statusDuration").floatValue = 5f;
        passive.FindPropertyRelative("statusStacks").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateExplicitDamageAndStatusCharacter(
        StatusEffectSO statusEffect)
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = "ExplicitDamageAndStatusCharacter";
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("nameLocalizationKey").stringValue =
            "character.suiren.name";
        serialized.FindProperty("descriptionLocalizationKey").stringValue =
            "character.suiren.description";
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = 2;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        skill.FindPropertyRelative("subjectCount").intValue = 1;

        // Explicit execution uses the list. Legacy values stay usable for
        // compatibility with the action preparation gate.
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        skill.FindPropertyRelative("damageAmount").floatValue = 1f;

        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 2;
        SerializedProperty damage = effects.GetArrayElementAtIndex(0);
        damage.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        damage.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        damage.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        damage.FindPropertyRelative("damageAmount").floatValue = 4f;

        SerializedProperty status = effects.GetArrayElementAtIndex(1);
        status.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.ApplyStatus;
        status.FindPropertyRelative("statusEffect").objectReferenceValue =
            statusEffect;
        status.FindPropertyRelative("statusDuration").floatValue = 3f;
        status.FindPropertyRelative("statusStacks").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateResourceGainCharacter(
        float fixedAmount,
        float sourceResourceScale,
        int targetCount)
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = "ResourceGainCharacter";
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("nameLocalizationKey").stringValue =
            "character.suiren.name";
        serialized.FindProperty("descriptionLocalizationKey").stringValue =
            "character.suiren.description";
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = 2;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        skill.FindPropertyRelative("subjectCount").intValue =
            Mathf.Max(1, targetCount);

        // Legacy fields remain valid for compatibility with shared editor
        // and preview paths; explicit execution reads the effect below.
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        skill.FindPropertyRelative("damageAmount").floatValue = 1f;

        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty gain = effects.GetArrayElementAtIndex(0);
        gain.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.GainResource;
        gain.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        gain.FindPropertyRelative("damageAmount").floatValue =
            fixedAmount;
        gain.FindPropertyRelative("sourceResourceScale").floatValue =
            sourceResourceScale;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateResourceSpendCharacter(
        params float[] amounts)
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 1f,
            sourceResourceScale: 0f,
            targetCount: 1);
        definition.name = "ResourceSpendCharacter";

        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        int effectCount = amounts?.Length ?? 0;
        effects.arraySize = effectCount;
        for (int index = 0; index < effectCount; index++)
        {
            ConfigureFixedResourceSpendEffect(
                effects.GetArrayElementAtIndex(index),
                amounts[index]);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void ConfigureFixedResourceSpendEffect(
        SerializedProperty effect,
        float amount)
    {
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.SpendResource;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.InheritAction;
        effect.FindPropertyRelative(
            "preconditionFailurePolicy").enumValueIndex =
            (int)CharacterEffectPreconditionFailurePolicy.AbortAction;
        effect.FindPropertyRelative("failurePolicy").enumValueIndex =
            (int)CharacterEffectFailurePolicy.Continue;
        effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        effect.FindPropertyRelative("damageAmount").floatValue = amount;
        effect.FindPropertyRelative("sourceResourceScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "targetCurrentHealthScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "targetMaxHealthScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "sourceStatusStacksScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "targetStatusStacksScale").floatValue = 0f;
    }

    private CharacterSO CreateHealCharacter(
        CharacterTargetFaction faction,
        CharacterAttackSubject subject,
        float amount)
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 1f,
            sourceResourceScale: 0f,
            targetCount: 1);
        definition.name = "HealCharacter";

        SerializedObject serialized = new(definition);
        serialized.FindProperty("maximumHealth").intValue = 10;
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)faction;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)subject;
        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 1;
        ConfigureHealEffect(
            effects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            amount);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateHealthSpendCharacter(
        params float[] amounts)
    {
        CharacterSO definition =
            CreateResourceSpendCharacter(amounts);
        definition.name = "HealthSpendCharacter";

        SerializedObject serialized = new(definition);
        serialized.FindProperty("maximumHealth").intValue = 10;
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        for (int index = 0; index < effects.arraySize; index++)
        {
            ConfigureFixedHealthSpendEffect(
                effects.GetArrayElementAtIndex(index),
                amounts[index]);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void ConfigureHealEffect(
        SerializedProperty effect,
        CharacterEffectTargetMode targetMode,
        float amount)
    {
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Heal;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)targetMode;
        effect.FindPropertyRelative(
            "preconditionFailurePolicy").enumValueIndex =
            (int)CharacterEffectPreconditionFailurePolicy.AbortAction;
        effect.FindPropertyRelative("failurePolicy").enumValueIndex =
            (int)CharacterEffectFailurePolicy.Continue;
        effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        effect.FindPropertyRelative("damageAmount").floatValue = amount;
        effect.FindPropertyRelative("sourceResourceScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "targetCurrentHealthScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "targetMaxHealthScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "sourceStatusStacksScale").floatValue = 0f;
        effect.FindPropertyRelative(
            "targetStatusStacksScale").floatValue = 0f;
    }

    private static void ConfigureFixedHealthSpendEffect(
        SerializedProperty effect,
        float amount)
    {
        ConfigureFixedResourceSpendEffect(effect, amount);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.SpendHealth;
    }

    private CharacterSO CreateShieldCharacter(
        CharacterTargetFaction faction,
        CharacterAttackSubject subject,
        params float[] amounts)
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 1f,
            sourceResourceScale: 0f,
            targetCount: 1);
        definition.name = "ShieldCharacter";

        SerializedObject serialized = new(definition);
        serialized.FindProperty("maximumHealth").intValue = 10;
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)faction;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)subject;
        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        int effectCount = amounts?.Length ?? 0;
        effects.arraySize = effectCount;
        for (int index = 0; index < effectCount; index++)
        {
            ConfigureShieldEffect(
                effects.GetArrayElementAtIndex(index),
                CharacterEffectTargetMode.InheritAction,
                amounts[index]);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void ConfigureShieldEffect(
        SerializedProperty effect,
        CharacterEffectTargetMode targetMode,
        float amount)
    {
        ConfigureHealEffect(effect, targetMode, amount);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Shield;
    }

    private CharacterSO CreateCumulativeUpgradeCharacter()
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 1f,
            sourceResourceScale: 0f,
            targetCount: 1);
        definition.name = "CumulativeUpgradeCharacter";

        SerializedObject serialized = new(definition);
        serialized.FindProperty("maximumHealth").intValue = 10;
        serialized.FindProperty("attackPower").intValue = 10;
        serialized.FindProperty("attackCooldown").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void SetCharacterInitiallyOwned(
        CharacterSO definition,
        bool initiallyOwned)
    {
        SerializedObject serialized = new(definition);
        serialized.FindProperty("initiallyOwned").boolValue =
            initiallyOwned;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCumulativeUpgradeDefinition(
        CharacterSO definition,
        int definitionIndex,
        string upgradeId,
        int maxLevel,
        params (
            CharacterCumulativeUpgradeModifierType Type,
            float Value)[] modifiers)
    {
        SerializedObject serialized = new(definition);
        SerializedProperty definitions = serialized.FindProperty(
            "cumulativeUpgradeDefinitions");
        definitions.arraySize = Mathf.Max(
            definitions.arraySize,
            definitionIndex + 1);
        SerializedProperty upgrade =
            definitions.GetArrayElementAtIndex(definitionIndex);
        upgrade.FindPropertyRelative("upgradeId").stringValue =
            upgradeId ?? string.Empty;
        upgrade.FindPropertyRelative("maxLevel").intValue = maxLevel;
        SerializedProperty modifierList =
            upgrade.FindPropertyRelative("modifiers");
        int modifierCount = modifiers?.Length ?? 0;
        modifierList.arraySize = modifierCount;
        for (int index = 0; index < modifierCount; index++)
        {
            SerializedProperty modifier =
                modifierList.GetArrayElementAtIndex(index);
            modifier.FindPropertyRelative("type").enumValueIndex =
                (int)modifiers[index].Type;
            modifier.FindPropertyRelative("valuePerLevel").floatValue =
                modifiers[index].Value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private CharacterSO CreateSourceRetargetCharacter(
        StatusEffectSO sourceStatus)
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = "SourceRetargetCharacter";
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("nameLocalizationKey").stringValue =
            "character.suiren.name";
        serialized.FindProperty("descriptionLocalizationKey").stringValue =
            "character.suiren.description";
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = 2;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        skill.FindPropertyRelative("subjectCount").intValue = 1;

        // Legacy values remain valid for the shared preparation path.
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        skill.FindPropertyRelative("damageAmount").floatValue = 1f;

        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 2;

        SerializedProperty applyToSource =
            effects.GetArrayElementAtIndex(0);
        applyToSource.FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.Source;
        applyToSource.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.ApplyStatus;
        applyToSource.FindPropertyRelative(
            "statusEffect").objectReferenceValue = sourceStatus;
        applyToSource.FindPropertyRelative(
            "statusDuration").floatValue = 1f;
        applyToSource.FindPropertyRelative(
            "statusStacks").floatValue = 1f;

        SerializedProperty inheritedDamage =
            effects.GetArrayElementAtIndex(1);
        inheritedDamage.FindPropertyRelative(
            "targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.InheritAction;
        inheritedDamage.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        inheritedDamage.FindPropertyRelative(
            "damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        inheritedDamage.FindPropertyRelative(
            "damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        inheritedDamage.FindPropertyRelative(
            "damageAmount").floatValue = 0f;
        inheritedDamage.FindPropertyRelative(
            "sourceResourceScale").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateTargetlessSourceCharacter(
        StatusEffectSO sourceStatus)
    {
        CharacterSO definition =
            CreateSourceRetargetCharacter(sourceStatus);
        definition.name = "TargetlessSourceCharacter";

        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        skill.FindPropertyRelative("effects").arraySize = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateAllySelfCharacter(
        StatusEffectSO sourceStatus)
    {
        CharacterSO definition =
            CreateTargetlessSourceCharacter(sourceStatus);
        definition.name = "AllySelfCharacter";

        SerializedObject serialized = new(definition);
        SerializedProperty skill = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0);
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        skill.FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.InheritAction;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateFreshSelectionCharacter()
    {
        CharacterSO definition = CreateResourceGainCharacter(
            fixedAmount: 1f,
            sourceResourceScale: 0f,
            targetCount: 1);
        definition.name = "FreshSelectionCharacter";

        SerializedObject serialized = new(definition);
        SerializedProperty effects = serialized
            .FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects");
        effects.arraySize = 2;

        ConfigureFixedDamageEffect(
            effects.GetArrayElementAtIndex(0),
            CharacterEffectTargetMode.InheritAction,
            2f);
        SerializedProperty freshDamage =
            effects.GetArrayElementAtIndex(1);
        ConfigureFixedDamageEffect(
            freshDamage,
            CharacterEffectTargetMode.FreshSelection,
            3f);
        SerializedProperty selector = freshDamage.FindPropertyRelative(
            "targetSelector");
        selector.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        selector.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        selector.FindPropertyRelative("subjectMetric").enumValueIndex =
            (int)CharacterAttackSubjectMetric.Health;
        selector.FindPropertyRelative("subjectCount").intValue = 1;
        selector.FindPropertyRelative("conditionMatchMode").enumValueIndex =
            (int)CharacterConditionMatchMode.All;
        selector.FindPropertyRelative("numericConditions").ClearArray();
        selector.FindPropertyRelative("areaOffsets").ClearArray();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void ConfigureFixedDamageEffect(
        SerializedProperty effect,
        CharacterEffectTargetMode targetMode,
        float amount)
    {
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)targetMode;
        effect.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        effect.FindPropertyRelative("damageAmount").floatValue = amount;
        effect.FindPropertyRelative("sourceResourceScale").floatValue = 0f;
        effect.FindPropertyRelative("targetCurrentHealthScale").floatValue =
            0f;
        effect.FindPropertyRelative("targetMaxHealthScale").floatValue = 0f;
        effect.FindPropertyRelative("sourceStatusStacksScale").floatValue =
            0f;
        effect.FindPropertyRelative("targetStatusStacksScale").floatValue =
            0f;
    }

    private static void SetFirstSkillSubject(
        CharacterSO definition,
        CharacterAttackSubject subject)
    {
        SerializedObject serialized = new(definition);
        serialized.FindProperty("skillDefinitions")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("subject").enumValueIndex =
            (int)subject;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private CharacterSO CreateTargetScalingCharacter(
        StatusEffectSO targetStatus,
        StatusEffectSO sourceStatus)
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = "TargetScalingCharacter";
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("nameLocalizationKey").stringValue =
            "character.suiren.name";
        serialized.FindProperty("descriptionLocalizationKey").stringValue =
            "character.suiren.description";
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = 1;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        skill.FindPropertyRelative("subjectCount").intValue = 2;
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        skill.FindPropertyRelative("damageAmount").floatValue = 1f;

        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 2;
        SerializedProperty applyStatus =
            effects.GetArrayElementAtIndex(0);
        applyStatus.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.ApplyStatus;
        applyStatus.FindPropertyRelative(
            "statusEffect").objectReferenceValue = targetStatus;
        applyStatus.FindPropertyRelative("statusDuration").floatValue = 3f;
        applyStatus.FindPropertyRelative("statusStacks").floatValue = 2f;

        SerializedProperty damage = effects.GetArrayElementAtIndex(1);
        damage.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        damage.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        damage.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        damage.FindPropertyRelative("damageAmount").floatValue = 0f;
        damage.FindPropertyRelative(
            "targetCurrentHealthScale").floatValue = -0.5f;
        damage.FindPropertyRelative(
            "targetMaxHealthScale").floatValue = 0.5f;
        damage.FindPropertyRelative(
            "sourceStatusScalingEffect").objectReferenceValue =
            sourceStatus;
        damage.FindPropertyRelative(
            "sourceStatusStacksScale").floatValue = 1f;
        damage.FindPropertyRelative(
            "targetStatusScalingEffect").objectReferenceValue =
            targetStatus;
        damage.FindPropertyRelative(
            "targetStatusStacksScale").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private CharacterSO CreateTargetOnlyScalingCharacter()
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = "TargetOnlyScalingCharacter";
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("nameLocalizationKey").stringValue =
            "character.suiren.name";
        serialized.FindProperty("descriptionLocalizationKey").stringValue =
            "character.suiren.description";
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = 1;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        skill.FindPropertyRelative("subjectCount").intValue = 1;
        skill.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        skill.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        skill.FindPropertyRelative("damageAmount").floatValue = 1f;

        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty damage = effects.GetArrayElementAtIndex(0);
        damage.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        damage.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        damage.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        damage.FindPropertyRelative("damageAmount").floatValue = 0f;
        damage.FindPropertyRelative(
            "targetCurrentHealthScale").floatValue = 0.25f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void SetSections(
        SerializedProperty sections,
        params int[] values)
    {
        sections.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            sections.GetArrayElementAtIndex(index).enumValueIndex =
                values[index];
        }
    }

    private static void AssertEffectsPreserveTargetDefault(
        IReadOnlyList<CharacterEffectDefinition> effects,
        string actionPath,
        ref int explicitEffectCount)
    {
        if (effects == null)
            return;

        for (int index = 0; index < effects.Count; index++)
        {
            CharacterEffectDefinition effect = effects[index];
            Assert.That(
                effect,
                Is.Not.Null,
                $"{actionPath}.effects[{index}] is null.");
            bool isIntentionalSourceStatusEffect =
                effect.TargetMode == CharacterEffectTargetMode.Source &&
                (effect.Type == CharacterEffectType.ApplyStatus ||
                 effect.Type == CharacterEffectType.RemoveStatus) &&
                effect.StatusEffect != null;
            Assert.That(
                effect.TargetMode == CharacterEffectTargetMode.InheritAction ||
                isIntentionalSourceStatusEffect,
                Is.True,
                $"{actionPath}.effects[{index}] changed the legacy " +
                "serialized target default without a supported explicit " +
                "source-status override.");
            explicitEffectCount++;
        }
    }

    private static bool HasDiagnostic(
        CharacterDefinitionValidationResult result,
        string code)
    {
        foreach (CharacterDefinitionDiagnostic diagnostic in
                 result.Diagnostics)
        {
            if (string.Equals(
                    diagnostic.Code,
                    code,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName)
    {
        GameObject textObject = new(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<TextMeshProUGUI>();
    }

    private EnemyRuntime CreateEnemyRuntime(
        int maximumHealth = 20,
        float initialArmorMultiplier = 0f,
        float abilityCooldown = 0f)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        SerializedObject serialized = new(definition);
        if (abilityCooldown > 0f)
        {
            SerializedProperty abilities =
                serialized.FindProperty("abilities");
            abilities.arraySize = 1;
            SerializedProperty ability =
                abilities.GetArrayElementAtIndex(0);
            ability.FindPropertyRelative("abilityId").stringValue =
                "test_enemy_cooldown";
            ability.FindPropertyRelative("fallbackName").stringValue =
                "Test Enemy Cooldown";
            ability.FindPropertyRelative("trigger").enumValueIndex =
                (int)EnemyAbilityTrigger.OnCooldown;
            ability.FindPropertyRelative("cooldown").floatValue =
                abilityCooldown;
            ability.FindPropertyRelative(
                "pauseCooldownWhileDisabled").boolValue = true;
            SerializedProperty target =
                ability.FindPropertyRelative("target");
            target.FindPropertyRelative("faction").enumValueIndex =
                (int)EnemyAbilityTargetFaction.Self;
            target.FindPropertyRelative("subject").enumValueIndex =
                (int)EnemyAbilityTargetSubject.Self;
            SerializedProperty operations =
                ability.FindPropertyRelative("operations");
            operations.arraySize = 1;
            SerializedProperty operation =
                operations.GetArrayElementAtIndex(0);
            operation.FindPropertyRelative("type").enumValueIndex =
                (int)EnemyAbilityOperationType.ExecuteEffects;
            operation.FindPropertyRelative("enabled").boolValue = true;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyRuntime runtime =
            new EnemyRuntime(definition, maximumHealth);
        if (initialArmorMultiplier > 0f)
        {
            int armor = Mathf.Max(
                0,
                Mathf.RoundToInt(
                    runtime.MaxHealth * initialArmorMultiplier));
            InvokeEnemyRuntime(
                runtime,
                "GainArmor",
                new[] { typeof(int) },
                armor);
        }
        return runtime;
    }

    private static GameObject CreateFlowTab(
        GameObject root,
        string name)
    {
        GameObject tab = new(name);
        tab.transform.SetParent(root.transform, false);
        return tab;
    }

    private static T LoadAsset<T>(string assetPath)
        where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        Assert.That(asset, Is.Not.Null, $"Missing test asset: {assetPath}");
        return asset;
    }

    private sealed class FakeActiveSkillResource : IActiveSkillResource
    {
        public int Current { get; private set; }
        public int Maximum { get; }
        public int TrySpendCallCount { get; private set; }
        public int TryGainCallCount { get; private set; }
        public event Action<int> Changed;

        public FakeActiveSkillResource(
            int current,
            int maximum = 10)
        {
            Maximum = Mathf.Max(1, maximum);
            Current = Mathf.Clamp(current, 0, Maximum);
        }

        public bool CanSpend(int amount)
        {
            return amount > 0 && Current >= amount;
        }

        public bool TrySpend(int amount)
        {
            TrySpendCallCount++;
            if (!CanSpend(amount))
                return false;

            Current -= amount;
            Changed?.Invoke(Current);
            return true;
        }

        public bool TryGain(int amount)
        {
            TryGainCallCount++;
            if (amount <= 0 || Current >= Maximum)
                return false;

            int previous = Current;
            Current = Mathf.Min(Maximum, Current + amount);
            if (Current == previous)
                return false;

            Changed?.Invoke(Current);
            return true;
        }
    }

    private sealed class FakeBattleBoard :
        IBattleBoard,
        IDungeonStageProgressProvider,
        IBattleManualTargetSelectionService
    {
        private EnemyRuntime _centerTarget;
        private EnemyRuntime _crossTarget;

        public int InitialEnemyCapacity => 9;
        public float DungeonStageProgress { get; set; }
        public int LivingEnemyCount => LivingEnemyCountValue;
        public bool HasEmptyEnemyTile => false;
        public int LivingEnemyCountValue { get; set; }
        public bool SimulateAislingAreaSequence { get; set; }
        public bool ReturnCenterTargetsForAreaExpansion { get; set; }
        public bool ApplyEffectsToEnemyRuntime { get; set; }
        public bool ForceStatusApplyFailure { get; set; }
        public Action<EnemyRuntime, int> TargetDamageApplied { get; set; }
        public bool CenterTargetAlive { get; private set; } = true;
        public EnemyRuntime DiagonalTarget { get; private set; }
        public EnemyRuntime ExposedCenterTarget { get; private set; }
        public int AreaExpansionCallCount { get; private set; }
        public int AreaExpansionCountAtFirstDamage { get; private set; }
        public int AlliedStatusRemovalCallCount { get; private set; }
        public int CharacterTargetSelectionCallCount { get; private set; }
        public int FilterCharacterTargetCallCount { get; private set; }
        public int AlliedCharacterTargetSelectionCallCount
            { get; private set; }
        public Queue<IReadOnlyList<EnemyRuntime>> PlannedEnemySelections
            { get; } = new();
        public HashSet<EnemyRuntime> InvalidEnemyTargets { get; } = new();
        public List<int> SelectionNumericConditionCounts
            { get; } = new();
        public List<CharacterAttackSubject> CharacterTargetSelectionSubjects
            { get; } = new();
        public List<CharacterAttackSubjectMetric>
            CharacterTargetSelectionMetrics { get; } = new();
        public List<int> CharacterTargetSelectionCounts { get; } = new();
        public IReadOnlyList<EnemyRuntime> SelectedEnemyTargets
            { get; set; } = Array.Empty<EnemyRuntime>();
        public IReadOnlyList<IBattleCharacter>
            LastAlliedStatusRemovalTargets { get; private set; } =
                Array.Empty<IBattleCharacter>();
        public List<IReadOnlyList<EnemyRuntime>> DamageTargetSnapshots
            { get; } = new();
        public List<int> DamageAmounts { get; } = new();
        public List<bool> DamageShowAttackRangeSnapshots { get; } = new();
        public List<IReadOnlyList<EnemyRuntime>> StatusTargetSnapshots
            { get; } = new();
        public List<StatusEffectSO> AppliedStatuses { get; } = new();
        public int StatusApplyCallCount { get; private set; }
        public bool IsManualTargetSelectionPending =>
            CurrentManualTargetRequest != null;
        public BattleManualTargetSelectionRequest
            CurrentManualTargetRequest { get; private set; }
        public int CurrentManualSelectedCount { get; private set; }

        public event Action<BattleEnemyDefeatedEvent> EnemyDefeated;
        public event Action<BattleStatusAppliedEvent> StatusApplied;
        public event Action OccupancyChanged
        {
            add { }
            remove { }
        }
        public event Action<bool> ManualTargetSelectionPendingChanged;
        public event Action ManualTargetSelectionProgressChanged;

        public void ConfigureAislingTargets(
            EnemyRuntime centerTarget,
            EnemyRuntime crossTarget,
            EnemyRuntime diagonalTarget,
            EnemyRuntime exposedCenterTarget)
        {
            _centerTarget = centerTarget;
            _crossTarget = crossTarget;
            DiagonalTarget = diagonalTarget;
            ExposedCenterTarget = exposedCenterTarget;
            CenterTargetAlive = true;
        }

        public void RaiseStatusApplied(BattleStatusAppliedEvent eventData)
        {
            NotifyStatusApplied(eventData);
        }

        public void RaiseEnemyDefeated(BattleEnemyDefeatedEvent eventData)
        {
            EnemyDefeated?.Invoke(eventData);
        }

        public void NotifyStatusApplied(BattleStatusAppliedEvent eventData)
        {
            StatusApplied?.Invoke(eventData);
        }

        public bool TryBeginManualTargetSelection(
            BattleManualTargetSelectionRequest request)
        {
            if (request == null ||
                request.RequiredCount <= 0 ||
                IsManualTargetSelectionPending)
            {
                return false;
            }

            CurrentManualTargetRequest = request;
            CurrentManualSelectedCount = 0;
            ManualTargetSelectionProgressChanged?.Invoke();
            ManualTargetSelectionPendingChanged?.Invoke(true);
            return true;
        }

        public void CompleteManualEnemyTargets(
            params EnemyRuntime[] targets)
        {
            BattleManualTargetSelectionRequest request =
                CurrentManualTargetRequest;
            Assert.That(request, Is.Not.Null);
            Assert.That(
                request.Faction,
                Is.EqualTo(CharacterTargetFaction.Enemy));
            CurrentManualTargetRequest = null;
            CurrentManualSelectedCount = 0;
            ManualTargetSelectionProgressChanged?.Invoke();
            ManualTargetSelectionPendingChanged?.Invoke(false);
            request.Complete(new BattleManualTargetSelectionResult(
                CharacterTargetFaction.Enemy,
                targets,
                null));
        }

        public void CancelManualTargetSelection()
        {
            BattleManualTargetSelectionRequest request =
                CurrentManualTargetRequest;
            if (request == null)
                return;

            CurrentManualTargetRequest = null;
            CurrentManualSelectedCount = 0;
            ManualTargetSelectionProgressChanged?.Invoke();
            ManualTargetSelectionPendingChanged?.Invoke(false);
            request.Complete(new BattleManualTargetSelectionResult(
                request.Faction,
                null,
                null,
                true));
        }

        public bool TryAddEnemy(EnemyRuntime enemy)
        {
            return false;
        }

        public bool TryAddEnemiesToDistinctTiles(
            IReadOnlyList<EnemyRuntime> enemies)
        {
            return false;
        }

        public void ClearAllEnemies()
        {
        }

        public void TickStatusEffects(float deltaTime)
        {
        }

        public void TickEnemyAbilities(
            float deltaTime,
            IReadOnlyList<IBattleCharacter> characters)
        {
        }

        public void SetBattleCharacters(
            IReadOnlyList<IBattleCharacter> characters)
        {
        }

        public IReadOnlyList<EnemyRuntime> SelectCharacterTargets(
            IBattleCharacter source,
            CharacterAttackSubject subject,
            CharacterAttackSubjectMetric metric,
            int targetCount,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            CharacterTargetSelectionCallCount++;
            CharacterTargetSelectionSubjects.Add(subject);
            CharacterTargetSelectionMetrics.Add(metric);
            CharacterTargetSelectionCounts.Add(targetCount);
            SelectionNumericConditionCounts.Add(
                numericConditions?.Count ?? 0);
            if (PlannedEnemySelections.Count > 0)
                return PlannedEnemySelections.Dequeue();
            return SimulateAislingAreaSequence && _centerTarget != null
                ? new[] { _centerTarget }
                : SelectedEnemyTargets;
        }

        public IReadOnlyList<IBattleCharacter> SelectAlliedCharacters(
            IBattleCharacter source,
            CharacterAttackSubject subject,
            CharacterAttackSubjectMetric metric,
            int targetCount,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            AlliedCharacterTargetSelectionCallCount++;
            return subject == CharacterAttackSubject.Self && source != null
                ? new[] { source }
                : Array.Empty<IBattleCharacter>();
        }

        public IReadOnlyList<EnemyRuntime> FilterCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            FilterCharacterTargetCallCount++;
            List<EnemyRuntime> validTargets = new();
            if (targets != null)
            {
                foreach (EnemyRuntime target in targets)
                {
                    if (target != null && !InvalidEnemyTargets.Contains(target))
                        validTargets.Add(target);
                }
            }

            bool hasTargets = validTargets.Count > 0;
            return CharacterConditionEvaluator.AllowsAction(
                    source,
                    conditionMatchMode,
                    numericConditions,
                    hasTargets)
                ? validTargets
                : Array.Empty<EnemyRuntime>();
        }

        public IReadOnlyList<IBattleCharacter> FilterAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            bool hasTargets = targets != null && targets.Count > 0;
            return CharacterConditionEvaluator.AllowsAction(
                    source,
                    conditionMatchMode,
                    numericConditions,
                    hasTargets)
                ? targets ?? Array.Empty<IBattleCharacter>()
                : Array.Empty<IBattleCharacter>();
        }

        public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
            IReadOnlyList<EnemyRuntime> centerTargets,
            IReadOnlyList<CharacterTargetAreaOffset> areaOffsets)
        {
            if (ReturnCenterTargetsForAreaExpansion)
            {
                return centerTargets ?? Array.Empty<EnemyRuntime>();
            }

            if (!SimulateAislingAreaSequence ||
                centerTargets == null ||
                centerTargets.Count == 0 ||
                centerTargets[0] != _centerTarget ||
                !CenterTargetAlive)
            {
                return Array.Empty<EnemyRuntime>();
            }

            AreaExpansionCallCount++;
            bool diagonal = false;
            if (areaOffsets != null)
            {
                foreach (CharacterTargetAreaOffset offset in areaOffsets)
                {
                    if (offset != null &&
                        offset.RowOffset != 0 &&
                        offset.ColumnOffset != 0)
                    {
                        diagonal = true;
                        break;
                    }
                }
            }

            return diagonal
                ? new[] { _centerTarget, DiagonalTarget }
                : new[] { _centerTarget, _crossTarget };
        }

        public int TryDamageCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int damage,
            CharacterAttackDamageType damageType,
            bool showAttackRange)
        {
            if (targets == null || targets.Count == 0 || damage <= 0)
                return 0;

            DamageShowAttackRangeSnapshots.Add(showAttackRange);
            List<EnemyRuntime> appliedTargets = new();
            int totalDamage = 0;
            foreach (EnemyRuntime target in targets)
            {
                if (target == _centerTarget && !CenterTargetAlive)
                    continue;

                appliedTargets.Add(target);
                DamageAmounts.Add(damage);
                totalDamage += ApplyEffectsToEnemyRuntime
                    ? TakeEnemyDamage(target, damage, damageType)
                    : damage;
                TargetDamageApplied?.Invoke(target, damage);
            }

            DamageTargetSnapshots.Add(appliedTargets);
            if (DamageTargetSnapshots.Count == 1)
            {
                AreaExpansionCountAtFirstDamage = AreaExpansionCallCount;
                CenterTargetAlive = false;
            }
            return totalDamage;
        }

        public int TryHealCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange)
        {
            if (targets == null || amount <= 0)
                return 0;

            int totalHealed = 0;
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in targets)
            {
                if (target != null && uniqueTargets.Add(target))
                    totalHealed += target.Heal(amount);
            }

            return totalHealed;
        }

        public int TryHealAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount)
        {
            if (targets == null || amount <= 0)
                return 0;

            int totalHealed = 0;
            HashSet<IBattleCharacter> uniqueTargets = new();
            foreach (IBattleCharacter target in targets)
            {
                if (target != null && uniqueTargets.Add(target))
                    totalHealed += target.Heal(amount);
            }

            return totalHealed;
        }

        public int TryGrantShieldToCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange)
        {
            if (targets == null || amount <= 0)
                return 0;

            int totalGranted = 0;
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in targets)
            {
                if (target != null && uniqueTargets.Add(target))
                    totalGranted += target.GainShield(amount);
            }

            return totalGranted;
        }

        public int TryGrantShieldToAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount)
        {
            if (targets == null || amount <= 0)
                return 0;

            int totalGranted = 0;
            HashSet<IBattleCharacter> uniqueTargets = new();
            foreach (IBattleCharacter target in targets)
            {
                if (target != null && uniqueTargets.Add(target))
                    totalGranted += target.GainShield(amount);
            }

            return totalGranted;
        }

        public bool TryApplyCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<EnemyRuntime> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks,
            float tickInterval,
            bool showAttackRange)
        {
            StatusApplyCallCount++;
            StatusTargetSnapshots.Add(
                targets != null
                    ? new List<EnemyRuntime>(targets)
                    : Array.Empty<EnemyRuntime>());
            AppliedStatuses.Add(statusEffect);
            bool hasValidTargets = targets != null &&
                                   targets.Count > 0 &&
                                   statusEffect != null;
            if (ForceStatusApplyFailure)
                return false;
            if (!hasValidTargets || !ApplyEffectsToEnemyRuntime)
                return hasValidTargets;

            bool applied = false;
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in targets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                applied |= ApplyEnemyStatus(
                    target,
                    statusEffect,
                    duration,
                    Mathf.Max(1, Mathf.RoundToInt(stacks)),
                    user.Unit.Ally,
                    tickInterval);
            }

            return applied;
        }

        public bool TryApplyAlliedCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<IBattleCharacter> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks)
        {
            if (targets == null)
                return false;

            bool applied = false;
            foreach (IBattleCharacter target in targets)
            {
                if (target != null)
                {
                    applied |= target.ApplyStatusEffect(
                        statusEffect,
                        duration,
                        Mathf.Max(1, Mathf.RoundToInt(stacks)),
                        user.Unit.Ally);
                }
            }

            return applied;
        }

        public bool TryRemoveCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount,
            bool showAttackRange)
        {
            return false;
        }

        public bool TryRemoveAlliedCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount)
        {
            AlliedStatusRemovalCallCount++;
            LastAlliedStatusRemovalTargets =
                targets != null
                    ? new List<IBattleCharacter>(targets)
                    : Array.Empty<IBattleCharacter>();
            if (targets == null)
                return false;

            bool removed = false;
            foreach (IBattleCharacter target in targets)
            {
                if (target != null)
                {
                    removed |= target.RemoveStatusEffects(
                        removalSelection,
                        removalAmount) > 0;
                }
            }

            return removed;
        }
    }
}
