using UnityEngine;

[DisallowMultipleComponent]
public sealed class DungeonWorldActorPrefabView : MonoBehaviour
{
    [SerializeField] private Transform footHudRoot;
    [SerializeField] private Transform verticalBillboardRoot;
    [SerializeField] private Transform actorTransform;
    [SerializeField] private SpriteRenderer actorRenderer;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private DungeonWorldPolylineRenderer movementLine;
    [SerializeField] private SpriteRenderer movementMarker;
    [SerializeField] private DungeonWorldPolylineRenderer movementMarkerRing;
    [SerializeField] private DungeonWorldPolylineRenderer cooldownTrack;
    [SerializeField] private DungeonWorldPolylineRenderer cooldownFill;
    [SerializeField] private DungeonWorldPolylineRenderer enemyHealthTrack;
    [SerializeField] private DungeonWorldPolylineRenderer enemyHealthFill;
    [SerializeField] private SpriteRenderer abilityReady;

    public Transform FootHudRoot => footHudRoot;
    public Transform VerticalBillboardRoot => verticalBillboardRoot;
    public Transform ActorTransform => actorTransform;
    public SpriteRenderer ActorRenderer => actorRenderer;
    public SpriteRenderer ShadowRenderer => shadowRenderer;
    public DungeonWorldPolylineRenderer MovementLine => movementLine;
    public SpriteRenderer MovementMarker => movementMarker;
    public DungeonWorldPolylineRenderer MovementMarkerRing => movementMarkerRing;
    public DungeonWorldPolylineRenderer CooldownTrack => cooldownTrack;
    public DungeonWorldPolylineRenderer CooldownFill => cooldownFill;
    public DungeonWorldPolylineRenderer EnemyHealthTrack => enemyHealthTrack;
    public DungeonWorldPolylineRenderer EnemyHealthFill => enemyHealthFill;
    public SpriteRenderer AbilityReady => abilityReady;

    public bool HasRequiredReferences =>
        footHudRoot != null &&
        verticalBillboardRoot != null &&
        actorTransform != null &&
        actorRenderer != null &&
        shadowRenderer != null &&
        movementLine != null &&
        movementMarker != null &&
        movementMarkerRing != null &&
        cooldownTrack != null &&
        cooldownFill != null &&
        enemyHealthTrack != null &&
        enemyHealthFill != null &&
        abilityReady != null;
}
