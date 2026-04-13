using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EndingSequencePlayer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private Image targetImage;
    [SerializeField] private Image fadeBlack;

    [Header("Audio")]
    [SerializeField] private AudioSource musicSource;

    [Header("Timing")]
    [Min(0.1f)][SerializeField] private float fallbackSecondsPerSlide = 3f;
    [Min(0f)][SerializeField] private float fadeToBlackSeconds = 0.25f;
    [Min(0f)][SerializeField] private float fadeFromBlackSeconds = 0.25f;

    [Header("Input")]
    [SerializeField] private bool allowEscapeToExit = true;

    [Header("Endings")]
    [SerializeField] private List<EndingSequenceDefinition> sequences = new List<EndingSequenceDefinition>();

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool _isPlaying;
    private float _elapsed;
    private float _sequenceDuration;
    private float _secondsPerSlide;
    private int _currentSlideIndex;
    private EndingSequenceDefinition _activeSequence;
    private Action _onFinished;

    private enum FadeState
    {
        None = 0,
        FadingToBlack = 1,
        FadingFromBlack = 2
    }

    private FadeState _fadeState = FadeState.None;
    private float _fadeElapsed;
    private Sprite _pendingSlide;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        if (presentationRoot != null)
        {
            presentationRoot.SetActive(false);
        }

        if (fadeBlack != null)
        {
            SetFadeAlpha(0f);
        }
    }

    private void Update()
    {
        if (!_isPlaying)
        {
            return;
        }

        if (allowEscapeToExit && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (debugLogs) Debug.Log("[EndingSequencePlayer] Escape pressed. Exiting ending sequence.", this);
            FinishSequence(invokeCallback: true);
            return;
        }

        if (_fadeState != FadeState.None)
        {
            TickFade();
            return;
        }

        _elapsed += Time.unscaledDeltaTime;

        if (ShouldAdvanceSlide())
        {
            BeginNextSlide();
            return;
        }

        if (HasSequenceFinished())
        {
            FinishSequence(invokeCallback: true);
        }
    }

    public bool PlayEndingByIndex(int endingIndex, Action onFinished = null)
    {
        EndingSequenceDefinition sequence = GetSequence(endingIndex);
        if (sequence == null)
        {
            return false;
        }

        return Play(sequence, onFinished);
    }

    public bool Play(EndingSequenceDefinition sequence, Action onFinished = null)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[EndingSequencePlayer] Sequence is null.", this);
            return false;
        }

        if (targetImage == null)
        {
            Debug.LogWarning("[EndingSequencePlayer] targetImage is not assigned.", this);
            return false;
        }

        if (sequence.slides == null || sequence.slides.Count == 0)
        {
            Debug.LogWarning($"[EndingSequencePlayer] Sequence '{sequence.displayName}' has no slides.", this);
            return false;
        }

        Stop();

        _activeSequence = sequence;
        _onFinished = onFinished;
        _isPlaying = true;
        _elapsed = 0f;
        _currentSlideIndex = 0;
        _pendingSlide = null;
        _fadeState = FadeState.None;
        _fadeElapsed = 0f;

        _secondsPerSlide = CalculateSecondsPerSlide(sequence);
        _sequenceDuration = CalculateSequenceDuration(sequence, _secondsPerSlide);

        if (presentationRoot != null)
        {
            presentationRoot.SetActive(true);
        }

        targetImage.sprite = sequence.slides[0];
        targetImage.color = Color.white;

        if (fadeBlack != null)
        {
            SetFadeAlpha(0f);
        }

        if (musicSource != null)
        {
            musicSource.clip = sequence.song;
            musicSource.loop = false;

            if (sequence.song != null)
            {
                musicSource.Play();
            }
            else
            {
                musicSource.Stop();
            }
        }

        if (debugLogs)
        {
            Debug.Log($"[EndingSequencePlayer] Play '{sequence.displayName}' ({sequence.endingIndex}) with {_secondsPerSlide:0.###} sec/slide.", this);
        }

        return true;
    }

    public void Stop()
    {
        FinishSequence(invokeCallback: false);
    }

    public EndingSequenceDefinition GetSequence(int endingIndex)
    {
        for (int i = 0; i < sequences.Count; i++)
        {
            EndingSequenceDefinition sequence = sequences[i];
            if (sequence != null && sequence.endingIndex == endingIndex)
            {
                return sequence;
            }
        }

        return null;
    }

    private bool ShouldAdvanceSlide()
    {
        if (_activeSequence == null || _activeSequence.slides == null)
        {
            return false;
        }

        if (_currentSlideIndex >= _activeSequence.slides.Count - 1)
        {
            return false;
        }

        float nextSlideTime = (_currentSlideIndex + 1) * _secondsPerSlide;
        return _elapsed >= nextSlideTime;
    }

    private bool HasSequenceFinished()
    {
        if (_elapsed < _sequenceDuration)
        {
            return false;
        }

        if (musicSource != null && musicSource.clip != null && musicSource.isPlaying)
        {
            return false;
        }

        return true;
    }

    private void BeginNextSlide()
    {
        if (_activeSequence == null || _activeSequence.slides == null)
        {
            return;
        }

        int nextIndex = _currentSlideIndex + 1;
        if (nextIndex < 0 || nextIndex >= _activeSequence.slides.Count)
        {
            return;
        }

        Sprite nextSlide = _activeSequence.slides[nextIndex];
        if (nextSlide == null)
        {
            _currentSlideIndex = nextIndex;
            return;
        }

        if (fadeBlack == null || (fadeToBlackSeconds <= 0f && fadeFromBlackSeconds <= 0f))
        {
            targetImage.sprite = nextSlide;
            _currentSlideIndex = nextIndex;
            return;
        }

        _pendingSlide = nextSlide;
        _currentSlideIndex = nextIndex;
        _fadeState = FadeState.FadingToBlack;
        _fadeElapsed = 0f;
    }

    private void TickFade()
    {
        if (fadeBlack == null)
        {
            _fadeState = FadeState.None;
            return;
        }

        if (_fadeState == FadeState.FadingToBlack)
        {
            float duration = Mathf.Max(0.0001f, fadeToBlackSeconds);
            _fadeElapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(_fadeElapsed / duration);
            SetFadeAlpha(alpha);

            if (alpha >= 1f)
            {
                if (_pendingSlide != null)
                {
                    targetImage.sprite = _pendingSlide;
                }

                _pendingSlide = null;
                _fadeState = FadeState.FadingFromBlack;
                _fadeElapsed = 0f;
            }

            return;
        }

        float fadeOutDuration = Mathf.Max(0.0001f, fadeFromBlackSeconds);
        _fadeElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_fadeElapsed / fadeOutDuration);
        SetFadeAlpha(1f - t);

        if (t >= 1f)
        {
            SetFadeAlpha(0f);
            _fadeState = FadeState.None;
            _fadeElapsed = 0f;
        }
    }

    private void FinishSequence(bool invokeCallback)
    {
        if (!_isPlaying && !invokeCallback)
        {
            return;
        }

        _isPlaying = false;
        _elapsed = 0f;
        _sequenceDuration = 0f;
        _secondsPerSlide = 0f;
        _currentSlideIndex = 0;
        _activeSequence = null;
        _pendingSlide = null;
        _fadeState = FadeState.None;
        _fadeElapsed = 0f;

        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = null;
        }

        if (fadeBlack != null)
        {
            SetFadeAlpha(0f);
        }

        if (presentationRoot != null)
        {
            presentationRoot.SetActive(false);
        }

        Action callback = _onFinished;
        _onFinished = null;

        if (invokeCallback)
        {
            callback?.Invoke();
        }
    }

    private float CalculateSecondsPerSlide(EndingSequenceDefinition sequence)
    {
        if (sequence.song != null && sequence.slides != null && sequence.slides.Count > 0)
        {
            return Mathf.Max(0.1f, sequence.song.length / sequence.slides.Count);
        }

        return Mathf.Max(0.1f, sequence.manualSecondsPerSlide > 0f ? sequence.manualSecondsPerSlide : fallbackSecondsPerSlide);
    }

    private static float CalculateSequenceDuration(EndingSequenceDefinition sequence, float secondsPerSlide)
    {
        if (sequence.song != null)
        {
            return Mathf.Max(0.1f, sequence.song.length);
        }

        int slideCount = sequence.slides == null ? 0 : sequence.slides.Count;
        return Mathf.Max(0.1f, slideCount * secondsPerSlide);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeBlack == null)
        {
            return;
        }

        Color color = fadeBlack.color;
        color.a = alpha;
        fadeBlack.color = color;
    }

    [Serializable]
    public sealed class EndingSequenceDefinition
    {
        [Min(1)] public int endingIndex = 1;
        public string displayName = "Ending";
        public AudioClip song;
        [Min(0.1f)] public float manualSecondsPerSlide = 3f;
        public List<Sprite> slides = new List<Sprite>();
    }
}
