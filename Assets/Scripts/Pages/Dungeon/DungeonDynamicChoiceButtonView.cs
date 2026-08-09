using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonDynamicChoiceButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI labelText;

    public void Bind(string label, bool interactable, Action action)
    {
        if (button == null || labelText == null)
        {
            Debug.LogError(
                "Dungeon choice button prefab references are incomplete.",
                this);
            return;
        }

        labelText.text = label ?? string.Empty;
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(() => action());
    }
}
