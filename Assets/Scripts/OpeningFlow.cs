using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// オープニング（ナギの登録面接）。Opening.unity 専用の進行スクリプト。
/// MOTHER2式: 濃グレー空間・中央にナギ本人・中央上のボックスでセリフ⇔入力が入れ替わる。
/// 流れ: 迎え→名前→飲み物+温度→食べ物→一言2つ(任意)→仮ID告知→締め→Cafeへ。
/// ※言葉1/言葉2（二つ名）の選択ステップは次段で名前の後に挿入する。
/// 結線(名前検索): OpeningCanvas > ClickCatcher / Box > BodyLabel / AdvanceCursor
///   / NameInput(TMP_InputField) / OKButton / ChoiceAButton / ChoiceBButton
/// </summary>
public class OpeningFlow : MonoBehaviour
{
    [SerializeField] private float charInterval = 0.04f;
    [SerializeField] private AvatarCatalog catalog; // 見た目選択の台帳（Openingシーンで割当）

    private GameObject[] _avatarPreviews;
    private int _avatarIndex;
    private string _avatarLabel = "";

    private TMPro.TMP_Text _body;
    private GameObject _advanceGO;
    private TMPro.TMP_Text _advanceTmp;
    private TMPro.TMP_InputField _input;
    private GameObject _inputGO;
    private GameObject _okGO;
    private Button _ok;
    private GameObject _choiceAGO, _choiceBGO;
    private Button _choiceA, _choiceB;
    private TMPro.TMP_Text _choiceALabel, _choiceBLabel;

    private bool _typing;
    private bool _advanceClicked;
    private bool _okClicked;
    private int _choiceClicked; // 0=none 1=A 2=B
    private float _blinkT;

    // 収集データ
    private string _name = "", _drink = "", _food = "", _tasty = "", _full = "";
    private bool _hot = true;

    private void Start()
    {
        Bind();
        HideInputs();
        SetupAvatarPreviews();
        if (_advanceGO != null) _advanceGO.SetActive(false);
        StartCoroutine(Flow());
    }

    private void Update()
    {
        if (_advanceGO == null || !_advanceGO.activeSelf || _advanceTmp == null) return;
        _blinkT += Time.deltaTime; // ▶点滅
        var c = _advanceTmp.color;
        c.a = Mathf.PingPong(_blinkT * 1.6f, 1f) > 0.5f ? 1f : 0.15f;
        _advanceTmp.color = c;
    }

    // ── 台本（承認済みドラフト） ──────────────────────────

    private IEnumerator Flow()
    {
        yield return Say("TOKYO CORNERへようこそ。");
        yield return Say("わたしはナギ。この店の記録係です。");
        yield return Say("開店の前に、あなたの登録を行います。");

        yield return Say("まず、あなたの見た目を教えてください。");
        yield return AskAvatar();
        yield return Say(_avatarLabel + "、ですね。");

        yield return Say("お名前を教えてください。");
        yield return AskInput(v => _name = v, 8, false, "（8文字まで）");
        yield return Say(_name + "様。登録しました。");

        // TODO(次段): 言葉1/言葉2（二つ名）の選択をここに挿入

        yield return Say("あなたの好きな飲み物は何ですか。");
        yield return AskInput(v => _drink = v, 12, false, "（ここに入力）");
        yield return Say("それは、温かいものですか。冷たいものですか。");
        yield return AskChoice("ホット", "アイス", isA => _hot = isA);
        yield return Say("好きな食べ物は。");
        yield return AskInput(v => _food = v, 12, false, "（ここに入力）");
        yield return Say(_drink + "と" + _food + "。控えました。");

        yield return Say("おいしいものを口にしたとき、あなたは何と言いますか。");
        yield return Say("思いつかなければ、空欄で構いません。");
        yield return AskInput(v => _tasty = v, 20, true, "（空欄でも可）");
        yield return Say("では、満腹で幸せなときは。");
        yield return AskInput(v => _full = v, 20, true, "（空欄でも可）");

        int guestNo = 0;
        var sdm = SaveDataManager.Instance;
        if (sdm != null)
        {
            sdm.SetPlayerMenuName(MenuCategory.Drink, _drink);
            sdm.SetPlayerMenuName(MenuCategory.Food, _food);
            guestNo = sdm.CompleteOpening(_name, _hot, _tasty, _full);
        }

        yield return Say("登録が完了しました。");
        yield return Say("あなたはこの店の、" + guestNo + "番目のお客様です。");
        yield return Say("それでは——ごゆっくり。");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Cafe");
    }

    // ── プリミティブ ──────────────────────────────────────

    /// <summary>セリフをタイプライター表示し、クリック送りを待つ</summary>
    private IEnumerator Say(string text)
    {
        _advanceClicked = false;
        if (_advanceGO != null) _advanceGO.SetActive(false);
        _typing = true;
        _body.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            _body.text = text.Substring(0, i + 1);
            float tEnd = Time.time + charInterval;
            bool skip = false;
            while (Time.time < tEnd && !skip)
            {
                if (_advanceClicked) skip = true; // クリック → 全文即表示
                else yield return null;
            }
            if (skip) { _body.text = text; break; }
        }
        _typing = false;
        _advanceClicked = false;
        if (_advanceGO != null) { _advanceGO.SetActive(true); _blinkT = 0f; }
        while (!_advanceClicked) yield return null; // 送り待ち
        _advanceClicked = false;
        if (_advanceGO != null) _advanceGO.SetActive(false);
    }

    /// <summary>入力欄を出して確定を待つ（allowEmpty=falseなら空は確定不可）</summary>
    private IEnumerator AskInput(Action<string> onDone, int maxLen, bool allowEmpty, string placeholder)
    {
        _okClicked = false;
        _input.characterLimit = maxLen;
        _input.text = "";
        var ph = _input.placeholder as TMPro.TMP_Text;
        if (ph != null) ph.text = placeholder;
        _inputGO.SetActive(true);
        _okGO.SetActive(true);
        _input.ActivateInputField();
        bool done = false;
        while (!done)
        {
            if (_okClicked)
            {
                _okClicked = false;
                string v = (_input.text ?? "").Trim();
                if (v.Length > 0 || allowEmpty) { onDone(v); done = true; }
                else _input.ActivateInputField(); // 空は受け付けない（必須項目）
            }
            if (!done) yield return null;
        }
        _inputGO.SetActive(false);
        _okGO.SetActive(false);
    }

    /// <summary>2択ボタンを出して選択を待つ（isA=trueでA側）</summary>
    private IEnumerator AskChoice(string labelA, string labelB, Action<bool> onDone)
    {
        _choiceClicked = 0;
        if (_choiceALabel != null) _choiceALabel.text = labelA;
        if (_choiceBLabel != null) _choiceBLabel.text = labelB;
        _choiceAGO.SetActive(true);
        _choiceBGO.SetActive(true);
        while (_choiceClicked == 0) yield return null;
        onDone(_choiceClicked == 1);
        _choiceClicked = 0;
        _choiceAGO.SetActive(false);
        _choiceBGO.SetActive(false);
    }

    /// <summary>カルーセルのプレビュー6体を生成（ナギの隣・カメラ向き・全て非表示）</summary>
    private void SetupAvatarPreviews()
    {
        if (catalog == null || catalog.entries == null) return;
        _avatarPreviews = new GameObject[catalog.entries.Length];
        for (int i = 0; i < catalog.entries.Length; i++)
        {
            var e = catalog.entries[i];
            if (e == null || e.prefab == null) continue;
            var go = Instantiate(e.prefab, new Vector3(-1.35f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
            go.name = "AvatarPreview_" + e.prefab.name;
            go.SetActive(false);
            _avatarPreviews[i] = go;
        }
    }

    private void ShowAvatarPreview(int index)
    {
        if (_avatarPreviews == null) return;
        for (int i = 0; i < _avatarPreviews.Length; i++)
            if (_avatarPreviews[i] != null) _avatarPreviews[i].SetActive(i == index);
    }

    /// <summary>見た目選択（◀▶で切替・決定で確定）。Fortnite/Diablo式カルーセル。矢印キー対応。</summary>
    private IEnumerator AskAvatar()
    {
        if (catalog == null || catalog.entries == null || catalog.entries.Length == 0) yield break;
        _choiceClicked = 0;
        _okClicked = false;
        if (_choiceALabel != null) _choiceALabel.text = "◀";
        if (_choiceBLabel != null) _choiceBLabel.text = "▶";
        _choiceAGO.SetActive(true);
        _choiceBGO.SetActive(true);
        _okGO.SetActive(true);
        _avatarIndex = Mathf.Clamp(_avatarIndex, 0, catalog.entries.Length - 1);
        ShowAvatarPreview(_avatarIndex);
        bool done = false;
        while (!done)
        {
            int move = 0;
            if (_choiceClicked == 1) move = -1;
            else if (_choiceClicked == 2) move = +1;
            _choiceClicked = 0;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame) move = -1;
                if (kb.rightArrowKey.wasPressedThisFrame) move = +1;
            }
            if (move != 0)
            {
                int n = catalog.entries.Length;
                _avatarIndex = (_avatarIndex + move + n) % n;
                ShowAvatarPreview(_avatarIndex);
            }
            if (_okClicked)
            {
                _okClicked = false;
                var e = catalog.entries[_avatarIndex];
                _avatarLabel = e != null ? e.label : "";
                if (SaveDataManager.Instance != null && e != null && e.prefab != null)
                    SaveDataManager.Instance.SetAvatar(e.prefab.name);
                done = true;
            }
            if (!done) yield return null;
        }
        _choiceAGO.SetActive(false);
        _choiceBGO.SetActive(false);
        _okGO.SetActive(false);
        // 選んだ姿はナギの隣に立たせたまま面接を続ける（あなたがそこに居る）
    }

    private void HideInputs()
    {
        if (_inputGO != null) _inputGO.SetActive(false);
        if (_okGO != null) _okGO.SetActive(false);
        if (_choiceAGO != null) _choiceAGO.SetActive(false);
        if (_choiceBGO != null) _choiceBGO.SetActive(false);
    }

    private void OnAdvanceClick()
    {
        // 入力・選択の表示中はクリック送りを無効化（誤送り防止）
        if ((_inputGO != null && _inputGO.activeSelf) || (_choiceAGO != null && _choiceAGO.activeSelf)) return;
        _advanceClicked = true;
    }

    // ── 結線 ─────────────────────────────────────────────

    private Transform FindDeep(Transform node, string goName)
    {
        if (node.name == goName) return node;
        for (int i = 0; i < node.childCount; i++)
        {
            var r = FindDeep(node.GetChild(i), goName);
            if (r != null) return r;
        }
        return null;
    }

    private void Bind()
    {
        var canvas = GameObject.Find("OpeningCanvas");
        if (canvas == null) { Debug.LogError("OpeningFlow: OpeningCanvas not found"); return; }
        var t = canvas.transform;
        var bodyT = FindDeep(t, "BodyLabel");
        _body = bodyT != null ? bodyT.GetComponent<TMPro.TMP_Text>() : null;
        var adv = FindDeep(t, "AdvanceCursor");
        _advanceGO = adv != null ? adv.gameObject : null;
        _advanceTmp = _advanceGO != null ? _advanceGO.GetComponent<TMPro.TMP_Text>() : null;
        var inp = FindDeep(t, "NameInput");
        _inputGO = inp != null ? inp.gameObject : null;
        _input = _inputGO != null ? _inputGO.GetComponent<TMPro.TMP_InputField>() : null;
        var ok = FindDeep(t, "OKButton");
        _okGO = ok != null ? ok.gameObject : null;
        _ok = _okGO != null ? _okGO.GetComponent<Button>() : null;
        var ca = FindDeep(t, "ChoiceAButton");
        _choiceAGO = ca != null ? ca.gameObject : null;
        _choiceA = _choiceAGO != null ? _choiceAGO.GetComponent<Button>() : null;
        var caL = _choiceAGO != null ? FindDeep(_choiceAGO.transform, "Label") : null;
        _choiceALabel = caL != null ? caL.GetComponent<TMPro.TMP_Text>() : null;
        var cb = FindDeep(t, "ChoiceBButton");
        _choiceBGO = cb != null ? cb.gameObject : null;
        _choiceB = _choiceBGO != null ? _choiceBGO.GetComponent<Button>() : null;
        var cbL = _choiceBGO != null ? FindDeep(_choiceBGO.transform, "Label") : null;
        _choiceBLabel = cbL != null ? cbL.GetComponent<TMPro.TMP_Text>() : null;

        var catcher = FindDeep(t, "ClickCatcher");
        var catchBtn = catcher != null ? catcher.GetComponent<Button>() : null;
        if (catchBtn != null) catchBtn.onClick.AddListener(OnAdvanceClick);
        if (_ok != null) _ok.onClick.AddListener(() => _okClicked = true);
        if (_choiceA != null) _choiceA.onClick.AddListener(() => _choiceClicked = 1);
        if (_choiceB != null) _choiceB.onClick.AddListener(() => _choiceClicked = 2);
        if (_input != null) _input.onSubmit.AddListener(_ => _okClicked = true); // Enterでも確定
    }
}
