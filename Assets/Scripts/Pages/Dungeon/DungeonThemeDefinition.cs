using UnityEngine;

[CreateAssetMenu(
    fileName = "DungeonTheme",
    menuName = "Dungeon/Theme")]
public sealed class DungeonThemeDefinition : ScriptableObject
{
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Color backgroundColor = Color.white;
    [SerializeField] private Color fieldFrameColor = Color.white;
    [SerializeField] private GameObject environmentPrefab;

    public Sprite BackgroundSprite => backgroundSprite;
    public Color BackgroundColor => backgroundColor;
    public Color FieldFrameColor => fieldFrameColor;
    public GameObject EnvironmentPrefab => environmentPrefab;
}
