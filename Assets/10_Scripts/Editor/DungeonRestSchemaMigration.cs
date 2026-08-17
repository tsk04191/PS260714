using UnityEditor;
using UnityEngine;

public static class DungeonRestSchemaMigration
{
    private const string MenuRoot = "Tools/PS260714/Migrations/";

    [MenuItem(MenuRoot + "Audit Rest Schema")]
    private static void Audit()
    {
        Run(apply: false);
    }

    [MenuItem(MenuRoot + "Migrate Rest Schema")]
    private static void Migrate()
    {
        Run(apply: true);
    }

    private static void Run(bool apply)
    {
        int inspected = 0;
        int outdated = 0;
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:DungeonRestSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DungeonRestSO rest =
                AssetDatabase.LoadAssetAtPath<DungeonRestSO>(path);
            if (rest == null)
                continue;

            inspected++;
            if (rest.RestSchemaVersion ==
                DungeonRestSO.CurrentRestSchemaVersion)
            {
                continue;
            }

            outdated++;
            if (!apply)
            {
                Debug.Log($"Rest schema migration required: {path}", rest);
                continue;
            }

            Undo.RecordObject(rest, "Migrate Rest Schema");
            if (!rest.ApplyRestSchemaMigration(rest.Choices))
                continue;

            EditorUtility.SetDirty(rest);
            changed++;
        }

        if (apply && changed > 0)
            AssetDatabase.SaveAssets();

        Debug.Log(
            $"Rest schema {(apply ? "migration" : "audit")}: " +
            $"inspected={inspected}, outdated={outdated}, changed={changed}.");
    }
}
