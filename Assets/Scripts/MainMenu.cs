using UnityEngine;

public class MainMenu : MonoBehaviour
{
    // Sleep hier je BeginScherm canvas naartoe in de Inspector (wordt UITgezet)
    public GameObject beginSchermCanvas;

    // Sleep hier je SpelCanvas (HUD) naartoe in de Inspector (wordt AANgezet)
    public GameObject spelCanvas;

    public void StartSpel()
    {
        // 1. Zet het beginscherm uit
        if (beginSchermCanvas != null)
        {
            beginSchermCanvas.SetActive(false);
        }

        // 2. Zet het spel-canvas aan
        if (spelCanvas != null)
        {
            spelCanvas.SetActive(true);
        }

        Debug.Log("Spel is gestart en het spel-canvas is geactiveerd!");
    }
}