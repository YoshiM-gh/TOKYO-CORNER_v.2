using System;
using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Entries;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.Extensions;
using CharacterCustomizationTool.Editor.Randomizer;
using CharacterCustomizationTool.Editor.SlotValidation;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterCustomizationTool.Editor.Character
{
    public class CustomizableCharacter
    {
        private const BodyType DefaultBodyType = BodyType.Adult;
        private const Gender DefaultGender = Gender.Male;
        private const int HistorySize = 10;

        private readonly List<BodyTypeEntry> _bodyTypeEntries;
        private readonly LinkedList<CharacterState> _stateHistory = new();
        private readonly SlotValidator _slotValidator = new();

        private CharacterState _state;
        private FullBodySlot _fullBodySlot;

        public Slot[] Slots { get; private set; }
        public BodyType BodyType => _state.BodyType;
        public Gender Gender => _state.Gender;
        public bool HasHistory => _stateHistory.Count > 1;

        public CustomizableCharacter(List<BodyTypeEntry> bodyTypeEntries)
        {
            _bodyTypeEntries = bodyTypeEntries;

            CreateSlots(DefaultBodyType, DefaultGender);
        }

        public GameObject InstantiateCharacter()
        {
            var baseMesh = _bodyTypeEntries.First(e => e.BodyType.Equals(_state.BodyType)).BaseMesh;
            var prefabBase = Object.Instantiate(baseMesh, Vector3.zero, Quaternion.identity);

            return prefabBase;
        }

        public void SetState(CharacterState state)
        {
            _state = state;
            CreateSlots();
        }

        public IEnumerable<BodyType> GetAvailableBodyTypes()
        {
            return _bodyTypeEntries.Select(b => b.BodyType);
        }

        public IEnumerable<Gender> GetAvailableGenders(BodyType bodyType)
        {
            return _bodyTypeEntries.FirstOrDefault(e => e.BodyType.Equals(bodyType))?.Genders.Select(g => g.Gender);
        }

        public string GetName(SlotType slotType)
        {
            return GetSlot(slotType).Name;
        }

        public string GetFullBodyName()
        {
            return FullBodySlot.Name;
        }

        public int GetSelectedVariantIndex(SlotType slotType)
        {
            if (_state.TryGetSlotState(slotType, out var slotState))
            {
                var selectedVariantIndex = GetSlot(slotType).VariantIndexBy(slotState);

                return selectedVariantIndex;
            }

            return 0;
        }

        public int GetVariantsCount(SlotType slotType)
        {
            return Slots.First(s => s.Type.Equals(slotType)).VariantsCount;
        }

        public bool TryGetVariantByName(SlotType slotType, string key, out int index)
        {
            return Slots.First(s => s.Type.Equals(slotType)).TryGetVariantByName(key, out index);
        }

        public GameObject GetPreview(SlotType slotType)
        {
            if (_state.TryGetSlotState(slotType, out var slotState))
            {
                return GetSlot(slotType).GetVariant(slotState);
            }

            return null;
        }

        public bool IsEnabled(SlotType slotType)
        {
            return _state.TryGetSlotState(slotType, out var slotState) && slotState.IsEnabled;
        }

        public void SetEnabled(SlotType slotType, bool isToggled)
        {
            if (_state.TryGetSlotState(slotType, out var slotState))
            {
                var allowedGroups = new[] { GroupType.HatHairstyle, GroupType.HatBeard, GroupType.HatMustache };

                Update(slotState.Toggle(isToggled));

                var shouldAllowFullBody = false;
                if (_state.TryGetSlotState(slotType, out var newSlotState))
                {
                    shouldAllowFullBody = newSlotState.IsEnabled && allowedGroups.Contains(newSlotState.GroupType);
                }

                if (!AlwaysOnRule.IsAlwaysOn(slotType) && isToggled && !shouldAllowFullBody)
                {
                    UpdateFullBody(_state.FullBodyState.Toggle(false));
                }

                var validatedSlotStates = _slotValidator.Validate(slotState);
                foreach (var validatedSlotState in validatedSlotStates)
                {
                    Update(validatedSlotState);
                }
            }
        }

        public void SelectPrevious(SlotType slotType)
        {
            if (_state.TryGetSlotState(slotType, out var slotState))
            {
                var newSlotState = GetSlot(slotType).GetPrevious(slotState);

                Update(newSlotState);
            }
        }

        public void SelectNext(SlotType slotType)
        {
            if (_state.TryGetSlotState(slotType, out var slotState))
            {
                var newSlotState = GetSlot(slotType).GetNext(slotState);

                Update(newSlotState);
            }
        }

        public IEnumerable<(Mesh mesh, Material[] materials)> GetMeshRenderers()
        {
            var activeSlots = _state.Slots.Where(s => s.IsEnabled);

            foreach (var slot in activeSlots)
            {
                yield return GetSlot(slot.SlotType).GetMesh(slot);
            }

            if (_state.FullBodyState.IsEnabled)
            {
                foreach (var fullBodyElement in _fullBodySlot.GetMeshes(_state.FullBodyState))
                {
                    yield return (fullBodyElement.Mesh, fullBodyElement.Materials);
                }
            }
        }

        public void Draw(int previewLayer, Camera camera)
        {
            foreach (var slotState in _state.Slots.Where(s => s.IsEnabled))
            {
                var variantMesh = GetSlot(slotState.SlotType).GetMesh(slotState);
                DrawMesh(variantMesh.mesh, variantMesh.materials);
            }

            if (_state.FullBodyState.IsEnabled)
            {
                var elements = _fullBodySlot.GetMeshes(_state.FullBodyState);

                foreach (var element in elements)
                {
                    DrawMesh(element.Mesh, element.Materials);
                }
            }

            void DrawMesh(Mesh mesh, Material[] materials)
            {
                for (var i = 0; i < materials.Length; i++)
                {
                    Graphics.DrawMesh(mesh, new Vector3(0, 0, 0), Quaternion.identity, materials[i], previewLayer, camera, i);
                }
            }
        }

        public void Randomize()
        {
            RandomCharacterGenerator.Randomize(this);
        }

        public void SaveCombination()
        {
            _stateHistory.AddLast(_state);

            while (_stateHistory.Count > HistorySize)
            {
                _stateHistory.RemoveFirst();
            }
        }

        public void LastCombination()
        {
            _stateHistory.RemoveLast();

            _state = _stateHistory.Last.Value;
            CreateSlots();

            _stateHistory.RemoveLast();
        }

        public int GetVariantsCountInGroup(CharacterState state, GroupType groupType)
        {
            return GetVariantsCountInGroup(state.BodyType, state.Gender, groupType);
        }

        public int GetVariantsCountInGroup(BodyType bodyType, Gender gender, GroupType groupType)
        {
            foreach (var slot in GetSlots(bodyType, gender))
            {
                var groupEntry = slot.Groups.FirstOrDefault(g => g.Type.Equals(groupType));
                if (groupEntry)
                {
                    return groupEntry.Count;
                }
            }

            return 0;
        }

        private IEnumerable<SlotEntry> GetSlots(BodyType bodyType, Gender gender)
        {
            return _bodyTypeEntries.First(e => e.BodyType.Equals(bodyType))
                .Genders.First(e => e.Gender.Equals(gender)).Slots;
        }

        private IEnumerable<FullBodyEntry> GetFullBody(BodyType bodyType, Gender gender)
        {
            return _bodyTypeEntries.First(e => e.BodyType.Equals(bodyType))
                .Genders.First(e => e.Gender.Equals(gender)).FullBodyEntries;
        }

        public int GetFullBodyVariantsCount(CharacterState characterState)
        {
            return GetFullBody(characterState.BodyType, characterState.Gender).Count();
        }

        public void SelectPreviousBodyType()
        {
            SelectBodyType(ArrayExtensions.PreviousIndex);
        }

        public void SelectNextBodyType()
        {
            SelectBodyType(ArrayExtensions.NextIndex);
        }

        public void SelectPreviousGender()
        {
            SelectGender(ArrayExtensions.PreviousIndex);
        }

        public void SelectNextGender()
        {
            SelectGender(ArrayExtensions.NextIndex);
        }

        private Slot GetSlot(SlotType slotType)
        {
            return Slots.FirstOrDefault(s => s.Type == slotType);
        }

        private void SelectBodyType(Func<BodyType[], int, int> indexCalculator)
        {
            var availableBodyTypes = ToolConfig.BodyTypeEntries.Select(e => e.BodyType).ToArray();
            var arrayIndex = Array.IndexOf(availableBodyTypes, _state.BodyType);

            var newBodyTypeIndex = indexCalculator(availableBodyTypes, arrayIndex);

            _state = _state.WithBodyType(availableBodyTypes[newBodyTypeIndex]);

            CreateSlots();
        }

        private void SelectGender(Func<Gender[], int, int> indexCalculator)
        {
            var selectedBodyType = ToolConfig.BodyTypeEntries.First(e => e.BodyType == _state.BodyType);
            var availableGenders = selectedBodyType.Genders.Select(g => g.Gender).ToArray();
            var arrayIndex = Array.IndexOf(availableGenders, _state.Gender);

            var newGenderIndex = indexCalculator(availableGenders, arrayIndex);

            _state = _state.WithGender(availableGenders[newGenderIndex]);

            CreateSlots();
        }

        private void CreateSlots()
        {
            var previousState = _state;
            CreateSlots(_state.BodyType, _state.Gender);

            foreach (var slot in previousState.Slots)
            {
                Update(slot);
            }

            UpdateFullBody(previousState.FullBodyState);
        }

        public IEnumerable<Slot> CreateSlotsFor(BodyType bodyType, Gender gender)
        {
            var bodyTypeEntry = _bodyTypeEntries.FirstOrDefault(e => e.BodyType.Equals(bodyType)) ?? _bodyTypeEntries.First(e => e.BodyType.Equals(DefaultBodyType));
            var genderEntry = bodyTypeEntry.Genders.FirstOrDefault(e => e.Gender.Equals(gender)) ?? bodyTypeEntry.Genders.First(e => e.Gender.Equals(DefaultGender));

            var slots = SlotSorter.Sort(genderEntry.Slots.Select(e => new Slot(e.Type, e.Groups.ToArray()))).ToArray();

            return slots;
        }

        private void CreateSlots(BodyType bodyType, Gender gender)
        {
            var bodyTypeEntry = _bodyTypeEntries.FirstOrDefault(e => e.BodyType.Equals(bodyType)) ?? _bodyTypeEntries.First(e => e.BodyType.Equals(DefaultBodyType));
            var genderEntry = bodyTypeEntry.Genders.FirstOrDefault(e => e.Gender.Equals(gender)) ?? bodyTypeEntry.Genders.First(e => e.Gender.Equals(DefaultGender));

            Slots = SlotSorter.Sort(genderEntry.Slots.Select(e => new Slot(e.Type, e.Groups.ToArray()))).ToArray();
            _fullBodySlot = new FullBodySlot(genderEntry.FullBodyEntries);

            _state = CharacterState.CreateDefault(bodyTypeEntry.BodyType, genderEntry.Gender, Slots);
        }

        private void Update(SlotState newSlotState)
        {
            var slot = GetSlot(newSlotState.SlotType);
            if (slot == null || !slot.HasVariant(newSlotState.GroupType, newSlotState.VariantIndex))
            {
                return;
            }

            _state = _state.Update(newSlotState);
        }

        private void UpdateFullBody(FullBodyState fullBodyState)
        {
            _state = _state.UpdateFullBody(fullBodyState.VariantIndex >= _fullBodySlot.VariantsCount
                ? new FullBodyState(fullBodyState.IsEnabled && _fullBodySlot.HasVariants, 0)
                : fullBodyState);
        }

        public GameObject GetFullBodyPreview()
        {
            return _fullBodySlot.GetPreview(_state.FullBodyState);
        }

        public bool IsFullBodyEnabled()
        {
            return _state.FullBodyState.IsEnabled;
        }

        public void SetFullBodyEnabled(bool isEnabled)
        {
            var fullBodyState = _state.FullBodyState.Toggle(isEnabled);
            UpdateFullBody(fullBodyState);

            FullBodyValidator.Validate(this, fullBodyState);
        }

        public void SelectPreviousFullBody()
        {
            var newFullBodyState = _fullBodySlot.GetPrevious(_state.FullBodyState);

            UpdateFullBody(newFullBodyState);
        }

        public void SelectNextFullBody()
        {
            var newFullBodyState = _fullBodySlot.GetNext(_state.FullBodyState);

            UpdateFullBody(newFullBodyState);
        }

        public int GetSelectedFullBodyIndex()
        {
            return _state.FullBodyState.VariantIndex;
        }

        public int GetFullBodyVariantsCount()
        {
            return _fullBodySlot.VariantsCount;
        }

        public bool IsFullBodyAvailable()
        {
            return _fullBodySlot.VariantsCount > 0;
        }

        public bool TryGetSlotState(SlotType slotType, out SlotState slotState)
        {
            return _state.TryGetSlotState(slotType, out slotState);
        }
    }
}