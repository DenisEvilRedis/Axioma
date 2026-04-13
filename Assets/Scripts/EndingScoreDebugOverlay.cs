using UnityEngine;
using UnityEngine.InputSystem;

public class EndingScoreDebugOverlay : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private VNManager prototypeManager;

    [Header("Display")]
    [SerializeField] private bool activateOnStart = false;
    [SerializeField] private bool visible = false;
    [SerializeField] private Key toggleKey = Key.F3;
    [SerializeField] private Vector2 position = new Vector2(16f, 16f);
    [SerializeField] private Vector2 size = new Vector2(260f, 90f);

    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;

    private void Awake()
    {
        prototypeManager ??= FindFirstObjectByType<VNManager>();
        visible = activateOnStart;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        var toggleKeyControl = keyboard[toggleKey];
        if (toggleKeyControl != null && toggleKeyControl.wasPressedThisFrame)
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        prototypeManager ??= FindFirstObjectByType<VNManager>();
        if (prototypeManager == null)
        {
            return;
        }

        EnsureStyles();

        Rect rect = new Rect(position.x, position.y, size.x, size.y);
        GUI.Box(rect, "DEBUG: Ending Scores", _boxStyle);

        Rect contentRect = new Rect(rect.x + 12f, rect.y + 28f, rect.width - 24f, rect.height - 36f);
        GUI.Label(
            contentRect,
            $"Escape: {prototypeManager.EscapeScore}\n" +
            $"Vanity: {prototypeManager.VanityScore}\n" +
            $"Honesty: {prototypeManager.HonestyScore}",
            _labelStyle);
    }

    private void EnsureStyles()
    {
        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(10, 10, 8, 8)
            };
        }

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16
            };
        }
    }
}
