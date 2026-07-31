using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "CharacterArchetype",
    menuName = "PS260714/Characters/Archetype")]
public sealed class CharacterArchetypeSO : ScriptableObject
{
    [SerializeField] private string archetypeId =
        Guid.NewGuid().ToString("N");
    [SerializeField] private CharacterRoleSO parentRole;
    [SerializeField] private string nameLocalizationKey;
    [FormerlySerializedAs("koreanName")]
    [SerializeField] private string fallbackName = "ARCHETYPE";
    [SerializeField] private Sprite iconSprite;

    public string ArchetypeId => archetypeId ?? string.Empty;
    public CharacterRoleSO ParentRole => parentRole;
    public string NameLocalizationKey =>
        nameLocalizationKey ?? string.Empty;
    public string FallbackName => fallbackName ?? string.Empty;
    public Sprite IconSprite => iconSprite;

    public string GetDisplayName()
    {
        return CharacterRolePassiveDefinition.ResolveLocalizedText(
            nameLocalizationKey,
            fallbackName,
            "UNASSIGNED ARCHETYPE");
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            archetypeId = Guid.NewGuid().ToString("N");
        archetypeId = (archetypeId ?? string.Empty).Trim();
        nameLocalizationKey =
            (nameLocalizationKey ?? string.Empty).Trim();
        fallbackName = (fallbackName ?? string.Empty).Trim();
        CharacterRolePresentation.Invalidate();
    }
}
