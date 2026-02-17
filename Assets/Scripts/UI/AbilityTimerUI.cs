using UnityEngine;
using UnityEngine.UI;

public class AbilityTimerUI : MonoBehaviour
{
    [SerializeField] Image fillImage;
    [SerializeField] GameObject container;

    private void Start()
    {
        if (container != null)
            container.SetActive(false);
    }

    public void Show()
    {
        if (container != null)
            container.SetActive(true);
    }

    public void Hide()
    {
        if (container != null)
            container.SetActive(false);
    }

    public void SetFill(float normalized)
    {
        if (fillImage != null)
            fillImage.fillAmount = normalized;
    }
}
