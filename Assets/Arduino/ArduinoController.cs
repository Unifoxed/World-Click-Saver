using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using UnityEngine.InputSystem;
using System.Collections;

public class MultiSliderGameController : MonoBehaviour
{
    SerialPort stream = new SerialPort("/dev/cu.usbmodem2101", 9600);

    [Header("Audio")]
    public AudioSource crisisAudioSource;     // Voor de crisis events (15 sec)
    public AudioSource countdownAudioSource;  // Voor het aftellen (tikt per seconde)
    public AudioSource redEarthAudioSource;   // Geluid dat meebeweegt met het rood worden van de aarde
    [Range(0f, 1f)] public float maxRedAudioVolume = 0.5f; // Hoe hard het vuurgeluid maximaal mag klinken (0.0 tot 1.0)

    [Header("Aarde Emission Instellingen")]
    public Renderer earthRenderer;            // Sleep hier de Mesh Renderer van je aarde naartoe
    public Color normalEmissionColor = Color.black; // Kleur bij veilige waarden
    public Color alertEmissionColor = Color.red;    // Kleur bij 0
    public float maxEmissionIntensity = 5f;         // Hoe fel de HDR glow maximaal mag worden bij 0
    private Material earthMaterial;

    [Header("Sliders")]
    public Slider slider1; 
    public Slider slider2; 
    public Slider slider3; 
    public Slider mainSlider; 
    public Slider winOrLoseSlider; 

    [Header("Win / Lose Slider Instellingen")]
    public float dangerTimeThreshold = 10f; 
    public float winLoseChangeSpeed = 20f;  

    [Header("UI Text voor Blokjes")]
    public Text levelText1;
    public Text levelText2;
    public Text levelText3;
    public Text countdownText; 

    [Header("Status Background Logo's")]
    public GameObject statusLogoPeople; 
    public GameObject statusLogoProfit; 
    public GameObject statusLogoPlanet; 

    [Header("Popup Logo's")]
    public GameObject popupLogoPeople; 
    public GameObject popupLogoProfit; 
    public GameObject popupLogoPlanet; 

    [Header("Centrale Eind Iconen (0 = Doodshoofd, 100 = Peace)")]
    public GameObject centerSkullObject;      // Sleep hier het Doodshoofd GameObject in
    public GameObject centerPeaceObject;      // Sleep hier het Peace GameObject in
    public float popupIconMaxSize = 2.5f;     // Hoe groot het logo in het midden moet verschijnen

    [Header("Schermen")]
    public GameObject popupPanel; 
    public Image statusBackgroundImage; 

    [Header("Basis Instellingen & Timings")]
    public float baseDecreaseSpeed = 40f;  
    public float baseBumpAmount = 25f;    
    public float startDelay = 5f;         
    public float firstEventDelay = 10f;   
    public float subsequentEventInterval = 30f; 
    public float crisisDuration = 15f;    

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
    private float crisisTimer = 0f;       
    private float dangerTimer = 0f;       
    private int activeEventIndex = 0; 
    private int eventsCompleted = 0;
    public int totalEventsToWin = 5; 
    private bool gameEnded = false;
    private int lastCountdownSecond = -1; 
    private Color originalStatusColor = Color.white;
    private Coroutine popupBlinkRoutine;
    private Coroutine crisisAudioStopRoutine;

    void Start()
    {
        stream.ReadTimeout = 25;
        try { stream.Open(); Debug.Log("Serial Port geopend!"); }
        catch (System.Exception e) { Debug.LogError("Kon poort niet openen: " + e.Message); }

        if (crisisAudioSource == null)
        {
            crisisAudioSource = GetComponent<AudioSource>();
        }

        if (redEarthAudioSource != null)
        {
            redEarthAudioSource.loop = true;
            redEarthAudioSource.volume = 0f;
            redEarthAudioSource.Play();
        }

        if (earthRenderer != null)
        {
            earthMaterial = earthRenderer.material;
            earthMaterial.EnableKeyword("_EMISSION");
        }

        InitSlider(slider1, ref value1, ref bump1, ref speed1, ref level1);
        InitSlider(slider2, ref value2, ref bump2, ref speed2, ref level2);
        InitSlider(slider3, ref value3, ref bump3, ref speed3, ref level3);

        if (mainSlider != null)
        {
            mainSlider.minValue = 0f;
            mainSlider.maxValue = 100f;
            mainSlider.value = 50f; 
        }

        if (winOrLoseSlider != null)
        {
            winOrLoseSlider.minValue = 0f;
            winOrLoseSlider.maxValue = 100f;
            winOrLoseSlider.value = 50f; 
        }

        if (statusBackgroundImage != null)
        {
            originalStatusColor = statusBackgroundImage.color;
        }

        // Zet de centrale eind-icoontjes uit bij de start
        if (centerSkullObject != null) centerSkullObject.SetActive(false);
        if (centerPeaceObject != null) centerPeaceObject.SetActive(false);

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
            
            int currentSecond = Mathf.CeilToInt(startTimer);
            if (currentSecond != lastCountdownSecond && currentSecond > 0)
            {
                lastCountdownSecond = currentSecond;
                if (countdownAudioSource != null)
                {
                    countdownAudioSource.Play();
                }
            }

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                countdownText.text = currentSecond.ToString(); 
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

        if (activeEventIndex > 0)
        {
            crisisTimer -= Time.deltaTime;
            if (crisisTimer <= 0)
            {
                activeEventIndex = 0;
                HideAllStatusLogos();
                if (statusBackgroundImage != null) statusBackgroundImage.color = originalStatusColor;

                if (crisisAudioSource != null && crisisAudioSource.isPlaying)
                {
                    crisisAudioSource.Stop();
                }
            }
            else if (statusBackgroundImage != null)
            {
                float lerp = Mathf.PingPong(Time.time * 4f, 1f); 
                statusBackgroundImage.color = Color.Lerp(originalStatusColor, Color.red, lerp);
            }
        }

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
        UpdateWinOrLoseSlider();
        UpdateEarthEmissionAndAudio(); 
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
            if (winOrLoseSlider != null) winOrLoseSlider.value = 100f;
            WinGame();
        }
    }

    void UpdateWinOrLoseSlider()
    {
        if (mainSlider == null || winOrLoseSlider == null) return;

        float mainVal = mainSlider.value;

        if (mainVal >= 0f && mainVal <= 10f)
        {
            dangerTimer += Time.deltaTime;
            if (dangerTimer >= dangerTimeThreshold)
            {
                winOrLoseSlider.value -= winLoseChangeSpeed * Time.deltaTime;
            }
        }
        else if (mainVal >= 90f && mainVal <= 100f)
        {
            dangerTimer = 0f; 
            winOrLoseSlider.value += winLoseChangeSpeed * Time.deltaTime;
        }
        else
        {
            dangerTimer = 0f; 
        }

        winOrLoseSlider.value = Mathf.Clamp(winOrLoseSlider.value, winOrLoseSlider.minValue, winOrLoseSlider.maxValue);

        // --- SCHERM GROOT MAKEN BIJ 0 OF 100 ---
        if (winOrLoseSlider.value <= 0f)
        {
            if (centerSkullObject != null && !centerSkullObject.activeSelf)
            {
                centerSkullObject.SetActive(true);
                RectTransform skullRect = centerSkullObject.GetComponent<RectTransform>();
                if (skullRect != null) skullRect.localScale = Vector3.one * popupIconMaxSize;
            }
            LoseGame();
        }
        else if (winOrLoseSlider.value >= 100f)
        {
            if (centerPeaceObject != null && !centerPeaceObject.activeSelf)
            {
                centerPeaceObject.SetActive(true);
                RectTransform peaceRect = centerPeaceObject.GetComponent<RectTransform>();
                if (peaceRect != null) peaceRect.localScale = Vector3.one * popupIconMaxSize;
            }
            WinGame();
        }
    }

    void UpdateEarthEmissionAndAudio()
    {
        if (mainSlider == null) return;

        float mainVal = mainSlider.value;
        float emissionFactor = 0f;

        if (mainVal <= 25f)
        {
            emissionFactor = Mathf.InverseLerp(25f, 0f, mainVal);
        }

        if (earthMaterial != null)
        {
            Color currentEmission = Color.Lerp(normalEmissionColor, alertEmissionColor, emissionFactor);
            currentEmission *= Mathf.Lerp(1f, maxEmissionIntensity, emissionFactor);
            earthMaterial.SetColor("_EmissionColor", currentEmission);
        }

        if (redEarthAudioSource != null)
        {
            redEarthAudioSource.volume = emissionFactor * maxRedAudioVolume;
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
        crisisTimer = crisisDuration; 
        
        UpdateStatusLogosDisplay();

        if (crisisAudioSource != null)
        {
            if (crisisAudioStopRoutine != null) StopCoroutine(crisisAudioStopRoutine);

            crisisAudioSource.Play();
            crisisAudioStopRoutine = StartCoroutine(StopAudioAfterSeconds(crisisDuration));
        }

        if (popupBlinkRoutine != null) StopCoroutine(popupBlinkRoutine);
        popupBlinkRoutine = StartCoroutine(BlinkPopupRoutine());
    }

    IEnumerator StopAudioAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (crisisAudioSource != null && crisisAudioSource.isPlaying)
        {
            crisisAudioSource.Stop();
        }
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

    void LoseGame()
    {
        gameEnded = true;
        HideAllStatusLogos();
        HideAllPopupLogos();
        if (popupPanel != null) popupPanel.SetActive(false);
        if (statusBackgroundImage != null) statusBackgroundImage.color = originalStatusColor;
        Debug.Log("VERLOREN! De balans is te ver doorgeslagen.");
    }

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