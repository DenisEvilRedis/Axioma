using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class ButtonHoverShift : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("Position")]
    public bool useShift = true;
    public Vector3 shiftDirection = Vector3.left;
    public float shiftAmount = 15f;
    public float moveTime = 12f;

    [Header("Scale")]
    public bool useScale = false;
    public Vector3 maxScale = new Vector3(1.05f, 1.05f, 1f);
    public float scaleTime = 12f;

    [Header("Audio")]
    public string hoverSoundCategory;
    public string clickSoundCategory;

    private RectTransform rect;

    private Vector2 startPos;
    private Vector2 targetPos;
    private Vector3 startScale;
    private Vector3 targetScale;

    private bool hoverPlayed;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        startPos = rect.anchoredPosition;
        targetPos = startPos;
        startScale = rect.localScale;
        targetScale = startScale;
    }

    private void Update()
    {
        if (useShift)
        {
            rect.anchoredPosition = Vector2.Lerp(
                rect.anchoredPosition,
                targetPos,
                Time.unscaledDeltaTime * moveTime);
        }

        if (useScale)
        {
            rect.localScale = Vector3.Lerp(
                rect.localScale,
                targetScale,
                Time.unscaledDeltaTime * scaleTime);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (useShift)
        {
            Vector3 normalizedDirection = shiftDirection.sqrMagnitude > 0f
                ? shiftDirection.normalized
                : Vector3.zero;
            Vector3 offset = normalizedDirection * shiftAmount;
            targetPos = startPos + new Vector2(offset.x, offset.y);
        }

        if (useScale)
        {
            targetScale = maxScale;
        }

        if (!hoverPlayed && !string.IsNullOrWhiteSpace(hoverSoundCategory))
        {
            AudioManager.Play(hoverSoundCategory);
            hoverPlayed = true;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverPlayed = false;

        targetPos = startPos;
        targetScale = startScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        rect.anchoredPosition = startPos;
        targetPos = startPos;
        rect.localScale = startScale;
        targetScale = startScale;

        if (!string.IsNullOrWhiteSpace(clickSoundCategory))
        {
            AudioManager.Play(clickSoundCategory);
        }
    }
}
