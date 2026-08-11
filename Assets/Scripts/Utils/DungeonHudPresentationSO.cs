using UnityEngine;

[CreateAssetMenu(
    fileName = "DungeonHudPresentation",
    menuName = "PS260714/Presentation/Dungeon HUD")]
public sealed class DungeonHudPresentationSO : ScriptableObject
{
    [Header("World Camera")]
    [SerializeField] private Vector3 worldCameraLocalPosition =
        new(0f, 10f, -8.4f);
    [SerializeField] private Vector3 worldCameraLocalEulerAngles =
        new(50f, 0f, 0f);
    [SerializeField, Range(25f, 65f)]
    private float worldCameraFieldOfView = 40f;

    [Header("World Actors")]
    [SerializeField, Min(0.1f)] private float worldAllyHeight = 1.95f;
    [SerializeField, Min(0.1f)] private float worldEnemyHeight = 1.8f;
    [Tooltip(
        "World-space gap kept between enemy SD sprites and the outer edge " +
        "of the arena shield gauge.")]
    [SerializeField, Min(0f)]
    private float worldEnemyArenaRingClearance = 0.65f;
    [SerializeField, Min(100)] private int worldDepthSortingRange = 8000;

    [Header("World Movement")]
    [SerializeField] private Sprite movementDestinationSprite;
    [SerializeField] private Color movementLineColor =
        new(1f, 0.24f, 0.18f, 0.92f);
    [SerializeField] private Color movementDestinationColor =
        new(1f, 0.36f, 0.22f, 0.96f);
    [SerializeField, Min(0.01f)] private float movementLineWidth = 0.055f;
    [SerializeField, Min(0.05f)] private float movementDestinationSize = 0.48f;

    [Header("World Character Status")]
    [SerializeField] private Sprite abilityReadySprite;
    [SerializeField] private Color abilityReadyColor = Color.white;
    [SerializeField, Min(0.05f)] private float abilityReadyIconSize = 0.34f;
    [Tooltip("World-space gap between the SD character's head and the icon.")]
    [SerializeField, Min(0f)] private float abilityReadyIconOffset = 0.2f;
    [SerializeField] private Color attackCooldownTrackColor =
        new(0.04f, 0.05f, 0.07f, 0.74f);
    [SerializeField] private Color attackCooldownReadyColor =
        new(1f, 0.78f, 0.18f, 0.98f);
    [SerializeField, Min(0.05f)] private float attackCooldownRingRadius = 0.39f;
    [SerializeField, Min(0.01f)] private float attackCooldownRingWidth = 0.055f;

    [Header("Battle Core Ring")]
    [SerializeField] private Color battleCoreRingTrackColor =
        new(0.03f, 0.06f, 0.08f, 0.72f);
    [SerializeField] private Color battleCoreRingDelayedColor =
        new(1f, 0.85f, 0.35f, 0.78f);
    [SerializeField] private Color battleCoreRingHealthyColor =
        new(0.2f, 0.9f, 0.68f, 1f);
    [SerializeField] private Color battleCoreRingCriticalColor =
        new(1f, 0.18f, 0.16f, 1f);
    [Tooltip("World-space width of the shield gauge on the arena floor.")]
    [SerializeField, Min(0.01f)]
    private float battleCoreRingThickness = 0.09f;
    [Tooltip("World-space gap outside the configured arena radius.")]
    [SerializeField, Min(0f)] private float battleCoreRingGap = 0.18f;
    [SerializeField, Min(0f)] private float battleCoreRingGroundHeight = 0.09f;
    [SerializeField, Range(24, 256)] private int battleCoreRingSegments = 128;
    [SerializeField, Range(-360f, 360f)]
    private float battleCoreRingStartAngle = 90f;
    [SerializeField, Range(1f, 360f)]
    private float battleCoreRingSweepAngle = 360f;
    [SerializeField] private bool battleCoreRingClockwise = true;
    [SerializeField, Min(0.01f)]
    private float battleCoreRingAnimationDuration = 0.18f;
    [SerializeField, Min(0f)] private float battleCoreRingDamageDelay = 0.25f;
    [SerializeField, Min(0.01f)]
    private float battleCoreRingDelayedDuration = 0.35f;
    [SerializeField, Range(0f, 1f)]
    private float battleCoreRingCriticalThreshold = 0.25f;

    [Header("Battle UI")]
    [SerializeField, Range(0f, 1f)] private float hudPanelAlpha = 0.72f;
    [SerializeField, Range(0f, 1f)] private float characterPanelAlpha = 0.78f;
    [SerializeField, Range(0f, 1f)] private float cardPanelAlpha = 0.9f;

    public Vector3 WorldCameraLocalPosition => worldCameraLocalPosition;
    public Vector3 WorldCameraLocalEulerAngles =>
        worldCameraLocalEulerAngles;
    public float WorldCameraFieldOfView =>
        Mathf.Clamp(worldCameraFieldOfView, 25f, 65f);
    public float WorldAllyHeight => Mathf.Max(0.1f, worldAllyHeight);
    public float WorldEnemyHeight => Mathf.Max(0.1f, worldEnemyHeight);
    public float WorldEnemyArenaRingClearance =>
        Mathf.Max(0f, worldEnemyArenaRingClearance);
    public int WorldDepthSortingRange =>
        Mathf.Max(100, worldDepthSortingRange);
    public Sprite MovementDestinationSprite => movementDestinationSprite;
    public Color MovementLineColor => movementLineColor;
    public Color MovementDestinationColor => movementDestinationColor;
    public float MovementLineWidth => Mathf.Max(0.01f, movementLineWidth);
    public float MovementDestinationSize =>
        Mathf.Max(0.05f, movementDestinationSize);
    public Sprite AbilityReadySprite => abilityReadySprite;
    public Color AbilityReadyColor => abilityReadyColor;
    public float AbilityReadyIconSize => Mathf.Max(0.05f, abilityReadyIconSize);
    public float AbilityReadyIconOffset => Mathf.Max(0f, abilityReadyIconOffset);
    public Color AttackCooldownTrackColor => attackCooldownTrackColor;
    public Color AttackCooldownReadyColor => attackCooldownReadyColor;
    public float AttackCooldownRingRadius =>
        Mathf.Max(0.05f, attackCooldownRingRadius);
    public float AttackCooldownRingWidth =>
        Mathf.Max(0.01f, attackCooldownRingWidth);
    public Color BattleCoreRingTrackColor => battleCoreRingTrackColor;
    public Color BattleCoreRingDelayedColor => battleCoreRingDelayedColor;
    public Color BattleCoreRingHealthyColor => battleCoreRingHealthyColor;
    public Color BattleCoreRingCriticalColor => battleCoreRingCriticalColor;
    public float BattleCoreRingThickness =>
        Mathf.Max(0.01f, battleCoreRingThickness);
    public float BattleCoreRingGap => Mathf.Max(0f, battleCoreRingGap);
    public float BattleCoreRingGroundHeight =>
        Mathf.Max(0f, battleCoreRingGroundHeight);
    public int BattleCoreRingSegments =>
        Mathf.Clamp(battleCoreRingSegments, 24, 256);
    public float BattleCoreRingStartAngle =>
        Mathf.Clamp(battleCoreRingStartAngle, -360f, 360f);
    public float BattleCoreRingSweepAngle =>
        Mathf.Clamp(battleCoreRingSweepAngle, 1f, 360f);
    public bool BattleCoreRingClockwise => battleCoreRingClockwise;
    public float BattleCoreRingAnimationDuration =>
        Mathf.Max(0.01f, battleCoreRingAnimationDuration);
    public float BattleCoreRingDamageDelay =>
        Mathf.Max(0f, battleCoreRingDamageDelay);
    public float BattleCoreRingDelayedDuration =>
        Mathf.Max(0.01f, battleCoreRingDelayedDuration);
    public float BattleCoreRingCriticalThreshold =>
        Mathf.Clamp01(battleCoreRingCriticalThreshold);
    public float HudPanelAlpha => Mathf.Clamp01(hudPanelAlpha);
    public float CharacterPanelAlpha => Mathf.Clamp01(characterPanelAlpha);
    public float CardPanelAlpha => Mathf.Clamp01(cardPanelAlpha);

    private void OnValidate()
    {
        worldCameraFieldOfView = Mathf.Clamp(
            worldCameraFieldOfView,
            25f,
            65f);
        worldAllyHeight = Mathf.Max(0.1f, worldAllyHeight);
        worldEnemyHeight = Mathf.Max(0.1f, worldEnemyHeight);
        worldEnemyArenaRingClearance = Mathf.Max(
            0f,
            worldEnemyArenaRingClearance);
        worldDepthSortingRange = Mathf.Max(100, worldDepthSortingRange);
        movementLineWidth = Mathf.Max(0.01f, movementLineWidth);
        movementDestinationSize = Mathf.Max(0.05f, movementDestinationSize);
        abilityReadyIconSize = Mathf.Max(0.05f, abilityReadyIconSize);
        abilityReadyIconOffset = Mathf.Max(0f, abilityReadyIconOffset);
        attackCooldownRingRadius = Mathf.Max(0.05f, attackCooldownRingRadius);
        attackCooldownRingWidth = Mathf.Max(0.01f, attackCooldownRingWidth);
        battleCoreRingThickness = Mathf.Max(0.01f, battleCoreRingThickness);
        battleCoreRingGap = Mathf.Max(0f, battleCoreRingGap);
        battleCoreRingGroundHeight = Mathf.Max(
            0f,
            battleCoreRingGroundHeight);
        battleCoreRingSegments = Mathf.Clamp(battleCoreRingSegments, 24, 256);
        battleCoreRingStartAngle = Mathf.Clamp(
            battleCoreRingStartAngle,
            -360f,
            360f);
        battleCoreRingSweepAngle = Mathf.Clamp(
            battleCoreRingSweepAngle,
            1f,
            360f);
        battleCoreRingAnimationDuration = Mathf.Max(
            0.01f,
            battleCoreRingAnimationDuration);
        battleCoreRingDamageDelay = Mathf.Max(0f, battleCoreRingDamageDelay);
        battleCoreRingDelayedDuration = Mathf.Max(
            0.01f,
            battleCoreRingDelayedDuration);
        battleCoreRingCriticalThreshold = Mathf.Clamp01(
            battleCoreRingCriticalThreshold);
        hudPanelAlpha = Mathf.Clamp01(hudPanelAlpha);
        characterPanelAlpha = Mathf.Clamp01(characterPanelAlpha);
        cardPanelAlpha = Mathf.Clamp01(cardPanelAlpha);
    }
}

public static class DungeonHudPresentation
{
    private static DungeonHudPresentationSO _cached;
    private static DungeonHudPresentationSO _fallback;

    public static DungeonHudPresentationSO Load()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<DungeonHudPresentationSO>(
            CommonDef.DungeonHudPresentationResourcePath);
        if (_cached != null)
            return _cached;

        if (_fallback == null)
        {
            _fallback = ScriptableObject.CreateInstance<
                DungeonHudPresentationSO>();
            _fallback.hideFlags = HideFlags.HideAndDontSave;
        }
        return _fallback;
    }

    public static void Invalidate()
    {
        _cached = null;
    }
}
