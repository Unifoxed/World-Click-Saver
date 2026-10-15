using UnityEngine;
using UnityEngine.UI; // Nodig voor de Slider
using System.IO.Ports;

public class SliderGameController : MonoBehaviour
{
    // Pas "COM3" aan naar jouw poort
    SerialPort stream = new SerialPort("/dev/cu.usbmodem2101", 9600);

    public Slider gameSlider;         // Sleep hier je Slider in via de Inspector
    
    [Header("Slider Instellingen")]
    public float decreaseSpeed = 100f; // Hoeveel punten per seconde de slider afloopt
    public float bumpAmount = 1f;    // Hoeveel punten erbij komen per druk op de knop
    
    private float sliderValue;

    void Start()
    {
        stream.ReadTimeout = 25;
        try 
        {
            stream.Open();
            Debug.Log("Serial Port geopend!");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Kon poort niet openen: " + e.Message);
        }

        // Zet de slider op het begin op zijn maximum
        if (gameSlider != null)
        {
            sliderValue = gameSlider.maxValue;
        }
    }

    void Update()
    {
        // 1. Laat de slider van nature aflopen over tijd
        if (sliderValue > 0)
        {
            sliderValue -= decreaseSpeed * Time.deltaTime;
        }
        else
        {
            sliderValue = 0; // Kan niet onder de 0 komen
        }

        // 2. Lees de Arduino uit of er op de knop is gedrukt
        if (stream.IsOpen)
        {
            try
            {
                string data = stream.ReadLine();
                if (!string.IsNullOrEmpty(data))
                {
                    data = data.Trim();
                    
                    // Als de Arduino "P" stuurt, geef de slider een tikje omhoog
                    if (data == "P")
                    {
                        sliderValue += bumpAmount;
                        Debug.Log("Knop ingedrukt! Slider omhoog.");
                    }
                }
            }
            catch (System.TimeoutException)
            {
                // Normaal als er niet gedrukt wordt
            }
        }

        // Zorg dat de waarde netjes binnen de min en max van de slider blijft
        if (gameSlider != null)
        {
            sliderValue = Mathf.Clamp(sliderValue, gameSlider.minValue, gameSlider.maxValue);
            gameSlider.value = sliderValue; // Update de UI slider
        }
    }

    void OnApplicationQuit()
    {
        if (stream != null && stream.IsOpen)
        {
            stream.Close();
            Debug.Log("Serial Port gesloten.");
        }
    }
}