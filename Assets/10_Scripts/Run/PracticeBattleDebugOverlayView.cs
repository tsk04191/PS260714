using UnityEngine;

[DisallowMultipleComponent]
public sealed class PracticeBattleDebugOverlayView : MonoBehaviour
{
    [SerializeField]
    private PracticeBattleDebugOverlayGraphic overlayGraphic;

    [Header("Click Bounds")]
    [SerializeField]
    private Color allyClickColor = new(0.15f, 0.9f, 1f, 0.95f);
    [SerializeField]
    private Color enemyClickColor = new(1f, 0.3f, 0.25f, 0.95f);
    [SerializeField, Min(0.5f)]
    private float clickLineWidth = 2.5f;

    [Header("World Bounds")]
    [SerializeField]
    private Color allySpacingColor = new(0.2f, 0.55f, 1f, 0.85f);
    [SerializeField]
    private Color enemyFormationColor = new(1f, 0.58f, 0.12f, 0.85f);
    [SerializeField]
    private Color abilityRangeColor = new(1f, 0.9f, 0.15f, 0.95f);
    [SerializeField]
    private Color coreReachColor = new(1f, 0.2f, 0.85f, 0.95f);
    [SerializeField, Min(0.5f)]
    private float worldLineWidth = 2f;

    private bool _visible;

    public bool HasRequiredReferences => overlayGraphic != null;
    public bool IsVisible => _visible &&
                             isActiveAndEnabled &&
                             overlayGraphic != null &&
                             overlayGraphic.isActiveAndEnabled;
    public int PrimitiveCount => overlayGraphic != null
        ? overlayGraphic.PrimitiveCount
        : 0;

    private void Awake()
    {
        if (overlayGraphic != null)
            overlayGraphic.raycastTarget = false;
        SetVisible(false);
    }

    private void OnDisable()
    {
        _visible = false;
        if (overlayGraphic != null)
            overlayGraphic.enabled = false;
        Clear();
    }

    public void SetVisible(bool visible)
    {
        _visible = visible && HasRequiredReferences;
        if (overlayGraphic == null)
            return;

        overlayGraphic.raycastTarget = false;
        overlayGraphic.enabled = _visible;
        if (!_visible)
            overlayGraphic.Clear();
    }

    public void BeginFrame()
    {
        if (!IsVisible)
            return;

        overlayGraphic.BeginFrame();
    }

    public void AddInputCircle(
        RectTransform inputRect,
        Vector2 inputCenter,
        float inputRadius,
        PracticeBattleDebugPrimitiveKind kind)
    {
        if (!IsVisible || inputRect == null || inputRadius <= 0f ||
            !TryConvertInputPoint(
                inputRect,
                inputCenter,
                out Vector2 center) ||
            !TryConvertInputPoint(
                inputRect,
                inputCenter + Vector2.right * inputRadius,
                out Vector2 right) ||
            !TryConvertInputPoint(
                inputRect,
                inputCenter + Vector2.up * inputRadius,
                out Vector2 up))
        {
            return;
        }

        overlayGraphic.AddCircle(
            center,
            right - center,
            up - center,
            ResolveWidth(kind),
            ResolveColor(kind));
    }

    public void AddInputLine(
        RectTransform inputRect,
        Vector2 inputStart,
        Vector2 inputEnd,
        PracticeBattleDebugPrimitiveKind kind)
    {
        if (!IsVisible || inputRect == null ||
            !TryConvertInputPoint(
                inputRect,
                inputStart,
                out Vector2 start) ||
            !TryConvertInputPoint(
                inputRect,
                inputEnd,
                out Vector2 end))
        {
            return;
        }

        overlayGraphic.AddLine(
            start,
            end,
            ResolveWidth(kind),
            ResolveColor(kind));
    }

    public void EndFrame()
    {
        if (IsVisible)
            overlayGraphic.EndFrame();
    }

    public void Clear()
    {
        overlayGraphic?.Clear();
    }

    private bool TryConvertInputPoint(
        RectTransform inputRect,
        Vector2 inputLocal,
        out Vector2 overlayLocal)
    {
        overlayLocal = default;
        if (overlayGraphic == null || inputRect == null)
            return false;

        Vector3 world = inputRect.TransformPoint(inputLocal);
        Vector3 local = overlayGraphic.rectTransform.InverseTransformPoint(
            world);
        if (float.IsNaN(local.x) || float.IsInfinity(local.x) ||
            float.IsNaN(local.y) || float.IsInfinity(local.y))
        {
            return false;
        }

        overlayLocal = new Vector2(local.x, local.y);
        return true;
    }

    private Color ResolveColor(PracticeBattleDebugPrimitiveKind kind)
    {
        return kind switch
        {
            PracticeBattleDebugPrimitiveKind.AllyClick => allyClickColor,
            PracticeBattleDebugPrimitiveKind.EnemyClick => enemyClickColor,
            PracticeBattleDebugPrimitiveKind.AllySpacing =>
                allySpacingColor,
            PracticeBattleDebugPrimitiveKind.EnemyFormation =>
                enemyFormationColor,
            PracticeBattleDebugPrimitiveKind.AbilityRange =>
                abilityRangeColor,
            PracticeBattleDebugPrimitiveKind.CoreReach => coreReachColor,
            _ => Color.white,
        };
    }

    private float ResolveWidth(PracticeBattleDebugPrimitiveKind kind)
    {
        return kind == PracticeBattleDebugPrimitiveKind.AllyClick ||
               kind == PracticeBattleDebugPrimitiveKind.EnemyClick
            ? Mathf.Max(0.5f, clickLineWidth)
            : Mathf.Max(0.5f, worldLineWidth);
    }
}
