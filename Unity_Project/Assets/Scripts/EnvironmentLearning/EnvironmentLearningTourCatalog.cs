/// <summary>Shared list of important tour destinations for world labels setup.</summary>
public static class EnvironmentLearningTourCatalog
{
    public enum TourGroup
    {
        Simulation1,
        Simulation2,
        Shared
    }

    public readonly struct Entry
    {
        public readonly string ObjectName;
        public readonly string DisplayName;
        public readonly TourGroup Group;
        public readonly float StandoffMeters;
        public readonly bool RightToLeft;

        public Entry(string objectName, string displayName, TourGroup group, float standoffMeters = 2.5f, bool rightToLeft = false)
        {
            ObjectName = objectName;
            DisplayName = displayName;
            Group = group;
            StandoffMeters = standoffMeters;
            RightToLeft = rightToLeft;
        }
    }

    public static readonly Entry[] Items =
    {
        new Entry("Home", "Home", TourGroup.Simulation1, 3f),
        new Entry("waterbottle", "Water Bottle", TourGroup.Simulation1, 1.8f),
        new Entry("Flashlight", "Flashlight", TourGroup.Simulation1, 1.8f),
        new Entry("Radio", "Radio", TourGroup.Simulation1, 1.8f),
        new Entry("phone", "Phone", TourGroup.Simulation1, 1.8f),
        new Entry("key", "Key", TourGroup.Simulation1, 1.8f),
        new Entry("PFB_Lightswitch (1)", "Light Switch", TourGroup.Simulation1, 1.2f),
        new Entry("PFB_DoorDouble", "Entrance Door", TourGroup.Simulation1, 2.2f),
        new Entry("firstaid", "First Aid", TourGroup.Simulation2, 2f),
        new Entry("WoundedCharacter_TPose", "Wounded Character", TourGroup.Simulation2, 2.5f),
        new Entry("PhoneBox", "Public Phone", TourGroup.Simulation2, 2.8f),
        new Entry("mamad", "Mamad", TourGroup.Shared, 3f),
    };
}
