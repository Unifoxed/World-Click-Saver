using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using UnityEngine.InputSystem; // Nodig voor het nieuwe Input System

public class MultiSliderGameController : MonoBehaviour
{
    SerialPort stream = new SerialPort("/dev/cu.usbmodem2101", 9600);

    [Header("Sliders")]
    public Slider slider1;
    public Slider slider2;
    public Slider slider3;

    [Header("UI Text voor Blokjes (Optioneel)")]
    public Text levelText1;
    public Text levelText2;
    public Text levelText3;
    
    [Header("Basis Instellingen (Lage startwaarden)")]
    public float baseDecreaseSpeed = 18f; // Loopt wat sneller leeg in het begin
    public float baseBumpAmount = 8f;     // Vult minder per klik in het begin

    [Header("Upgrade / Downgrade Grenzen")]
    public int maxLevel = 4;
    public int startLevel = 2;
    public float bumpStep = 4f;           
    public float decreaseSpeedStep = 3f;  
    public float minDecreaseSpeed = 3f;     

    [Header("Gedeelde Punten Pool")]
    public int availableUpgradePoints = 0; // Punten verkregen door te downgraden

    // Individuele waardes per slider
    private float value1, bump1, speed1;
    private int level1;

    private float value2, bump2, speed2;
    private int level2;

    private float value3, bump3, speed3;
    private int level3;

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

        // Initialiseer sliders op startlevel
        InitSlider(slider1, ref value1, ref bump1, ref speed1, ref level1);
        InitSlider(slider2, ref value2, ref bump2, ref speed2, ref level2);
        InitSlider(slider3, ref value3, ref bump3, ref speed3, ref level3);

        UpdateAllUI();
    }

    void InitSlider(Slider targetSlider, ref float val, ref float bump, ref float spd, ref int lvl)
    {
        if (targetSlider != null) val = targetSlider.maxValue;
        lvl = startLevel;
        bump = baseBumpAmount + (lvl - startLevel) * bumpStep;
        spd = Mathf.Max(minDecreaseSpeed, baseDecreaseSpeed - (lvl - startLevel) * decreaseSpeedStep);
    }

    void Update()
    {
        // 1. Laat alle drie de sliders van nature aflopen
        if (value1 > 0) value1 -= speed1 * Time.deltaTime; else value1 = 0;
        if (value2 > 0) value2 -= speed2 * Time.deltaTime; else value2 = 0;
        if (value3 > 0) value3 -= speed3 * Time.deltaTime; else value3 = 0;

        // 2. Toetsenbordbesturing (Cijfers 1, 2 en 3)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                value1 += bump1;
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                value2 += bump2;
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                value3 += bump3;
        }

        // 3. Lees de Arduino uit voor knoppen
        if (stream.IsOpen)
        {
            try
            {
                string data = stream.ReadLine();
                if (!string.IsNullOrEmpty(data))
                {
                    data = data.Trim();
                    if (data == "P1") value1 += bump1;
                    else if (data == "P2") value2 += bump2;
                    else if (data == "P3") value3 += bump3;
                }
            }
            catch (System.TimeoutException) { }
        }

        // 4. Update de visuele UI sliders en blokjes
        UpdateSliderUI(slider1, ref value1);
        UpdateSliderUI(slider2, ref value2);
        UpdateSliderUI(slider3, ref value3);
    }

    void UpdateSliderUI(Slider targetSlider, ref float val)
    {
        if (targetSlider != null)
        {
            val = Mathf.Clamp(val, targetSlider.minValue, targetSlider.maxValue);
            targetSlider.value = val;
        }
    }

    // --- UPGRADE & DOWNGRADE LOGICA MET PUNTENBEHEER ---

    public void UpgradeSlider1() { TryUpgrade(ref level1, ref bump1, ref speed1); UpdateAllUI(); }
    public void DowngradeSlider1() { TryDowngrade(ref level1, ref bump1, ref speed1); UpdateAllUI(); }

    public void UpgradeSlider2() { TryUpgrade(ref level2, ref bump2, ref speed2); UpdateAllUI(); }
    public void DowngradeSlider2() { TryDowngrade(ref level2, ref bump2, ref speed2); UpdateAllUI(); }

    public void UpgradeSlider3() { TryUpgrade(ref level3, ref bump3, ref speed3); UpdateAllUI(); }
    public void DowngradeSlider3() { TryDowngrade(ref level3, ref bump3, ref speed3); UpdateAllUI(); }

    private void TryUpgrade(ref int lvl, ref float bump, ref float spd)
    {
        if (lvl < maxLevel && availableUpgradePoints > 0)
        {
            lvl++;
            availableUpgradePoints--; // Kost 1 punt uit de pool
            RecalculateStats(lvl, out bump, out spd);
            Debug.Log($"Upgrade gelukt! Nieuw level: {lvl}, Resterende punten: {availableUpgradePoints}");
        }
        else
        {
            Debug.Log("Kan niet upgraden: Max level bereikt of geen upgrade-punten beschikbaar! (Downgrade eerst iets anders)");
        }
    }

    private void TryDowngrade(ref int lvl, ref float bump, ref float spd)
    {
        if (lvl > 0)
        {
            lvl--;
            availableUpgradePoints++; // Levert 1 punt op voor de pool
            RecalculateStats(lvl, out bump, out spd);
            Debug.Log($"Downgrade gelukt! Nieuw level: {lvl}, Beschikbare punten: {availableUpgradePoints}");
        }
        else
        {
            Debug.Log("Kan niet verder downgraden: Minimum level (0) bereikt!");
        }
    }

    private void RecalculateStats(int lvl, out float bump, out float spd)
    {
        bump = baseBumpAmount + (lvl - startLevel) * bumpStep;
        spd = Mathf.Max(minDecreaseSpeed, baseDecreaseSpeed - (lvl - startLevel) * decreaseSpeedStep);
    }

    private void UpdateAllUI()
    {
        if (levelText1 != null) levelText1.text = GetBlockString(level1);
        if (levelText2 != null) levelText2.text = GetBlockString(level2);
        if (levelText3 != null) levelText3.text = GetBlockString(level3);
    }

    private string GetBlockString(int currentLevel)
    {
        string blocks = "";
        for (int i = 0; i <= maxLevel; i++)
        {
            if (i <= currentLevel) blocks += "■ ";
            else blocks += "□ ";
        }
        return blocks.Trim();
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