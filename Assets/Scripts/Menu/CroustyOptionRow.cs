using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The scene owns the row's visuals; this component binds its setting and value.
public sealed class CroustyOptionRow : MonoBehaviour
{
    public CroustySettings.Option option;
    public TMP_Text value;
    void Awake() => GetComponent<Button>().onClick.AddListener(Cycle);
    void OnEnable() { CroustySettings.Changed += Refresh; Refresh(); }
    void OnDisable() => CroustySettings.Changed -= Refresh;
    public void Cycle() => CroustySettings.Cycle(option);
    public void Refresh() { if (value != null) value.text = CroustySettings.Label(option); }
}
