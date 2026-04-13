using System.Collections;
using TMPro;
using UnityEngine;

public class VNDialogAuto : MonoBehaviour
{
    [Header("Auto Mode")]
    [SerializeField] private bool isAutoMode = false;

    [Tooltip("Базовая задержка перед авто-переходом.")]
    [SerializeField] private float baseDelay = 1.5f;

    [Tooltip("Добавка за каждый символ текста.")]
    [SerializeField] private float delayPerCharacter = 0.04f;

    [Header("UI")]
    [SerializeField] private TMP_Text autoButtonText;
    [SerializeField] private Color autoOffColor = Color.white;
    [SerializeField] private Color autoOnColor = Color.yellow;

    private Coroutine _autoRoutine;

    public bool IsAutoMode => isAutoMode;

    private void Start()
    {
        RefreshAutoButtonVisual();
    }

    public void ToggleAutoMode()
    {
        isAutoMode = !isAutoMode;
        RefreshAutoButtonVisual();
        NotifyAutoModeChanged();
    }

    public void SetAutoMode(bool value)
    {
        isAutoMode = value;
        RefreshAutoButtonVisual();
        NotifyAutoModeChanged();
    }

    private void RefreshAutoButtonVisual()
    {
        if (autoButtonText != null)
        {
            autoButtonText.color = isAutoMode ? autoOnColor : autoOffColor;
        }
    }

    /// <summary>
    /// Вызывать, когда текущая реплика уже полностью показана на экране.
    /// </summary>
    public void StartAutoAdvanceForCurrentText(string fullText)
    {
        StopAutoAdvance();

        if (!isAutoMode)
            return;

        float delay = GetDelayForText(fullText);
        _autoRoutine = StartCoroutine(AutoAdvanceRoutine(delay));
    }

    /// <summary>
    /// Вызывать, если игрок кликнул дальше вручную, открыл меню,
    /// сменил сцену, началась печать нового текста и т.п.
    /// </summary>
    public void StopAutoAdvance()
    {
        if (_autoRoutine != null)
        {
            StopCoroutine(_autoRoutine);
            _autoRoutine = null;
        }
    }

    private float GetDelayForText(string text)
    {
        int characters = string.IsNullOrEmpty(text) ? 0 : text.Length;
        return baseDelay + characters * delayPerCharacter;
    }

    private IEnumerator AutoAdvanceRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        _autoRoutine = null;

        if (!isAutoMode)
            yield break;

        AdvanceDialogue();
    }

    /// <summary>
    /// Тут подключи свой реальный переход к следующей реплике / панели.
    /// </summary>
    private void AdvanceDialogue()
    {
        SendMessage("OnAutoAdvanceRequest", SendMessageOptions.DontRequireReceiver);
    }

    private void NotifyAutoModeChanged()
    {
        SendMessage("OnAutoModeChanged", isAutoMode, SendMessageOptions.DontRequireReceiver);
    }
}
