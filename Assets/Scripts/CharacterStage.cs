using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CharacterCard 内に 3D オーナー(283)を表示する小さなステージ。
/// 画面外(遠方)に専用カメラ＋キャラ＋点光源を生成し、RenderTexture 経由でカードの RawImage に描画する。
/// Phase B: idle 表示のみ。エモートは Phase C（コントローラ拡張＋タイマー）。
/// URP 前提：カメラ背景はカード色に合わせて不透明合成（透明アルファに依存しない）。点光源は遠方配置でシーンを汚さない。
/// </summary>
public class CharacterStage : MonoBehaviour
{
    [Header("必須参照（インスペクタで割当）")]
    [SerializeField] private GameObject characterPrefab;            // 283.prefab
    [SerializeField] private Avatar characterAvatar;               // Basic_Characters_Plus-sizeAvatar
    [SerializeField] private RuntimeAnimatorController controller;  // CharacterOwner.controller

    [Header("RenderTexture")]
    [SerializeField] private int rtWidth = 512;
    [SerializeField] private int rtHeight = 430;

    [Header("カメラ・キャラ調整")]
    [SerializeField] private float camDistance = 3.7f;   // キャラからの距離
    [SerializeField] private float camHeight = 1.0f;     // カメラ高さ
    [SerializeField] private float lookHeight = 1.0f;    // 注視点の高さ
    [SerializeField] private float fov = 30f;
    [SerializeField] private float charYaw = 0f;         // キャラの向き（背中向きなら 180）
    [SerializeField] private Color bgColor = new Color(0.173f, 0.200f, 0.227f, 1f); // カード背景色

    [Header("ライト")]
    [SerializeField] private float lightIntensity = 3f;
    [SerializeField] private float lightRange = 30f;

    [Header("エモート（約10分±ランダムで1回）")]
    [SerializeField] private float emoteMinSeconds = 480f;
    [SerializeField] private float emoteMaxSeconds = 720f;

    [Header("背景（朝の窓・観葉植物）")]
    [SerializeField] private Color skyColor = new Color(0.95f, 0.93f, 0.88f, 1f);
    [SerializeField] private Color sunColor = new Color(1.0f, 0.96f, 0.88f, 1f);
    [SerializeField] private Vector3 sunEuler = new Vector3(35f, -35f, 0f);
    [SerializeField] private float sunIntensity = 1.1f;
    [SerializeField] private BackdropItem[] backdropItems;
    [SerializeField] private bool buildFloor = true;
    [SerializeField] private bool buildWall = true;
    [SerializeField] private Color floorColor = new Color(0.55f, 0.42f, 0.30f, 1f);
    [SerializeField] private Color wallColor = new Color(0.82f, 0.74f, 0.62f, 1f);
    [SerializeField] private bool windowGlow = true;
    [SerializeField] private Color windowGlowColor = new Color(1.0f, 0.96f, 0.85f, 1f);
    [SerializeField] private Vector3 windowGlowPos = new Vector3(0.7f, 1.0f, -2.5f);
    [SerializeField] private Vector3 windowGlowScale = new Vector3(1.7f, 2.0f, 0.05f);

    [System.Serializable]
    public class BackdropItem
    {
        public GameObject prefab;
        public Vector3 position;
        public Vector3 euler;
        public Vector3 scale = Vector3.one;
    }

    private RenderTexture _rt;
    private RawImage _raw;
    private Camera _cam;
    private GameObject _char;
    private Vector3 _origin;
    private Animator _anim;
    private float _emoteTimer;
    private readonly string[] _emoteTriggers = { "Wave", "LookPhone", "LookAround", "Listen", "Dance", "TalkPhone", "TakePhoto" };

    void Start()
    {
        if (characterPrefab == null) { Debug.LogWarning("[CharacterStage] characterPrefab が未割当です"); return; }
        BuildRawImage();
        BuildStage();
    }

    void Update()
    {
        if (_cam == null) return;
        _cam.fieldOfView = fov;
        _cam.transform.position = _origin + new Vector3(0f, camHeight, camDistance);
        _cam.transform.LookAt(_origin + new Vector3(0f, lookHeight, 0f));
        if (_char != null) _char.transform.localRotation = Quaternion.Euler(0f, charYaw, 0f);

        if (_anim != null && _emoteTriggers.Length > 0)
        {
            _emoteTimer -= Time.deltaTime;
            if (_emoteTimer <= 0f)
            {
                PlayRandomEmote();
                _emoteTimer = Random.Range(emoteMinSeconds, emoteMaxSeconds);
            }
        }
    }

    void BuildRawImage()
    {
        var go = new GameObject("CharacterRender", typeof(RectTransform), typeof(RawImage));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;   // カード全面
        _raw = go.GetComponent<RawImage>();
        _raw.raycastTarget = false;
        rt.SetAsFirstSibling();   // 吹き出しより後ろ（背面）
    }

    void BuildStage()
    {
        int layer = LayerMask.NameToLayer("CharacterStage");
        var origin = new Vector3(1000f, 1000f, 1000f);
        _origin = origin;

        _rt = new RenderTexture(rtWidth, rtHeight, 16, RenderTextureFormat.ARGB32);
        _rt.Create();
        if (_raw != null) { _raw.texture = _rt; _raw.color = Color.white; }

        var root = new GameObject("CharacterStage_Runtime");
        root.transform.position = origin;

        _char = Instantiate(characterPrefab, origin, Quaternion.Euler(0f, charYaw, 0f), root.transform);
        _char.name = "OwnerCharacter";
        SetLayerRecursive(_char, layer);
        MergeToSingleSkeleton(_char);   // パーツ毎の別スケルトンを1つに束ねる（T字対策）
        var anim = _char.GetComponent<Animator>();
        if (anim == null) anim = _char.AddComponent<Animator>();
        anim.avatar = characterAvatar;
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _anim = anim;

        var lightGO = new GameObject("StageLight");
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.position = origin + new Vector3(0.6f, 2.0f, 2.2f);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = lightRange;
        light.intensity = lightIntensity;
        light.color = Color.white;

        // 朝の太陽光（指向性・ステージレイヤーのみ）
        var sunGO = new GameObject("StageSun");
        sunGO.transform.SetParent(root.transform, false);
        sunGO.transform.rotation = Quaternion.Euler(sunEuler);
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = sunColor;
        sun.intensity = sunIntensity;
        sun.cullingMask = 1 << layer;

        // 背景アイテム（窓・観葉植物など）
        if (backdropItems != null)
        {
            foreach (var item in backdropItems)
            {
                if (item == null || item.prefab == null) continue;
                var b = Instantiate(item.prefab, origin + item.position, Quaternion.Euler(item.euler), root.transform);
                b.transform.localScale = (item.scale == Vector3.zero) ? Vector3.one : item.scale;
                SetLayerRecursive(b, layer);
            }
        }

        // 床と壁（部屋化）
        if (buildFloor)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "StageFloor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.position = origin + new Vector3(0f, -0.05f, -1.5f);
            floor.transform.localScale = new Vector3(14f, 0.1f, 10f);
            StripCollider(floor);
            SetStageMaterial(floor, floorColor);
            SetLayerRecursive(floor, layer);
        }
        if (buildWall)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "StageWall";
            wall.transform.SetParent(root.transform, false);
            wall.transform.position = origin + new Vector3(0f, 3f, -3.2f);
            wall.transform.localScale = new Vector3(14f, 6f, 0.1f);
            StripCollider(wall);
            SetStageMaterial(wall, wallColor);
            SetLayerRecursive(wall, layer);
        }

        // 窓グロー（外から光が差し込む＝発光パネル）
        if (windowGlow)
        {
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glow.name = "WindowGlow";
            glow.transform.SetParent(root.transform, false);
            glow.transform.position = origin + windowGlowPos;
            glow.transform.localScale = (windowGlowScale == Vector3.zero) ? new Vector3(1.7f,2f,0.05f) : windowGlowScale;
            StripCollider(glow);
            SetUnlitMaterial(glow, windowGlowColor);
            SetLayerRecursive(glow, layer);
        }

        var camGO = new GameObject("StageCamera");
        camGO.transform.SetParent(root.transform, false);
        _cam = camGO.AddComponent<Camera>();
        _cam.cullingMask = 1 << layer;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = skyColor;
        _cam.orthographic = false;
        _cam.fieldOfView = fov;
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane = 50f;
        _cam.targetTexture = _rt;
        _cam.transform.position = origin + new Vector3(0f, camHeight, camDistance);
        _cam.transform.LookAt(origin + new Vector3(0f, lookHeight, 0f));

        var main = Camera.main;
        if (main != null) main.cullingMask &= ~(1 << layer);
        _emoteTimer = Random.Range(emoteMinSeconds, emoteMaxSeconds);
    }

    [ContextMenu("▶ エモートを1回再生(テスト)")]
    public void PlayRandomEmote()
    {
        if (_anim == null || _emoteTriggers.Length == 0) return;
        _anim.SetTrigger(_emoteTriggers[Random.Range(0, _emoteTriggers.Length)]);
    }

    static void SetStageMaterial(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        r.sharedMaterial = m;
    }

    static void SetUnlitMaterial(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        r.sharedMaterial = m;
    }

    static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    void MergeToSingleSkeleton(GameObject root)
    {
        var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs.Length == 0) return;
        Transform master = smrs[0].rootBone;
        if (master == null) return;
        var byName = new System.Collections.Generic.Dictionary<string, Transform>();
        foreach (var b in master.GetComponentsInChildren<Transform>(true))
            if (!byName.ContainsKey(b.name)) byName[b.name] = b;
        foreach (var smr in smrs)
        {
            var oldB = smr.bones;
            var newB = new Transform[oldB.Length];
            for (int i = 0; i < oldB.Length; i++)
                newB[i] = (oldB[i] != null && byName.TryGetValue(oldB[i].name, out var mb)) ? mb : oldB[i];
            smr.bones = newB;
            smr.rootBone = master;
        }
        var toRemove = new System.Collections.Generic.List<GameObject>();
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == null || !tr.name.StartsWith("Skeleton_")) continue;
            var r = tr.Find("Root");
            if (r != null && r != master) toRemove.Add(tr.gameObject);
        }
        foreach (var g in toRemove) Destroy(g);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    void OnDestroy()
    {
        if (_cam != null) _cam.targetTexture = null;
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
    }
}
