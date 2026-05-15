using Controller;
using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public enum GameMode { Roaming, Focus }
    public GameMode CurrentMode { get; private set; } = GameMode.Roaming;

    [SerializeField] private MovePlayerInput movePlayerInput;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject focusUI;
    [SerializeField] private TimerController timerController;
    [SerializeField] private Transform focusCameraPoint;
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 2f, 1.5f);
    [SerializeField] private Vector3 focusLookAtOffset = new Vector3(0f, -0.5f, 0f);

    private Vector3 roamingOffset;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        roamingOffset = cameraFollow.GetOffset();
    }

    public void EnterFocusMode(Transform seatTransform)
    {
        CurrentMode = GameMode.Focus;
        if (movePlayerInput != null) movePlayerInput.enabled = false;
        cameraFollow.SetTarget(seatTransform);
        cameraFollow.SetOffset(focusOffset);
        
        // ✅ 椅子固有の FocusCameraPoint を自動検索
        Transform chairFocusPoint = FindFocusCameraPoint(seatTransform);
        
        if (chairFocusPoint != null)
        {
            cameraFollow.SetFocusTransform(chairFocusPoint);
            Debug.Log($"[Focus] Using FocusCameraPoint from {seatTransform.parent.name}");
        }
        else if (focusCameraPoint != null)
        {
            // フォールバック：Inspector で設定された global FocusCameraPoint
            cameraFollow.SetFocusTransform(focusCameraPoint);
            Debug.LogWarning($"[Focus] Chair {seatTransform.parent.name} has no FocusCameraPoint, using global");
        }
        else
        {
            cameraFollow.SetLookAt(true);
            cameraFollow.SetLookAtOffset(focusLookAtOffset);
        }
        if (FocusOverlayController.Instance != null) FocusOverlayController.Instance.OpenSettings();
        else if (focusUI != null) focusUI.SetActive(true);
        if (timerController != null) timerController.StartSession();
        Debug.Log("[Focus] Entered focus mode.");
    }

    public void ExitFocusMode(Transform playerTransform)
    {
        CurrentMode = GameMode.Roaming;
        if (movePlayerInput != null) movePlayerInput.enabled = true;
        cameraFollow.SetTarget(playerTransform);
        cameraFollow.SetOffset(roamingOffset);
        cameraFollow.ClearFocusTransform();
        cameraFollow.SetLookAt(false);
        if (FocusOverlayController.Instance != null) FocusOverlayController.Instance.Close();
        else if (focusUI != null) focusUI.SetActive(false);
        if (timerController != null) timerController.StopSession();
        Debug.Log("[Focus] Exited focus mode.");
    }

    /// <summary>
    /// SitPoint の親（椅子）から FocusCameraPoint を検索
    /// </summary>
    private Transform FindFocusCameraPoint(Transform sitPoint)
    {
        if (sitPoint == null) return null;
        
        // SitPoint の親（椅子）を取得
        Transform chair = sitPoint.parent;
        if (chair == null) return null;
        
        // 椅子の直下から FocusCameraPoint を探す
        foreach (Transform child in chair)
        {
            if (child.name == "FocusCameraPoint")
            {
                return child;
            }
        }
        
        return null;
    }
}
