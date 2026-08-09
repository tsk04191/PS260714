using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class DungeonRewardCardHoverView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private bool _hovered;

    private void Update()
    {
        Vector3 targetScale = _hovered
            ? new Vector3(1.03f, 1.03f, 1f)
            : Vector3.one;
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
    }

    private void OnDisable()
    {
        _hovered = false;
        transform.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
    }
}
