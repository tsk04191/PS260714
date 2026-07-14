using UnityEngine;
using UnityEngine.UI;

public class DisplayData
{
    public const int DefaultFpsMode = 0;

    private const float MinBrightnessPercent = 0f;
    private const float MaxBrightnessPercent = 100f;
    private const int MaxFpsMode = 3;

    private int brightness_min = 10;
    private Image imgBrightness
    { 
        get { return GameManager.Instance.Data.imgBrightness; }
        set { GameManager.Instance.Data.imgBrightness = value; }
    }

    public int brightness = 100;
    public int fps = 0;

    public void Init()
    {
        brightness = 100;
        fps = 0;
        Apply();
    }

    public void Save()
    {
        fps = NormalizeFpsMode(fps);

        PlayerPrefs.SetInt("Display.Brightness", brightness);
        PlayerPrefs.SetInt("Display.FPS", fps);

        PlayerPrefs.Save();
        
        Apply();
    }

    public void Load()
    {
        brightness = PlayerPrefs.GetInt("Display.Brightness", brightness);
        fps = NormalizeFpsMode(PlayerPrefs.GetInt("Display.FPS", fps));
        
        Apply();
    }

    public void Apply()
    {
        ApplyBrightness(brightness);
        ApplyFPS(fps);
    }

    public static bool IsValidFpsMode(int value)
    {
        return value >= DefaultFpsMode && value <= MaxFpsMode;
    }

    private static int NormalizeFpsMode(int value)
    {
        return IsValidFpsMode(value) ? value : DefaultFpsMode;
    }

    private void ApplyBrightness(float brightness)
    {
        if (imgBrightness == null)
            return;

        float cappedBrightness = Mathf.Clamp(brightness, brightness_min, MaxBrightnessPercent);
        float alpha = 1f - Mathf.InverseLerp(MinBrightnessPercent, MaxBrightnessPercent, cappedBrightness);
        Color color = imgBrightness.color;
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
}
