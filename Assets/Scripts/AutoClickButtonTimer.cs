using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AutoClickButtonTimer : MonoBehaviour
{
    [SerializeField] private float timer = 3f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool restartOnEnable = true;
    [SerializeField] private bool disableAfterAutoClick = true;

    private Button button;
    private float timeLeft;
    private bool isRunning;

    private void Awake()
    {
        button = GetComponent<Button>();
        ResetTimer();
    }

    private void OnEnable()
    {
        if (restartOnEnable)
        {
            ResetTimer();
            StartTimer();
        }
    }

    private void Update()
    {
        if (!isRunning || button == null || !button.interactable)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        timeLeft -= deltaTime;

        if (timeLeft > 0f)
        {
            return;
        }

        isRunning = false;
        button.onClick.Invoke();

        if (disableAfterAutoClick)
        {
            enabled = false;
        }
    }

    public void StartTimer()
    {
        timeLeft = timer;
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void ResetTimer()
    {
        timeLeft = timer;
    }
}
