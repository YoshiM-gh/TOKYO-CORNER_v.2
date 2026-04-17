using System.IO;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.Randomizer;
using CharacterCustomizationTool.FaceManagement;
using Controller;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterCustomizationTool.Editor
{
    public class CharacterCustomizationWindow : EditorWindow
    {
        private const string PreviewLayerName = "Character Preview";
        private const int Width = 100;
        private const int SpaceHeight = 5;

        private CustomizableCharacter _customizableCharacter;
        private SlotsEditor _slotsEditor;
        private Camera _camera;
        private int _previewLayer;
        private RenderTexture _renderTexture;
        private string _prefabName;

        [MenuItem("Tools/Character Customization #%&e", priority = 0)]
        private static void Init()
        {
            ToolConfig.Reload();
            var window = GetWindow<CharacterCustomizationWindow>("Character Customization");
            window.Show();

            GeneratorSettings.ToDefault();
            window._customizableCharacter.Randomize();
            window._customizableCharacter.SaveCombination();
        }

        private void OnEnable()
        {
            _customizableCharacter = new CustomizableCharacter(ToolConfig.BodyTypeEntries);
            _slotsEditor = new SlotsEditor();

            LayerMaskUtility.CreateLayer(PreviewLayerName);
            _previewLayer = LayerMask.NameToLayer(PreviewLayerName);
        }

        private void OnGUI()
        {
            minSize = new Vector2(1050, 750);
            CreateRenderTexture();
            InitializeCamera();
            DrawCharacter();

            const int borderSize = 15;

            using (new GUILayout.AreaScope(new Rect(borderSize, borderSize, position.width - borderSize * 2, position.height - borderSize * 2), GUIContent.none, EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    DrawSetUpPanel();
                    _slotsEditor.DrawSlots(_customizableCharacter);
                }
            }
        }

        private void DrawSetUpPanel()
        {
            using (new GUILayout.VerticalScope(GUILayout.Width(300)))
            {
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var currentActiveRT = RenderTexture.active;
                    RenderTexture.active = _renderTexture;
                    var newTexture2D = new Texture2D(_renderTexture.width, _renderTexture.height, TextureFormat.RGBA32, false);
                    newTexture2D.ReadPixels(new Rect(0, 0, _renderTexture.width, _renderTexture.height), 0, 0);
                    newTexture2D.Apply();
                    RenderTexture.active = currentActiveRT;

                    var style = new GUIStyle();
                    style.normal.background = newTexture2D;

                    GUILayout.Box(GUIContent.none, style, GUILayout.Width(300), GUILayout.Height(300));
                    SlotsEditor.DrawBodyTypeAndGenderSelection(_customizableCharacter);
                }

                GUILayout.Space(SpaceHeight);

                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Prefab folder:");
                    GUILayout.Label(ToolConfig.SavedCharacters);

                    GUILayout.Label("Name:");
                    _prefabName = GUILayout.TextField(_prefabName);
                }

                GUILayout.Space(SpaceHeight);

                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            Draw(BodyType.Adult);
                            Draw(BodyType.PlusSize, "Plus-Size");
                            Draw(BodyType.Senior);
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            Draw(BodyType.Teen);
                            Draw(BodyType.Child);
                            DrawPumped();
                        }

                        GUILayout.Space(SpaceHeight);

                        Draw(Gender.Male);
                        DrawFemale();

                        GUILayout.Space(SpaceHeight);

                        GeneratorSettings.StandardFace = GUILayout.Toggle(GeneratorSettings.StandardFace, "Standard Face", GUILayout.Width(Width));

                        GUILayout.Space(SpaceHeight);

                        if (GUILayout.Button("To Default"))
                        {
                            GeneratorSettings.ToDefault();
                        }
                    }

                    GUILayout.Space(SpaceHeight);

                    if (GUILayout.Button("Save Prefab"))
                    {
                        SavePrefab();
                    }

                    using (new EditorGUI.DisabledScope(GeneratorSettings.BodyTypes.Count == 0 || GeneratorSettings.Genders.Count == 0))
                    {
                        if (GUILayout.Button("Randomize"))
                        {
                            _customizableCharacter.Randomize();
                            _customizableCharacter.SaveCombination();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!_customizableCharacter.HasHistory))
                    {
                        if (GUILayout.Button("Last"))
                        {
                            _customizableCharacter.LastCombination();
                            _customizableCharacter.SaveCombination();
                        }
                    }
                }
            }
        }

        private static void Draw(BodyType bodyType, string name = "")
        {
            var isToggled = GUILayout.Toggle(GeneratorSettings.IsToggled(bodyType), string.IsNullOrEmpty(name) ? bodyType.ToString() : name, GUILayout.Width(Width));
            GeneratorSettings.Toggle(bodyType, isToggled);
        }

        private static void Draw(Gender gender)
        {
            var isToggled = GUILayout.Toggle(GeneratorSettings.IsToggled(gender), gender.ToString(), GUILayout.Width(Width));
            GeneratorSettings.Toggle(gender, isToggled);
        }

        private static void DrawPumped()
        {
            if (!GeneratorSettings.IsToggled(Gender.Male))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Toggle(false, nameof(BodyType.Pumped), GUILayout.Width(Width));
                    GeneratorSettings.Toggle(BodyType.Pumped, false);
                }
            }
            else
            {
                Draw(BodyType.Pumped);
            }
        }

        private static void DrawFemale()
        {
            if (GeneratorSettings.BodyTypes.Count == 1 && GeneratorSettings.IsToggled(BodyType.Pumped))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Toggle(false, nameof(Gender.Female), GUILayout.Width(Width));
                    GeneratorSettings.Toggle(Gender.Female, false);
                }
            }
            else
            {
                Draw(Gender.Female);
            }
        }

        private void SavePrefab()
        {
            var character = _customizableCharacter.InstantiateCharacter();

            var availableSlots = character.GetComponentsInChildren<SkinnedMeshRenderer>().ToList();
            availableSlots.ForEach(s => s.sharedMesh = null);

            var meshes = _customizableCharacter.GetMeshRenderers();

            foreach (var meshInfo in meshes)
            {
                var availableSlot = availableSlots.First();
                availableSlots.Remove(availableSlot);

                availableSlot.sharedMesh = meshInfo.mesh;
                availableSlot.sharedMaterials = meshInfo.materials;
                availableSlot.localBounds = availableSlot.sharedMesh.bounds;
                availableSlot.name = meshInfo.mesh.name;
            }

            availableSlots.ForEach(s => DestroyImmediate(s.gameObject));

            AddAnimator(character);
            AddFacePicker(character, _customizableCharacter.Slots.First(s => s.Type == SlotType.Face).GetVariants());
            AddMovementComponents(character);

            const string defaultCharacterName = "Character";

            Directory.CreateDirectory(ToolConfig.SavedCharacters);
            _prefabName = string.IsNullOrEmpty(_prefabName) ? defaultCharacterName : _prefabName;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{ToolConfig.SavedCharacters}{_prefabName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(character, path);
            DestroyImmediate(character);

            if (_prefabName == defaultCharacterName)
            {
                _prefabName = string.Empty;
            }
        }

        private static void AddAnimator(GameObject character)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ToolConfig.AnimationController);
            if (!character.TryGetComponent<Animator>(out var animator))
            {
                animator = character.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        private static void AddFacePicker(GameObject character, Mesh[] variants)
        {
            var facePicker = character.AddComponent<FacePicker>();
            facePicker.SetFaces(variants);
        }

        private static void AddMovementComponents(GameObject character)
        {
            AddCharacterController(character);
            character.AddComponent<CharacterMover>();
            character.AddComponent<MovePlayerInput>();
        }

        private static void AddCharacterController(GameObject character)
        {
            var characterController = character.AddComponent<CharacterController>();

            var bounds = GetCombinedBounds(character);
            characterController.center = new Vector3(0, bounds.center.y, 0);
            characterController.height = bounds.extents.y * 2;
            characterController.radius = bounds.extents.z * 1.1f;
        }

        private void InitializeCamera()
        {
            if (_camera)
            {
                return;
            }

            var cameraPivot = new GameObject("CameraPivot").transform;
            cameraPivot.gameObject.hideFlags = HideFlags.HideAndDontSave;

            var cameraObject = new GameObject("PreviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _camera = cameraObject.AddComponent<Camera>();
            _camera.targetTexture = _renderTexture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.renderingPath = RenderingPath.Forward;
            _camera.enabled = false;
            _camera.useOcclusionCulling = false;
            _camera.cameraType = CameraType.Preview;
            _camera.fieldOfView = 3.4f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.transform.SetParent(cameraPivot);
            _camera.cullingMask = 1 << _previewLayer;

            cameraPivot.Rotate(Vector3.up, 150, Space.Self);
            cameraPivot.position += .1f * Vector3.down;
        }

        private void CreateRenderTexture()
        {
            if (_renderTexture)
            {
                return;
            }

            _renderTexture = new RenderTexture(400, 400, 30, RenderTextureFormat.ARGB32);
        }

        private void DrawCharacter()
        {
            _camera.transform.localPosition = new Vector3(0, 1.1f, -36);
            _customizableCharacter.Draw(_previewLayer, _camera);
            _camera.Render();
        }

        private static Bounds GetCombinedBounds(GameObject parentObject)
        {
            var renderers = parentObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(parentObject.transform.position, Vector3.zero);
            }

            var combinedBounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            return combinedBounds;
        }
    }
}