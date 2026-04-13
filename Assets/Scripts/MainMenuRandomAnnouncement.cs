using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainMenuRandomAnnouncement : MonoBehaviour
{
    [Header("Main Menu State")]
    [SerializeField] private GameObject sceneFirstLoad;

    [Header("Audio")]
    [SerializeField] private AudioSource playbackSource;
    [SerializeField] private AudioClip[] announcementClips;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;
    [SerializeField] private bool duckMusicWhilePlaying = true;
    [Range(0f, 1f)]
    [SerializeField] private float duckedMusicVolume = 0.35f;
    [Min(0f)]
    [SerializeField] private float duckFadeSeconds = 0.35f;

    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float initialDelaySeconds = 12f;
    [Min(0.1f)]
    [SerializeField] private float minDelaySeconds = 35f;
    [Min(0.1f)]
    [SerializeField] private float maxDelaySeconds = 80f;

    private readonly List<int> _remainingClipIndices = new List<int>();
    private float _nextPlaybackTime = -1f;
    private bool _started;
    private bool _warnedNoClips;
    private bool _musicDuckActive;

    private void OnEnable()
    {
        if (playbackSource == null)
        {
            Debug.LogWarning("[MainMenuRandomAnnouncement] PlaybackSource is not assigned.", this);
            enabled = false;
            return;
        }

        playbackSource.playOnAwake = false;
        playbackSource.loop = false;

        if (!IsMainMenuActive())
        {
            StopAndDisable();
            return;
        }

        RebuildClipPool();
        _started = true;
        _nextPlaybackTime = Time.unscaledTime + Mathf.Max(0f, initialDelaySeconds);
    }

    private void Update()
    {
        if (!IsMainMenuActive())
        {
            StopAndDisable();
            return;
        }

        if (Time.unscaledTime < _nextPlaybackTime)
        {
            ReleaseMusicDuckIfNeeded();
            return;
        }

        if (playbackSource.isPlaying)
        {
            AcquireMusicDuckIfNeeded();
            _nextPlaybackTime = Time.unscaledTime + 0.5f;
            return;
        }

        ReleaseMusicDuckIfNeeded();

        if (!TryGetNextClip(out AudioClip clip))
        {
            if (!_warnedNoClips)
            {
                _warnedNoClips = true;
                Debug.LogWarning("[MainMenuRandomAnnouncement] No valid announcement clips assigned.", this);
            }

            _nextPlaybackTime = Time.unscaledTime + 2f;
            return;
        }

        _warnedNoClips = false;
        playbackSource.PlayOneShot(clip, volume);
        AcquireMusicDuckIfNeeded();
        ScheduleNextPlayback();
    }

    private bool IsMainMenuActive()
    {
        return sceneFirstLoad != null && sceneFirstLoad.activeInHierarchy;
    }

    private void StopAndDisable()
    {
        if (playbackSource != null && playbackSource.isPlaying)
        {
            playbackSource.Stop();
        }

        ReleaseMusicDuckIfNeeded();
        enabled = false;
    }

    private void ScheduleNextPlayback()
    {
        float min = Mathf.Max(0.1f, minDelaySeconds);
        float max = Mathf.Max(min, maxDelaySeconds);
        _nextPlaybackTime = Time.unscaledTime + Random.Range(min, max);
    }

    private bool TryGetNextClip(out AudioClip clip)
    {
        clip = null;

        if (announcementClips == null || announcementClips.Length == 0)
        {
            return false;
        }

        if (_remainingClipIndices.Count == 0)
        {
            RebuildClipPool();
        }

        if (_remainingClipIndices.Count == 0)
        {
            return false;
        }

        int randomPoolIndex = Random.Range(0, _remainingClipIndices.Count);
        int clipIndex = _remainingClipIndices[randomPoolIndex];
        _remainingClipIndices.RemoveAt(randomPoolIndex);

        if (clipIndex < 0 || clipIndex >= announcementClips.Length)
        {
            return false;
        }

        clip = announcementClips[clipIndex];
        return clip != null;
    }

    private void RebuildClipPool()
    {
        _remainingClipIndices.Clear();

        if (announcementClips == null)
        {
            return;
        }

        for (int i = 0; i < announcementClips.Length; i++)
        {
            if (announcementClips[i] != null)
            {
                _remainingClipIndices.Add(i);
            }
        }
    }

    private void AcquireMusicDuckIfNeeded()
    {
        if (!duckMusicWhilePlaying || _musicDuckActive)
        {
            return;
        }

        MusicManager.SetDuck(duckedMusicVolume, duckFadeSeconds);
        _musicDuckActive = true;
    }

    private void ReleaseMusicDuckIfNeeded()
    {
        if (!_musicDuckActive)
        {
            return;
        }

        MusicManager.ClearDuck(duckFadeSeconds);
        _musicDuckActive = false;
    }
}
