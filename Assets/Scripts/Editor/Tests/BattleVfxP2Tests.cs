using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleVfxP2Tests
{
    private readonly List<Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                Object.DestroyImmediate(_createdObjects[index]);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void EmptyCue_IsRejectedUntilOutputIsAssigned()
    {
        BattleVfxCueSO cue = CreateCue("Empty");

        BattleVfxCueValidationResult result =
            BattleVfxCueValidator.Validate(cue);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                result,
                "vfx.output_missing",
                BattleVfxCueDiagnosticSeverity.Error),
            Is.True);
    }

    [Test]
    public void AudioOnlyCue_IsValidButReportsMissing3DPrefab()
    {
        BattleVfxCueSO cue = CreateCue("AudioOnly");
        AudioClip clip = AudioClip.Create(
            "AudioOnlyClip",
            64,
            1,
            44100,
            false);
        _createdObjects.Add(clip);
        SetField(cue, "audioClip", clip);

        BattleVfxCueValidationResult result =
            BattleVfxCueValidator.Validate(cue);

        Assert.That(result.IsValid, Is.True);
        Assert.That(
            HasDiagnostic(
                result,
                "vfx.prefab_missing",
                BattleVfxCueDiagnosticSeverity.Warning),
            Is.True);
    }

    [Test]
    public void ParticleLifetime_WarnsWhenPrefabHasNoParticleSystem()
    {
        BattleVfxCueSO cue = CreateCue("MissingParticle");
        GameObject prefab = CreatePrefab("AnimatorOnly");
        SetField(cue, "prefab", prefab);
        SetField(
            cue,
            "lifetimeMode",
            BattleVfxLifetimeMode.ParticleSystem);

        BattleVfxCueValidationResult result =
            BattleVfxCueValidator.Validate(cue);

        Assert.That(result.IsValid, Is.True);
        Assert.That(
            HasDiagnostic(
                result,
                "vfx.particle_missing",
                BattleVfxCueDiagnosticSeverity.Warning),
            Is.True);
    }

    [Test]
    public void ParticlePrefab_Passes3DOutputValidation()
    {
        BattleVfxCueSO cue = CreateCue("Particle");
        GameObject prefab = CreatePrefab("ParticlePrefab");
        prefab.AddComponent<ParticleSystem>();
        SetField(cue, "prefab", prefab);
        SetField(
            cue,
            "lifetimeMode",
            BattleVfxLifetimeMode.ParticleSystem);

        BattleVfxCueValidationResult result =
            BattleVfxCueValidator.Validate(cue);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.WarningCount, Is.EqualTo(0));
    }

    [Test]
    public void DuplicateCueId_IsRejectedCaseInsensitively()
    {
        BattleVfxCueSO first = CreateCue("First");
        BattleVfxCueSO second = CreateCue("Second");
        SetField(first, "cueId", "shared-cue");
        SetField(second, "cueId", "SHARED-CUE");
        SetField(first, "prefab", CreatePrefab("FirstPrefab"));
        SetField(second, "prefab", CreatePrefab("SecondPrefab"));
        SetField(first, "lifetimeMode", BattleVfxLifetimeMode.Timed);
        SetField(second, "lifetimeMode", BattleVfxLifetimeMode.Timed);

        BattleVfxCueValidationResult result =
            BattleVfxCueValidator.Validate(
                second,
                new[] { first, second });

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                result,
                "vfx.id_duplicate",
                BattleVfxCueDiagnosticSeverity.Error),
            Is.True);
    }

    [Test]
    public void EditorMenu_IsGroupedUnderEffects()
    {
        Assert.That(
            BattleVfxEditorWindow.MenuPath,
            Is.EqualTo("PS260714/Effects/Battle VFX Editor"));
        Assert.That(
            PS260714AssetEditorToolbar.ButtonOrder,
            Is.EqualTo(new[]
            {
                "New",
                "Save",
                "Duplicate",
                "Rename",
                "Delete",
                "Ping",
                "Refresh"
            }));
        Assert.That(PS260714AssetEditorList.Width, Is.EqualTo(230f));
        Assert.That(PS260714AssetEditorList.RowHeight, Is.EqualTo(42f));
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

    private GameObject CreatePrefab(string objectName)
    {
        GameObject prefab = new(objectName);
        _createdObjects.Add(prefab);
        return prefab;
    }

    private static bool HasDiagnostic(
        BattleVfxCueValidationResult result,
        string code,
        BattleVfxCueDiagnosticSeverity severity)
    {
        foreach (BattleVfxCueDiagnostic diagnostic in result.Diagnostics)
        {
            if (diagnostic.Code == code &&
                diagnostic.Severity == severity)
            {
                return true;
            }
        }

        return false;
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
}
