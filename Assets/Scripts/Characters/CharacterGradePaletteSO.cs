using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterGradePalette",
    menuName = "PS260714/Characters/Grade Palette")]
public sealed class CharacterGradePaletteSO : ScriptableObject
{
    [SerializeField] private CharacterGradeStyle grade0 =
        CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade0);
    [SerializeField] private CharacterGradeStyle grade1 =
        CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade1);
    [SerializeField] private CharacterGradeStyle grade2 =
        CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade2);
    [SerializeField] private CharacterGradeStyle grade3 =
        CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade3);

    public CharacterGradeStyle GetStyle(CharacterGrade grade)
    {
        CharacterGradeStyle style = CharacterGradePresentation.Clamp(grade)
            switch
            {
                CharacterGrade.Grade3 => grade3,
                CharacterGrade.Grade2 => grade2,
                CharacterGrade.Grade1 => grade1,
                _ => grade0,
            };
        return style ?? CharacterGradePresentation.CreateFallbackStyle(grade);
    }

    private void OnValidate()
    {
        grade0 ??= CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade0);
        grade1 ??= CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade1);
        grade2 ??= CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade2);
        grade3 ??= CharacterGradePresentation.CreateFallbackStyle(
            CharacterGrade.Grade3);
        CharacterGradePresentation.Invalidate();
    }
}
