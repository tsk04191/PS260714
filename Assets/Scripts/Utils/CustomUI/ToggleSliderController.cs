using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ToggleSliderController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image imgHandle;
    [SerializeField] private Image imgFill;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float animationDuration = 0.15f;
    [SerializeField, Min(0f)] private float padding = 2f;

    [Header("Color")]
    [SerializeField] private Color clrFill;

    [Header("Info")]
    [SerializeField] private bool value;

    public bool Value => value;

    private bool isAniRun;
    private bool isInitialized;
    private bool isRefreshingLayout;
    private RectTransform toggleRect;
    private RectTransform handleRect;
    private RectTransform fillRect;
    private DrivenRectTransformTracker drivenTracker;

    private void Awake()
    {
        CacheRectTransforms();
    }

    private void Start()
    {
        if (Application.IsPlaying(gameObject))
            Init();
    }

    private void OnEnable()
    {
        if (!Application.IsPlaying(gameObject))
        {
            RefreshEditorPreview();
            return;
        }

        if (!isInitialized)
            return;

        RefreshLayout();
        ApplyVisualImmediately(value);
    }

    private void OnDisable()
    {
        drivenTracker.Clear();
    }

    private void OnValidate()
    {
        animationDuration = Mathf.Max(0f, animationDuration);
        padding = Mathf.Max(0f, padding);

        if (!Application.IsPlaying(gameObject))
            RefreshEditorPreview();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isRefreshingLayout)
            return;

        if (!Application.IsPlaying(gameObject))
        {
            RefreshEditorPreview();
            return;
        }

        if (!isInitialized)
            return;

        RefreshLayout();

        if (!isAniRun)
            ApplyVisualImmediately(value);
    }

    public void OnClick()
    {
        if (isAniRun)
            return;

        SetValue(!value, true);
    }

    public void Init()
    {
        if (!CacheRectTransforms())
            return;

        imgFill.color = clrFill;
        RefreshLayout();
        ApplyVisualImmediately(value);
        isInitialized = true;
    }

    public void SetValue(bool newValue, bool animated = false)
    {
        if (isAniRun)
            return;

        bool hasChanged = value != newValue;
        value = newValue;

        if (!isInitialized)
            return;

        if (!animated || !hasChanged || animationDuration <= 0f)
        {
            RefreshLayout();
            ApplyVisualImmediately(value);
            return;
        }

        ToggleAniRun().Forget();
    }

    private async UniTask ToggleAniRun()
    {
        if (!CacheRectTransforms())
            return;

        isAniRun = true;

        try
        {
            RefreshLayout();

            float elapsedTime = 0f;
            Vector2 startPosition = handleRect.anchoredPosition;
            float startFill = imgFill.fillAmount;
            float targetFill = value ? 1f : 0f;
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsedTime / animationDuration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

                Vector2 currentPosition = handleRect.anchoredPosition;
                currentPosition.x = Mathf.Lerp(startPosition.x, GetHandleTargetX(value), easedProgress);
                handleRect.anchoredPosition = currentPosition;
                imgFill.fillAmount = Mathf.Lerp(startFill, targetFill, easedProgress);

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            RefreshLayout();
            ApplyVisualImmediately(value);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isAniRun = false;
        }
    }

    private bool CacheRectTransforms()
    {
        if (toggleRect == null)
            toggleRect = transform as RectTransform;

        if (handleRect == null && imgHandle != null)
            handleRect = imgHandle.rectTransform;

        if (fillRect == null && imgFill != null)
            fillRect = imgFill.rectTransform;

        return toggleRect != null && handleRect != null && fillRect != null;
    }

    private void RefreshLayout()
    {
        if (!CacheRectTransforms())
            return;

        isRefreshingLayout = true;

        drivenTracker.Clear();
        DrivenTransformProperties drivenProperties =
            DrivenTransformProperties.Anchors |
            DrivenTransformProperties.AnchoredPosition |
            DrivenTransformProperties.SizeDelta |
            DrivenTransformProperties.Pivot;
        drivenTracker.Add(this, handleRect, drivenProperties);
        drivenTracker.Add(this, fillRect, drivenProperties);

        float width = Mathf.Max(0f, toggleRect.rect.width);
        float height = Mathf.Max(0f, toggleRect.rect.height);
        float inset = Mathf.Clamp(padding, 0f, Mathf.Min(width, height) * 0.5f);
        float handleSize = Mathf.Max(0f, Mathf.Min(width, height) - inset * 2f);

        Vector2 center = new Vector2(0.5f, 0.5f);
        SetVector2IfChanged(() => handleRect.anchorMin, value => handleRect.anchorMin = value, center);
        SetVector2IfChanged(() => handleRect.anchorMax, value => handleRect.anchorMax = value, center);
        SetVector2IfChanged(() => handleRect.pivot, value => handleRect.pivot = value, center);
        SetSizeIfChanged(handleRect, RectTransform.Axis.Horizontal, handleSize);
        SetSizeIfChanged(handleRect, RectTransform.Axis.Vertical, handleSize);

        SetVector2IfChanged(() => fillRect.anchorMin, value => fillRect.anchorMin = value, Vector2.zero);
        SetVector2IfChanged(() => fillRect.anchorMax, value => fillRect.anchorMax = value, Vector2.one);
        SetVector2IfChanged(() => fillRect.pivot, value => fillRect.pivot = value, center);
        SetVector2IfChanged(() => fillRect.offsetMin, value => fillRect.offsetMin = value, new Vector2(inset, inset));
        SetVector2IfChanged(() => fillRect.offsetMax, value => fillRect.offsetMax = value, new Vector2(-inset, -inset));

        isRefreshingLayout = false;
    }

    private void ApplyVisualImmediately(bool targetValue)
    {
        if (!CacheRectTransforms())
            return;

        Vector2 position = handleRect.anchoredPosition;
        position.x = GetHandleTargetX(targetValue);
        position.y = 0f;
        SetVector2IfChanged(() => handleRect.anchoredPosition, value => handleRect.anchoredPosition = value, position);

        float targetFill = targetValue ? 1f : 0f;
        if (!Mathf.Approximately(imgFill.fillAmount, targetFill))
            imgFill.fillAmount = targetFill;
    }

    private float GetHandleTargetX(bool targetValue)
    {
        float width = Mathf.Max(0f, toggleRect.rect.width);
        float height = Mathf.Max(0f, toggleRect.rect.height);
        float inset = Mathf.Clamp(padding, 0f, Mathf.Min(width, height) * 0.5f);
        float travel = Mathf.Max(0f, (width - handleRect.rect.width) * 0.5f - inset);

        return targetValue ? travel : -travel;
    }

    private void RefreshEditorPreview()
    {
        if (!isActiveAndEnabled || !CacheRectTransforms())
            return;

        if (imgFill.color != clrFill)
            imgFill.color = clrFill;

        RefreshLayout();
        ApplyVisualImmediately(value);
    }

    private static void SetVector2IfChanged(Func<Vector2> getter, Action<Vector2> setter, Vector2 value)
    {
        if ((getter() - value).sqrMagnitude > 0.0001f)
            setter(value);
    }

    private static void SetSizeIfChanged(RectTransform target, RectTransform.Axis axis, float size)
    {
        float currentSize = axis == RectTransform.Axis.Horizontal ? target.rect.width : target.rect.height;

        if (!Mathf.Approximately(currentSize, size))
            target.SetSizeWithCurrentAnchors(axis, size);
    }
}
