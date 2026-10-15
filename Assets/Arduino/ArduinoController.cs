using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;

public class MultiSliderGameController : MonoBehaviour
{
    // Pas "COM3" aan naar jouw poort
    SerialPort stream = new SerialPort("/dev/cu.usbmodem2101", 9600);

    [Header("Sliders")]
    public Slider slider1;
    public Slider slider2;
    public Slider slider3;
    
    [Header("Instellingen")]
    public float decreaseSpeed = 10f; // Hoe snel ze allemaal van nature aflopen
    public float bumpAmount = 15f;    // Hoeveel punten erbij komen per druk op de knop

    private float value1;
    private float value2;
    private float value3;

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

        // Zet alle sliders in het begin op hun maximum
        if (slider1 != null) value1 = slider1.maxValue;
        if (slider2 != null) value2 = slider2.maxValue;
        if (slider3 != null) value3 = slider3.maxValue;
    }

    void Update()
    {
        // 1. Laat alle drie de sliders van nature aflopen over tijd
        if (value1 > 0) value1 -= decreaseSpeed * Time.deltaTime;
        else value1 = 0;

        if (value2 > 0) value2 -= decreaseSpeed * Time.deltaTime;
        else value2 = 0;

        if (value3 > 0) value3 -= decreaseSpeed * Time.deltaTime;
        else value3 = 0;

        // 2. Lees de Arduino uit voor binnengekomen knoppen
        if (stream.IsOpen)
        {
            try
            {
                string data = stream.ReadLine();
                if (!string.IsNullOrEmpty(data))
                {
                    data = data.Trim();
                    
                    // Geef de juiste slider een tikje omhoog op basis van de knop
                    if (data == "P1")
                    {
                        value1 += bumpAmount;
                        Debug.Log("Knop 1 ingedrukt -> Slider 1 omhoog");
                    }
                    else if (data == "P2")
                    {
                        value2 += bumpAmount;
                        Debug.Log("Knop 2 ingedrukt -> Slider 2 omhoog");
                    }
                    else if (data == "P3")
                    {
                        value3 += bumpAmount;
                        Debug.Log("Knop 3 ingedrukt -> Slider 3 omhoog");
                    }
                }
            }
            catch (System.TimeoutException)
            {
                // Normaal als er niet gedrukt wordt
            }
        }

        // 3. Update de visuele UI sliders in Unity
        if (slider1 != null)
        {
            value1 = Mathf.Clamp(value1, slider1.minValue, slider1.maxValue);
            slider1.value = value1;
        }
        if (slider2 != null)
        {
            value2 = Mathf.Clamp(value2, slider2.minValue, slider2.maxValue);
            slider2.value = value2;
        }
        if (slider3 != null)
        {
            value3 = Mathf.Clamp(value3, slider3.minValue, slider3.maxValue);
            slider3.value = value3;
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