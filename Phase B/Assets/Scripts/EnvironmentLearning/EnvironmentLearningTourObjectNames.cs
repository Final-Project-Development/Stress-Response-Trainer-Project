using System.Collections.Generic;

/// <summary>Maps sidebar button names to scene object names for tour navigation.</summary>
public static class EnvironmentLearningTourObjectNames
{
    static readonly Dictionary<string, (string sceneObject, float standoff)> Map = BuildMap();

    static Dictionary<string, (string, float)> BuildMap()
    {
        var map = new Dictionary<string, (string, float)>();
        foreach (var entry in EnvironmentLearningTourCatalog.Items)
        {
            string key = Normalize(entry.DisplayName);
            if (!map.ContainsKey(key))
                map[key] = (entry.ObjectName, entry.StandoffMeters);

            string objectKey = Normalize(entry.ObjectName);
            if (!map.ContainsKey(objectKey))
                map[objectKey] = (entry.ObjectName, entry.StandoffMeters);
        }

        map[Normalize("Water bottle")] = ("waterbottle", 1.8f);
        map[Normalize("First aid")] = ("firstaid", 2f);
        map[Normalize("Light Switch")] = ("PFB_Lightswitch (1)", 1.2f);
        map[Normalize("Entrance Door")] = ("PFB_DoorDouble", 2.2f);
        return map;
    }

    public static bool TryResolve(string buttonName, out string sceneObjectName, out float standoffMeters)
    {
        sceneObjectName = null;
        standoffMeters = 0f;
        if (string.IsNullOrWhiteSpace(buttonName))
            return false;

        if (Map.TryGetValue(Normalize(buttonName), out var resolved))
        {
            sceneObjectName = resolved.sceneObject;
            standoffMeters = resolved.standoff;
            return true;
        }

        return false;
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
    }
}
