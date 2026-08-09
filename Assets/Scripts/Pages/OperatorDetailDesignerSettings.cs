using UnityEngine;

[DisallowMultipleComponent]
public sealed class OperatorDetailDesignerSettings : MonoBehaviour
{
    [Header("Dynamic UI Prefabs")]
    [SerializeField] private GameObject abilityIconPrefab;

    public GameObject AbilityIconPrefab => abilityIconPrefab;
}
