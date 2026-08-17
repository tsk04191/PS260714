using UnityEditor;
using UnityEngine;

public static class EnemyCombatStatMigration
{
    private const string MenuRoot =
        "Tools/PS260714/Migrations/";

    [MenuItem(MenuRoot + "Audit Enemy Attack Power")]
    private static void Audit()
    {
        Run(apply: false);
    }

    [MenuItem(MenuRoot + "Migrate Enemy Attack Power")]
    private static void Migrate()
    {
        Run(apply: true);
    }

    private static void Run(bool apply)
    {
        int inspected = 0;
        int outdated = 0;
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemySO enemy = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (enemy == null)
                continue;

            inspected++;
            SerializedObject serialized = new(enemy);
            SerializedProperty version = serialized.FindProperty(
                "combatStatSchemaVersion");
            SerializedProperty attackPower = serialized.FindProperty(
                "attackPower");
            SerializedProperty coreAttackDamage = serialized.FindProperty(
                "coreAttackDamage");
            if (version == null || attackPower == null ||
                coreAttackDamage == null ||
                version.intValue == EnemySO.CurrentCombatStatSchemaVersion)
            {
                continue;
            }

            outdated++;
            if (!apply)
            {
                Debug.Log($"Enemy attack-power migration required: {path}", enemy);
                continue;
            }

            Undo.RecordObject(enemy, "Migrate Enemy Attack Power");
            attackPower.floatValue = Mathf.Max(
                0.1f,
                coreAttackDamage.intValue);
            version.intValue = EnemySO.CurrentCombatStatSchemaVersion;
            if (!serialized.ApplyModifiedProperties())
                continue;

            EditorUtility.SetDirty(enemy);
            changed++;
        }

        if (apply && changed > 0)
            AssetDatabase.SaveAssets();

        Debug.Log(
            $"Enemy attack-power {(apply ? "migration" : "audit")}: " +
            $"inspected={inspected}, outdated={outdated}, changed={changed}.");
    }
}
