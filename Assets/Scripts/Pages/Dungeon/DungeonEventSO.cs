using UnityEngine;

[CreateAssetMenu(
    fileName = "DungeonEvent",
    menuName = "Dungeon/Event")]
public sealed class DungeonEventSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string eventId = "dungeon_event";
    [SerializeField] private string displayName = "DUNGEON EVENT";

    [Header("Presentation Override")]
    [SerializeField, Tooltip(
        "Optional music used while this event is active. When empty, the " +
        "dungeon's default Rest Clip is used.")]
    private AudioClip bgmOverride;

    public string EventId => eventId;
    public string DisplayName => displayName;
    public AudioClip BgmOverride => bgmOverride;

    private void OnValidate()
    {
        eventId = string.IsNullOrWhiteSpace(eventId)
            ? name.Trim().ToLowerInvariant().Replace(' ', '_')
            : eventId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? name.ToUpperInvariant()
            : displayName.Trim();
    }
}
