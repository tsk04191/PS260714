using UnityEngine;
using UnityEngine.UI;

public class DisplayData
{
    public const int DefaultFpsMode = 0;
    public const int DefaultDisplayMode = 1;

    private const int MinBrightnessPercent = 0;
    private const int MaxBrightnessPercent = 100;
    private const byte MinBrightnessOverlayAlpha = 0;
    private const byte MaxBrightnessOverlayAlpha = 254;
    private const int MaxFpsMode = 3;
    private const int MaxDisplayMode = 2;

    private Image imgBrightness
    { 
        get { return GameManager.Instance.Data.imgBrightness; }
        set { GameManager.Instance.Data.imgBrightness = value; }
    }

    public int brightness = 100;
    public int fps = 0;
    public int displayMode = DefaultDisplayMode;
    public string resolution;

    public void Init()
    {
        brightness = 100;
        fps = 0;
        displayMode = GetCurrentDisplayMode();
        resolution = GetCurrentResolution();
        Apply();
    }

    public void Save()
    {
        brightness = Mathf.Clamp(
            brightness,
            MinBrightnessPercent,
            MaxBrightnessPercent);
        fps = NormalizeFpsMode(fps);
        displayMode = NormalizeDisplayMode(displayMode);
        if (!TryNormalizeResolution(resolution, out resolution))
            resolution = GetCurrentResolution();

        PlayerPrefs.SetInt("Display.Brightness", brightness);
        PlayerPrefs.SetInt("Display.FPS", fps);
        PlayerPrefs.SetInt("Display.Mode", displayMode);
        PlayerPrefs.SetString("Display.Resolution", resolution);

        PlayerPrefs.Save();
        
        Apply();
    }

    public void Load()
    {
        brightness = Mathf.Clamp(
            PlayerPrefs.GetInt("Display.Brightness", brightness),
            MinBrightnessPercent,
            MaxBrightnessPercent);
        fps = NormalizeFpsMode(PlayerPrefs.GetInt("Display.FPS", fps));
        displayMode = NormalizeDisplayMode(PlayerPrefs.GetInt(
            "Display.Mode",
            GetCurrentDisplayMode()));

        string savedResolution = PlayerPrefs.GetString(
            "Display.Resolution",
            GetCurrentResolution());
        resolution = TryNormalizeResolution(savedResolution, out string normalizedResolution)
            ? normalizedResolution
            : GetCurrentResolution();
        
        Apply();
    }

    public void Apply()
    {
        ApplyBrightness(brightness);
        ApplyFPS(fps);
        ApplyResolution(resolution, displayMode);
    }

    public static bool IsValidFpsMode(int value)
    {
        return value >= DefaultFpsMode && value <= MaxFpsMode;
    }

    public static bool IsValidDisplayMode(int value)
    {
        return value >= 0 && value <= MaxDisplayMode;
    }

    public static bool TryNormalizeResolution(string value, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] parts = value.Split('x', 'X');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0].Trim(), out int width) ||
            !int.TryParse(parts[1].Trim(), out int height) ||
            width <= 0 || height <= 0)
        {
            return false;
        }

        normalized = $"{width} x {height}";
        return true;
    }

    public static string GetCurrentResolution()
    {
        return $"{Screen.width} x {Screen.height}";
    }

    private static int NormalizeFpsMode(int value)
    {
        return IsValidFpsMode(value) ? value : DefaultFpsMode;
    }

    private static int NormalizeDisplayMode(int value)
    {
        return IsValidDisplayMode(value) ? value : DefaultDisplayMode;
    }

    private static int GetCurrentDisplayMode()
    {
        return Screen.fullScreenMode switch
        {
            FullScreenMode.ExclusiveFullScreen => 0,
            FullScreenMode.FullScreenWindow => 1,
            _ => 2,
        };
    }

    private void ApplyBrightness(int value)
    {
        if (imgBrightness == null)
            return;

        float brightnessRatio = Mathf.InverseLerp(
            MinBrightnessPercent,
            MaxBrightnessPercent,
            Mathf.Clamp(value, MinBrightnessPercent, MaxBrightnessPercent));
        byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(
            MaxBrightnessOverlayAlpha,
            MinBrightnessOverlayAlpha,
            brightnessRatio));
        Color32 color = imgBrightness.color;
        color.a = alpha;
        imgBrightness.color = color;
    }

    private void ApplyFPS(int frame)
    {
        frame = NormalizeFpsMode(frame);

        switch (frame)
        {
            case 0:
                Application.targetFrameRate = -1;
                break;
            case 1:
                Application.targetFrameRate = 120;
                break;
            case 2:
                Application.targetFrameRate = 60;
                break;
            case 3:
                Application.targetFrameRate = 30;
                break;
        }
    }

    private static void ApplyResolution(string value, int mode)
    {
        if (!TryNormalizeResolution(value, out string normalized))
            return;

        string[] parts = normalized.Split('x');
        int width = int.Parse(parts[0].Trim());
        int height = int.Parse(parts[1].Trim());
        FullScreenMode fullScreenMode = mode switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed,
        };

        Screen.SetResolution(width, height, fullScreenMode);
    }
}
