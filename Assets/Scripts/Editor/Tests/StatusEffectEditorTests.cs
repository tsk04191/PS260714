using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StatusEffectEditorTests
{
    private const BindingFlags PrivateStatic =
        BindingFlags.NonPublic | BindingFlags.Static;

    private StatusEffectSO _definition;

    [SetUp]
    public void SetUp()
    {
        _definition = ScriptableObject.CreateInstance<StatusEffectSO>();
        _definition.hideFlags = HideFlags.HideAndDontSave;
    }

    [TearDown]
    public void TearDown()
    {
        if (_definition != null)
            UnityEngine.Object.DestroyImmediate(_definition);
    }

    [Test]
    public void EditorOptionLabels_AreReadableAndEnumAligned()
    {
        AssertOptions(
            "AlignmentOptions",
            "버프",
            "디버프",
            "중립");
        AssertOptions(
            "DurationModeOptions",
            "시간제",
            "영구");
        AssertOptions(
            "StackModeOptions",
            "스택 추가 + 시간 갱신",
            "스택 추가 + 시간 유지",
            "적용 묶음별 순차 지속시간",
            "기존 상태 교체");
        AssertOptions(
            "RemovalOrderOptions",
            "오래된 스택부터",
            "새로운 스택부터",
            "무작위");
        AssertOptions(
            "OperationTriggerOptions",
            "적용 시",
            "주기마다",
            "만료 시",
            "제거 시",
            "스택 변경 시");
        AssertOptions(
            "OperationTypeOptions",
            "주기 피해",
            "즉시 피해",
            "공격력 변경",
            "공격 속도 변경",
            "행동 불가");
        AssertOptions(
            "ValueModeOptions",
            "고정",
            "비율");
        AssertOptions(
            "LifecycleTriggerOptions",
            "최초 적용 시",
            "재적용 시",
            "주기마다",
            "스택 변경 시",
            "자연 만료 시",
            "수동 제거 시");
        AssertOptions(
            "EffectTypeOptions",
            "피해",
            "상태 부여",
            "상태 제거",
            "자원 획득",
            "자원 소비",
            "체력 회복",
            "체력 소비",
            "보호막 부여");
        AssertOptions(
            "EffectTargetModeOptions",
            "상태 보유자",
            "효과 제공자",
            "별도 새 대상");
        AssertOptions(
            "EffectAmountModeOptions",
            "공격력 비율",
            "고정");
        AssertOptions(
            "StatusRemovalTargetOptions",
            "지정 상태",
            "무작위 상태",
            "모든 버프",
            "모든 디버프",
            "모든 상태");
        AssertOptions(
            "StatusRemovalAmountModeOptions",
            "고정 스택",
            "현재 스택 비율");
        AssertOptions(
            "StatTypeOptions",
            "공격력",
            "공격 속도",
            "받는 피해",
            "대상 우선순위");
        AssertOptions(
            "StatModifierModeOptions",
            "고정 가산",
            "기본값 기준 비율 가산",
            "곱연산 비율");
        AssertOptions(
            "ControlTypeOptions",
            "전체 행동 불가",
            "기본 공격 금지",
            "액티브 스킬 금지",
            "패시브 쿨다운 정지",
            "강제 포커싱");
    }

    [Test]
    public void AddOperation_CreatesSupportedPeriodicDamageDefaults()
    {
        SerializedObject serialized = new(_definition);
        SerializedProperty operations = serialized.FindProperty(
            "operations");
        MethodInfo addOperation = typeof(StatusEffectEditorWindow).GetMethod(
            "AddOperation",
            PrivateStatic);

        Assert.That(addOperation, Is.Not.Null);
        addOperation.Invoke(null, new object[] { operations });
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(_definition.Operations, Has.Count.EqualTo(1));
        StatusEffectOperationDefinition operation =
            _definition.Operations[0];
        Assert.That(
            operation.Trigger,
            Is.EqualTo(StatusEffectOperationTrigger.OnTick));
        Assert.That(
            operation.OperationType,
            Is.EqualTo(StatusEffectOperationType.PeriodicDamage));
        Assert.That(
            operation.ValueMode,
            Is.EqualTo(StatusEffectValueMode.Fixed));
        Assert.That(operation.Value, Is.EqualTo(1f));
        Assert.That(operation.ScaleWithStacks, Is.True);
    }

    [Test]
    public void RegenerateStatusId_CreatesDistinctPersistentGuid()
    {
        string previousId = _definition.StatusId;

        _definition.RegenerateStatusId();

        Assert.That(_definition.StatusId, Is.Not.EqualTo(previousId));
        Assert.That(_definition.StatusId, Has.Length.EqualTo(32));
        Assert.That(
            Guid.TryParseExact(_definition.StatusId, "N", out _),
            Is.True);
    }

    [Test]
    public void GetIconTexture_UnassignedIconReturnsNull()
    {
        MethodInfo getIconTexture =
            typeof(StatusEffectEditorWindow).GetMethod(
                "GetIconTexture",
                PrivateStatic);

        Assert.That(getIconTexture, Is.Not.Null);
        Assert.That(
            getIconTexture.Invoke(null, new object[] { _definition }),
            Is.Null);
    }

    [Test]
    public void GetIconTexture_DestroyedIconReferenceReturnsNull()
    {
        Texture2D texture = new(1, 1);
        Sprite icon = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            Vector2.zero);
        SerializedObject serialized = new(_definition);
        serialized.FindProperty("icon").objectReferenceValue = icon;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Object.DestroyImmediate(icon);

        try
        {
            MethodInfo getIconTexture =
                typeof(StatusEffectEditorWindow).GetMethod(
                    "GetIconTexture",
                    PrivateStatic);

            Assert.That(getIconTexture, Is.Not.Null);
            Assert.That(
                getIconTexture.Invoke(
                    null,
                    new object[] { _definition }),
                Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void EditorMenus_GroupDungeonToolsUnderDungeonRoot()
    {
        Assert.That(
            CharacterEditorWindow.MenuPath,
            Is.EqualTo("PS260714/Character Editor"));
        Assert.That(
            EnemyEditorWindow.MenuPath,
            Is.EqualTo("PS260714/Enemy Editor"));
        Assert.That(
            StageSelectEditorWindow.MenuPath,
            Is.EqualTo("PS260714/Dungeon/Dungeon Editor"));
        Assert.That(
            PS260714EditorMenu.RecruitEditor,
            Is.EqualTo("PS260714/UI/Recruit Editor"));
        Assert.That(
            StatusEffectEditorWindow.MenuPath,
            Is.EqualTo("PS260714/Status Effect Editor"));
        Assert.That(
            BattleEditorWindow.MenuPath,
            Is.EqualTo("PS260714/Dungeon/Battle Editor"));
        Assert.That(
            PS260714EditorMenu.EventEditor,
            Is.EqualTo("PS260714/Dungeon/Event Editor"));
        Assert.That(
            PS260714EditorMenu.RestEditor,
            Is.EqualTo("PS260714/Dungeon/Rest Editor"));
        Assert.That(
            PS260714EditorMenu.ShopEditor,
            Is.EqualTo("PS260714/Dungeon/Shop Editor"));
        Assert.That(
            PS260714EditorMenu.LocalizationEditor,
            Is.EqualTo(
                "PS260714/Localization/Localization Editor"));
        Assert.That(
            PS260714EditorMenu.ValidateLocalization,
            Is.EqualTo("PS260714/Localization/Validate CSV"));
        Assert.That(
            PS260714EditorMenu.GenerateLocalization,
            Is.EqualTo("PS260714/Localization/Generate C#"));
        Assert.That(
            typeof(BattleEditorWindow).GetMethod(
                "OpenFromMenu",
                BindingFlags.Public | BindingFlags.Static),
            Is.Not.Null);
        Assert.That(
            typeof(PS260714.Localization.Editor.LocalizationEditorWindow)
                .GetMethod(
                    "OpenAtKey",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null),
            Is.Not.Null);
        AssertEditorOpenOverload(
            typeof(CharacterEditorWindow),
            typeof(CharacterSO));
        AssertEditorOpenOverload(
            typeof(EnemyEditorWindow),
            typeof(EnemySO));
        AssertEditorOpenOverload(
            typeof(ItemEditorWindow),
            typeof(ItemDefinitionSO));
        AssertEditorOpenOverload(
            typeof(StatusEffectEditorWindow),
            typeof(StatusEffectSO));
        AssertEditorOpenOverload(
            typeof(BattleVfxEditorWindow),
            typeof(BattleVfxCueSO));
        AssertEditorOpenOverload(
            typeof(StageSelectEditorWindow),
            typeof(DungeonDefinition));
        Assert.That(
            typeof(MenuPageSceneBuilder).GetMethod(
                "RebuildClientPages",
                BindingFlags.Public | BindingFlags.Static),
            Is.Null);
        Assert.That(
            typeof(MenuPageSceneBuilder).GetCustomAttributes(
                typeof(InitializeOnLoadAttribute),
                false),
            Is.Empty,
            "Menu UI must not rebuild automatically on editor load.");
        foreach (MethodInfo method in typeof(MenuPageSceneBuilder)
                     .GetMethods(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Static))
        {
            Assert.That(
                method.GetCustomAttributes(
                    typeof(InitializeOnLoadMethodAttribute),
                    false),
                Is.Empty,
                $"{method.Name} must not mutate UI on domain reload.");
        }
        Assert.That(
            typeof(StatusEffectEditorWindow).Assembly.GetType(
                "FireStatusEffectAssetGenerator"),
            Is.Null,
            "Legacy 2D fire VFX generator must not return.");
    }

    [Test]
    public void EditorMenus_HaveStableUniquePriorities()
    {
        Dictionary<string, int> prioritiesByPath =
            new(StringComparer.Ordinal);
        foreach (Type type in typeof(PS260714EditorMenu).Assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Static))
            {
                foreach (CustomAttributeData attribute in
                         method.CustomAttributes)
                {
                    if (attribute.AttributeType != typeof(MenuItem) ||
                        attribute.ConstructorArguments.Count == 0)
                    {
                        continue;
                    }

                    string path = attribute.ConstructorArguments[0].Value
                        as string;
                    if (path == null ||
                        (!path.StartsWith(
                             PS260714EditorMenu.Root,
                             StringComparison.Ordinal) &&
                         !path.StartsWith(
                             PS260714EditorMenu.DungeonRoot,
                             StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    Assert.That(
                        attribute.ConstructorArguments.Count,
                        Is.EqualTo(3),
                        $"{path} must declare an explicit priority.");
                    int priority =
                        (int)attribute.ConstructorArguments[2].Value;
                    if (prioritiesByPath.TryGetValue(
                            path,
                            out int registeredPriority))
                    {
                        Assert.That(
                            priority,
                            Is.EqualTo(registeredPriority),
                            $"{path} action and validation priorities " +
                            "must match.");
                    }
                    else
                    {
                        prioritiesByPath.Add(path, priority);
                    }
                }
            }
        }

        Assert.That(
            prioritiesByPath,
            Does.Not.ContainKey("PS260714/UI/Apply Main Lobby Layout"));
        Assert.That(prioritiesByPath.Count, Is.EqualTo(22));

        HashSet<int> uniquePriorities = new();
        foreach (KeyValuePair<string, int> menu in prioritiesByPath)
        {
            Assert.That(
                uniquePriorities.Add(menu.Value),
                Is.True,
                $"{menu.Key} reuses priority {menu.Value}.");
        }

        Assert.That(
            new[]
            {
                PS260714EditorMenu.DungeonEditorPriority,
                PS260714EditorMenu.BattleEditorPriority,
                PS260714EditorMenu.EventEditorPriority,
                PS260714EditorMenu.RestEditorPriority,
                PS260714EditorMenu.ShopEditorPriority,
                PS260714EditorMenu.CommonSettingsPriority,
                PS260714EditorMenu.CharacterEditorPriority,
                PS260714EditorMenu.ItemEditorPriority,
                PS260714EditorMenu.EnemyEditorPriority,
                PS260714EditorMenu.StatusEffectEditorPriority,
                PS260714EditorMenu.BattleCardEditorPriority,
                PS260714EditorMenu.BattleVfxEditorPriority,
                PS260714EditorMenu.ValidateBattleVfxPriority,
                PS260714EditorMenu.LocalizationEditorPriority,
                PS260714EditorMenu.ValidateLocalizationPriority,
                PS260714EditorMenu.GenerateLocalizationPriority,
                PS260714EditorMenu.CharacterStandingFramingEditorPriority,
                PS260714EditorMenu.RecruitEditorPriority,
                PS260714EditorMenu.ValidateDesignerUiPriority,
                PS260714EditorMenu.InstallDynamicUiPreviewsPriority,
                PS260714EditorMenu.MigrateBattleItemUsagePriority,
                PS260714EditorMenu.MigrateCharacterModifierIdsPriority,
            },
            Is.EqualTo(new[]
            {
                0, 1, 2, 3, 4,
                100, 101, 102, 103, 104, 105, 106, 107, 108,
                109, 110, 111, 112, 113, 114, 115, 116,
            }));
    }

    [Test]
    public void AddTriggerBlock_CreatesSafeDefaultBattleEffect()
    {
        SerializedObject serialized = new(_definition);
        SerializedProperty blocks =
            serialized.FindProperty("triggerBlocks");
        InvokePrivateEditorMethod("AddTriggerBlock", blocks);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(_definition.TriggerBlocks.Count, Is.EqualTo(1));
        StatusEffectTriggerBlockDefinition block =
            _definition.TriggerBlocks[0];
        Assert.That(
            block.Trigger,
            Is.EqualTo(StatusEffectLifecycleTrigger.OnApply));
        Assert.That(block.ScaleWithCurrentStacks, Is.False);
        Assert.That(block.ScaleWithOccurrences, Is.True);
        Assert.That(block.Effects.Count, Is.EqualTo(1));
        CharacterEffectDefinition effect = block.Effects[0];
        Assert.That(
            effect.Type,
            Is.EqualTo(CharacterEffectType.Damage));
        Assert.That(
            effect.TargetMode,
            Is.EqualTo(CharacterEffectTargetMode.InheritAction));
        Assert.That(
            effect.DamageType,
            Is.EqualTo(CharacterAttackDamageType.Physical));
        Assert.That(
            effect.DamageAmountMode,
            Is.EqualTo(CharacterDamageAmountMode.Ratio));
        Assert.That(effect.DamageAmount, Is.EqualTo(1f));
    }

    [Test]
    public void AddPersistentModules_CreatesSafeDefaults()
    {
        SerializedObject serialized = new(_definition);
        InvokePrivateEditorMethod(
            "AddStatModifier",
            serialized.FindProperty("statModifiers"));
        InvokePrivateEditorMethod(
            "AddControlEffect",
            serialized.FindProperty("controlEffects"));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(_definition.StatModifiers.Count, Is.EqualTo(1));
        StatusEffectStatModifierDefinition modifier =
            _definition.StatModifiers[0];
        Assert.That(
            modifier.StatType,
            Is.EqualTo(StatusEffectStatType.AttackPower));
        Assert.That(
            modifier.Mode,
            Is.EqualTo(StatusEffectStatModifierMode.Flat));
        Assert.That(modifier.Value, Is.Zero);
        Assert.That(modifier.ScaleWithStacks, Is.True);

        Assert.That(_definition.ControlEffects.Count, Is.EqualTo(1));
        Assert.That(
            _definition.ControlEffects[0].ControlType,
            Is.EqualTo(StatusEffectControlType.DisableAllActions));
    }

    [Test]
    public void TargetPriorityModules_ResolveStackedAdjustmentAndForce()
    {
        SerializedObject serialized = new(_definition);
        SerializedProperty modifiers =
            serialized.FindProperty("statModifiers");
        modifiers.arraySize = 1;
        SerializedProperty modifier =
            modifiers.GetArrayElementAtIndex(0);
        modifier.FindPropertyRelative("statType").enumValueIndex =
            (int)StatusEffectStatType.TargetPriority;
        modifier.FindPropertyRelative("mode").enumValueIndex =
            (int)StatusEffectStatModifierMode.Flat;
        modifier.FindPropertyRelative("value").floatValue = 5f;
        modifier.FindPropertyRelative("scaleWithStacks").boolValue = true;
        SerializedProperty controls =
            serialized.FindProperty("controlEffects");
        controls.arraySize = 1;
        controls.GetArrayElementAtIndex(0)
            .FindPropertyRelative("controlType")
            .enumValueIndex =
            (int)StatusEffectControlType.ForceTargeting;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        StatusEffectTargetPriority priority =
            StatusEffectTargetPriorityResolver.Resolve(
                new[]
                {
                    new BattleStatusSnapshot(
                        _definition,
                        2,
                        1f)
                });

        Assert.That(priority.IsForced, Is.True);
        Assert.That(priority.Adjustment, Is.EqualTo(10f));
    }

    private static void AssertOptions(
        string fieldName,
        params string[] expected)
    {
        FieldInfo field = typeof(StatusEffectEditorWindow).GetField(
            fieldName,
            PrivateStatic);
        Assert.That(field, Is.Not.Null, fieldName);
        Assert.That(
            field.GetValue(null) as string[],
            Is.EqualTo(expected),
            fieldName);
    }

    private static void AssertEditorOpenOverload(
        Type editorType,
        Type assetType)
    {
        Assert.That(
            editorType.GetMethod(
                "Open",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { assetType },
                null),
            Is.Not.Null,
            $"{editorType.Name} must open a specific {assetType.Name}.");
    }

    private static void InvokePrivateEditorMethod(
        string methodName,
        SerializedProperty property)
    {
        MethodInfo method = typeof(StatusEffectEditorWindow).GetMethod(
            methodName,
            PrivateStatic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(null, new object[] { property });
    }
}

public sealed class BattleEffectCoreTests
{
    [Test]
    public void CharacterEffect_ExposesSharedBattleEffectContract()
    {
        CharacterEffectDefinition characterEffect = new();
        IBattleEffectDefinition battleEffect = characterEffect;

        Assert.That(
            battleEffect.BattleEffectType,
            Is.EqualTo(BattleEffectType.Damage));
        Assert.That(
            battleEffect.BattleTargetMode,
            Is.EqualTo(BattleEffectTargetMode.InheritContext));
        Assert.That(
            battleEffect.BattlePreconditionFailurePolicy,
            Is.EqualTo(
                BattleEffectPreconditionFailurePolicy.AbortSequence));
        Assert.That(
            battleEffect.BattleFailurePolicy,
            Is.EqualTo(BattleEffectFailurePolicy.Continue));
        Assert.That(battleEffect.BattleTargetSelector, Is.Not.Null);
        Assert.That(
            battleEffect.BattleTargetSelector,
            Is.SameAs(characterEffect.TargetSelector));
    }

    [Test]
    public void BattleContext_StatusHolderSupportsAllyHealthScaling()
    {
        FakeBattleCharacter holder = new(
            currentHealth: 4,
            maximumHealth: 10);
        BattleStatusTarget holderTarget =
            BattleStatusTarget.FromAlly(holder);
        BattleEffectContext context = BattleEffectContext.ForStatus(
            holderTarget,
            holderTarget,
            null,
            sourceAttackPower: 3f,
            previousStacks: 1,
            currentStacks: 3,
            occurrenceCount: 2);
        ScalingValue scaling = new(
            fixedAmount: 0f,
            sourceAttackPowerScale: 0f,
            sourceResourceScale: 0f,
            targetCurrentHealthScale: 1f,
            targetMaximumHealthScale: 0.5f,
            sourceStatusStacksScale: 0f,
            targetStatusStacksScale: 0f);

        Assert.That(
            context.OriginKind,
            Is.EqualTo(BattleEffectOriginKind.StatusEffect));
        Assert.That(context.Holder.Ally, Is.SameAs(holder));
        Assert.That(context.HasTargetHealth, Is.True);
        Assert.That(context.TargetCurrentHealth, Is.EqualTo(4));
        Assert.That(context.TargetMaximumHealth, Is.EqualTo(10));
        Assert.That(context.PreviousStacks, Is.EqualTo(1));
        Assert.That(context.CurrentStacks, Is.EqualTo(3));
        Assert.That(context.AddedStacks, Is.EqualTo(2));
        Assert.That(context.RemovedStacks, Is.Zero);
        Assert.That(context.OccurrenceCount, Is.EqualTo(2));
        Assert.That(scaling.EvaluateBattle(context), Is.EqualTo(9f));
    }

    [Test]
    public void BattleContext_SupportsSourceCurrentAndMaximumHealthScaling()
    {
        FakeBattleCharacter source = new(
            currentHealth: 40,
            maximumHealth: 120);
        BattleStatusTarget sourceTarget =
            BattleStatusTarget.FromAlly(source);
        BattleEffectContext context = BattleEffectContext.ForStatus(
            sourceTarget,
            sourceTarget,
            null,
            sourceAttackPower: 0f,
            previousStacks: 0,
            currentStacks: 1,
            occurrenceCount: 1);
        ScalingValue scaling =
            ScalingValue.SourceCurrentHealth(0.5f) +
            ScalingValue.SourceMaximumHealth(0.25f);

        Assert.That(context.SourceCurrentHealth, Is.EqualTo(40));
        Assert.That(context.SourceMaximumHealth, Is.EqualTo(120));
        Assert.That(scaling.EvaluateBattle(context), Is.EqualTo(50f));
    }

    [Test]
    public void CharacterConditions_SupportMaximumHealthAndHealthPerformance()
    {
        FakeBattleCharacter character = new(
            currentHealth: 130,
            maximumHealth: 150);
        CharacterNumericCondition condition = new();
        SetPrivateField(
            condition,
            "comparison",
            CharacterNumericComparison.GreaterThanOrEqual);
        SetPrivateField(condition, "threshold", 150f);
        SetPrivateField(
            condition,
            "metric",
            CharacterNumericConditionMetric.MaximumHealth);

        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True);

        SetPrivateField(condition, "threshold", 100f);
        SetPrivateField(
            condition,
            "metric",
            CharacterNumericConditionMetric.HealthPerformancePercentage);

        Assert.That(
            CharacterConditionEvaluator.MatchesCharacter(
                condition,
                character),
            Is.True);
    }

    [Test]
    public void SharedContext_PreservesLegacyScalingResult()
    {
        EffectContext legacy = EffectContext.ForPreview(
            CharacterActionKind.Skill,
            sourceAttackPower: 10f,
            sourceResource: 4,
            sourceResourceMaximum: 10);
        BattleEffectContext shared =
            BattleEffectContext.FromCharacter(legacy);
        ScalingValue scaling =
            ScalingValue.Fixed(2f) +
            ScalingValue.SourceAttackPower(0.5f) +
            ScalingValue.SourceResource(1.5f);

        Assert.That(
            scaling.EvaluateBattle(shared),
            Is.EqualTo(scaling.Evaluate(legacy)));
        Assert.That(
            shared.OriginKind,
            Is.EqualTo(BattleEffectOriginKind.CharacterSkill));
    }

    [Test]
    public void StatusTriggerBlock_ExposesSharedBattleEffects()
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        StatusEffectTriggerBlockDefinition block = new();
        CharacterEffectDefinition effect = new();
        SetPrivateField(
            block,
            "trigger",
            StatusEffectLifecycleTrigger.OnReapply);
        GetPrivateList<CharacterEffectDefinition>(
            block,
            "effects").Add(effect);
        GetPrivateList<StatusEffectTriggerBlockDefinition>(
            status,
            "triggerBlocks").Add(block);
        try
        {
            block.Validate();
            FakeBattleCharacter holder = new(10, 10);
            StatusEffectLifecycleEvent eventData = new(
                status,
                StatusEffectLifecycleTrigger.OnReapply,
                BattleStatusTarget.FromAlly(holder),
                holder,
                1,
                2);
            IReadOnlyList<StatusEffectTriggerInvocation> invocations =
                StatusEffectLifecycleResolver.ResolveInvocations(eventData);

            Assert.That(
                block.Trigger,
                Is.EqualTo(StatusEffectLifecycleTrigger.OnReapply));
            Assert.That(block.HasEffects, Is.True);
            Assert.That(block.Effects.Count, Is.EqualTo(1));
            Assert.That(block.BattleEffects.Count, Is.EqualTo(1));
            Assert.That(
                block.BattleEffects[0],
                Is.SameAs((IBattleEffectDefinition)effect));
            Assert.That(invocations.Count, Is.EqualTo(1));
            Assert.That(invocations[0].IsValid, Is.True);
            Assert.That(invocations[0].Block, Is.SameAs(block));
            Assert.That(invocations[0].BlockIndex, Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(status);
        }
    }

    [Test]
    public void StatusLifecycleResolver_ProducesOrderedExclusiveTriggers()
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            FakeBattleCharacter holder = new(8, 10);
            FakeBattleCharacter source = new(10, 10, 7f);
            BattleStatusTarget target =
                BattleStatusTarget.FromAlly(holder);
            BattleStatusSnapshot active = new(
                status,
                2,
                3f,
                source);

            IReadOnlyList<StatusEffectLifecycleEvent> applied =
                StatusEffectLifecycleResolver.Resolve(
                    new BattleStatusChangedEvent(
                        target,
                        BattleStatusChangeType.Applied,
                        default,
                        active));
            AssertTriggers(
                applied,
                StatusEffectLifecycleTrigger.OnApply,
                StatusEffectLifecycleTrigger.OnStackChanged);

            IReadOnlyList<StatusEffectLifecycleEvent> reapplied =
                StatusEffectLifecycleResolver.Resolve(
                    new BattleStatusChangedEvent(
                        target,
                        BattleStatusChangeType.Reapplied,
                        active,
                        active));
            AssertTriggers(
                reapplied,
                StatusEffectLifecycleTrigger.OnReapply);

            BattleStatusSnapshot inactive = new(status, 0, 0f);
            IReadOnlyList<StatusEffectLifecycleEvent> removed =
                StatusEffectLifecycleResolver.Resolve(
                    new BattleStatusChangedEvent(
                        target,
                        BattleStatusChangeType.Removed,
                        active,
                        inactive));
            AssertTriggers(
                removed,
                StatusEffectLifecycleTrigger.OnStackChanged,
                StatusEffectLifecycleTrigger.OnRemove);

            IReadOnlyList<StatusEffectLifecycleEvent> expired =
                StatusEffectLifecycleResolver.Resolve(
                    new BattleStatusChangedEvent(
                        target,
                        BattleStatusChangeType.Expired,
                        active,
                        inactive));
            AssertTriggers(
                expired,
                StatusEffectLifecycleTrigger.OnStackChanged,
                StatusEffectLifecycleTrigger.OnExpire);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(status);
        }
    }

    [Test]
    public void StatusTickContext_CarriesHolderSourceStacksAndOccurrences()
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            FakeBattleCharacter holder = new(4, 12);
            FakeBattleCharacter source = new(10, 10, 7f);
            BattleStatusTarget target =
                BattleStatusTarget.FromAlly(holder);
            StatusEffectLifecycleEvent tick =
                StatusEffectLifecycleResolver.ResolveTick(
                    target,
                    new BattleStatusSnapshot(
                        status,
                        3,
                        2f,
                        source),
                    4);
            BattleEffectContext context =
                tick.CreateEffectContext(null);

            Assert.That(tick.IsValid, Is.True);
            Assert.That(
                tick.Trigger,
                Is.EqualTo(StatusEffectLifecycleTrigger.OnTick));
            Assert.That(tick.Definition, Is.SameAs(status));
            Assert.That(tick.Target.Ally, Is.SameAs(holder));
            Assert.That(tick.Source, Is.SameAs(source));
            Assert.That(tick.PreviousStacks, Is.EqualTo(3));
            Assert.That(tick.CurrentStacks, Is.EqualTo(3));
            Assert.That(tick.OccurrenceCount, Is.EqualTo(4));
            Assert.That(
                context.OriginKind,
                Is.EqualTo(BattleEffectOriginKind.StatusEffect));
            Assert.That(context.Holder.Ally, Is.SameAs(holder));
            Assert.That(context.Source, Is.SameAs(source));
            Assert.That(context.SourceAttackPower, Is.EqualTo(7f));
            Assert.That(context.TargetCurrentHealth, Is.EqualTo(4));
            Assert.That(context.TargetMaximumHealth, Is.EqualTo(12));
            Assert.That(context.CurrentStacks, Is.EqualTo(3));
            Assert.That(context.OccurrenceCount, Is.EqualTo(4));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(status);
        }
    }

    private static void AssertTriggers(
        IReadOnlyList<StatusEffectLifecycleEvent> events,
        params StatusEffectLifecycleTrigger[] expected)
    {
        Assert.That(events.Count, Is.EqualTo(expected.Length));
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.That(
                events[index].Trigger,
                Is.EqualTo(expected[index]),
                $"trigger[{index}]");
        }
    }

    private static List<T> GetPrivateList<T>(
        object target,
        string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return field.GetValue(target) as List<T>;
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private sealed class FakeBattleCharacter : IBattleCharacter
    {
        public int PartySlotIndex => 0;
        public int TotalDamageDealt => 0;
        public int CurrentHealth { get; private set; }
        public int MaximumHealth { get; }
        public int CurrentShield { get; private set; }
        public float DisabledTimeRemaining => 0f;
        public float CurrentAttackPower { get; }
        public float CurrentAttackSpeed => 1f;

        public event Action<BattleStatusChangedEvent> StatusChanged
        {
            add { }
            remove { }
        }

        public FakeBattleCharacter(
            int currentHealth,
            int maximumHealth,
            float currentAttackPower = 0f)
        {
            MaximumHealth = Math.Max(1, maximumHealth);
            CurrentHealth = Math.Min(
                MaximumHealth,
                Math.Max(0, currentHealth));
            CurrentAttackPower = Math.Max(0f, currentAttackPower);
        }

        public bool HasStatusEffect(StatusEffectSO statusEffect)
        {
            return false;
        }

        public int GetStatusStackCount(StatusEffectSO statusEffect)
        {
            return 0;
        }

        public IReadOnlyList<BattleStatusSnapshot>
            GetActiveStatusEffects()
        {
            return Array.Empty<BattleStatusSnapshot>();
        }

        public bool TryConsumeStatusStacks(
            StatusEffectSO statusEffect,
            int stackCount)
        {
            return false;
        }

        public int Heal(int amount)
        {
            int previous = CurrentHealth;
            CurrentHealth = Math.Min(
                MaximumHealth,
                CurrentHealth + Math.Max(0, amount));
            return CurrentHealth - previous;
        }

        public int GainShield(int amount)
        {
            int gained = Math.Max(0, amount);
            CurrentShield += gained;
            return gained;
        }

        public int TakeDamage(int amount)
        {
            int damage = Math.Min(
                CurrentHealth,
                Math.Max(0, amount));
            CurrentHealth -= damage;
            return damage;
        }

        public bool CanSpendHealth(int amount)
        {
            return amount > 0 && CurrentHealth - amount >= 1;
        }

        public bool TrySpendHealth(int amount)
        {
            if (!CanSpendHealth(amount))
                return false;

            CurrentHealth -= amount;
            return true;
        }

        public bool Initialize()
        {
            return true;
        }

        public void BindBattle(
            IActiveSkillResource activeSkillResource,
            IBattleBoard board)
        {
        }

        public void ResetRuntime()
        {
            CurrentHealth = MaximumHealth;
            CurrentShield = 0;
        }

        public void TickBattle(float deltaTime, IBattleBoard board)
        {
        }

        public void RecordDamageDealt(int damage)
        {
        }

        public void DisableFor(float duration)
        {
        }

        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks)
        {
            return false;
        }

        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks,
            IBattleCharacter source)
        {
            return false;
        }

        public int RemoveStatusEffects(
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount)
        {
            return 0;
        }
    }
}
