using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonSpawnQueueItemView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI orderText;
    [SerializeField] private TextMeshProUGUI healthText;

    public void Setup(int order, DungeonEnemyData enemy)
    {
        if (orderText != null)
            orderText.text = $"#{Mathf.Max(1, order):00}";

        if (healthText != null)
            healthText.text = enemy != null ? $"HP {enemy.Health}" : "HP -";
    }
}
