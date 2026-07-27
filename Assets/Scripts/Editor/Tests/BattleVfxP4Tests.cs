using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleVfxP4Tests
{
    private readonly List<UnityEngine.Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void UnitDefinitions_ExposeConfiguredSpawnAndDeathCues()
    {
        BattleVfxCueSO spawn = CreateCue("Spawn");
        BattleVfxCueSO death = CreateCue("Death");
        CharacterSO character = CreateCharacterDefinition("Character");
        EnemySO enemy = CreateEnemyDefinition("Enemy");
        SetField(character, "spawnVfxCue", spawn);
        SetField(character, "deathVfxCue", death);
        SetField(enemy, "spawnVfxCue", spawn);
        SetField(enemy, "deathVfxCue", death);

        Assert.That(character.SpawnVfxCue, Is.SameAs(spawn));
        Assert.That(character.DeathVfxCue, Is.SameAs(death));
        Assert.That(enemy.SpawnVfxCue, Is.SameAs(spawn));
        Assert.That(enemy.DeathVfxCue, Is.SameAs(death));
        Assert.That(character, Is.InstanceOf<IBattlePresentationUnitDefinition>());
        Assert.That(enemy, Is.InstanceOf<IBattlePresentationUnitDefinition>());
    }

    [Test]
    public void LifecycleEvents_MapSpawnAndDeathToConfiguredCues()
    {
        BattleVfxCueSO spawn = CreateCue("Spawn");
        BattleVfxCueSO death = CreateCue("Death");
        CharacterSO definition = CreateCharacterDefinition("Character");
        SetField(definition, "spawnVfxCue", spawn);
        SetField(definition, "deathVfxCue", death);
        CharacterRuntime character = CreateCharacterRuntime(
            "CharacterRuntime",
            definition);
        BattleStatusTarget target = BattleStatusTarget.FromAlly(character);
        RecordingSource source = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(source, new FixedResolver());

        source.Raise(new BattleUnitLifecycleEvent(
            BattleUnitLifecycleType.Spawned,
            target,
            definition));
        source.Raise(new BattleUnitLifecycleEvent(
            BattleUnitLifecycleType.Defeated,
            target,
            definition,
            0.75f));

        Assert.That(sink.Requests, Has.Count.EqualTo(2));
        Assert.That(sink.Requests[0].Cue, Is.SameAs(spawn));
        Assert.That(sink.Requests[0].Phase, Is.EqualTo(BattleVfxPhase.Spawn));
        Assert.That(sink.Requests[1].Cue, Is.SameAs(death));
        Assert.That(sink.Requests[1].Phase, Is.EqualTo(BattleVfxPhase.Death));
        Assert.That(sink.Requests[1].DelaySeconds, Is.EqualTo(0.75f));
        Assert.That(
            sink.Requests[1].OriginKind,
            Is.EqualTo(BattleEffectOriginKind.BattleLifecycle));
    }

    [Test]
    public void LifecycleEventWithoutMatchingCue_IsIgnored()
    {
        CharacterSO definition = CreateCharacterDefinition("Character");
        CharacterRuntime character = CreateCharacterRuntime(
            "CharacterRuntime",
            definition);
        RecordingSource source = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(source, new FixedResolver());

        source.Raise(new BattleUnitLifecycleEvent(
            BattleUnitLifecycleType.Defeated,
            BattleStatusTarget.FromAlly(character),
            definition));

        Assert.That(sink.Requests, Is.Empty);
    }

    [Test]
    public void BoardRoster_AnnouncesCharacterSpawnOnlyWhenNewlyAdded()
    {
        CharacterSO definition = CreateCharacterDefinition("Character");
        SetField(definition, "spawnVfxCue", CreateCue("Spawn"));
        CharacterRuntime character = CreateCharacterRuntime(
            "CharacterRuntime",
            definition);
        DungeonBoardView board = CreateBoard();
        List<BattleUnitLifecycleEvent> events = new();
        board.UnitLifecycle += events.Add;

        board.SetBattleCharacters(new IBattleCharacter[] { character });
        board.SetBattleCharacters(new IBattleCharacter[] { character });

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(
            events[0].Type,
            Is.EqualTo(BattleUnitLifecycleType.Spawned));
        Assert.That(events[0].Target.Ally, Is.SameAs(character));
        Assert.That(events[0].Definition, Is.SameAs(definition));
    }

    [Test]
    public void LethalEffect_PublishesDeathAtImpactDelay()
    {
        BattleVfxCueSO death = CreateCue("Death");
        CharacterSO definition = CreateCharacterDefinition("Character");
        SetField(definition, "deathVfxCue", death);
        CharacterRuntime character = CreateCharacterRuntime(
            "CharacterRuntime",
            definition);
        SetField(character, "_currentHealth", 5);
        DungeonBoardView board = CreateBoard();
        List<BattleUnitLifecycleEvent> events = new();
        board.UnitLifecycle += events.Add;

        BattleVfxCueSO cast = CreateCue("Cast");
        BattleVfxCueSO projectile = CreateCue("Projectile");
        SetField(projectile, "motionMode", BattleVfxMotionMode.Linear);
        CharacterEffectDefinition effect = new();
        SetField(effect, "type", CharacterEffectType.Damage);
        SetField(
            effect,
            "targetMode",
            CharacterEffectTargetMode.InheritAction);
        SetField(
            effect,
            "damageType",
            CharacterAttackDamageType.Physical);
        SetField(
            effect,
            "damageAmountMode",
            CharacterDamageAmountMode.Fixed);
        SetField(effect, "damageAmount", 5f);
        SetField(effect, "castVfxCue", cast);
        SetField(effect, "projectileVfxCue", projectile);

        EffectContext context = new(
            character,
            board,
            null,
            CharacterActionKind.Skill,
            CharacterTargetFaction.Ally,
            Array.Empty<EnemyRuntime>(),
            new IBattleCharacter[] { character },
            0f);
        BattleEffectResult result = BattleEffectExecutor.ExecuteEffect(
            BattleEffectContext.FromCharacter(context),
            effect);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(character.CurrentHealth, Is.EqualTo(0));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(
            events[0].Type,
            Is.EqualTo(BattleUnitLifecycleType.Defeated));
        Assert.That(events[0].Definition, Is.SameAs(definition));
        Assert.That(events[0].DelaySeconds, Is.EqualTo(1.25f));
    }

    [Test]
    public void DispatcherUnbind_StopsLifecycleRequests()
    {
        BattleVfxCueSO spawn = CreateCue("Spawn");
        CharacterSO definition = CreateCharacterDefinition("Character");
        SetField(definition, "spawnVfxCue", spawn);
        CharacterRuntime character = CreateCharacterRuntime(
            "CharacterRuntime",
            definition);
        RecordingSource source = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(source, new FixedResolver());
        dispatcher.Unbind();

        source.Raise(new BattleUnitLifecycleEvent(
            BattleUnitLifecycleType.Spawned,
            BattleStatusTarget.FromAlly(character),
            definition));

        Assert.That(sink.Requests, Is.Empty);
    }

    private BattleVfxCueSO CreateCue(string objectName)
    {
        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = objectName;
        cue.RegenerateCueId();
        _createdObjects.Add(cue);
        return cue;
    }

    private CharacterSO CreateCharacterDefinition(string objectName)
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.name = objectName;
        _createdObjects.Add(definition);
        return definition;
    }

    private EnemySO CreateEnemyDefinition(string objectName)
    {
        EnemySO definition =
            ScriptableObject.CreateInstance<EnemySO>();
        definition.name = objectName;
        _createdObjects.Add(definition);
        return definition;
    }

    private CharacterRuntime CreateCharacterRuntime(
        string objectName,
        CharacterSO definition)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        _createdObjects.Add(gameObject);
        CharacterRuntime runtime =
            gameObject.AddComponent<CharacterRuntime>();
        SetField(runtime, "original", definition);
        return runtime;
    }

    private DungeonBoardView CreateBoard()
    {
        GameObject gameObject = new("DungeonBoard", typeof(RectTransform));
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<DungeonBoardView>();
    }

    private static void SetField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(target, value);
    }

    private sealed class RecordingSink : IBattleVfxRequestSink
    {
        public List<BattleVfxRequest> Requests { get; } = new();

        public void Enqueue(BattleVfxRequest request)
        {
            Requests.Add(request);
        }
    }

    private sealed class RecordingSource : IBattlePresentationEventSource
    {
        public event Action<BattleEffectResolvedEvent> EffectResolved;
        public event Action<StatusEffectLifecycleEvent> StatusLifecycle;
        public event Action<BattleUnitLifecycleEvent> UnitLifecycle;

        public void Raise(BattleUnitLifecycleEvent eventData)
        {
            UnitLifecycle?.Invoke(eventData);
        }

        public void Raise(BattleEffectResolvedEvent eventData)
        {
            EffectResolved?.Invoke(eventData);
        }

        public void Raise(StatusEffectLifecycleEvent eventData)
        {
            StatusLifecycle?.Invoke(eventData);
        }
    }

    private sealed class FixedResolver : IBattleVfxTargetResolver
    {
        public BattleVfxTarget ResolveVfxTarget(
            BattleStatusTarget target,
            BattleVfxAnchorType anchorType)
        {
            return new BattleVfxTarget(
                new BattleVfxTargetHandle(1),
                target,
                BattleVfxAnchorSnapshot.FromWorld(
                    Vector3.one,
                    Quaternion.identity));
        }
    }
}
