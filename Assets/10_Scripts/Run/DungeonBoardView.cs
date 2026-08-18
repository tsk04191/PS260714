using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonBoardView : MonoBehaviour, IBattleBoard,
    IDungeonStageProgressProvider,
    IBattlePresentationEventPublisher,
    IBattleVfxTargetResolver,
    IBattleManualTargetSelectionService,
    IBattleCardDrawServiceProvider,
    IBattleAbilityUserModifierServiceProvider,
    IBattleCardControlServiceProvider,
    IBattleEnemySummonServiceProvider,
    IBattleObjectiveProvider,
    IBattleSpatialServiceProvider,
    IBattleSpatialService,
    IEnemyCombatRuntimeServiceProvider,
    IEnemyCombatRuntimeService,
    IPracticeBattleDebugVisualization
{
    private const int MaximumStatusEventsPerDispatch = 128;
    private const int MaximumDefeatEventsPerDispatch = 128;
    private const int MaximumPresentationEventsPerDispatch = 256;
    private const float AuthoredArenaRingRadius = 2.24f;
    private const float MinimumWorldGroundSize = 40f;
    private const float WorldActorGroundHeight = 0f;
    public const int MinimumGridSize = 3;
    public const int MaximumGridSize = 9;
    private const float FormationArrivalTolerance = 0.001f;
    private const int PracticeDebugGroundCircleSegments = 48;

    private sealed class EnemyPlacement
    {
        public EnemyRuntime Enemy { get; }
        public DungeonBoardSlot Anchor { get; }
        public IReadOnlyList<DungeonBoardSlot> OccupiedTiles { get; }
        public bool IsExclusive { get; }

        public EnemyPlacement(
            EnemyRuntime enemy,
            DungeonBoardSlot anchor,
            IReadOnlyList<DungeonBoardSlot> occupiedTiles,
            bool isExclusive)
        {
            Enemy = enemy;
            Anchor = anchor;
            OccupiedTiles = occupiedTiles;
            IsExclusive = isExclusive;
        }
    }

    private sealed class TimedScalarModifier
    {
        public string SourceId { get; }
        public float Multiplier { get; private set; }
        public float Remaining { get; private set; }

        public TimedScalarModifier(
            string sourceId,
            float multiplier,
            float duration)
        {
            SourceId = sourceId;
            Refresh(multiplier, duration);
        }

        public bool Refresh(float multiplier, float duration)
        {
            multiplier = Mathf.Max(0f, multiplier);
            float resolvedDuration = duration > 0f
                ? duration
                : float.PositiveInfinity;
            bool changed = !Mathf.Approximately(Multiplier, multiplier) ||
                           resolvedDuration > Remaining;
            Multiplier = multiplier;
            Remaining = Mathf.Max(Remaining, resolvedDuration);
            return changed;
        }

        public bool Tick(float deltaTime)
        {
            if (float.IsPositiveInfinity(Remaining))
                return false;
            Remaining = Mathf.Max(0f, Remaining - deltaTime);
            return Remaining <= 0f;
        }
    }

    private sealed class EnemyDamageLinkGroup
    {
        public EnemyRuntime Owner { get; }
        public string SourceId { get; }
        public float ShareRatio { get; }
        public float Remaining { get; private set; }
        public List<EnemyRuntime> Members { get; } = new();

        public EnemyDamageLinkGroup(
            EnemyRuntime owner,
            string sourceId,
            float shareRatio,
            float duration)
        {
            Owner = owner;
            SourceId = sourceId;
            ShareRatio = Mathf.Clamp01(shareRatio);
            Remaining = duration > 0f
                ? duration
                : float.PositiveInfinity;
        }

        public bool Tick(float deltaTime)
        {
            if (!float.IsPositiveInfinity(Remaining))
                Remaining = Mathf.Max(0f, Remaining - deltaTime);
            return Remaining <= 0f || Owner == null || Owner.Health <= 0;
        }
    }

    private readonly struct PendingEnemyPlacement
    {
        public EnemyRuntime Enemy { get; }
        public DungeonBoardSlot Anchor { get; }
        public IReadOnlyList<DungeonBoardSlot> OccupiedTiles { get; }
        public bool IsExclusive { get; }

        public PendingEnemyPlacement(
            EnemyRuntime enemy,
            DungeonBoardSlot anchor,
            IReadOnlyList<DungeonBoardSlot> occupiedTiles,
            bool isExclusive)
        {
            Enemy = enemy;
            Anchor = anchor;
            OccupiedTiles = occupiedTiles;
            IsExclusive = isExclusive;
        }
    }

    private sealed class CircularEnemyState
    {
        public float ApproachProgress;
        public float AttackTimeRemaining;
        public int SectorIndex;
        public int LayerIndex;
        public Vector2 ResolvedPosition;
        public float CurrentRadius;
        public float TargetRadius;
        public float DefenseLineRadius;
        public int StableOrder;
        public bool WasWithinCoreRange;
        public bool HasContactedCore;
        public Vector2 SpawnDirection { get; set; }

        public CircularEnemyState(
            float attackInterval,
            int sectorIndex,
            int layerIndex,
            Vector2 spawnDirection,
            float spawnRadius,
            float targetRadius,
            float defenseLineRadius,
            int stableOrder)
        {
            AttackTimeRemaining = Mathf.Max(0.1f, attackInterval);
            SectorIndex = Mathf.Max(0, sectorIndex);
            LayerIndex = Mathf.Max(0, layerIndex);
            SpawnDirection = spawnDirection.sqrMagnitude > 0.0001f
                ? spawnDirection.normalized
                : Vector2.up;
            CurrentRadius = Mathf.Max(0f, spawnRadius);
            TargetRadius = Mathf.Max(0f, targetRadius);
            DefenseLineRadius = Mathf.Max(0f, defenseLineRadius);
            ResolvedPosition = SpawnDirection * CurrentRadius;
            StableOrder = Mathf.Max(0, stableOrder);
        }
    }

    private sealed class AllyMovementState
    {
        public Vector2 Position;
        public Vector2 Destination;

        public AllyMovementState(Vector2 position)
        {
            Position = position;
            Destination = position;
        }
    }

    private sealed class WorldActorView
    {
        private const float FootHudLocalHeight = 0.012f;
        private readonly Transform _root;
        private readonly Transform _footHudRoot;
        private readonly Transform _verticalBillboardRoot;
        private readonly Transform _spriteTransform;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly SpriteRenderer _shadowRenderer;
        private readonly DungeonWorldPolylineRenderer _movementLine;
        private readonly SpriteRenderer _movementMarker;
        private readonly DungeonWorldPolylineRenderer _movementMarkerRing;
        private readonly DungeonWorldPolylineRenderer _cooldownTrack;
        private readonly DungeonWorldPolylineRenderer _cooldownFill;
        private readonly SpriteRenderer _abilityReady;
        private float _height;
        private float _spriteScale = 1f;
        private float _groundOffset;
        private float _visualTopLocalY;
        private Sprite _groundAnchoredSprite;
        private float _spriteVisualBottom;
        private int _sortingOrder;
        private int _sortingTieBreaker;
        private bool _sourceFacesRight = true;
        private Color _hitFlashColor = Color.white;
        private float _hitFlashDuration;
        private float _hitFlashRemaining;
        private bool _selected;

        public GameObject GameObject => _root != null
            ? _root.gameObject
            : null;
        public Vector3 WorldPosition => _root != null
            ? _root.localPosition
            : Vector3.zero;
        public Vector3 InteractionWorldPosition =>
            TransformBillboardPoint(_height * 0.5f);

        public WorldActorView(GameObject instance, bool useAllyHud)
        {
            _root = instance != null ? instance.transform : null;
            DungeonWorldActorPrefabView prefabView = instance != null
                ? instance.GetComponent<DungeonWorldActorPrefabView>()
                : null;
            if (prefabView == null || !prefabView.HasRequiredReferences)
            {
                Debug.LogError(
                    "DungeonWorldActor prefab references are incomplete.",
                    instance);
                return;
            }

            _footHudRoot = prefabView.FootHudRoot;
            _verticalBillboardRoot = prefabView.VerticalBillboardRoot;
            _spriteTransform = prefabView.ActorTransform;
            _spriteRenderer = prefabView.ActorRenderer;
            _shadowRenderer = prefabView.ShadowRenderer;
            if (!useAllyHud)
            {
                _footHudRoot.gameObject.SetActive(false);
                prefabView.AbilityReady.gameObject.SetActive(false);
                return;
            }

            _footHudRoot.gameObject.SetActive(true);
            _movementLine = prefabView.MovementLine;
            _movementMarker = prefabView.MovementMarker;
            _movementMarkerRing = prefabView.MovementMarkerRing;
            _cooldownTrack = prefabView.CooldownTrack;
            _cooldownFill = prefabView.CooldownFill;
            _abilityReady = prefabView.AbilityReady;
        }

        public bool Configure(
            Sprite sprite,
            float height,
            int sortingOrder,
            Sprite scaleReference = null,
            float scaleMultiplier = 1f,
            float groundOffset = 0f,
            float headHeightNormalized = 1f,
            bool sourceFacesRight = true)
        {
            if (_root == null || _spriteRenderer == null || sprite == null)
            {
                if (_root != null)
                    _root.gameObject.SetActive(false);
                return false;
            }

            _height = Mathf.Max(0.1f, height) *
                      Mathf.Max(0.1f, scaleMultiplier);
            Sprite reference = scaleReference != null
                ? scaleReference
                : sprite;
            float referenceHeight = Mathf.Max(
                0.0001f,
                reference.bounds.size.y);
            _spriteScale = _height / referenceHeight;
            _groundOffset = Mathf.Clamp(groundOffset, -2f, 2f);
            _visualTopLocalY = _groundOffset +
                               _height * Mathf.Clamp(
                                   headHeightNormalized,
                                   0.4f,
                                   1.4f);
            _sourceFacesRight = sourceFacesRight;
            _sortingTieBreaker = sortingOrder;
            _sortingOrder = sortingOrder;
            SetSprite(sprite);
            RefreshSpriteColor();
            _spriteRenderer.sortingOrder = sortingOrder;
            if (_shadowRenderer != null)
                _shadowRenderer.sortingOrder = sortingOrder - 1;
            SetHudSortingOrders();
            _root.gameObject.SetActive(true);
            return true;
        }

        public void SetSprite(Sprite sprite)
        {
            if (_spriteRenderer == null || sprite == null)
                return;

            if (!ReferenceEquals(_spriteRenderer.sprite, sprite))
                _spriteRenderer.sprite = sprite;
            if (!ReferenceEquals(_groundAnchoredSprite, sprite))
            {
                _groundAnchoredSprite = sprite;
                _spriteVisualBottom = ResolveVisualBottom(sprite);
            }
            _spriteTransform.localScale = Vector3.one * _spriteScale;
            _spriteTransform.localPosition = new Vector3(
                0f,
                _groundOffset - _spriteVisualBottom * _spriteScale,
                0f);
        }

        private static float ResolveVisualBottom(Sprite sprite)
        {
            float visualBottom = sprite.bounds.min.y;
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0)
                return visualBottom;

            float minimum = float.PositiveInfinity;
            for (int index = 0; index < vertices.Length; index++)
                minimum = Mathf.Min(minimum, vertices[index].y);
            return float.IsNaN(minimum) || float.IsInfinity(minimum)
                ? visualBottom
                : minimum;
        }

        public void RefreshAllyHud(
            CharacterRuntime runtime,
            AllyMovementState movement,
            DungeonHudPresentationSO style,
            Camera camera)
        {
            if (runtime == null || movement == null || style == null)
                return;

            SetSprite(runtime.ResolveCurrentBattleSdSprite());
            RefreshCooldownRing(runtime.AttackCooldownProgress, style);
            RefreshMovementIndicator(movement, style);
            RefreshAbilityReady(runtime, style, camera);
        }

        public void ShowHitFlash(Color color, float duration)
        {
            if (_spriteRenderer == null)
                return;

            _hitFlashColor = new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(color.a));
            _hitFlashDuration = Mathf.Max(0.01f, duration);
            _hitFlashRemaining = _hitFlashDuration;
            RefreshSpriteColor();
        }

        public void TickHitFlash(float deltaTime)
        {
            if (_hitFlashRemaining <= 0f)
                return;

            _hitFlashRemaining = Mathf.Max(
                0f,
                _hitFlashRemaining - Mathf.Max(0f, deltaTime));
            RefreshSpriteColor();
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;

            _selected = selected;
            RefreshSpriteColor();
        }

        public void SetWorldPosition(Vector3 position)
        {
            if (_root != null)
                _root.localPosition = position;
        }

        public void FaceCamera(Camera camera)
        {
            if (_spriteRenderer == null || camera == null)
                return;

            Quaternion screenFacing = camera.transform.rotation;
            if (_verticalBillboardRoot != null)
                _verticalBillboardRoot.rotation = screenFacing;
            else
                _spriteRenderer.transform.rotation = screenFacing;
        }

        public void SetFacingDirection(Vector2 direction, Camera camera)
        {
            if (_spriteRenderer == null || camera == null ||
                direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 worldDirection = new(direction.x, 0f, direction.y);
            float screenHorizontal = Vector3.Dot(
                worldDirection.normalized,
                camera.transform.right);
            if (Mathf.Abs(screenHorizontal) <= 0.001f)
                return;

            bool faceScreenRight = screenHorizontal > 0f;
            _spriteRenderer.flipX = _sourceFacesRight
                ? !faceScreenRight
                : faceScreenRight;
        }

        public void RefreshDepthSorting(Camera camera, int sortingRange)
        {
            if (_root == null || camera == null)
                return;

            Vector3 viewport = camera.WorldToViewportPoint(_root.position);
            int depthOrder = Mathf.RoundToInt(
                (1f - Mathf.Clamp01(viewport.y)) *
                Mathf.Max(100, sortingRange));
            _sortingOrder = 1000 + depthOrder + _sortingTieBreaker;
            _spriteRenderer.sortingOrder = _sortingOrder;
            if (_shadowRenderer != null)
                _shadowRenderer.sortingOrder = _sortingOrder - 1;
            SetHudSortingOrders();
        }

        public bool TryGetAnchor(
            Camera camera,
            BattleVfxAnchorType anchorType,
            out BattleVfxAnchorSnapshot snapshot)
        {
            snapshot = default;
            if (_root == null || !_root.gameObject.activeInHierarchy ||
                camera == null)
            {
                return false;
            }

            float vertical = anchorType switch
            {
                BattleVfxAnchorType.Ground => 0.05f,
                BattleVfxAnchorType.Head => Mathf.Max(
                    _height * 0.88f,
                    _visualTopLocalY),
                BattleVfxAnchorType.Muzzle => _height * 0.62f,
                BattleVfxAnchorType.Status => _height * 0.76f,
                _ => _height * 0.48f,
            };
            Vector3 position = anchorType == BattleVfxAnchorType.Ground
                ? _root.position + Vector3.up * vertical
                : TransformBillboardPoint(vertical);
            Vector3 frameRight = camera.transform.right * (_height * 0.42f);
            Vector3 frameUp = camera.transform.up * _height;
            snapshot = BattleVfxAnchorSnapshot.FromWorld(
                position,
                camera.transform.rotation,
                TransformBillboardPoint(_height * 0.5f),
                frameRight,
                frameUp);
            return snapshot.IsValid;
        }

        private Vector3 TransformBillboardPoint(float localHeight)
        {
            if (_verticalBillboardRoot != null)
            {
                return _verticalBillboardRoot.TransformPoint(
                    new Vector3(0f, localHeight, 0f));
            }

            return _root != null
                ? _root.position + Vector3.up * localHeight
                : Vector3.zero;
        }

        private void RefreshSpriteColor()
        {
            if (_spriteRenderer == null)
                return;

            if (_hitFlashRemaining <= 0f)
            {
                _spriteRenderer.color = _selected
                    ? new Color(0.45f, 0.9f, 1f, 1f)
                    : Color.white;
                return;
            }

            float normalizedRemaining = _hitFlashDuration > 0f
                ? Mathf.Clamp01(_hitFlashRemaining / _hitFlashDuration)
                : 0f;
            _spriteRenderer.color = Color.Lerp(
                _hitFlashColor,
                Color.white,
                1f - normalizedRemaining);
        }

        private void RefreshMovementIndicator(
            AllyMovementState movement,
            DungeonHudPresentationSO style)
        {
            if (_movementLine == null)
                return;

            Vector2 delta = movement.Destination - movement.Position;
            bool moving = delta.sqrMagnitude > 0.0004f;
            _movementLine.SetVisible(moving);
            if (_movementMarker != null)
                _movementMarker.gameObject.SetActive(false);
            if (_movementMarkerRing != null)
                _movementMarkerRing.SetVisible(false);
            if (!moving)
                return;

            _movementLine.SetSegment(
                new Vector3(0f, 0.055f, 0f),
                new Vector3(delta.x, 0.055f, delta.y),
                style.MovementLineWidth,
                style.MovementLineColor);

            Sprite markerSprite = style.MovementDestinationSprite;
            if (_movementMarker != null && markerSprite != null)
            {
                _movementMarker.sprite = markerSprite;
                _movementMarker.color = style.MovementDestinationColor;
                _movementMarker.transform.localPosition = new Vector3(
                    delta.x,
                    0.06f,
                    delta.y);
                _movementMarker.transform.localRotation =
                    Quaternion.Euler(90f, 0f, 0f);
                SetSpriteWorldSize(
                    _movementMarker,
                    style.MovementDestinationSize);
                _movementMarker.gameObject.SetActive(true);
            }
            else if (_movementMarkerRing != null)
            {
                _movementMarkerRing.SetRing(
                    style.MovementDestinationSize * 0.5f,
                    1f,
                    style.MovementLineWidth,
                    style.MovementDestinationColor,
                    new Vector3(delta.x, 0.06f, delta.y));
            }
        }

        private void RefreshCooldownRing(
            float progress,
            DungeonHudPresentationSO style)
        {
            if (_cooldownTrack == null || _cooldownFill == null)
                return;

            _cooldownTrack.SetRing(
                style.AttackCooldownRingRadius,
                1f,
                style.AttackCooldownRingWidth,
                style.AttackCooldownTrackColor,
                new Vector3(0f, FootHudLocalHeight, 0f));

            progress = Mathf.Clamp01(progress);
            _cooldownFill.SetVisible(progress > 0.001f);
            if (progress <= 0.001f)
                return;
            _cooldownFill.SetRing(
                style.AttackCooldownRingRadius,
                progress,
                style.AttackCooldownRingWidth,
                style.AttackCooldownReadyColor,
                new Vector3(0f, FootHudLocalHeight + 0.002f, 0f));
        }

        private void RefreshAbilityReady(
            CharacterRuntime runtime,
            DungeonHudPresentationSO style,
            Camera camera)
        {
            if (_abilityReady == null)
                return;

            Sprite sprite = style.AbilityReadySprite;
            bool visible = runtime.IsActiveSkillReady && sprite != null;
            _abilityReady.gameObject.SetActive(visible);
            if (!visible)
                return;

            _abilityReady.sprite = sprite;
            _abilityReady.color = style.AbilityReadyColor;
            _abilityReady.transform.localRotation = Quaternion.identity;
            _abilityReady.transform.localPosition = new Vector3(
                0f,
                _visualTopLocalY +
                style.AbilityReadyIconOffset +
                style.AbilityReadyIconSize * 0.5f,
                -0.01f);
            SetSpriteWorldSize(_abilityReady, style.AbilityReadyIconSize);
        }

        private void SetHudSortingOrders()
        {
            if (_movementLine != null)
                _movementLine.SetSortingOrder(_sortingOrder - 6);
            if (_movementMarker != null)
                _movementMarker.sortingOrder = _sortingOrder - 5;
            if (_movementMarkerRing != null)
                _movementMarkerRing.SetSortingOrder(_sortingOrder - 5);
            if (_cooldownTrack != null)
                _cooldownTrack.SetSortingOrder(_sortingOrder - 4);
            if (_cooldownFill != null)
                _cooldownFill.SetSortingOrder(_sortingOrder - 3);
            if (_abilityReady != null)
                _abilityReady.sortingOrder = _sortingOrder + 4;
        }

        private static void SetSpriteWorldSize(
            SpriteRenderer renderer,
            float targetSize)
        {
            if (renderer == null || renderer.sprite == null)
                return;
            Vector2 size = renderer.sprite.bounds.size;
            float largest = Mathf.Max(0.0001f, size.x, size.y);
            renderer.transform.localScale =
                Vector3.one * (Mathf.Max(0.01f, targetSize) / largest);
        }

    }

    [SerializeField] private RectTransform boardRect;

    [Header("2.5D World Presentation")]
    [SerializeField] private GameObject worldPresentationRoot;
    [SerializeField] private GameObject worldOutput;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera worldForegroundCamera;
    [SerializeField] private Transform worldActorRoot;
    [SerializeField] private Transform worldVfxRoot;
    [SerializeField] private GameObject worldActorPrefab;
    [SerializeField] private GameObject worldAreaPreviewPrefab;
    [SerializeField] private GameObject worldActorPreview;
    [SerializeField] private DungeonWorldInputView worldInputView;
    [SerializeField]
    private PracticeBattleDebugOverlayView practiceDebugOverlay;
    [SerializeField] private SpriteRenderer worldBackdrop;
    [SerializeField] private Transform worldGround;
    [SerializeField] private Transform worldArenaRing;
    [SerializeField]
    private DungeonBattleCoreWorldGaugeView worldBattleCoreGauge;
    [SerializeField, Min(0.1f)] private float worldSpawnRadius = 4.25f;
    [SerializeField, Min(0f), Tooltip(
        "Extra world-space distance beyond the camera's longest visible " +
        "horizontal ground edge used by the circular enemy spawn line.")]
    private float worldSpawnLinePadding = 0.35f;
    [SerializeField, Min(0.1f)] private float worldAllyHeight = 1.7f;
    [SerializeField, Min(0.1f)] private float worldEnemyHeight = 1.85f;

    [Header("World Ally Movement")]
    [SerializeField, Min(0.1f)] private float worldAllyMoveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float worldAllyBoundaryPadding = 0.35f;
    [SerializeField, Min(0f)] private float worldAllyMinimumSpacing = 0.55f;
    [SerializeField, Min(1f)] private float worldActorHitRadiusPixels = 46f;

    [Header("Enemy Hit Feedback")]
    [SerializeField]
    private Color enemyHitFlashColor = new(1f, 0.16f, 0.16f, 1f);
    [SerializeField, Min(0.01f)]
    private float enemyHitFlashDuration = 0.16f;

    [Header("3D VFX")]
    [SerializeField] private BattleVfxPlayer vfxPlayer;
    [SerializeField]
    private BattleVfxQualityProfileSO vfxQualityProfile;

    private readonly List<DungeonBoardSlot> _tiles = new();
    private readonly Dictionary<EnemyRuntime, EnemyPlacement>
        _enemyPlacements = new();
    private readonly Dictionary<DungeonBoardSlot, EnemyRuntime>
        _exclusiveOccupants = new();
    private readonly Dictionary<EnemyRuntime, CircularEnemyState>
        _circularEnemyStates = new();
    private readonly List<EnemyRuntime> _circularEnemySnapshot = new();
    private readonly BattleCoreRuntime _battleCore = new();
    private readonly List<IBattleCharacter> _battleCharacters = new();
    private readonly Dictionary<EnemyRuntime, WorldActorView>
        _worldEnemyActors = new();
    private readonly Dictionary<IBattleCharacter, WorldActorView>
        _worldAllyActors = new();
    private readonly Dictionary<IBattleCharacter, AllyMovementState>
        _worldAllyMovement = new();
    private readonly Dictionary<EnemyRuntime, float>
        _recentCoreAttackTimes = new();
    private readonly List<TimedScalarModifier>
        _timedResourceRecoveryModifiers = new();
    private readonly List<EnemyDamageLinkGroup> _enemyDamageLinks = new();
    private readonly HashSet<(
        EnemyRuntime Source,
        EnemyAbilityDefinition Ability,
        EnemyRuntime Target)> _enemyRadiusContacts = new();
    private readonly HashSet<(
        EnemyRuntime Source,
        EnemyAbilityDefinition Ability,
        EnemyRuntime Target)> _currentEnemyRadiusContacts = new();
    private readonly HashSet<(
        EnemyRuntime Source,
        EnemyAbilityDefinition Ability)> _initializedEnemyRadiusAbilities =
        new();
    private bool _resolvingDamageLink;
    private readonly HashSet<EnemySO> _missingWorldEnemySpriteWarnings = new();
    private readonly HashSet<EnemyRuntime> _worldEnemySync = new();
    private readonly List<float> _practiceDebugRangeRadii = new();
    private Func<EnemyRuntime, bool> _itemTargetHandler;
    private IBattleCardDrawService _cardDrawService;
    private IBattleCardControlService _cardControlService;
    private IBattleEnemySummonService _enemySummonService;
    private IBattleAbilityUserModifierService _abilityUserModifierService;
    private BattleManualTargetSelectionRequest _manualTargetRequest;
    private readonly List<EnemyRuntime> _manualEnemyTargets = new();
    private readonly List<IBattleCharacter> _manualAllyTargets = new();
    private BattleAreaPreviewView _areaPreview;
    private RenderTexture _runtimeWorldRenderTexture;
    private IBattleCharacter _selectedWorldAlly;
    private IBattleCharacter _lastPracticeDebugAlly;
    private EnemyRuntime _lastPracticeDebugEnemy;
    private bool _practiceDebugVisualizationEnabled;
    private IBattleCharacter _pressedWorldAlly;
    private bool _draggingWorldAlly;
    private Vector2 _manualAreaOrigin;
    private Vector2 _manualAreaDirection = Vector2.up;
    private bool _manualAreaAnchorSet;
    private bool _manualAreaPointerDown;
    private EnemyRuntime _forcedPriorityTarget;
    private float _forcedPriorityRemaining;
    private float _worldSpawnLineRadius;
    private float _spatialBattleElapsedTime;
    private int _maximumStackSize = 8;
    private bool _initialized;
    private readonly Queue<BattleStatusAppliedEvent> _statusEventQueue = new();
    private bool _dispatchingStatusEvents;
    private readonly Queue<BattleEnemyDefeatedEvent> _defeatEventQueue = new();
    private bool _dispatchingDefeatEvents;
    private readonly Queue<EnemyCombatEvent> _enemyCombatEventQueue = new();
    private bool _dispatchingEnemyCombatEvents;
    private EnemyCombatEvent _currentEnemyCombatEvent;
    private readonly Queue<BattleEffectResolvedEvent>
        _effectResolvedEventQueue = new();
    private readonly Queue<StatusEffectLifecycleEvent>
        _statusLifecycleEventQueue = new();
    private readonly Queue<BattleUnitLifecycleEvent>
        _unitLifecycleEventQueue = new();
    private bool _dispatchingEffectResolvedEvents;
    private bool _dispatchingStatusLifecycleEvents;
    private bool _dispatchingUnitLifecycleEvents;
    private readonly HashSet<EnemyRuntime> _boundPresentationEnemies = new();
    private readonly HashSet<CharacterRuntime>
        _boundPresentationCharacters = new();
    private readonly Dictionary<EnemyRuntime, BattleVfxTargetHandle>
        _enemyVfxHandles = new();
    private readonly Dictionary<IBattleCharacter, BattleVfxTargetHandle>
        _allyVfxHandles = new();
    private readonly Dictionary<
        EnemyRuntime,
        Dictionary<BattleVfxAnchorType, BattleVfxAnchorSnapshot>>
        _lastEnemyVfxAnchors = new();
    private int _nextVfxTargetHandle = 1;
    private BattlePresentationDispatcher _presentationDispatcher;
    private BattleArenaSetup _arenaSetup = BattleArenaSetup.Legacy;
    private BattleEnvironmentSetup _environmentSetup =
        BattleEnvironmentSetup.Default;
    private int _activeTileCount;
    private int _nextFormationStableOrder;
    private float _formationSpawnRadius;
    private float _formationDefenseLineRadius;
    private bool _formationRadiiInitialized;

    private bool HasWorldPresentation =>
        worldPresentationRoot != null &&
        worldOutput != null &&
        worldCamera != null &&
        worldForegroundCamera != null &&
        worldActorRoot != null &&
        worldActorPrefab != null &&
        worldAreaPreviewPrefab != null &&
        worldGround != null &&
        worldArenaRing != null &&
        worldBattleCoreGauge != null &&
        worldInputView != null;

    private bool UsesWorldPresentation =>
        _arenaSetup.UsesBattleCore && HasWorldPresentation;

    public bool UsesFullscreenWorldPresentation => UsesWorldPresentation;
    public bool SupportsWorldPresentation => HasWorldPresentation;
    public bool PracticeDebugVisualizationEnabled =>
        _practiceDebugVisualizationEnabled;

    public int GridSize { get; private set; } = MinimumGridSize;
    public float DungeonStageProgress { get; private set; }
    public RectTransform HighlightRect => boardRect != null
        ? boardRect
        : transform as RectTransform;
    public int InitialEnemyCapacity => _arenaSetup.UsesBattleCore
        ? Mathf.Max(1, _arenaSetup.LaneCount)
        : _activeTileCount > 0
            ? _activeTileCount
            : GridSize * GridSize;
    public int TotalEnemyCapacity => _arenaSetup.UsesBattleCore
        ? Mathf.Max(1, _arenaSetup.MaximumEnemyCapacity)
        : InitialEnemyCapacity;
    public IBattleObjective Objective => _battleCore;
    public IBattleSpatialService SpatialService => this;
    public bool IsAvailable => _arenaSetup.UsesBattleCore;
    public float ArenaRadius => GetAllowedAllyRadius();
    public float InnerZoneBoundaryRadius =>
        ArenaRadius * BattleSpatialDefaults.InnerZoneRadiusRatio;
    public IBattleCardDrawService CardDrawService => _cardDrawService;
    public IBattleCardControlService CardControlService =>
        _cardControlService;
    public IBattleEnemySummonService EnemySummonService =>
        _enemySummonService;
    public IBattleAbilityUserModifierService AbilityUserModifierService =>
        _abilityUserModifierService;
    public IEnemyCombatRuntimeService EnemyCombatRuntimeService => this;
    public int LivingEnemyCount
    {
        get
        {
            int count = 0;
            for (int index = 0;
                 index < _tiles.Count && index < TotalEnemyCapacity;
                 index++)
            {
                DungeonBoardSlot tile = _tiles[index];
                if (tile != null)
                    count = BattleValueMath.SaturatingAddNonNegative(
                        count,
                        tile.StackCount);
            }

            return count;
        }
    }
    public bool HasEmptyEnemyTile
    {
        get
        {
            for (int index = 0;
                 index < _tiles.Count && index < TotalEnemyCapacity;
                 index++)
            {
                DungeonBoardSlot tile = _tiles[index];
                if (tile != null &&
                    tile.StackCount == 0 &&
                    !_exclusiveOccupants.ContainsKey(tile))
                    return true;
            }

            return false;
        }
    }
    public BattleVfxPlayer VfxPlayer => vfxPlayer;
    public bool IsManualTargetSelectionPending =>
        _manualTargetRequest != null;
    public BattleManualTargetSelectionRequest CurrentManualTargetRequest =>
        _manualTargetRequest;
    public int CurrentManualSelectedCount =>
        _manualTargetRequest?.Faction == CharacterTargetFaction.Ally
            ? _manualAllyTargets.Count
            : _manualEnemyTargets.Count;
    public event Action<BattleEnemyDefeatedEvent> EnemyDefeated;
    public event Action OccupancyChanged;
    public event Action<EnemyRuntime> EnemyClicked;
    public event Action<BattleStatusAppliedEvent> StatusApplied;
    public event Action<BattleEffectResolvedEvent> EffectResolved;
    public event Action<StatusEffectLifecycleEvent> StatusLifecycle;
    public event Action<BattleUnitLifecycleEvent> UnitLifecycle;
    public event Action<EnemyCombatEvent> EnemyCombatEventRaised;
    public event Action<bool> ManualTargetSelectionPendingChanged;
    public event Action ManualTargetSelectionProgressChanged;

    public void PublishEffectResolved(BattleEffectResolvedEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _effectResolvedEventQueue.Enqueue(eventData);
        if (_dispatchingEffectResolvedEvents)
            return;

        _dispatchingEffectResolvedEvents = true;
        try
        {
            int processedCount = 0;
            while (_effectResolvedEventQueue.Count > 0 &&
                   processedCount < MaximumPresentationEventsPerDispatch)
            {
                InvokeSafely(
                    EffectResolved,
                    _effectResolvedEventQueue.Dequeue());
                processedCount++;
            }

            DiscardExcessPresentationEvents(
                _effectResolvedEventQueue,
                "effect result");
        }
        finally
        {
            _dispatchingEffectResolvedEvents = false;
        }
    }

    public void PublishStatusLifecycle(StatusEffectLifecycleEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _statusLifecycleEventQueue.Enqueue(eventData);
        if (_dispatchingStatusLifecycleEvents)
            return;

        _dispatchingStatusLifecycleEvents = true;
        try
        {
            int processedCount = 0;
            while (_statusLifecycleEventQueue.Count > 0 &&
                   processedCount < MaximumPresentationEventsPerDispatch)
            {
                InvokeSafely(
                    StatusLifecycle,
                    _statusLifecycleEventQueue.Dequeue());
                processedCount++;
            }

            DiscardExcessPresentationEvents(
                _statusLifecycleEventQueue,
                "status lifecycle");
        }
        finally
        {
            _dispatchingStatusLifecycleEvents = false;
        }
    }

    public void PublishUnitLifecycle(BattleUnitLifecycleEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _unitLifecycleEventQueue.Enqueue(eventData);
        if (_dispatchingUnitLifecycleEvents)
            return;

        _dispatchingUnitLifecycleEvents = true;
        try
        {
            int processedCount = 0;
            while (_unitLifecycleEventQueue.Count > 0 &&
                   processedCount < MaximumPresentationEventsPerDispatch)
            {
                InvokeSafely(
                    UnitLifecycle,
                    _unitLifecycleEventQueue.Dequeue());
                processedCount++;
            }

            DiscardExcessPresentationEvents(
                _unitLifecycleEventQueue,
                "unit lifecycle");
        }
        finally
        {
            _dispatchingUnitLifecycleEvents = false;
        }
    }

    private void InvokeSafely<T>(Action<T> handlers, T eventData)
    {
        if (handlers == null)
            return;

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler).Invoke(eventData);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void DiscardExcessPresentationEvents<T>(
        Queue<T> queue,
        string eventName)
    {
        if (queue.Count == 0)
            return;

        int discardedCount = queue.Count;
        queue.Clear();
        Debug.LogError(
            $"Battle presentation {eventName} dispatch exceeded " +
            $"{MaximumPresentationEventsPerDispatch} events. " +
            $"Discarded {discardedCount} queued events.",
            this);
    }

    public void NotifyStatusApplied(BattleStatusAppliedEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _statusEventQueue.Enqueue(eventData);
        if (_dispatchingStatusEvents)
            return;

        _dispatchingStatusEvents = true;
        try
        {
            int processedCount = 0;
            while (_statusEventQueue.Count > 0 &&
                   processedCount < MaximumStatusEventsPerDispatch)
            {
                BattleStatusAppliedEvent queuedEvent =
                    _statusEventQueue.Dequeue();
                StatusApplied?.Invoke(queuedEvent);
                processedCount++;
            }

            if (_statusEventQueue.Count > 0)
            {
                Debug.LogError(
                    "Status event dispatch limit exceeded. " +
                    "Remaining chained status events were discarded.",
                    this);
                _statusEventQueue.Clear();
            }
        }
        finally
        {
            _dispatchingStatusEvents = false;
        }
    }

    private void NotifyEnemyDefeated(BattleEnemyDefeatedEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _defeatEventQueue.Enqueue(eventData);
        if (_dispatchingDefeatEvents)
            return;

        _dispatchingDefeatEvents = true;
        try
        {
            int processedCount = 0;
            while (_defeatEventQueue.Count > 0 &&
                   processedCount < MaximumDefeatEventsPerDispatch)
            {
                BattleEnemyDefeatedEvent queuedEvent =
                    _defeatEventQueue.Dequeue();
                EnemyDefeated?.Invoke(queuedEvent);
                processedCount++;
            }

            if (_defeatEventQueue.Count > 0)
            {
                Debug.LogError(
                    "Enemy defeat event dispatch limit exceeded. " +
                    "Remaining chained defeat events were discarded.",
                    this);
                _defeatEventQueue.Clear();
            }
        }
        finally
        {
            _dispatchingDefeatEvents = false;
        }
    }

    public float ResolvePassiveModifier(
        EnemyRuntime target,
        EnemyCombatModifierType modifierType,
        float baseValue)
    {
        if (target == null || target.Health <= 0)
            return baseValue;

        float result = baseValue;
        foreach (EnemyRuntime source in _enemyPlacements.Keys)
        {
            if (source == null || source.Health <= 0 ||
                source.AreAllActionsDisabled ||
                !TryFindEnemyTile(source, out DungeonBoardSlot sourceTile))
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state.Definition;
                if (!CanActivateEnemyAbility(source, state) ||
                    !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.AlwaysWhileActive) ||
                    !PassiveAbilityTargetsEnemy(
                        sourceTile,
                        source,
                        ability,
                        target,
                        out IReadOnlyList<EnemyRuntime> enemyTargets))
                {
                    continue;
                }

                foreach (EnemyAbilityOperationDefinition operation in
                         ability.Operations)
                {
                    if (operation == null || !operation.Enabled ||
                        !MatchesPassiveModifierType(
                            operation.Type,
                            modifierType))
                    {
                        continue;
                    }

                    result = EvaluateOperationModifier(result, operation);
                }
            }
        }

        return NormalizeEnemyCombatValue(result, baseValue);
    }

    public EnemyStatusApplicationPolicy ResolveStatusApplication(
        EnemyRuntime target,
        StatusEffectSO statusEffect,
        float duration)
    {
        if (target == null || statusEffect == null)
            return new EnemyStatusApplicationPolicy(false, 0f);

        bool permanent = float.IsPositiveInfinity(duration);
        float resolvedDuration = duration;
        foreach (EnemyRuntime source in _enemyPlacements.Keys)
        {
            if (source == null || source.Health <= 0 ||
                source.AreAllActionsDisabled ||
                !TryFindEnemyTile(source, out DungeonBoardSlot sourceTile))
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state.Definition;
                if (!CanActivateEnemyAbility(source, state) ||
                    !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.AlwaysWhileActive) ||
                    !PassiveAbilityTargetsEnemy(
                        sourceTile,
                        source,
                        ability,
                        target,
                        out _))
                {
                    continue;
                }

                EnemyStatusModifierScope scope =
                    ResolveStatusModifierScope(ability);
                if (!MatchesStatusModifierScope(scope, statusEffect))
                    continue;

                foreach (EnemyAbilityOperationDefinition operation in
                         ability.Operations)
                {
                    if (operation == null || !operation.Enabled)
                        continue;
                    if (operation.Type ==
                        EnemyAbilityOperationType.GrantStatusImmunity)
                    {
                        return EnemyStatusApplicationPolicy.Immune(duration);
                    }
                    if (!permanent && operation.Type ==
                        EnemyAbilityOperationType.ModifyStatusDuration)
                    {
                        resolvedDuration = EvaluateOperationModifier(
                            resolvedDuration,
                            operation);
                    }
                }
            }
        }

        return resolvedDuration > 0f || permanent
            ? EnemyStatusApplicationPolicy.Allowed(resolvedDuration)
            : new EnemyStatusApplicationPolicy(false, 0f);
    }

    public float ResolvePlayerActionPeriodMultiplier(
        IBattleCharacter target)
    {
        if (target == null || target.CurrentHealth <= 0)
            return 1f;

        float result = 1f;
        foreach (EnemyRuntime source in _enemyPlacements.Keys)
        {
            if (source == null || source.Health <= 0 ||
                source.AreAllActionsDisabled ||
                !TryFindEnemyTile(source, out DungeonBoardSlot sourceTile))
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state.Definition;
                if (!CanActivateEnemyAbility(source, state) ||
                    !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.AlwaysWhileActive) ||
                    !TryResolveEnemyAbilityTargets(
                        sourceTile,
                        source,
                        ability.Target,
                        _battleCharacters,
                        out CharacterTargetFaction faction,
                        out IReadOnlyList<EnemyRuntime> enemyTargets,
                        out IReadOnlyList<IBattleCharacter> playerTargets) ||
                    faction != CharacterTargetFaction.Ally ||
                    !ContainsCharacterReference(playerTargets, target) ||
                    !MatchesEnemyAbilityConditions(
                        ability,
                        source,
                        enemyTargets,
                        playerTargets))
                {
                    continue;
                }

                foreach (EnemyAbilityOperationDefinition operation in
                         ability.Operations)
                {
                    if (operation != null && operation.Enabled &&
                        operation.Type ==
                            EnemyAbilityOperationType
                                .ModifyPlayerActionInterval)
                    {
                        result = EvaluateOperationModifier(
                            result,
                            operation);
                    }
                }
            }
        }

        return Mathf.Max(TimePrecision.Step, result);
    }

    public float ResolveResourceRecoveryMultiplier()
    {
        float result = 1f;
        foreach (EnemyRuntime source in _enemyPlacements.Keys)
        {
            if (source == null || source.Health <= 0 ||
                source.AreAllActionsDisabled ||
                !TryFindEnemyTile(source, out DungeonBoardSlot sourceTile))
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state.Definition;
                if (!CanActivateEnemyAbility(source, state) ||
                    !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.AlwaysWhileActive))
                {
                    continue;
                }

                IReadOnlyList<EnemyRuntime> enemyTargets =
                    Array.Empty<EnemyRuntime>();
                IReadOnlyList<IBattleCharacter> playerTargets =
                    Array.Empty<IBattleCharacter>();
                if (ability.Target?.HasTarget == true &&
                    (!TryResolveEnemyAbilityTargets(
                         sourceTile,
                         source,
                         ability.Target,
                         _battleCharacters,
                         out CharacterTargetFaction faction,
                         out enemyTargets,
                         out playerTargets) ||
                     faction != CharacterTargetFaction.Ally ||
                     playerTargets.Count == 0))
                {
                    continue;
                }
                if (!MatchesEnemyAbilityConditions(
                        ability,
                        source,
                        enemyTargets,
                        playerTargets))
                {
                    continue;
                }

                foreach (EnemyAbilityOperationDefinition operation in
                         ability.Operations)
                {
                    if (operation != null && operation.Enabled &&
                        operation.Type ==
                            EnemyAbilityOperationType
                                .ModifyResourceRecovery)
                    {
                        result = EvaluateOperationModifier(
                            result,
                            operation);
                    }
                }
            }
        }

        foreach (TimedScalarModifier modifier in
                 _timedResourceRecoveryModifiers)
        {
            if (modifier != null && modifier.Remaining > 0f)
                result *= modifier.Multiplier;
        }

        return Mathf.Max(0f, result);
    }

    private bool TryAddTimedResourceRecoveryModifier(
        string sourceId,
        float multiplier,
        float duration)
    {
        sourceId = (sourceId ?? string.Empty).Trim();
        duration = TimePrecision.Normalize(duration);
        if (string.IsNullOrEmpty(sourceId) || float.IsNaN(multiplier) ||
            float.IsInfinity(multiplier) || multiplier < 0f ||
            float.IsNaN(duration) || float.IsInfinity(duration) ||
            duration < 0f)
        {
            return false;
        }

        foreach (TimedScalarModifier modifier in
                 _timedResourceRecoveryModifiers)
        {
            if (modifier != null && string.Equals(
                    modifier.SourceId,
                    sourceId,
                    StringComparison.Ordinal))
            {
                return modifier.Refresh(multiplier, duration);
            }
        }

        _timedResourceRecoveryModifiers.Add(
            new TimedScalarModifier(sourceId, multiplier, duration));
        return true;
    }

    private void TickTimedEnemyGlobalModifiers(float deltaTime)
    {
        for (int index = _timedResourceRecoveryModifiers.Count - 1;
             index >= 0;
             index--)
        {
            TimedScalarModifier modifier =
                _timedResourceRecoveryModifiers[index];
            if (modifier == null || modifier.Tick(deltaTime))
                _timedResourceRecoveryModifiers.RemoveAt(index);
        }


        for (int index = _enemyDamageLinks.Count - 1;
             index >= 0;
             index--)
        {
            EnemyDamageLinkGroup group = _enemyDamageLinks[index];
            if (group == null || group.Tick(deltaTime))
            {
                _enemyDamageLinks.RemoveAt(index);
                continue;
            }

            for (int memberIndex = group.Members.Count - 1;
                 memberIndex >= 0;
                 memberIndex--)
            {
                EnemyRuntime member = group.Members[memberIndex];
                if (member == null || member.Health <= 0)
                    group.Members.RemoveAt(memberIndex);
            }
            if (group.Members.Count < 2)
                _enemyDamageLinks.RemoveAt(index);
        }
    }

    public void PublishEnemyCombatEvent(EnemyCombatEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        _enemyCombatEventQueue.Enqueue(eventData);
        if (_dispatchingEnemyCombatEvents)
            return;

        _dispatchingEnemyCombatEvents = true;
        try
        {
            int processedCount = 0;
            while (_enemyCombatEventQueue.Count > 0 &&
                   processedCount < MaximumStatusEventsPerDispatch)
            {
                EnemyCombatEvent queuedEvent =
                    _enemyCombatEventQueue.Dequeue();
                InvokeSafely(EnemyCombatEventRaised, queuedEvent);
                ProcessEnemyCombatEvent(queuedEvent);
                processedCount++;
            }

            if (_enemyCombatEventQueue.Count > 0)
            {
                int discarded = _enemyCombatEventQueue.Count;
                _enemyCombatEventQueue.Clear();
                Debug.LogError(
                    "Enemy combat event dispatch limit exceeded. " +
                    $"Discarded {discarded} chained events.",
                    this);
            }
        }
        finally
        {
            _dispatchingEnemyCombatEvents = false;
        }
    }

    private void ProcessEnemyCombatEvent(EnemyCombatEvent eventData)
    {
        EnemyAbilityTrigger? trigger = eventData.Type switch
        {
            EnemyCombatEventType.FirstCoreContact =>
                EnemyAbilityTrigger.OnFirstCoreContact,
            EnemyCombatEventType.CoreContact =>
                EnemyAbilityTrigger.OnCoreContact,
            EnemyCombatEventType.CoreAttackPreparing =>
                EnemyAbilityTrigger.BeforeCoreAttack,
            EnemyCombatEventType.CoreDamageApplied =>
                EnemyAbilityTrigger.OnCoreHit,
            EnemyCombatEventType.DamageTaken =>
                EnemyAbilityTrigger.OnDamageTaken,
            EnemyCombatEventType.ChargeStarted =>
                EnemyAbilityTrigger.OnChargeStarted,
            EnemyCombatEventType.ChargeInterrupted =>
                EnemyAbilityTrigger.OnChargeInterrupted,
            EnemyCombatEventType.StatusApplied =>
                EnemyAbilityTrigger.OnStatusApplied,
            EnemyCombatEventType.PhaseChanged =>
                EnemyAbilityTrigger.OnPhaseChanged,
            _ => null,
        };
        if (!trigger.HasValue || eventData.Source == null ||
            !TryFindEnemyTile(
                eventData.Source,
                out DungeonBoardSlot sourceTile))
        {
            return;
        }

        EnemyCombatEvent previousEvent = _currentEnemyCombatEvent;
        _currentEnemyCombatEvent = eventData;
        try
        {
            ExecuteTriggeredAbilities(
                sourceTile,
                eventData.Source,
                trigger.Value,
                _battleCharacters);
        }
        finally
        {
            _currentEnemyCombatEvent = previousEvent;
        }
    }

    private bool PassiveAbilityTargetsEnemy(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        EnemyAbilityDefinition ability,
        EnemyRuntime target,
        out IReadOnlyList<EnemyRuntime> enemyTargets)
    {
        enemyTargets = Array.Empty<EnemyRuntime>();
        EnemyAbilityTargetDefinition targetDefinition = ability?.Target;
        if (targetDefinition == null || !targetDefinition.HasTarget)
        {
            enemyTargets = new[] { source };
        }
        else if (!TryResolveEnemyAbilityTargets(
                     sourceTile,
                     source,
                     targetDefinition,
                     _battleCharacters,
                     out CharacterTargetFaction faction,
                     out enemyTargets,
                     out IReadOnlyList<IBattleCharacter> playerTargets) ||
                 faction != CharacterTargetFaction.Enemy)
        {
            return false;
        }

        if (!ContainsEnemyReference(enemyTargets, target) ||
            !MatchesRequiredEnemyRoleTag(ability, target) ||
            !MatchesPassiveTargetMetadata(ability, target) ||
            !MatchesEnemyAbilityConditions(
                ability,
                source,
                enemyTargets,
                Array.Empty<IBattleCharacter>()))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesPassiveTargetMetadata(
        EnemyAbilityDefinition ability,
        EnemyRuntime target)
    {
        if (ability == null || target == null)
            return false;

        if (TryGetAbilityBooleanParameter(
                ability,
                "summonedOnly",
                out bool summonedOnly) && summonedOnly &&
            !target.IsSummoned)
        {
            return false;
        }

        if (TryGetAbilityTextParameter(
                ability,
                "requiredEnemyTier",
                out string requiredTier) &&
            (!Enum.TryParse(
                 requiredTier,
                 true,
                 out EnemyRosterTier rosterTier) ||
             target.Definition.RosterTier != rosterTier))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesRequiredEnemyRoleTag(
        EnemyAbilityDefinition ability,
        EnemyRuntime target)
    {
        if (!TryGetAbilityTextParameter(
                ability,
                "requiredRoleTag",
                out string requiredRoleTag))
        {
            return true;
        }
        if (target?.Definition?.RoleTags == null)
            return false;
        foreach (string roleTag in target.Definition.RoleTags)
        {
            if (string.Equals(
                    roleTag?.Trim(),
                    requiredRoleTag.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsEnemyReference(
        IReadOnlyList<EnemyRuntime> enemies,
        EnemyRuntime target)
    {
        if (enemies == null || target == null)
            return false;
        foreach (EnemyRuntime enemy in enemies)
        {
            if (ReferenceEquals(enemy, target))
                return true;
        }
        return false;
    }

    private static bool ContainsCharacterReference(
        IReadOnlyList<IBattleCharacter> characters,
        IBattleCharacter target)
    {
        if (characters == null || target == null)
            return false;
        foreach (IBattleCharacter character in characters)
        {
            if (ReferenceEquals(character, target))
                return true;
        }
        return false;
    }

    private static bool MatchesPassiveModifierType(
        EnemyAbilityOperationType operationType,
        EnemyCombatModifierType modifierType)
    {
        return (operationType ==
                    EnemyAbilityOperationType.ModifyCoreAttackDamage &&
                modifierType == EnemyCombatModifierType.CoreAttackDamage) ||
               (operationType ==
                    EnemyAbilityOperationType.ModifyCoreAttackInterval &&
                modifierType == EnemyCombatModifierType.CoreAttackInterval);
    }

    private static float EvaluateOperationModifier(
        float baseValue,
        EnemyAbilityOperationDefinition operation)
    {
        float result = (baseValue + operation.Amount) *
                       Mathf.Max(0f, 1f + operation.Percentage) *
                       operation.Multiplier;
        return NormalizeEnemyCombatValue(result, baseValue);
    }

    private static float NormalizeEnemyCombatValue(
        float value,
        float fallback)
    {
        if (float.IsNaN(value))
            return Mathf.Max(0f, fallback);
        if (float.IsPositiveInfinity(value))
            return float.MaxValue;
        return Mathf.Max(0f, value);
    }

    private static bool MatchesStatusModifierScope(
        EnemyStatusModifierScope scope,
        StatusEffectSO statusEffect)
    {
        return scope switch
        {
            EnemyStatusModifierScope.All => true,
            EnemyStatusModifierScope.Debuffs =>
                statusEffect.Alignment == StatusEffectAlignment.Debuff,
            EnemyStatusModifierScope.Controls =>
                EnemyStatusRules.HasControlEffect(statusEffect),
            _ => false,
        };
    }

    public void BindItemTargetHandler(
        Func<EnemyRuntime, bool> itemTargetHandler)
    {
        _itemTargetHandler = itemTargetHandler;
    }

    public void BindCardDrawService(
        IBattleCardDrawService cardDrawService)
    {
        _cardDrawService = cardDrawService;
    }

    public void BindCardControlService(
        IBattleCardControlService cardControlService)
    {
        _cardControlService = cardControlService;
    }

    public void BindEnemySummonService(
        IBattleEnemySummonService enemySummonService)
    {
        _enemySummonService = enemySummonService;
    }

    public void BindAbilityUserModifierService(
        IBattleAbilityUserModifierService modifierService)
    {
        _abilityUserModifierService = modifierService;
    }

    public bool TryBeginManualTargetSelection(
        BattleManualTargetSelectionRequest request)
    {
        if (request == null ||
            (!request.UsesWorldArea && request.RequiredCount <= 0) ||
            (request.UsesWorldArea && !UsesWorldPresentation) ||
            IsManualTargetSelectionPending)
        {
            return false;
        }

        _manualTargetRequest = request;
        _manualEnemyTargets.Clear();
        _manualAllyTargets.Clear();
        if (request.UsesWorldArea)
            InitializeManualAreaAim(request);
        RefreshManualTargetHighlights();
        ManualTargetSelectionPendingChanged?.Invoke(true);
        ManualTargetSelectionProgressChanged?.Invoke();
        return true;
    }

    public void CancelManualTargetSelection()
    {
        if (_manualTargetRequest == null)
            return;

        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        ClearManualTargetSelection();
        request.Complete(new BattleManualTargetSelectionResult(
            request.Faction,
            null,
            null,
            true));
    }

    private void CompleteManualTargetSelection()
    {
        if (_manualTargetRequest == null)
            return;

        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        BattleManualTargetSelectionResult result = new(
            request.Faction,
            _manualEnemyTargets.ToArray(),
            _manualAllyTargets.ToArray(),
            false,
            _manualAreaOrigin,
            _manualAreaDirection,
            request.UsesWorldArea && _manualAreaAnchorSet);
        ClearManualTargetSelection();
        request.Complete(result);
    }

    private void ClearManualTargetSelection()
    {
        if (_manualTargetRequest == null)
            return;

        _manualTargetRequest = null;
        _manualEnemyTargets.Clear();
        _manualAllyTargets.Clear();
        _manualAreaAnchorSet = false;
        _manualAreaPointerDown = false;
        _areaPreview?.Hide();
        RefreshManualTargetHighlights();
        ManualTargetSelectionProgressChanged?.Invoke();
        ManualTargetSelectionPendingChanged?.Invoke(false);
    }

    private bool HandleManualEnemyClicked(EnemyRuntime enemy)
    {
        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        if (request == null ||
            request.Faction != CharacterTargetFaction.Enemy ||
            enemy == null ||
            !ContainsReference(request.EnemyCandidates, enemy))
        {
            return false;
        }

        if (!_manualEnemyTargets.Remove(enemy))
            _manualEnemyTargets.Add(enemy);
        RefreshManualTargetHighlights();
        ManualTargetSelectionProgressChanged?.Invoke();
        if (_manualEnemyTargets.Count >= request.RequiredCount)
            CompleteManualTargetSelection();
        return true;
    }

    private bool HandleManualAllyClicked(CharacterRuntime character)
    {
        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        if (request == null ||
            request.Faction != CharacterTargetFaction.Ally ||
            character == null ||
            !ContainsReference(request.AllyCandidates, character))
        {
            return false;
        }

        if (!_manualAllyTargets.Remove(character))
            _manualAllyTargets.Add(character);
        RefreshManualTargetHighlights();
        ManualTargetSelectionProgressChanged?.Invoke();
        if (_manualAllyTargets.Count >= request.RequiredCount)
            CompleteManualTargetSelection();
        return true;
    }

    private void RefreshManualTargetHighlights()
    {
        BattleManualTargetSelectionRequest request =
            _manualTargetRequest;
        foreach (DungeonBoardSlot tile in _tiles)
        {
            EnemyRuntime enemy = tile?.TopEnemy;
            bool candidate = request != null &&
                             !request.UsesWorldArea &&
                             request.Faction ==
                             CharacterTargetFaction.Enemy &&
                             ContainsReference(
                                 request.EnemyCandidates,
                                 enemy);
            tile?.SetManualSelectionState(
                candidate,
                candidate && _manualEnemyTargets.Contains(enemy));
        }

        foreach (CharacterRuntime character in
                 _boundPresentationCharacters)
        {
            bool candidate = request != null &&
                             request.Faction ==
                             CharacterTargetFaction.Ally &&
                             ContainsReference(
                                 request.AllyCandidates,
                                 character);
            character?.SetManualTargetSelectionState(
                candidate,
                candidate && _manualAllyTargets.Contains(character));
        }

        foreach (KeyValuePair<EnemyRuntime, WorldActorView> entry in
                 _worldEnemyActors)
        {
            entry.Value?.SetSelected(
                request != null &&
                _manualEnemyTargets.Contains(entry.Key));
        }

        foreach (KeyValuePair<IBattleCharacter, WorldActorView> entry in
                 _worldAllyActors)
        {
            entry.Value?.SetSelected(
                ReferenceEquals(entry.Key, _selectedWorldAlly) ||
                (request != null &&
                 _manualAllyTargets.Contains(entry.Key)));
        }
    }

    private static bool ContainsReference<T>(
        IReadOnlyList<T> values,
        T target)
        where T : class
    {
        if (values == null || target == null)
            return false;

        foreach (T value in values)
        {
            if (ReferenceEquals(value, target))
                return true;
        }

        return false;
    }

    public void SetBattleCharacters(
        IReadOnlyList<IBattleCharacter> characters)
    {
        HashSet<IBattleCharacter> previousCharacters =
            new(_battleCharacters);
        UnbindAllPresentationCharacters();
        ClearWorldAllyViews();
        _battleCharacters.Clear();
        if (characters == null)
        {
            _worldAllyMovement.Clear();
            _selectedWorldAlly = null;
            RefreshWorldAllyViews();
            return;
        }

        foreach (IBattleCharacter character in characters)
        {
            if (character != null && !_battleCharacters.Contains(character))
            {
                _battleCharacters.Add(character);
                CharacterRuntime runtime = character as CharacterRuntime;
                BindPresentationCharacter(runtime);
                if (!previousCharacters.Contains(character) &&
                    runtime?.Definition != null)
                {
                    PublishUnitLifecycle(new BattleUnitLifecycleEvent(
                        BattleUnitLifecycleType.Spawned,
                        BattleStatusTarget.FromAlly(character),
                        runtime.Definition));
                }
            }
        }
        RemoveStaleWorldAllyMovementStates();
        RefreshWorldAllyViews();
    }

    public BattleVfxTarget ResolveVfxTarget(
        BattleStatusTarget target,
        BattleVfxAnchorType anchorType)
    {
        if (!target.IsValid)
            return default;

        if (target.Enemy != null)
        {
            BattleVfxTargetHandle handle =
                GetOrCreateEnemyVfxHandle(target.Enemy);
            BattleVfxAnchorSnapshot anchor = default;
            if (TryGetWorldEnemyAnchor(
                    target.Enemy,
                    anchorType,
                    out BattleVfxAnchorSnapshot worldAnchor))
            {
                anchor = worldAnchor;
                StoreEnemyVfxAnchor(target.Enemy, anchorType, worldAnchor);
            }
            else if (TryFindEnemyTile(target.Enemy, out DungeonBoardSlot tile) &&
                tile.TryGetEnemyVfxAnchor(
                    target.Enemy,
                    anchorType,
                    out BattleVfxAnchorSnapshot liveAnchor))
            {
                anchor = liveAnchor;
                StoreEnemyVfxAnchor(target.Enemy, anchorType, liveAnchor);
            }
            else
            {
                TryGetStoredEnemyVfxAnchor(
                    target.Enemy,
                    anchorType,
                    out anchor);
            }

            return new BattleVfxTarget(handle, target, anchor);
        }

        BattleVfxTargetHandle allyHandle =
            GetOrCreateAllyVfxHandle(target.Ally);
        BattleVfxAnchorSnapshot allyAnchor = default;
        if (TryGetWorldAllyAnchor(
                target.Ally,
                anchorType,
                out BattleVfxAnchorSnapshot worldAllyAnchor))
        {
            allyAnchor = worldAllyAnchor;
        }
        else if (target.Ally is IBattleVfxAnchorProvider provider)
        {
            provider.TryGetVfxAnchor(anchorType, out allyAnchor);
            if (!allyAnchor.HasFrame &&
                TryGetVfxTileFrame(out RectTransform tileFrame))
            {
                BattleVfxUiAnchorUtility.TryAttachScreenFrame(
                    allyAnchor,
                    tileFrame,
                    out allyAnchor);
            }
        }
        return new BattleVfxTarget(allyHandle, target, allyAnchor);
    }

    private bool TryGetVfxTileFrame(out RectTransform tileFrame)
    {
        tileFrame = null;
        return false;
    }

    public void SetPracticeDebugVisualization(bool enabled)
    {
        bool canEnable = enabled &&
                         UsesWorldPresentation &&
                         practiceDebugOverlay != null &&
                         practiceDebugOverlay.HasRequiredReferences;
        _practiceDebugVisualizationEnabled = canEnable;
        practiceDebugOverlay?.SetVisible(canEnable);
        if (!canEnable)
        {
            practiceDebugOverlay?.Clear();
            _lastPracticeDebugAlly = null;
            _lastPracticeDebugEnemy = null;
            return;
        }

        if (_lastPracticeDebugAlly == null &&
            _lastPracticeDebugEnemy == null)
        {
            _lastPracticeDebugAlly = _selectedWorldAlly;
        }
        RefreshPracticeDebugVisualization();
    }

    public void Initialize(int gridSize, int stackSize)
    {
        EnsurePresentationPipeline();
        if (boardRect == null)
        {
            Debug.LogError(
                "DungeonBoardView requires an authored world viewport.",
                this);
            return;
        }

        if (_arenaSetup.UsesBattleCore && !HasWorldPresentation)
        {
            Debug.LogError(
                "DungeonBoardView 2.5D world Scene and prefab " +
                "references are incomplete.",
                this);
            return;
        }

        if (_arenaSetup.UsesBattleCore)
        {
            gridSize = ResolveRequiredFormationGridSize();
            stackSize = 1;
            _activeTileCount = Mathf.Min(
                _arenaSetup.MaximumEnemyCapacity,
                gridSize * gridSize);
        }
        else
        {
            _activeTileCount = gridSize * gridSize;
        }

        _maximumStackSize = Mathf.Max(1, stackSize);
        _initialized = true;
        CreateEnemySlots(gridSize);
        SetGridSize(gridSize);
    }

    public void ConfigureArena(
        BattleArenaSetup setup,
        BattleEnvironmentSetup environment = null,
        int currentCoreHealth = -1)
    {
        _arenaSetup = setup ?? BattleArenaSetup.Legacy;
        _environmentSetup = environment ?? BattleEnvironmentSetup.Default;
        _battleCore.Configure(
            _arenaSetup.CoreMaximumHealth,
            _arenaSetup.UsesBattleCore,
            currentCoreHealth);
        worldBattleCoreGauge?.SetArenaRadius(
            _arenaSetup.UsesBattleCore
                ? _arenaSetup.WorldRadius
                : BattleArenaSetup.DefaultWorldRadius);
        ApplyWorldArenaRadius();
        ApplyWorldEnvironment();
        ApplyArenaPresentation();
        ConfigureVfxPresentationTarget();
        _circularEnemyStates.Clear();
        _recentCoreAttackTimes.Clear();
        _spatialBattleElapsedTime = 0f;
        _nextFormationStableOrder = 0;
        _formationSpawnRadius = 0f;
        _formationDefenseLineRadius = 0f;
        _formationRadiiInitialized = false;
        RefreshWorldAllyViews();
    }

    public void SetDungeonStageProgress(float progress)
    {
        DungeonStageProgress = float.IsNaN(progress) ||
                               float.IsInfinity(progress)
            ? 0f
            : Mathf.Max(0f, progress);
    }

    public void SetPixelSize(float size)
    {
        RefreshWorldRenderTexture();
    }

    public void SetGridSize(int size)
    {
        if (!_initialized)
            return;

        size = _arenaSetup.UsesBattleCore
            ? ResolveRequiredFormationGridSize()
            : Mathf.Clamp(size, MinimumGridSize, MaximumGridSize);
        _activeTileCount = _arenaSetup.UsesBattleCore
            ? Mathf.Min(_arenaSetup.MaximumEnemyCapacity, size * size)
            : size * size;

        if (size == GridSize && _tiles.Count == size * size)
        {
            RefreshLayout();
            return;
        }

        List<EnemyRuntime>[,] previousEnemies = CaptureExistingStacks();
        int previousSize = GridSize;

        ClearTileObjects();
        _enemyPlacements.Clear();
        _exclusiveOccupants.Clear();
        GridSize = size;
        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
            {
                DungeonBoardSlot tile = new();
                tile.Initialize(row, column, _maximumStackSize);
                _tiles.Add(tile);
            }
        }

        RestoreExistingStacks(previousEnemies, previousSize);
        SynchronizeEnemyPresentationBindings();
        RefreshLayout();
        OccupancyChanged?.Invoke();
    }

    public bool TryAddEnemyCard(
        int row,
        int column,
        EnemyRuntime enemy)
    {
        if (enemy == null ||
            !TryBuildPlacementAt(
                row,
                column,
                enemy,
                null,
                out PendingEnemyPlacement placement))
        {
            return false;
        }

        return TryCommitPlacements(new[] { placement });
    }

    public bool TryAddEnemyCardToRandomTile(EnemyRuntime enemy)
    {
        if (enemy == null)
            return false;

        List<PendingEnemyPlacement> candidates =
            CollectPlacementCandidates(enemy, null);
        if (candidates.Count == 0)
            return false;

        int index = Random.Range(0, candidates.Count);
        return TryCommitPlacements(new[] { candidates[index] });
    }

    public bool TryAddEnemyCardToNextAvailableTile(EnemyRuntime enemy)
    {
        if (enemy == null)
            return false;

        List<PendingEnemyPlacement> candidates =
            CollectPlacementCandidates(enemy, null);
        if (candidates.Count == 0)
            return false;

        List<PendingEnemyPlacement> best = new();
        int smallestStackCount = int.MaxValue;
        foreach (PendingEnemyPlacement candidate in candidates)
        {
            int stackCount = candidate.Anchor.StackCount;
            if (stackCount < smallestStackCount)
            {
                smallestStackCount = stackCount;
                best.Clear();
            }

            if (stackCount == smallestStackCount)
                best.Add(candidate);
        }

        int randomIndex = Random.Range(0, best.Count);
        return TryCommitPlacements(new[] { best[randomIndex] });
    }

    public bool TryAddEnemy(EnemyRuntime enemy)
    {
        return TryAddEnemyCardToNextAvailableTile(enemy);
    }

    public bool TryAddEnemiesToDistinctTiles(
        IReadOnlyList<EnemyRuntime> enemies)
    {
        if (enemies == null || enemies.Count == 0)
            return false;

        HashSet<DungeonBoardSlot> reservedTiles = new();
        List<PendingEnemyPlacement> placements = new(enemies.Count);
        return TryBuildGroupPlacements(
                   enemies,
                   0,
                   reservedTiles,
                   placements) &&
               TryCommitPlacements(placements);
    }

    private bool TryBuildGroupPlacements(
        IReadOnlyList<EnemyRuntime> enemies,
        int index,
        HashSet<DungeonBoardSlot> reservedTiles,
        List<PendingEnemyPlacement> placements)
    {
        if (index >= enemies.Count)
            return true;

        EnemyRuntime enemy = enemies[index];
        if (enemy == null)
            return false;

        List<PendingEnemyPlacement> candidates =
            CollectPlacementCandidates(enemy, reservedTiles);
        foreach (PendingEnemyPlacement candidate in candidates)
        {
            foreach (DungeonBoardSlot tile in candidate.OccupiedTiles)
                reservedTiles.Add(tile);
            placements.Add(candidate);

            if (TryBuildGroupPlacements(
                    enemies,
                    index + 1,
                    reservedTiles,
                    placements))
            {
                return true;
            }

            placements.RemoveAt(placements.Count - 1);
            foreach (DungeonBoardSlot tile in candidate.OccupiedTiles)
                reservedTiles.Remove(tile);
        }

        return false;
    }

    private List<PendingEnemyPlacement> CollectPlacementCandidates(
        EnemyRuntime enemy,
        ISet<DungeonBoardSlot> reservedTiles)
    {
        List<PendingEnemyPlacement> result = new();
        if (enemy == null)
            return result;

        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
            {
                if (TryBuildPlacementAt(
                        row,
                        column,
                        enemy,
                        reservedTiles,
                        out PendingEnemyPlacement placement))
                {
                    result.Add(placement);
                }
            }
        }

        result.Sort((left, right) =>
        {
            int stack = left.Anchor.StackCount.CompareTo(
                right.Anchor.StackCount);
            if (stack != 0)
                return stack;
            int row = left.Anchor.Row.CompareTo(right.Anchor.Row);
            return row != 0
                ? row
                : left.Anchor.Column.CompareTo(right.Anchor.Column);
        });
        return result;
    }

    private bool TryBuildPlacementAt(
        int row,
        int column,
        EnemyRuntime enemy,
        ISet<DungeonBoardSlot> reservedTiles,
        out PendingEnemyPlacement placement)
    {
        placement = default;
        if (enemy?.Definition == null ||
            _enemyPlacements.ContainsKey(enemy))
        {
            return false;
        }

        EnemySO definition = enemy.Definition;
        bool exclusive = !_arenaSetup.UsesBattleCore &&
                         definition.StackingPolicy ==
                             EnemyStackingPolicy.Exclusive;
        int width = exclusive ? definition.FootprintWidth : 1;
        int height = exclusive ? definition.FootprintHeight : 1;
        if (row < 0 || column < 0 ||
            row + height > GridSize ||
            column + width > GridSize)
        {
            return false;
        }

        List<DungeonBoardSlot> occupiedTiles = new(width * height);
        for (int rowOffset = 0; rowOffset < height; rowOffset++)
        {
            for (int columnOffset = 0;
                 columnOffset < width;
                 columnOffset++)
            {
                if (!TryGetTile(
                        row + rowOffset,
                        column + columnOffset,
                        out DungeonBoardSlot tile) ||
                    tile == null ||
                    reservedTiles?.Contains(tile) == true ||
                    _exclusiveOccupants.ContainsKey(tile))
                {
                    return false;
                }

                if (exclusive && tile.StackCount > 0)
                    return false;
                occupiedTiles.Add(tile);
            }
        }

        DungeonBoardSlot anchor = occupiedTiles[0];
        if (!anchor.CanAddEnemy ||
            (!exclusive && anchor.IsFull))
        {
            return false;
        }

        placement = new PendingEnemyPlacement(
            enemy,
            anchor,
            occupiedTiles,
            exclusive);
        return true;
    }

    private bool TryCommitPlacements(
        IReadOnlyList<PendingEnemyPlacement> placements)
    {
        if (placements == null || placements.Count == 0)
            return false;
        if (_arenaSetup.UsesBattleCore &&
            _enemyPlacements.Count + placements.Count >
            TotalEnemyCapacity)
        {
            return false;
        }

        List<PendingEnemyPlacement> added = new(placements.Count);
        foreach (PendingEnemyPlacement placement in placements)
        {
            if (placement.Enemy == null ||
                placement.Anchor == null ||
                !placement.Anchor.TryAdd(placement.Enemy))
            {
                for (int index = added.Count - 1; index >= 0; index--)
                    added[index].Anchor.TryRemoveTop();
                return false;
            }
            added.Add(placement);
        }

        foreach (PendingEnemyPlacement pending in added)
            RegisterPlacement(pending);

        foreach (PendingEnemyPlacement pending in added)
        {
            EnemyRuntime enemy = pending.Enemy;
            BindPresentationEnemy(enemy);
            PublishUnitLifecycle(new BattleUnitLifecycleEvent(
                BattleUnitLifecycleType.Spawned,
                BattleStatusTarget.FromEnemy(enemy),
                enemy.Definition));
            ExecuteSpawnAbilities(pending.Anchor, enemy);
            pending.Anchor.RefreshTopEnemyCard();
        }

        OccupancyChanged?.Invoke();
        return true;
    }

    private void RegisterPlacement(PendingEnemyPlacement pending)
    {
        pending.Enemy.BindBattleBoard(this);
        EnemyPlacement placement = new(
            pending.Enemy,
            pending.Anchor,
            pending.OccupiedTiles,
            pending.IsExclusive);
        _enemyPlacements[pending.Enemy] = placement;
        if (_arenaSetup.UsesBattleCore)
        {
            EnsureFormationRadii();
            if (TryReserveFormationCell(
                    out int sectorIndex,
                    out int layerIndex))
            {
                Vector2 direction = ResolveFormationSectorDirection(
                    sectorIndex);
                float initialRadius = ResolveFormationSpawnRadius(
                    pending.Enemy,
                    sectorIndex,
                    layerIndex);
                int stableOrder = _nextFormationStableOrder++;
                CircularEnemyState state = new(
                    pending.Enemy.CoreAttackInterval,
                    sectorIndex,
                    layerIndex,
                    direction,
                    initialRadius,
                    _formationDefenseLineRadius,
                    _formationDefenseLineRadius,
                    stableOrder);
                _circularEnemyStates[pending.Enemy] = state;
                RefreshFormationTargets();
            }
            else
            {
                Debug.LogError(
                    "Circular enemy formation has no available cell.",
                    this);
            }
        }
        if (!pending.IsExclusive)
            return;

        foreach (DungeonBoardSlot tile in pending.OccupiedTiles)
        {
            _exclusiveOccupants[tile] = pending.Enemy;
            tile.SetExclusiveFootprintOccupant(
                pending.Enemy,
                ReferenceEquals(tile, pending.Anchor));
        }
    }

    private void ReleasePlacement(
        EnemyRuntime enemy,
        bool notify = true)
    {
        if (enemy == null ||
            !_enemyPlacements.TryGetValue(
                enemy,
                out EnemyPlacement placement))
        {
            return;
        }

        _enemyPlacements.Remove(enemy);
        enemy.BindBattleBoard(null);
        _enemyRadiusContacts.RemoveWhere(contact =>
            ReferenceEquals(contact.Source, enemy) ||
            ReferenceEquals(contact.Target, enemy));
        _currentEnemyRadiusContacts.RemoveWhere(contact =>
            ReferenceEquals(contact.Source, enemy) ||
            ReferenceEquals(contact.Target, enemy));
        _initializedEnemyRadiusAbilities.RemoveWhere(contact =>
            ReferenceEquals(contact.Source, enemy));
        int releasedSector = -1;
        if (_circularEnemyStates.TryGetValue(
                enemy,
                out CircularEnemyState releasedState))
        {
            releasedSector = releasedState.SectorIndex;
        }
        _circularEnemyStates.Remove(enemy);
        _recentCoreAttackTimes.Remove(enemy);
        RemoveWorldEnemyView(enemy);

        if (placement.IsExclusive)
        {
            foreach (DungeonBoardSlot tile in placement.OccupiedTiles)
            {
                if (tile != null &&
                    _exclusiveOccupants.TryGetValue(
                        tile,
                        out EnemyRuntime occupant) &&
                    ReferenceEquals(occupant, enemy))
                {
                    _exclusiveOccupants.Remove(tile);
                    tile.SetExclusiveFootprintOccupant(null, false);
                }
            }
        }

        if (notify)
            OccupancyChanged?.Invoke();
        if (_arenaSetup.UsesBattleCore)
        {
            if (releasedSector >= 0)
                CompactFormationSector(releasedSector);
            RefreshFormationTargets();
            RefreshCircularLayout();
        }
    }

    private EnemyRuntime GetEnemyAtTile(DungeonBoardSlot tile)
    {
        if (tile == null)
            return null;
        return _exclusiveOccupants.TryGetValue(
            tile,
            out EnemyRuntime occupant)
            ? occupant
            : tile.TopEnemy;
    }

    private DungeonBoardSlot ResolveAnchorTile(DungeonBoardSlot tile)
    {
        EnemyRuntime enemy = GetEnemyAtTile(tile);
        return enemy != null &&
               _enemyPlacements.TryGetValue(
                   enemy,
                   out EnemyPlacement placement)
            ? placement.Anchor
            : tile;
    }

    public bool TryRemoveTopEnemyCard(int row, int column)
    {
        if (!TryGetTile(row, column, out DungeonBoardSlot selectedTile))
            return false;

        DungeonBoardSlot tile = ResolveAnchorTile(selectedTile);
        EnemyRuntime enemy = GetEnemyAtTile(selectedTile);
        if (tile == null || enemy == null ||
            !ReferenceEquals(tile.TopEnemy, enemy))
        {
            return false;
        }

        CaptureEnemyVfxAnchor(enemy, tile);
        bool removed = tile.TryRemoveTop();
        if (removed)
        {
            ReleasePlacement(enemy, false);
            SynchronizeEnemyPresentationBindings();
            OccupancyChanged?.Invoke();
        }
        return removed;
    }

    public int GetStackCount(int row, int column)
    {
        if (!TryGetTile(row, column, out DungeonBoardSlot tile))
            return 0;
        return _exclusiveOccupants.ContainsKey(tile)
            ? 1
            : tile.StackCount;
    }

    public int GetTopEnemyHealth(int row, int column)
    {
        return TryGetTile(row, column, out DungeonBoardSlot tile)
            ? GetEnemyAtTile(tile)?.Health ?? 0
            : 0;
    }

    public bool TrySetTopEnemyHealth(int row, int column, int health)
    {
        if (!TryGetTile(row, column, out DungeonBoardSlot tile))
            return false;
        DungeonBoardSlot anchor = ResolveAnchorTile(tile);
        return anchor != null && anchor.TrySetTopEnemyHealth(health);
    }

    public bool ContainsTargetableEnemy(EnemyRuntime enemy)
    {
        return TryFindEnemyTile(enemy, out _);
    }

    public int TryDamageEnemy(EnemyRuntime enemy, int damage)
    {
        if (damage <= 0 ||
            !TryFindEnemyTile(enemy, out DungeonBoardSlot tile))
        {
            return 0;
        }

        bool wasAlive = enemy.Health > 0;
        int appliedDamage = TryDamageTile(tile, damage);
        if (wasAlive && enemy.Health <= 0)
        {
            PublishUnitLifecycle(new BattleUnitLifecycleEvent(
                BattleUnitLifecycleType.Defeated,
                BattleStatusTarget.FromEnemy(enemy),
                enemy.Definition));
        }
        return appliedDamage;
    }

    public bool TryApplyFireToEnemy(
        EnemyRuntime enemy,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        return TryApplyFireToEnemy(
            enemy,
            duration,
            tickInterval,
            tickDamage,
            BattleAbilityUser.ForStatusEffect());
    }

    public bool TryApplyFireToEnemy(
        EnemyRuntime enemy,
        float duration,
        float tickInterval,
        int tickDamage,
        BattleAbilityUser user)
    {
        if (!TryFindEnemyTile(enemy, out DungeonBoardSlot tile))
            return false;

        bool applied = TryApplyFireStatus(
            tile,
            user,
            duration,
            tickInterval,
            tickDamage);
        if (applied && ReferenceEquals(tile.TopEnemy, enemy))
            tile.ShowTargetArea();
        return applied;
    }

    public bool TryForcePriorityTarget(EnemyRuntime enemy, float duration)
    {
        duration = TimePrecision.Normalize(duration, 0.1f);
        if (duration <= 0f || !TryFindEnemyTile(enemy, out _))
            return false;

        _forcedPriorityTarget = enemy;
        _forcedPriorityRemaining = duration;
        return true;
    }

    public IReadOnlyList<EnemyRuntime> SelectCharacterTargets(
        IBattleCharacter source,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null)
            return Array.Empty<EnemyRuntime>();

        // Ally-only modes are normalized defensively in case serialized data
        // changes faction without going through the character editor.
        if (subject == CharacterAttackSubject.Self ||
            subject == CharacterAttackSubject.RandomExceptSelf ||
            subject == CharacterAttackSubject.None ||
            subject == CharacterAttackSubject.Manual)
            subject = CharacterAttackSubject.Random;
        else if (subject == CharacterAttackSubject.AllExceptSelf)
            subject = CharacterAttackSubject.All;

        List<DungeonBoardSlot> candidates = CollectPriorityTargetTiles(
            out bool hasAlternateTarget);
        candidates.RemoveAll(tile => !MatchesCharacterConditions(
            source,
            tile,
            conditionMatchMode,
            numericConditions));
        if (candidates.Count == 0)
            return Array.Empty<EnemyRuntime>();

        targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);
        List<DungeonBoardSlot> selected = new(candidates.Count);
        IBattleCardActionRuntimeService cardRuntime =
            _abilityUserModifierService as
                IBattleCardActionRuntimeService;
        if (cardRuntime?.TryGetForcedTarget(
                source,
                out EnemyRuntime sourceForcedEnemy) == true &&
            TryFindEnemyTile(
                sourceForcedEnemy,
                out DungeonBoardSlot sourceForcedTarget) &&
            candidates.Remove(sourceForcedTarget))
        {
            selected.Add(sourceForcedTarget);
        }
        else if (TryGetForcedPriorityTile(
                     out DungeonBoardSlot forcedTarget) &&
            candidates.Remove(forcedTarget))
        {
            selected.Add(forcedTarget);
        }

        if (subject == CharacterAttackSubject.All)
        {
            selected.AddRange(candidates);
        }
        else if (subject == CharacterAttackSubject.Random)
        {
            for (int index = 0;
                 index < candidates.Count;
                 index++)
            {
                int randomIndex = Random.Range(index, candidates.Count);
                (candidates[index], candidates[randomIndex]) =
                    (candidates[randomIndex], candidates[index]);
            }
            StableSortByTargetPriority(
                candidates,
                hasAlternateTarget);
            for (int index = 0;
                 index < candidates.Count && selected.Count < targetCount;
                 index++)
            {
                selected.Add(candidates[index]);
            }
        }
        else
        {
            bool descending = subject == CharacterAttackSubject.HighestValue;
            candidates.Sort((left, right) =>
            {
                int priorityComparison = CompareTargetPriority(
                    left,
                    right,
                    hasAlternateTarget);
                if (priorityComparison != 0)
                    return priorityComparison;

                int leftValue = metric == CharacterAttackSubjectMetric.Health
                    ? left.TopEnemyHealth
                    : metric == CharacterAttackSubjectMetric.Shield
                        ? left.TopEnemy.CurrentShield
                        : left.StackCount;
                int rightValue = metric == CharacterAttackSubjectMetric.Health
                    ? right.TopEnemyHealth
                    : metric == CharacterAttackSubjectMetric.Shield
                        ? right.TopEnemy.CurrentShield
                        : right.StackCount;
                int comparison = leftValue.CompareTo(rightValue);
                return descending ? -comparison : comparison;
            });

            for (int index = 0;
                 index < candidates.Count && selected.Count < targetCount;
                 index++)
            {
                selected.Add(candidates[index]);
            }
        }

        List<EnemyRuntime> result = new(selected.Count);
        foreach (DungeonBoardSlot tile in selected)
        {
            if (tile?.TopEnemy != null)
                result.Add(tile.TopEnemy);
        }

        return result;
    }

    public IReadOnlyList<IBattleCharacter> SelectAlliedCharacters(
        IBattleCharacter source,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null)
            return Array.Empty<IBattleCharacter>();

        if (subject == CharacterAttackSubject.None ||
            subject == CharacterAttackSubject.Manual)
            subject = CharacterAttackSubject.Random;

        List<IBattleCharacter> candidates = new();
        foreach (IBattleCharacter character in _battleCharacters)
        {
            if (character != null && MatchesCharacterConditions(
                    source,
                    character,
                    conditionMatchMode,
                    numericConditions))
            {
                candidates.Add(character);
            }
        }

        if (candidates.Count == 0)
            return Array.Empty<IBattleCharacter>();

        if (subject == CharacterAttackSubject.Self)
        {
            foreach (IBattleCharacter candidate in candidates)
            {
                if (ReferenceEquals(candidate, source))
                    return new[] { candidate };
            }

            return Array.Empty<IBattleCharacter>();
        }

        if (subject == CharacterAttackSubject.AllExceptSelf)
        {
            candidates.RemoveAll(candidate => ReferenceEquals(
                candidate,
                source));
            return candidates;
        }

        if (subject == CharacterAttackSubject.RandomExceptSelf)
        {
            candidates.RemoveAll(candidate => ReferenceEquals(
                candidate,
                source));
            if (candidates.Count == 0)
                return Array.Empty<IBattleCharacter>();
            subject = CharacterAttackSubject.Random;
        }

        if (subject == CharacterAttackSubject.All)
            return candidates;

        targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);
        if (subject == CharacterAttackSubject.Random)
        {
            for (int index = 0; index < targetCount; index++)
            {
                int randomIndex = Random.Range(index, candidates.Count);
                (candidates[index], candidates[randomIndex]) =
                    (candidates[randomIndex], candidates[index]);
            }
        }
        else
        {
            bool descending = subject == CharacterAttackSubject.HighestValue;
            candidates.Sort((left, right) =>
            {
                float leftValue = GetCharacterMetric(left, metric);
                float rightValue = GetCharacterMetric(right, metric);
                int comparison = leftValue.CompareTo(rightValue);
                return descending ? -comparison : comparison;
            });
        }

        if (candidates.Count > targetCount)
            candidates.RemoveRange(targetCount, candidates.Count - targetCount);
        return candidates;
    }

    public IReadOnlyList<EnemyRuntime> FilterCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null || targets == null || targets.Count == 0)
            return Array.Empty<EnemyRuntime>();

        List<EnemyRuntime> result = new(targets.Count);
        foreach (EnemyRuntime target in targets)
        {
            if (target != null &&
                TryFindEnemyTile(target, out DungeonBoardSlot tile) &&
                MatchesCharacterConditions(
                    source,
                    tile,
                    conditionMatchMode,
                    numericConditions))
            {
                result.Add(target);
            }
        }

        return result;
    }

    public IReadOnlyList<IBattleCharacter> FilterAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (source == null || targets == null || targets.Count == 0)
            return Array.Empty<IBattleCharacter>();

        List<IBattleCharacter> result = new(targets.Count);
        foreach (IBattleCharacter target in targets)
        {
            if (target != null &&
                _battleCharacters.Contains(target) &&
                MatchesCharacterConditions(
                    source,
                    target,
                    conditionMatchMode,
                    numericConditions))
            {
                result.Add(target);
            }
        }

        return result;
    }

    public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
        IReadOnlyList<EnemyRuntime> centerTargets,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets)
    {
        return ExpandCharacterAreaTargets(
            centerTargets,
            areaOffsets,
            true);
    }

    public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
        IReadOnlyList<EnemyRuntime> centerTargets,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets,
        bool includeCenterTargets)
    {
        if (centerTargets == null || centerTargets.Count == 0)
            return Array.Empty<EnemyRuntime>();

        List<EnemyRuntime> result = new();
        HashSet<EnemyRuntime> uniqueEnemies = new();
        HashSet<DungeonBoardSlot> uniqueTiles = new();

        void AddAreaTile(DungeonBoardSlot tile)
        {
            if (tile == null || !uniqueTiles.Add(tile))
                return;

            tile.ShowTargetArea();
            EnemyRuntime enemy = GetEnemyAtTile(tile);
            if (enemy != null && uniqueEnemies.Add(enemy))
                result.Add(enemy);
        }

        foreach (EnemyRuntime centerTarget in centerTargets)
        {
            if (!TryFindEnemyTile(centerTarget, out DungeonBoardSlot centerTile))
                continue;

            if (includeCenterTargets)
                AddAreaTile(centerTile);
            if (areaOffsets == null)
                continue;

            foreach (CharacterTargetAreaOffset offset in areaOffsets)
            {
                if (offset == null || offset.IsCenter ||
                    !TryGetTile(
                        centerTile.Row + offset.RowOffset,
                        centerTile.Column + offset.ColumnOffset,
                        out DungeonBoardSlot areaTile))
                {
                    continue;
                }

                AddAreaTile(areaTile);
            }
        }

        return result;
    }

    private static bool MatchesCharacterConditions(
        IBattleCharacter source,
        DungeonBoardSlot tile,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return true;
        if (tile?.TopEnemy == null)
            return false;

        bool matchAny = matchMode == CharacterConditionMatchMode.Any;
        bool evaluatedAny = false;
        foreach (CharacterNumericCondition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            if (condition.Target == CharacterConditionTarget.Source)
            {
                bool sourceMatched =
                    CharacterConditionEvaluator.MatchesCharacter(
                        condition,
                        source);
                if (matchAny && sourceMatched)
                    return true;
                if (!matchAny && !sourceMatched)
                    return false;
                continue;
            }

            if (condition.Metric ==
                CharacterNumericConditionMetric.StatusStackCount)
            {
                bool statusMatched =
                    CharacterConditionEvaluator.MatchesStatusCondition(
                        condition,
                        tile.TopEnemy.GetStatusStackCount,
                        tile.TopEnemy.GetActiveStatusEffects());
                if (matchAny && statusMatched)
                    return true;
                if (!matchAny && !statusMatched)
                    return false;
                continue;
            }

            float value = condition.Metric switch
            {
                CharacterNumericConditionMetric.Health =>
                    tile.TopEnemy.Health,
                CharacterNumericConditionMetric.HealthPercentage =>
                    tile.TopEnemy.MaxHealth > 0
                        ? tile.TopEnemy.Health * 100f /
                          tile.TopEnemy.MaxHealth
                        : 0f,
                CharacterNumericConditionMetric.MaximumHealth =>
                    tile.TopEnemy.MaxHealth,
                CharacterNumericConditionMetric.StackCount =>
                    tile.StackCount,
                CharacterNumericConditionMetric.Shield =>
                    tile.TopEnemy.CurrentShield,
                _ => 0f
            };
            bool matched = CharacterConditionEvaluator.Compare(
                value,
                condition.Comparison,
                condition.Threshold);
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static bool MatchesCharacterConditions(
        IBattleCharacter source,
        IBattleCharacter character,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
            return true;
        if (character == null)
            return false;

        bool matchAny = matchMode == CharacterConditionMatchMode.Any;
        bool evaluatedAny = false;
        foreach (CharacterNumericCondition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            IBattleCharacter evaluatedCharacter =
                condition.Target == CharacterConditionTarget.Source
                    ? source
                    : character;
            bool matched =
                CharacterConditionEvaluator.MatchesCharacter(
                    condition,
                    evaluatedCharacter);
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static float GetCharacterMetric(
        IBattleCharacter character,
        CharacterAttackSubjectMetric metric)
    {
        if (character == null)
            return 0f;

        return metric switch
        {
            CharacterAttackSubjectMetric.Health =>
                character.CurrentHealth,
            CharacterAttackSubjectMetric.Shield =>
                character.CurrentShield,
            CharacterAttackSubjectMetric.AttackSpeed =>
                character.CurrentAttackSpeed,
            _ => character.CurrentAttackPower,
        };
    }

    private static bool CompareCharacterCondition(
        float value,
        CharacterNumericComparison comparison,
        float threshold)
    {
        return comparison switch
        {
            CharacterNumericComparison.GreaterThanOrEqual =>
                value >= threshold,
            CharacterNumericComparison.LessThanOrEqual => value <= threshold,
            CharacterNumericComparison.GreaterThan => value > threshold,
            CharacterNumericComparison.LessThan => value < threshold,
            CharacterNumericComparison.Equal =>
                Mathf.Approximately(value, threshold),
            CharacterNumericComparison.NotEqual =>
                !Mathf.Approximately(value, threshold),
            _ => true
        };
    }

    public int TryDamageCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int damage,
        CharacterAttackDamageType damageType,
        bool showAttackRange)
    {
        if (targets == null || damage <= 0 ||
            damageType == CharacterAttackDamageType.StatusEffect ||
            damageType == CharacterAttackDamageType.StatusRemoval)
        {
            return 0;
        }

        int totalDamage = 0;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonBoardSlot tile))
            {
                continue;
            }

            totalDamage = BattleValueMath.SaturatingAddNonNegative(
                totalDamage,
                TryDamageTile(
                    tile,
                    damage,
                    damageType,
                    source));
        }

        return totalDamage;
    }

    public int TryHealCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int amount,
        bool showAttackRange)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalHealed = 0;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonBoardSlot tile) ||
                !ReferenceEquals(tile.TopEnemy, enemy))
            {
                continue;
            }

            if (showAttackRange)
                tile.ShowTargetArea();
            totalHealed = BattleValueMath.SaturatingAddNonNegative(
                totalHealed,
                tile.TryHealTop(amount));
        }

        return totalHealed;
    }

    public int TryHealAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        int amount)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalHealed = 0;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target != null && uniqueTargets.Add(target))
            {
                totalHealed = BattleValueMath.SaturatingAddNonNegative(
                    totalHealed,
                    target.Heal(amount));
            }
        }

        return totalHealed;
    }

    public int TryGrantShieldToCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int amount,
        bool showAttackRange)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalGranted = 0;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonBoardSlot tile) ||
                !ReferenceEquals(tile.TopEnemy, enemy))
            {
                continue;
            }

            if (showAttackRange)
                tile.ShowTargetArea();
            totalGranted = BattleValueMath.SaturatingAddNonNegative(
                totalGranted,
                tile.TryGrantShieldTop(amount));
        }

        return totalGranted;
    }

    public int TryGrantShieldToAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        int amount)
    {
        if (targets == null || amount <= 0)
            return 0;

        int totalGranted = 0;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target != null && uniqueTargets.Add(target))
            {
                totalGranted = BattleValueMath.SaturatingAddNonNegative(
                    totalGranted,
                    target.GainShield(amount));
            }
        }

        return totalGranted;
    }

    public bool TryApplyCharacterStatus(
        BattleAbilityUser user,
        IReadOnlyList<EnemyRuntime> targets,
        StatusEffectSO statusEffect,
        float duration,
        float stacks,
        float tickInterval,
        bool showAttackRange)
    {
        if (targets == null || statusEffect == null ||
            !statusEffect.CanTargetEnemy || stacks <= 0f)
        {
            return false;
        }

        float effectiveDuration = statusEffect.DurationMode ==
            StatusEffectDurationMode.Permanent
                ? 0f
                : (duration > 0f ? duration : statusEffect.DefaultDuration);
        if (statusEffect.DurationMode == StatusEffectDurationMode.Timed &&
            effectiveDuration <= 0f)
        {
            return false;
        }

        int stackCount = Mathf.Max(
            1,
            Mathf.RoundToInt(stacks));

        bool applied = false;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonBoardSlot tile))
            {
                continue;
            }

            int previousStacks = enemy.GetStatusStackCount(statusEffect);
            bool targetApplied = tile.TryApplyStatusToTop(
                statusEffect,
                effectiveDuration,
                stackCount,
                user,
                tickInterval,
                TryDamageTile);
            if (targetApplied)
            {
                NotifyStatusApplied(new BattleStatusAppliedEvent(
                    BattleStatusTarget.FromEnemy(enemy),
                    statusEffect,
                    previousStacks,
                    enemy.GetStatusStackCount(statusEffect),
                    user));
            }
            if (targetApplied && showAttackRange &&
                ReferenceEquals(tile.TopEnemy, enemy))
                tile.ShowTargetArea();
            applied |= targetApplied;
        }

        return applied;
    }

    public bool TryApplyAlliedCharacterStatus(
        BattleAbilityUser user,
        IReadOnlyList<IBattleCharacter> targets,
        StatusEffectSO statusEffect,
        float duration,
        float stacks)
    {
        if (targets == null || statusEffect == null ||
            !statusEffect.CanTargetAlly || stacks <= 0f)
        {
            return false;
        }

        int stackCount = Mathf.Max(1, Mathf.RoundToInt(stacks));
        bool applied = false;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target == null || !uniqueTargets.Add(target))
                continue;

            applied |= target is CharacterRuntime runtime
                ? runtime.ApplyStatusEffect(
                    statusEffect,
                    duration,
                    stackCount,
                    user)
                : target.ApplyStatusEffect(
                    statusEffect,
                    duration,
                    stackCount,
                    user.Unit.Ally);
        }

        return applied;
    }

    public bool TryRemoveCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        bool showAttackRange)
    {
        if (targets == null)
            return false;

        bool removedAny = false;
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || !uniqueTargets.Add(enemy) ||
                !TryFindEnemyTile(enemy, out DungeonBoardSlot tile))
            {
                continue;
            }

            int removed = tile.TryRemoveStatusFromTop(
                removalSelection,
                removalAmount,
                TryDamageTile);
            if (removed <= 0)
                continue;

            if (showAttackRange &&
                ReferenceEquals(tile.TopEnemy, enemy))
                tile.ShowTargetArea();
            removedAny = true;
        }

        return removedAny;
    }

    public bool TryRemoveAlliedCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount)
    {
        if (targets == null)
            return false;

        bool removedAny = false;
        HashSet<IBattleCharacter> uniqueTargets = new();
        foreach (IBattleCharacter target in targets)
        {
            if (target == null || !uniqueTargets.Add(target))
                continue;

            removedAny |= target.RemoveStatusEffects(
                removalSelection,
                removalAmount) > 0;
        }

        return removedAny;
    }

    private bool TryApplyFireStatus(
        DungeonBoardSlot tile,
        BattleAbilityUser user,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        tile = ResolveAnchorTile(tile);
        if (tile == null)
            return false;

        EnemyRuntime enemy = tile.TopEnemy;
        StatusEffectSO fire =
            StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Fire);
        int previousStacks = enemy?.GetStatusStackCount(fire) ?? 0;
        bool applied = fire != null && tile.TryApplyStatusToTop(
            fire,
            duration,
            tickDamage,
            user,
            tickInterval,
            TryDamageTile);
        if (applied && enemy != null)
        {
            NotifyStatusApplied(new BattleStatusAppliedEvent(
                BattleStatusTarget.FromEnemy(enemy),
                fire,
                previousStacks,
                enemy.GetStatusStackCount(fire),
                user));
        }
        return applied;
    }

    public void TickStatusEffects(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        TickForcedPriorityTarget(deltaTime);
        foreach (DungeonBoardSlot tile in _tiles)
        {
            if (tile != null)
                tile.TickStatusEffects(deltaTime, TryDamageTile);
        }
    }

    public void TickEnemyAbilities(
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters)
    {
        if (deltaTime <= 0f)
            return;

        _battleCore.Tick(deltaTime);
        TickTimedEnemyGlobalModifiers(deltaTime);
        if (UsesWorldPresentation)
            TickWorldAllyMovement(deltaTime);
        TickEnemyCombatRuntimeStates(deltaTime, characters);
        if (_arenaSetup.UsesBattleCore)
            TickCircularEnemies(deltaTime);

        foreach (DungeonBoardSlot tile in _tiles)
        {
            EnemyRuntime enemy = tile != null ? tile.TopEnemy : null;
            if (enemy == null)
                continue;

            TickModularEnemyAbilities(
                tile,
                enemy,
                deltaTime,
                characters);
        }
    }

    private void TickEnemyCombatRuntimeStates(
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters)
    {
        _circularEnemySnapshot.Clear();
        foreach (EnemyRuntime enemy in _enemyPlacements.Keys)
            _circularEnemySnapshot.Add(enemy);

        foreach (EnemyRuntime enemy in _circularEnemySnapshot)
        {
            if (enemy == null || enemy.Health <= 0)
                continue;

            if (enemy.AreAllActionsDisabled && enemy.IsCharging)
            {
                EnemyChargeInterruptReason reason =
                    enemy.HasStatusEffectId(StatusEffectIds.Stun)
                        ? EnemyChargeInterruptReason.Stun
                        : EnemyChargeInterruptReason.OtherControl;
                if (enemy.TryInterruptCharge(
                        reason,
                        out EnemyActiveChargeRuntimeState interrupted))
                {
                    RecordEnemyAbilityActivation(
                        interrupted.AbilityState,
                        enemy,
                        true,
                        false);
                }
            }

            enemy.TickCombatRuntime(
                deltaTime,
                out EnemyActiveChargeRuntimeState completed);
            if (completed?.AbilityState != null &&
                !completed.IsCoreAttackCharge &&
                TryFindEnemyTile(enemy, out DungeonBoardSlot sourceTile))
            {
                BattleEffectResult result = ExecuteTriggeredAbilityNow(
                    sourceTile,
                    enemy,
                    null,
                    completed.AbilityState,
                    characters);
                RecordEnemyAbilityActivation(
                    completed.AbilityState,
                    enemy,
                    result.Attempted,
                    result.Succeeded);
            }

            ExecuteNoDamageAbilities(enemy, characters);
        }
        TickEnemyRadiusEntryAbilities(
            _circularEnemySnapshot,
            characters);
        _circularEnemySnapshot.Clear();
    }

    private void TickEnemyRadiusEntryAbilities(
        IReadOnlyList<EnemyRuntime> sources,
        IReadOnlyList<IBattleCharacter> characters)
    {
        _currentEnemyRadiusContacts.Clear();
        if (sources == null)
            return;

        foreach (EnemyRuntime source in sources)
        {
            if (source == null || source.Health <= 0 ||
                source.AreAllActionsDisabled ||
                !TryFindEnemyTile(source, out DungeonBoardSlot sourceTile))
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state?.Definition;
                if (ability == null || !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.OnAllyEnteredRadius))
                {
                    continue;
                }

                var abilityContact = (source, ability);
                bool initializing =
                    _initializedEnemyRadiusAbilities.Add(abilityContact);
                IReadOnlyList<EnemyRuntime> targets;
                if (ability.Target?.Faction ==
                        EnemyAbilityTargetFaction.EnemyAllies &&
                    ability.Target.Subject ==
                        EnemyAbilityTargetSubject.WorldRadius)
                {
                    targets = CollectWorldRadiusEnemyTargets(
                        source,
                        ability.Target);
                }
                else if (!TryResolveEnemyAbilityTargets(
                             sourceTile,
                             source,
                             ability.Target,
                             characters,
                             out CharacterTargetFaction faction,
                             out targets,
                             out _) ||
                         faction != CharacterTargetFaction.Enemy)
                {
                    continue;
                }

                bool canRespond = !initializing &&
                                  CanActivateEnemyAbility(source, state) &&
                                  !source.IsAbilityCharging(ability);
                bool hasNewContact = false;
                foreach (EnemyRuntime target in targets)
                {
                    if (target == null || target.Health <= 0 ||
                        ReferenceEquals(target, source))
                    {
                        continue;
                    }

                    var contact = (source, ability, target);
                    bool wasInRadius = _enemyRadiusContacts.Contains(contact);
                    if (initializing || wasInRadius || canRespond)
                        _currentEnemyRadiusContacts.Add(contact);
                    hasNewContact |= canRespond && !wasInRadius;
                }

                if (!hasNewContact)
                {
                    continue;
                }

                BattleEffectResult result = ExecuteTriggeredAbilityNow(
                    sourceTile,
                    source,
                    EnemyAbilityTrigger.OnAllyEnteredRadius,
                    state,
                    characters);
                RecordEnemyAbilityActivation(
                    state,
                    source,
                    result.Attempted,
                    result.Succeeded);
            }
        }

        _enemyRadiusContacts.Clear();
        foreach (var contact in _currentEnemyRadiusContacts)
            _enemyRadiusContacts.Add(contact);
    }

    private void TickCircularEnemies(float deltaTime)
    {
        if (!_battleCore.IsActive || _battleCore.IsDestroyed)
            return;

        float appliedDelta = Mathf.Max(0f, deltaTime);
        _spatialBattleElapsedTime += appliedDelta;
        RemoveExpiredCoreAttackHistory();
        RefreshFormationTargets();
        _circularEnemySnapshot.Clear();
        foreach (EnemyRuntime enemy in _enemyPlacements.Keys)
            _circularEnemySnapshot.Add(enemy);

        for (int index = 0;
             index < _circularEnemySnapshot.Count;
             index++)
        {
            if (_battleCore.IsDestroyed)
                break;

            EnemyRuntime enemy = _circularEnemySnapshot[index];
            if (enemy == null || enemy.Health <= 0 ||
                !_enemyPlacements.ContainsKey(enemy) ||
                !_circularEnemyStates.TryGetValue(
                    enemy,
                    out CircularEnemyState state) ||
                enemy.AreAllActionsDisabled)
            {
                continue;
            }

            float movementTargetRadius = ResolveFormationMovementTargetRadius(
                enemy,
                state);
            Vector2 targetPosition =
                state.SpawnDirection * movementTargetRadius;
            if ((state.ResolvedPosition - targetPosition).sqrMagnitude >
                FormationArrivalTolerance * FormationArrivalTolerance)
            {
                state.ResolvedPosition = Vector2.MoveTowards(
                    state.ResolvedPosition,
                    targetPosition,
                    ResolveFormationMovementSpeed(enemy) * appliedDelta);
            }
            else
            {
                state.ResolvedPosition = targetPosition;
            }
            UpdateFormationApproachProgress(state);

            float distanceFromDefenseLine = Mathf.Max(
                0f,
                state.CurrentRadius - state.DefenseLineRadius);
            bool withinCoreRange = distanceFromDefenseLine <=
                enemy.CoreAttackRange + FormationArrivalTolerance;
            if (!withinCoreRange)
            {
                if (state.WasWithinCoreRange)
                {
                    state.WasWithinCoreRange = false;
                    PublishEnemyCombatEvent(new EnemyCombatEvent(
                        EnemyCombatEventType.CoreRangeExited,
                        enemy,
                        worldPosition: state.ResolvedPosition));
                }
                continue;
            }

            if (!state.WasWithinCoreRange)
            {
                state.WasWithinCoreRange = true;
                PublishEnemyCombatEvent(new EnemyCombatEvent(
                    EnemyCombatEventType.CoreRangeEntered,
                    enemy,
                    worldPosition: state.ResolvedPosition));
                TryAdvanceEnemyPhaseOnCoreContact(
                    enemy,
                    state.ResolvedPosition);
                if (!state.HasContactedCore)
                {
                    state.HasContactedCore = true;
                    PublishEnemyCombatEvent(new EnemyCombatEvent(
                        EnemyCombatEventType.FirstCoreContact,
                        enemy,
                        worldPosition: state.ResolvedPosition));
                }
                PublishEnemyCombatEvent(new EnemyCombatEvent(
                    EnemyCombatEventType.CoreContact,
                    enemy,
                    worldPosition: state.ResolvedPosition));
            }

            state.AttackTimeRemaining -= appliedDelta;
            while (state.AttackTimeRemaining <= 0f &&
                   !_battleCore.IsDestroyed)
            {
                if (!enemy.HasReadyChargedCoreAttack)
                {
                    PublishEnemyCombatEvent(new EnemyCombatEvent(
                        EnemyCombatEventType.CoreAttackPreparing,
                        enemy,
                        worldPosition: state.ResolvedPosition));
                }
                if (enemy.IsCharging)
                    break;

                int requestedDamage =
                    enemy.ResolveCoreAttackDamageForHit();
                float protectionBypass =
                    enemy.ConsumeNextCoreProtectionBypass();
                int appliedDamage = _battleCore is
                    IBattleObjectiveModifierService modifierService
                        ? modifierService.TakeDamage(
                            requestedDamage,
                            protectionBypass)
                        : _battleCore.TakeDamage(requestedDamage);
                PublishEnemyCombatEvent(new EnemyCombatEvent(
                    EnemyCombatEventType.CoreAttackResolved,
                    enemy,
                    requestedDamage: requestedDamage,
                    appliedDamage: appliedDamage,
                    worldPosition: state.ResolvedPosition));
                if (appliedDamage > 0)
                {
                    _recentCoreAttackTimes[enemy] =
                        _spatialBattleElapsedTime;
                    PublishEnemyCombatEvent(new EnemyCombatEvent(
                        EnemyCombatEventType.CoreDamageApplied,
                        enemy,
                        requestedDamage: requestedDamage,
                        appliedDamage: appliedDamage,
                        worldPosition: state.ResolvedPosition));
                }
                float attackInterval = Mathf.Max(
                    0.1f,
                    enemy.CoreAttackInterval);
                state.AttackTimeRemaining += attackInterval;
            }
        }

        _circularEnemySnapshot.Clear();

        RefreshCircularLayout();
    }

    private void TickModularEnemyAbilities(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters)
    {
        if (source == null || source.IsCharging)
            return;

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            if (source.IsAbilityCharging(state.Definition))
                continue;
            if (!state.TickCooldown(
                    deltaTime,
                    source.AreAllActionsDisabled,
                    GetEnemyHealthPercentage(source)))
            {
                continue;
            }

            BattleEffectResult result = ExecuteCooldownAbility(
                sourceTile,
                source,
                state,
                characters,
                out bool charging);
            if (!charging)
                RecordEnemyAbilityActivation(
                    state,
                    source,
                    result.Attempted,
                    result.Succeeded);
        }
    }

    private BattleEffectResult ExecuteCooldownAbility(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        EnemyAbilityRuntimeState state,
        IReadOnlyList<IBattleCharacter> characters,
        out bool charging)
    {
        charging = false;
        EnemyAbilityDefinition ability = state?.Definition;
        if (ability == null ||
            !ability.RespondsToTrigger(EnemyAbilityTrigger.OnCooldown) ||
            !TryResolveEnemyAbilityTargets(
                sourceTile,
                source,
                ResolveEnemyAbilityTarget(ability),
                characters,
                out CharacterTargetFaction targetFaction,
                out IReadOnlyList<EnemyRuntime> enemyTargets,
                out IReadOnlyList<IBattleCharacter> playerTargets) ||
            !MatchesEnemyAbilityConditions(
                ability,
                source,
                enemyTargets,
                playerTargets))
        {
            return default;
        }

        if (ability.Charge.IsEnabled && ability.Charge.Duration > 0f &&
            source.TryBeginAbilityCharge(state, out _))
        {
            charging = true;
            return new BattleEffectResult(true, true);
        }

        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                this,
                targetFaction,
                enemyTargets,
                playerTargets);
        return ExecuteAbilityOperations(
            source,
            state,
            ability,
            context,
            enemyTargets);
    }

    private void ExecuteSpawnAbilities(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source)
    {
        if (source == null)
            return;

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!CanActivateEnemyAbility(source, state) ||
                !ability.RespondsToTrigger(EnemyAbilityTrigger.OnSpawn) ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    ResolveEnemyAbilityTarget(ability),
                    _battleCharacters,
                    out CharacterTargetFaction targetFaction,
                    out IReadOnlyList<EnemyRuntime> enemyTargets,
                    out IReadOnlyList<IBattleCharacter> playerTargets) ||
                !MatchesEnemyAbilityConditions(
                    ability,
                    source,
                    enemyTargets,
                    playerTargets))
            {
                continue;
            }

            BattleEffectContext context =
                BattleEffectContext.ForEnemyAbility(
                    source,
                    this,
                    targetFaction,
                    enemyTargets,
                    playerTargets);
            BattleEffectResult combined =
                ExecuteAbilityOperations(
                    source,
                    state,
                    ability,
                    context,
                    enemyTargets);
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation == null || !operation.Enabled ||
                    operation.Type !=
                        EnemyAbilityOperationType.GrantArmor)
                {
                    continue;
                }

                int armor = ResolveGrantedArmor(source, operation);
                if (operation.Count > 1)
                {
                    long totalArmor = (long)armor * operation.Count;
                    armor = totalArmor >= int.MaxValue
                        ? int.MaxValue
                        : (int)Math.Max(0L, totalArmor);
                }
                int granted = source.GainArmor(armor);
                combined = combined.Combine(
                    new BattleEffectResult(true, granted > 0));
            }

            RecordEnemyAbilityActivation(
                state,
                source,
                combined.Attempted,
                combined.Succeeded);
        }
    }

    private int ExecuteBeforeSelfDamageAbilities(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        int damage,
        CharacterAttackDamageType damageType,
        string damageSourceId)
    {
        if (source == null || source.AreAllActionsDisabled)
        {
            return damage;
        }

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!CanActivateEnemyAbility(source, state) ||
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.BeforeSelfDamage) ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    ResolveEnemyAbilityTarget(ability),
                    _battleCharacters,
                    out CharacterTargetFaction targetFaction,
                    out IReadOnlyList<EnemyRuntime> enemyTargets,
                    out IReadOnlyList<IBattleCharacter> playerTargets) ||
                !MatchesEnemyAbilityConditions(
                    ability,
                    source,
                    enemyTargets,
                    playerTargets,
                    damageType,
                    damageSourceId))
            {
                continue;
            }

            BattleEffectContext context =
                BattleEffectContext.ForEnemyAbility(
                    source,
                    this,
                    targetFaction,
                    enemyTargets,
                    playerTargets);
            BattleEffectResult combined =
                ExecuteAbilityOperations(
                    source,
                    state,
                    ability,
                    context,
                    enemyTargets);
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation == null || !operation.Enabled ||
                    operation.Type !=
                        EnemyAbilityOperationType.ModifyIncomingDamage ||
                    damageType == CharacterAttackDamageType.Fixed)
                {
                    continue;
                }

                damage = Mathf.Max(
                    0,
                    Mathf.RoundToInt(EvaluateOperationModifier(
                        damage,
                        operation)));
                combined = combined.Combine(
                    new BattleEffectResult(true, true));
            }

            RecordEnemyAbilityActivation(
                state,
                source,
                combined.Attempted,
                combined.Succeeded);
        }

        return damage;
    }

    private DungeonBoardSlot FindModularDamageRedirect(
        DungeonBoardSlot targetTile,
        CharacterAttackDamageType damageType,
        string damageSourceId)
    {
        EnemyRuntime target = targetTile?.TopEnemy;
        if (target == null)
            return null;

        foreach (DungeonBoardSlot sourceTile in _tiles)
        {
            EnemyRuntime source = sourceTile?.TopEnemy;
            if (source == null || ReferenceEquals(source, target) ||
                source.AreAllActionsDisabled)
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state.Definition;
                if (!CanActivateEnemyAbility(source, state) ||
                    !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.BeforeAllyDamage))
                {
                    continue;
                }

                IReadOnlyList<EnemyRuntime> enemyTargets =
                    new[] { target };
                if (!MatchesEnemyAbilityConditions(
                        ability,
                        source,
                        enemyTargets,
                        Array.Empty<IBattleCharacter>(),
                        damageType,
                        damageSourceId))
                {
                    continue;
                }

                EnemyAbilityOperationDefinition redirect = null;
                foreach (EnemyAbilityOperationDefinition operation in
                         ability.Operations)
                {
                    if (operation != null && operation.Enabled &&
                        operation.Type ==
                            EnemyAbilityOperationType.RedirectDamage &&
                        IsWithinAbilityRange(
                            sourceTile,
                            targetTile,
                            operation.Range,
                            operation.IncludeDiagonals))
                    {
                        redirect = operation;
                        break;
                    }
                }
                if (redirect == null)
                    continue;

                BattleEffectContext context =
                    BattleEffectContext.ForEnemyAbility(
                        source,
                        this,
                        CharacterTargetFaction.Enemy,
                        enemyTargets,
                        null);
                BattleEffectResult combined =
                    ExecuteAbilityOperations(
                        source,
                        state,
                        ability,
                        context,
                        enemyTargets).Combine(
                        new BattleEffectResult(true, true));
                RecordEnemyAbilityActivation(
                    state,
                    source,
                    combined.Attempted,
                    combined.Succeeded);
                return sourceTile;
            }
        }

        return null;
    }

    private void ExecuteDeathAbilities(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source)
    {
        if (source == null)
            return;

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!CanActivateEnemyAbility(source, state) ||
                !ability.RespondsToTrigger(EnemyAbilityTrigger.OnDeath) ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    ResolveEnemyAbilityTarget(ability),
                    _battleCharacters,
                    out CharacterTargetFaction targetFaction,
                    out IReadOnlyList<EnemyRuntime> enemyTargets,
                    out IReadOnlyList<IBattleCharacter> playerTargets) ||
                !MatchesEnemyAbilityConditions(
                    ability,
                    source,
                    enemyTargets,
                    playerTargets))
            {
                continue;
            }

            BattleEffectContext context =
                BattleEffectContext.ForEnemyAbility(
                    source,
                    this,
                    targetFaction,
                    enemyTargets,
                    playerTargets);
            BattleEffectResult combined =
                ExecuteAbilityOperations(
                    source,
                    state,
                    ability,
                    context,
                    enemyTargets);
            RecordEnemyAbilityActivation(
                state,
                source,
                combined.Attempted,
                combined.Succeeded);
        }
    }

    private void ExecuteHealthThresholdAbilities(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        int previousHealth,
        Vector2 worldPosition)
    {
        if (source == null || source.Health <= 0 ||
            source.AreAllActionsDisabled)
        {
            return;
        }

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!CanActivateEnemyAbility(source, state) ||
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.OnHealthThreshold) ||
                !source.TryMarkHealthThresholdCrossed(
                    ability,
                    previousHealth,
                    source.Health))
            {
                continue;
            }

            BattleEffectResult result = ExecuteTriggeredAbilityNow(
                sourceTile,
                source,
                EnemyAbilityTrigger.OnHealthThreshold,
                state,
                _battleCharacters);
            RecordEnemyAbilityActivation(
                state,
                source,
                result.Attempted,
                result.Succeeded);
            PublishEnemyCombatEvent(new EnemyCombatEvent(
                EnemyCombatEventType.HealthThresholdCrossed,
                source,
                ability: ability,
                previousHealth: previousHealth,
                currentHealth: source.Health,
                thresholdPercent: ability.HealthThresholdPercent,
                worldPosition: worldPosition));
        }
    }

    private void TryAdvanceEnemyPhaseForHealth(
        EnemyRuntime source,
        Vector2 worldPosition)
    {
        if (source == null || !source.TryAdvancePhaseForHealth(
                out _,
                out _))
        {
            return;
        }

        PublishEnemyCombatEvent(new EnemyCombatEvent(
            EnemyCombatEventType.PhaseChanged,
            source,
            currentHealth: source.Health,
            worldPosition: worldPosition));
    }

    private void TryAdvanceEnemyPhaseOnCoreContact(
        EnemyRuntime source,
        Vector2 worldPosition)
    {
        if (source == null || !source.TryAdvancePhaseOnCoreContact(
                out _,
                out _))
        {
            return;
        }

        PublishEnemyCombatEvent(new EnemyCombatEvent(
            EnemyCombatEventType.PhaseChanged,
            source,
            currentHealth: source.Health,
            worldPosition: worldPosition));
    }

    private void ExecuteNearbyEnemyDeathAbilities(
        EnemyRuntime defeated,
        Vector2 defeatedPosition,
        DungeonBoardSlot defeatedTile)
    {
        _circularEnemySnapshot.Clear();
        foreach (EnemyRuntime source in _enemyPlacements.Keys)
            _circularEnemySnapshot.Add(source);
        if (defeated != null &&
            !_circularEnemySnapshot.Contains(defeated))
        {
            _circularEnemySnapshot.Add(defeated);
        }

        foreach (EnemyRuntime source in _circularEnemySnapshot)
        {
            bool isDefeatedSource = ReferenceEquals(source, defeated);
            if (source == null || source.Health <= 0 && !isDefeatedSource ||
                source.AreAllActionsDisabled && !isDefeatedSource)
            {
                continue;
            }

            DungeonBoardSlot sourceTile;
            Vector2 sourcePosition;
            if (isDefeatedSource)
            {
                sourceTile = defeatedTile;
                sourcePosition = defeatedPosition;
            }
            else if (!TryFindEnemyTile(
                         source,
                         out sourceTile) ||
                     !TryGetUnitPosition(
                         BattleStatusTarget.FromEnemy(source),
                         out sourcePosition))
            {
                continue;
            }

            foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
            {
                EnemyAbilityDefinition ability = state.Definition;
                if (!CanActivateEnemyAbility(source, state) ||
                    !ability.RespondsToTrigger(
                        EnemyAbilityTrigger.OnNearbyEnemyDeath))
                {
                    continue;
                }

                IReadOnlyList<EnemyRuntime> linkedSurvivors = null;
                bool requiresOwnedLink = TryGetAbilityBooleanParameter(
                        ability,
                        "linkedOnly",
                        out bool linkedOnly) && linkedOnly;
                if (isDefeatedSource && !requiresOwnedLink)
                    continue;
                if (requiresOwnedLink && !TryGetLinkedSurvivors(
                        source,
                        defeated,
                        ability.Target?.IncludeSource == true,
                        ability.Target?.TargetCount ?? int.MaxValue,
                        out linkedSurvivors))
                {
                    continue;
                }
                if (TryGetAbilityTextParameter(
                        ability,
                        "requiredEnemyTier",
                        out string requiredTier) &&
                    (!Enum.TryParse(
                         requiredTier,
                         true,
                         out EnemyRosterTier rosterTier) ||
                     defeated.Definition.RosterTier != rosterTier))
                {
                    continue;
                }

                float radius = ResolveNearbyDeathRadius(ability);
                if ((sourcePosition - defeatedPosition).sqrMagnitude >
                    radius * radius)
                {
                    continue;
                }

                BattleEffectResult result;
                if (linkedSurvivors != null)
                {
                    if (!MatchesEnemyAbilityConditions(
                            ability,
                            source,
                            linkedSurvivors,
                            Array.Empty<IBattleCharacter>()))
                    {
                        continue;
                    }
                    BattleEffectContext context =
                        BattleEffectContext.ForEnemyAbility(
                            source,
                            this,
                            CharacterTargetFaction.Enemy,
                            linkedSurvivors,
                            Array.Empty<IBattleCharacter>());
                    result = ExecuteAbilityOperations(
                        source,
                        state,
                        ability,
                        context,
                        linkedSurvivors);
                }
                else
                {
                    result = ExecuteTriggeredAbilityNow(
                        sourceTile,
                        source,
                        EnemyAbilityTrigger.OnNearbyEnemyDeath,
                        state,
                        _battleCharacters);
                }
                RecordEnemyAbilityActivation(
                    state,
                    source,
                    result.Attempted,
                    result.Succeeded);
                PublishEnemyCombatEvent(new EnemyCombatEvent(
                    EnemyCombatEventType.NearbyEnemyDefeated,
                    source,
                    relatedEnemy: defeated,
                    ability: ability,
                    worldPosition: defeatedPosition));
            }
        }
        _circularEnemySnapshot.Clear();
    }

    private bool TryGetLinkedSurvivors(
        EnemyRuntime source,
        EnemyRuntime defeated,
        bool includeSource,
        int targetLimit,
        out IReadOnlyList<EnemyRuntime> survivors)
    {
        List<EnemyRuntime> result = new();
        for (int groupIndex = 0;
             groupIndex < _enemyDamageLinks.Count;
             groupIndex++)
        {
            EnemyDamageLinkGroup group = _enemyDamageLinks[groupIndex];
            if (group == null ||
                !ReferenceEquals(group.Owner, source) ||
                !group.Members.Contains(source) ||
                !group.Members.Contains(defeated))
            {
                continue;
            }

            for (int memberIndex = 0;
                 memberIndex < group.Members.Count;
                 memberIndex++)
            {
                EnemyRuntime member = group.Members[memberIndex];
                if (member == null || member.Health <= 0 ||
                    ReferenceEquals(member, defeated) ||
                    (!includeSource && ReferenceEquals(member, source)) ||
                    result.Contains(member))
                {
                    continue;
                }
                result.Add(member);
                if (targetLimit > 0 && result.Count >= targetLimit)
                    break;
            }
            break;
        }

        survivors = result.Count > 0
            ? result
            : Array.Empty<EnemyRuntime>();
        return result.Count > 0;
    }

    private static float ResolveNearbyDeathRadius(
        EnemyAbilityDefinition ability)
    {
        float radius = ability?.Target?.WorldRadius ?? 0f;
        if (ability?.Operations != null)
        {
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation != null)
                    radius = Mathf.Max(radius, operation.WorldRadius);
            }
        }

        return radius > 0f
            ? radius
            : BattleSpatialDefaults.NearbyRadius;
    }

    private BattleEffectResult ExecuteAbilityOperations(
        EnemyRuntime source,
        EnemyAbilityRuntimeState abilityState,
        EnemyAbilityDefinition ability,
        BattleEffectContext context,
        IReadOnlyList<EnemyRuntime> enemyTargets)
    {
        BattleEffectResult combined = default;
        foreach (EnemyAbilityOperationDefinition operation in
                 ability.Operations)
        {
            if (operation == null || !operation.Enabled)
            {
                continue;
            }

            if (operation.Type == EnemyAbilityOperationType.ExecuteEffects)
            {
                combined = combined.Combine(
                    BattleEffectExecutor.ExecuteSequence(
                        context,
                        operation.Effects));
                continue;
            }

            combined = combined.Combine(
                ExecuteEnemyRuntimeOperation(
                    source,
                    abilityState,
                    ability,
                    operation,
                    enemyTargets));
        }

        return combined;
    }

    private BattleEffectResult ExecuteEnemyRuntimeOperation(
        EnemyRuntime source,
        EnemyAbilityRuntimeState abilityState,
        EnemyAbilityDefinition ability,
        EnemyAbilityOperationDefinition operation,
        IReadOnlyList<EnemyRuntime> enemyTargets)
    {
        if (source == null || ability == null || operation == null)
            return default;

        if (operation.Type == EnemyAbilityOperationType.ChargeCoreAttack)
        {
            if (ability.Charge.IsEnabled && ability.Charge.Duration > 0f)
            {
                EnemyCombatModifier chargedAttack = new(
                    source.ResolveModifierSourceId(ability, operation),
                    EnemyCombatModifierType.CoreAttackDamage,
                    operation.Amount,
                    operation.Percentage,
                    operation.Multiplier,
                    0f,
                    1,
                    EnemyStatusModifierScope.All);
                bool reserved =
                    source.ReserveReadyChargedCoreAttackModifier(
                        chargedAttack);
                return new BattleEffectResult(true, reserved);
            }

            bool started = source.TryBeginCoreAttackCharge(
                abilityState,
                operation,
                out _);
            return new BattleEffectResult(true, started);
        }

        if (operation.Type == EnemyAbilityOperationType.ApplyCoreEffect)
        {
            if (_currentEnemyCombatEvent.Type ==
                    EnemyCombatEventType.CoreDamageApplied &&
                ReferenceEquals(_currentEnemyCombatEvent.Source, source))
            {
                if (operation.Amount > 0 && operation.Interval > 0f &&
                    operation.Duration >= operation.Interval &&
                    _battleCore is
                        IBattleObjectiveModifierService objectiveEffects)
                {
                    bool applied = objectiveEffects.TryApplyDamageOverTime(
                        source.ResolveModifierSourceId(ability, operation),
                        operation.Amount,
                        operation.Interval,
                        operation.Duration,
                        Mathf.Max(1, operation.MaximumStacks));
                    return new BattleEffectResult(true, applied);
                }

                int healing = Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        _currentEnemyCombatEvent.AppliedDamage *
                        Mathf.Max(0f, operation.Percentage)));
                int healed = source.Heal(healing);
                return new BattleEffectResult(true, healed > 0);
            }

            bool reserved = source.ReserveNextCoreProtectionBypass(
                operation.Percentage);
            return new BattleEffectResult(true, reserved);
        }
        if (operation.Type == EnemyAbilityOperationType.SummonEnemy)
        {
            if (operation.Duration > 0f)
            {
                bool scheduled =
                    _enemySummonService?.TryScheduleSummon(
                        source,
                        ability.AbilityId,
                        operation.Summon,
                        operation.Duration) == true;
                return new BattleEffectResult(true, scheduled);
            }

            int summoned = _enemySummonService?.TrySummonEnemies(
                source,
                ability.AbilityId,
                operation.Summon) ?? 0;
            return new BattleEffectResult(true, summoned > 0);
        }

        if (operation.Type ==
            EnemyAbilityOperationType.ModifySpawnInterval)
        {
            bool applied =
                _enemySummonService?.TryAddSpawnIntervalModifier(
                    source.ResolveModifierSourceId(ability, operation),
                    operation.Multiplier,
                    operation.Duration) == true;
            return new BattleEffectResult(true, applied);
        }
        if (operation.Type ==
            EnemyAbilityOperationType.ConvertCoreDamageToSelfShield)
        {
            if (_currentEnemyCombatEvent.Type !=
                    EnemyCombatEventType.CoreDamageApplied ||
                !ReferenceEquals(_currentEnemyCombatEvent.Source, source) ||
                _currentEnemyCombatEvent.AppliedDamage <= 0)
            {
                return default;
            }

            float ratio = Mathf.Max(0f, operation.Percentage);
            int requestedShield = ratio > 0f
                ? Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        _currentEnemyCombatEvent.AppliedDamage * ratio))
                : Mathf.Max(0, operation.Amount);
            int granted = source.GainShield(requestedShield);
            return new BattleEffectResult(true, granted > 0);
        }

        if (operation.Type ==
            EnemyAbilityOperationType.ModifyCoreAttackDamage &&
            _currentEnemyCombatEvent.Type ==
                EnemyCombatEventType.FirstCoreContact &&
            ReferenceEquals(_currentEnemyCombatEvent.Source, source))
        {
            EnemyCombatModifier nextAttack = new(
                source.ResolveModifierSourceId(ability, operation),
                EnemyCombatModifierType.CoreAttackDamage,
                operation.Amount,
                operation.Percentage,
                operation.Multiplier,
                0f,
                1,
                EnemyStatusModifierScope.All);
            bool reserved = source.ReserveNextCoreAttackModifier(nextAttack);
            return new BattleEffectResult(true, reserved);
        }

        if (operation.Type ==
                EnemyAbilityOperationType.ModifyCoreAttackDamage &&
            TryGetAbilityIntegerParameter(
                ability,
                "stackDelta",
                out int stackDelta) &&
            stackDelta < 0)
        {
            int removed = source.RemoveCombatModifierStacks(
                EnemyCombatModifierType.CoreAttackDamage,
                -stackDelta);
            return new BattleEffectResult(true, removed > 0);
        }

        if (operation.Type == EnemyAbilityOperationType.ModifyCoreRecovery &&
            _battleCore is IBattleObjectiveModifierService recoveryService)
        {
            bool applied = recoveryService.TryAddTimedModifier(
                source.ResolveModifierSourceId(ability, operation),
                BattleObjectiveModifierType.HealingReceivedMultiplier,
                operation.Multiplier,
                operation.Duration,
                Mathf.Max(1, operation.MaximumStacks));
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type ==
                EnemyAbilityOperationType.ModifyCoreMaximumHealth &&
            _battleCore is IBattleObjectiveModifierService maximumService)
        {
            float reduction = Mathf.Abs(operation.Percentage);
            bool applied = maximumService.TryAddTimedModifier(
                source.ResolveModifierSourceId(ability, operation),
                BattleObjectiveModifierType.MaximumHealthReduction,
                reduction,
                operation.Duration,
                Mathf.Max(1, operation.MaximumStacks));
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type == EnemyAbilityOperationType.CreateWorldZone &&
            _battleCore is IBattleObjectiveModifierService zoneService)
        {
            // Until enemy-authored spatial zones have their own runtime,
            // multiple authored zone markers collapse into one global effect.
            // Stacking here would incorrectly square the multiplier merely
            // because the presentation requested more than one marker.
            bool applied = zoneService.TryAddTimedModifier(
                source.ResolveModifierSourceId(ability, operation),
                BattleObjectiveModifierType.HealingReceivedMultiplier,
                operation.Multiplier,
                operation.Duration,
                1);
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type ==
            EnemyAbilityOperationType.ModifyResourceRecovery)
        {
            bool applied = TryAddTimedResourceRecoveryModifier(
                source.ResolveModifierSourceId(ability, operation),
                operation.Multiplier,
                operation.Duration);
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type == EnemyAbilityOperationType.ModifyCardCost)
        {
            bool applied = _cardControlService?.TryAddTimedCostModifier(
                BattleCardCostModifierMode.Add,
                operation.Amount,
                operation.Duration) == true;
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type == EnemyAbilityOperationType.LockCard)
        {
            bool applied = _cardControlService?.TryLockRandomHandCard(
                operation.Duration) == true;
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type == EnemyAbilityOperationType.SetUntargetable)
        {
            bool applied = source.TrySetUntargetable(operation.Duration);
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type == EnemyAbilityOperationType.ReflectDamage)
        {
            bool applied = source.TryReserveDamageReflection(
                operation.Percentage,
                operation.Duration);
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type == EnemyAbilityOperationType.LinkTargets)
        {
            string sourceId = source.ResolveModifierSourceId(
                ability,
                operation);
            for (int index = _enemyDamageLinks.Count - 1;
                 index >= 0;
                 index--)
            {
                EnemyDamageLinkGroup existing = _enemyDamageLinks[index];
                if (existing != null &&
                    ReferenceEquals(existing.Owner, source) &&
                    string.Equals(
                        existing.SourceId,
                        sourceId,
                        StringComparison.Ordinal))
                {
                    _enemyDamageLinks.RemoveAt(index);
                }
            }

            EnemyDamageLinkGroup group = new(
                source,
                sourceId,
                operation.Percentage,
                operation.Duration);
            group.Members.Add(source);
            if (enemyTargets != null)
            {
                foreach (EnemyRuntime target in enemyTargets)
                {
                    if (target != null && target.Health > 0 &&
                        !group.Members.Contains(target))
                    {
                        group.Members.Add(target);
                    }
                }
            }

            if (group.ShareRatio <= 0f || group.Members.Count < 2)
                return new BattleEffectResult(true, false);
            _enemyDamageLinks.Add(group);
            return new BattleEffectResult(true, true);
        }

        if (operation.Type ==
                EnemyAbilityOperationType.ModifyTargetPriority &&
            operation.TargetPriorityMode ==
                EnemyTargetPriorityMode.ForceFocus &&
            operation.Duration > 0f)
        {
            bool applied = TryForcePriorityTarget(
                source,
                operation.Duration);
            return new BattleEffectResult(true, applied);
        }

        if (operation.Type == EnemyAbilityOperationType.GrantArmor &&
            !ability.RespondsToTrigger(EnemyAbilityTrigger.OnSpawn))
        {
            bool armorAttempted = false;
            bool armorSucceeded = false;
            IReadOnlyList<EnemyRuntime> targets = enemyTargets != null &&
                                                  enemyTargets.Count > 0
                ? enemyTargets
                : new[] { source };
            foreach (EnemyRuntime target in targets)
            {
                if (target == null || target.Health <= 0)
                    continue;
                armorAttempted = true;
                int armor = ResolveGrantedArmor(target, operation);
                if (operation.Count > 1)
                {
                    long totalArmor = (long)armor * operation.Count;
                    armor = totalArmor >= int.MaxValue
                        ? int.MaxValue
                        : (int)Math.Max(0L, totalArmor);
                }
                armorSucceeded |= target.GainArmor(armor) > 0;
            }
            return new BattleEffectResult(
                armorAttempted,
                armorSucceeded);
        }

        EnemyCombatModifierType modifierType;
        switch (operation.Type)
        {
            case EnemyAbilityOperationType.ModifyCoreAttackDamage:
                modifierType = EnemyCombatModifierType.CoreAttackDamage;
                break;

            case EnemyAbilityOperationType.ModifyCoreAttackInterval:
                modifierType = EnemyCombatModifierType.CoreAttackInterval;
                break;

            case EnemyAbilityOperationType.ModifyStatusDuration:
                modifierType = EnemyCombatModifierType.StatusDuration;
                break;

            case EnemyAbilityOperationType.GrantStatusImmunity:
                modifierType = EnemyCombatModifierType.StatusImmunity;
                break;

            case EnemyAbilityOperationType.ModifyIncomingDamage
                when !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.BeforeSelfDamage):
                modifierType = EnemyCombatModifierType.IncomingDamage;
                break;

            default:
                return default;
        }

        EnemyStatusModifierScope statusScope =
            ResolveStatusModifierScope(ability);
        EnemyCombatModifier modifier = new(
            source.ResolveModifierSourceId(ability, operation),
            modifierType,
            operation.Amount,
            operation.Percentage,
            operation.Multiplier,
            operation.Duration,
            operation.MaximumStacks > 0
                ? operation.MaximumStacks
                : 1,
            statusScope);
        bool attempted = false;
        bool succeeded = false;
        if (enemyTargets != null)
        {
            foreach (EnemyRuntime target in enemyTargets)
            {
                if (target == null || target.Health <= 0)
                    continue;
                attempted = true;
                succeeded |= target.ApplyCombatModifier(modifier);
            }
        }
        if (!attempted)
        {
            attempted = true;
            succeeded = source.ApplyCombatModifier(modifier);
        }

        return new BattleEffectResult(attempted, succeeded);
    }

    private static EnemyStatusModifierScope ResolveStatusModifierScope(
        EnemyAbilityDefinition ability)
    {
        if (ability?.Parameters != null)
        {
            foreach (EnemyAbilityParameterDefinition parameter in
                     ability.Parameters)
            {
                if (parameter == null ||
                    (!string.Equals(
                         parameter.Key,
                         "scope",
                         StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(
                         parameter.Key,
                         "statusScope",
                         StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string value = parameter.TextValue;
                if (value.IndexOf(
                        "control",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return EnemyStatusModifierScope.Controls;
                }
                if (value.IndexOf(
                        "debuff",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return EnemyStatusModifierScope.Debuffs;
                }
            }
        }

        return ability != null &&
               ability.AbilityTypeId.IndexOf(
                   "control",
                   StringComparison.OrdinalIgnoreCase) >= 0
            ? EnemyStatusModifierScope.Controls
            : EnemyStatusModifierScope.All;
    }

    private static bool TryGetAbilityIntegerParameter(
        EnemyAbilityDefinition ability,
        string key,
        out int value)
    {
        value = 0;
        if (ability?.Parameters == null || string.IsNullOrWhiteSpace(key))
            return false;

        foreach (EnemyAbilityParameterDefinition parameter in
                 ability.Parameters)
        {
            if (parameter != null && string.Equals(
                    parameter.Key,
                    key,
                    StringComparison.OrdinalIgnoreCase) &&
                parameter.ValueType ==
                    EnemyAbilityParameterValueType.Integer)
            {
                value = parameter.IntValue;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetAbilityBooleanParameter(
        EnemyAbilityDefinition ability,
        string key,
        out bool value)
    {
        value = false;
        if (ability?.Parameters == null || string.IsNullOrWhiteSpace(key))
            return false;
        foreach (EnemyAbilityParameterDefinition parameter in
                 ability.Parameters)
        {
            if (parameter != null && string.Equals(
                    parameter.Key,
                    key,
                    StringComparison.OrdinalIgnoreCase) &&
                parameter.ValueType ==
                    EnemyAbilityParameterValueType.Boolean)
            {
                value = parameter.BoolValue;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetAbilityTextParameter(
        EnemyAbilityDefinition ability,
        string key,
        out string value)
    {
        value = string.Empty;
        if (ability?.Parameters == null || string.IsNullOrWhiteSpace(key))
            return false;
        foreach (EnemyAbilityParameterDefinition parameter in
                 ability.Parameters)
        {
            if (parameter != null && string.Equals(
                    parameter.Key,
                    key,
                    StringComparison.OrdinalIgnoreCase) &&
                parameter.ValueType ==
                    EnemyAbilityParameterValueType.Text)
            {
                value = parameter.TextValue;
                return !string.IsNullOrWhiteSpace(value);
            }
        }
        return false;
    }

    private BattleEffectResult ExecuteTriggeredAbilityNow(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        EnemyAbilityTrigger? trigger,
        EnemyAbilityRuntimeState state,
        IReadOnlyList<IBattleCharacter> characters)
    {
        EnemyAbilityDefinition ability = state?.Definition;
        if (ability == null ||
            (trigger.HasValue &&
             !ability.RespondsToTrigger(trigger.Value)) ||
            !TryResolveEnemyAbilityTargets(
                sourceTile,
                source,
                ResolveEnemyAbilityTarget(ability),
                characters,
                out CharacterTargetFaction targetFaction,
                out IReadOnlyList<EnemyRuntime> enemyTargets,
                out IReadOnlyList<IBattleCharacter> playerTargets) ||
            !MatchesEnemyAbilityConditions(
                ability,
                source,
                enemyTargets,
                playerTargets))
        {
            return default;
        }

        BattleEffectContext context = BattleEffectContext.ForEnemyAbility(
            source,
            this,
            targetFaction,
            enemyTargets,
            playerTargets);
        return ExecuteAbilityOperations(
            source,
            state,
            ability,
            context,
            enemyTargets);
    }

    private void ExecuteTriggeredAbilities(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        EnemyAbilityTrigger trigger,
        IReadOnlyList<IBattleCharacter> characters)
    {
        if (source == null || source.AreAllActionsDisabled)
            return;

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!CanActivateEnemyAbility(source, state) ||
                !ability.RespondsToTrigger(trigger) ||
                source.IsAbilityCharging(ability))
            {
                continue;
            }

            if (ability.Charge.IsEnabled && ability.Charge.Duration > 0f)
            {
                if (source.IsCharging)
                    continue;
                if (CanBeginTriggeredCharge(
                        sourceTile,
                        source,
                        ability,
                        characters) &&
                    source.TryBeginAbilityCharge(state, out _))
                {
                    continue;
                }
            }

            BattleEffectResult result = ExecuteTriggeredAbilityNow(
                sourceTile,
                source,
                trigger,
                state,
                characters);
            RecordEnemyAbilityActivation(
                state,
                source,
                result.Attempted,
                result.Succeeded);
        }
    }

    private bool CanBeginTriggeredCharge(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        EnemyAbilityDefinition ability,
        IReadOnlyList<IBattleCharacter> characters)
    {
        return TryResolveEnemyAbilityTargets(
                   sourceTile,
                   source,
                   ResolveEnemyAbilityTarget(ability),
                   characters,
                   out _,
                   out IReadOnlyList<EnemyRuntime> enemyTargets,
                   out IReadOnlyList<IBattleCharacter> playerTargets) &&
               MatchesEnemyAbilityConditions(
                   ability,
                   source,
                   enemyTargets,
                   playerTargets);
    }

    private void ExecuteNoDamageAbilities(
        EnemyRuntime source,
        IReadOnlyList<IBattleCharacter> characters)
    {
        if (source == null || source.AreAllActionsDisabled ||
            !TryFindEnemyTile(source, out DungeonBoardSlot sourceTile))
        {
            return;
        }

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!CanActivateEnemyAbility(source, state) ||
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.AfterNoDamage) ||
                !source.TryMarkNoDamageDurationReached(ability))
            {
                continue;
            }

            BattleEffectResult result = ExecuteTriggeredAbilityNow(
                sourceTile,
                source,
                EnemyAbilityTrigger.AfterNoDamage,
                state,
                characters);
            RecordEnemyAbilityActivation(
                state,
                source,
                result.Attempted,
                result.Succeeded);
            PublishEnemyCombatEvent(new EnemyCombatEvent(
                EnemyCombatEventType.NoDamageDurationReached,
                source,
                ability: ability,
                elapsedTime: source.TimeSinceLastDamage));
        }
    }

    private static int ResolveGrantedArmor(
        EnemyRuntime source,
        EnemyAbilityOperationDefinition operation)
    {
        double amount = operation.Amount +
                        source.MaxHealth * (double)operation.Multiplier;
        if (double.IsNaN(amount) || amount <= 0d)
            return 0;
        if (double.IsInfinity(amount) || amount >= int.MaxValue)
            return int.MaxValue;
        return Mathf.Max(0, Mathf.RoundToInt((float)amount));
    }

    private static float GetEnemyHealthPercentage(EnemyRuntime enemy)
    {
        return enemy != null && enemy.MaxHealth > 0
            ? enemy.Health * 100f / enemy.MaxHealth
            : 0f;
    }

    private static bool CanActivateEnemyAbility(
        EnemyRuntime source,
        EnemyAbilityRuntimeState state)
    {
        return source != null && state?.Definition != null &&
               state.CanActivate &&
               source.IsAbilityEnabledInCurrentPhase(state.Definition);
    }

    private static void RecordEnemyAbilityActivation(
        EnemyAbilityRuntimeState state,
        EnemyRuntime source,
        bool attempted,
        bool succeeded)
    {
        state?.RecordActivation(
            attempted,
            succeeded,
            GetEnemyHealthPercentage(source));
    }

    private bool IsWithinAbilityRange(
        DungeonBoardSlot source,
        DungeonBoardSlot target,
        int range,
        bool includeDiagonals)
    {
        if (source == null || target == null)
            return false;

        range = Mathf.Max(1, range);
        EnemyRuntime sourceEnemy = GetEnemyAtTile(source);
        EnemyRuntime targetEnemy = GetEnemyAtTile(target);
        IReadOnlyList<DungeonBoardSlot> sourceTiles =
            GetOccupiedTiles(sourceEnemy, source);
        IReadOnlyList<DungeonBoardSlot> targetTiles =
            GetOccupiedTiles(targetEnemy, target);

        foreach (DungeonBoardSlot sourceTile in sourceTiles)
        {
            foreach (DungeonBoardSlot targetTile in targetTiles)
            {
                int rowDistance = Mathf.Abs(
                    sourceTile.Row - targetTile.Row);
                int columnDistance = Mathf.Abs(
                    sourceTile.Column - targetTile.Column);
                if (includeDiagonals
                        ? Mathf.Max(rowDistance, columnDistance) <= range
                        : rowDistance + columnDistance <= range)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IReadOnlyList<DungeonBoardSlot> GetOccupiedTiles(
        EnemyRuntime enemy,
        DungeonBoardSlot fallback)
    {
        if (enemy != null &&
            _enemyPlacements.TryGetValue(
                enemy,
                out EnemyPlacement placement))
        {
            return placement.OccupiedTiles;
        }

        return fallback != null
            ? new[] { fallback }
            : Array.Empty<DungeonBoardSlot>();
    }

    private bool TryResolveEnemyAbilityTargets(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        EnemyAbilityTargetDefinition target,
        IReadOnlyList<IBattleCharacter> characters,
        out CharacterTargetFaction targetFaction,
        out IReadOnlyList<EnemyRuntime> enemyTargets,
        out IReadOnlyList<IBattleCharacter> playerTargets)
    {
        targetFaction = CharacterTargetFaction.Enemy;
        enemyTargets = Array.Empty<EnemyRuntime>();
        playerTargets = Array.Empty<IBattleCharacter>();
        if (target == null || !target.HasTarget)
            return true;

        if (target.Faction == EnemyAbilityTargetFaction.Self ||
            (target.Faction ==
                 EnemyAbilityTargetFaction.EnemyAllies &&
             target.Subject == EnemyAbilityTargetSubject.Self))
        {
            enemyTargets = source != null
                ? new[] { source }
                : Array.Empty<EnemyRuntime>();
            return enemyTargets.Count > 0;
        }

        if (target.Faction ==
            EnemyAbilityTargetFaction.PlayerCharacters)
        {
            if (target.Subject == EnemyAbilityTargetSubject.Self ||
                target.Subject == EnemyAbilityTargetSubject.Adjacent)
            {
                return false;
            }

            targetFaction = CharacterTargetFaction.Ally;
            List<IBattleCharacter> candidates =
                target.Subject == EnemyAbilityTargetSubject.WorldRadius
                    ? CollectWorldRadiusPlayerTargets(
                        source,
                        characters,
                        target.WorldRadius)
                    : new List<IBattleCharacter>();
            if (target.Subject != EnemyAbilityTargetSubject.WorldRadius &&
                characters != null)
            {
                foreach (IBattleCharacter character in characters)
                {
                    if (character != null &&
                        character.CurrentHealth > 0)
                    {
                        candidates.Add(character);
                    }
                }
            }

            SelectEnemyAbilityTargets(
                candidates,
                target.Subject,
                target.Metric,
                target.TargetCount,
                GetPlayerAbilityTargetMetric,
                GetPlayerTargetPriority);
            playerTargets = candidates;
            return candidates.Count > 0;
        }

        if (target.Faction !=
            EnemyAbilityTargetFaction.EnemyAllies)
        {
            return false;
        }

        List<EnemyRuntime> enemyCandidates =
            target.Subject switch
            {
                EnemyAbilityTargetSubject.Adjacent =>
                    CollectAdjacentEnemyTargets(
                        sourceTile,
                        target.Range,
                        target.IncludeDiagonals),
                EnemyAbilityTargetSubject.WorldRadius =>
                    CollectWorldRadiusEnemyTargets(source, target),
                _ => CollectEnemyAbilityTargets(source),
            };
        SelectEnemyAbilityTargets(
            enemyCandidates,
            target.Subject,
            target.Metric,
            target.TargetCount,
            GetEnemyAbilityTargetMetric,
            GetEnemyAllyTargetPriority);
        enemyTargets = enemyCandidates;
        return enemyCandidates.Count > 0;
    }

    private static EnemyAbilityTargetDefinition ResolveEnemyAbilityTarget(
        EnemyAbilityDefinition ability)
    {
        if (ability == null)
            return null;

        return ability.Target?.HasTarget == true ||
               BattleAbilityRules.RequiresActionTargets(ability)
            ? ability.Target
            : null;
    }

    private List<EnemyRuntime> CollectEnemyAbilityTargets(
        EnemyRuntime source)
    {
        List<EnemyRuntime> result = new();
        foreach (DungeonBoardSlot tile in _tiles)
        {
            EnemyRuntime candidate = tile?.TopEnemy;
            if (candidate != null &&
                !ReferenceEquals(candidate, source) &&
                candidate.Health > 0)
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private List<EnemyRuntime> CollectAdjacentEnemyTargets(
        DungeonBoardSlot sourceTile,
        int range,
        bool includeDiagonals)
    {
        List<EnemyRuntime> result = new();
        if (sourceTile == null)
            return result;

        EnemyRuntime source = GetEnemyAtTile(sourceTile);
        HashSet<EnemyRuntime> unique = new();
        foreach (DungeonBoardSlot candidateTile in _tiles)
        {
            EnemyRuntime candidate = candidateTile?.TopEnemy;
            if (candidate == null ||
                ReferenceEquals(candidate, source) ||
                candidate.Health <= 0 ||
                !unique.Add(candidate))
            {
                continue;
            }

            if (IsWithinAbilityRange(
                    sourceTile,
                    candidateTile,
                    range,
                    includeDiagonals))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private List<EnemyRuntime> CollectWorldRadiusEnemyTargets(
        EnemyRuntime source,
        EnemyAbilityTargetDefinition target)
    {
        List<EnemyRuntime> result = new();
        if (source == null || target == null || target.WorldRadius <= 0f ||
            !TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(source),
                out Vector2 sourcePosition))
        {
            return result;
        }

        IReadOnlyList<EnemyRuntime> nearby = SelectNearbyEnemies(
            BattleStatusTarget.FromEnemy(source),
            target.WorldRadius,
            0,
            target.IncludeSource);
        foreach (EnemyRuntime candidate in nearby)
        {
            if (candidate != null &&
                IsWithinWorldLayerScope(
                    source,
                    candidate,
                    target.LayerScope))
            {
                result.Add(candidate);
            }
        }

        SortEnemiesByDistance(result, sourcePosition);
        return result;
    }

    private List<IBattleCharacter> CollectWorldRadiusPlayerTargets(
        EnemyRuntime source,
        IReadOnlyList<IBattleCharacter> characters,
        float radius)
    {
        List<IBattleCharacter> result = new();
        if (source == null || characters == null || radius <= 0f ||
            !TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(source),
                out Vector2 sourcePosition))
        {
            return result;
        }

        float radiusSquared = radius * radius;
        foreach (IBattleCharacter character in characters)
        {
            if (character == null || character.CurrentHealth <= 0 ||
                !TryGetUnitPosition(
                    BattleStatusTarget.FromAlly(character),
                    out Vector2 position) ||
                (position - sourcePosition).sqrMagnitude > radiusSquared)
            {
                continue;
            }

            result.Add(character);
        }

        result.Sort((left, right) =>
        {
            TryGetUnitPosition(
                BattleStatusTarget.FromAlly(left),
                out Vector2 leftPosition);
            TryGetUnitPosition(
                BattleStatusTarget.FromAlly(right),
                out Vector2 rightPosition);
            return (leftPosition - sourcePosition).sqrMagnitude.CompareTo(
                (rightPosition - sourcePosition).sqrMagnitude);
        });
        return result;
    }

    private bool IsWithinWorldLayerScope(
        EnemyRuntime source,
        EnemyRuntime target,
        EnemyWorldLayerScope layerScope)
    {
        if (layerScope == EnemyWorldLayerScope.All ||
            !_circularEnemyStates.TryGetValue(
                source,
                out CircularEnemyState sourceState) ||
            !_circularEnemyStates.TryGetValue(
                target,
                out CircularEnemyState targetState))
        {
            return true;
        }

        int difference = Mathf.Abs(
            sourceState.LayerIndex - targetState.LayerIndex);
        return layerScope == EnemyWorldLayerScope.Same
            ? difference == 0
            : difference <= 1;
    }

    private static void SelectEnemyAbilityTargets<T>(
        List<T> candidates,
        EnemyAbilityTargetSubject subject,
        EnemyAbilityTargetMetric metric,
        int targetCount,
        Func<T, EnemyAbilityTargetMetric, float> getMetric,
        Func<T, StatusEffectTargetPriority> getPriority)
    {
        if (candidates == null || candidates.Count == 0 ||
            subject == EnemyAbilityTargetSubject.All ||
            subject == EnemyAbilityTargetSubject.Adjacent)
        {
            return;
        }

        targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);
        if (subject == EnemyAbilityTargetSubject.Random)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                int randomIndex = Random.Range(index, candidates.Count);
                (candidates[index], candidates[randomIndex]) =
                    (candidates[randomIndex], candidates[index]);
            }
            StableSortEnemyAbilityTargetsByPriority(
                candidates,
                getPriority);
        }
        else if (subject != EnemyAbilityTargetSubject.WorldRadius ||
                 metric != EnemyAbilityTargetMetric.None)
        {
            bool descending =
                subject == EnemyAbilityTargetSubject.HighestValue;
            for (int index = 1; index < candidates.Count; index++)
            {
                T candidate = candidates[index];
                float candidateValue = getMetric(candidate, metric);
                StatusEffectTargetPriority candidatePriority =
                    getPriority(candidate);
                int insertionIndex = index - 1;
                while (insertionIndex >= 0)
                {
                    StatusEffectTargetPriority previousPriority =
                        getPriority(candidates[insertionIndex]);
                    int priorityComparison = CompareTargetPriority(
                        candidatePriority,
                        previousPriority);
                    float previousValue = getMetric(
                        candidates[insertionIndex],
                        metric);
                    bool shouldMove = priorityComparison < 0 ||
                                      (priorityComparison == 0 &&
                                       (descending
                                           ? previousValue < candidateValue
                                           : previousValue > candidateValue));
                    if (!shouldMove)
                        break;

                    candidates[insertionIndex + 1] =
                        candidates[insertionIndex];
                    insertionIndex--;
                }

                candidates[insertionIndex + 1] = candidate;
            }
        }

        if (candidates.Count > targetCount)
            candidates.RemoveRange(targetCount, candidates.Count - targetCount);
    }

    private static void StableSortEnemyAbilityTargetsByPriority<T>(
        List<T> candidates,
        Func<T, StatusEffectTargetPriority> getPriority)
    {
        for (int index = 1; index < candidates.Count; index++)
        {
            T candidate = candidates[index];
            StatusEffectTargetPriority candidatePriority =
                getPriority(candidate);
            int insertionIndex = index - 1;
            while (insertionIndex >= 0 &&
                   CompareTargetPriority(
                       candidatePriority,
                       getPriority(candidates[insertionIndex])) < 0)
            {
                candidates[insertionIndex + 1] =
                    candidates[insertionIndex];
                insertionIndex--;
            }

            candidates[insertionIndex + 1] = candidate;
        }
    }

    private static int CompareTargetPriority(
        StatusEffectTargetPriority left,
        StatusEffectTargetPriority right)
    {
        if (left.IsForced != right.IsForced)
            return left.IsForced ? -1 : 1;

        return right.Adjustment.CompareTo(left.Adjustment);
    }

    private static StatusEffectTargetPriority GetPlayerTargetPriority(
        IBattleCharacter target)
    {
        return StatusEffectTargetPriorityResolver.Resolve(
            target?.GetActiveStatusEffects());
    }

    private static StatusEffectTargetPriority GetEnemyAllyTargetPriority(
        EnemyRuntime target)
    {
        return StatusEffectTargetPriorityResolver.Resolve(
            target?.GetActiveStatusEffects());
    }

    private float GetEnemyAbilityTargetMetric(
        EnemyRuntime target,
        EnemyAbilityTargetMetric metric)
    {
        if (target == null)
            return 0f;

        return metric switch
        {
            EnemyAbilityTargetMetric.Health => target.Health,
            EnemyAbilityTargetMetric.HealthPercentage =>
                target.MaxHealth > 0
                    ? target.Health * 100f / target.MaxHealth
                    : 0f,
            EnemyAbilityTargetMetric.Shield => target.CurrentShield,
            EnemyAbilityTargetMetric.StackCount =>
                TryFindEnemyTile(target, out DungeonBoardSlot tile)
                    ? tile.StackCount
                    : 0f,
            _ => 0f
        };
    }

    private static float GetPlayerAbilityTargetMetric(
        IBattleCharacter target,
        EnemyAbilityTargetMetric metric)
    {
        if (target == null)
            return 0f;

        return metric switch
        {
            EnemyAbilityTargetMetric.Health => target.CurrentHealth,
            EnemyAbilityTargetMetric.HealthPercentage =>
                target.MaximumHealth > 0
                    ? target.CurrentHealth * 100f / target.MaximumHealth
                    : 0f,
            EnemyAbilityTargetMetric.Shield => target.CurrentShield,
            EnemyAbilityTargetMetric.TotalDamageDealt =>
                target.TotalDamageDealt,
            EnemyAbilityTargetMetric.StackCount =>
                CountStatusStacks(target.GetActiveStatusEffects()),
            _ => 0f
        };
    }

    private static int CountStatusStacks(
        IReadOnlyList<BattleStatusSnapshot> statuses)
    {
        int total = 0;
        if (statuses == null)
            return total;

        foreach (BattleStatusSnapshot status in statuses)
            total = BattleValueMath.SaturatingAddNonNegative(
                total,
                status.StackCount);
        return total;
    }

    private static bool MatchesEnemyAbilityConditions(
        EnemyAbilityDefinition ability,
        EnemyRuntime source,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerTargets,
        CharacterAttackDamageType? incomingDamageType = null,
        string incomingDamageSourceId = null)
    {
        IReadOnlyList<EnemyAbilityConditionDefinition> conditions =
            ability.Conditions;
        if (conditions == null || conditions.Count == 0)
            return true;

        bool matchAny =
            ability.ConditionMatchMode == CharacterConditionMatchMode.Any;
        bool evaluatedAny = false;
        foreach (EnemyAbilityConditionDefinition condition in conditions)
        {
            if (condition == null)
                continue;

            evaluatedAny = true;
            bool matched = MatchesEnemyAbilityCondition(
                condition,
                source,
                enemyTargets,
                playerTargets,
                incomingDamageType,
                incomingDamageSourceId);
            if (matchAny && matched)
                return true;
            if (!matchAny && !matched)
                return false;
        }

        return !evaluatedAny || !matchAny;
    }

    private static bool MatchesEnemyAbilityCondition(
        EnemyAbilityConditionDefinition condition,
        EnemyRuntime source,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerTargets,
        CharacterAttackDamageType? incomingDamageType,
        string incomingDamageSourceId)
    {
        switch (condition.Type)
        {
            case EnemyAbilityConditionType.SourceHealth:
                return CompareCharacterCondition(
                    source?.Health ?? 0,
                    condition.Comparison,
                    condition.Threshold);

            case EnemyAbilityConditionType.SourceHealthPercentage:
                return CompareCharacterCondition(
                    source != null && source.MaxHealth > 0
                        ? source.Health * 100f / source.MaxHealth
                        : 0f,
                    condition.Comparison,
                    condition.Threshold);

            case EnemyAbilityConditionType.SourceHasStatus:
            {
                bool hasStatus = source != null &&
                    EnemyAbilityConditionEvaluator.MatchesStatusSelection(
                        condition,
                        source.HasStatusEffect,
                        source.GetActiveStatusEffects());
                return hasStatus == condition.Expected;
            }

            case EnemyAbilityConditionType.TargetHealth:
                return AnyTargetMatches(
                    enemyTargets,
                    playerTargets,
                    target => target.Health,
                    target => target.CurrentHealth,
                    condition);

            case EnemyAbilityConditionType.TargetHealthPercentage:
                return AnyTargetMatches(
                    enemyTargets,
                    playerTargets,
                    target => target.MaxHealth > 0
                        ? target.Health * 100f / target.MaxHealth
                        : 0f,
                    target => target.MaximumHealth > 0
                        ? target.CurrentHealth * 100f /
                          target.MaximumHealth
                        : 0f,
                    condition);

            case EnemyAbilityConditionType.TargetTotalDamageDealt:
                return AnyTargetMatches(
                    enemyTargets,
                    playerTargets,
                    _ => 0f,
                    target => target.TotalDamageDealt,
                    condition);

            case EnemyAbilityConditionType.TargetHasStatus:
            {
                bool hasStatus = false;
                foreach (EnemyRuntime target in enemyTargets)
                {
                    hasStatus |= target != null &&
                        EnemyAbilityConditionEvaluator
                            .MatchesStatusSelection(
                                condition,
                                target.HasStatusEffect,
                                target.GetActiveStatusEffects());
                }
                foreach (IBattleCharacter target in playerTargets)
                {
                    hasStatus |= target != null &&
                        EnemyAbilityConditionEvaluator
                            .MatchesStatusSelection(
                                condition,
                                target.HasStatusEffect,
                                target.GetActiveStatusEffects());
                }
                return hasStatus == condition.Expected;
            }

            case EnemyAbilityConditionType.IncomingDamageType:
            {
                bool matches =
                    incomingDamageType.HasValue &&
                    incomingDamageType.Value ==
                    condition.IncomingDamageType;
                return matches == condition.Expected;
            }

            case EnemyAbilityConditionType.HasAlternateTarget:
            {
                bool hasTarget =
                    (enemyTargets?.Count ?? 0) +
                    (playerTargets?.Count ?? 0) > 0;
                return hasTarget == condition.Expected;
            }

            case EnemyAbilityConditionType.RepeatedDamageSource:
            {
                bool repeated = source != null &&
                    source.WasDamagedBySourceWithin(
                        incomingDamageSourceId,
                        condition.WindowDuration);
                return repeated == condition.Expected;
            }

            default:
                return false;
        }
    }

    private static bool AnyTargetMatches(
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerTargets,
        Func<EnemyRuntime, float> enemyValue,
        Func<IBattleCharacter, float> playerValue,
        EnemyAbilityConditionDefinition condition)
    {
        foreach (EnemyRuntime target in enemyTargets)
        {
            if (target != null && CompareCharacterCondition(
                    enemyValue(target),
                    condition.Comparison,
                    condition.Threshold))
            {
                return true;
            }
        }
        foreach (IBattleCharacter target in playerTargets)
        {
            if (target != null && CompareCharacterCondition(
                    playerValue(target),
                    condition.Comparison,
                    condition.Threshold))
            {
                return true;
            }
        }

        return false;
    }

    public void ClearAllStacks()
    {
        _lastPracticeDebugEnemy = null;
        practiceDebugOverlay?.Clear();
        UnbindAllPresentationEnemies();
        foreach (EnemyRuntime enemy in _enemyPlacements.Keys)
            enemy?.BindBattleBoard(null);
        foreach (DungeonBoardSlot tile in _exclusiveOccupants.Keys)
        {
            if (tile != null)
                tile.SetExclusiveFootprintOccupant(null, false);
        }
        _exclusiveOccupants.Clear();
        _enemyPlacements.Clear();
        _circularEnemyStates.Clear();
        _recentCoreAttackTimes.Clear();
        _spatialBattleElapsedTime = 0f;
        _nextFormationStableOrder = 0;
        ClearWorldEnemyViews();
        foreach (DungeonBoardSlot tile in _tiles)
        {
            if (tile != null)
                tile.ClearStack();
        }
        OccupancyChanged?.Invoke();
    }

    public void ClearAllEnemies()
    {
        _lastPracticeDebugEnemy = null;
        practiceDebugOverlay?.Clear();
        CancelManualTargetSelection();
        _forcedPriorityTarget = null;
        _forcedPriorityRemaining = 0f;
        _statusEventQueue.Clear();
        _defeatEventQueue.Clear();
        _enemyCombatEventQueue.Clear();
        _timedResourceRecoveryModifiers.Clear();
        _enemyDamageLinks.Clear();
        _enemyRadiusContacts.Clear();
        _currentEnemyRadiusContacts.Clear();
        _initializedEnemyRadiusAbilities.Clear();
        _resolvingDamageLink = false;
        _effectResolvedEventQueue.Clear();
        _statusLifecycleEventQueue.Clear();
        _unitLifecycleEventQueue.Clear();
        ClearAllStacks();
        _enemyVfxHandles.Clear();
        _lastEnemyVfxAnchors.Clear();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_initialized)
            RefreshLayout();
        if (UsesWorldPresentation)
            RefreshWorldRenderTexture();
    }

    private void OnDestroy()
    {
        SetPracticeDebugVisualization(false);
        _presentationDispatcher?.Dispose();
        _presentationDispatcher = null;
        UnbindAllPresentationEnemies();
        UnbindAllPresentationCharacters();
        ClearWorldEnemyViews();
        ClearWorldAllyViews();
        _areaPreview?.Dispose();
        _areaPreview = null;
        if (_runtimeWorldRenderTexture != null)
        {
            if (worldCamera != null &&
                ReferenceEquals(
                    worldCamera.targetTexture,
                    _runtimeWorldRenderTexture))
            {
                worldCamera.targetTexture = null;
            }
            _runtimeWorldRenderTexture.Release();
            Destroy(_runtimeWorldRenderTexture);
        }
        _runtimeWorldRenderTexture = null;
    }

    private void Awake()
    {
        practiceDebugOverlay?.SetVisible(false);
        if (worldActorPreview != null)
            worldActorPreview.SetActive(false);
        if (!HasWorldPresentation)
        {
            Debug.LogWarning(
                "DungeonBoardView 2.5D world Scene references are incomplete. " +
                "The battle world cannot be presented.",
                this);
        }
        if (worldInputView != null)
            worldInputView.Bind(this);
        if (worldActorRoot != null && worldAreaPreviewPrefab != null)
        {
            _areaPreview = new BattleAreaPreviewView(
                worldAreaPreviewPrefab,
                worldActorRoot);
        }
        ApplyArenaPresentation();
        EnsurePresentationPipeline();
    }

    private void ApplyArenaPresentation()
    {
        if (worldPresentationRoot != null)
            worldPresentationRoot.SetActive(UsesWorldPresentation);
        if (worldOutput != null)
            worldOutput.SetActive(UsesWorldPresentation);
        if (worldInputView != null)
            worldInputView.gameObject.SetActive(UsesWorldPresentation);
        ApplyResponsiveViewport();
    }

    public void ApplyResponsiveViewport()
    {
        RefreshWorldRenderTexture();
    }

    private void RefreshWorldRenderTexture()
    {
        if (!HasWorldPresentation || boardRect == null || worldCamera == null)
            return;

        Rect rect = boardRect.rect;
        float logicalWidth = Mathf.Max(1f, rect.width);
        float logicalHeight = Mathf.Max(1f, rect.height);
        float scale = Mathf.Min(
            1f,
            1600f / Mathf.Max(logicalWidth, logicalHeight));
        int width = QuantizeRenderTextureSize(logicalWidth * scale);
        int height = QuantizeRenderTextureSize(logicalHeight * scale);
        if (_runtimeWorldRenderTexture != null &&
            _runtimeWorldRenderTexture.width == width &&
            _runtimeWorldRenderTexture.height == height)
        {
            worldCamera.aspect = (float)width / height;
            SyncForegroundCamera();
            ResizeWorldGroundToCamera();
            return;
        }

        RenderTexture next = new(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32)
        {
            name = $"Dungeon World {width}x{height}",
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false,
        };
        next.Create();
        RenderTexture previous = _runtimeWorldRenderTexture;
        _runtimeWorldRenderTexture = next;
        worldCamera.targetTexture = next;
        worldCamera.aspect = (float)width / height;
        SyncForegroundCamera();
        if (worldOutput.TryGetComponent(out RawImage outputImage))
            outputImage.texture = next;

        ResizeWorldGroundToCamera();

        if (previous != null)
        {
            previous.Release();
            Destroy(previous);
        }
    }

    private static int QuantizeRenderTextureSize(float size)
    {
        int pixels = Mathf.Clamp(Mathf.RoundToInt(size), 256, 1600);
        return Mathf.Clamp(
            Mathf.CeilToInt(pixels / 16f) * 16,
            256,
            1600);
    }

    private void OnEnable()
    {
        ApplyArenaPresentation();
        EnsurePresentationPipeline();
    }

    private void OnDisable()
    {
        SetPracticeDebugVisualization(false);
        _presentationDispatcher?.Unbind();
        vfxPlayer?.ClearActive();
        if (worldPresentationRoot != null)
            worldPresentationRoot.SetActive(false);
        if (worldOutput != null)
            worldOutput.SetActive(false);
        if (worldInputView != null)
            worldInputView.gameObject.SetActive(false);
        _areaPreview?.Hide();
    }

    private void EnsurePresentationPipeline()
    {
        if (vfxPlayer == null)
            vfxPlayer = GetComponent<BattleVfxPlayer>();
        if (vfxPlayer == null)
        {
            Debug.LogError(
                "DungeonBoardView requires an authored BattleVfxPlayer component.",
                this);
            return;
        }

        if (vfxQualityProfile != null)
            vfxPlayer.ConfigureQuality(vfxQualityProfile);
        ConfigureVfxPresentationTarget();
        vfxPlayer.BindTargetResolver(this);
        _presentationDispatcher ??=
            new BattlePresentationDispatcher(vfxPlayer);
        _presentationDispatcher.Bind(this, this);
    }

    private void ConfigureVfxPresentationTarget()
    {
        if (vfxPlayer == null)
            return;

        if (UsesWorldPresentation)
            vfxPlayer.Configure(worldCamera, worldVfxRoot);
        else
            vfxPlayer.Configure(Camera.main);
    }

    private void RefreshLayout()
    {
        if (!_initialized)
            return;

        if (_arenaSetup.UsesBattleCore)
            RefreshCircularLayout();
        else
            ClearWorldEnemyViews();
    }

    private void RefreshCircularLayout()
    {
        if (!_initialized || _tiles.Count == 0)
            return;

        _worldEnemySync.Clear();
        _circularEnemySnapshot.Clear();
        foreach (KeyValuePair<EnemyRuntime, CircularEnemyState> entry in
                 _circularEnemyStates)
        {
            if (entry.Key != null && entry.Key.Health > 0 &&
                entry.Value != null &&
                _enemyPlacements.ContainsKey(entry.Key))
                _circularEnemySnapshot.Add(entry.Key);
        }
        _circularEnemySnapshot.Sort((left, right) =>
        {
            CircularEnemyState leftState = _circularEnemyStates[left];
            CircularEnemyState rightState = _circularEnemyStates[right];
            int layerOrder = leftState.LayerIndex.CompareTo(
                rightState.LayerIndex);
            if (layerOrder != 0)
                return layerOrder;
            int sectorOrder = leftState.SectorIndex.CompareTo(
                rightState.SectorIndex);
            return sectorOrder != 0
                ? sectorOrder
                : leftState.StableOrder.CompareTo(rightState.StableOrder);
        });

        for (int index = 0; index < _circularEnemySnapshot.Count; index++)
        {
            EnemyRuntime enemy = _circularEnemySnapshot[index];
            UpdateWorldEnemyView(
                enemy,
                _circularEnemyStates[enemy],
                index);
        }
        _circularEnemySnapshot.Clear();

        RemoveWorldEnemyViewsNotInSync();
    }

    private void LateUpdate()
    {
        if (!UsesWorldPresentation || worldCamera == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        DungeonHudPresentationSO style = DungeonHudPresentation.Load();
        foreach (WorldActorView view in _worldEnemyActors.Values)
        {
            view?.TickHitFlash(deltaTime);
            view?.FaceCamera(worldCamera);
            view?.RefreshDepthSorting(
                worldCamera,
                style.WorldDepthSortingRange);
        }
        foreach (WorldActorView view in _worldAllyActors.Values)
        {
            view?.FaceCamera(worldCamera);
            view?.RefreshDepthSorting(
                worldCamera,
                style.WorldDepthSortingRange);
        }

        foreach (KeyValuePair<IBattleCharacter, WorldActorView> entry in
                 _worldAllyActors)
        {
            if (entry.Key is not CharacterRuntime runtime ||
                !_worldAllyMovement.TryGetValue(
                    entry.Key,
                    out AllyMovementState movement))
            {
                continue;
            }

            entry.Value?.RefreshAllyHud(
                runtime,
                movement,
                style,
                worldCamera);
            entry.Value?.SetFacingDirection(
                movement.Destination - movement.Position,
                worldCamera);
        }

        if (_practiceDebugVisualizationEnabled)
            RefreshPracticeDebugVisualization();
    }

    private void ApplyWorldEnvironment()
    {
        if (!HasWorldPresentation)
            return;

        DungeonHudPresentationSO presentation =
            DungeonHudPresentation.Load();
        worldCamera.transform.localPosition =
            presentation.WorldCameraLocalPosition;
        worldCamera.transform.localRotation = Quaternion.Euler(
            presentation.WorldCameraLocalEulerAngles);
        worldCamera.fieldOfView = _environmentSetup.CameraFieldOfView;
        worldCamera.backgroundColor = _environmentSetup.ClearColor;
        SyncForegroundCamera();
        ResizeWorldGroundToCamera();
        if (worldBackdrop != null)
        {
            worldBackdrop.sprite = _environmentSetup.Backdrop;
            worldBackdrop.color = _environmentSetup.BackdropTint;
            worldBackdrop.gameObject.SetActive(
                _environmentSetup.Backdrop != null);
            ScaleWorldBackdrop();
        }
    }

    private void SyncForegroundCamera()
    {
        if (worldForegroundCamera == null || worldCamera == null)
            return;

        worldForegroundCamera.transform.localPosition =
            worldCamera.transform.localPosition;
        worldForegroundCamera.transform.localRotation =
            worldCamera.transform.localRotation;
        worldForegroundCamera.fieldOfView = worldCamera.fieldOfView;
        worldForegroundCamera.aspect = worldCamera.aspect;
        worldForegroundCamera.nearClipPlane = worldCamera.nearClipPlane;
        worldForegroundCamera.farClipPlane = worldCamera.farClipPlane;
    }

    private void ResizeWorldGroundToCamera()
    {
        if (worldGround == null || worldCamera == null)
            return;

        Transform groundSpace = worldGround.parent;
        if (groundSpace == null)
            return;

        Plane plane = new(
            groundSpace.up,
            groundSpace.TransformPoint(Vector3.zero));
        Vector2[] viewportCorners =
        {
            new(0f, 0f),
            new(0f, 1f),
            new(1f, 0f),
            new(1f, 1f),
        };
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float minimumZ = float.PositiveInfinity;
        float maximumZ = float.NegativeInfinity;
        Vector2[] actorGroundCorners = new Vector2[viewportCorners.Length];
        int intersectionCount = 0;
        for (int index = 0; index < viewportCorners.Length; index++)
        {
            Ray ray = worldCamera.ViewportPointToRay(viewportCorners[index]);
            if (!plane.Raycast(ray, out float distance) || distance < 0f)
                continue;

            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 localPoint = groundSpace.InverseTransformPoint(
                worldPoint);
            minimumX = Mathf.Min(minimumX, localPoint.x);
            maximumX = Mathf.Max(maximumX, localPoint.x);
            minimumZ = Mathf.Min(minimumZ, localPoint.z);
            maximumZ = Mathf.Max(maximumZ, localPoint.z);
            if (worldActorRoot != null)
            {
                Vector3 actorPoint = worldActorRoot.InverseTransformPoint(
                    worldPoint);
                actorGroundCorners[index] = new Vector2(
                    actorPoint.x,
                    actorPoint.z);
            }
            intersectionCount++;
        }

        if (intersectionCount != viewportCorners.Length)
            return;

        if (worldActorRoot != null)
        {
            _worldSpawnLineRadius =
                DungeonWorldSpawnGeometry.ResolveSpawnLineRadius(
                    actorGroundCorners,
                    worldSpawnLinePadding);
        }

        float visibleWidth = maximumX - minimumX;
        float visibleDepth = maximumZ - minimumZ;
        float margin = Mathf.Max(
            2f,
            Mathf.Max(visibleWidth, visibleDepth) * 0.1f);
        Vector3 localPosition = worldGround.localPosition;
        localPosition.x = (minimumX + maximumX) * 0.5f;
        localPosition.z = (minimumZ + maximumZ) * 0.5f;
        worldGround.localPosition = localPosition;

        Vector3 localScale = worldGround.localScale;
        localScale.x = Mathf.Max(
            MinimumWorldGroundSize,
            visibleWidth + margin * 2f);
        localScale.z = Mathf.Max(
            MinimumWorldGroundSize,
            visibleDepth + margin * 2f);
        worldGround.localScale = localScale;
    }

    private void ApplyWorldArenaRadius()
    {
        if (worldArenaRing == null)
            return;

        float radius = _arenaSetup.UsesBattleCore
            ? _arenaSetup.WorldRadius
            : BattleArenaSetup.DefaultWorldRadius;
        float scale = radius / AuthoredArenaRingRadius;
        scale = Mathf.Max(0.01f, scale);
        worldArenaRing.localScale = new Vector3(scale, 1f, scale);
    }

    private void ScaleWorldBackdrop()
    {
        if (worldBackdrop == null || worldBackdrop.sprite == null)
            return;

        Vector2 size = worldBackdrop.sprite.bounds.size;
        if (size.x <= 0f || size.y <= 0f)
            return;

        const float targetWidth = 16f;
        float scale = targetWidth / size.x;
        worldBackdrop.transform.localScale = new Vector3(
            scale,
            scale,
            1f);
    }

    private WorldActorView CreateWorldActor(
        string objectName,
        bool createAllyHud = false)
    {
        if (!HasWorldPresentation)
            return null;

        GameObject instance = Instantiate(worldActorPrefab, worldActorRoot);
        instance.name = objectName;
        SetLayerRecursively(instance, worldActorRoot.gameObject.layer);
        return new WorldActorView(instance, createAllyHud);
    }

    private void UpdateWorldEnemyView(
        EnemyRuntime enemy,
        CircularEnemyState state,
        int laneIndex)
    {
        if (!UsesWorldPresentation || enemy == null || state == null)
            return;

        _worldEnemySync.Add(enemy);
        if (!_worldEnemyActors.TryGetValue(enemy, out WorldActorView view) ||
            view == null || view.GameObject == null)
        {
            view = CreateWorldActor($"imgWorldEnemy_{laneIndex + 1}");
            if (view == null)
                return;
            _worldEnemyActors[enemy] = view;
        }

        Sprite sprite = enemy.Definition != null
            ? enemy.Definition.BoardSprite
            : null;
        if (sprite == null && enemy.Definition != null &&
            _missingWorldEnemySpriteWarnings.Add(enemy.Definition))
        {
            Debug.LogWarning(
                $"Enemy '{enemy.Definition.name}' has no 1:1 Board Sprite " +
                "and cannot be rendered in the dungeon world. Assign one " +
                "in PS260714/Enemy Editor.",
                enemy.Definition);
        }
        DungeonHudPresentationSO presentation =
            DungeonHudPresentation.Load();
        if (!view.Configure(
                sprite,
                presentation != null
                    ? presentation.WorldEnemyHeight
                    : worldEnemyHeight,
                100 + laneIndex))
            return;

        view.SetWorldPosition(new Vector3(
            state.ResolvedPosition.x,
            0f,
            state.ResolvedPosition.y));
        view.FaceCamera(worldCamera);
    }

    private void RefreshWorldAllyViews()
    {
        if (!UsesWorldPresentation)
        {
            ClearWorldAllyViews();
            return;
        }

        List<IBattleCharacter> ordered = new(_battleCharacters);
        ordered.Sort((left, right) =>
            left.PartySlotIndex.CompareTo(right.PartySlotIndex));
        HashSet<IBattleCharacter> active = new(ordered);
        List<IBattleCharacter> removed = new();
        foreach (KeyValuePair<IBattleCharacter, WorldActorView> entry in
                 _worldAllyActors)
        {
            if (!active.Contains(entry.Key))
                removed.Add(entry.Key);
        }
        foreach (IBattleCharacter character in removed)
            RemoveWorldAllyView(character);

        float wallRadius = GetWorldWallRadius();
        for (int index = 0; index < ordered.Count; index++)
        {
            IBattleCharacter character = ordered[index];
            if (!_worldAllyActors.TryGetValue(
                    character,
                    out WorldActorView view) ||
                view == null || view.GameObject == null)
            {
                view = CreateWorldActor(
                    $"imgWorldAlly_{character.PartySlotIndex + 1}",
                    true);
                if (view == null)
                    continue;
                _worldAllyActors[character] = view;
            }

            CharacterRuntime runtime = character as CharacterRuntime;
            Sprite sprite = runtime != null
                ? runtime.CurrentBattleSdSprite
                : null;
            DungeonHudPresentationSO presentation =
                DungeonHudPresentation.Load();
            if (!view.Configure(
                    sprite,
                    presentation != null
                        ? presentation.WorldAllyHeight
                        : worldAllyHeight,
                    200 + index,
                    runtime?.WorldSdReferenceSprite,
                    runtime?.WorldSdScaleMultiplier ?? 1f,
                    runtime?.WorldSdGroundOffset ?? 0f,
                    runtime?.WorldSdHeadHeightNormalized ?? 1f,
                    runtime?.WorldSdFacesRight ?? true))
                continue;

            AllyMovementState movement = GetOrCreateAllyMovementState(
                character,
                index,
                ordered.Count,
                wallRadius);
            view.SetWorldPosition(new Vector3(
                movement.Position.x,
                WorldActorGroundHeight,
                movement.Position.y));
            view.SetSelected(ReferenceEquals(character, _selectedWorldAlly));
            view.FaceCamera(worldCamera);
        }
    }

    private AllyMovementState GetOrCreateAllyMovementState(
        IBattleCharacter character,
        int index,
        int count,
        float wallRadius)
    {
        if (_worldAllyMovement.TryGetValue(
                character,
                out AllyMovementState movement))
        {
            movement.Position = BattleAreaGeometry.ClampToRadius(
                movement.Position,
                Vector2.zero,
                Mathf.Max(0f, wallRadius - worldAllyBoundaryPadding));
            movement.Destination = BattleAreaGeometry.ClampToRadius(
                movement.Destination,
                Vector2.zero,
                Mathf.Max(0f, wallRadius - worldAllyBoundaryPadding));
            return movement;
        }

        float placementRadius = count <= 1 ? 0f : wallRadius * 0.42f;
        float angle = count switch
        {
            1 => 90f,
            2 => index == 0 ? 180f : 0f,
            _ => 90f - index * (360f / count),
        };
        float radians = angle * Mathf.Deg2Rad;
        movement = new AllyMovementState(new Vector2(
            Mathf.Cos(radians) * placementRadius,
            Mathf.Sin(radians) * placementRadius));
        _worldAllyMovement[character] = movement;
        return movement;
    }

    private void RemoveStaleWorldAllyMovementStates()
    {
        List<IBattleCharacter> removed = new();
        foreach (IBattleCharacter character in _worldAllyMovement.Keys)
        {
            if (!_battleCharacters.Contains(character))
                removed.Add(character);
        }

        foreach (IBattleCharacter character in removed)
            _worldAllyMovement.Remove(character);
        if (_selectedWorldAlly != null &&
            !_battleCharacters.Contains(_selectedWorldAlly))
        {
            _selectedWorldAlly = null;
        }
    }

    private void TickWorldAllyMovement(float deltaTime)
    {
        float step = Mathf.Max(0.1f, worldAllyMoveSpeed) *
                     Mathf.Max(0f, deltaTime);
        foreach (KeyValuePair<IBattleCharacter, AllyMovementState> entry in
                 _worldAllyMovement)
        {
            AllyMovementState movement = entry.Value;
            movement.Position = Vector2.MoveTowards(
                movement.Position,
                movement.Destination,
                step);
            if (_worldAllyActors.TryGetValue(
                    entry.Key,
                    out WorldActorView view))
            {
                view?.SetWorldPosition(new Vector3(
                    movement.Position.x,
                    WorldActorGroundHeight,
                    movement.Position.y));
            }
        }
    }

    private bool TryGetWorldEnemyAnchor(
        EnemyRuntime enemy,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot anchor)
    {
        anchor = default;
        return UsesWorldPresentation &&
               enemy != null &&
               _worldEnemyActors.TryGetValue(enemy, out WorldActorView view) &&
               view != null &&
               view.TryGetAnchor(worldCamera, anchorType, out anchor);
    }

    private bool TryGetWorldAllyAnchor(
        IBattleCharacter character,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot anchor)
    {
        anchor = default;
        return UsesWorldPresentation &&
               character != null &&
               _worldAllyActors.TryGetValue(
                   character,
                   out WorldActorView view) &&
               view != null &&
               view.TryGetAnchor(worldCamera, anchorType, out anchor);
    }

    private void RemoveWorldEnemyViewsNotInSync()
    {
        if (!UsesWorldPresentation)
        {
            ClearWorldEnemyViews();
            return;
        }

        List<EnemyRuntime> removed = new();
        foreach (EnemyRuntime enemy in _worldEnemyActors.Keys)
        {
            if (!_worldEnemySync.Contains(enemy))
                removed.Add(enemy);
        }
        foreach (EnemyRuntime enemy in removed)
            RemoveWorldEnemyView(enemy);
    }

    private void RemoveWorldEnemyView(EnemyRuntime enemy)
    {
        if (enemy == null)
            return;

        if (ReferenceEquals(enemy, _lastPracticeDebugEnemy))
            _lastPracticeDebugEnemy = null;
        if (!_worldEnemyActors.TryGetValue(enemy, out WorldActorView view))
            return;

        if (view?.GameObject != null)
            Destroy(view.GameObject);
        _worldEnemyActors.Remove(enemy);
        _worldEnemySync.Remove(enemy);
    }

    private void RemoveWorldAllyView(IBattleCharacter character)
    {
        if (character == null)
            return;

        if (ReferenceEquals(character, _lastPracticeDebugAlly))
            _lastPracticeDebugAlly = null;
        if (!_worldAllyActors.TryGetValue(
                character,
                out WorldActorView view))
        {
            return;
        }

        if (view?.GameObject != null)
            Destroy(view.GameObject);
        _worldAllyActors.Remove(character);
    }

    private void ClearWorldEnemyViews()
    {
        foreach (WorldActorView view in _worldEnemyActors.Values)
        {
            if (view?.GameObject != null)
                Destroy(view.GameObject);
        }
        _worldEnemyActors.Clear();
        _worldEnemySync.Clear();
        _lastPracticeDebugEnemy = null;
        practiceDebugOverlay?.Clear();
    }

    private void ClearWorldAllyViews()
    {
        foreach (WorldActorView view in _worldAllyActors.Values)
        {
            if (view?.GameObject != null)
                Destroy(view.GameObject);
        }
        _worldAllyActors.Clear();
        _lastPracticeDebugAlly = null;
        practiceDebugOverlay?.Clear();
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private bool TryGetTile(int row, int column, out DungeonBoardSlot tile)
    {
        tile = null;
        if (row < 0 || row >= GridSize || column < 0 || column >= GridSize)
            return false;

        int index = row * GridSize + column;
        if (index < 0 || index >= _tiles.Count ||
            index >= TotalEnemyCapacity)
            return false;

        tile = _tiles[index];
        return tile != null;
    }

    private int TryDamageTile(int row, int column, int damage)
    {
        return TryGetTile(row, column, out DungeonBoardSlot tile)
            ? TryDamageTile(tile, damage)
            : 0;
    }

    private int TryDamageTile(DungeonBoardSlot targetTile, int damage)
    {
        return TryDamageTile(
            targetTile,
            damage,
            CharacterAttackDamageType.Physical);
    }

    private int TryDamageTile(
        DungeonBoardSlot targetTile,
        int damage,
        CharacterAttackDamageType damageType)
    {
        return TryDamageTile(targetTile, damage, damageType, null);
    }

    private int TryDamageTile(
        DungeonBoardSlot targetTile,
        int damage,
        IBattleCharacter source)
    {
        return TryDamageTile(
            targetTile,
            damage,
            CharacterAttackDamageType.Physical,
            source);
    }

    private int TryDamageTile(
        DungeonBoardSlot targetTile,
        int damage,
        CharacterAttackDamageType damageType,
        IBattleCharacter source)
    {
        targetTile = ResolveAnchorTile(targetTile);
        if (targetTile == null || targetTile.TopEnemy == null || damage <= 0 ||
            targetTile.TopEnemy.IsUntargetable)
            return 0;

        string damageSourceId = ResolveDamageSourceId(source);
        DungeonBoardSlot redirectTile =
            damageType == CharacterAttackDamageType.Fixed
                ? null
                : FindModularDamageRedirect(
                    targetTile,
                    damageType,
                    damageSourceId);
        DungeonBoardSlot damageReceiver = redirectTile != null
            ? redirectTile
            : targetTile;
        EnemyRuntime damagedEnemy = damageReceiver.TopEnemy;
        int linkedAppliedDamage = 0;
        if (!_resolvingDamageLink)
        {
            linkedAppliedDamage = ShareLinkedDamage(
                damagedEnemy,
                ref damage,
                damageType,
                source);
        }
        CaptureEnemyVfxAnchor(damagedEnemy, damageReceiver);
        damage = ExecuteBeforeSelfDamageAbilities(
            damageReceiver,
            damagedEnemy,
            damage,
            damageType,
            damageSourceId);
        if (damage <= 0 || damagedEnemy.Health <= 0)
            return linkedAppliedDamage;

        int previousHealth = damagedEnemy.Health;
        TryGetUnitPosition(
            BattleStatusTarget.FromEnemy(damagedEnemy),
            out Vector2 damagedPosition);
        int appliedDamage = damageReceiver.TryDamageTop(damage, damageType);
        if (appliedDamage > 0)
        {
            damagedEnemy.RecordDamageTaken(damageSourceId);
            if (source != null &&
                damageType != CharacterAttackDamageType.StatusEffect &&
                damageType != CharacterAttackDamageType.StatusRemoval &&
                damagedEnemy.TryConsumeDamageReflection(
                    out float reflectionRatio))
            {
                int reflectedDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(appliedDamage * reflectionRatio));
                source.TakeDamage(reflectedDamage);
            }
            if (damageType != CharacterAttackDamageType.StatusEffect &&
                damageType != CharacterAttackDamageType.StatusRemoval &&
                damagedEnemy.TryInterruptCharge(
                    EnemyChargeInterruptReason.DirectDamage,
                    out EnemyActiveChargeRuntimeState interrupted))
            {
                RecordEnemyAbilityActivation(
                    interrupted.AbilityState,
                    damagedEnemy,
                    true,
                    false);
            }
            TryAdvanceEnemyPhaseForHealth(
                damagedEnemy,
                damagedPosition);
            PublishEnemyCombatEvent(new EnemyCombatEvent(
                EnemyCombatEventType.DamageTaken,
                damagedEnemy,
                relatedCharacter: source,
                requestedDamage: damage,
                appliedDamage: appliedDamage,
                previousHealth: previousHealth,
                currentHealth: damagedEnemy.Health,
                damageSourceId: damageSourceId,
                worldPosition: damagedPosition));
            ExecuteHealthThresholdAbilities(
                damageReceiver,
                damagedEnemy,
                previousHealth,
                damagedPosition);
            ShowEnemyHitFeedback(damageReceiver, damagedEnemy);
        }
        if (appliedDamage > 0 && damagedEnemy.Health <= 0)
        {
            ExecuteDeathAbilities(damageReceiver, damagedEnemy);
            ReleasePlacement(damagedEnemy, false);
            NotifyEnemyDefeated(new BattleEnemyDefeatedEvent(
                damagedEnemy,
                source));
            ExecuteNearbyEnemyDeathAbilities(
                damagedEnemy,
                damagedPosition,
                damageReceiver);
            SynchronizeEnemyPresentationBindings();
            OccupancyChanged?.Invoke();
        }

        return BattleValueMath.SaturatingAddNonNegative(
            appliedDamage,
            linkedAppliedDamage);
    }

    private int ShareLinkedDamage(
        EnemyRuntime damagedEnemy,
        ref int primaryDamage,
        CharacterAttackDamageType damageType,
        IBattleCharacter damageSource)
    {
        if (damagedEnemy == null || primaryDamage <= 0 ||
            damageType == CharacterAttackDamageType.Fixed)
        {
            return 0;
        }

        EnemyDamageLinkGroup selected = null;
        foreach (EnemyDamageLinkGroup group in _enemyDamageLinks)
        {
            if (group == null || group.Remaining <= 0f ||
                !group.Members.Contains(damagedEnemy) ||
                group.ShareRatio <= 0f)
            {
                continue;
            }
            if (selected == null || group.ShareRatio > selected.ShareRatio)
                selected = group;
        }
        if (selected == null)
            return 0;

        List<EnemyRuntime> receivers = new();
        foreach (EnemyRuntime member in selected.Members)
        {
            if (member != null && member.Health > 0 &&
                !ReferenceEquals(member, damagedEnemy) &&
                TryFindEnemyTile(member, out _))
            {
                receivers.Add(member);
            }
        }
        if (receivers.Count == 0)
            return 0;

        int sharedRequested = Mathf.Clamp(
            Mathf.RoundToInt(primaryDamage * selected.ShareRatio),
            0,
            primaryDamage);
        if (sharedRequested <= 0)
            return 0;
        primaryDamage -= sharedRequested;

        int totalApplied = 0;
        int baseShare = sharedRequested / receivers.Count;
        int remainder = sharedRequested % receivers.Count;
        _resolvingDamageLink = true;
        try
        {
            for (int index = 0; index < receivers.Count; index++)
            {
                int share = baseShare + (index < remainder ? 1 : 0);
                if (share <= 0 || !TryFindEnemyTile(
                        receivers[index],
                        out DungeonBoardSlot receiverTile))
                {
                    continue;
                }

                totalApplied = BattleValueMath.SaturatingAddNonNegative(
                    totalApplied,
                    TryDamageTile(
                        receiverTile,
                        share,
                        damageType,
                        damageSource));
            }
        }
        finally
        {
            _resolvingDamageLink = false;
        }
        return totalApplied;
    }

    private bool AreEnemiesDamageLinked(
        EnemyRuntime left,
        EnemyRuntime right)
    {
        if (left == null || right == null)
            return false;
        foreach (EnemyDamageLinkGroup group in _enemyDamageLinks)
        {
            if (group != null && group.Remaining > 0f &&
                group.Members.Contains(left) &&
                group.Members.Contains(right))
            {
                return true;
            }
        }
        return false;
    }

    private static string ResolveDamageSourceId(IBattleCharacter source)
    {
        if (source == null)
            return string.Empty;

        if (source.PartySlotIndex >= 0)
            return $"player-slot:{source.PartySlotIndex}";

        if (source is CharacterRuntime runtime &&
            !string.IsNullOrWhiteSpace(
                runtime.Definition?.CharacterId))
        {
            return $"player-character:{runtime.Definition.CharacterId}";
        }

        return string.Empty;
    }

    private void ShowEnemyHitFeedback(
        DungeonBoardSlot tile,
        EnemyRuntime enemy)
    {
        if (enemy == null)
            return;

        tile?.ShowEnemyHitFeedback(
            enemy,
            enemyHitFlashColor,
            enemyHitFlashDuration);
        if (_worldEnemyActors.TryGetValue(enemy, out WorldActorView view))
        {
            view?.ShowHitFlash(
                enemyHitFlashColor,
                enemyHitFlashDuration);
        }
    }

    private List<DungeonBoardSlot> CollectOccupiedTiles()
    {
        List<DungeonBoardSlot> result = new();
        foreach (DungeonBoardSlot tile in _tiles)
        {
            if (tile != null && tile.StackCount > 0)
                result.Add(tile);
        }

        return result;
    }

    private readonly struct EnemyTargetPriorityEvaluation
    {
        public bool IsExcluded { get; }
        public bool IsForced { get; }
        public float Adjustment { get; }

        public EnemyTargetPriorityEvaluation(
            bool isExcluded,
            bool isForced,
            float adjustment)
        {
            IsExcluded = isExcluded;
            IsForced = isForced;
            Adjustment =
                float.IsNaN(adjustment) || float.IsInfinity(adjustment)
                    ? 0f
                    : adjustment;
        }
    }

    private List<DungeonBoardSlot> CollectPriorityTargetTiles(
        out bool hasAlternateTarget)
    {
        List<DungeonBoardSlot> occupiedTiles = CollectOccupiedTiles();
        hasAlternateTarget = occupiedTiles.Count > 1;
        List<DungeonBoardSlot> priorityTargets = new();
        foreach (DungeonBoardSlot tile in occupiedTiles)
        {
            EnemyRuntime enemy = tile.TopEnemy;
            if (enemy == null)
                continue;

            EnemyTargetPriorityEvaluation evaluation =
                EvaluateTargetPriority(enemy, hasAlternateTarget);
            bool itemForced =
                _forcedPriorityRemaining > 0f &&
                ReferenceEquals(enemy, _forcedPriorityTarget);
            if (itemForced || evaluation.IsForced ||
                !evaluation.IsExcluded)
            {
                priorityTargets.Add(tile);
            }
        }

        return priorityTargets.Count > 0
            ? priorityTargets
            : occupiedTiles;
    }

    private static EnemyTargetPriorityEvaluation EvaluateTargetPriority(
        EnemyRuntime source,
        bool hasAlternateTarget)
    {
        if (source == null)
            return default;

        StatusEffectTargetPriority statusPriority =
            StatusEffectTargetPriorityResolver.Resolve(
                source.GetActiveStatusEffects());
        bool excluded = false;
        bool forced = statusPriority.IsForced;
        double adjustment = statusPriority.Adjustment;
        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!CanActivateEnemyAbility(source, state) ||
                !ability.RespondsToTrigger(
                    EnemyAbilityTrigger.OnTargetPriorityEvaluation) ||
                !EnemyAbilityConditionEvaluator.MatchesSourceOnly(
                    ability,
                    source,
                    hasAlternateTarget))
            {
                continue;
            }

            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation != null && operation.Enabled &&
                    operation.Type ==
                        EnemyAbilityOperationType.ModifyTargetPriority)
                {
                    switch (operation.TargetPriorityMode)
                    {
                        case EnemyTargetPriorityMode.Exclude:
                            excluded = true;
                            break;
                        case EnemyTargetPriorityMode.Adjust:
                            adjustment +=
                                operation.TargetPriorityAdjustment;
                            break;
                        case EnemyTargetPriorityMode.ForceFocus:
                            forced = true;
                            break;
                    }
                }
            }
        }

        float clampedAdjustment = adjustment > float.MaxValue
            ? float.MaxValue
            : adjustment < -float.MaxValue
                ? -float.MaxValue
                : (float)adjustment;
        return new EnemyTargetPriorityEvaluation(
            excluded,
            forced,
            clampedAdjustment);
    }

    private static int CompareTargetPriority(
        DungeonBoardSlot left,
        DungeonBoardSlot right,
        bool hasAlternateTarget)
    {
        EnemyTargetPriorityEvaluation leftPriority =
            EvaluateTargetPriority(left?.TopEnemy, hasAlternateTarget);
        EnemyTargetPriorityEvaluation rightPriority =
            EvaluateTargetPriority(right?.TopEnemy, hasAlternateTarget);
        if (leftPriority.IsForced != rightPriority.IsForced)
            return leftPriority.IsForced ? -1 : 1;

        return rightPriority.Adjustment.CompareTo(
            leftPriority.Adjustment);
    }

    private static void StableSortByTargetPriority(
        List<DungeonBoardSlot> candidates,
        bool hasAlternateTarget)
    {
        for (int index = 1; index < candidates.Count; index++)
        {
            DungeonBoardSlot candidate = candidates[index];
            int insertionIndex = index - 1;
            while (insertionIndex >= 0 &&
                   CompareTargetPriority(
                       candidate,
                       candidates[insertionIndex],
                       hasAlternateTarget) < 0)
            {
                candidates[insertionIndex + 1] =
                    candidates[insertionIndex];
                insertionIndex--;
            }

            candidates[insertionIndex + 1] = candidate;
        }
    }

    private bool TryFindEnemyTile(
        EnemyRuntime enemy,
        out DungeonBoardSlot targetTile)
    {
        targetTile = null;
        if (enemy == null)
            return false;

        if (_enemyPlacements.TryGetValue(
                enemy,
                out EnemyPlacement placement) &&
            placement?.Anchor != null &&
            ReferenceEquals(placement.Anchor.TopEnemy, enemy))
        {
            targetTile = placement.Anchor;
            return true;
        }

        foreach (DungeonBoardSlot tile in _tiles)
        {
            if (tile != null && ReferenceEquals(tile.TopEnemy, enemy))
            {
                targetTile = tile;
                return true;
            }
        }

        return false;
    }

    private bool TryGetForcedPriorityTile(out DungeonBoardSlot targetTile)
    {
        if (_forcedPriorityRemaining > 0f &&
            TryFindEnemyTile(_forcedPriorityTarget, out targetTile))
        {
            return true;
        }

        targetTile = null;
        return false;
    }

    private void TickForcedPriorityTarget(float deltaTime)
    {
        if (_forcedPriorityTarget == null)
            return;

        _forcedPriorityRemaining = Mathf.Max(
            0f,
            _forcedPriorityRemaining - Mathf.Max(0f, deltaTime));
        if (_forcedPriorityRemaining > 0f &&
            TryFindEnemyTile(_forcedPriorityTarget, out _))
        {
            return;
        }

        _forcedPriorityTarget = null;
        _forcedPriorityRemaining = 0f;
    }

    private List<EnemyRuntime>[,] CaptureExistingStacks()
    {
        if (_tiles.Count != GridSize * GridSize)
            return null;

        List<EnemyRuntime>[,] result =
            new List<EnemyRuntime>[GridSize, GridSize];
        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
                result[row, column] =
                    _tiles[row * GridSize + column].CopyEnemyRuntimes();
        }

        return result;
    }

    private void RestoreExistingStacks(
        List<EnemyRuntime>[,] previousEnemies,
        int previousSize)
    {
        if (previousEnemies == null)
            return;

        int preservedSize = Mathf.Min(previousSize, GridSize);
        for (int row = 0; row < preservedSize; row++)
        {
            for (int column = 0; column < preservedSize; column++)
            {
                foreach (EnemyRuntime enemy in previousEnemies[row, column])
                {
                    if (!TryBuildPlacementAt(
                            row,
                            column,
                            enemy,
                            null,
                            out PendingEnemyPlacement pending) ||
                        !pending.Anchor.TryAdd(enemy))
                    {
                        continue;
                    }

                    RegisterPlacement(pending);
                }
            }
        }
    }

    private void ClearTileObjects()
    {
        _tiles.Clear();
    }

    private void CreateEnemySlots(int gridSize)
    {
        _tiles.Clear();
        GridSize = _arenaSetup.UsesBattleCore
            ? Mathf.Max(MinimumGridSize, gridSize)
            : Mathf.Clamp(gridSize, MinimumGridSize, MaximumGridSize);
        for (int row = 0; row < GridSize; row++)
        {
            for (int column = 0; column < GridSize; column++)
            {
                DungeonBoardSlot slot = new();
                slot.Initialize(row, column, _maximumStackSize);
                _tiles.Add(slot);
            }
        }
    }

    private void BindPresentationEnemy(EnemyRuntime enemy)
    {
        if (enemy == null || !_boundPresentationEnemies.Add(enemy))
            return;

        enemy.StatusLifecycle += HandleStatusLifecycle;
        GetOrCreateEnemyVfxHandle(enemy);
    }

    private void UnbindPresentationEnemy(EnemyRuntime enemy)
    {
        if (enemy == null || !_boundPresentationEnemies.Remove(enemy))
            return;

        enemy.StatusLifecycle -= HandleStatusLifecycle;
    }

    private void UnbindAllPresentationEnemies()
    {
        foreach (EnemyRuntime enemy in _boundPresentationEnemies)
        {
            if (enemy != null)
                enemy.StatusLifecycle -= HandleStatusLifecycle;
        }

        _boundPresentationEnemies.Clear();
    }

    private void SynchronizeEnemyPresentationBindings()
    {
        HashSet<EnemyRuntime> currentEnemies = new();
        foreach (DungeonBoardSlot tile in _tiles)
        {
            if (tile == null)
                continue;

            foreach (EnemyRuntime enemy in tile.CopyEnemyRuntimes())
            {
                if (enemy != null)
                    currentEnemies.Add(enemy);
            }
        }

        List<EnemyRuntime> removedEnemies = new();
        foreach (EnemyRuntime enemy in _boundPresentationEnemies)
        {
            if (enemy == null || !currentEnemies.Contains(enemy))
                removedEnemies.Add(enemy);
        }
        foreach (EnemyRuntime enemy in removedEnemies)
            UnbindPresentationEnemy(enemy);
        foreach (EnemyRuntime enemy in currentEnemies)
            BindPresentationEnemy(enemy);
    }

    private void BindPresentationCharacter(CharacterRuntime character)
    {
        if (character == null ||
            !_boundPresentationCharacters.Add(character))
        {
            return;
        }

        character.StatusLifecycle += HandleStatusLifecycle;
        character.BindManualTargetHandler(HandleManualAllyClicked);
        GetOrCreateAllyVfxHandle(character);
    }

    private void UnbindAllPresentationCharacters()
    {
        foreach (CharacterRuntime character in _boundPresentationCharacters)
        {
            if (character != null)
            {
                character.StatusLifecycle -= HandleStatusLifecycle;
                character.BindManualTargetHandler(null);
                character.SetManualTargetSelectionState(false, false);
            }
        }

        _boundPresentationCharacters.Clear();
    }

    private void HandleStatusLifecycle(StatusEffectLifecycleEvent eventData)
    {
        PublishStatusLifecycle(eventData);
    }

    private BattleVfxTargetHandle GetOrCreateEnemyVfxHandle(
        EnemyRuntime enemy)
    {
        if (enemy == null)
            return default;
        if (_enemyVfxHandles.TryGetValue(enemy, out BattleVfxTargetHandle handle))
            return handle;

        handle = CreateVfxTargetHandle();
        _enemyVfxHandles.Add(enemy, handle);
        return handle;
    }

    private BattleVfxTargetHandle GetOrCreateAllyVfxHandle(
        IBattleCharacter ally)
    {
        if (ally == null)
            return default;
        if (_allyVfxHandles.TryGetValue(ally, out BattleVfxTargetHandle handle))
            return handle;

        handle = CreateVfxTargetHandle();
        _allyVfxHandles.Add(ally, handle);
        return handle;
    }

    private BattleVfxTargetHandle CreateVfxTargetHandle()
    {
        if (_nextVfxTargetHandle <= 0)
            _nextVfxTargetHandle = 1;

        BattleVfxTargetHandle handle =
            new(_nextVfxTargetHandle);
        _nextVfxTargetHandle = _nextVfxTargetHandle == int.MaxValue
            ? 1
            : _nextVfxTargetHandle + 1;
        return handle;
    }

    private void CaptureEnemyVfxAnchor(
        EnemyRuntime enemy,
        DungeonBoardSlot tile)
    {
        if (enemy == null || tile == null)
            return;

        foreach (BattleVfxAnchorType anchorType in
                 Enum.GetValues(typeof(BattleVfxAnchorType)))
        {
            if (tile.TryGetEnemyVfxAnchor(
                    enemy,
                    anchorType,
                    out BattleVfxAnchorSnapshot snapshot))
            {
                StoreEnemyVfxAnchor(enemy, anchorType, snapshot);
            }
        }
    }

    private void StoreEnemyVfxAnchor(
        EnemyRuntime enemy,
        BattleVfxAnchorType anchorType,
        BattleVfxAnchorSnapshot snapshot)
    {
        if (enemy == null || !snapshot.IsValid)
            return;

        if (!_lastEnemyVfxAnchors.TryGetValue(
                enemy,
                out Dictionary<
                    BattleVfxAnchorType,
                    BattleVfxAnchorSnapshot> anchors))
        {
            anchors = new Dictionary<
                BattleVfxAnchorType,
                BattleVfxAnchorSnapshot>();
            _lastEnemyVfxAnchors.Add(enemy, anchors);
        }

        anchors[anchorType] = snapshot;
    }

    private bool TryGetStoredEnemyVfxAnchor(
        EnemyRuntime enemy,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot snapshot)
    {
        snapshot = default;
        return enemy != null &&
               _lastEnemyVfxAnchors.TryGetValue(
                   enemy,
                   out Dictionary<
                       BattleVfxAnchorType,
                       BattleVfxAnchorSnapshot> anchors) &&
               anchors.TryGetValue(anchorType, out snapshot) &&
               snapshot.IsValid;
    }

    internal void HandleWorldPointerDown(PointerEventData eventData)
    {
        if (!UsesWorldPresentation || eventData == null)
            return;

        if (_manualTargetRequest?.UsesWorldArea == true)
        {
            if (eventData.button == PointerEventData.InputButton.Right &&
                _manualTargetRequest.AllowCancel)
            {
                CancelManualTargetSelection();
            }
            else if (eventData.button ==
                     PointerEventData.InputButton.Left)
            {
                BeginManualAreaAim(eventData.position);
            }
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (_manualTargetRequest != null &&
                _manualTargetRequest.AllowCancel)
            {
                CancelManualTargetSelection();
            }
            else if (_selectedWorldAlly != null &&
                     TryScreenToWorldGround(
                         eventData.position,
                         out Vector2 destination))
            {
                RequestWorldAllyMove(_selectedWorldAlly, destination);
            }
            return;
        }

        if (_manualTargetRequest != null)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (_manualTargetRequest.Faction ==
                        CharacterTargetFaction.Enemy &&
                    TryHitWorldEnemy(
                        eventData.position,
                        out EnemyRuntime manualEnemy))
                {
                    HandleEnemyClicked(manualEnemy);
                }
                else if (_manualTargetRequest.Faction ==
                             CharacterTargetFaction.Ally &&
                    TryHitWorldAlly(
                             eventData.position,
                             out IBattleCharacter manualAlly) &&
                         manualAlly is CharacterRuntime manualRuntime)
                {
                    RecordPracticeDebugAlly(manualAlly);
                    manualRuntime.TryHandleWorldTargetClick();
                }
            }
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _pressedWorldAlly = null;
        _draggingWorldAlly = false;
        if (TryHitWorldAlly(eventData.position, out IBattleCharacter ally))
        {
            RecordPracticeDebugAlly(ally);
            if (ally is CharacterRuntime runtime &&
                runtime.TryHandleWorldTargetClick())
            {
                return;
            }

            SelectWorldAlly(ally);
            _pressedWorldAlly = ally;
            return;
        }

        if (TryHitWorldEnemy(eventData.position, out EnemyRuntime enemy))
        {
            HandleEnemyClicked(enemy);
            return;
        }

        SelectWorldAlly(null);
    }

    internal void HandleWorldPointerMove(
        PointerEventData eventData,
        bool dragging)
    {
        if (!UsesWorldPresentation || eventData == null)
            return;

        if (_manualTargetRequest?.UsesWorldArea == true)
        {
            if (_manualAreaPointerDown ||
                _manualTargetRequest.AreaDefinition.OriginMode ==
                    CharacterAreaOriginMode.Caster)
            {
                UpdateManualAreaAim(eventData.position);
            }
            else
            {
                PreviewManualAreaAnchor(eventData.position);
            }
            return;
        }

        if (dragging &&
            eventData.button == PointerEventData.InputButton.Left &&
            _pressedWorldAlly != null)
        {
            _draggingWorldAlly = true;
        }
    }

    internal void HandleWorldPointerUp(PointerEventData eventData)
    {
        if (!UsesWorldPresentation || eventData == null)
            return;

        if (_manualTargetRequest?.UsesWorldArea == true)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                UpdateManualAreaAim(eventData.position);
                _manualAreaPointerDown = false;
                if (_manualAreaAnchorSet)
                {
                    CompleteManualTargetSelection();
                }
            }
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left &&
            _draggingWorldAlly && _pressedWorldAlly != null &&
            TryScreenToWorldGround(
                eventData.position,
                out Vector2 destination))
        {
            RequestWorldAllyMove(_pressedWorldAlly, destination);
        }

        _pressedWorldAlly = null;
        _draggingWorldAlly = false;
    }

    private void SelectWorldAlly(IBattleCharacter character)
    {
        _selectedWorldAlly = character;
        RecordPracticeDebugAlly(character);
        RefreshManualTargetHighlights();
    }

    private void RecordPracticeDebugAlly(IBattleCharacter character)
    {
        _lastPracticeDebugAlly = character;
        _lastPracticeDebugEnemy = null;
    }

    private void RequestWorldAllyMove(
        IBattleCharacter character,
        Vector2 destination)
    {
        TrySetAllyDestination(character, destination, false);
    }

    private void InitializeManualAreaAim(
        BattleManualTargetSelectionRequest request)
    {
        Vector2 source = GetWorldAllyPosition(request.Source);
        _manualAreaDirection = Vector2.up;
        _manualAreaOrigin = source;
        _manualAreaAnchorSet =
            request.AreaDefinition.OriginMode ==
            CharacterAreaOriginMode.Caster;
        _manualAreaPointerDown = false;
        if (!_manualAreaAnchorSet)
        {
            _manualEnemyTargets.Clear();
            _manualAllyTargets.Clear();
            _areaPreview?.Hide();
            return;
        }
        RefreshManualAreaTargets();
    }

    private void BeginManualAreaAim(Vector2 screenPosition)
    {
        BattleManualTargetSelectionRequest request = _manualTargetRequest;
        BattleAreaDefinition definition = request?.AreaDefinition;
        if (definition == null || !definition.UsesWorldArea)
            return;

        _manualAreaPointerDown = true;
        if (definition.OriginMode == CharacterAreaOriginMode.Caster)
        {
            _manualAreaAnchorSet = true;
            UpdateManualAreaAim(screenPosition);
            return;
        }

        if (!TryScreenToWorldGround(screenPosition, out Vector2 cursor))
            return;

        Vector2 source = GetWorldAllyPosition(request.Source);
        _manualAreaOrigin = ResolveManualAreaOrigin(
            cursor,
            source,
            definition,
            request.AreaPlacementMode);
        Vector2 initialDirection = _manualAreaOrigin - source;
        if (initialDirection.sqrMagnitude > 0.0001f)
            _manualAreaDirection = initialDirection.normalized;
        _manualAreaAnchorSet = true;
        RefreshManualAreaTargets();
    }

    private void PreviewManualAreaAnchor(Vector2 screenPosition)
    {
        BattleManualTargetSelectionRequest request = _manualTargetRequest;
        BattleAreaDefinition definition = request?.AreaDefinition;
        if (definition == null || !definition.UsesWorldArea ||
            definition.OriginMode !=
                CharacterAreaOriginMode.DesignatedPoint ||
            !TryScreenToWorldGround(screenPosition, out Vector2 cursor))
        {
            return;
        }

        Vector2 source = GetWorldAllyPosition(request.Source);
        _manualAreaOrigin = ResolveManualAreaOrigin(
            cursor,
            source,
            definition,
            request.AreaPlacementMode);
        Vector2 previewDirection = _manualAreaOrigin - source;
        if (previewDirection.sqrMagnitude > 0.0001f)
            _manualAreaDirection = previewDirection.normalized;
        RefreshManualAreaTargets();
    }

    private void UpdateManualAreaAim(Vector2 screenPosition)
    {
        BattleManualTargetSelectionRequest request = _manualTargetRequest;
        BattleAreaDefinition definition = request?.AreaDefinition;
        if (definition == null || !definition.UsesWorldArea ||
            !TryScreenToWorldGround(screenPosition, out Vector2 cursor))
        {
            return;
        }

        Vector2 source = GetWorldAllyPosition(request.Source);
        if (definition.OriginMode == CharacterAreaOriginMode.Caster)
        {
            _manualAreaOrigin = source;
            Vector2 aim = cursor - source;
            if (aim.sqrMagnitude > 0.0001f)
                _manualAreaDirection = aim.normalized;
        }
        else if (_manualAreaAnchorSet)
        {
            Vector2 dragDirection = cursor - _manualAreaOrigin;
            if (dragDirection.sqrMagnitude > 0.0001f)
                _manualAreaDirection = dragDirection.normalized;
        }

        RefreshManualAreaTargets();
    }

    private Vector2 ResolveManualAreaOrigin(
        Vector2 cursor,
        Vector2 source,
        BattleAreaDefinition definition,
        BattleManualAreaPlacementMode placementMode)
    {
        return BattleAreaGeometry.ResolveManualOrigin(
            cursor,
            source,
            definition,
            GetWorldWallRadius(),
            placementMode);
    }

    private void RefreshManualAreaTargets()
    {
        BattleManualTargetSelectionRequest request = _manualTargetRequest;
        BattleAreaDefinition definition = request?.AreaDefinition;
        if (definition == null || !definition.UsesWorldArea)
            return;

        _manualEnemyTargets.Clear();
        _manualAllyTargets.Clear();
        int targetLimit = request.TargetCount;
        if (request.Faction == CharacterTargetFaction.Enemy)
        {
            foreach (EnemyRuntime enemy in request.EnemyCandidates)
            {
                if (enemy != null &&
                    _worldEnemyActors.TryGetValue(
                        enemy,
                        out WorldActorView view) &&
                    view != null &&
                    BattleAreaGeometry.Contains(
                        ToGround(view.WorldPosition),
                        _manualAreaOrigin,
                        _manualAreaDirection,
                        definition.Radius,
                        definition.Angle))
                {
                    _manualEnemyTargets.Add(enemy);
                    if (targetLimit > 0 &&
                        _manualEnemyTargets.Count >= targetLimit)
                    {
                        break;
                    }
                }
            }
        }
        else
        {
            foreach (IBattleCharacter ally in request.AllyCandidates)
            {
                if (ally != null &&
                    BattleAreaGeometry.Contains(
                        GetWorldAllyPosition(ally),
                        _manualAreaOrigin,
                        _manualAreaDirection,
                        definition.Radius,
                        definition.Angle))
                {
                    _manualAllyTargets.Add(ally);
                    if (targetLimit > 0 &&
                        _manualAllyTargets.Count >= targetLimit)
                    {
                        break;
                    }
                }
            }
        }

        _areaPreview?.Show(
            _manualAreaOrigin,
            _manualAreaDirection,
            definition);
        RefreshManualTargetHighlights();
        ManualTargetSelectionProgressChanged?.Invoke();
    }

    private bool TryScreenToWorldGround(
        Vector2 screenPosition,
        out Vector2 ground)
    {
        ground = default;
        if (worldInputView == null || worldCamera == null ||
            worldInputView.transform is not RectTransform rect)
        {
            return false;
        }

        Camera uiCamera = ResolveUiCamera(rect);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                screenPosition,
                uiCamera,
                out Vector2 local))
        {
            return false;
        }

        Rect bounds = rect.rect;
        if (bounds.width <= 0f || bounds.height <= 0f)
            return false;
        Vector2 viewport = new(
            Mathf.InverseLerp(bounds.xMin, bounds.xMax, local.x),
            Mathf.InverseLerp(bounds.yMin, bounds.yMax, local.y));
        Ray ray = worldCamera.ViewportPointToRay(
            new Vector3(viewport.x, viewport.y, 0f));
        Plane plane = new(Vector3.up, worldActorRoot.position);
        if (!plane.Raycast(ray, out float distance))
            return false;

        Vector3 localWorld = worldActorRoot.InverseTransformPoint(
            ray.GetPoint(distance));
        ground = ToGround(localWorld);
        return true;
    }

    private bool TryHitWorldAlly(
        Vector2 screenPosition,
        out IBattleCharacter ally)
    {
        ally = null;
        float nearest = float.PositiveInfinity;
        foreach (KeyValuePair<IBattleCharacter, WorldActorView> entry in
                 _worldAllyActors)
        {
            if (TryGetActorPointerDistance(
                    entry.Value,
                    screenPosition,
                    out float distance) && distance < nearest)
            {
                nearest = distance;
                ally = entry.Key;
            }
        }
        return ally != null;
    }

    private bool TryHitWorldEnemy(
        Vector2 screenPosition,
        out EnemyRuntime enemy)
    {
        enemy = null;
        float nearest = float.PositiveInfinity;
        foreach (KeyValuePair<EnemyRuntime, WorldActorView> entry in
                 _worldEnemyActors)
        {
            if (TryGetActorPointerDistance(
                    entry.Value,
                    screenPosition,
                    out float distance) && distance < nearest)
            {
                nearest = distance;
                enemy = entry.Key;
            }
        }
        return enemy != null;
    }

    private bool TryGetActorPointerDistance(
        WorldActorView view,
        Vector2 screenPosition,
        out float distance)
    {
        distance = float.PositiveInfinity;
        if (view == null || view.GameObject == null ||
            worldInputView == null || worldCamera == null ||
            worldInputView.transform is not RectTransform rect)
        {
            return false;
        }

        Camera uiCamera = ResolveUiCamera(rect);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                screenPosition,
                uiCamera,
                out Vector2 pointerLocal))
        {
            return false;
        }

        return TryGetActorInputLocalPosition(view, rect, out Vector2 center) &&
               PracticeBattleDebugGeometry.TryMeasureActorHit(
                   pointerLocal,
                   center,
                   worldActorHitRadiusPixels,
                   out distance);
    }

    private bool TryGetActorInputLocalPosition(
        WorldActorView view,
        RectTransform inputRect,
        out Vector2 localPosition)
    {
        localPosition = default;
        return view != null &&
               TryProjectWorldToInputLocal(
                   view.InteractionWorldPosition,
                   inputRect,
                   out localPosition);
    }

    private bool TryProjectGroundToInputLocal(
        Vector2 ground,
        out Vector2 localPosition)
    {
        localPosition = default;
        if (worldActorRoot == null ||
            worldInputView == null ||
            worldInputView.transform is not RectTransform inputRect)
        {
            return false;
        }

        Vector3 world = worldActorRoot.TransformPoint(new Vector3(
            ground.x,
            WorldActorGroundHeight,
            ground.y));
        return TryProjectWorldToInputLocal(
            world,
            inputRect,
            out localPosition,
            false);
    }

    private bool TryProjectWorldToInputLocal(
        Vector3 world,
        RectTransform inputRect,
        out Vector2 localPosition,
        bool clampToViewport = true)
    {
        localPosition = default;
        if (worldCamera == null || inputRect == null)
            return false;

        Vector3 viewport = worldCamera.WorldToViewportPoint(world);
        if (viewport.z <= 0f ||
            float.IsNaN(viewport.x) || float.IsInfinity(viewport.x) ||
            float.IsNaN(viewport.y) || float.IsInfinity(viewport.y))
        {
            return false;
        }

        Rect bounds = inputRect.rect;
        localPosition = clampToViewport
            ? new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, viewport.x),
                Mathf.Lerp(bounds.yMin, bounds.yMax, viewport.y))
            : new Vector2(
                bounds.xMin + bounds.width * viewport.x,
                bounds.yMin + bounds.height * viewport.y);
        return true;
    }

    private void RefreshPracticeDebugVisualization()
    {
        if (!_practiceDebugVisualizationEnabled ||
            practiceDebugOverlay == null ||
            !practiceDebugOverlay.HasRequiredReferences ||
            worldInputView == null ||
            worldInputView.transform is not RectTransform inputRect)
        {
            practiceDebugOverlay?.Clear();
            return;
        }

        practiceDebugOverlay.BeginFrame();
        float actorHitRadius =
            PracticeBattleDebugGeometry.ResolveActorHitRadius(
                worldActorHitRadiusPixels);
        float allySpacingRadius =
            PracticeBattleDebugGeometry.ResolveAllySpacingRadius(
                worldAllyMinimumSpacing);
        foreach (KeyValuePair<IBattleCharacter, WorldActorView> entry in
                 _worldAllyActors)
        {
            if (entry.Key == null || entry.Key.CurrentHealth <= 0 ||
                entry.Value == null)
            {
                continue;
            }

            if (TryGetActorInputLocalPosition(
                    entry.Value,
                    inputRect,
                    out Vector2 actorCenter))
            {
                practiceDebugOverlay.AddInputCircle(
                    inputRect,
                    actorCenter,
                    actorHitRadius,
                    PracticeBattleDebugPrimitiveKind.AllyClick);
            }

            if (allySpacingRadius > 0f &&
                _worldAllyMovement.TryGetValue(
                    entry.Key,
                    out AllyMovementState movement) &&
                movement != null)
            {
                AddPracticeDebugGroundCircle(
                    inputRect,
                    movement.Position,
                    allySpacingRadius,
                    PracticeBattleDebugPrimitiveKind.AllySpacing);
            }
        }

        foreach (KeyValuePair<EnemyRuntime, WorldActorView> entry in
                 _worldEnemyActors)
        {
            EnemyRuntime enemy = entry.Key;
            if (enemy == null || enemy.Health <= 0 || entry.Value == null ||
                !_circularEnemyStates.TryGetValue(
                    enemy,
                    out CircularEnemyState state) ||
                state == null)
            {
                continue;
            }

            if (TryGetActorInputLocalPosition(
                    entry.Value,
                    inputRect,
                    out Vector2 actorCenter))
            {
                practiceDebugOverlay.AddInputCircle(
                    inputRect,
                    actorCenter,
                    actorHitRadius,
                    PracticeBattleDebugPrimitiveKind.EnemyClick);
            }

            float formationRadius =
                PracticeBattleDebugGeometry.ResolveEnemyFormationRadius(
                    enemy.FormationRadius,
                    _arenaSetup.FormationSeparationRatio);
            if (formationRadius > 0f)
            {
                AddPracticeDebugGroundCircle(
                    inputRect,
                    state.ResolvedPosition,
                    formationRadius,
                    PracticeBattleDebugPrimitiveKind.EnemyFormation);
            }
        }

        AddPracticeDebugSelectedAllyRanges(inputRect);
        AddPracticeDebugSelectedEnemyRanges(inputRect);
        practiceDebugOverlay.EndFrame();
    }

    private void AddPracticeDebugSelectedAllyRanges(
        RectTransform inputRect)
    {
        if (_lastPracticeDebugAlly is not CharacterRuntime runtime ||
            runtime.CurrentHealth <= 0 || runtime.Data?.Definition == null ||
            !_worldAllyMovement.TryGetValue(
                runtime,
                out AllyMovementState movement) ||
            movement == null)
        {
            return;
        }

        _practiceDebugRangeRadii.Clear();
        foreach (IBattleAbilityDefinition ability in
                 runtime.Data.Definition.EnumerateBattleAbilities())
        {
            if (ability == null)
                continue;

            AddPracticeDebugAreaRadius(
                ability.Targeting.AreaDefinition);
            if (ability.BattleEffects == null)
                continue;
            foreach (IBattleEffectDefinition effect in
                     ability.BattleEffects)
            {
                AddPracticeDebugAreaRadius(
                    effect?.BattleTargetSelector?.AreaDefinition);
            }
        }

        AddPracticeDebugAbilityCircles(inputRect, movement.Position);
    }

    private void AddPracticeDebugSelectedEnemyRanges(
        RectTransform inputRect)
    {
        EnemyRuntime enemy = _lastPracticeDebugEnemy;
        if (enemy == null || enemy.Health <= 0 ||
            enemy.Definition == null ||
            !_circularEnemyStates.TryGetValue(
                enemy,
                out CircularEnemyState state) ||
            state == null)
        {
            return;
        }

        _practiceDebugRangeRadii.Clear();
        foreach (EnemyAbilityDefinition ability in
                 enemy.Definition.Abilities)
        {
            if (ability == null)
                continue;

            AddUniquePracticeDebugRadius(ability.Target?.WorldRadius ?? 0f);
            AddPracticeDebugAreaRadius(
                ability.Target?.AreaDefinition);
            AddUniquePracticeDebugRadius(
                ability.Telegraph?.WorldRadius ?? 0f);
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation == null || !operation.Enabled)
                    continue;

                AddUniquePracticeDebugRadius(operation.WorldRadius);
                foreach (CharacterEffectDefinition effect in
                         operation.Effects)
                {
                    AddPracticeDebugAreaRadius(
                        effect?.BattleTargetSelector?.AreaDefinition);
                }
            }
        }

        AddPracticeDebugAbilityCircles(
            inputRect,
            state.ResolvedPosition);

        if (!PracticeBattleDebugGeometry.IsFinitePositive(
                enemy.CoreAttackRange))
        {
            return;
        }

        Vector2 reachEnd =
            PracticeBattleDebugGeometry.ResolveEnemyCoreReachEnd(
                state.ResolvedPosition,
                state.DefenseLineRadius,
                enemy.CoreAttackRange);
        if (TryProjectGroundToInputLocal(
                state.ResolvedPosition,
                out Vector2 projectedStart) &&
            TryProjectGroundToInputLocal(
                reachEnd,
                out Vector2 projectedEnd))
        {
            practiceDebugOverlay.AddInputLine(
                inputRect,
                projectedStart,
                projectedEnd,
                PracticeBattleDebugPrimitiveKind.CoreReach);
        }
    }

    private void AddPracticeDebugAreaRadius(BattleAreaDefinition area)
    {
        if (area?.UsesWorldArea != true || !area.IsValid)
            return;

        float radius = area.OriginMode == CharacterAreaOriginMode.Caster
            ? area.Radius
            : area.MaxCastDistance;
        AddUniquePracticeDebugRadius(radius);
    }

    private void AddUniquePracticeDebugRadius(float radius)
    {
        if (!PracticeBattleDebugGeometry.IsFinitePositive(radius))
            return;

        foreach (float existing in _practiceDebugRangeRadii)
        {
            if (Mathf.Abs(existing - radius) <= 0.001f)
                return;
        }
        _practiceDebugRangeRadii.Add(radius);
    }

    private void AddPracticeDebugAbilityCircles(
        RectTransform inputRect,
        Vector2 center)
    {
        foreach (float radius in _practiceDebugRangeRadii)
        {
            AddPracticeDebugGroundCircle(
                inputRect,
                center,
                radius,
                PracticeBattleDebugPrimitiveKind.AbilityRange);
        }
        _practiceDebugRangeRadii.Clear();
    }

    private void AddPracticeDebugGroundCircle(
        RectTransform inputRect,
        Vector2 center,
        float radius,
        PracticeBattleDebugPrimitiveKind kind)
    {
        PracticeBattleDebugGeometry.AppendProjectedGroundCircle(
            center,
            radius,
            PracticeDebugGroundCircleSegments,
            TryProjectGroundToInputLocal,
            (start, end) => practiceDebugOverlay.AddInputLine(
                inputRect,
                start,
                end,
                kind));
    }

    private static Camera ResolveUiCamera(RectTransform rect)
    {
        Canvas canvas = rect != null
            ? rect.GetComponentInParent<Canvas>()
            : null;
        return canvas != null &&
               canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    public bool TryGetUnitPosition(
        BattleStatusTarget target,
        out Vector2 position)
    {
        position = Vector2.zero;
        if (!target.IsValid)
            return false;

        if (target.Ally != null)
        {
            if (!TryGetAllyMovementState(
                    target.Ally,
                    out AllyMovementState movement))
            {
                return false;
            }

            position = movement.Position;
            return true;
        }

        EnemyRuntime enemy = target.Enemy;
        if (enemy == null || enemy.Health <= 0 ||
            !_enemyPlacements.ContainsKey(enemy))
        {
            return false;
        }

        if (_circularEnemyStates.TryGetValue(
                enemy,
                out CircularEnemyState circularState))
        {
            position = ResolveCircularEnemyPosition(circularState);
            return true;
        }

        if (!_enemyPlacements.TryGetValue(
                enemy,
                out EnemyPlacement placement) ||
            placement?.Anchor == null)
            return false;

        DungeonBoardSlot tile = placement.Anchor;
        float center = (GridSize - 1) * 0.5f;
        position = new Vector2(
            tile.Column - center,
            tile.Row - center);
        return true;
    }

    public BattleSpatialZone GetUnitZone(BattleStatusTarget target)
    {
        if (!target.IsValid)
            return BattleSpatialZone.Unknown;

        if (target.Enemy != null &&
            _circularEnemyStates.TryGetValue(
                target.Enemy,
                out CircularEnemyState enemyState) &&
            enemyState.LayerIndex == 0 &&
            HasReachedFormationCell(enemyState))
        {
            return BattleSpatialZone.DefenseLine;
        }

        if (!TryGetUnitPosition(target, out Vector2 position))
            return BattleSpatialZone.Unknown;

        return position.magnitude <= InnerZoneBoundaryRadius
            ? BattleSpatialZone.Inner
            : BattleSpatialZone.Outer;
    }

    public IReadOnlyList<EnemyRuntime> SelectNearbyEnemies(
        BattleStatusTarget anchor,
        float radius = BattleSpatialDefaults.NearbyRadius,
        int maximumCount = 0,
        bool includeAnchor = false)
    {
        if (!TryGetUnitPosition(anchor, out Vector2 anchorPosition) ||
            !IsFinite(radius) || radius <= 0f)
        {
            return Array.Empty<EnemyRuntime>();
        }

        float radiusSquared = radius * radius;
        List<EnemyRuntime> result = new();
        foreach (EnemyRuntime enemy in _enemyPlacements.Keys)
        {
            if (enemy == null || enemy.Health <= 0 ||
                (!includeAnchor && ReferenceEquals(enemy, anchor.Enemy)) ||
                !TryGetUnitPosition(
                    BattleStatusTarget.FromEnemy(enemy),
                    out Vector2 enemyPosition) ||
                (enemyPosition - anchorPosition).sqrMagnitude >
                    radiusSquared)
            {
                continue;
            }

            result.Add(enemy);
        }

        SortEnemiesByDistance(result, anchorPosition);
        TrimToMaximumCount(result, maximumCount);
        return result.Count > 0
            ? result.ToArray()
            : Array.Empty<EnemyRuntime>();
    }

    public IReadOnlyList<EnemyRuntime> SelectEnemiesBehind(
        EnemyRuntime anchor,
        float maximumDistance = BattleSpatialDefaults.NearbyRadius,
        int maximumCount = 1,
        float halfAngle = BattleSpatialDefaults.BehindHalfAngle)
    {
        if (anchor == null ||
            !TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(anchor),
                out Vector2 anchorPosition) ||
            !IsFinite(maximumDistance) || maximumDistance <= 0f ||
            !IsFinite(halfAngle))
        {
            return Array.Empty<EnemyRuntime>();
        }

        Vector2 outward = anchorPosition.sqrMagnitude > 0.0001f
            ? anchorPosition.normalized
            : _circularEnemyStates.TryGetValue(
                anchor,
                out CircularEnemyState anchorState)
                ? anchorState.SpawnDirection
                : Vector2.up;
        float appliedHalfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
        float maximumDistanceSquared = maximumDistance * maximumDistance;
        List<EnemyRuntime> result = new();
        foreach (EnemyRuntime enemy in _enemyPlacements.Keys)
        {
            if (enemy == null || enemy.Health <= 0 ||
                ReferenceEquals(enemy, anchor) ||
                !TryGetUnitPosition(
                    BattleStatusTarget.FromEnemy(enemy),
                    out Vector2 enemyPosition))
            {
                continue;
            }

            Vector2 offset = enemyPosition - anchorPosition;
            if (offset.sqrMagnitude <= 0.0001f ||
                offset.sqrMagnitude > maximumDistanceSquared ||
                Vector2.Dot(offset, outward) <= 0f ||
                Vector2.Angle(outward, offset) > appliedHalfAngle)
            {
                continue;
            }

            result.Add(enemy);
        }

        SortEnemiesByDistance(result, anchorPosition);
        TrimToMaximumCount(result, maximumCount);
        return result.Count > 0
            ? result.ToArray()
            : Array.Empty<EnemyRuntime>();
    }

    public IReadOnlyList<EnemyRuntime> SelectDefenseLineEnemies()
    {
        List<EnemyRuntime> result = new();
        foreach (KeyValuePair<EnemyRuntime, CircularEnemyState> entry in
                 _circularEnemyStates)
        {
            if (entry.Key != null && entry.Key.Health > 0 &&
                _enemyPlacements.ContainsKey(entry.Key) &&
                entry.Value != null &&
                entry.Value.LayerIndex == 0 &&
                HasReachedFormationCell(entry.Value))
            {
                result.Add(entry.Key);
            }
        }

        SortEnemiesByStableOrder(result);
        return result.Count > 0
            ? result.ToArray()
            : Array.Empty<EnemyRuntime>();
    }

    public IReadOnlyList<EnemyRuntime> SelectRecentCoreAttackers(
        float lookbackSeconds =
            BattleSpatialDefaults.RecentCoreAttackWindow)
    {
        if (!IsFinite(lookbackSeconds) || lookbackSeconds <= 0f)
            return Array.Empty<EnemyRuntime>();

        float appliedLookback = Mathf.Min(
            lookbackSeconds,
            BattleSpatialDefaults.RecentCoreAttackWindow);
        RemoveExpiredCoreAttackHistory();
        List<EnemyRuntime> result = new();
        foreach (KeyValuePair<EnemyRuntime, float> entry in
                 _recentCoreAttackTimes)
        {
            if (entry.Key != null && entry.Key.Health > 0 &&
                _enemyPlacements.ContainsKey(entry.Key) &&
                _spatialBattleElapsedTime - entry.Value <= appliedLookback)
            {
                result.Add(entry.Key);
            }
        }

        result.Sort((left, right) =>
        {
            int timeOrder = _recentCoreAttackTimes[right].CompareTo(
                _recentCoreAttackTimes[left]);
            return timeOrder != 0
                ? timeOrder
                : GetEnemyStableOrder(left).CompareTo(
                    GetEnemyStableOrder(right));
        });
        return result.Count > 0
            ? result.ToArray()
            : Array.Empty<EnemyRuntime>();
    }

    public int MoveAlliesCoreward(
        IReadOnlyList<IBattleCharacter> targets,
        float distance = BattleSpatialDefaults.MovementStep)
    {
        if (!TryNormalizeMovementTargets(targets, distance, out float step))
            return 0;

        int changed = 0;
        foreach (IBattleCharacter target in targets)
        {
            if (!TryGetMovableAllyState(target, out AllyMovementState state))
                continue;

            Vector2 destination = Vector2.MoveTowards(
                state.Destination,
                Vector2.zero,
                step);
            if (TrySetAllyDestination(target, destination, false))
                changed++;
        }
        return changed;
    }

    public int MoveAlliesOutward(
        IReadOnlyList<IBattleCharacter> targets,
        float distance = BattleSpatialDefaults.MovementStep)
    {
        if (!TryNormalizeMovementTargets(targets, distance, out float step))
            return 0;

        int changed = 0;
        foreach (IBattleCharacter target in targets)
        {
            if (!TryGetMovableAllyState(target, out AllyMovementState state))
                continue;

            Vector2 direction = ResolveAllyRadialDirection(target, state);
            if (TrySetAllyDestination(
                    target,
                    state.Destination + direction * step,
                    false))
            {
                changed++;
            }
        }
        return changed;
    }

    public int MoveAlliesToOuterZone(
        IReadOnlyList<IBattleCharacter> targets)
    {
        if (!CanMoveSpatialTargets(targets))
            return 0;

        float radius = GetAllowedAllyRadius();
        int changed = 0;
        foreach (IBattleCharacter target in targets)
        {
            if (!TryGetMovableAllyState(target, out AllyMovementState state))
                continue;

            Vector2 direction = ResolveAllyRadialDirection(target, state);
            if (TrySetAllyDestination(
                    target,
                    direction * radius,
                    false))
            {
                changed++;
            }
        }
        return changed;
    }

    public int MoveAlliesToPoint(
        IReadOnlyList<IBattleCharacter> targets,
        Vector2 point,
        bool instant = false)
    {
        if (!CanMoveSpatialTargets(targets) || !IsFinite(point))
            return 0;

        int changed = 0;
        foreach (IBattleCharacter target in targets)
        {
            if (TryGetMovableAllyState(target, out _) &&
                TrySetAllyDestination(target, point, instant))
            {
                changed++;
            }
        }
        return changed;
    }

    public int MoveAlliesToEnemyFlank(
        IReadOnlyList<IBattleCharacter> targets,
        EnemyRuntime enemy,
        float flankDistance = BattleSpatialDefaults.MovementStep,
        bool instant = false)
    {
        if (!CanMoveSpatialTargets(targets) || enemy == null ||
            !IsFinite(flankDistance) || flankDistance <= 0f ||
            !TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(enemy),
                out Vector2 enemyPosition))
        {
            return 0;
        }

        Vector2 radial = enemyPosition.sqrMagnitude > 0.0001f
            ? enemyPosition.normalized
            : Vector2.up;
        Vector2 tangent = new(-radial.y, radial.x);
        int changed = 0;
        foreach (IBattleCharacter target in targets)
        {
            if (!TryGetMovableAllyState(target, out AllyMovementState state))
                continue;

            Vector2 first = enemyPosition + tangent * flankDistance;
            Vector2 second = enemyPosition - tangent * flankDistance;
            Vector2 destination =
                (first - state.Destination).sqrMagnitude <=
                (second - state.Destination).sqrMagnitude
                    ? first
                    : second;
            if (TrySetAllyDestination(target, destination, instant))
                changed++;
        }
        return changed;
    }

    public bool TrySwapAllies(
        IBattleCharacter first,
        IBattleCharacter second)
    {
        if (first == null || second == null ||
            ReferenceEquals(first, second) ||
            !TryGetMovableAllyState(first, out AllyMovementState firstState) ||
            !TryGetMovableAllyState(second, out AllyMovementState secondState))
        {
            return false;
        }

        Vector2 firstPosition = firstState.Position;
        Vector2 secondPosition = secondState.Position;
        if ((firstPosition - secondPosition).sqrMagnitude <= 0.0001f)
            return false;

        firstState.Position = secondPosition;
        firstState.Destination = secondPosition;
        secondState.Position = firstPosition;
        secondState.Destination = firstPosition;
        RefreshWorldAllyPosition(first, firstState);
        RefreshWorldAllyPosition(second, secondState);
        return true;
    }

    public int PullEnemiesTowardPoint(
        IReadOnlyList<EnemyRuntime> targets,
        Vector2 point,
        float distance = BattleSpatialDefaults.MovementStep)
    {
        if (!_arenaSetup.UsesBattleCore || targets == null ||
            targets.Count == 0 || !IsFinite(point) ||
            !IsFinite(distance) || distance <= 0f)
        {
            return 0;
        }

        EnsureFormationRadii();
        HashSet<EnemyRuntime> movable = new();
        foreach (EnemyRuntime enemy in targets)
        {
            if (enemy == null || enemy.Health <= 0 ||
                !_enemyPlacements.ContainsKey(enemy) ||
                !_circularEnemyStates.ContainsKey(enemy))
            {
                continue;
            }
            movable.Add(enemy);
        }
        if (movable.Count == 0)
            return 0;

        HashSet<int> occupiedCells = new();
        foreach (KeyValuePair<EnemyRuntime, CircularEnemyState> entry in
                 _circularEnemyStates)
        {
            if (entry.Key == null || entry.Value == null ||
                movable.Contains(entry.Key))
            {
                continue;
            }
            occupiedCells.Add(GetFormationCellKey(
                entry.Value.SectorIndex,
                entry.Value.LayerIndex));
        }

        List<EnemyRuntime> ordered = new(movable);
        ordered.Sort(CompareEnemiesByStableOrder);
        int sectorCount = Mathf.Max(1, _arenaSetup.LaneCount);
        int maximumLayers = Mathf.Max(1, _arenaSetup.MaximumLayerCount);
        float maximumFormationRadius = GetMaximumFormationRadius();
        int changed = 0;
        bool movementRequested = false;
        foreach (EnemyRuntime enemy in ordered)
        {
            CircularEnemyState state = _circularEnemyStates[enemy];
            Vector2 current = state.ResolvedPosition;
            Vector2 requested = Vector2.MoveTowards(
                current,
                point,
                distance);
            movementRequested |= (requested - current).sqrMagnitude >
                                 FormationArrivalTolerance *
                                 FormationArrivalTolerance;
            int bestSector = state.SectorIndex;
            int bestLayer = state.LayerIndex;
            float bestDistance = float.PositiveInfinity;
            for (int sector = 0; sector < sectorCount; sector++)
            {
                for (int layer = 0; layer < maximumLayers; layer++)
                {
                    int cellKey = GetFormationCellKey(sector, layer);
                    if (occupiedCells.Contains(cellKey))
                        continue;

                    Vector2 candidate = EstimateFormationCellPosition(
                        enemy,
                        sector,
                        layer,
                        maximumFormationRadius);
                    float candidateDistance =
                        (candidate - requested).sqrMagnitude;
                    if (candidateDistance < bestDistance - 0.0001f ||
                        (Mathf.Approximately(
                             candidateDistance,
                             bestDistance) &&
                         cellKey < GetFormationCellKey(
                             bestSector,
                             bestLayer)))
                    {
                        bestDistance = candidateDistance;
                        bestSector = sector;
                        bestLayer = layer;
                    }
                }
            }

            int reservedKey = GetFormationCellKey(
                bestSector,
                bestLayer);
            if (float.IsPositiveInfinity(bestDistance))
                continue;
            occupiedCells.Add(reservedKey);
            if (bestSector != state.SectorIndex ||
                bestLayer != state.LayerIndex)
            {
                changed++;
            }
            state.SectorIndex = bestSector;
            state.LayerIndex = bestLayer;
        }

        RefreshFormationTargets();
        foreach (EnemyRuntime enemy in ordered)
        {
            CircularEnemyState state = _circularEnemyStates[enemy];
            float resolvedRadius = ResolveFormationMovementTargetRadius(
                enemy,
                state);
            Vector2 resolved = state.SpawnDirection * resolvedRadius;
            if ((resolved - state.ResolvedPosition).sqrMagnitude >
                FormationArrivalTolerance * FormationArrivalTolerance)
            {
                changed = Mathf.Max(1, changed);
            }
            state.ResolvedPosition = resolved;
            UpdateFormationApproachProgress(state);
        }

        if (changed == 0 && movementRequested)
            changed = 1;

        if (changed > 0)
        {
            foreach (EnemyRuntime enemy in ordered)
            {
                if (enemy.TryInterruptCharge(
                        EnemyChargeInterruptReason.ForcedMovement,
                        out EnemyActiveChargeRuntimeState interrupted))
                {
                    RecordEnemyAbilityActivation(
                        interrupted.AbilityState,
                        enemy,
                        true,
                        false);
                }
            }
            RefreshCircularLayout();
        }
        return changed;
    }

    private bool TryGetAllyMovementState(
        IBattleCharacter character,
        out AllyMovementState movement)
    {
        movement = null;
        if (character == null || !_battleCharacters.Contains(character))
            return false;

        if (_worldAllyMovement.TryGetValue(character, out movement))
            return true;

        int index = _battleCharacters.IndexOf(character);
        movement = GetOrCreateAllyMovementState(
            character,
            index,
            _battleCharacters.Count,
            GetWorldWallRadius());
        return movement != null;
    }

    private bool TryGetMovableAllyState(
        IBattleCharacter character,
        out AllyMovementState movement)
    {
        movement = null;
        return character != null &&
               character.CurrentHealth > 0 &&
               TryGetAllyMovementState(character, out movement);
    }

    private bool CanMoveSpatialTargets(
        IReadOnlyList<IBattleCharacter> targets)
    {
        return _arenaSetup.UsesBattleCore &&
               targets != null &&
               targets.Count > 0;
    }

    private bool TryNormalizeMovementTargets(
        IReadOnlyList<IBattleCharacter> targets,
        float requestedDistance,
        out float distance)
    {
        distance = 0f;
        if (!CanMoveSpatialTargets(targets) ||
            !IsFinite(requestedDistance) ||
            requestedDistance <= 0f)
        {
            return false;
        }

        distance = requestedDistance;
        return true;
    }

    private bool TrySetAllyDestination(
        IBattleCharacter character,
        Vector2 destination,
        bool instant)
    {
        if (!IsFinite(destination) ||
            !TryGetAllyMovementState(character, out AllyMovementState state))
        {
            return false;
        }

        Vector2 resolved = ResolveWorldAllyDestination(
            character,
            state,
            destination);
        bool destinationChanged =
            (state.Destination - resolved).sqrMagnitude > 0.0001f;
        bool positionChanged = instant &&
            (state.Position - resolved).sqrMagnitude > 0.0001f;
        if (!destinationChanged && !positionChanged)
            return false;

        state.Destination = resolved;
        if (instant)
        {
            state.Position = resolved;
            RefreshWorldAllyPosition(character, state);
        }
        return true;
    }

    private Vector2 ResolveWorldAllyDestination(
        IBattleCharacter character,
        AllyMovementState movement,
        Vector2 destination)
    {
        float allowedRadius = GetAllowedAllyRadius();
        Vector2 resolved = BattleAreaGeometry.ClampToRadius(
            destination,
            Vector2.zero,
            allowedRadius);
        float spacing = Mathf.Max(0f, worldAllyMinimumSpacing);
        foreach (KeyValuePair<IBattleCharacter, AllyMovementState> entry in
                 _worldAllyMovement)
        {
            if (ReferenceEquals(entry.Key, character) || entry.Value == null)
                continue;

            Vector2 otherDestination = entry.Value.Destination;
            Vector2 separation = resolved - otherDestination;
            if (separation.sqrMagnitude >= spacing * spacing)
                continue;

            Vector2 direction = separation.sqrMagnitude > 0.0001f
                ? separation.normalized
                : (movement.Position - otherDestination).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = ResolveAllyRadialDirection(character, movement);
            resolved = otherDestination + direction * spacing;
            resolved = BattleAreaGeometry.ClampToRadius(
                resolved,
                Vector2.zero,
                allowedRadius);
        }

        return resolved;
    }

    private Vector2 ResolveAllyRadialDirection(
        IBattleCharacter character,
        AllyMovementState movement)
    {
        Vector2 radial = movement != null &&
                         movement.Destination.sqrMagnitude > 0.0001f
            ? movement.Destination
            : movement != null
                ? movement.Position
                : Vector2.zero;
        if (radial.sqrMagnitude > 0.0001f)
            return radial.normalized;

        int count = Mathf.Max(1, _battleCharacters.Count);
        int index = Mathf.Max(0, _battleCharacters.IndexOf(character));
        float angle = count switch
        {
            1 => 90f,
            2 => index == 0 ? 180f : 0f,
            _ => 90f - index * (360f / count),
        };
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void RefreshWorldAllyPosition(
        IBattleCharacter character,
        AllyMovementState movement)
    {
        if (character == null || movement == null ||
            !_worldAllyActors.TryGetValue(
                character,
                out WorldActorView view))
        {
            return;
        }

        view?.SetWorldPosition(new Vector3(
            movement.Position.x,
            WorldActorGroundHeight,
            movement.Position.y));
    }

    private float GetAllowedAllyRadius()
    {
        return Mathf.Max(
            0f,
            GetWorldWallRadius() - worldAllyBoundaryPadding);
    }

    private Vector2 ResolveCircularEnemyPosition(
        CircularEnemyState state)
    {
        return state != null
            ? state.ResolvedPosition
            : Vector2.zero;
    }

    private int ResolveRequiredFormationGridSize()
    {
        int capacity = Mathf.Max(
            1,
            _arenaSetup.MaximumEnemyCapacity);
        return Mathf.Max(
            MinimumGridSize,
            Mathf.CeilToInt(Mathf.Sqrt(capacity)));
    }

    private void EnsureFormationRadii()
    {
        if (_formationRadiiInitialized)
            return;

        ResolveCircularEnemyRadii(
            out _formationSpawnRadius,
            out _formationDefenseLineRadius);
        _formationSpawnRadius = Mathf.Max(
            _formationDefenseLineRadius,
            _formationSpawnRadius);
        _formationRadiiInitialized = true;
    }

    private bool TryReserveFormationCell(
        out int sectorIndex,
        out int layerIndex)
    {
        sectorIndex = -1;
        layerIndex = -1;
        if (!_arenaSetup.UsesBattleCore)
            return false;

        int sectorCount = Mathf.Max(1, _arenaSetup.LaneCount);
        int maximumLayers = Mathf.Max(1, _arenaSetup.MaximumLayerCount);
        int shallowestLayer = maximumLayers;
        List<int> candidateSectors = new();
        for (int sector = 0; sector < sectorCount; sector++)
        {
            int firstFreeLayer = 0;
            while (firstFreeLayer < maximumLayers &&
                   IsFormationCellOccupied(sector, firstFreeLayer))
            {
                firstFreeLayer++;
            }

            if (firstFreeLayer >= maximumLayers)
                continue;
            if (firstFreeLayer < shallowestLayer)
            {
                shallowestLayer = firstFreeLayer;
                candidateSectors.Clear();
            }
            if (firstFreeLayer == shallowestLayer)
                candidateSectors.Add(sector);
        }

        if (candidateSectors.Count == 0)
            return false;

        sectorIndex = candidateSectors[
            Random.Range(0, candidateSectors.Count)];
        layerIndex = shallowestLayer;
        return true;
    }

    private bool IsFormationCellOccupied(
        int sectorIndex,
        int layerIndex,
        EnemyRuntime ignoredEnemy = null)
    {
        foreach (KeyValuePair<EnemyRuntime, CircularEnemyState> entry in
                 _circularEnemyStates)
        {
            CircularEnemyState state = entry.Value;
            if (state != null &&
                !ReferenceEquals(entry.Key, ignoredEnemy) &&
                state.SectorIndex == sectorIndex &&
                state.LayerIndex == layerIndex)
            {
                return true;
            }
        }
        return false;
    }

    private Vector2 ResolveFormationSectorDirection(int sectorIndex)
    {
        int sectorCount = Mathf.Max(1, _arenaSetup.LaneCount);
        int normalizedSector = ((sectorIndex % sectorCount) + sectorCount) %
                               sectorCount;
        return DungeonWorldSpawnGeometry.DirectionFromUnitSample(
            (normalizedSector + 0.5f) / sectorCount);
    }

    private int GetFormationCellKey(int sectorIndex, int layerIndex)
    {
        return Mathf.Max(0, layerIndex) *
               Mathf.Max(1, _arenaSetup.LaneCount) +
               Mathf.Max(0, sectorIndex);
    }

    private Vector2 EstimateFormationCellPosition(
        EnemyRuntime enemy,
        int sectorIndex,
        int layerIndex)
    {
        float maximumFormationRadius = GetMaximumFormationRadius(enemy);
        return EstimateFormationCellPosition(
            enemy,
            sectorIndex,
            layerIndex,
            maximumFormationRadius);
    }

    private Vector2 EstimateFormationCellPosition(
        EnemyRuntime enemy,
        int sectorIndex,
        int layerIndex,
        float maximumFormationRadius)
    {
        maximumFormationRadius = Mathf.Max(
            maximumFormationRadius,
            enemy != null ? enemy.FormationRadius : 0f);
        int sectorCount = Mathf.Max(1, _arenaSetup.LaneCount);
        float angularSine = Mathf.Sin(Mathf.PI / sectorCount);
        float angularSafeRadius = angularSine > 0.0001f
            ? maximumFormationRadius *
              _arenaSetup.FormationSeparationRatio / angularSine
            : _formationDefenseLineRadius;
        float baseRadius = Mathf.Max(
            _formationDefenseLineRadius,
            angularSafeRadius);
        float radialSpacing = Mathf.Max(
            _arenaSetup.LayerSpacing,
            Mathf.Max(0f, maximumFormationRadius * 2f) *
            _arenaSetup.FormationSeparationRatio);
        float radius = baseRadius +
                       Mathf.Max(0, layerIndex) * radialSpacing;
        return ResolveFormationSectorDirection(sectorIndex) * radius;
    }

    private float GetMaximumFormationRadius(
        EnemyRuntime additionalEnemy = null)
    {
        float maximumFormationRadius = additionalEnemy != null
            ? additionalEnemy.FormationRadius
            : 0f;
        foreach (EnemyRuntime candidate in _circularEnemyStates.Keys)
        {
            if (candidate != null)
            {
                maximumFormationRadius = Mathf.Max(
                    maximumFormationRadius,
                    candidate.FormationRadius);
            }
        }
        return maximumFormationRadius;
    }

    private float ResolveFormationSpawnRadius(
        EnemyRuntime enemy,
        int sectorIndex,
        int layerIndex)
    {
        Vector2 estimatedCell = EstimateFormationCellPosition(
            enemy,
            sectorIndex,
            layerIndex);
        float estimatedTargetRadius = estimatedCell.magnitude;
        float estimatedBaseRadius = EstimateFormationCellPosition(
            enemy,
            sectorIndex,
            0).magnitude;
        return Mathf.Max(
            estimatedTargetRadius,
            _formationSpawnRadius +
            Mathf.Max(0f, estimatedTargetRadius - estimatedBaseRadius));
    }

    private void CompactFormationSector(int sectorIndex)
    {
        _circularEnemySnapshot.Clear();
        foreach (KeyValuePair<EnemyRuntime, CircularEnemyState> entry in
                 _circularEnemyStates)
        {
            if (entry.Key != null && entry.Value != null &&
                entry.Value.SectorIndex == sectorIndex)
            {
                _circularEnemySnapshot.Add(entry.Key);
            }
        }
        _circularEnemySnapshot.Sort((left, right) =>
        {
            CircularEnemyState leftState = _circularEnemyStates[left];
            CircularEnemyState rightState = _circularEnemyStates[right];
            int layerOrder = leftState.LayerIndex.CompareTo(
                rightState.LayerIndex);
            return layerOrder != 0
                ? layerOrder
                : leftState.StableOrder.CompareTo(rightState.StableOrder);
        });
        for (int index = 0; index < _circularEnemySnapshot.Count; index++)
            _circularEnemyStates[_circularEnemySnapshot[index]].LayerIndex =
                index;
        _circularEnemySnapshot.Clear();
    }

    private void RefreshFormationTargets()
    {
        if (!_arenaSetup.UsesBattleCore)
            return;

        EnsureFormationRadii();
        int sectorCount = Mathf.Max(1, _arenaSetup.LaneCount);
        for (int sector = 0; sector < sectorCount; sector++)
            CompactFormationSector(sector);

        float maximumFormationRadius = 0f;
        foreach (EnemyRuntime enemy in _circularEnemyStates.Keys)
        {
            if (enemy != null)
            {
                maximumFormationRadius = Mathf.Max(
                    maximumFormationRadius,
                    enemy.FormationRadius);
            }
        }

        float angularSine = Mathf.Sin(Mathf.PI / sectorCount);
        float angularSafeRadius = angularSine > 0.0001f
            ? maximumFormationRadius *
              _arenaSetup.FormationSeparationRatio / angularSine
            : _formationDefenseLineRadius;
        float defenseLineRadius = Mathf.Max(
            _formationDefenseLineRadius,
            angularSafeRadius);

        for (int sector = 0; sector < sectorCount; sector++)
        {
            _circularEnemySnapshot.Clear();
            foreach (KeyValuePair<EnemyRuntime, CircularEnemyState> entry in
                     _circularEnemyStates)
            {
                if (entry.Key != null && entry.Value != null &&
                    entry.Value.SectorIndex == sector)
                {
                    _circularEnemySnapshot.Add(entry.Key);
                }
            }
            _circularEnemySnapshot.Sort((left, right) =>
                _circularEnemyStates[left].LayerIndex.CompareTo(
                    _circularEnemyStates[right].LayerIndex));

            float targetRadius = defenseLineRadius;
            EnemyRuntime previousEnemy = null;
            Vector2 direction = ResolveFormationSectorDirection(sector);
            for (int layer = 0;
                 layer < _circularEnemySnapshot.Count;
                 layer++)
            {
                EnemyRuntime enemy = _circularEnemySnapshot[layer];
                CircularEnemyState state = _circularEnemyStates[enemy];
                if (previousEnemy != null)
                {
                    float separation =
                        (previousEnemy.FormationRadius +
                         enemy.FormationRadius) *
                        _arenaSetup.FormationSeparationRatio;
                    targetRadius += Mathf.Max(
                        _arenaSetup.LayerSpacing,
                        separation);
                }

                state.SectorIndex = sector;
                state.LayerIndex = layer;
                state.SpawnDirection = direction;
                state.DefenseLineRadius = defenseLineRadius;
                state.TargetRadius = targetRadius;
                UpdateFormationApproachProgress(state);
                previousEnemy = enemy;
            }
            _circularEnemySnapshot.Clear();
        }
    }

    private void UpdateFormationApproachProgress(CircularEnemyState state)
    {
        if (state == null)
            return;

        state.CurrentRadius = state.ResolvedPosition.magnitude;
        float travel = _formationSpawnRadius - state.DefenseLineRadius;
        state.ApproachProgress = travel > FormationArrivalTolerance
            ? Mathf.Clamp01(
                (_formationSpawnRadius - state.CurrentRadius) / travel)
            : state.CurrentRadius <=
              state.DefenseLineRadius + FormationArrivalTolerance
                ? 1f
                : 0f;
    }

    private static bool HasReachedFormationCell(CircularEnemyState state)
    {
        if (state == null)
            return false;

        Vector2 target = state.SpawnDirection * state.TargetRadius;
        return (state.ResolvedPosition - target).sqrMagnitude <=
               FormationArrivalTolerance * FormationArrivalTolerance;
    }

    private float ResolveFormationMovementSpeed(EnemyRuntime enemy)
    {
        if (enemy == null)
            return 0f;

        float normalizedSpan = Mathf.Max(
            0.01f,
            _arenaSetup.SpawnRadiusNormalized -
            _arenaSetup.WallRadiusNormalized);
        float worldSpan = Mathf.Max(
            0.01f,
            _formationSpawnRadius - _formationDefenseLineRadius);
        return Mathf.Max(
            0.01f,
            enemy.ApproachSpeed * worldSpan / normalizedSpan);
    }

    private float ResolveFormationMovementTargetRadius(
        EnemyRuntime enemy,
        CircularEnemyState state)
    {
        if (enemy == null || state == null || state.LayerIndex <= 0)
            return state != null ? state.TargetRadius : 0f;

        foreach (KeyValuePair<EnemyRuntime, CircularEnemyState> entry in
                 _circularEnemyStates)
        {
            CircularEnemyState innerState = entry.Value;
            if (entry.Key == null || innerState == null ||
                innerState.SectorIndex != state.SectorIndex ||
                innerState.LayerIndex != state.LayerIndex - 1)
            {
                continue;
            }

            float separation = Mathf.Max(
                _arenaSetup.LayerSpacing,
                (entry.Key.FormationRadius + enemy.FormationRadius) *
                _arenaSetup.FormationSeparationRatio);
            return Mathf.Max(
                state.TargetRadius,
                innerState.CurrentRadius + separation);
        }

        return state.TargetRadius;
    }

    private void ResolveCircularEnemyRadii(
        out float spawnRadius,
        out float stopRadius)
    {
        DungeonHudPresentationSO presentation =
            DungeonHudPresentation.Load();
        stopRadius = GetWorldEnemyStopRadius(presentation);
        float clearance = presentation != null
            ? presentation.WorldEnemyArenaRingClearance
            : 0f;
        spawnRadius = Mathf.Max(
            GetWorldSpawnRadius(),
            stopRadius + clearance);
    }

    private void SortEnemiesByDistance(
        List<EnemyRuntime> enemies,
        Vector2 origin)
    {
        enemies?.Sort((left, right) =>
        {
            float leftDistance = TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(left),
                out Vector2 leftPosition)
                ? (leftPosition - origin).sqrMagnitude
                : float.PositiveInfinity;
            float rightDistance = TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(right),
                out Vector2 rightPosition)
                ? (rightPosition - origin).sqrMagnitude
                : float.PositiveInfinity;
            int distanceOrder = leftDistance.CompareTo(rightDistance);
            return distanceOrder != 0
                ? distanceOrder
                : CompareEnemiesByStableOrder(left, right);
        });
    }

    private void SortEnemiesByStableOrder(List<EnemyRuntime> enemies)
    {
        enemies?.Sort(CompareEnemiesByStableOrder);
    }

    private int CompareEnemiesByStableOrder(
        EnemyRuntime left,
        EnemyRuntime right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        int placementOrder = GetEnemyStableOrder(left).CompareTo(
            GetEnemyStableOrder(right));
        if (placementOrder != 0)
            return placementOrder;

        string leftName = left?.Definition != null
            ? left.Definition.name
            : string.Empty;
        string rightName = right?.Definition != null
            ? right.Definition.name
            : string.Empty;
        int nameOrder = string.CompareOrdinal(leftName, rightName);
        return nameOrder != 0
            ? nameOrder
            : (left?.GetHashCode() ?? 0).CompareTo(
                right?.GetHashCode() ?? 0);
    }

    private int GetEnemyStableOrder(EnemyRuntime enemy)
    {
        if (enemy != null &&
            _circularEnemyStates.TryGetValue(
                enemy,
                out CircularEnemyState state) &&
            state != null)
        {
            return state.StableOrder;
        }

        return enemy != null &&
               _enemyPlacements.TryGetValue(
                   enemy,
                   out EnemyPlacement placement) &&
               placement.Anchor != null
            ? placement.Anchor.Row * GridSize +
              placement.Anchor.Column
            : int.MaxValue;
    }

    private static void TrimToMaximumCount<T>(
        List<T> values,
        int maximumCount)
    {
        if (values == null || maximumCount <= 0 ||
            values.Count <= maximumCount)
        {
            return;
        }

        values.RemoveRange(maximumCount, values.Count - maximumCount);
    }

    private void RemoveExpiredCoreAttackHistory()
    {
        if (_recentCoreAttackTimes.Count == 0)
            return;

        _circularEnemySnapshot.Clear();
        foreach (KeyValuePair<EnemyRuntime, float> entry in
                 _recentCoreAttackTimes)
        {
            if (entry.Key == null || entry.Key.Health <= 0 ||
                !_enemyPlacements.ContainsKey(entry.Key) ||
                _spatialBattleElapsedTime - entry.Value >
                    BattleSpatialDefaults.RecentCoreAttackWindow)
            {
                _circularEnemySnapshot.Add(entry.Key);
            }
        }

        foreach (EnemyRuntime enemy in _circularEnemySnapshot)
            _recentCoreAttackTimes.Remove(enemy);
        _circularEnemySnapshot.Clear();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private Vector2 GetWorldAllyPosition(IBattleCharacter character)
    {
        return character != null &&
               _worldAllyMovement.TryGetValue(
                   character,
                   out AllyMovementState movement)
            ? movement.Position
            : Vector2.zero;
    }

    private float GetWorldWallRadius()
    {
        return _arenaSetup.UsesBattleCore
            ? Mathf.Max(0.5f, _arenaSetup.WorldRadius)
            : worldSpawnRadius *
              (_arenaSetup.WallRadiusNormalized /
               Mathf.Max(
                   _arenaSetup.WallRadiusNormalized + 0.01f,
                   _arenaSetup.SpawnRadiusNormalized));
    }

    private float GetWorldSpawnRadius()
    {
        float authoredRadius = !_arenaSetup.UsesBattleCore
            ? worldSpawnRadius
            : GetWorldWallRadius() *
              (_arenaSetup.SpawnRadiusNormalized /
               Mathf.Max(
                   0.01f,
                   _arenaSetup.WallRadiusNormalized));
        return Mathf.Max(authoredRadius, _worldSpawnLineRadius);
    }

    private float GetWorldEnemyStopRadius(
        DungeonHudPresentationSO presentation)
    {
        float arenaRadius = GetWorldWallRadius();
        if (presentation == null)
            return arenaRadius;

        float gaugeOuterRadius = arenaRadius +
                                 presentation.BattleCoreRingGap +
                                 presentation.BattleCoreRingThickness * 0.5f;
        return gaugeOuterRadius +
               presentation.WorldEnemyArenaRingClearance;
    }

    private static Vector2 ToGround(Vector3 value)
    {
        return new Vector2(value.x, value.z);
    }

    private void HandleEnemyClicked(EnemyRuntime enemy)
    {
        if (enemy == null)
            return;

        _lastPracticeDebugEnemy = enemy;
        _lastPracticeDebugAlly = null;

        if (HandleManualEnemyClicked(enemy))
            return;

        if (_itemTargetHandler != null && _itemTargetHandler(enemy))
            return;

        EnemyClicked?.Invoke(enemy);
    }
}
