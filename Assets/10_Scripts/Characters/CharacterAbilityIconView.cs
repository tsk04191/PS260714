using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CharacterAbilityIconView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private CharacterRuntime _owner;
    private CharacterAbilityIconKind _kind;

    internal void Configure(
        CharacterRuntime owner,
        CharacterAbilityIconKind kind)
    {
        _owner = owner;
        _kind = kind;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowAbilityTooltip(_kind);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideAbilityTooltip(_kind);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_kind != CharacterAbilityIconKind.Active ||
            eventData == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        _owner?.HandleAbilityIconClick(_kind);
    }
}
