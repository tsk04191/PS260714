using UnityEngine;

[DisallowMultipleComponent]
public sealed class OperatorRosterDesignerSettings : MonoBehaviour
{
    [Header("Dynamic UI Prefabs")]
    [SerializeField] private GameObject cardPrefab;

    public GameObject CardPrefab => cardPrefab;
}
