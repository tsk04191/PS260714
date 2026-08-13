using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CharacterInfoHoverView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private CharacterRuntime _owner;

    internal void Configure(CharacterRuntime owner)
    {
        _owner = owner;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowAbilityTooltip(
            CharacterAbilityIconKind.Details);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideAbilityTooltip(
            CharacterAbilityIconKind.Details);
    }
}
