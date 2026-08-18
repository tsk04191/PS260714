using UnityEditor;
using UnityEngine;

public static class EnemyRosterSchemaMigration
{
    private const string MenuRoot = "Tools/PS260714/Migrations/";

    [MenuItem(MenuRoot + "Audit Enemy Roster Metadata")]
    private static void Audit()
    {
        Run(apply: false);
    }

    [MenuItem(MenuRoot + "Migrate Enemy Roster Metadata")]
    private static void Migrate()
    {
        Run(apply: true);
    }

    private static void Run(bool apply)
    {
        int inspected = 0;
        int outdated = 0;
        int changed = 0;
        int unsupported = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemySO enemy = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (enemy == null)
                continue;

            inspected++;
            if (enemy.AuthoredRosterSchemaVersion ==
                EnemySO.CurrentRosterSchemaVersion)
            {
                continue;
            }

            if (enemy.AuthoredRosterSchemaVersion >
                EnemySO.CurrentRosterSchemaVersion)
            {
                unsupported++;
                Debug.LogWarning(
                    $"Enemy roster schema " +
                    $"{enemy.AuthoredRosterSchemaVersion} is newer than " +
                    $"the supported schema " +
                    $"{EnemySO.CurrentRosterSchemaVersion}: {path}",
                    enemy);
                continue;
            }

            outdated++;
            if (!apply)
            {
                Debug.Log(
                    $"Enemy roster migration required " +
                    $"(schema {enemy.AuthoredRosterSchemaVersion} -> " +
                    $"{EnemySO.CurrentRosterSchemaVersion}): {path}",
                    enemy);
                continue;
            }

            Undo.RecordObject(enemy, "Migrate Enemy Roster Metadata");
            if (!ApplyMigration(enemy))
                continue;

            EditorUtility.SetDirty(enemy);
            changed++;
        }

        if (apply && changed > 0)
            AssetDatabase.SaveAssets();

        Debug.Log(
            $"Enemy roster {(apply ? "migration" : "audit")}: " +
            $"inspected={inspected}, outdated={outdated}, " +
            $"changed={changed}, unsupported={unsupported}.");
    }

    internal static bool ApplyMigration(EnemySO enemy)
    {
        if (enemy == null ||
            enemy.AuthoredRosterSchemaVersion >=
                EnemySO.CurrentRosterSchemaVersion)
        {
            return false;
        }

        SerializedObject serialized = new(enemy);
        serialized.Update();
        SerializedProperty schema = serialized.FindProperty(
            "rosterSchemaVersion");
        SerializedProperty tier = serialized.FindProperty("rosterTier");
        SerializedProperty roleTags = serialized.FindProperty("roleTags");
        SerializedProperty waveCap = serialized.FindProperty(
            "recommendedMaxPerWave");
        SerializedProperty encounterOnly = serialized.FindProperty(
            "encounterOnly");
        SerializedProperty preciseDamage = serialized.FindProperty(
            "preciseCoreAttackDamage");
        SerializedProperty damagePolicy = serialized.FindProperty(
            "coreAttackDamagePolicy");
        SerializedProperty legacyDamage = serialized.FindProperty(
            "coreAttackDamage");
        if (schema == null || tier == null || roleTags == null ||
            waveCap == null || encounterOnly == null ||
            preciseDamage == null || damagePolicy == null ||
            legacyDamage == null)
        {
            Debug.LogError(
                "Enemy roster migration fields are unavailable.",
                enemy);
            return false;
        }

        EnemyRosterTier resolvedTier = EnemySO.GetRosterTier(enemy.Grade);
        tier.enumValueIndex = (int)resolvedTier;
        if (roleTags.arraySize == 0)
        {
            roleTags.arraySize = 1;
            roleTags.GetArrayElementAtIndex(0).stringValue =
                EnemyTypeDisplay.GetId(enemy.Type);
        }

        if (waveCap.intValue <= 0)
        {
            waveCap.intValue = resolvedTier switch
            {
                EnemyRosterTier.Special => 2,
                EnemyRosterTier.Elite => 1,
                EnemyRosterTier.Boss => 1,
                _ => 0,
            };
        }
        encounterOnly.boolValue =
            encounterOnly.boolValue ||
            resolvedTier == EnemyRosterTier.Boss;
        preciseDamage.floatValue = Mathf.Max(1, legacyDamage.intValue);
        damagePolicy.enumValueIndex =
            (int)EnemyCoreAttackDamagePolicy.LegacyInteger;
        schema.intValue = EnemySO.CurrentRosterSchemaVersion;
        return serialized.ApplyModifiedProperties();
    }
}
