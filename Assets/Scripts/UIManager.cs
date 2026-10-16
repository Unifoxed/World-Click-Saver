using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI infoText;

    // Voorbeeldmethode om tekst aan te passen vanuit andere scripts
    public void UpdateInfo(string message)
    {
        if (infoText != null)
        {
            infoText.text = message;
        }
    }
}