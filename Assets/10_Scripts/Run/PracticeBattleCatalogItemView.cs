using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PracticeBattleCatalogItemView : MonoBehaviour
{
    [SerializeField] private Button actionButton;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI idText;
    [SerializeField] private TextMeshProUGUI actionText;

    private Action _clicked;

    public bool HasDesignerReferences =>
        actionButton != null &&
        background != null &&
        icon != null &&
        nameText != null &&
        idText != null &&
        actionText != null;

    public string EntryId => idText != null ? idText.text : string.Empty;

    public bool Initialize(
        Sprite entryIcon,
        string displayName,
        string entryId,
        string actionLabel,
        Action clicked)
    {
        if (!HasDesignerReferences || clicked == null)
        {
            Debug.LogError(
                "Practice battle catalog item references are incomplete.",
                this);
            return false;
        }

        Unbind();
        _clicked = clicked;
        icon.sprite = entryIcon;
        icon.enabled = entryIcon != null;
        nameText.text = displayName ?? string.Empty;
        idText.text = entryId ?? string.Empty;
        actionText.text = actionLabel ?? string.Empty;
        actionButton.onClick.AddListener(HandleClicked);
        return true;
    }

    public void SetInteractable(bool interactable)
    {
        if (actionButton != null)
            actionButton.interactable = interactable;
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void HandleClicked()
    {
        _clicked?.Invoke();
    }

    private void Unbind()
    {
        if (actionButton != null)
            actionButton.onClick.RemoveListener(HandleClicked);
        _clicked = null;
    }
}
