using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger volume that loads the next scene when the player body walks through it.
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

    private bool used;

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

    private IEnumerator ExitSequence()
    {
        if (RespawnManager.Instance != null)
            yield return RespawnManager.Instance.FadeToBlack(exitFadeDuration);

        if (debugOnly || string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.Log("[LevelExit] Level complete! Wire up nextSceneName to load the next scene.");
            yield break;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color  = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.85f);
        if (col is BoxCollider box2)
            Gizmos.DrawWireCube(box2.center, box2.size);
    }
}
