using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public enum GameMode { Roaming, Focus }
    public GameMode CurrentMode { get; private set; } = GameMode.Roaming;

    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject focusUI;
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 3f, -2f);

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
        playerMovement.SetMovementEnabled(false);
        cameraFollow.SetTarget(seatTransform);
        cameraFollow.SetOffset(focusOffset);
        if (focusUI != null) focusUI.SetActive(true);
    }

    public void ExitFocusMode(Transform playerTransform)
    {
        CurrentMode = GameMode.Roaming;
        playerMovement.SetMovementEnabled(true);
        cameraFollow.SetTarget(playerTransform);
        cameraFollow.SetOffset(roamingOffset);
        if (focusUI != null) focusUI.SetActive(false);
    }
}
