using System;
using UnityEngine;

/// <summary>
/// Deletes only local player-owned save state. Project assets and
/// ScriptableObject definitions are never part of this operation.
/// </summary>
public static class LocalDataResetService
{
    public static bool IsResetInProgress { get; private set; }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        IsResetInProgress = false;
    }

    public static bool TryDeleteAllLocalData()
    {
        if (IsResetInProgress)
            return false;

        IsResetInProgress = true;
        try
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception exception)
        {
            IsResetInProgress = false;
            Debug.LogError(
                $"Failed to delete local data: {exception.Message}");
            return false;
        }
    }

    public static void ExitWithoutSaving()
    {
        if (!IsResetInProgress)
            return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
