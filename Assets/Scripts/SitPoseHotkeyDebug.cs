using Controller;
using UnityEngine;
using UnityEngine.InputSystem;

public class SitPoseHotkeyDebug : MonoBehaviour
{
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private string sitBoolParameter = "IsSitting";
    [SerializeField] private string sitStateName = "Adult_SitTableIdle";
    [SerializeField] private string moveStateName = "Movement";
    [SerializeField] private KeyCode toggleKey = KeyCode.T;
    [SerializeField] private bool disableMoverWhileSitting = true;
    [SerializeField] private bool forcePlayState = true;

    private CharacterMover characterMover;
    private bool isSitting;

    private void Awake()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>();

        characterMover = GetComponent<CharacterMover>();
    }

    private void Update()
    {
        bool pressed = Input.GetKeyDown(toggleKey) || IsNewInputPressed(toggleKey);

        if (!pressed) return;
        ToggleSit();
    }

    private static bool IsNewInputPressed(KeyCode keyCode)
    {
        if (Keyboard.current == null) return false;

        if (keyCode == KeyCode.T) return Keyboard.current.tKey.wasPressedThisFrame;
        if (keyCode == KeyCode.Y) return Keyboard.current.yKey.wasPressedThisFrame;
        if (keyCode == KeyCode.U) return Keyboard.current.uKey.wasPressedThisFrame;
        if (keyCode == KeyCode.F8) return Keyboard.current.f8Key.wasPressedThisFrame;
        return false;
    }

    public void ToggleSit()
    {
        SetSitState(!isSitting);
    }

    public void SetSitState(bool sit)
    {
        isSitting = sit;

        if (targetAnimator != null)
        {
            targetAnimator.SetBool(sitBoolParameter, isSitting);
            if (forcePlayState)
            {
                string state = isSitting ? sitStateName : moveStateName;
                targetAnimator.CrossFadeInFixedTime(state, 0.05f, 0, 0f);
            }
        }

        if (characterMover != null && disableMoverWhileSitting)
            characterMover.enabled = !isSitting;

        Debug.Log($"[SitDebug] sit={isSitting} param={sitBoolParameter} animator={(targetAnimator != null ? targetAnimator.name : "null")}");
    }
}
