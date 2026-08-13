#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSuirenPlayModeTests
{
    private const string SuirenResourcePath = "Characters/2_Suiren";
    private const string AislingResourcePath = "Characters/2_Aisling";
    private const string EmergencyKitResourcePath =
        "StatusEffects/EmergencyKit";
    private const string StunResourcePath = "StatusEffects/Stun";

    private readonly List<CharacterRuntime> _characters = new();
    private readonly List<GameObject> _createdObjects = new();
    private DungeonBoardView _board;

    [TearDown]
    public void TearDown()
    {
        foreach (CharacterRuntime character in _characters)
        {
            if (character != null)
                character.BindBattle(null, null);
        }

        if (_board != null)
            _board.SetBattleCharacters(null);

        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
            {
                _createdObjects[index].SetActive(false);
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
            }
        }

        _characters.Clear();
        _createdObjects.Clear();
        _board = null;
    }

    [Test]
    public void ActualSuiren_ChargesCappedKits_CleansStun_AndRebinds()
    {
        Assert.That(
            Application.isPlaying,
            Is.True,
            "This integration test must run on the PlayMode tab.");

        CharacterSO definition =
            LoadResource<CharacterSO>(SuirenResourcePath);
        CharacterSO allyDefinition =
            LoadResource<CharacterSO>(AislingResourcePath);
        StatusEffectSO emergencyKit =
            LoadResource<StatusEffectSO>(EmergencyKitResourcePath);
        StatusEffectSO stun =
            LoadResource<StatusEffectSO>(StunResourcePath);

        Assert.That(definition.PassiveDefinitions, Has.Count.GreaterThan(1));
        Assert.That(
            definition.PassiveDefinitions[0].HasExplicitEffects,
            Is.True);
        Assert.That(
            definition.PassiveDefinitions[1].HasExplicitEffects,
            Is.True);

        _board = CreateBoard();
        CharacterRuntime suiren =
            CreateCharacter(definition, "Suiren");
        CharacterRuntime ally =
            CreateCharacter(allyDefinition, "Ally");
        _board.SetBattleCharacters(
            new IBattleCharacter[] { suiren, ally });
        suiren.BindBattle(null, _board);
        ally.BindBattle(null, _board);

        for (int expectedStacks = 1;
             expectedStacks <= 3;
             expectedStacks++)
        {
            suiren.TickBattle(10f, _board);
            Assert.That(
                suiren.GetStatusStackCount(emergencyKit),
                Is.EqualTo(expectedStacks));
        }

        suiren.TickBattle(10f, _board);
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(3),
            "Emergency Kit must remain capped at three stacks.");

        Assert.That(ally.ApplyStatusEffect(stun, 5f, 1), Is.True);
        Assert.That(ally.HasStatusEffect(stun), Is.False);
        Assert.That(ally.DisabledTimeRemaining, Is.Zero);
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(2),
            "Removing the ally's Stun must consume exactly one kit.");

        suiren.BindBattle(null, null);
        suiren.ResetRuntime();
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.Zero);

        suiren.BindBattle(null, _board);
        suiren.TickBattle(10f, _board);
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.EqualTo(1));

        Assert.That(ally.ApplyStatusEffect(stun, 5f, 1), Is.True);
        Assert.That(ally.HasStatusEffect(stun), Is.False);
        Assert.That(
            suiren.GetStatusStackCount(emergencyKit),
            Is.Zero,
            "The rebound passive must consume its single refreshed kit.");
    }

    private DungeonBoardView CreateBoard()
    {
        GameObject root = new(
            "Test_DungeonBoard",
            typeof(RectTransform));
        _createdObjects.Add(root);
        return root.AddComponent<DungeonBoardView>();
    }

    private CharacterRuntime CreateCharacter(
        CharacterSO definition,
        string label)
    {
        GameObject root = new(
            $"Test_{label}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(AudioSource));
        _createdObjects.Add(root);

        CharacterRuntime character =
            root.AddComponent<CharacterRuntime>();
        TextMeshProUGUI nameText =
            CreateText(root.transform, "txtName");
        TextMeshProUGUI attackText =
            CreateText(root.transform, "txtAttack");
        TextMeshProUGUI cooldownText =
            CreateText(root.transform, "txtCooldown");

        GameObject cooldownTrack = new(
            "grpCooldown",
            typeof(RectTransform));
        cooldownTrack.transform.SetParent(root.transform, false);
        GameObject cooldownFillObject = new(
            "imgCooldownFill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        cooldownFillObject.transform.SetParent(
            cooldownTrack.transform,
            false);

        SetPrivateField(character, "nameText", nameText);
        SetPrivateField(character, "attackText", attackText);
        SetPrivateField(character, "cooldownText", cooldownText);
        SetPrivateField(
            character,
            "cooldownFill",
            cooldownFillObject.GetComponent<Image>());

        Assert.That(
            character.ConfigureDefinition(definition),
            Is.True,
            $"Failed to configure CharacterRuntime '{label}'.");
        _characters.Add(character);
        return character;
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

    private static void SetPrivateField<T>(
        CharacterRuntime character,
        string fieldName,
        T value)
    {
        FieldInfo field = typeof(CharacterRuntime).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(
            field,
            Is.Not.Null,
            $"Missing CharacterRuntime test field '{fieldName}'.");
        field.SetValue(character, value);
    }

    private static T LoadResource<T>(string resourcePath)
        where T : UnityEngine.Object
    {
        T resource = Resources.Load<T>(resourcePath);
        Assert.That(
            resource,
            Is.Not.Null,
            $"Missing Resources asset '{resourcePath}'.");
        return resource;
    }
}
#endif
