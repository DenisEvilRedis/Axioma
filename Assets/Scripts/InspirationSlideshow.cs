// InspirationSlideshow.cs
// Slideshow with "cinematic fade" using a black overlay Image.
// Works with Time.timeScale = 0 (uses unscaledDeltaTime).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InspirationSlideshow : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image targetImage;   // BG image
    [SerializeField] private Image fadeBlack;     // black overlay (alpha animates 0..1)

    [Header("Sprites")]
    [SerializeField] private List<Sprite> backgrounds = new List<Sprite>();

    [Header("Timing")]
    [Min(0.1f)][SerializeField] private float secondsPerSlide = 4f;

    [Header("Fade")]
    [Min(0f)][SerializeField] private float fadeToBlackSeconds = 0.25f;
    [Min(0f)][SerializeField] private float fadeFromBlackSeconds = 0.25f;

    [Header("Random")]
    [SerializeField] private bool avoidImmediateRepeat = true;
    [SerializeField] private bool useShuffleBag = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool _running;
    private float _timer;

    private int _lastIndex = -1;

    // Shuffle-bag
    private readonly List<int> _bag = new List<int>();
    private int _bagPos = 0;

    // Fade state machine
    private enum FadeState { None, FadingToBlack, FadingFromBlack }
    private FadeState _fadeState = FadeState.None;
    private float _fadeT = 0f;
    private Sprite _pendingSprite;

    private void Reset()
    {
        // Try auto-find common setup (optional)
        targetImage = GetComponentInChildren<Image>();
    }

    private void Awake()
    {
        if (fadeBlack) SetFadeAlpha(0f);
    }

    private void Update()
    {
        if (!_running) return;
        if (!targetImage) return;
        if (backgrounds == null || backgrounds.Count == 0) return;

        // Fade animation (unscaled)
        if (_fadeState != FadeState.None && fadeBlack)
        {
            TickFade();
            return; // while fading, we don't advance timer
        }

        _timer += Time.unscaledDeltaTime;
        if (_timer >= secondsPerSlide)
        {
            _timer = 0f;
            BeginNextSlide();
        }
    }

    // Called by MenuManager
    public void StartShow()
    {
        if (_running) return;

        if (!targetImage)
        {
            Debug.LogWarning("[InspirationSlideshow] targetImage is not set.");
            return;
        }

        if (backgrounds == null || backgrounds.Count == 0)
        {
            Debug.LogWarning("[InspirationSlideshow] backgrounds list is empty.");
            return;
        }

        _running = true;
        _timer = 0f;

        if (fadeBlack) SetFadeAlpha(0f);

        if (useShuffleBag) BuildBag();

        // show first immediately
        int idx = PickNextIndex();
        if (idx >= 0 && idx < backgrounds.Count && backgrounds[idx])
        {
            targetImage.sprite = backgrounds[idx];
            _lastIndex = idx;
        }

        if (debugLogs) Debug.Log("[InspirationSlideshow] StartShow");
    }

    // Called by MenuManager
    public void StopShow()
    {
        if (!_running) return;

        _running = false;
        _timer = 0f;

        _fadeState = FadeState.None;
        _fadeT = 0f;
        _pendingSprite = null;

        if (fadeBlack) SetFadeAlpha(0f);

        if (debugLogs) Debug.Log("[InspirationSlideshow] StopShow");
    }

    // ---- Slide change with fade ----

    private void BeginNextSlide()
    {
        int idx = PickNextIndex();
        if (idx < 0 || idx >= backgrounds.Count) return;

        var sprite = backgrounds[idx];
        if (!sprite) return;

        // If no fadeBlack, just swap
        if (!fadeBlack || (fadeToBlackSeconds <= 0f && fadeFromBlackSeconds <= 0f))
        {
            targetImage.sprite = sprite;
            _lastIndex = idx;
            return;
        }

        _pendingSprite = sprite;
        _fadeState = FadeState.FadingToBlack;
        _fadeT = 0f;

        if (debugLogs) Debug.Log($"[InspirationSlideshow] FadeToBlack -> next {sprite.name}");
        _lastIndex = idx;
    }

    private void TickFade()
    {
        if (!fadeBlack) { _fadeState = FadeState.None; return; }

        if (_fadeState == FadeState.FadingToBlack)
        {
            float dur = Mathf.Max(0.0001f, fadeToBlackSeconds);
            _fadeT += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(_fadeT / dur);
            SetFadeAlpha(a);

            if (a >= 1f)
            {
                // swap at full black
                if (_pendingSprite) targetImage.sprite = _pendingSprite;
                _pendingSprite = null;

                _fadeState = FadeState.FadingFromBlack;
                _fadeT = 0f;

                if (debugLogs) Debug.Log("[InspirationSlideshow] Swap @ black, FadeFromBlack");
            }
        }
        else if (_fadeState == FadeState.FadingFromBlack)
        {
            float dur = Mathf.Max(0.0001f, fadeFromBlackSeconds);
            _fadeT += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_fadeT / dur);
            float a = 1f - t;
            SetFadeAlpha(a);

            if (t >= 1f)
            {
                SetFadeAlpha(0f);
                _fadeState = FadeState.None;
                _fadeT = 0f;
            }
        }
    }

    private void SetFadeAlpha(float a)
    {
        var c = fadeBlack.color;
        c.a = a;
        fadeBlack.color = c;
    }

    // ---- Random picking ----

    private int PickNextIndex()
    {
        int count = backgrounds.Count;
        if (count <= 0) return -1;
        if (count == 1) return 0;

        if (useShuffleBag)
        {
            if (_bag.Count != count || _bagPos >= _bag.Count)
                BuildBag();

            int idx = _bag[_bagPos++];

            if (avoidImmediateRepeat && idx == _lastIndex)
            {
                if (_bagPos < _bag.Count) idx = _bag[_bagPos++];
                else { BuildBag(); idx = _bag[_bagPos++]; }
            }

            return idx;
        }
        else
        {
            int idx = Random.Range(0, count);
            if (avoidImmediateRepeat)
            {
                int guard = 0;
                while (idx == _lastIndex && guard++ < 10)
                    idx = Random.Range(0, count);
            }
            return idx;
        }
    }

    private void BuildBag()
    {
        int count = backgrounds.Count;

        _bag.Clear();
        for (int i = 0; i < count; i++)
            _bag.Add(i);

        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
        }

        _bagPos = 0;

        if (avoidImmediateRepeat && count > 1 && _lastIndex >= 0 && _bag[0] == _lastIndex)
        {
            int swapWith = Random.Range(1, count);
            (_bag[0], _bag[swapWith]) = (_bag[swapWith], _bag[0]);
        }
    }
}