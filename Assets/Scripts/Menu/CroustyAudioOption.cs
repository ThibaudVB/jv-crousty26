using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class CroustyAudioOption : MonoBehaviour
{
    public bool music;
    public TMP_Text value;
    string Key => music ? "Crousty.MusicVolume" : "Crousty.EffectsVolume";
    void Awake() => GetComponent<Slider>().onValueChanged.AddListener(Set);
    void OnEnable()
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Key, 1));
        GetComponent<Slider>().SetValueWithoutNotify(volume);
        value.text = Mathf.RoundToInt(volume * 100) + "%";
    }
    public void Set(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(Key, volume);
        value.text = Mathf.RoundToInt(volume * 100) + "%";
        var sound = CroustySoundDesignManager.Instance;
        if (sound != null) { if (music) sound.musicVolume = volume; else sound.effectsVolume = volume; }
    }
    void OnDisable() => PlayerPrefs.Save();
}
