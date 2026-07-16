using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class EnemyCard : MonoBehaviour
{
    [SerializeField] private RectTransform cardShadow;
    [SerializeField] private RectTransform tileFace;
    [SerializeField] private TMP_Text healthText;

    private RectTransform _rectTransform;
    private Image _tileFaceImage;
    private Color _defaultFaceColor;

    public EnemyRuntime Runtime { get; private set; }
    public RectTransform RectTransform =>
        _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

    public void Bind(EnemyRuntime runtime)
    {
        if (runtime == null)
        {
            Debug.LogError("EnemyCard requires an enemy runtime.", this);
            return;
        }

        Runtime = runtime;
        RefreshHealth();
    }

    public void RefreshHealth()
    {
        if (healthText != null && Runtime != null)
        {
            string typeCode = Runtime.Definition.CardCode;
            string health = Runtime.Armor > 0
                ? $"{Runtime.Health} A{Runtime.Armor}"
                : Runtime.Health.ToString();
            healthText.text = string.IsNullOrEmpty(typeCode)
                ? health
                : $"{typeCode} {health}";
        }

        RefreshStatus();
    }

    public void RefreshStatus()
    {
        CacheFaceImage();
        if (_tileFaceImage == null || Runtime == null)
            return;

        _tileFaceImage.color = _defaultFaceColor;
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
