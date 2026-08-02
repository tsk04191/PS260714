using PS260714.Localization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonSpawnQueueItemView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI orderText;
    [SerializeField] private TextMeshProUGUI healthText;

    internal void ApplyGameDefaultFonts()
    {
        LocalizationFontResolver.ApplyGameDefault(orderText);
        LocalizationFontResolver.ApplyGameDefault(healthText);
    }

    public void Setup(int order, EnemyRuntime enemy)
    {
        if (orderText != null)
            orderText.text = $"#{Mathf.Max(1, order):00}";

        if (healthText != null)
        {
            healthText.text = enemy != null
                ? LocalizationService.Get(
                    LocalizationKeys.UiDungeonQueueEnemy,
                    LocalizationService.Arg(
                        "name",
                        EnemyLocalization.GetName(enemy.Definition)),
                    LocalizationService.Arg("health", enemy.Health))
                : LocalizationService.Get(
                    LocalizationKeys.UiDungeonQueueEnemyEmpty);
        }
    }
}
