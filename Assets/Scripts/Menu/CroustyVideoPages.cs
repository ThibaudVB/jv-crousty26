using UnityEngine;
using TMPro;
public sealed class CroustyVideoPages : MonoBehaviour
{
    public GameObject displayPage, detailPage;
    public TMP_Text pageLabel;
    public void Toggle()
    {
        bool details = !detailPage.activeSelf;
        displayPage.SetActive(!details); detailPage.SetActive(details);
        pageLabel.text = details ? "<  DISPLAY   2 / 2" : "DETAILS  >   1 / 2";
    }
}
