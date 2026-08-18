#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class EnemyRosterCatalogGeneratorTests
{
    [Test]
    public void SourceAudit_HasExpectedTierCountsAndAssumptions()
    {
        EnemyRosterCatalogAudit audit =
            EnemyRosterCatalogGenerator.AuditCatalogSource();

        Assert.That(audit.TotalCount, Is.EqualTo(46));
        Assert.That(audit.GeneralCount, Is.EqualTo(30));
        Assert.That(audit.SpecialCount, Is.EqualTo(10));
        Assert.That(audit.EliteCount, Is.EqualTo(5));
        Assert.That(audit.BossCount, Is.EqualTo(1));
        Assert.That(audit.EncounterOnlyCount, Is.EqualTo(1));
        Assert.That(audit.FractionalCoreDamageCount, Is.EqualTo(11));
        Assert.That(audit.AssumptionCount, Is.GreaterThanOrEqualTo(16));
        Assert.That(
            EnemyRosterCatalogGenerator.GetAssumptionIds().Distinct().Count(),
            Is.EqualTo(audit.AssumptionCount));
        Assert.That(
            EnemyRosterCatalogGenerator.GetAssumptionIds(),
            Does.Contain("A21"));
    }

    [Test]
    public void FractionalCoreDamage_KeepsPreciseAndLegacyValues()
    {
        Assert.That(
            EnemyRosterCatalogGenerator.TryGetSpecSummary(
                "G003",
                out EnemyRosterSpecSummary heavy),
            Is.True);
        Assert.That(heavy.PreciseCoreAttackDamage, Is.EqualTo(7.5f));
        Assert.That(heavy.LegacyCoreAttackDamage, Is.EqualTo(8));

        Assert.That(
            EnemyRosterCatalogGenerator.TryGetSpecSummary(
                "G008",
                out EnemyRosterSpecSummary swarm),
            Is.True);
        Assert.That(swarm.PreciseCoreAttackDamage, Is.EqualTo(1.75f));
        Assert.That(swarm.LegacyCoreAttackDamage, Is.EqualTo(2));
    }

    [Test]
    public void Summons_AreNonRecursiveAndRespectGlobalCap()
    {
        foreach (string id in new[]
                 {
                     "G007", "G008", "G023", "G027", "S007", "E003",
                     "B001",
                 })
        {
            Assert.That(
                EnemyRosterCatalogGenerator.TryGetSpecSummary(
                    id,
                    out EnemyRosterSpecSummary summary),
                Is.True,
                id);
            Assert.That(summary.AllowsRecursiveSummon, Is.False, id);
            Assert.That(
                summary.MaximumActiveSummons,
                Is.InRange(1, EnemyRosterCatalogGenerator.SimultaneousSummonCap),
                id);
        }
    }

    [Test]
    public void Boss_IsDedicatedEncounterWithExplicitLargeFootprintStats()
    {
        Assert.That(
            EnemyRosterCatalogGenerator.TryGetSpecSummary(
                "B001",
                out EnemyRosterSpecSummary boss),
            Is.True);
        Assert.That(boss.Tier, Is.EqualTo(EnemyRosterTier.Boss));
        Assert.That(boss.EncounterOnly, Is.True);
        Assert.That(boss.RecommendedMaxPerWave, Is.EqualTo(1));
        Assert.That(boss.BaseHealth, Is.EqualTo(560));
        Assert.That(boss.PreciseCoreAttackDamage, Is.EqualTo(20f));
    }

    [Test]
    public void InferredSupportAndRangeRules_AreDeterministic()
    {
        Assert.That(
            EnemyRosterCatalogGenerator.TryGetSpecSummary(
                "G014",
                out EnemyRosterSpecSummary pulsar),
            Is.True);
        Assert.That(pulsar.RecommendedMaxPerWave, Is.EqualTo(2));
        Assert.That(pulsar.CoreAttackRange, Is.EqualTo(0.6f));

        Assert.That(
            EnemyRosterCatalogGenerator.TryGetSpecSummary(
                "G001",
                out EnemyRosterSpecSummary baseline),
            Is.True);
        Assert.That(baseline.RecommendedMaxPerWave, Is.Zero);
        Assert.That(baseline.CoreAttackRange, Is.Zero);
        Assert.That(
            baseline.ForwardSearchAngle,
            Is.EqualTo(EnemySO.DefaultForwardSearchAngle));
    }

    [Test]
    public void GeneratedCatalog_HasAllIdsAndPreservesLegacyGuids()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EnemySO",
            new[] { "Assets/06_Runtime/Resources/Enemies" });
        List<EnemySO> definitions = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                Path.GetDirectoryName(path)?.Replace('\\', '/') ==
                "Assets/06_Runtime/Resources/Enemies")
            .Select(AssetDatabase.LoadAssetAtPath<EnemySO>)
            .Where(item => item != null)
            .ToList();

        Assert.That(definitions.Count, Is.EqualTo(46));
        Assert.That(
            definitions.Select(item => item.EnemyId).Distinct().Count(),
            Is.EqualTo(46));
        AssertLegacy("G001_BasicRemnant.asset", "G001", "a4cc83968a6844d89f4087133b25ee3e");
        AssertLegacy("G002_AssaultRemnant.asset", "G002", "fdbb9b76bc2747528b12a02ed1174696");
        AssertLegacy("G003_HeavyRemnant.asset", "G003", "273cd6a369fc4f04a91f700675cbff67");
        AssertLegacy("S001_MedicRemnant.asset", "S001", "bb549f894fe9441caafb1499d2b3d6dc");
        AssertLegacy("S002_MechanicRemnant.asset", "S002", "c1cc343b600a41c5a4131a5ddf257a87");
        AssertLegacy("S003_InfiltratorRemnant.asset", "S003", "8ea554dd9a134ef8b3a9a88b227a4971");
        AssertLegacy("S004_PointmanRemnant.asset", "S004", "7df2e18bb63f4108a85bdce82e508f53");
        AssertLegacy("S005_ShieldBearerRemnant.asset", "S005", "273cc0a824e845bda2fcf898627236a7");
    }

    [Test]
    public void GeneratedCatalog_FileNamesFollowRosterConvention()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EnemySO",
            new[] { "Assets/06_Runtime/Resources/Enemies" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path)?.Replace('\\', '/') !=
                "Assets/06_Runtime/Resources/Enemies")
            {
                continue;
            }

            EnemySO definition =
                AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            Assert.That(definition, Is.Not.Null, path);
            string safeName = new(
                definition.DisplayName
                    .Where(char.IsLetterOrDigit)
                    .ToArray());
            Assert.That(
                Path.GetFileName(path),
                Is.EqualTo($"{definition.EnemyId}_{safeName}.asset"),
                path);
            Assert.That(
                definition.name,
                Is.EqualTo(Path.GetFileNameWithoutExtension(path)),
                path);
        }
    }

    private static void AssertLegacy(
        string fileName,
        string expectedId,
        string expectedGuid)
    {
        string path = "Assets/06_Runtime/Resources/Enemies/" + fileName;
        EnemySO definition = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
        Assert.That(definition, Is.Not.Null, path);
        Assert.That(definition.EnemyId, Is.EqualTo(expectedId), path);
        Assert.That(
            AssetDatabase.AssetPathToGUID(path),
            Is.EqualTo(expectedGuid),
            path);
    }
}
#endif
