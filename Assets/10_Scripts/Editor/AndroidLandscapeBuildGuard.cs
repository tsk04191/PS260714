using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

internal static class AndroidLandscapeBuildSettings
{
    public static bool IsConfigured =>
        PlayerSettings.defaultInterfaceOrientation ==
        UIOrientation.AutoRotation &&
        !PlayerSettings.allowedAutorotateToPortrait &&
        !PlayerSettings.allowedAutorotateToPortraitUpsideDown &&
        PlayerSettings.allowedAutorotateToLandscapeRight &&
        PlayerSettings.allowedAutorotateToLandscapeLeft;

    public static void Apply()
    {
        PlayerSettings.defaultInterfaceOrientation =
            UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
    }
}

public sealed class AndroidLandscapeBuildGuard :
    IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        AndroidLandscapeBuildSettings.Apply();
        if (!AndroidLandscapeBuildSettings.IsConfigured)
        {
            throw new BuildFailedException(
                "Android builds require landscape-only orientation.");
        }
    }
}
