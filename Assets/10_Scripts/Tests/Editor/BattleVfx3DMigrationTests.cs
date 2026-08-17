using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BattleVfx3DMigrationTests
{
    private const string FireStatusPath =
        "Assets/06_Runtime/Resources/StatusEffects/Fire.asset";
    private const string DungeonTilePath =
        "Assets/07_Prefabs/UI/Dungeon/DungeonTile.prefab";
    private const string AreaPreviewPath =
        "Assets/06_Runtime/Resources/Presentation/DungeonWorld/" +
        "DungeonBattleAreaPreview.prefab";

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
    public void LegacyDungeonTileAnd2DFireOverlayAssets_AreDeleted()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(DungeonTilePath);

        Assert.That(prefab, Is.Null);
        Assert.That(
            typeof(BattleEditorWindow).Assembly.GetType(
                "FireStatusEffectAssetGenerator"),
            Is.Null);
        Assert.That(
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/02_Visual/VFX/Animations/Battle/FireStatus/" +
                "FireStatus.controller"),
            Is.Null);
        Assert.That(
            AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/02_Visual/VFX/Animations/Battle/FireStatus/" +
                "FireStatusHidden.anim"),
            Is.Null);
        Assert.That(
            AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/02_Visual/VFX/Animations/Battle/FireStatus/" +
                "FireStatusLoop.anim"),
            Is.Null);
    }

    [Test]
    public void BattleAreaPreview_UsesAuthoredWorldMeshPrefab()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(AreaPreviewPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(
            prefab.GetComponent<DungeonBattleAreaPreviewPrefabView>(),
            Is.Not.Null);
        Assert.That(prefab.GetComponent<MeshFilter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<MeshRenderer>(), Is.Not.Null);
        Assert.That(
            prefab.GetComponentInChildren<DungeonWorldPolylineRenderer>(true),
            Is.Not.Null);
    }
}
