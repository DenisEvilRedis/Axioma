using UnityEngine;

public static class DefaultFrameRateLimiter
{
    private const int MaxDefaultFps = 60;
    private const bool OverrideVSync = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyDefaultLimit()
    {
        if (OverrideVSync)
        {
            QualitySettings.vSyncCount = 0;
        }

        int currentTarget = Application.targetFrameRate;
        if (currentTarget <= 0 || currentTarget > MaxDefaultFps)
        {
            Application.targetFrameRate = MaxDefaultFps;
        }

#if UNITY_EDITOR
        Debug.Log($"[DefaultFrameRateLimiter] Target FPS = {Application.targetFrameRate} (vSyncCount={QualitySettings.vSyncCount})");
#endif
    }
}
