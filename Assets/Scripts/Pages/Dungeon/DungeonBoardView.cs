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
    IBattleObjectiveProvider
{
    private const int MaximumStatusEventsPerDispatch = 128;
    private const int MaximumDefeatEventsPerDispatch = 128;
    private const int MaximumPresentationEventsPerDispatch = 256;
    private const float AuthoredArenaRingRadius = 2.24f;
    private const float MinimumWorldGroundSize = 40f;
    private const float WorldActorGroundHeight = 0f;
    public const int MinimumGridSize = 3;
    public const int MaximumGridSize = 9;

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
        public Vector2 SpawnDirection { get; }

        public CircularEnemyState(
            float attackInterval,
            Vector2 spawnDirection)
        {
            AttackTimeRemaining = Mathf.Max(0.1f, attackInterval);
            SpawnDirection = spawnDirection.sqrMagnitude > 0.0001f
                ? spawnDirection.normalized
                : Vector2.up;
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
    private readonly HashSet<EnemySO> _missingWorldEnemySpriteWarnings = new();
    private readonly HashSet<EnemyRuntime> _worldEnemySync = new();
    private Func<EnemyRuntime, bool> _itemTargetHandler;
    private IBattleCardDrawService _cardDrawService;
    private BattleManualTargetSelectionRequest _manualTargetRequest;
    private readonly List<EnemyRuntime> _manualEnemyTargets = new();
    private readonly List<IBattleCharacter> _manualAllyTargets = new();
    private BattleAreaPreviewView _areaPreview;
    private RenderTexture _runtimeWorldRenderTexture;
    private IBattleCharacter _selectedWorldAlly;
    private IBattleCharacter _pressedWorldAlly;
    private bool _draggingWorldAlly;
    private Vector2 _manualAreaOrigin;
    private Vector2 _manualAreaDirection = Vector2.up;
    private bool _manualAreaAnchorSet;
    private bool _manualAreaPointerDown;
    private EnemyRuntime _forcedPriorityTarget;
    private float _forcedPriorityRemaining;
    private float _worldSpawnLineRadius;
    private int _maximumStackSize = 8;
    private bool _initialized;
    private readonly Queue<BattleStatusAppliedEvent> _statusEventQueue = new();
    private bool _dispatchingStatusEvents;
    private readonly Queue<BattleEnemyDefeatedEvent> _defeatEventQueue = new();
    private bool _dispatchingDefeatEvents;
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

    public int GridSize { get; private set; } = MinimumGridSize;
    public float DungeonStageProgress { get; private set; }
    public RectTransform HighlightRect => boardRect != null
        ? boardRect
        : transform as RectTransform;
    public int InitialEnemyCapacity => _activeTileCount > 0
        ? _activeTileCount
        : GridSize * GridSize;
    public IBattleObjective Objective => _battleCore;
    public IBattleCardDrawService CardDrawService => _cardDrawService;
    public int LivingEnemyCount
    {
        get
        {
            int count = 0;
            for (int index = 0;
                 index < _tiles.Count && index < InitialEnemyCapacity;
                 index++)
            {
                DungeonBoardSlot tile = _tiles[index];
                if (tile != null)
                    count += tile.StackCount;
            }

            return count;
        }
    }
    public bool HasEmptyEnemyTile
    {
        get
        {
            for (int index = 0;
                 index < _tiles.Count && index < InitialEnemyCapacity;
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

    public bool TryBeginManualTargetSelection(
        BattleManualTargetSelectionRequest request)
    {
        if (request == null || request.Source == null ||
            request.RequiredCount <= 0 ||
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
            _manualAllyTargets.ToArray());
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
            gridSize = Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Sqrt(_arenaSetup.LaneCount)),
                MinimumGridSize,
                MaximumGridSize);
            stackSize = 1;
            _activeTileCount = Mathf.Min(
                _arenaSetup.LaneCount,
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

        size = Mathf.Clamp(size, MinimumGridSize, MaximumGridSize);
        _activeTileCount = _arenaSetup.UsesBattleCore
            ? Mathf.Min(_arenaSetup.LaneCount, size * size)
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
        EnemyPlacement placement = new(
            pending.Enemy,
            pending.Anchor,
            pending.OccupiedTiles,
            pending.IsExclusive);
        _enemyPlacements[pending.Enemy] = placement;
        if (_arenaSetup.UsesBattleCore)
        {
            _circularEnemyStates[pending.Enemy] =
                new CircularEnemyState(
                    pending.Enemy.CoreAttackInterval,
                    DungeonWorldSpawnGeometry.DirectionFromUnitSample(
                        Random.value));
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
        _circularEnemyStates.Remove(enemy);
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
            RefreshCircularLayout();
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
        if (!TryFindEnemyTile(enemy, out DungeonBoardSlot tile))
            return false;

        bool applied = TryApplyFireStatus(
            tile,
            null,
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
        if (TryGetForcedPriorityTile(out DungeonBoardSlot forcedTarget) &&
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

            totalDamage += TryDamageTile(
                tile,
                damage,
                damageType,
                source);
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
            totalHealed += tile.TryHealTop(amount);
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
                totalHealed += target.Heal(amount);
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
            totalGranted += tile.TryGrantShieldTop(amount);
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
                totalGranted += target.GainShield(amount);
        }

        return totalGranted;
    }

    public bool TryApplyCharacterStatus(
        IBattleCharacter source,
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
                source,
                tickInterval,
                TryDamageTile);
            if (targetApplied)
            {
                NotifyStatusApplied(new BattleStatusAppliedEvent(
                    BattleStatusTarget.FromEnemy(enemy),
                    statusEffect,
                    previousStacks,
                    enemy.GetStatusStackCount(statusEffect),
                    source));
            }
            if (targetApplied && showAttackRange &&
                ReferenceEquals(tile.TopEnemy, enemy))
                tile.ShowTargetArea();
            applied |= targetApplied;
        }

        return applied;
    }

    public bool TryApplyAlliedCharacterStatus(
        IBattleCharacter source,
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

            applied |= target.ApplyStatusEffect(
                statusEffect,
                duration,
                stackCount,
                source);
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
        IBattleCharacter source,
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
        bool applied = tile.TryApplyFireToTop(
            source,
            duration,
            tickInterval,
            tickDamage,
            TryDamageTile);
        if (applied && enemy != null)
        {
            NotifyStatusApplied(new BattleStatusAppliedEvent(
                BattleStatusTarget.FromEnemy(enemy),
                fire,
                previousStacks,
                enemy.GetStatusStackCount(fire),
                source));
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

        if (UsesWorldPresentation)
            TickWorldAllyMovement(deltaTime);
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

    private void TickCircularEnemies(float deltaTime)
    {
        if (!_battleCore.IsActive || _battleCore.IsDestroyed)
            return;

        float appliedDelta = Mathf.Max(0f, deltaTime);
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

            if (state.ApproachProgress < 1f)
            {
                state.ApproachProgress = Mathf.Clamp01(
                    state.ApproachProgress +
                    enemy.ApproachSpeed * appliedDelta /
                    Mathf.Max(
                        0.01f,
                        _arenaSetup.SpawnRadiusNormalized -
                        _arenaSetup.WallRadiusNormalized));
                continue;
            }

            state.AttackTimeRemaining -= appliedDelta;
            float attackInterval = Mathf.Max(
                0.1f,
                enemy.CoreAttackInterval);
            while (state.AttackTimeRemaining <= 0f &&
                   !_battleCore.IsDestroyed)
            {
                _battleCore.TakeDamage(enemy.CoreAttackDamage);
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
        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            if (!state.TickCooldown(
                    deltaTime,
                    source.AreAllActionsDisabled))
            {
                continue;
            }

            BattleEffectResult result = ExecuteCooldownAbility(
                sourceTile,
                source,
                state.Definition,
                characters);
            state.RecordActivation(result.Attempted, result.Succeeded);
        }
    }

    private BattleEffectResult ExecuteCooldownAbility(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        EnemyAbilityDefinition ability,
        IReadOnlyList<IBattleCharacter> characters)
    {
        if (ability == null ||
            ability.Trigger != EnemyAbilityTrigger.OnCooldown ||
            !TryResolveEnemyAbilityTargets(
                sourceTile,
                source,
                BattleAbilityRules.RequiresActionTargets(ability)
                    ? ability.Target
                    : null,
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

        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                this,
                targetFaction,
                enemyTargets,
                playerTargets);
        return ExecuteEffectOperations(ability, context);
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
            if (!state.CanActivate ||
                ability.Trigger != EnemyAbilityTrigger.OnSpawn ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    BattleAbilityRules.RequiresActionTargets(ability)
                        ? ability.Target
                        : null,
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
                ExecuteEffectOperations(ability, context);
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
                int granted = source.GainArmor(armor);
                combined = combined.Combine(
                    new BattleEffectResult(true, granted > 0));
            }

            state.RecordActivation(
                combined.Attempted,
                combined.Succeeded);
        }
    }

    private int ExecuteBeforeSelfDamageAbilities(
        DungeonBoardSlot sourceTile,
        EnemyRuntime source,
        int damage,
        CharacterAttackDamageType damageType)
    {
        if (source == null || source.AreAllActionsDisabled)
        {
            return damage;
        }

        foreach (EnemyAbilityRuntimeState state in source.AbilityStates)
        {
            EnemyAbilityDefinition ability = state.Definition;
            if (!state.CanActivate ||
                ability.Trigger !=
                    EnemyAbilityTrigger.BeforeSelfDamage ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    BattleAbilityRules.RequiresActionTargets(ability)
                        ? ability.Target
                        : null,
                    _battleCharacters,
                    out CharacterTargetFaction targetFaction,
                    out IReadOnlyList<EnemyRuntime> enemyTargets,
                    out IReadOnlyList<IBattleCharacter> playerTargets) ||
                !MatchesEnemyAbilityConditions(
                    ability,
                    source,
                    enemyTargets,
                    playerTargets,
                    damageType))
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
                ExecuteEffectOperations(ability, context);
            foreach (EnemyAbilityOperationDefinition operation in
                     ability.Operations)
            {
                if (operation == null || !operation.Enabled ||
                    operation.Type !=
                        EnemyAbilityOperationType.ModifyIncomingDamage)
                {
                    continue;
                }

                damage = Mathf.Max(0, operation.Amount);
                combined = combined.Combine(
                    new BattleEffectResult(true, true));
            }

            state.RecordActivation(
                combined.Attempted,
                combined.Succeeded);
        }

        return damage;
    }

    private DungeonBoardSlot FindModularDamageRedirect(
        DungeonBoardSlot targetTile,
        CharacterAttackDamageType damageType)
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
                if (!state.CanActivate ||
                    ability.Trigger !=
                        EnemyAbilityTrigger.BeforeAllyDamage)
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
                        damageType))
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
                    ExecuteEffectOperations(ability, context).Combine(
                        new BattleEffectResult(true, true));
                state.RecordActivation(
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
            if (!state.CanActivate ||
                ability.Trigger != EnemyAbilityTrigger.OnDeath ||
                !TryResolveEnemyAbilityTargets(
                    sourceTile,
                    source,
                    BattleAbilityRules.RequiresActionTargets(ability)
                        ? ability.Target
                        : null,
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
                ExecuteEffectOperations(ability, context);
            state.RecordActivation(
                combined.Attempted,
                combined.Succeeded);
        }
    }

    private static BattleEffectResult ExecuteEffectOperations(
        EnemyAbilityDefinition ability,
        BattleEffectContext context)
    {
        BattleEffectResult combined = default;
        foreach (EnemyAbilityOperationDefinition operation in
                 ability.Operations)
        {
            if (operation == null || !operation.Enabled ||
                operation.Type !=
                    EnemyAbilityOperationType.ExecuteEffects)
            {
                continue;
            }

            combined = combined.Combine(
                BattleEffectExecutor.ExecuteSequence(
                    context,
                    operation.Effects));
        }

        return combined;
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
            List<IBattleCharacter> candidates = new();
            if (characters != null)
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
            target.Subject == EnemyAbilityTargetSubject.Adjacent
                ? CollectAdjacentEnemyTargets(
                    sourceTile,
                    target.Range,
                    target.IncludeDiagonals)
                : CollectEnemyAbilityTargets(source);
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
        else
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
            total += Mathf.Max(0, status.StackCount);
        return total;
    }

    private static bool MatchesEnemyAbilityConditions(
        EnemyAbilityDefinition ability,
        EnemyRuntime source,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> playerTargets,
        CharacterAttackDamageType? incomingDamageType = null)
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
                incomingDamageType);
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
        CharacterAttackDamageType? incomingDamageType)
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
        UnbindAllPresentationEnemies();
        foreach (DungeonBoardSlot tile in _exclusiveOccupants.Keys)
        {
            if (tile != null)
                tile.SetExclusiveFootprintOccupant(null, false);
        }
        _exclusiveOccupants.Clear();
        _enemyPlacements.Clear();
        _circularEnemyStates.Clear();
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
        CancelManualTargetSelection();
        _forcedPriorityTarget = null;
        _forcedPriorityRemaining = 0f;
        _statusEventQueue.Clear();
        _defeatEventQueue.Clear();
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

        int laneCount = Mathf.Clamp(
            InitialEnemyCapacity,
            1,
            _tiles.Count);

        _worldEnemySync.Clear();

        for (int index = 0; index < _tiles.Count; index++)
        {
            DungeonBoardSlot tile = _tiles[index];
            if (tile == null)
                continue;

            bool active = index < laneCount;
            if (!active)
                continue;

            EnemyRuntime enemy = tile.TopEnemy;
            if (enemy != null &&
                _circularEnemyStates.TryGetValue(
                    enemy,
                    out CircularEnemyState state))
            {
                UpdateWorldEnemyView(
                    enemy,
                    state.SpawnDirection,
                    state.ApproachProgress,
                    index);
            }
        }

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
        Vector2 direction,
        float progress,
        int laneIndex)
    {
        if (!UsesWorldPresentation || enemy == null)
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

        float wallRadius = GetWorldEnemyStopRadius(presentation);
        float spawnRadius = Mathf.Max(
            GetWorldSpawnRadius(),
            wallRadius + presentation.WorldEnemyArenaRingClearance);
        float radius = Mathf.Lerp(
            spawnRadius,
            wallRadius,
            Mathf.Clamp01(progress));
        view.SetWorldPosition(new Vector3(
            direction.x * radius,
            0f,
            direction.y * radius));
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
        if (enemy == null ||
            !_worldEnemyActors.TryGetValue(enemy, out WorldActorView view))
        {
            return;
        }

        if (view?.GameObject != null)
            Destroy(view.GameObject);
        _worldEnemyActors.Remove(enemy);
        _worldEnemySync.Remove(enemy);
    }

    private void RemoveWorldAllyView(IBattleCharacter character)
    {
        if (character == null ||
            !_worldAllyActors.TryGetValue(character, out WorldActorView view))
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
    }

    private void ClearWorldAllyViews()
    {
        foreach (WorldActorView view in _worldAllyActors.Values)
        {
            if (view?.GameObject != null)
                Destroy(view.GameObject);
        }
        _worldAllyActors.Clear();
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
            index >= InitialEnemyCapacity)
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
        if (targetTile == null || targetTile.TopEnemy == null || damage <= 0)
            return 0;

        DungeonBoardSlot redirectTile =
            FindModularDamageRedirect(targetTile, damageType);
        DungeonBoardSlot damageReceiver = redirectTile != null
            ? redirectTile
            : targetTile;
        EnemyRuntime damagedEnemy = damageReceiver.TopEnemy;
        CaptureEnemyVfxAnchor(damagedEnemy, damageReceiver);
        damage = ExecuteBeforeSelfDamageAbilities(
            damageReceiver,
            damagedEnemy,
            damage,
            damageType);
        if (damage <= 0 || damagedEnemy.Health <= 0)
            return 0;

        int appliedDamage = damageReceiver.TryDamageTop(damage, damageType);
        if (appliedDamage > 0)
        {
            ShowEnemyHitFeedback(damageReceiver, damagedEnemy);
        }
        if (appliedDamage > 0 && damagedEnemy.Health <= 0)
        {
            ReleasePlacement(damagedEnemy, false);
            ExecuteDeathAbilities(damageReceiver, damagedEnemy);
            NotifyEnemyDefeated(new BattleEnemyDefeatedEvent(
                damagedEnemy,
                source));
            SynchronizeEnemyPresentationBindings();
            OccupancyChanged?.Invoke();
        }

        return appliedDamage;
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
            if (!state.CanActivate ||
                ability.Trigger !=
                    EnemyAbilityTrigger.OnTargetPriorityEvaluation ||
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
        GridSize = Mathf.Clamp(gridSize, MinimumGridSize, MaximumGridSize);
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
                if (_manualEnemyTargets.Count > 0 ||
                    _manualAllyTargets.Count > 0)
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
        RefreshManualTargetHighlights();
    }

    private void RequestWorldAllyMove(
        IBattleCharacter character,
        Vector2 destination)
    {
        if (character == null ||
            !_worldAllyMovement.TryGetValue(
                character,
                out AllyMovementState movement))
        {
            return;
        }

        float allowedRadius = Mathf.Max(
            0f,
            GetWorldWallRadius() - worldAllyBoundaryPadding);
        Vector2 resolved = BattleAreaGeometry.ClampToRadius(
            destination,
            Vector2.zero,
            allowedRadius);
        foreach (KeyValuePair<IBattleCharacter, AllyMovementState> entry in
                 _worldAllyMovement)
        {
            if (ReferenceEquals(entry.Key, character))
                continue;

            Vector2 otherDestination = entry.Value.Destination;
            Vector2 separation = resolved - otherDestination;
            float spacing = Mathf.Max(0f, worldAllyMinimumSpacing);
            if (separation.sqrMagnitude >= spacing * spacing)
                continue;

            Vector2 direction = separation.sqrMagnitude > 0.0001f
                ? separation.normalized
                : (movement.Position - otherDestination).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.right;
            resolved = otherDestination + direction * spacing;
            resolved = BattleAreaGeometry.ClampToRadius(
                resolved,
                Vector2.zero,
                allowedRadius);
        }

        movement.Destination = resolved;
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

        Vector3 world = view.InteractionWorldPosition;
        Vector3 viewport = worldCamera.WorldToViewportPoint(world);
        if (viewport.z <= 0f)
            return false;
        Rect bounds = rect.rect;
        Vector2 actorLocal = new(
            Mathf.Lerp(bounds.xMin, bounds.xMax, viewport.x),
            Mathf.Lerp(bounds.yMin, bounds.yMax, viewport.y));
        distance = Vector2.Distance(pointerLocal, actorLocal);
        return distance <= Mathf.Max(1f, worldActorHitRadiusPixels);
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

        if (HandleManualEnemyClicked(enemy))
            return;

        if (_itemTargetHandler != null && _itemTargetHandler(enemy))
            return;

        EnemyClicked?.Invoke(enemy);
    }
}

public sealed class BattleAreaPreviewView
{
    private readonly GameObject _root;
    private readonly DungeonBattleAreaPreviewPrefabView _view;

    public BattleAreaPreviewView(GameObject prefab, Transform parent)
    {
        _root = prefab != null && parent != null
            ? UnityEngine.Object.Instantiate(prefab, parent)
            : null;
        if (_root != null)
            SetLayerRecursively(_root, parent.gameObject.layer);
        _view = _root != null
            ? _root.GetComponent<DungeonBattleAreaPreviewPrefabView>()
            : null;
        if (_view == null || !_view.HasRequiredReferences)
        {
            Debug.LogError(
                "Dungeon battle area preview prefab references are incomplete.",
                _root);
        }
        _view?.Hide();
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public void Show(
        Vector2 origin,
        Vector2 direction,
        BattleAreaDefinition definition)
    {
        _view?.Show(origin, direction, definition);
    }

    public void Hide()
    {
        _view?.Hide();
    }

    public void Dispose()
    {
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
    }
}

public static class DungeonWorldSpawnGeometry
{
    public static float ResolveSpawnLineRadius(
        IReadOnlyList<Vector2> viewportGroundCorners,
        float padding)
    {
        float radius = 0f;
        if (viewportGroundCorners?.Count >= 4)
        {
            float bottomWidth = Vector2.Distance(
                viewportGroundCorners[0],
                viewportGroundCorners[2]);
            float topWidth = Vector2.Distance(
                viewportGroundCorners[1],
                viewportGroundCorners[3]);
            int leftIndex = topWidth >= bottomWidth ? 1 : 0;
            int rightIndex = topWidth >= bottomWidth ? 3 : 2;
            radius = Mathf.Max(
                viewportGroundCorners[leftIndex].magnitude,
                viewportGroundCorners[rightIndex].magnitude);
        }
        else if (viewportGroundCorners != null)
        {
            for (int index = 0;
                 index < viewportGroundCorners.Count;
                 index++)
            {
                radius = Mathf.Max(
                    radius,
                    viewportGroundCorners[index].magnitude);
            }
        }

        return radius + Mathf.Max(0f, padding);
    }

    public static Vector2 DirectionFromUnitSample(float sample)
    {
        float angle = Mathf.Clamp01(sample) * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
