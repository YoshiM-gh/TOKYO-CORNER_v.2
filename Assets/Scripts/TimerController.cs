using UnityEngine;
using TMPro;

public class TimerController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;

    private float elapsedSeconds = 0f;
    private bool isRunning = false;
    private System.DateTime sessionStartUtc;

    private void Update()
    {
        if (!isRunning) return;

        UpdateElapsedSecondsRealtime();
        UpdateDisplay();
    }

    public void StartSession()
    {
        elapsedSeconds = 0f;
        sessionStartUtc = System.DateTime.UtcNow;
        isRunning = true;
        UpdateDisplay();
    }

    public void StopSession()
    {
        if (isRunning) UpdateElapsedSecondsRealtime();
        isRunning = false;
        // 滞在時間は SaveDataManager が GameMode に応じて毎フレーム集計
    }

    private void UpdateElapsedSecondsRealtime()
    {
        float dt = (float)(System.DateTime.UtcNow - sessionStartUtc).TotalSeconds;
        if (dt < 0f) dt = 0f;
        elapsedSeconds = dt;
    }

    private void UpdateDisplay()
    {
        int minutes = Mathf.FloorToInt(elapsedSeconds / 60f);
        int seconds = Mathf.FloorToInt(elapsedSeconds % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
