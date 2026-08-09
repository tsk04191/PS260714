using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleVfx3DMigrationTests
{
    private const string FireStatusPath =
        "Assets/Resources/StatusEffects/Fire.asset";
    private const string DungeonTilePath =
        "Assets/Prefabs/UI/Dungeon/DungeonTile.prefab";

    [Test]
    public void FireStatus_UsesPersistent3DLoopCue()
    {
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FireStatusPath);

        Assert.That(fire, Is.Not.Null);
        Assert.That(fire.VisualEffectPrefab, Is.Null);
        Assert.That(fire.LoopVfxCue, Is.Not.Null);
        Assert.That(fire.LoopVfxCue.IsPersistent, Is.True);
        Assert.That(
            fire.LoopVfxCue.AnchorType,
            Is.EqualTo(BattleVfxAnchorType.Ground));
        Assert.That(
            fire.LoopVfxCue.AttachMode,
            Is.EqualTo(BattleVfxAttachMode.FollowTarget));
        Assert.That(
            fire.LoopVfxCue.StopMode,
            Is.EqualTo(BattleVfxStopMode.Immediate));
        foreach (BattleVfxClipDefinition clip in
                 fire.LoopVfxCue.Clips)
        {
            Assert.That(
                clip.StopMode,
                Is.EqualTo(BattleVfxStopMode.Immediate));
        }
    }

    [Test]
    public void DungeonTile_HasNoLegacy2DFireOverlay()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(DungeonTilePath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.Find("grpFireStatusEffect"), Is.Null);
        Assert.That(
            typeof(BattleEditorWindow).Assembly.GetType(
                "FireStatusEffectAssetGenerator"),
            Is.Null);
        Assert.That(
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Battle/FireStatus/" +
                "FireStatus.controller"),
            Is.Null);
        Assert.That(
            AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Animations/Battle/FireStatus/" +
                "FireStatusHidden.anim"),
            Is.Null);
        Assert.That(
            AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Animations/Battle/FireStatus/" +
                "FireStatusLoop.anim"),
            Is.Null);
    }

    [Test]
    public void DungeonTile_TargetAreaFeedbackDoesNotAccumulateOpacity()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(DungeonTilePath);
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab);

        try
        {
            DungeonTileView tile = instance.GetComponent<DungeonTileView>();
            Image overlay = instance.transform
                .Find("imgAttackRangeOverlay")
                ?.GetComponent<Image>();
            MethodInfo showTargetArea = typeof(DungeonTileView).GetMethod(
                "ShowTargetArea",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo remainingField = typeof(DungeonTileView).GetField(
                "_targetAreaDisplayRemaining",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(tile, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(showTargetArea, Is.Not.Null);
            Assert.That(remainingField, Is.Not.Null);

            showTargetArea.Invoke(tile, null);
            Color firstColor = overlay.color;
            float firstRemaining = (float)remainingField.GetValue(tile);
            showTargetArea.Invoke(tile, null);

            Assert.That(overlay.enabled, Is.True);
            Assert.That(overlay.color, Is.EqualTo(firstColor));
            Assert.That(
                (float)remainingField.GetValue(tile),
                Is.EqualTo(firstRemaining));
            Assert.That(firstColor.a, Is.LessThanOrEqualTo(0.3f));
            Assert.That(firstColor.g, Is.GreaterThan(0.25f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
