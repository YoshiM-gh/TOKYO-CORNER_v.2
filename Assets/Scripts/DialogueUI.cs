using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 汎用会話ウィンドウ（VN風・画面下部）。スタッフ話しかけ／将来のオープニング(ナギ)で共用する。
/// 立ち絵は左右2枠: 左=プレイヤー(23_Businessman・自動)、右=話しかけた相手(portraitTarget)。
/// どちらも「その場の本人」を専用カメラでバストアップ撮影しRenderTexture表示する
/// （CharacterStage方式の軽量版・複製ステージ不要・アイドルアニメがそのまま映る）。
/// ShowLines(話者名, セリフ配列, 完了時, 相手, holdAfterComplete):
///   クリック（画面全体）: タイプ中→全文表示 ／ 全文表示後→次の行 or 終了。
///   holdAfterComplete=true なら最終行の後も閉じずに残す（raycast透過・メニュー等と共存）。
/// 結線（名前検索）: Panel(全画面クリック受けButton) > Window > NamePlate(NameLabel)
///   / BodyLabel / AdvanceCursor、Panel > PortraitFrameLeft/Right > PortraitImageLeft/Right(RawImage)。
/// カメラ: シーンの "DialoguePortraitCameraLeft/Right"（初期disabled・表示中のみ有効化）。
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [SerializeField] private float charInterval = 0.03f; // タイプライター速度（1文字あたり秒）

    private GameObject _panel;
    private Image _panelImg;         // hold中はraycastを透過させる
    private TMPro.TMP_Text _nameLabel;
    private TMPro.TMP_Text _bodyLabel;
    private GameObject _advanceCursor;
    private TMPro.TMP_Text _advanceTmp;

    // 立ち絵（左=プレイヤー / 右=相手）
    private GameObject _leftFrame;
    private RawImage _leftImage;
    private Camera _leftCam;
    private RenderTexture _leftRT;
    private GameObject _rightFrame;
    private RawImage _rightImage;
    private Camera _rightCam;
    private RenderTexture _rightRT;
    private Transform _player; // 23_Businessman（自動取得・キャッシュ）
    private Behaviour _playerInput; // MovePlayerInput（会話中は移動をロック）
    private bool _playerInputWasEnabled = true; // ロック前の状態（解除時に復元・他システムと衝突しない）
    private bool _movementLocked; // 再入ガード（連鎖ShowLinesでロック中のfalseを元値として上書きしないため）

    private string[] _lines;
    private int _lineIndex;
    private bool _typing;
    private Coroutine _typeCo;
    private Action _onComplete;
    private bool _hold;
    private PortraitSide _speakerSide;
    private float _blinkT;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Bind();
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_leftRT != null) { _leftRT.Release(); _leftRT = null; }
        if (_rightRT != null) { _rightRT.Release(); _rightRT = null; }
    }

    private void Update()
    {
        if (_advanceCursor == null || !_advanceCursor.activeSelf || _advanceTmp == null) return;
        _blinkT += Time.deltaTime; // ▶点滅
        var c = _advanceTmp.color;
        c.a = Mathf.PingPong(_blinkT * 1.6f, 1f) > 0.5f ? 1f : 0.15f;
        _advanceTmp.color = c;
    }

    /// <summary>立ち絵の話者サイド。聞き手側は暗く表示される。</summary>
    public enum PortraitSide { None, Left, Right }

    /// <summary>会話を表示する。portraitTarget=相手（右枠）。プレイヤーは自動で左枠。speakerSide=話者側（聞き手を暗く）。</summary>
    public void ShowLines(string speaker, string[] lines, Action onComplete, Transform portraitTarget = null, bool holdAfterComplete = false, PortraitSide speakerSide = PortraitSide.Right)
    {
        if (_panel == null || lines == null || lines.Length == 0) { onComplete?.Invoke(); return; }
        _lines = lines;
        _lineIndex = 0;
        _onComplete = onComplete;
        _hold = holdAfterComplete;
        if (_panelImg != null) _panelImg.raycastTarget = true; // hold透過からの復帰
        if (_nameLabel != null) _nameLabel.text = speaker ?? "";
        _speakerSide = speakerSide;
        SetupPortraits(portraitTarget);
        SetPlayerMovementLocked(true); // 会話中は移動禁止（立ち絵カメラから外れない・所作としても自然）
        _panel.SetActive(true);
        StartTypeLine();
    }

    /// <summary>即座に閉じる（コールバックは呼ばない）</summary>
    public void CloseImmediate()
    {
        StopTypingCo();
        TeardownPortraits();
        SetPlayerMovementLocked(false);
        _onComplete = null;
        _hold = false;
        if (_panelImg != null) _panelImg.raycastTarget = true;
        if (_panel != null) _panel.SetActive(false);
    }

    /// <summary>クリック送り（Panelの全画面Buttonから呼ばれる）</summary>
    public void Advance()
    {
        if (!IsOpen) return;
        if (_typing)
        {
            StopTypingCo(); // タイプ中 → 全文即表示
            if (_bodyLabel != null) _bodyLabel.text = _lines[_lineIndex];
            ApplyHighlight(false);
            if (_advanceCursor != null) { _advanceCursor.SetActive(true); _blinkT = 0f; }
            return;
        }
        _lineIndex++;
        if (_lineIndex < _lines.Length) { StartTypeLine(); return; }
        var cb = _onComplete;
        if (_hold)
        {
            // 閉じずに残す（メニュー等と共存）。クリックは透過・▶は消灯
            _onComplete = null;
            if (_advanceCursor != null) _advanceCursor.SetActive(false);
            if (_panelImg != null) _panelImg.raycastTarget = false;
            cb?.Invoke();
            return;
        }
        CloseImmediate();
        cb?.Invoke();
    }

    private void StartTypeLine()
    {
        ApplyHighlight(true); // セリフが流れている間だけ話者を立てる
        if (_advanceCursor != null) _advanceCursor.SetActive(false);
        StopTypingCo();
        _typeCo = StartCoroutine(TypeRoutine(_lines[_lineIndex]));
    }

    private IEnumerator TypeRoutine(string text)
    {
        _typing = true;
        if (_bodyLabel != null) _bodyLabel.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            if (_bodyLabel != null) _bodyLabel.text = text.Substring(0, i + 1);
            yield return new WaitForSeconds(charInterval);
        }
        _typing = false;
        ApplyHighlight(false); // 待ち（間）は両方ニュートラル
        if (_advanceCursor != null) { _advanceCursor.SetActive(true); _blinkT = 0f; }
        _typeCo = null;
    }

    private void StopTypingCo()
    {
        if (_typeCo != null) { StopCoroutine(_typeCo); _typeCo = null; }
        _typing = false;
    }

    // ── 立ち絵（左右バストアップ） ──────────────────────────

    private void SetupPortraits(Transform rightTarget)
    {
        // 左: プレイヤー（自動取得）
        if (_player == null)
        {
            var pgo = GameObject.Find("23_Businessman");
            if (pgo == null) pgo = GameObject.FindWithTag("Player");
            if (pgo != null) _player = pgo.transform;
        }
        // 話しかけた相手の方を向く（立ち絵が正面になる・自然な所作）
        if (rightTarget != null && _player != null)
        {
            Vector3 dir = rightTarget.position - _player.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) _player.rotation = Quaternion.LookRotation(dir);
        }

        bool useRight = rightTarget != null && _rightCam != null && _rightImage != null;
        bool useLeft = useRight && _player != null && _leftCam != null && _leftImage != null; // 相手が居る会話のみ両者表示

        if (useLeft)
        {
            if (_leftRT == null) _leftRT = new RenderTexture(512, 640, 16) { name = "DialoguePortraitRT_L" };
            AimCamera(_leftCam, _leftRT, _leftImage, _player);
        }
        else if (_leftCam != null) _leftCam.enabled = false;
        if (_leftFrame != null) _leftFrame.SetActive(useLeft);

        if (useRight)
        {
            if (_rightRT == null) _rightRT = new RenderTexture(512, 640, 16) { name = "DialoguePortraitRT_R" };
            AimCamera(_rightCam, _rightRT, _rightImage, rightTarget);
        }
        else if (_rightCam != null) _rightCam.enabled = false;
        if (_rightFrame != null) _rightFrame.SetActive(useRight);

    }

    /// <summary>話者ハイライト。speaking中のみ聞き手側を暗くし、待ち（間）は両方ニュートラルに戻す。</summary>
    private void ApplyHighlight(bool speaking)
    {
        var dim = new Color(0.45f, 0.45f, 0.45f, 1f);
        Color l = Color.white, r = Color.white;
        if (speaking)
        {
            if (_speakerSide == PortraitSide.Right) l = dim;
            else if (_speakerSide == PortraitSide.Left) r = dim;
        }
        if (_leftImage != null) _leftImage.color = l;
        if (_rightImage != null) _rightImage.color = r;
    }

    private static void AimCamera(Camera cam, RenderTexture rt, RawImage img, Transform target)
    {
        cam.targetTexture = rt;
        img.texture = rt;
        // 本人の顔の前にカメラを置く（バストアップ・その場のアニメがそのまま映る）
        Vector3 face = target.position + Vector3.up * 1.4f;
        cam.transform.position = face + target.forward * 1.15f + Vector3.up * 0.05f;
        cam.transform.LookAt(face);
        cam.fieldOfView = 32f;
        cam.enabled = true;
    }

    private void TeardownPortraits()
    {
        if (_leftCam != null) { _leftCam.enabled = false; _leftCam.targetTexture = null; }
        if (_rightCam != null) { _rightCam.enabled = false; _rightCam.targetTexture = null; }
        if (_leftFrame != null) _leftFrame.SetActive(false);
        if (_rightFrame != null) _rightFrame.SetActive(false);
    }

    /// <summary>会話中のプレイヤー移動ロック（MovePlayerInputを無効化）</summary>
    private void SetPlayerMovementLocked(bool locked)
    {
        if (_player == null) return;
        if (_playerInput == null)
        {
            // namespace(Controller.*)に依存しない名前検索
            foreach (var b in _player.GetComponents<Behaviour>())
                if (b != null && b.GetType().Name == "MovePlayerInput") { _playerInput = b; break; }
        }
        if (_playerInput == null) return;
        if (locked)
        {
            if (!_movementLocked) // 初回ロック時のみ元値を保存（見送りセリフ等の連鎖ShowLinesで上書きしない）
            {
                _playerInputWasEnabled = _playerInput.enabled;
                _movementLocked = true;
            }
            _playerInput.enabled = false;
        }
        else if (_movementLocked)
        {
            _playerInput.enabled = _playerInputWasEnabled; // 元の状態へ復元
            _movementLocked = false;
        }
    }

    // ── 結線 ─────────────────────────────────────────────────

    private Transform FindDeep(Transform root, string goName)
    {
        if (root.name == goName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), goName);
            if (r != null) return r;
        }
        return null;
    }

    private void Bind()
    {
        var p = FindDeep(transform, "Panel");
        _panel = p != null ? p.gameObject : null;
        _panelImg = _panel != null ? _panel.GetComponent<Image>() : null;
        var nl = FindDeep(transform, "NameLabel");
        _nameLabel = nl != null ? nl.GetComponent<TMPro.TMP_Text>() : null;
        var bl = FindDeep(transform, "BodyLabel");
        _bodyLabel = bl != null ? bl.GetComponent<TMPro.TMP_Text>() : null;
        var ac = FindDeep(transform, "AdvanceCursor");
        _advanceCursor = ac != null ? ac.gameObject : null;
        _advanceTmp = _advanceCursor != null ? _advanceCursor.GetComponent<TMPro.TMP_Text>() : null;

        var lf = FindDeep(transform, "PortraitFrameLeft");
        _leftFrame = lf != null ? lf.gameObject : null;
        var li = FindDeep(transform, "PortraitImageLeft");
        _leftImage = li != null ? li.GetComponent<RawImage>() : null;
        var rf = FindDeep(transform, "PortraitFrameRight");
        _rightFrame = rf != null ? rf.gameObject : null;
        var ri = FindDeep(transform, "PortraitImageRight");
        _rightImage = ri != null ? ri.GetComponent<RawImage>() : null;
        var camL = GameObject.Find("DialoguePortraitCameraLeft");
        _leftCam = camL != null ? camL.GetComponent<Camera>() : null;
        var camR = GameObject.Find("DialoguePortraitCameraRight");
        _rightCam = camR != null ? camR.GetComponent<Camera>() : null;

        var clickBtn = _panel != null ? _panel.GetComponent<Button>() : null;
        if (clickBtn != null) clickBtn.onClick.AddListener(Advance);
    }
}
