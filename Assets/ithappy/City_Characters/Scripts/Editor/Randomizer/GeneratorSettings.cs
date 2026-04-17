using System.Collections.Generic;
using CharacterCustomizationTool.Editor.Enums;

namespace CharacterCustomizationTool.Editor.Randomizer
{
    public static class GeneratorSettings
    {
        public static List<BodyType> BodyTypes { get; private set; } = new();
        public static List<Gender> Genders { get; private set; } = new();
        public static bool StandardFace { get; set; }

        public static void ToDefault()
        {
            BodyTypes = new List<BodyType>
            {
                BodyType.Adult, BodyType.PlusSize, BodyType.Senior,
                BodyType.Teen, BodyType.Child, BodyType.Pumped,
            };

            Genders = new List<Gender> { Gender.Male, Gender.Female, };

            StandardFace = true;
        }

        public static bool IsToggled(BodyType bodyType)
        {
            return BodyTypes.Contains(bodyType);
        }

        public static bool IsToggled(Gender gender)
        {
            return Genders.Contains(gender);
        }

        public static void Toggle(BodyType bodyType, bool isToggled)
        {
            switch (isToggled)
            {
                case false when IsToggled(bodyType):
                    BodyTypes.Remove(bodyType);
                    return;
                case true when !IsToggled(bodyType):
                    BodyTypes.Add(bodyType);
                    break;
            }
        }

        public static void Toggle(Gender gender, bool isToggled)
        {
            switch (isToggled)
            {
                case false when IsToggled(gender):
                    Genders.Remove(gender);
                    return;
                case true when !IsToggled(gender):
                    Genders.Add(gender);
                    break;
            }
        }
    }
}