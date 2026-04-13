// MenuManager.cs (New Input System)
// Overlay menu for a small VN.
// - Opens by Esc or by a small Menu button
// - Pauses game time (optional)
// - Routes buttons: Continue, Restart, Endings, Inspiration, Credits, Quit(+confirm)

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Reflection;

public class MenuManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private bool allowEsc = true;
    [Header("UI")]
    [SerializeField] private GameObject smallMenuButton;
    [SerializeField] private GameObject autoButton;

    [Header("Pause")]
    [Tooltip("If true, sets Time.timeScale=0 when menu is open.")]
    [SerializeField] private bool pauseWithTimeScale = true;
    [SerializeField] private bool pauseAudioListener = false; // optional

    [Header("Panels (enable/disable GameObjects)")]
    [SerializeField] private GameObject overlayMenuRoot;      // main menu overlay (dark bg + menu panel)
    [SerializeField] private GameObject endingsPanelRoot;     // "Ваши финалы" panel (inside overlay)
    [SerializeField] private GameObject inspirationPanelRoot; // "Вдохновиться" panel (inside overlay)
    [SerializeField] private GameObject controlsPanelRoot;     // "Управление" panel (inside overlay)
    [SerializeField] private GameObject creditsPanelRoot;     // "Авторы" panel (inside overlay)
    [SerializeField] private GameObject quitConfirmRoot;      // quit confirm dialog (inside overlay)

    [Header("Optional: Endings UI (3 images/buttons)")]
    [SerializeField] private Button endingBtn1;
    [SerializeField] private Button endingBtn2;
    [SerializeField] private Button endingBtn3;
    [SerializeField] private Image endingImg1;
    [SerializeField] private Image endingImg2;
    [SerializeField] private Image endingImg3;
    [SerializeField] private Color lockedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color unlockedColor = Color.white;

    [Header("Inspiration")]
    [Tooltip("If set, will Play() when Inspiration opens and Stop() when closes.")]
    [SerializeField] private AudioSource inspirationMusic;
    [Tooltip("Any script that has StartShow()/StopShow() methods can be assigned here.")]
    [SerializeField] private MonoBehaviour slideShowBehaviour;

    [Header("Continue behaviour")]
    [SerializeField] private bool continueJustCloses = true;

    [Header("Music")]
    [SerializeField] private string overlayMenuMusicCueId = "menu_ambient";

    [Header("VN bridge (optional)")]
    [Tooltip("Optional reference to your VN manager to restart / jump to endings.")]
    [SerializeField] private MonoBehaviour vnManager; // keep generic to avoid coupling

    // PlayerPrefs keys for unlocked endings
    private const string ENDING1 = "Ending_1";
    private const string ENDING2 = "Ending_2";
    private const string ENDING3 = "Ending_3";

    private bool _isOpen;
    private float _prevTimeScale = 1f;
    private bool _prevAudioPaused;
    private bool _inspirationSilenceOverrideActive;

    // small input debounce so Esc doesn't instantly reopen/close due to UI focus quirks
    private float _nextToggleTime;

    private void Awake()
    {
        ResolveVNManager();
        SetAllSubPanelsOff();
        SetMenuOpen(false, force: true);
        RefreshEndingsUI();
    }

    private void Update()
    {
        if (!allowEsc) return;

        // New Input System way:
        var kb = Keyboard.current;
        if (kb == null) return;

        if (Time.unscaledTime < _nextToggleTime) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            _nextToggleTime = Time.unscaledTime + 0.08f;

            // If some subpanel is open, Esc goes back to main menu (not closing everything)
            if (_isOpen && IsAnySubPanelOpen())
            {
                ShowMainMenuOnly();
                return;
            }

            ToggleMenu();
        }
    }

    // ---------- Public API (wire these to UI buttons) ----------

    public void OnSmallMenuButton() => ToggleMenu();

    public void OnContinue()
    {
        if (continueJustCloses)
        {
            CloseMenu();
            return;
        }

        CloseMenu();
    }

    public void OnRestart()
    {
        CloseMenu(resumePreviousMusic: false);

        if (TryCall(vnManager, "RestartNovel"))
        {
            return;
        }

        // Fallback: reload current Unity scene
        var active = SceneManager.GetActiveScene();
        UnpauseNow();
        SceneManager.LoadScene(active.buildIndex);
    }

    public void OnOpenEndings()
    {
        EnsureMenuOpen();
        SetAllSubPanelsOff();
        if (endingsPanelRoot) endingsPanelRoot.SetActive(true);
        if (quitConfirmRoot) quitConfirmRoot.SetActive(false);
        RefreshEndingsUI();
    }

    public void OnOpenInspiration()
    {
        EnsureMenuOpen();
        SetAllSubPanelsOff();
        if (inspirationPanelRoot) inspirationPanelRoot.SetActive(true);
        if (quitConfirmRoot) quitConfirmRoot.SetActive(false);

        if (inspirationMusic)
        {
            inspirationMusic.loop = true; // continues after song
            inspirationMusic.Play();
        }

        ActivateInspirationMusicOverride();
        TryCall(slideShowBehaviour, "StartShow");
    }

    public void OnCloseInspiration()
    {
        if (inspirationMusic) inspirationMusic.Stop();
        DeactivateInspirationMusicOverride(resumePreviousMusic: true);
        TryCall(slideShowBehaviour, "StopShow");
        ShowMainMenuOnly();
    }

    public void OnOpenControls()
    {
        EnsureMenuOpen();
        SetAllSubPanelsOff();
        if (controlsPanelRoot) controlsPanelRoot.SetActive(true);
        if (quitConfirmRoot) quitConfirmRoot.SetActive(false);
    }
    public void OnOpenCredits()
    {
        EnsureMenuOpen();
        SetAllSubPanelsOff();
        if (creditsPanelRoot) creditsPanelRoot.SetActive(true);
        if (quitConfirmRoot) quitConfirmRoot.SetActive(false);
    }

    public void OnBackToMenu() => ShowMainMenuOnly();

    public void OnQuit()
    {
        EnsureMenuOpen();
        if (quitConfirmRoot) quitConfirmRoot.SetActive(true);
    }

    public void OnQuitYes()
    {
        Debug.Log("QUIT YES pressed");
        UnpauseNow();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    public void OnQuitNo()
    {
        if (quitConfirmRoot) quitConfirmRoot.SetActive(false);
    }

    public void OnEnding1() => TryPlayEnding(1, ENDING1);
    public void OnEnding2() => TryPlayEnding(2, ENDING2);
    public void OnEnding3() => TryPlayEnding(3, ENDING3);

    // ---------- Menu open/close ----------

    public void ToggleMenu()
    {
        if (_isOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        EnsureMenuOpen();
        ShowMainMenuOnly();
    }

    public void CloseMenu()
    {
        CloseMenu(resumePreviousMusic: true);
    }

    public void CloseMenu(bool resumePreviousMusic)
    {
        // stop inspiration if it was open
        if (inspirationPanelRoot && inspirationPanelRoot.activeSelf)
        {
            if (inspirationMusic) inspirationMusic.Stop();
            DeactivateInspirationMusicOverride(resumePreviousMusic: false);
            TryCall(slideShowBehaviour, "StopShow");
        }

        SetMenuOpen(false, resumePreviousMusic: resumePreviousMusic);
        SetAllSubPanelsOff();
    }

    private void EnsureMenuOpen()
    {
        if (_isOpen) return;
        SetMenuOpen(true);
    }

    private void SetMenuOpen(bool open, bool force = false, bool resumePreviousMusic = true)
    {
        if (!force && _isOpen == open) return;
        _isOpen = open;

        if (open)
        {
            ApplyMusicOverride();
        }
        else
        {
            ReleaseMusicOverride(resumePreviousMusic);
        }

        if (overlayMenuRoot) overlayMenuRoot.SetActive(open);
        if (smallMenuButton) smallMenuButton.SetActive(!open);
        if (autoButton) autoButton.SetActive(!open);
        if (pauseWithTimeScale)
        {
            if (open)
            {
                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = (_prevTimeScale <= 0f) ? 1f : _prevTimeScale;
            }
        }

        if (pauseAudioListener)
        {
            if (open)
            {
                _prevAudioPaused = AudioListener.pause;
                AudioListener.pause = true;
            }
            else
            {
                AudioListener.pause = _prevAudioPaused;
            }
        }
    }

    private void ApplyMusicOverride()
    {
        if (string.IsNullOrWhiteSpace(overlayMenuMusicCueId))
        {
            return;
        }

        MusicManager.PushOverride(overlayMenuMusicCueId);
    }

    private void ReleaseMusicOverride(bool resumePreviousMusic)
    {
        MusicManager.PopOverride(resumePreviousMusic);
    }

    private void ActivateInspirationMusicOverride()
    {
        if (_inspirationSilenceOverrideActive)
        {
            return;
        }

        MusicManager.PushSilenceOverride();
        _inspirationSilenceOverrideActive = true;
    }

    private void DeactivateInspirationMusicOverride(bool resumePreviousMusic)
    {
        if (!_inspirationSilenceOverrideActive)
        {
            return;
        }

        MusicManager.PopOverride(resumePreviousMusic);
        _inspirationSilenceOverrideActive = false;
    }

    private void UnpauseNow()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // ---------- Panels helpers ----------

    private void SetAllSubPanelsOff()
    {
        if (endingsPanelRoot) endingsPanelRoot.SetActive(false);
        if (inspirationPanelRoot) inspirationPanelRoot.SetActive(false);
        if (creditsPanelRoot) creditsPanelRoot.SetActive(false);
        if (controlsPanelRoot) controlsPanelRoot.SetActive(false);
        if (quitConfirmRoot) quitConfirmRoot.SetActive(false);
    }

    private void ShowMainMenuOnly()
    {
        // stop inspiration if leaving it
        if (inspirationPanelRoot && inspirationPanelRoot.activeSelf)
        {
            if (inspirationMusic) inspirationMusic.Stop();
            DeactivateInspirationMusicOverride(resumePreviousMusic: true);
            TryCall(slideShowBehaviour, "StopShow");
        }

        SetAllSubPanelsOff();
        RefreshEndingsUI();
    }

    private bool IsAnySubPanelOpen()
    {
        return (endingsPanelRoot && endingsPanelRoot.activeSelf)
            || (inspirationPanelRoot && inspirationPanelRoot.activeSelf)
            || (creditsPanelRoot && creditsPanelRoot.activeSelf)
            || (controlsPanelRoot && controlsPanelRoot.activeSelf)
            || (quitConfirmRoot && quitConfirmRoot.activeSelf);
    }

    // ---------- Endings ----------

    private void RefreshEndingsUI()
    {
        bool e1 = PlayerPrefs.GetInt(ENDING1, 0) == 1;
        bool e2 = PlayerPrefs.GetInt(ENDING2, 0) == 1;
        bool e3 = PlayerPrefs.GetInt(ENDING3, 0) == 1;

        ApplyEndingState(endingBtn1, endingImg1, e1);
        ApplyEndingState(endingBtn2, endingImg2, e2);
        ApplyEndingState(endingBtn3, endingImg3, e3);
    }

    private void ApplyEndingState(Button btn, Image img, bool unlocked)
    {
        if (btn) btn.interactable = unlocked;
        if (img) img.color = unlocked ? unlockedColor : lockedColor;
    }

    private void TryPlayEnding(int endingIndex, string key)
    {
        bool unlocked = PlayerPrefs.GetInt(key, 0) == 1;
        if (!unlocked) return;

        if (TryPlayEndingViaBridge(endingIndex))
        {
            return;
        }

        Debug.LogWarning($"Ending {endingIndex} is unlocked, but vnManager.PlayEnding(int) not found. Assign vnManager or implement PlayEnding.");
    }

    private bool TryPlayEndingViaBridge(int endingIndex)
    {
        if (!vnManager)
        {
            ResolveVNManager();
        }

        if (!vnManager)
        {
            return false;
        }

        if (vnManager is VNManager concreteManager)
        {
            CloseMenu(resumePreviousMusic: false);
            concreteManager.PlayEnding(endingIndex);
            return true;
        }

        // Fallback for alternative manager implementations that keep the same public API.
        if (!HasMethod(vnManager, "PlayEnding", typeof(int)))
        {
            return false;
        }

        CloseMenu(resumePreviousMusic: false);
        return TryCall(vnManager, "PlayEnding", endingIndex);
    }

    // ---------- Reflection bridge (no hard dependency on your VN manager class) ----------

    private static bool TryCall(MonoBehaviour target, string methodName)
    {
        if (!target) return false;
        var m = target.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (m == null) return false;
        if (m.GetParameters().Length != 0) return false;
        m.Invoke(target, null);
        return true;
    }

    private static bool TryCall(MonoBehaviour target, string methodName, int arg0)
    {
        if (!target) return false;
        var m = target.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (m == null) return false;

        var p = m.GetParameters();
        if (p.Length != 1 || p[0].ParameterType != typeof(int)) return false;

        m.Invoke(target, new object[] { arg0 });
        return true;
    }

    private static bool HasMethod(MonoBehaviour target, string methodName, params System.Type[] parameterTypes)
    {
        if (!target) return false;

        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        return method != null;
    }

    private void ResolveVNManager()
    {
        if (IsUsableVNManager(vnManager))
        {
            return;
        }

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (!IsUsableVNManager(behaviour))
            {
                continue;
            }

            vnManager = behaviour;
            return;
        }
    }

    private static bool IsUsableVNManager(MonoBehaviour behaviour)
    {
        if (!behaviour || !behaviour.isActiveAndEnabled)
        {
            return false;
        }

        MethodInfo restart = behaviour.GetType().GetMethod("RestartNovel",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo playEnding = behaviour.GetType().GetMethod("PlayEnding",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (restart == null || playEnding == null)
        {
            return false;
        }

        ParameterInfo[] restartParams = restart.GetParameters();
        ParameterInfo[] endingParams = playEnding.GetParameters();

        return restartParams.Length == 0
            && endingParams.Length == 1
            && endingParams[0].ParameterType == typeof(int);
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Unlock all endings")]
    private void DebugUnlockAll()
    {
        PlayerPrefs.SetInt(ENDING1, 1);
        PlayerPrefs.SetInt(ENDING2, 1);
        PlayerPrefs.SetInt(ENDING3, 1);
        PlayerPrefs.Save();
        RefreshEndingsUI();
    }

    [ContextMenu("DEBUG: Lock all endings")]
    private void DebugLockAll()
    {
        PlayerPrefs.DeleteKey(ENDING1);
        PlayerPrefs.DeleteKey(ENDING2);
        PlayerPrefs.DeleteKey(ENDING3);
        PlayerPrefs.Save();
        RefreshEndingsUI();
    }
#endif
}
