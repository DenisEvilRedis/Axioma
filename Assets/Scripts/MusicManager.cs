using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource templateSource;
    [SerializeField] private AudioSource primarySource;
    [SerializeField] private AudioSource secondarySource;
    [SerializeField] private bool dontDestroyOnLoad;

    [Header("Timing")]
    [Min(0f)][SerializeField] private float fallbackStopFadeSeconds = 0.75f;
    [Min(0f)][SerializeField] private float defaultDuckFadeSeconds = 0.35f;

    [Header("Cues")]
    [SerializeField] private MusicCue[] cues;

    private readonly Dictionary<string, MusicCue> _cueMap =
        new Dictionary<string, MusicCue>(StringComparer.OrdinalIgnoreCase);

    private MusicChannel _channelA;
    private MusicChannel _channelB;
    private MusicChannel _activeChannel;
    private MusicChannel _inactiveChannel;
    private MusicCue _currentCue;
    private Coroutine _transitionRoutine;
    private Coroutine _duckRoutine;
    private float _duckMultiplier = 1f;
    private bool _initialized;
    private readonly Stack<SuspendedPlaybackState> _overrideStack = new Stack<SuspendedPlaybackState>();

    public static string CurrentCueId => Instance != null && Instance._currentCue != null
        ? Instance._currentCue.id
        : string.Empty;

    private void Awake()
    {
        if (!TryBecomeInstance())
        {
            return;
        }

        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        RebuildCueCache();
    }

    [ContextMenu("Rebuild Music Cue Cache")]
    public void RebuildCueCache()
    {
        _cueMap.Clear();

        if (cues == null)
        {
            return;
        }

        for (int i = 0; i < cues.Length; i++)
        {
            MusicCue cue = cues[i];
            if (cue == null || string.IsNullOrWhiteSpace(cue.id))
            {
                continue;
            }

            string key = cue.id.Trim();
            cue.id = key;

            if (_cueMap.ContainsKey(key))
            {
                continue;
            }

            _cueMap.Add(key, cue);
        }
    }

    public static bool Play(string cueId)
    {
        MusicManager manager = GetOrFindInstance();
        return manager != null && manager.PlayCueInternal(cueId);
    }

    public static bool Stop(float fadeOutSeconds = -1f)
    {
        MusicManager manager = GetOrFindInstance();
        return manager != null && manager.StopInternal(fadeOutSeconds);
    }

    public static void SetDuck(float multiplier, float fadeSeconds = -1f)
    {
        MusicManager manager = GetOrFindInstance();
        if (manager == null)
        {
            return;
        }

        manager.SetDuckInternal(multiplier, fadeSeconds);
    }

    public static void ClearDuck(float fadeSeconds = -1f)
    {
        SetDuck(1f, fadeSeconds);
    }

    public static bool PushOverride(string cueId)
    {
        MusicManager manager = GetOrFindInstance();
        return manager != null && manager.PushOverrideInternal(cueId);
    }

    public static bool PushSilenceOverride()
    {
        MusicManager manager = GetOrFindInstance();
        return manager != null && manager.PushSilenceOverrideInternal();
    }

    public static bool PopOverride(bool resumePrevious = true)
    {
        MusicManager manager = GetOrFindInstance();
        return manager != null && manager.PopOverrideInternal(resumePrevious);
    }

    public bool PlayCue(string cueId)
    {
        return PlayCueInternal(cueId);
    }

    public bool StopMusic(float fadeOutSeconds = -1f)
    {
        return StopInternal(fadeOutSeconds);
    }

    public void SetDuckMultiplier(float multiplier, float fadeSeconds = -1f)
    {
        SetDuckInternal(multiplier, fadeSeconds);
    }

    private static MusicManager GetOrFindInstance()
    {
        if (Instance != null)
        {
            Instance.EnsureInitialized();
            return Instance;
        }

        MusicManager manager = FindFirstObjectByType<MusicManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            return null;
        }

        if (!manager.TryBecomeInstance())
        {
            return Instance;
        }

        manager.EnsureInitialized();
        return manager;
    }

    private bool TryBecomeInstance()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return false;
        }

        Instance = this;
        return true;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        templateSource ??= GetComponent<AudioSource>();

        if (primarySource == null)
        {
            primarySource = CreateMusicSource();
        }

        if (secondarySource == null)
        {
            secondarySource = CreateMusicSource();
        }

        _channelA = new MusicChannel(primarySource);
        _channelB = new MusicChannel(secondarySource);
        _activeChannel = _channelA;
        _inactiveChannel = _channelB;

        ResetChannel(_channelA);
        ResetChannel(_channelB);
        RebuildCueCache();

        _duckMultiplier = 1f;
        _initialized = true;
    }

    private AudioSource CreateMusicSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        CopyAudioSourceSettings(templateSource, source);
        source.playOnAwake = false;
        source.loop = true;
        source.clip = null;
        source.volume = 0f;
        return source;
    }

    private bool PlayCueInternal(string cueId)
    {
        EnsureInitialized();

        if (!TryGetCue(cueId, out MusicCue cue) || cue.clip == null)
        {
            return false;
        }

        if (_currentCue != null && string.Equals(_currentCue.id, cue.id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return StartTransitionToCue(cue, startTimeSeconds: 0f);
    }

    private bool PushOverrideInternal(string cueId)
    {
        EnsureInitialized();

        if (!TryGetCue(cueId, out MusicCue cue) || cue.clip == null)
        {
            return false;
        }

        if (_currentCue != null && string.Equals(_currentCue.id, cue.id, StringComparison.OrdinalIgnoreCase))
        {
            _overrideStack.Push(SuspendedPlaybackState.CreateNoChange());
            return true;
        }

        _overrideStack.Push(_currentCue != null
            ? SuspendedPlaybackState.CreateResumeCue(_currentCue, GetPlaybackTime(_activeChannel?.Source))
            : SuspendedPlaybackState.CreateResumeSilence());

        return StartTransitionToCue(cue, startTimeSeconds: 0f);
    }

    private bool PushSilenceOverrideInternal()
    {
        EnsureInitialized();

        if (_currentCue == null)
        {
            _overrideStack.Push(SuspendedPlaybackState.CreateNoChange());
            return true;
        }

        _overrideStack.Push(SuspendedPlaybackState.CreateResumeCue(_currentCue, GetPlaybackTime(_activeChannel?.Source)));
        return StopInternal(_currentCue != null
            ? Mathf.Max(0f, _currentCue.transitionSeconds)
            : fallbackStopFadeSeconds);
    }

    private bool PopOverrideInternal(bool resumePrevious)
    {
        EnsureInitialized();

        if (_overrideStack.Count == 0)
        {
            return false;
        }

        SuspendedPlaybackState suspendedPlayback = _overrideStack.Pop();

        if (!resumePrevious || suspendedPlayback == null || suspendedPlayback.NoAudibleChange)
        {
            return true;
        }

        if (suspendedPlayback.ResumeToSilence)
        {
            return StopInternal(_currentCue != null
                ? Mathf.Max(0f, _currentCue.transitionSeconds)
                : fallbackStopFadeSeconds);
        }

        if (suspendedPlayback.Cue == null || suspendedPlayback.Cue.clip == null)
        {
            return false;
        }

        if (_currentCue != null
            && string.Equals(_currentCue.id, suspendedPlayback.Cue.id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return StartTransitionToCue(suspendedPlayback.Cue, suspendedPlayback.TimeSeconds);
    }

    private bool StartTransitionToCue(MusicCue cue, float startTimeSeconds)
    {
        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        MusicChannel previousChannel = HasAudioContent(_activeChannel) ? _activeChannel : null;
        MusicChannel nextChannel = previousChannel == _channelA ? _channelB : _channelA;

        if (nextChannel == null)
        {
            return false;
        }

        PrepareChannel(nextChannel, cue, startTimeSeconds);

        _currentCue = cue;
        _activeChannel = nextChannel;
        _inactiveChannel = previousChannel ?? GetOtherChannel(nextChannel);

        float transitionSeconds = Mathf.Max(0f, cue.transitionSeconds);
        if (transitionSeconds <= 0f)
        {
            CompleteImmediateTransition(previousChannel, nextChannel);
            return true;
        }

        _transitionRoutine = StartCoroutine(TransitionRoutine(previousChannel, nextChannel, cue, transitionSeconds));
        return true;
    }

    private bool StopInternal(float fadeOutSeconds)
    {
        EnsureInitialized();

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        if (!HasPlayback(_channelA) && !HasPlayback(_channelB) && _currentCue == null)
        {
            return false;
        }

        float duration = fadeOutSeconds >= 0f
            ? fadeOutSeconds
            : _currentCue != null
                ? Mathf.Max(0f, _currentCue.transitionSeconds)
                : Mathf.Max(0f, fallbackStopFadeSeconds);

        if (duration <= 0f)
        {
            _currentCue = null;
            ResetChannel(_channelA);
            ResetChannel(_channelB);
            _activeChannel = _channelA;
            _inactiveChannel = _channelB;
            return true;
        }

        _transitionRoutine = StartCoroutine(FadeOutRoutine(duration));
        return true;
    }

    private void SetDuckInternal(float multiplier, float fadeSeconds)
    {
        EnsureInitialized();

        float targetMultiplier = Mathf.Clamp01(multiplier);
        float duration = fadeSeconds >= 0f ? fadeSeconds : defaultDuckFadeSeconds;

        if (_duckRoutine != null)
        {
            StopCoroutine(_duckRoutine);
            _duckRoutine = null;
        }

        if (duration <= 0f)
        {
            _duckMultiplier = targetMultiplier;
            RefreshChannelVolumes();
            return;
        }

        _duckRoutine = StartCoroutine(DuckRoutine(targetMultiplier, duration));
    }

    private IEnumerator TransitionRoutine(
        MusicChannel previousChannel,
        MusicChannel nextChannel,
        MusicCue nextCue,
        float duration)
    {
        float previousStartWeight = previousChannel != null ? Mathf.Clamp01(previousChannel.Weight) : 0f;
        float nextStartWeight = Mathf.Clamp01(nextChannel.Weight);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (nextCue.transitionMode == MusicTransitionMode.FadeOutIn && previousChannel != null)
            {
                if (t < 0.5f)
                {
                    float localT = t / 0.5f;
                    previousChannel.Weight = Mathf.Lerp(previousStartWeight, 0f, localT);
                    nextChannel.Weight = 0f;
                }
                else
                {
                    float localT = (t - 0.5f) / 0.5f;
                    previousChannel.Weight = 0f;
                    nextChannel.Weight = Mathf.Lerp(0f, 1f, localT);
                }
            }
            else
            {
                if (previousChannel != null)
                {
                    previousChannel.Weight = Mathf.Lerp(previousStartWeight, 0f, t);
                }

                nextChannel.Weight = Mathf.Lerp(nextStartWeight, 1f, t);
            }

            RefreshChannelVolumes();
            yield return null;
        }

        CompleteImmediateTransition(previousChannel, nextChannel);
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startWeightA = _channelA != null ? Mathf.Clamp01(_channelA.Weight) : 0f;
        float startWeightB = _channelB != null ? Mathf.Clamp01(_channelB.Weight) : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (_channelA != null)
            {
                _channelA.Weight = Mathf.Lerp(startWeightA, 0f, t);
            }

            if (_channelB != null)
            {
                _channelB.Weight = Mathf.Lerp(startWeightB, 0f, t);
            }

            RefreshChannelVolumes();
            yield return null;
        }

        _currentCue = null;
        ResetChannel(_channelA);
        ResetChannel(_channelB);
        _activeChannel = _channelA;
        _inactiveChannel = _channelB;
        _transitionRoutine = null;
    }

    private IEnumerator DuckRoutine(float targetMultiplier, float duration)
    {
        float startMultiplier = _duckMultiplier;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _duckMultiplier = Mathf.Lerp(startMultiplier, targetMultiplier, t);
            RefreshChannelVolumes();
            yield return null;
        }

        _duckMultiplier = targetMultiplier;
        RefreshChannelVolumes();
        _duckRoutine = null;
    }

    private void CompleteImmediateTransition(MusicChannel previousChannel, MusicChannel nextChannel)
    {
        if (previousChannel != null)
        {
            previousChannel.Weight = 0f;
        }

        nextChannel.Weight = 1f;
        RefreshChannelVolumes();

        if (previousChannel != null)
        {
            ResetChannel(previousChannel);
        }

        _activeChannel = nextChannel;
        _inactiveChannel = GetOtherChannel(nextChannel);
        _transitionRoutine = null;
    }

    private void PrepareChannel(MusicChannel channel, MusicCue cue, float startTimeSeconds)
    {
        if (channel == null || channel.Source == null)
        {
            return;
        }

        AudioSource source = channel.Source;
        source.Stop();
        source.clip = cue.clip;
        source.loop = cue.loop;
        source.time = GetNormalizedTime(cue.clip, startTimeSeconds);
        source.volume = 0f;
        source.Play();

        channel.BaseVolume = Mathf.Clamp01(cue.volume);
        channel.Weight = 0f;
    }

    private void ResetChannel(MusicChannel channel)
    {
        if (channel == null || channel.Source == null)
        {
            return;
        }

        channel.Source.Stop();
        channel.Source.clip = null;
        channel.Source.volume = 0f;
        channel.Weight = 0f;
        channel.BaseVolume = 0f;
    }

    private void RefreshChannelVolumes()
    {
        ApplyChannelVolume(_channelA);
        ApplyChannelVolume(_channelB);
    }

    private void ApplyChannelVolume(MusicChannel channel)
    {
        if (channel == null || channel.Source == null)
        {
            return;
        }

        channel.Source.volume = Mathf.Clamp01(channel.BaseVolume * Mathf.Clamp01(channel.Weight) * _duckMultiplier);
    }

    private MusicChannel GetOtherChannel(MusicChannel channel)
    {
        if (ReferenceEquals(channel, _channelA))
        {
            return _channelB;
        }

        return _channelA;
    }

    private bool TryGetCue(string cueId, out MusicCue cue)
    {
        cue = null;

        if (string.IsNullOrWhiteSpace(cueId))
        {
            return false;
        }

        if (_cueMap.Count == 0)
        {
            RebuildCueCache();
        }

        return _cueMap.TryGetValue(cueId.Trim(), out cue);
    }

    private static bool HasPlayback(MusicChannel channel)
    {
        return channel != null
            && channel.Source != null
            && channel.Source.clip != null
            && channel.Source.isPlaying;
    }

    private static bool HasAudioContent(MusicChannel channel)
    {
        return channel != null
            && channel.Source != null
            && channel.Source.clip != null;
    }

    private static float GetPlaybackTime(AudioSource source)
    {
        if (source == null || source.clip == null)
        {
            return 0f;
        }

        return source.time;
    }

    private static float GetNormalizedTime(AudioClip clip, float timeSeconds)
    {
        if (clip == null || clip.length <= 0f)
        {
            return 0f;
        }

        return Mathf.Repeat(Mathf.Max(0f, timeSeconds), clip.length);
    }

    private static void CopyAudioSourceSettings(AudioSource source, AudioSource destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.playOnAwake = false;
        destination.loop = false;
        destination.outputAudioMixerGroup = source != null ? source.outputAudioMixerGroup : null;
        destination.priority = source != null ? source.priority : 128;
        destination.pitch = source != null ? source.pitch : 1f;
        destination.panStereo = source != null ? source.panStereo : 0f;
        destination.spatialBlend = source != null ? source.spatialBlend : 0f;
        destination.reverbZoneMix = source != null ? source.reverbZoneMix : 1f;
        destination.dopplerLevel = source != null ? source.dopplerLevel : 1f;
        destination.spread = source != null ? source.spread : 0f;
        destination.minDistance = source != null ? source.minDistance : 1f;
        destination.maxDistance = source != null ? source.maxDistance : 500f;
        destination.rolloffMode = source != null ? source.rolloffMode : AudioRolloffMode.Logarithmic;
        destination.mute = source != null && source.mute;
        destination.bypassEffects = source != null && source.bypassEffects;
        destination.bypassListenerEffects = source != null && source.bypassListenerEffects;
        destination.bypassReverbZones = source != null && source.bypassReverbZones;
        destination.ignoreListenerPause = source != null && source.ignoreListenerPause;
        destination.ignoreListenerVolume = source != null && source.ignoreListenerVolume;
        destination.velocityUpdateMode = source != null ? source.velocityUpdateMode : AudioVelocityUpdateMode.Auto;
    }

    [Serializable]
    public sealed class MusicCue
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float transitionSeconds = 2f;
        public MusicTransitionMode transitionMode = MusicTransitionMode.Crossfade;
        public bool loop = true;
    }

    [Serializable]
    public enum MusicTransitionMode
    {
        Crossfade = 0,
        FadeOutIn = 1,
    }

    private sealed class MusicChannel
    {
        public MusicChannel(AudioSource source)
        {
            Source = source;
        }

        public AudioSource Source { get; }
        public float BaseVolume { get; set; }
        public float Weight { get; set; }
    }

    private sealed class SuspendedPlaybackState
    {
        public MusicCue Cue { get; private set; }
        public float TimeSeconds { get; private set; }
        public bool ResumeToSilence { get; private set; }
        public bool NoAudibleChange { get; private set; }

        public static SuspendedPlaybackState CreateResumeCue(MusicCue cue, float timeSeconds)
        {
            return new SuspendedPlaybackState
            {
                Cue = cue,
                TimeSeconds = Mathf.Max(0f, timeSeconds)
            };
        }

        public static SuspendedPlaybackState CreateResumeSilence()
        {
            return new SuspendedPlaybackState
            {
                ResumeToSilence = true
            };
        }

        public static SuspendedPlaybackState CreateNoChange()
        {
            return new SuspendedPlaybackState
            {
                NoAudibleChange = true
            };
        }
    }
}
