using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class VNManager : MonoBehaviour
{
    private const string Ending1Key = "Ending_1";
    private const string Ending2Key = "Ending_2";
    private const string Ending3Key = "Ending_3";
    private const string NovelPageTurnSoundCategoryId = "NovelPageTurn";

    [Header("UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject sceneMenuRoot;
    [SerializeField] private GameObject sceneStoryRoot;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject dialogPanel;
    [SerializeField] private GameObject dialogPortraitPanel;
    [SerializeField] private GameObject choicesPanel;
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private GameObject maxPortrait;
    [SerializeField] private GameObject arturPortrait;
    [SerializeField] private GameObject zaraPortrait;
    [SerializeField] private GameObject lekhaPortrait;
    [SerializeField] private Button startButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button[] choiceButtons;
    [SerializeField] private TextMeshProUGUI[] choiceLabels;
    [SerializeField] private VNDialogAuto autoDialogController;

    [Header("Defaults")]
    [SerializeField] private Sprite menuBackground;

    [Header("Beat Transitions")]
    [SerializeField] private Image fadeBlack;
    [SerializeField] private bool animateBackgroundTransitions = true;
    [Min(0f)][SerializeField] private float fadeToBlackSeconds = 0.25f;
    [Min(0f)][SerializeField] private float fadeFromBlackSeconds = 0.25f;

    [Header("Flow")]
    [SerializeField] private string startBeatId = "intro";
    [SerializeField] private List<VNScene> storyScenes = new List<VNScene>();

    [Header("Ending Sequences")]
    [SerializeField] private EndingSequencePlayer endingSequencePlayer;

    [Header("Music")]
    [SerializeField] private string menuMusicCueId = "menu_ambient";
    [SerializeField] private string defaultNovelMusicCueId = "menu_ambient";
    [Min(0f)][SerializeField] private float endingMusicFadeOutSeconds = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private readonly Dictionary<string, BeatData> _beatMap = new Dictionary<string, BeatData>(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

    private StoryState _state;
    private BeatData _currentBeat;
    private readonly List<RaycastResult> _pointerRaycastResults = new List<RaycastResult>();
    private Coroutine _backgroundFadeRoutine;
    private readonly int[] _choiceIndexByButtonSlot = { -1, -1, -1 };
    private bool _endingSequenceActive;

    private void Awake()
    {
        AutoBindIfNeeded();
        EnsureStoryConfigured();
        RebuildBeatMap();

        if (fadeBlack != null)
        {
            SetFadeAlpha(0f);
        }

        if (startButton)
        {
            startButton.onClick.RemoveListener(StartNovel);
            startButton.onClick.AddListener(StartNovel);
        }

        if (nextButton)
        {
            nextButton.onClick.RemoveListener(Next);
            nextButton.onClick.AddListener(Next);
        }

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int buttonSlot = i;
                if (choiceButtons[i] == null)
                {
                    continue;
                }

                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => OnChoiceButtonPressed(buttonSlot));
            }
        }

        ShowMenu();
    }

    private void OnValidate()
    {
        if (storyScenes == null)
        {
            storyScenes = new List<VNScene>();
        }
    }

    private void Update()
    {
        if (TryHandleChoiceKeyboardInput())
        {
            return;
        }

        if (ShouldAdvanceFromKeyboard() || ShouldAdvanceFromPointer())
        {
            Next();
        }
    }

    [ContextMenu("Rebuild Beat Map")]
    public void RebuildBeatMap()
    {
        _beatMap.Clear();

        if (storyScenes == null)
        {
            return;
        }

        for (int sceneIndex = 0; sceneIndex < storyScenes.Count; sceneIndex++)
        {
            VNScene storyScene = storyScenes[sceneIndex];
            if (storyScene == null || storyScene.Beats == null)
            {
                continue;
            }

            IReadOnlyList<BeatData> sceneBeats = storyScene.Beats;
            for (int beatIndex = 0; beatIndex < sceneBeats.Count; beatIndex++)
            {
                BeatData beat = sceneBeats[beatIndex];
                if (beat == null || string.IsNullOrWhiteSpace(beat.beatId))
                {
                    continue;
                }

                string key = beat.beatId.Trim();
                if (_beatMap.ContainsKey(key))
                {
                    Debug.LogWarning($"[PrototypeVN] Duplicate beatId '{key}' in scene '{storyScene.SceneId}' at index {beatIndex}.", this);
                    continue;
                }

                beat.beatId = key;
                _beatMap.Add(key, beat);
            }
        }
    }

    public void StartNovel()
    {
        ResetState();
        ShowDialog();
        PlayDefaultNovelMusic();
        JumpTo(startBeatId);
    }

    public void RestartNovel()
    {
        StartNovel();
    }

    public void RestartToMenu()
    {
        if (endingSequencePlayer != null)
        {
            endingSequencePlayer.Stop();
        }

        StopAutoAdvance();
        ResetState();
        ShowMenu();
        ClearText();
    }

    public void Next()
    {
        StopAutoAdvance();

        if (_currentBeat == null)
        {
            return;
        }

        if (_currentBeat.mode != BeatMode.Dialogue)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentBeat.nextBeatId))
        {
            Log($"Beat '{_currentBeat.beatId}' has no next beat. Returning to menu.");
            RestartToMenu();
            return;
        }

        TryPlayNovelPageTurn(_currentBeat.nextBeatId);
        JumpTo(_currentBeat.nextBeatId, _currentBeat.nextBeatSoundCategoryId);
    }

    public void Choose(int choiceIndex)
    {
        StopAutoAdvance();

        if (_currentBeat == null || _currentBeat.mode != BeatMode.Choice)
        {
            return;
        }

        if (_currentBeat.choices == null || choiceIndex < 0 || choiceIndex >= _currentBeat.choices.Length)
        {
            Debug.LogWarning($"[PrototypeVN] Choice index '{choiceIndex}' is invalid for beat '{_currentBeat.beatId}'.", this);
            return;
        }

        ChoiceData choice = _currentBeat.choices[choiceIndex];
        if (choice == null)
        {
            return;
        }

        ApplyEffects(choice.effects);
        JumpTo(choice.nextBeatId, choice.nextBeatSoundCategoryId);
    }

    private void OnChoiceButtonPressed(int buttonSlot)
    {
        if (buttonSlot < 0 || buttonSlot >= _choiceIndexByButtonSlot.Length)
        {
            return;
        }

        int choiceIndex = _choiceIndexByButtonSlot[buttonSlot];
        if (choiceIndex < 0)
        {
            return;
        }

        Choose(choiceIndex);
    }

    public void JumpTo(string beatId, string soundCategoryId = null)
    {
        StopAutoAdvance();

        if (string.IsNullOrWhiteSpace(beatId))
        {
            Debug.LogWarning("[PrototypeVN] Empty beat id.", this);
            return;
        }

        if (!_beatMap.TryGetValue(beatId.Trim(), out BeatData beat))
        {
            Debug.LogWarning($"[PrototypeVN] Beat '{beatId}' not found.", this);
            return;
        }

        if (!string.IsNullOrWhiteSpace(soundCategoryId))
        {
            AudioManager.Play(soundCategoryId);
        }

        _currentBeat = beat;
        RenderCurrentBeat();
    }

    public bool HasFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId) && _flags.Contains(flagId);
    }

    public int EscapeScore => _state?.escape ?? 0;
    public int VanityScore => _state?.vanity ?? 0;
    public int HonestyScore => _state?.honesty ?? 0;

    public PathScore GetDominantPath()
    {
        int escape = _state.escape;
        int vanity = _state.vanity;
        int honesty = _state.honesty;

        if (escape > vanity && escape > honesty) return PathScore.Escape;
        if (vanity > escape && vanity > honesty) return PathScore.Vanity;
        if (honesty > escape && honesty > vanity) return PathScore.Honesty;
        return PathScore.None;
    }

    public int GetLead(PathScore path)
    {
        int score = GetScore(path);
        if (score == int.MinValue)
        {
            return 0;
        }

        int highestOtherScore = int.MinValue;

        if (path != PathScore.Escape)
        {
            highestOtherScore = Mathf.Max(highestOtherScore, _state.escape);
        }

        if (path != PathScore.Vanity)
        {
            highestOtherScore = Mathf.Max(highestOtherScore, _state.vanity);
        }

        if (path != PathScore.Honesty)
        {
            highestOtherScore = Mathf.Max(highestOtherScore, _state.honesty);
        }

        if (highestOtherScore == int.MinValue)
        {
            return 0;
        }

        return score - highestOtherScore;
    }

    private void RenderCurrentBeat()
    {
        if (_currentBeat == null)
        {
            return;
        }

        if (_currentBeat.mode == BeatMode.Branch)
        {
            ResolveBranchBeat(_currentBeat);
            return;
        }

        HideChoices();

        if (_currentBeat.background != null && backgroundImage != null)
        {
            SetBackgroundForBeat(_currentBeat.background);
        }

        if (_currentBeat.mode != BeatMode.Ending)
        {
            ApplyMusicState(_currentBeat);
        }

        switch (_currentBeat.mode)
        {
            case BeatMode.Dialogue:
                RenderDialogueBeat(_currentBeat);
                break;

            case BeatMode.Choice:
                RenderChoiceBeat(_currentBeat);
                break;

            case BeatMode.Branch:
                ResolveBranchBeat(_currentBeat);
                break;

            case BeatMode.Ending:
                RenderEndingBeat(_currentBeat);
                break;

            default:
                Debug.LogWarning($"[PrototypeVN] Unsupported beat mode '{_currentBeat.mode}' on '{_currentBeat.beatId}'.", this);
                break;
        }
    }

    private void RenderDialogueBeat(BeatData beat)
    {
        ShowStoryBeat(true);
        SetLine(beat);

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
            nextButton.interactable = true;
        }

        StartAutoAdvanceForCurrentBeat(beat);
        Log($"Dialogue -> {beat.beatId}");
    }

    private void RenderChoiceBeat(BeatData beat)
    {
        ShowStoryBeat(false);
        SetLine(beat);
        ShowChoices(beat.choices);
        StopAutoAdvance();
        Log($"Choice -> {beat.beatId}");
    }

    private void ResolveBranchBeat(BeatData beat)
    {
        if (!TryGetBranchTarget(beat, out string nextBeatId, out string nextBeatSoundCategoryId))
        {
            Debug.LogWarning($"[PrototypeVN] Branch beat '{beat?.beatId}' has no valid next beat.", this);
            return;
        }

        Log($"Branch -> {beat.beatId} -> {nextBeatId}");
        JumpTo(nextBeatId, nextBeatSoundCategoryId);
    }

    private void RenderEndingBeat(BeatData beat)
    {
        StopMusicForEnding();
        ApplyEffects(beat?.onEnterEffects);
        UnlockEnding(beat?.endingToUnlock ?? EndingType.None);
        StopAutoAdvance();

        if (beat != null && beat.endingToUnlock != EndingType.None && TryPlayEndingSequence(beat.endingToUnlock))
        {
            Log($"Ending Sequence -> {beat.beatId} -> {beat.endingToUnlock}");
            return;
        }

        Debug.LogWarning($"[PrototypeVN] Ending beat '{beat?.beatId}' has no playable ending sequence. Falling back to static state.", this);
        ShowStoryBeat(false);
        ClearText();

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }

        Log($"Ending Fallback -> {beat?.beatId} -> {beat?.endingToUnlock}");
    }

    private void TryPlayNovelPageTurn(string beatId)
    {
        BeatData targetBeat = ResolveBeatForNovelPageTurn(beatId);
        if (targetBeat == null || targetBeat.mode == BeatMode.Ending)
        {
            return;
        }

        AudioManager.Play(NovelPageTurnSoundCategoryId);
    }

    private BeatData ResolveBeatForNovelPageTurn(string beatId)
    {
        if (string.IsNullOrWhiteSpace(beatId))
        {
            return null;
        }

        string currentBeatId = beatId.Trim();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(currentBeatId)
            && _beatMap.TryGetValue(currentBeatId, out BeatData beat)
            && beat != null)
        {
            if (!visited.Add(currentBeatId))
            {
                return beat;
            }

            if (beat.mode != BeatMode.Branch)
            {
                return beat;
            }

            if (!TryGetBranchTarget(beat, out string nextBeatId, out _))
            {
                return null;
            }

            currentBeatId = nextBeatId;
        }

        return null;
    }

    private bool TryGetBranchTarget(BeatData beat, out string nextBeatId, out string nextBeatSoundCategoryId)
    {
        nextBeatId = null;
        nextBeatSoundCategoryId = null;

        if (beat == null)
        {
            return false;
        }

        BranchRuleData matchedRule = null;
        if (beat.branchRules != null)
        {
            for (int i = 0; i < beat.branchRules.Length; i++)
            {
                BranchRuleData rule = beat.branchRules[i];
                if (rule != null && IsBranchMatch(rule))
                {
                    matchedRule = rule;
                    break;
                }
            }
        }

        nextBeatId = matchedRule != null ? matchedRule.nextBeatId : beat.defaultNextBeatId;
        nextBeatSoundCategoryId = matchedRule != null
            ? matchedRule.nextBeatSoundCategoryId
            : beat.defaultNextBeatSoundCategoryId;

        if (string.IsNullOrWhiteSpace(nextBeatId))
        {
            nextBeatId = null;
            nextBeatSoundCategoryId = null;
            return false;
        }

        nextBeatId = nextBeatId.Trim();
        if (!string.IsNullOrWhiteSpace(nextBeatSoundCategoryId))
        {
            nextBeatSoundCategoryId = nextBeatSoundCategoryId.Trim();
        }

        return true;
    }

    private void ApplyEffects(EffectData effects)
    {
        if (effects == null)
        {
            return;
        }

        _state.escape += effects.escapeDelta;
        _state.vanity += effects.vanityDelta;
        _state.honesty += effects.honestyDelta;

        ApplyFlags(_flags, effects.flagsToAdd);
        RemoveFlags(_flags, effects.flagsToRemove);

        Log($"State -> escape={_state.escape}, vanity={_state.vanity}, honesty={_state.honesty}, dominant={GetDominantPath()}");
    }

    private bool IsBranchMatch(BranchRuleData rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.requiredFlag) && !HasFlag(rule.requiredFlag))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.forbiddenFlag) && HasFlag(rule.forbiddenFlag))
        {
            return false;
        }

        if (rule.requiredDominantPath != PathScore.Any && GetDominantPath() != rule.requiredDominantPath)
        {
            return false;
        }

        if (rule.minLead > 0)
        {
            PathScore leadPath = rule.requiredDominantPath == PathScore.Any
                ? GetDominantPath()
                : rule.requiredDominantPath;

            if (leadPath == PathScore.None || GetLead(leadPath) < rule.minLead)
            {
                return false;
            }
        }

        if (_state.escape < rule.minEscape) return false;
        if (_state.vanity < rule.minVanity) return false;
        if (_state.honesty < rule.minHonesty) return false;

        return true;
    }

    private void UnlockEnding(EndingType endingType)
    {
        switch (endingType)
        {
            case EndingType.None:
                return;
            case EndingType.Escape:
                PlayerPrefs.SetInt(Ending1Key, 1);
                break;
            case EndingType.Vanity:
                PlayerPrefs.SetInt(Ending2Key, 1);
                break;
            case EndingType.Honesty:
                PlayerPrefs.SetInt(Ending3Key, 1);
                break;
            default:
                return;
        }

        PlayerPrefs.Save();
    }

    private void ResetState()
    {
        if (endingSequencePlayer != null)
        {
            endingSequencePlayer.Stop();
        }

        StopAutoAdvance();
        _state = new StoryState();
        _flags.Clear();
        _currentBeat = null;
        _endingSequenceActive = false;
        HideChoices();
    }

    private void ShowMenu()
    {
        StopAutoAdvance();
        StopBackgroundFade();
        if (sceneMenuRoot != null) sceneMenuRoot.SetActive(true);
        if (sceneStoryRoot != null) sceneStoryRoot.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (dialogPanel != null) dialogPanel.SetActive(false);
        if (dialogPortraitPanel != null) dialogPortraitPanel.SetActive(false);
        if (choicesPanel != null) choicesPanel.SetActive(false);

        if (backgroundImage != null && menuBackground != null)
        {
            backgroundImage.sprite = menuBackground;
            backgroundImage.color = Color.white;
        }

        PlayMenuMusic();
    }

    private void ShowStoryBeat(bool showDialogPanel)
    {
        if (sceneMenuRoot != null) sceneMenuRoot.SetActive(false);
        if (sceneStoryRoot != null) sceneStoryRoot.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (dialogPanel != null) dialogPanel.SetActive(showDialogPanel);
        if (dialogPortraitPanel != null) dialogPortraitPanel.SetActive(false);
        if (choicesPanel != null) choicesPanel.SetActive(false);
    }

    private void ShowDialog()
    {
        ShowStoryBeat(true);
    }

    private void SetBackgroundForBeat(Sprite nextBackground)
    {
        if (backgroundImage == null || nextBackground == null)
        {
            return;
        }

        backgroundImage.color = Color.white;

        if (!animateBackgroundTransitions
            || fadeBlack == null
            || backgroundImage.sprite == null
            || ReferenceEquals(backgroundImage.sprite, nextBackground)
            || (fadeToBlackSeconds <= 0f && fadeFromBlackSeconds <= 0f))
        {
            StopBackgroundFade();
            backgroundImage.sprite = nextBackground;
            SetFadeAlpha(0f);
            return;
        }

        StopBackgroundFade();
        _backgroundFadeRoutine = StartCoroutine(FadeBackgroundSwap(nextBackground));
    }

    private IEnumerator FadeBackgroundSwap(Sprite nextBackground)
    {
        yield return FadeOverlayTo(1f, fadeToBlackSeconds);

        if (backgroundImage != null)
        {
            backgroundImage.sprite = nextBackground;
            backgroundImage.color = Color.white;
        }

        yield return FadeOverlayTo(0f, fadeFromBlackSeconds);
        _backgroundFadeRoutine = null;
    }

    private IEnumerator FadeOverlayTo(float targetAlpha, float duration)
    {
        if (fadeBlack == null)
        {
            yield break;
        }

        float startAlpha = fadeBlack.color.a;
        if (duration <= 0f)
        {
            SetFadeAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
    }

    private void StopBackgroundFade()
    {
        if (_backgroundFadeRoutine != null)
        {
            StopCoroutine(_backgroundFadeRoutine);
            _backgroundFadeRoutine = null;
        }
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

    private void ShowChoices(ChoiceData[] choices)
    {
        if (choicesPanel != null)
        {
            choicesPanel.SetActive(true);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }

        int maxButtons = choiceButtons == null ? 0 : choiceButtons.Length;
        ResetChoiceButtonMapping();
        int[] buttonSlots = GetButtonSlotsForChoiceCount(choices?.Length ?? 0);

        int visibleChoiceCount = choices == null ? 0 : Mathf.Min(choices.Length, buttonSlots.Length);
        for (int choiceIndex = 0; choiceIndex < visibleChoiceCount; choiceIndex++)
        {
            if (choices[choiceIndex] == null)
            {
                continue;
            }

            int buttonSlot = buttonSlots[choiceIndex];
            if (buttonSlot < 0 || buttonSlot >= maxButtons)
            {
                continue;
            }

            _choiceIndexByButtonSlot[buttonSlot] = choiceIndex;
        }

        for (int i = 0; i < maxButtons; i++)
        {
            int choiceIndex = i < _choiceIndexByButtonSlot.Length ? _choiceIndexByButtonSlot[i] : -1;
            bool active = choiceIndex >= 0
                && choices != null
                && choiceIndex < choices.Length
                && choiceButtons[i] != null
                && choices[choiceIndex] != null;

            if (choiceButtons[i] != null)
            {
                choiceButtons[i].gameObject.SetActive(active);
            }

            if (!active)
            {
                continue;
            }

            if (choiceLabels != null && i < choiceLabels.Length && choiceLabels[i] != null)
            {
                choiceLabels[i].text = choices[choiceIndex].label;
            }
        }
    }

    private void HideChoices()
    {
        if (choicesPanel != null)
        {
            choicesPanel.SetActive(false);
        }

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        ResetChoiceButtonMapping();

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
        }
    }

    private void SetLine(BeatData beat)
    {
        string speaker = beat?.speaker ?? string.Empty;
        string text = beat?.bodyText ?? string.Empty;

        if (speakerText != null) speakerText.text = speaker;
        if (bodyText != null) bodyText.text = text;

        ApplyDialogueVisibility(speaker, text);
        UpdateSpeakerPortrait(beat?.speakerPortrait ?? SpeakerPortrait.None, speaker);
    }

    private void ApplyDialogueVisibility(string speaker, string text)
    {
        bool hasBodyText = !string.IsNullOrWhiteSpace(text);
        bool hasSpeakerText = !string.IsNullOrWhiteSpace(speaker);

        if (dialogPanel != null)
        {
            dialogPanel.SetActive(hasBodyText);
        }

        if (dialogPortraitPanel != null)
        {
            dialogPortraitPanel.SetActive(hasSpeakerText);
        }
    }

    private void ResetChoiceButtonMapping()
    {
        for (int i = 0; i < _choiceIndexByButtonSlot.Length; i++)
        {
            _choiceIndexByButtonSlot[i] = -1;
        }
    }

    private static int[] GetButtonSlotsForChoiceCount(int choiceCount)
    {
        return choiceCount switch
        {
            <= 0 => Array.Empty<int>(),
            1 => new[] { 1 },
            2 => new[] { 0, 2 },
            _ => new[] { 0, 1, 2 }
        };
    }

    private void StartAutoAdvanceForCurrentBeat(BeatData beat)
    {
        if (autoDialogController == null || beat == null)
        {
            return;
        }

        string fullText = string.IsNullOrWhiteSpace(beat.speaker)
            ? beat.bodyText
            : $"{beat.speaker}\n{beat.bodyText}";

        autoDialogController.StartAutoAdvanceForCurrentText(fullText);
    }

    private void StopAutoAdvance()
    {
        if (autoDialogController != null)
        {
            autoDialogController.StopAutoAdvance();
        }
    }

    private void ClearText()
    {
        SetLine(null);
    }

    private void PlayMenuMusic()
    {
        if (string.IsNullOrWhiteSpace(menuMusicCueId))
        {
            MusicManager.Stop();
            return;
        }

        MusicManager.Play(menuMusicCueId);
    }

    private void PlayDefaultNovelMusic()
    {
        if (string.IsNullOrWhiteSpace(defaultNovelMusicCueId))
        {
            return;
        }

        MusicManager.Play(defaultNovelMusicCueId);
    }

    private void ApplyMusicState(BeatData beat)
    {
        if (beat == null)
        {
            return;
        }

        if (beat.stopMusic)
        {
            MusicManager.Stop();
            return;
        }

        if (!string.IsNullOrWhiteSpace(beat.musicCueId))
        {
            MusicManager.Play(beat.musicCueId);
        }
    }

    private void StopMusicForEnding()
    {
        MusicManager.Stop(endingMusicFadeOutSeconds);
    }

    private void ApplyFlags(HashSet<string> flags, string[] values)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                flags.Add(values[i].Trim());
            }
        }
    }

    private void RemoveFlags(HashSet<string> flags, string[] values)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                flags.Remove(values[i].Trim());
            }
        }
    }

    private void Log(string message)
    {
        if (debugLogs)
        {
            Debug.Log($"[PrototypeVN] {message}", this);
        }
    }

    private void OnAutoAdvanceRequest()
    {
        if (_endingSequenceActive)
        {
            return;
        }

        if (_currentBeat == null)
        {
            return;
        }

        if (_currentBeat.mode == BeatMode.Dialogue)
        {
            Next();
            return;
        }

        if (_currentBeat.mode == BeatMode.Ending)
        {
            RestartToMenu();
        }
    }

    private void OnAutoModeChanged(bool isEnabled)
    {
        if (!isEnabled)
        {
            StopAutoAdvance();
            return;
        }

        if (_currentBeat != null && _currentBeat.mode == BeatMode.Dialogue)
        {
            StartAutoAdvanceForCurrentBeat(_currentBeat);
        }
    }

    public void PlayEnding(int endingIndex)
    {
        ResetState();
        StopMusicForEnding();

        if (endingSequencePlayer != null && endingSequencePlayer.PlayEndingByIndex(endingIndex, OnEndingSequenceFinished))
        {
            _endingSequenceActive = true;
            ShowStoryBeat(false);
            ClearText();
            return;
        }

        ShowDialog();

        switch (endingIndex)
        {
            case 1:
                JumpTo("ending_escape");
                break;
            case 2:
                JumpTo("ending_vanity");
                break;
            case 3:
                JumpTo("ending_honesty");
                break;
            default:
                Debug.LogWarning($"[PrototypeVN] Unsupported ending index '{endingIndex}'.", this);
                RestartToMenu();
                break;
        }
    }

    private bool TryPlayEndingSequence(EndingType endingType)
    {
        if (_endingSequenceActive || endingSequencePlayer == null)
        {
            return false;
        }

        int endingIndex = endingType switch
        {
            EndingType.Escape => 1,
            EndingType.Vanity => 2,
            EndingType.Honesty => 3,
            _ => 0
        };

        if (endingIndex == 0 || !endingSequencePlayer.PlayEndingByIndex(endingIndex, OnEndingSequenceFinished))
        {
            return false;
        }

        _endingSequenceActive = true;
        ShowStoryBeat(false);
        ClearText();
        return true;
    }

    private void OnEndingSequenceFinished()
    {
        _endingSequenceActive = false;
        RestartToMenu();
    }

    private void AutoBindIfNeeded()
    {
        autoDialogController ??= GetComponent<VNDialogAuto>();
        endingSequencePlayer ??= GetComponent<EndingSequencePlayer>();
        sceneMenuRoot ??= FindByName("Scene_0");
        sceneStoryRoot ??= FindByName("Scene_1");

        if (mainMenuPanel == null && sceneMenuRoot != null)
        {
            mainMenuPanel = FindByName("MainMenuPanel", sceneMenuRoot.transform);
        }

        if (dialogPanel == null && sceneStoryRoot != null)
        {
            dialogPanel = FindByName("DialogPanel", sceneStoryRoot.transform);
        }

        if (dialogPortraitPanel == null && dialogPanel != null)
        {
            dialogPortraitPanel = FindByName("DialogPortraitPanel", dialogPanel.transform);
        }

        if (choicesPanel == null && sceneStoryRoot != null)
        {
            choicesPanel = FindByName("ChoicesPanel", sceneStoryRoot.transform);
        }

        if (backgroundImage == null && sceneStoryRoot != null)
        {
            GameObject backgroundRoot = FindDirectChildByName(sceneStoryRoot.transform, "Panel_BG");
            if (backgroundRoot != null)
            {
                backgroundImage = backgroundRoot.GetComponent<Image>();
            }
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = Color.white;
        }

        if (fadeBlack == null && sceneStoryRoot != null)
        {
            GameObject fadeObject = FindByName("Black", sceneStoryRoot.transform);
            if (fadeObject != null)
            {
                fadeBlack = fadeObject.GetComponent<Image>();
            }
        }

        if (speakerText == null && dialogPanel != null)
        {
            GameObject speakerObject = FindByName("SpeakerText", dialogPanel.transform);
            if (speakerObject != null)
            {
                speakerText = speakerObject.GetComponent<TextMeshProUGUI>();
            }
        }

        if (bodyText == null && dialogPanel != null)
        {
            GameObject bodyObject = FindByName("BodyText", dialogPanel.transform);
            if (bodyObject != null)
            {
                bodyText = bodyObject.GetComponent<TextMeshProUGUI>();
            }
        }

        if (maxPortrait == null && dialogPanel != null)
        {
            maxPortrait = FindByName("SuperStart", dialogPanel.transform);
        }

        if (arturPortrait == null && dialogPanel != null)
        {
            arturPortrait = FindByName("PM", dialogPanel.transform);
        }

        if (zaraPortrait == null && dialogPanel != null)
        {
            zaraPortrait = FindByName("YongStar", dialogPanel.transform);
        }

        if (lekhaPortrait == null && dialogPanel != null)
        {
            lekhaPortrait = FindByName("OldFrend", dialogPanel.transform);
        }

        if (startButton == null && sceneMenuRoot != null)
        {
            GameObject startObject = FindByName("StartButton", sceneMenuRoot.transform);
            if (startObject != null)
            {
                startButton = startObject.GetComponent<Button>();
            }
        }

        if (nextButton == null && dialogPanel != null)
        {
            GameObject nextObject = FindByName("NextButton", dialogPanel.transform);
            if (nextObject != null)
            {
                nextButton = nextObject.GetComponent<Button>();
            }
        }

        if (choiceButtons == null || choiceButtons.Length != 3)
        {
            choiceButtons = new Button[3];
        }

        if (choiceLabels == null || choiceLabels.Length != 3)
        {
            choiceLabels = new TextMeshProUGUI[3];
        }

        if (choicesPanel != null)
        {
            for (int i = 0; i < 3; i++)
            {
                string buttonName = $"ChoiceBtn{i}";
                GameObject buttonObject = FindByName(buttonName, choicesPanel.transform);
                if (buttonObject == null)
                {
                    continue;
                }

                choiceButtons[i] ??= buttonObject.GetComponent<Button>();
                choiceLabels[i] ??= buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        HideAllSpeakerPortraits();
    }

    private void UpdateSpeakerPortrait(SpeakerPortrait speakerPortrait, string speaker)
    {
        HideAllSpeakerPortraits();

        if (string.IsNullOrWhiteSpace(speaker))
        {
            return;
        }

        GameObject portraitToShow = GetPortraitForSpeaker(speakerPortrait);
        if (portraitToShow != null && dialogPortraitPanel != null && dialogPortraitPanel.activeSelf)
        {
            portraitToShow.SetActive(true);
        }
    }

    private void HideAllSpeakerPortraits()
    {
        SetPortraitActive(maxPortrait, false);
        SetPortraitActive(arturPortrait, false);
        SetPortraitActive(zaraPortrait, false);
        SetPortraitActive(lekhaPortrait, false);
    }

    private static void SetPortraitActive(GameObject portrait, bool isActive)
    {
        if (portrait != null)
        {
            portrait.SetActive(isActive);
        }
    }

    private GameObject GetPortraitForSpeaker(SpeakerPortrait speakerPortrait)
    {
        switch (speakerPortrait)
        {
            case SpeakerPortrait.Max:
                return maxPortrait;
            case SpeakerPortrait.Artur:
                return arturPortrait;
            case SpeakerPortrait.Zara:
                return zaraPortrait;
            case SpeakerPortrait.Lekha:
                return lekhaPortrait;
            default:
                return null;
        }
    }

    private void EnsureStoryConfigured()
    {
        if (string.IsNullOrWhiteSpace(startBeatId))
        {
            Debug.LogWarning("[PrototypeVN] startBeatId is not set.", this);
            return;
        }

        if (storyScenes == null)
        {
            Debug.LogWarning("[PrototypeVN] storyScenes is not configured.", this);
            return;
        }

        for (int i = 0; i < storyScenes.Count; i++)
        {
            VNScene storyScene = storyScenes[i];
            if (storyScene == null || storyScene.Beats == null)
            {
                continue;
            }

            IReadOnlyList<BeatData> sceneBeats = storyScene.Beats;
            for (int beatIndex = 0; beatIndex < sceneBeats.Count; beatIndex++)
            {
                BeatData beat = sceneBeats[beatIndex];
                if (beat == null || string.IsNullOrWhiteSpace(beat.beatId))
                {
                    continue;
                }

                if (string.Equals(beat.beatId.Trim(), startBeatId.Trim(), StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        Debug.LogWarning("[PrototypeVN] startBeatId is missing or not found in storyScenes.", this);
    }


    private GameObject FindByName(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return null;
        }

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject result = FindByName(objectName, roots[i].transform);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private GameObject FindByName(string objectName, Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            if (string.Equals(allChildren[i].name, objectName, StringComparison.Ordinal))
            {
                return allChildren[i].gameObject;
            }
        }

        return null;
    }

    private GameObject FindDirectChildByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private bool ShouldAdvanceFromPointer()
    {
        if (_currentBeat == null || _currentBeat.mode != BeatMode.Dialogue)
        {
            return false;
        }

        if (choicesPanel != null && choicesPanel.activeInHierarchy)
        {
            return false;
        }

        if (!WasAdvancePointerPressedThisFrame())
        {
            return false;
        }

        return !IsPointerOverButton();
    }

    private bool ShouldAdvanceFromKeyboard()
    {
        if (_currentBeat == null || _currentBeat.mode != BeatMode.Dialogue)
        {
            return false;
        }

        if (choicesPanel != null && choicesPanel.activeInHierarchy)
        {
            return false;
        }

        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private bool TryHandleChoiceKeyboardInput()
    {
        if (_currentBeat == null || _currentBeat.mode != BeatMode.Choice)
        {
            return false;
        }

        if (_currentBeat.choices == null || _currentBeat.choices.Length == 0)
        {
            return false;
        }

        if (Keyboard.current == null)
        {
            return false;
        }

        if (WasChoiceKeyPressedThisFrame(0) && TryChooseMappedSlot(0))
        {
            return true;
        }

        if (WasChoiceKeyPressedThisFrame(1) && TryChooseMappedSlot(1))
        {
            return true;
        }

        if (WasChoiceKeyPressedThisFrame(2) && TryChooseMappedSlot(2))
        {
            return true;
        }

        return false;
    }

    private bool TryChooseMappedSlot(int buttonSlot)
    {
        if (buttonSlot < 0 || buttonSlot >= _choiceIndexByButtonSlot.Length)
        {
            return false;
        }

        int choiceIndex = _choiceIndexByButtonSlot[buttonSlot];
        if (_currentBeat?.choices == null || choiceIndex < 0 || choiceIndex >= _currentBeat.choices.Length)
        {
            return false;
        }

        if (_currentBeat.choices[choiceIndex] == null)
        {
            return false;
        }

        Choose(choiceIndex);
        return true;
    }

    private bool WasChoiceKeyPressedThisFrame(int choiceIndex)
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return choiceIndex switch
        {
            0 => Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame,
            1 => Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame,
            2 => Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame,
            _ => false
        };
    }

    private bool WasAdvancePointerPressedThisFrame()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private bool IsPointerOverButton()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        Vector2? pointerPosition = null;
        if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null)
        {
            pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (!pointerPosition.HasValue)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition.Value
        };

        _pointerRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, _pointerRaycastResults);

        for (int i = 0; i < _pointerRaycastResults.Count; i++)
        {
            GameObject hitObject = _pointerRaycastResults[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            if (hitObject.GetComponentInParent<Button>() != null)
            {
                return true;
            }
        }

        return false;
    }

    [Serializable]
    private sealed class StoryState
    {
        public int escape;
        public int vanity;
        public int honesty;
    }

    [Serializable]
    public sealed class BeatData
    {
        public string beatId;
        public BeatMode mode = BeatMode.Dialogue;
        public string speaker;
        public SpeakerPortrait speakerPortrait = SpeakerPortrait.None;

        [TextArea(2, 6)]
        public string bodyText;

        public Sprite background;
        public string musicCueId;
        public bool stopMusic;
        public string nextBeatId;
        public string nextBeatSoundCategoryId;
        public ChoiceData[] choices;
        public BranchRuleData[] branchRules;
        public string defaultNextBeatId;
        public string defaultNextBeatSoundCategoryId;
        public EffectData onEnterEffects;
        public EndingType endingToUnlock = EndingType.None;
    }

    [Serializable]
    public sealed class ChoiceData
    {
        public string label;
        public string nextBeatId;
        public string nextBeatSoundCategoryId;
        public EffectData effects;
    }

    [Serializable]
    public sealed class BranchRuleData
    {
        public string requiredFlag;
        public string forbiddenFlag;
        public PathScore requiredDominantPath = PathScore.Any;
        public int minLead;
        public int minEscape;
        public int minVanity;
        public int minHonesty;
        public string nextBeatId;
        public string nextBeatSoundCategoryId;
    }

    private int GetScore(PathScore path)
    {
        switch (path)
        {
            case PathScore.Escape:
                return _state.escape;
            case PathScore.Vanity:
                return _state.vanity;
            case PathScore.Honesty:
                return _state.honesty;
            default:
                return int.MinValue;
        }
    }

    [Serializable]
    public sealed class EffectData
    {
        public int escapeDelta;
        public int vanityDelta;
        public int honestyDelta;
        public string[] flagsToAdd;
        public string[] flagsToRemove;
    }

    public enum BeatMode
    {
        Dialogue = 0,
        Choice = 1,
        Branch = 2,
        Ending = 3,
    }

    public enum PathScore
    {
        Any = -1,
        None = 0,
        Escape = 1,
        Vanity = 2,
        Honesty = 3,
    }

    public enum EndingType
    {
        None = 0,
        Escape = 1,
        Vanity = 2,
        Honesty = 3,
    }

    public enum SpeakerPortrait
    {
        None = 0,
        Max = 1,
        Artur = 2,
        Zara = 3,
        Lekha = 4,
    }
}
