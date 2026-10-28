using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using UnityEngine.InputSystem;
using System.Collections;

public class MultiSliderGameController : MonoBehaviour
{
    SerialPort stream = new SerialPort("/dev/cu.usbmodem2101", 9600);

    [Header("Sliders")]
    public Slider slider1; 
    public Slider slider2; 
    public Slider slider3; 
    public Slider mainSlider; 

    [Header("UI Text voor Blokjes")]
    public Text levelText1;
    public Text levelText2;
    public Text levelText3;
    public Text countdownText; 

    [Header("Status Background Logo's (Staan in Canvas onder StatusBackground)")]
    public GameObject statusLogoPeople; 
    public GameObject statusLogoProfit; 
    public GameObject statusLogoPlanet; 

    [Header("Popup Logo's (Staan in Canvas onder het Popup Panel)")]
    public GameObject popupLogoPeople; 
    public GameObject popupLogoProfit; 
    public GameObject popupLogoPlanet; 

    [Header("Schermen")]
    public GameObject popupPanel; 
    public Image statusBackgroundImage; 

    [Header("Basis Instellingen & Timings")]
    public float baseDecreaseSpeed = 40f;  // Basissnelheid per seconde
    public float baseBumpAmount = 25f;    // Vult per klik 25% op
    public float startDelay = 5f;         
    public float firstEventDelay = 10f;   
    public float subsequentEventInterval = 30f; 

    [Header("Upgrade / Downgrade Belang & Balans")]
    public int maxLevel = 4;
    public int startLevel = 2;
    public float bumpStep = 10f;          
    public float decreaseSpeedStep = 8f;  
    public float minDecreaseSpeed = 10f;  

    [Header("Gedeelde Punten Pool")]
    public int availableUpgradePoints = 0; 

    private float value1, bump1, speed1;
    private int level1;
    private float value2, bump2, speed2;
    private int level2;
    private float value3, bump3, speed3;
    private int level3;

    private float startTimer = 0f;
    private float eventTimer = 0f;
    private int activeEventIndex = 0; 
    private int eventsCompleted = 0;
    public int totalEventsToWin = 5; 
    private bool gameEnded = false;
    private Color originalStatusColor = Color.white;
    private Coroutine popupBlinkRoutine;

    void Start()
    {
        stream.ReadTimeout = 25;
        try { stream.Open(); Debug.Log("Serial Port geopend!"); }
        catch (System.Exception e) { Debug.LogError("Kon poort niet openen: " + e.Message); }

        InitSlider(slider1, ref value1, ref bump1, ref speed1, ref level1);
        InitSlider(slider2, ref value2, ref bump2, ref speed2, ref level2);
        InitSlider(slider3, ref value3, ref bump3, ref speed3, ref level3);

        if (mainSlider != null)
        {
            mainSlider.minValue = 0f;
            mainSlider.maxValue = 100f;
            mainSlider.value = 50f; 
        }

        if (statusBackgroundImage != null)
        {
            originalStatusColor = statusBackgroundImage.color;
        }

        startTimer = startDelay;
        eventTimer = firstEventDelay; 
        
        HideAllStatusLogos();
        HideAllPopupLogos();
        if (popupPanel != null) popupPanel.SetActive(false); 
        UpdateAllUI();
    }

    void InitSlider(Slider targetSlider, ref float val, ref float bump, ref float spd, ref int lvl)
    {
        if (targetSlider != null) 
        {
            targetSlider.minValue = 0f;
            targetSlider.maxValue = 100f;
            val = targetSlider.maxValue;
        }
        lvl = startLevel;
        RecalculateStats(lvl, out bump, out spd);
    }

    void Update()
    {
        if (gameEnded) return;

        if (startTimer > 0)
        {
            startTimer -= Time.deltaTime;
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                countdownText.text = Mathf.Ceil(startTimer).ToString(); 
            }
            return; 
        }
        else
        {
            if (countdownText != null && countdownText.gameObject.activeSelf)
            {
                countdownText.gameObject.SetActive(false); 
            }
        }

        eventTimer -= Time.deltaTime;
        if (eventTimer <= 0)
        {
            TriggerNextEvent();
            eventTimer = subsequentEventInterval; 
        }

        if (activeEventIndex > 0 && statusBackgroundImage != null)
        {
            float lerp = Mathf.PingPong(Time.time * 4f, 1f); 
            statusBackgroundImage.color = Color.Lerp(originalStatusColor, Color.red, lerp);
        }

        // EXTREME EVENT DRUK: Tijdens een event vermenigvuldigt de daling met maar liefst 5.0x!
        float spd1 = speed1 * (activeEventIndex == 1 ? 5.0f : 1f);
        float spd2 = speed2 * (activeEventIndex == 2 ? 5.0f : 1f);
        float spd3 = speed3 * (activeEventIndex == 3 ? 5.0f : 1f);

        if (value1 > 0) value1 -= spd1 * Time.deltaTime; else value1 = 0;
        if (value2 > 0) value2 -= spd2 * Time.deltaTime; else value2 = 0;
        if (value3 > 0) value3 -= spd3 * Time.deltaTime; else value3 = 0;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) value1 += bump1;
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) value2 += bump2;
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) value3 += bump3;
        }

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

        UpdateSliderUI(slider1, ref value1);
        UpdateSliderUI(slider2, ref value2);
        UpdateSliderUI(slider3, ref value3);

        UpdateMainSliderHealth();
    }

    void UpdateMainSliderHealth()
    {
        if (mainSlider == null) return;

        int slidersBelow75 = 0;
        
        if (slider1 != null && value1 < slider1.maxValue * 0.75f) slidersBelow75++;
        if (slider2 != null && value2 < slider2.maxValue * 0.75f) slidersBelow75++;
        if (slider3 != null && value3 < slider3.maxValue * 0.75f) slidersBelow75++;

        float healthChangeRate = 4f; 

        if (slidersBelow75 == 0)
        {
            mainSlider.value += healthChangeRate * Time.deltaTime;
        }
        else
        {
            float penaltyMultiplier = slidersBelow75 * 1.5f;
            mainSlider.value -= healthChangeRate * penaltyMultiplier * Time.deltaTime;
        }

        if (eventsCompleted >= totalEventsToWin && mainSlider.value >= mainSlider.maxValue)
        {
            WinGame();
        }
    }

    void TriggerNextEvent()
    {
        eventsCompleted++;
        if (eventsCompleted > totalEventsToWin)
        {
            activeEventIndex = 0;
            HideAllStatusLogos();
            HideAllPopupLogos();
            if (popupPanel != null) popupPanel.SetActive(false);
            if (statusBackgroundImage != null) statusBackgroundImage.color = originalStatusColor;
            return;
        }

        activeEventIndex = Random.Range(1, 4);
        
        UpdateStatusLogosDisplay();

        if (popupBlinkRoutine != null) StopCoroutine(popupBlinkRoutine);
        popupBlinkRoutine = StartCoroutine(BlinkPopupRoutine());
    }

    IEnumerator BlinkPopupRoutine()
    {
        if (popupPanel == null) yield break;

        HideAllPopupLogos();
        if (activeEventIndex == 1 && popupLogoPeople != null) popupLogoPeople.SetActive(true);
        if (activeEventIndex == 2 && popupLogoProfit != null) popupLogoProfit.SetActive(true);
        if (activeEventIndex == 3 && popupLogoPlanet != null) popupLogoPlanet.SetActive(true);

        int blinkCount = 3;
        for (int i = 0; i < blinkCount; i++)
        {
            popupPanel.SetActive(true);
            yield return new WaitForSeconds(0.3f); 
            popupPanel.SetActive(false);
            yield return new WaitForSeconds(0.3f); 
        }

        HideAllPopupLogos();
    }

    void UpdateStatusLogosDisplay()
    {
        HideAllStatusLogos();
        if (activeEventIndex == 1 && statusLogoPeople != null) statusLogoPeople.SetActive(true);
        if (activeEventIndex == 2 && statusLogoProfit != null) statusLogoProfit.SetActive(true);
        if (activeEventIndex == 3 && statusLogoPlanet != null) statusLogoPlanet.SetActive(true);
    }

    void HideAllStatusLogos()
    {
        if (statusLogoPeople != null) statusLogoPeople.SetActive(false);
        if (statusLogoProfit != null) statusLogoProfit.SetActive(false);
        if (statusLogoPlanet != null) statusLogoPlanet.SetActive(false);
    }

    void HideAllPopupLogos()
    {
        if (popupLogoPeople != null) popupLogoPeople.SetActive(false);
        if (popupLogoProfit != null) popupLogoProfit.SetActive(false);
        if (popupLogoPlanet != null) popupLogoPlanet.SetActive(false);
    }

    void WinGame()
    {
        gameEnded = true;
        HideAllStatusLogos();
        HideAllPopupLogos();
        if (popupPanel != null) popupPanel.SetActive(false);
        if (statusBackgroundImage != null) statusBackgroundImage.color = originalStatusColor;
        Debug.Log("GEFELICITEERD! De wereld is gered!");
    }

    // --- UPGRADE & DOWNGRADE LOGICA ---
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
            availableUpgradePoints--;
            RecalculateStats(lvl, out bump, out spd);
        }
    }

    private void TryDowngrade(ref int lvl, ref float bump, ref float spd)
    {
        if (lvl > 0)
        {
            lvl--;
            availableUpgradePoints++;
            RecalculateStats(lvl, out bump, out spd);
        }
    }

    private void RecalculateStats(int lvl, out float bump, out float spd)
    {
        bump = baseBumpAmount + (lvl - startLevel) * bumpStep;
        spd = Mathf.Max(minDecreaseSpeed, baseDecreaseSpeed - (lvl - startLevel) * decreaseSpeedStep);
    }

    void UpdateSliderUI(Slider targetSlider, ref float val)
    {
        if (targetSlider != null)
        {
            val = Mathf.Clamp(val, targetSlider.minValue, targetSlider.maxValue);
            targetSlider.value = val;
        }
    }

    void UpdateAllUI()
    {
        if (levelText1 != null) levelText1.text = GetBlockString(level1);
        if (levelText2 != null) levelText2.text = GetBlockString(level2);
        if (levelText3 != null) levelText3.text = GetBlockString(level3);
    }

    string GetBlockString(int currentLevel)
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
        if (stream != null && stream.IsOpen) { stream.Close(); }
    }
}