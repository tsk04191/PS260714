using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RecruitRevealDesignerBindings : MonoBehaviour
{
    [Header("Fixed Scene UI")]
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backdrop;
    [SerializeField] private Button backdropButton;
    [SerializeField] private RectTransform rowsContainer;
    [SerializeField] private VerticalLayoutGroup rowsLayout;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI instruction;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipLabel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmLabel;
    [SerializeField] private List<RectTransform> resultRows = new();
    [SerializeField, HideInInspector] private int designerLayoutVersion;

    public RectTransform Root => root;
    public CanvasGroup CanvasGroup => canvasGroup;
    public Image Backdrop => backdrop;
    public Button BackdropButton => backdropButton;
    public RectTransform RowsContainer => rowsContainer;
    public VerticalLayoutGroup RowsLayout => rowsLayout;
    public TextMeshProUGUI Title => title;
    public TextMeshProUGUI Instruction => instruction;
    public Button SkipButton => skipButton;
    public TextMeshProUGUI SkipLabel => skipLabel;
    public Button ConfirmButton => confirmButton;
    public TextMeshProUGUI ConfirmLabel => confirmLabel;
    public IReadOnlyList<RectTransform> ResultRows => resultRows;
    public bool HasDesignerLayout => designerLayoutVersion > 0;

    public bool HasRequiredReferences =>
        root != null &&
        canvasGroup != null &&
        backdrop != null &&
        backdropButton != null &&
        rowsContainer != null &&
        rowsLayout != null &&
        title != null &&
        instruction != null &&
        skipButton != null &&
        skipLabel != null &&
        confirmButton != null &&
        confirmLabel != null &&
        resultRows != null &&
        resultRows.Count == 10 &&
        resultRows.TrueForAll(row => row != null);

    public bool CaptureReferencesFromHierarchy()
    {
        root = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        backdrop = GetComponent<Image>();
        backdropButton = GetComponent<Button>();
        rowsContainer = transform.Find("grpRevealRows") as RectTransform;
        rowsLayout = rowsContainer != null
            ? rowsContainer.GetComponent<VerticalLayoutGroup>()
            : null;
        title = transform.Find("txtRevealTitle")
            ?.GetComponent<TextMeshProUGUI>();
        instruction = transform.Find("txtRevealInstruction")
            ?.GetComponent<TextMeshProUGUI>();
        skipButton = transform.Find("btnRevealSkip")
            ?.GetComponent<Button>();
        skipLabel = skipButton != null
            ? skipButton.transform.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        confirmButton = transform.Find("btnRevealConfirm")
            ?.GetComponent<Button>();
        confirmLabel = confirmButton != null
            ? confirmButton.transform.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;

        resultRows.Clear();
        if (rowsContainer != null)
        {
            for (int index = 0; index < 10; index++)
            {
                resultRows.Add(
                    rowsContainer.Find(
                        $"grpRevealRow{index + 1:00}") as RectTransform);
            }
        }
        return HasRequiredReferences;
    }

#if UNITY_EDITOR
    public void MarkDesignerLayoutCurrent()
    {
        designerLayoutVersion = 1;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
