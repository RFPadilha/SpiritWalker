using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that renders tutorial hint text in a semi-transparent bar at the bottom of the screen.
/// Call TutorialHintDisplay.Instance.Show("message") / .Hide() from TutorialHintZone triggers.
/// Creates its own Canvas at runtime — no manual scene setup needed.
/// Place this on the RespawnManager GameObject (or any persistent manager) so it exists for the
/// full duration of the level.
/// </summary>
public class TutorialHintDisplay : MonoBehaviour
{
    public static TutorialHintDisplay Instance { get; private set; }

    [SerializeField] float fadeSpeed = 4f;

    private CanvasGroup  canvasGroup;
    private TextMeshProUGUI label;
    private Coroutine    fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCanvas();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Show(string message)
    {
        label.text = message;
        FadeTo(1f);
    }

    public void Hide() => FadeTo(0f);

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private void FadeTo(float target)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        while (!Mathf.Approximately(canvasGroup.alpha, target))
        {
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    private void BuildCanvas()
    {
        // Root canvas
        var canvasGo = new GameObject("TutorialHintCanvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGo.AddComponent<CanvasScaler>();

        // Background panel — anchored to the bottom strip of the screen
        var bgGo = new GameObject("HintBackground");
        bgGo.transform.SetParent(canvasGo.transform, false);

        var bg = bgGo.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        var bgRect       = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.15f, 0.04f);
        bgRect.anchorMax = new Vector2(0.85f, 0.14f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // CanvasGroup on the panel so we can fade just the hint without affecting other UI
        canvasGroup             = bgGo.AddComponent<CanvasGroup>();
        canvasGroup.alpha       = 0f;
        canvasGroup.interactable    = false;
        canvasGroup.blocksRaycasts  = false;

        // Text
        var textGo = new GameObject("HintText");
        textGo.transform.SetParent(bgGo.transform, false);

        label               = textGo.AddComponent<TextMeshProUGUI>();
        label.alignment     = TextAlignmentOptions.Center;
        label.fontSize      = 22f;
        label.color         = Color.white;
        label.raycastTarget = false;

        var textRect       = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 6f);
        textRect.offsetMax = new Vector2(-16f, -6f);
    }
}
