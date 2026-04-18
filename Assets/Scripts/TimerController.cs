using UnityEngine;
using TMPro;

public class TimerController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private int[] presetMinutes = { 15, 25, 45, 60 };

    private float remainingSeconds = 0f;
    private bool isRunning = false;

    private void Update()
    {
        if (!isRunning) return;

        remainingSeconds -= Time.deltaTime;

        if (remainingSeconds <= 0f)
        {
            remainingSeconds = 0f;
            isRunning = false;
            UpdateDisplay();
            return;
        }

        UpdateDisplay();
    }

    public void StartTimer(int minutes)
    {
        remainingSeconds = minutes * 60f;
        isRunning = true;
        UpdateDisplay();
    }

    public void PauseTimer()
    {
        isRunning = false;
    }

    public void ResumeTimer()
    {
        if (remainingSeconds > 0f)
            isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
        remainingSeconds = 0f;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
        int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
