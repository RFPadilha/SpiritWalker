using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Trigger volume that loads the next scene when the player body walks through it.
/// When <see cref="isEndOfGame"/> is enabled, fades to black then shows a
/// "Thanks for playing" screen with Restart and Quit buttons instead.
/// Place at the end of each level. Requires a Trigger Collider on this GameObject.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelExit : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load. Must be added to Build Settings.")]
    [SerializeField] string nextSceneName;

    [Tooltip("If true, logs a message instead of loading — useful during blockout when the next scene isn't ready.")]
    [SerializeField] bool debugOnly = true;

    [Tooltip("Seconds to fade to black before the next scene loads.")]
    [SerializeField] float exitFadeDuration = 0.6f;

    [Header("End of Game")]
    [Tooltip("When true, shows an end-game screen instead of loading the next scene.")]
    [SerializeField] bool isEndOfGame = false;

    [Tooltip("Scene to load when Restart is pressed. Leave empty to restart from scene index 0.")]
    [SerializeField] string restartSceneName;

    [Tooltip("Seconds for the end-game text and buttons to fade in over the black screen.")]
    [SerializeField] float endScreenFadeInDuration = 1.2f;

    private bool used;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        used = true;
        StartCoroutine(ExitSequence());
    }

    // -------------------------------------------------------------------------
    // Exit sequence
    // -------------------------------------------------------------------------
    private IEnumerator ExitSequence()
    {
        if (RespawnManager.Instance != null)
            yield return RespawnManager.Instance.FadeToBlack(exitFadeDuration);

        if (isEndOfGame)
        {
            StartCoroutine(ShowEndScreen());
            yield break;
        }

        if (debugOnly || string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.Log("[LevelExit] Level complete! Wire up nextSceneName to load the next scene.");
            yield break;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    // -------------------------------------------------------------------------
    // End-game screen
    // -------------------------------------------------------------------------
    private IEnumerator ShowEndScreen()
    {
        // Freeze the player — no need to move any more.
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) player.enabled = false;

        // Restore timescale in case Soul Walk was active when the trigger fired.
        Time.timeScale = 1f;

        // Unlock the cursor so the player can click the buttons.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // An EventSystem is required for UI button input.
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // Build canvas above all other UI (the black fade canvas sits at 999).
        var canvasGo = new GameObject("EndGameCanvas");
        var canvas         = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // CanvasGroup drives the unified fade-in and keeps buttons non-interactive
        // until fully visible.
        var cg              = canvasGo.AddComponent<CanvasGroup>();
        cg.alpha            = 0f;
        cg.interactable     = false;
        cg.blocksRaycasts   = false;

        // "Thanks for playing!" title
        CreateLabel(canvasGo.transform,
            text:      "Thanks for playing!",
            fontSize:  52f,
            anchor:    new Rect(0.1f, 0.55f, 0.8f, 0.2f));

        // Buttons
        var restartBtn = CreateButton(canvasGo.transform, "Restart",
            anchor: new Rect(0.28f, 0.36f, 0.19f, 0.13f));

        var quitBtn = CreateButton(canvasGo.transform, "Quit",
            anchor: new Rect(0.53f, 0.36f, 0.19f, 0.13f));

        restartBtn.onClick.AddListener(OnRestartClicked);
        quitBtn.onClick.AddListener(OnQuitClicked);

        // Fade in.
        float elapsed = 0f;
        while (elapsed < endScreenFadeInDuration)
        {
            elapsed  += Time.unscaledDeltaTime;
            cg.alpha  = Mathf.Clamp01(elapsed / endScreenFadeInDuration);
            yield return null;
        }

        cg.alpha          = 1f;
        cg.interactable   = true;
        cg.blocksRaycasts = true;
    }

    // -------------------------------------------------------------------------
    // Button callbacks
    // -------------------------------------------------------------------------
    private void OnRestartClicked()
    {
        if (string.IsNullOrWhiteSpace(restartSceneName))
            SceneManager.LoadScene(0);
        else
            SceneManager.LoadScene(restartSceneName);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -------------------------------------------------------------------------
    // UI helpers — build elements procedurally, no prefabs needed
    // -------------------------------------------------------------------------

    // anchor is a Rect where x/y = anchorMin and width/height = size in anchor-space
    private static void CreateLabel(Transform parent, string text, float fontSize, Rect anchor)
    {
        var go  = new GameObject("Label");
        go.transform.SetParent(parent, false);

        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        var r        = go.GetComponent<RectTransform>();
        r.anchorMin  = new Vector2(anchor.x, anchor.y);
        r.anchorMax  = new Vector2(anchor.x + anchor.width, anchor.y + anchor.height);
        r.offsetMin  = Vector2.zero;
        r.offsetMax  = Vector2.zero;
    }

    private static Button CreateButton(Transform parent, string label, Rect anchor)
    {
        // Root — holds Image + Button
        var go  = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);

        var img   = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

        var btn    = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        colors.highlightedColor = new Color(0.30f, 0.30f, 0.30f, 1.0f);
        colors.pressedColor     = new Color(0.05f, 0.05f, 0.05f, 1.0f);
        btn.colors = colors;

        var r       = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(anchor.x, anchor.y);
        r.anchorMax = new Vector2(anchor.x + anchor.width, anchor.y + anchor.height);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        // Label text
        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);

        var tmp       = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 26f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        var tr       = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        return btn;
    }

    // -------------------------------------------------------------------------
    // Gizmos — orange tint when in end-of-game mode, green otherwise
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Color fill = isEndOfGame
            ? new Color(1f, 0.55f, 0f, 0.25f)
            : new Color(0f, 1f, 0.4f, 0.25f);
        Color edge = isEndOfGame
            ? new Color(1f, 0.55f, 0f, 0.85f)
            : new Color(0f, 1f, 0.4f, 0.85f);

        Gizmos.color  = fill;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = edge;
        if (col is BoxCollider box2)
            Gizmos.DrawWireCube(box2.center, box2.size);
    }
}
