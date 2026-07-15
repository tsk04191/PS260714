using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonEnemyCard : MonoBehaviour
{
    [SerializeField] private RectTransform cardShadow;
    [SerializeField] private RectTransform tileFace;
    [SerializeField] private TMP_Text healthText;

    private RectTransform _rectTransform;

    public DungeonEnemyData Enemy { get; private set; }
    public RectTransform RectTransform =>
        _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

    public void Setup(DungeonEnemyData enemy)
    {
        if (enemy == null)
        {
            Debug.LogError("DungeonEnemyCard requires enemy data.", this);
            return;
        }

        Enemy = enemy;
        RefreshHealth();
    }

    public void RefreshHealth()
    {
        if (healthText != null && Enemy != null)
            healthText.text = Enemy.Health.ToString();
    }

    public void ApplyLayout(float edge, float sideDepth)
    {
        if (cardShadow != null)
        {
            cardShadow.offsetMin = new Vector2(edge * 2f, -edge * 2.5f);
            cardShadow.offsetMax = new Vector2(edge * 2f, -edge * 2.5f);
        }

        if (tileFace != null)
        {
            tileFace.offsetMin = new Vector2(edge, sideDepth);
            tileFace.offsetMax = new Vector2(-edge, -edge);
        }
    }
}
