using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class DungeonWorldInputView : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler,
    IPointerMoveHandler
{
    private DungeonBoardView _owner;

    public void Bind(DungeonBoardView owner)
    {
        _owner = owner;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null)
            _owner?.HandleWorldPointerDown(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData != null)
            _owner?.HandleWorldPointerUp(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData != null)
            _owner?.HandleWorldPointerMove(eventData, true);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (eventData != null)
            _owner?.HandleWorldPointerMove(eventData, false);
    }
}
