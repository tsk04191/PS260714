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
    [SerializeField] private DungeonBgmProfile bgmProfile;
    [Tooltip("Legacy single-loop fallback used when no BGM Profile is assigned.")]
    [SerializeField] private AudioClip music;

    public Sprite BackgroundSprite => backgroundSprite;
    public Color BackgroundColor => backgroundColor;
    public Color FieldFrameColor => fieldFrameColor;
    public GameObject EnvironmentPrefab => environmentPrefab;
    public DungeonBgmProfile BgmProfile => bgmProfile;
    public AudioClip Music => music;
}
