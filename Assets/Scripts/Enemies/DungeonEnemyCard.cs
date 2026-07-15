using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonEnemyCard : MonoBehaviour
{
    [SerializeField] private RectTransform cardShadow;
    [SerializeField] private RectTransform tileFace;
    [SerializeField] private TMP_Text healthText;

    private RectTransform _rectTransform;
    private Image _tileFaceImage;
    private Color _defaultFaceColor;

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
        {
            string typeCode = EnemyTypeDisplay.GetCardCode(Enemy.Type);
            healthText.text = string.IsNullOrEmpty(typeCode)
                ? Enemy.Health.ToString()
                : $"{typeCode} {Enemy.Health}";
        }

        RefreshStatus();
    }

    public void RefreshStatus()
    {
        CacheFaceImage();
        if (_tileFaceImage == null || Enemy == null)
            return;

        _tileFaceImage.color = Enemy.HasFire
            ? new Color(0.58f, 0.19f, 0.06f, 1f)
            : _defaultFaceColor;
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

    private void CacheFaceImage()
    {
        if (_tileFaceImage != null || tileFace == null)
            return;

        _tileFaceImage = tileFace.GetComponent<Image>();
        if (_tileFaceImage == null)
            return;

        _defaultFaceColor = _tileFaceImage.color;
    }
}
