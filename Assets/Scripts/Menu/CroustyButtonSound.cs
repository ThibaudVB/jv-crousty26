using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class CroustyButtonSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private int pointerUpFrame = -1;
    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Click);
        if (GetComponent<CroustyMenuHover>() == null) gameObject.AddComponent<CroustyMenuHover>();
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && button.IsInteractable()) CroustySoundDesignManager.PlayUI(CroustySound.ButtonPop);
    }
    public void OnPointerUp(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pointerUpFrame = Time.frameCount; }
    private void Click()
    {
        if (pointerUpFrame != Time.frameCount) CroustySoundDesignManager.PlayUI(CroustySound.ButtonPop);
        if (this != null) GetComponent<CroustyMenuHover>()?.Bounce();
    }
    private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Click); }
}
