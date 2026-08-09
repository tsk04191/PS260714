using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DungeonStartingItemSlotView : MonoBehaviour
{
    [SerializeField] private DungeonItemCardView itemCard;
    [SerializeField] private DungeonDynamicChoiceButtonView rerollButton;

    public bool BindItem(BattleItemSO item)
    {
        if (itemCard == null)
        {
            Debug.LogError(
                "Starting item slot card reference is incomplete.",
                this);
            return false;
        }

        return itemCard.Initialize(item, null);
    }

    public void BindReroll(
        string label,
        bool interactable,
        Action action)
    {
        if (itemCard == null || rerollButton == null)
        {
            Debug.LogError(
                "Starting item slot prefab references are incomplete.",
                this);
            return;
        }

        rerollButton.Bind(label, interactable, action);
    }
}
