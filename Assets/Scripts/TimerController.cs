using UnityEngine;
using TMPro;

public class TimerController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;

    private float elapsedSeconds = 0f;
    private bool isRunning = false;

    private void Update()
    {
        if (!isRunning) return;

        elapsedSeconds += Time.deltaTime;
        UpdateDisplay();
    }

    public void StartSession()
    {
        elapsedSeconds = 0f;
        isRunning = true;
        UpdateDisplay();
    }

    public void StopSession()
    {
        isRunning = false;
        // 滞在時間は SaveDataManager が GameMode に応じて毎フレーム集計
    }

    private void UpdateDisplay()
    {
        int minutes = Mathf.FloorToInt(elapsedSeconds / 60f);
        int seconds = Mathf.FloorToInt(elapsedSeconds % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
