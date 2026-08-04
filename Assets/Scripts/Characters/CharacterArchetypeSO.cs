using System;
using System.Collections.Generic;
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
    [SerializeField] private string descriptionLocalizationKey;
    [FormerlySerializedAs("koreanName")]
    [SerializeField] private string fallbackName = "ARCHETYPE";
    [FormerlySerializedAs("koreanDescription")]
    [SerializeField, TextArea(2, 6)] private string fallbackDescription;
    [SerializeField] private Sprite iconSprite;
    [SerializeField]
    private List<CharacterRolePassiveDefinition> passiveDefinitions = new();

    public string ArchetypeId => archetypeId ?? string.Empty;
    public CharacterRoleSO ParentRole => parentRole;
    public string NameLocalizationKey =>
        nameLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public string FallbackName => fallbackName ?? string.Empty;
    public string FallbackDescription =>
        fallbackDescription ?? string.Empty;
    public Sprite IconSprite => iconSprite;
    public IReadOnlyList<CharacterRolePassiveDefinition>
        PassiveDefinitions => passiveDefinitions != null
            ? passiveDefinitions
            : Array.Empty<CharacterRolePassiveDefinition>();

    public string GetDisplayName()
    {
        return CharacterRolePassiveDefinition.ResolveLocalizedText(
            nameLocalizationKey,
            fallbackName,
            "UNASSIGNED ARCHETYPE");
    }

    public string GetDescription()
    {
        return CharacterRolePassiveDefinition.ResolveLocalizedText(
            descriptionLocalizationKey,
            fallbackDescription,
            string.Empty);
    }

    public void RegenerateArchetypeId()
    {
        archetypeId = Guid.NewGuid().ToString("N");
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            archetypeId = Guid.NewGuid().ToString("N");
        archetypeId = (archetypeId ?? string.Empty).Trim();
        nameLocalizationKey =
            (nameLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        fallbackName = (fallbackName ?? string.Empty).Trim();
        fallbackDescription =
            (fallbackDescription ?? string.Empty).Trim();
        passiveDefinitions ??=
            new List<CharacterRolePassiveDefinition>();
        foreach (CharacterRolePassiveDefinition passive in
                 passiveDefinitions)
        {
            passive?.Validate();
        }

        CharacterRolePresentation.Invalidate();
    }
}
