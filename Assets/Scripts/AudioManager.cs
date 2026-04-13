using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Playback")]
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Categories")]
    [SerializeField] private SoundCategory[] categories;

    private readonly Dictionary<string, SoundCategory> _categoryMap =
        new Dictionary<string, SoundCategory>(StringComparer.OrdinalIgnoreCase);
    private readonly List<AudioSource> _pooledSources = new List<AudioSource>();
    private readonly Dictionary<AudioSource, SoundCategory> _sourceCategories =
        new Dictionary<AudioSource, SoundCategory>();

    private void Update()
    {
        if (categories == null || categories.Length == 0)
        {
            return;
        }

        float now = Time.unscaledTime;
        for (int i = 0; i < categories.Length; i++)
        {
            SoundCategory category = categories[i];
            if (category == null)
            {
                continue;
            }

            ProcessQueuedPlayback(category, now);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (oneShotSource == null)
        {
            oneShotSource = GetComponent<AudioSource>();
        }

        RebuildCache();
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    [ContextMenu("Rebuild Audio Cache")]
    public void RebuildCache()
    {
        _categoryMap.Clear();

        if (categories == null)
        {
            return;
        }

        for (int i = 0; i < categories.Length; i++)
        {
            SoundCategory category = categories[i];
            if (category == null || string.IsNullOrWhiteSpace(category.id))
            {
                continue;
            }

            string key = category.id.Trim();
            category.id = key;

            if (_categoryMap.ContainsKey(key))
            {
                continue;
            }

            _categoryMap.Add(key, category);
        }
    }

    public static bool Play(string categoryId)
    {
        return Instance != null && Instance.PlayInternal(categoryId);
    }

    public static bool Play(string categoryId, int index)
    {
        return Instance != null && Instance.PlayInternal(categoryId, index);
    }

    public bool PlayInternal(string categoryId)
    {
        if (!TryGetCategory(categoryId, out SoundCategory category))
        {
            return false;
        }

        int clipIndex = category.GetNextIndex();
        if (clipIndex < 0)
        {
            return false;
        }

        if (category.useTempoQueue)
        {
            return QueueOrPlayClip(category, clipIndex);
        }

        InterruptTempoQueuedCategoriesExcept(null);
        return PlayClip(category, category.clips[clipIndex], category.volume, category.GetRandomPitch());
    }

    public bool PlayInternal(string categoryId, int index)
    {
        if (!TryGetCategory(categoryId, out SoundCategory category))
        {
            return false;
        }

        if (category.clips == null || index < 0 || index >= category.clips.Length)
        {
            return false;
        }

        if (category.useTempoQueue)
        {
            return QueueOrPlayClip(category, index);
        }

        InterruptTempoQueuedCategoriesExcept(null);
        return PlayClip(category, category.clips[index], category.volume, category.GetRandomPitch());
    }

    private bool TryGetCategory(string categoryId, out SoundCategory category)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            category = null;
            return false;
        }

        if (_categoryMap.Count == 0)
        {
            RebuildCache();
        }

        return _categoryMap.TryGetValue(categoryId.Trim(), out category);
    }

    private bool PlayClip(SoundCategory category, AudioClip clip, float volume, float pitch)
    {
        if (clip == null || oneShotSource == null)
        {
            return false;
        }

        AudioSource playbackSource = GetPlaybackSource();
        if (playbackSource == null)
        {
            return false;
        }

        playbackSource.pitch = pitch;
        _sourceCategories[playbackSource] = category;
        playbackSource.PlayOneShot(clip, volume);
        return true;
    }

    private AudioSource GetPlaybackSource()
    {
        for (int i = 0; i < _pooledSources.Count; i++)
        {
            AudioSource pooledSource = _pooledSources[i];
            if (pooledSource != null && !pooledSource.isPlaying)
            {
                return pooledSource;
            }
        }

        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        CopyAudioSourceSettings(oneShotSource, newSource);
        _pooledSources.Add(newSource);
        return newSource;
    }

    private void ProcessQueuedPlayback(SoundCategory category, float now)
    {
        if (category == null || !category.useTempoQueue || !category.HasQueuedClips)
        {
            return;
        }

        if (category.clips == null || category.clips.Length == 0)
        {
            category.ClearQueue();
            return;
        }

        float interval = category.GetTempoIntervalSeconds();
        if (interval <= 0f)
        {
            category.ClearQueue();
            return;
        }

        while (category.HasQueuedClips && now >= category.NextQueuedPlaybackTime)
        {
            int clipIndex = category.DequeueClip();
            if (clipIndex >= 0 && clipIndex < category.clips.Length)
            {
                PlayClip(category, category.clips[clipIndex], category.volume, category.GetRandomPitch());
            }

            category.AdvanceQueuedPlaybackTime(interval, now);
        }
    }

    private bool QueueOrPlayClip(SoundCategory category, int clipIndex)
    {
        if (category == null || category.clips == null || clipIndex < 0 || clipIndex >= category.clips.Length)
        {
            return false;
        }

        float now = Time.unscaledTime;
        float interval = category.GetTempoIntervalSeconds();

        if (!category.HasQueuedClips && category.CanPlayImmediately(now))
        {
            InterruptTempoQueuedCategoriesExcept(category);
            bool played = PlayClip(category, category.clips[clipIndex], category.volume, category.GetRandomPitch());
            if (played)
            {
                category.NotifyImmediatePlayback(now, interval);
            }

            return played;
        }

        category.EnqueueClip(clipIndex);
        return true;
    }

    private void InterruptTempoQueuedCategoriesExcept(SoundCategory exceptCategory)
    {
        if (categories == null || categories.Length == 0)
        {
            return;
        }

        for (int i = 0; i < categories.Length; i++)
        {
            SoundCategory category = categories[i];
            if (category == null || !category.useTempoQueue || ReferenceEquals(category, exceptCategory))
            {
                continue;
            }

            category.ClearQueue();
            StopCategoryPlayback(category);
        }
    }

    private void StopCategoryPlayback(SoundCategory category)
    {
        for (int i = 0; i < _pooledSources.Count; i++)
        {
            AudioSource source = _pooledSources[i];
            if (source == null)
            {
                continue;
            }

            if (_sourceCategories.TryGetValue(source, out SoundCategory sourceCategory)
                && ReferenceEquals(sourceCategory, category))
            {
                source.Stop();
                _sourceCategories.Remove(source);
            }
        }
    }

    private static void CopyAudioSourceSettings(AudioSource source, AudioSource destination)
    {
        destination.playOnAwake = false;
        destination.loop = false;
        destination.outputAudioMixerGroup = source.outputAudioMixerGroup;
        destination.priority = source.priority;
        destination.volume = source.volume;
        destination.pitch = source.pitch;
        destination.panStereo = source.panStereo;
        destination.spatialBlend = source.spatialBlend;
        destination.reverbZoneMix = source.reverbZoneMix;
        destination.dopplerLevel = source.dopplerLevel;
        destination.spread = source.spread;
        destination.minDistance = source.minDistance;
        destination.maxDistance = source.maxDistance;
        destination.rolloffMode = source.rolloffMode;
        destination.mute = source.mute;
        destination.bypassEffects = source.bypassEffects;
        destination.bypassListenerEffects = source.bypassListenerEffects;
        destination.bypassReverbZones = source.bypassReverbZones;
        destination.ignoreListenerPause = source.ignoreListenerPause;
        destination.ignoreListenerVolume = source.ignoreListenerVolume;
        destination.velocityUpdateMode = source.velocityUpdateMode;
    }

    [Serializable]
    public sealed class SoundCategory
    {
        public string id;
        public PlaybackMode playMode = PlaybackMode.Random;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0.01f)] public float minPitch = 1f;
        [Min(0.01f)] public float maxPitch = 1f;
        public bool useTempoQueue;
        [Min(1f)] public float tempoBpm = 120f;
        public AudioClip[] clips;

        [NonSerialized] private int nextIndex;
        [NonSerialized] private readonly Queue<int> queuedClipIndices = new Queue<int>();
        [NonSerialized] private float nextQueuedPlaybackTime = -1f;

        public bool HasQueuedClips => queuedClipIndices.Count > 0;
        public float NextQueuedPlaybackTime => nextQueuedPlaybackTime;

        public float GetRandomPitch()
        {
            float normalizedMinPitch = Mathf.Min(minPitch, maxPitch);
            float normalizedMaxPitch = Mathf.Max(minPitch, maxPitch);
            return UnityEngine.Random.Range(normalizedMinPitch, normalizedMaxPitch);
        }

        public void EnqueueClip(int clipIndex)
        {
            if (clipIndex < 0)
            {
                return;
            }

            queuedClipIndices.Enqueue(clipIndex);
        }

        public int DequeueClip()
        {
            if (queuedClipIndices.Count == 0)
            {
                return -1;
            }

            return queuedClipIndices.Dequeue();
        }

        public void AdvanceQueuedPlaybackTime(float interval, float now)
        {
            nextQueuedPlaybackTime += interval;

            if (queuedClipIndices.Count == 0 && nextQueuedPlaybackTime < now)
            {
                nextQueuedPlaybackTime = -1f;
            }
        }

        public void ClearQueue()
        {
            queuedClipIndices.Clear();
            nextQueuedPlaybackTime = -1f;
        }

        public float GetTempoIntervalSeconds()
        {
            return tempoBpm > 0f ? 60f / tempoBpm : 0f;
        }

        public bool CanPlayImmediately(float now)
        {
            return nextQueuedPlaybackTime < 0f || now >= nextQueuedPlaybackTime;
        }

        public void NotifyImmediatePlayback(float now, float interval)
        {
            nextQueuedPlaybackTime = now + interval;
        }

        public int GetNextIndex()
        {
            if (clips == null || clips.Length == 0)
            {
                return -1;
            }

            switch (playMode)
            {
                case PlaybackMode.Random:
                    return UnityEngine.Random.Range(0, clips.Length);

                case PlaybackMode.Sequential:
                {
                    if (nextIndex >= clips.Length)
                    {
                        return clips.Length - 1;
                    }

                    int currentIndex = nextIndex;
                    nextIndex++;
                    return currentIndex;
                }

                case PlaybackMode.SequentialLoop:
                {
                    int currentIndex = nextIndex;
                    nextIndex = (nextIndex + 1) % clips.Length;
                    return currentIndex;
                }

                default:
                    return -1;
            }
        }
    }

    public enum PlaybackMode
    {
        Random = 0,
        Sequential = 1,
        SequentialLoop = 2,
    }
}
