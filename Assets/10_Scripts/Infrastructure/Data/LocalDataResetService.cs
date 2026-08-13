using System;
using UnityEngine;

/// <summary>
/// Deletes only local player-owned save state. Project assets and
/// ScriptableObject definitions are never part of this operation.
/// </summary>
public static class LocalDataResetService
{
    internal const string LastInitializedBuildGuidPlayerPrefsKey =
        "System.LastInitializedBuildGuid";

    public static bool IsResetInProgress { get; private set; }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        IsResetInProgress = false;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void ResetForNewBuildIfEnabled()
    {
#if !UNITY_EDITOR
        bool resetEnabled = IsBuildResetEnabled();
        if (!resetEnabled ||
            !TryNormalizeBuildGuid(
                Application.buildGUID,
                out string currentBuildGuid))
        {
            if (resetEnabled)
            {
                Debug.LogError(
                    "Automatic local-data reset is enabled, but this build " +
                    "does not have a valid build GUID.");
            }
            return;
        }

        string previousBuildGuid = PlayerPrefs.GetString(
            LastInitializedBuildGuidPlayerPrefsKey,
            string.Empty);
        if (!ShouldResetForBuild(
                resetEnabled,
                previousBuildGuid,
                currentBuildGuid))
        {
            return;
        }

        TryResetForNewBuild(previousBuildGuid, currentBuildGuid);
#endif
    }

    private static bool IsBuildResetEnabled()
    {
        return CommonDef.ResetLocalDataOnNewBuild;
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

    internal static bool ShouldResetForBuild(
        bool enabled,
        string previousBuildGuid,
        string currentBuildGuid)
    {
        if (!enabled ||
            !TryNormalizeBuildGuid(
                currentBuildGuid,
                out string normalizedCurrent))
        {
            return false;
        }

        return !TryNormalizeBuildGuid(
                   previousBuildGuid,
                   out string normalizedPrevious) ||
               !string.Equals(
                   normalizedPrevious,
                   normalizedCurrent,
                   StringComparison.Ordinal);
    }

    internal static bool TryNormalizeBuildGuid(
        string value,
        out string normalized)
    {
        normalized = string.Empty;
        if (!Guid.TryParse(value, out Guid parsed) ||
            parsed == Guid.Empty)
        {
            return false;
        }

        normalized = parsed.ToString("N");
        return true;
    }

    private static bool TryResetForNewBuild(
        string previousBuildGuid,
        string currentBuildGuid)
    {
        try
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetString(
                LastInitializedBuildGuidPlayerPrefsKey,
                currentBuildGuid);
            PlayerPrefs.Save();
            Debug.Log(
                "Local player data was reset for a new test build " +
                $"('{previousBuildGuid}' -> '{currentBuildGuid}').");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Failed to reset local data for a new test build: " +
                exception.Message);
            return false;
        }
    }
}
