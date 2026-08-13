using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("PS260714/UI/Page BGM Selection")]
public sealed class PageBgmSelection : MonoBehaviour
{
    [SerializeField]
    private string bgmClipName;

    public string BgmClipName => NormalizeClipName(bgmClipName);
    public bool HasSelection => !string.IsNullOrEmpty(BgmClipName);

    private void Start()
    {
        // PageControl handles normal navigation. Start covers the page that
        // is already active when the scene begins.
        RequestSelectedBgm();
    }

    public bool RequestSelectedBgm()
    {
        GameEventManager events = GameManager.Instance != null
            ? GameManager.Instance.Events
            : null;
        return RequestSelectedBgm(events);
    }

    public bool RequestSelectedBgm(GameEventManager events)
    {
        string clipName = BgmClipName;
        if (events == null || string.IsNullOrEmpty(clipName))
            return false;

        events.RequestBgm(clipName);
        return true;
    }

    private static string NormalizeClipName(string clipName)
    {
        return string.IsNullOrWhiteSpace(clipName)
            ? string.Empty
            : clipName.Trim();
    }
}
