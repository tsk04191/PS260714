using UnityEngine;

[DisallowMultipleComponent]
public sealed class OperatorRosterDesignerSettings : MonoBehaviour
{
    [Header("Dynamic UI Prefabs")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Role Filter State Colors")]
    [SerializeField] private Color selectedFilterBackground =
        new(0.12f, 0.39f, 0.36f, 1f);
    [SerializeField] private Color unselectedFilterBackground =
        new(0.055f, 0.075f, 0.071f, 1f);
    [SerializeField] private Color selectedFilterContent = Color.white;
    [SerializeField] private Color unselectedFilterContent =
        new(0.42f, 0.48f, 0.46f, 1f);

    public GameObject CardPrefab => cardPrefab;
    public Color SelectedFilterBackground => selectedFilterBackground;
    public Color UnselectedFilterBackground =>
        unselectedFilterBackground;
    public Color SelectedFilterContent => selectedFilterContent;
    public Color UnselectedFilterContent => unselectedFilterContent;
}
