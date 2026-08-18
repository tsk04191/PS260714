using UnityEditor;
using UnityEngine;

public static class EnemyCombatStatMigration
{
    private const string MenuRoot =
        "Tools/PS260714/Migrations/";
    private const int AttackPowerSchemaVersion = 1;
    private const int FormationSchemaVersion = 2;

    [MenuItem(MenuRoot + "Audit Enemy Combat Stats")]
    private static void Audit()
    {
        Run(apply: false);
    }

    [MenuItem(MenuRoot + "Migrate Enemy Combat Stats")]
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
            int version = enemy.AuthoredCombatStatSchemaVersion;
            if (version == EnemySO.CurrentCombatStatSchemaVersion)
            {
                continue;
            }

            if (version > EnemySO.CurrentCombatStatSchemaVersion)
            {
                unsupported++;
                Debug.LogWarning(
                    $"Enemy combat schema {version} is newer than the " +
                    $"supported schema " +
                    $"{EnemySO.CurrentCombatStatSchemaVersion}: {path}",
                    enemy);
                continue;
            }

            outdated++;
            if (!apply)
            {
                Debug.Log(
                    $"Enemy combat-stat migration required " +
                    $"(schema {version} -> " +
                    $"{EnemySO.CurrentCombatStatSchemaVersion}): {path}",
                    enemy);
                continue;
            }

            Undo.RecordObject(enemy, "Migrate Enemy Combat Stats");
            if (!ApplyMigration(enemy))
                continue;

            EditorUtility.SetDirty(enemy);
            changed++;
        }

        if (apply && changed > 0)
            AssetDatabase.SaveAssets();

        Debug.Log(
            $"Enemy combat-stat {(apply ? "migration" : "audit")}: " +
            $"inspected={inspected}, outdated={outdated}, " +
            $"changed={changed}, unsupported={unsupported}.");
    }

    internal static bool ApplyMigration(EnemySO enemy)
    {
        if (enemy == null)
            return false;

        SerializedObject serialized = new(enemy);
        serialized.Update();
        SerializedProperty version = serialized.FindProperty(
            "combatStatSchemaVersion");
        SerializedProperty attackPower = serialized.FindProperty(
            "attackPower");
        SerializedProperty coreAttackDamage = serialized.FindProperty(
            "coreAttackDamage");
        SerializedProperty formationRadius = serialized.FindProperty(
            "formationRadius");
        SerializedProperty coreAttackRange = serialized.FindProperty(
            "coreAttackRange");
        if (version == null || attackPower == null ||
            coreAttackDamage == null || formationRadius == null ||
            coreAttackRange == null)
        {
            Debug.LogError(
                "Enemy combat-stat migration fields are unavailable.",
                enemy);
            return false;
        }

        int sourceVersion = version.intValue;
        if (sourceVersion >= EnemySO.CurrentCombatStatSchemaVersion)
            return false;

        if (sourceVersion < AttackPowerSchemaVersion)
        {
            attackPower.floatValue = Mathf.Max(
                0.1f,
                coreAttackDamage.intValue);
        }

        if (sourceVersion < FormationSchemaVersion)
        {
            formationRadius.floatValue =
                EnemySO.GetDefaultFormationRadius(enemy.Type);
            coreAttackRange.floatValue = 0f;
        }

        version.intValue = EnemySO.CurrentCombatStatSchemaVersion;
        return serialized.ApplyModifiedProperties();
    }
}
