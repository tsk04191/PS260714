using UnityEditor;
using UnityEngine;

public static class NeutralBattleCardUserMigration
{
    private const string MenuRoot = "Tools/PS260714/Migrations/";

    [MenuItem(MenuRoot + "Audit Neutral Battle Card User")]
    private static void Audit()
    {
        Run(apply: false);
    }

    [MenuItem(MenuRoot + "Migrate Neutral Battle Card User")]
    private static void Migrate()
    {
        Run(apply: true);
    }

    private static void Run(bool apply)
    {
        int inspected = 0;
        int outdated = 0;
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:BattleCardSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleCardSO card =
                AssetDatabase.LoadAssetAtPath<BattleCardSO>(path);
            if (card == null ||
                card.Affiliation != BattleCardAffiliation.Neutral)
            {
                continue;
            }

            inspected++;
            SerializedObject serialized = new(card);
            SerializedProperty effects = serialized.FindProperty(
                "abilityEffects");
            if (effects == null || !effects.isArray)
                continue;

            bool requiresMigration = false;
            for (int index = 0; index < effects.arraySize; index++)
            {
                SerializedProperty effect =
                    effects.GetArrayElementAtIndex(index);
                SerializedProperty type = effect.FindPropertyRelative("type");
                SerializedProperty amountMode = effect.FindPropertyRelative(
                    "damageAmountMode");
                SerializedProperty targetMode = effect.FindPropertyRelative(
                    "targetMode");
                if (type == null || amountMode == null || targetMode == null)
                {
                    continue;
                }

                bool ratioUsesCharacter = UsesAmountScaling(
                        (CharacterEffectType)type.enumValueIndex) &&
                    amountMode.enumValueIndex ==
                        (int)CharacterDamageAmountMode.Ratio;
                bool targetsCharacterSource = targetMode.enumValueIndex ==
                    (int)CharacterEffectTargetMode.Source;
                if (!ratioUsesCharacter && !targetsCharacterSource)
                    continue;

                requiresMigration = true;
                if (apply && ratioUsesCharacter)
                {
                    amountMode.enumValueIndex =
                        (int)CharacterDamageAmountMode.Fixed;
                }
                if (apply && targetsCharacterSource)
                {
                    targetMode.enumValueIndex =
                        (int)CharacterEffectTargetMode.InheritAction;
                }
            }

            if (!requiresMigration)
                continue;

            outdated++;
            if (!apply)
            {
                Debug.Log(
                    $"Neutral battle-card user migration required: {path}",
                    card);
                continue;
            }

            Undo.RecordObject(card, "Migrate Neutral Battle Card User");
            if (!serialized.ApplyModifiedProperties())
                continue;

            EditorUtility.SetDirty(card);
            changed++;
        }

        if (apply && changed > 0)
            AssetDatabase.SaveAssets();

        Debug.Log(
            $"Neutral battle-card user {(apply ? "migration" : "audit")}: " +
            $"inspected={inspected}, outdated={outdated}, changed={changed}.");
    }

    private static bool UsesAmountScaling(CharacterEffectType type)
    {
        return type == CharacterEffectType.Damage ||
               type == CharacterEffectType.GainResource ||
               type == CharacterEffectType.SpendResource ||
               type == CharacterEffectType.Heal ||
               type == CharacterEffectType.SpendHealth ||
               type == CharacterEffectType.Shield ||
               type == CharacterEffectType.CardDraw;
    }
}
