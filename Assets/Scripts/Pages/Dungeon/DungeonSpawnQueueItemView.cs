using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonSpawnQueueItemView : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private Image accent;
    [SerializeField] private TextMeshProUGUI orderText;
    [SerializeField] private TextMeshProUGUI healthText;

    internal void ApplyGameDefaultFonts()
    {
        LocalizationFontResolver.ApplyGameDefault(orderText);
        LocalizationFontResolver.ApplyGameDefault(healthText);
    }

    public void Setup(int order, EnemyRuntime enemy)
    {
        if (portrait != null)
        {
            Sprite sprite = enemy?.Definition != null
                ? enemy.Definition.IconSprite
                : null;
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
        }

        if (accent != null)
        {
            accent.color = order <= 1
                ? new Color(0.12f, 0.86f, 0.94f, 0.95f)
                : new Color(0.34f, 0.48f, 0.54f, 0.72f);
        }

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
