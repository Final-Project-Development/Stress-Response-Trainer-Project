using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Static copy, prefab paths, and scene object names for briefing item cards.</summary>
public static class MissionBriefingCatalog
{
    public enum Simulation
    {
        Simulation1,
        Simulation2
    }

    public readonly struct Entry
    {
        public readonly string DisplayName;
        public readonly string LocationHint;
        public readonly string SceneObjectName;
        public readonly string PrefabAssetPath;

        public Entry(string displayName, string locationHint, string sceneObjectName, string prefabAssetPath = null)
        {
            DisplayName = displayName;
            LocationHint = locationHint;
            SceneObjectName = sceneObjectName;
            PrefabAssetPath = prefabAssetPath;
        }
    }

    static readonly Dictionary<string, Entry> Sim1 = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
    {
        ["waterbottle"] = new Entry(
            "Water Bottle", "Kitchen", "waterbottle",
            "Assets/Survival Tools/Prefabs/waterbottle.prefab"),
        ["flashlight"] = new Entry(
            "Flashlight", "Bathroom", "Flashlight",
            "Assets/Supercyan/Prefabs/Survival/Base/Mobile/MobileFlashlight.prefab"),
        ["radio"] = new Entry(
            "Radio", "Bedroom", "Radio",
            "Assets/Supercyan/Prefabs/Survival/Base/Mobile/MobileRadio.prefab"),
        ["phone"] = new Entry(
            "Phone", "Living room, near the TV", "phone",
            "Assets/Supercyan/Prefabs/Survival/WithItemLogic/Mobile/MobileCompassWithItemLogic.prefab"),
        ["key"] = new Entry(
            "Key", "Living room, table by the sofa", "key",
            "Assets/Supercyan/Prefabs/Survival/WithItemLogic/Mobile/MobileMapWithItemLogic.prefab"),
        ["light"] = new Entry(
            "Light Switch", "Entrance, near the door", "PFB_Lightswitch (1)",
            "Assets/FurnishedCabin/Prefabs/PFB_Lightswitch.prefab"),
        ["door"] = new Entry(
            "Entrance Door", "Home entrance", "PFB_DoorDouble",
            "Assets/FurnishedCabin/Prefabs/PFB_DoorDouble.prefab"),
        ["mamad"] = new Entry(
            "Mamad Shelter", "Outside, near the home", "mamad", null),
    };

    static readonly Dictionary<string, Entry> Sim2 = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
    {
        ["firstaid"] = new Entry(
            "First Aid Kit", "Near the home entrance", "firstaid",
            "Assets/Survival Tools/Prefabs/firstaid.prefab"),
        ["wounded"] = new Entry(
            "Wounded Person", "In the city", "WoundedCharacter_TPose",
            "Assets/Characters/WoundedCharacter_TPose.fbx"),
        ["phone"] = new Entry(
            "Public Phone", "Near the shoe store", "PhoneBox",
            "Assets/danthaigames/DS UK Public Telephone/Prefabs/UK Phone Box Clean.prefab"),
        ["treatment"] = new Entry(
            "Return & Treat", "Back at the wounded person", "WoundedCharacter_TPose",
            "Assets/Characters/WoundedCharacter_TPose.fbx"),
        ["phonedoor"] = new Entry(
            "Phone Door", "On the public phone", "PhoneBox",
            "Assets/danthaigames/DS UK Public Telephone/Prefabs/UK Phone Box Clean.prefab"),
        ["coin"] = new Entry(
            "Coin Slot", "On the public phone", "PhoneBox",
            "Assets/danthaigames/DS UK Public Telephone/Prefabs/UK Phone Box Clean.prefab"),
        ["receiver"] = new Entry(
            "Phone Receiver", "On the public phone", "PhoneBox",
            "Assets/danthaigames/DS UK Public Telephone/Prefabs/UK Phone Clean.prefab"),
        ["publicphone"] = new Entry(
            "Public Phone", "Near the shoe store", "PhoneBox",
            "Assets/danthaigames/DS UK Public Telephone/Prefabs/UK Phone Box Clean.prefab"),
        ["reciever"] = new Entry(
            "Phone Receiver", "On the public phone", "PhoneBox",
            "Assets/danthaigames/DS UK Public Telephone/Prefabs/UK Phone Clean.prefab"),
    };

    static readonly Dictionary<string, string> ThumbnailFileByKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["waterbottle"] = "water",
            ["flashlight"] = "flashlight 1",
            ["radio"] = "radio 1",
            ["phone"] = "phone",
            ["key"] = "key 1",
            ["door"] = "door 1",
            ["light"] = "lightswitch",
            ["mamad"] = "mamad",
            ["firstaid"] = "first aid",
            ["wounded"] = "wounded",
            ["publicphone"] = "public phone",
            ["reciever"] = "reciver",
            ["receiver"] = "reciver",
        };

    public static bool TryGet(Simulation simulation, string itemKey, out Entry entry)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
        {
            entry = default;
            return false;
        }

        var map = simulation == Simulation.Simulation1 ? Sim1 : Sim2;
        return map.TryGetValue(itemKey.Trim(), out entry);
    }

    public static IEnumerable<KeyValuePair<string, Entry>> GetAll(Simulation simulation) =>
        simulation == Simulation.Simulation1 ? Sim1 : Sim2;

    public static string ThumbnailResourcePath(Simulation simulation, string itemKey)
    {
        var fileName = ThumbnailFileByKey.TryGetValue(itemKey.Trim(), out var mapped)
            ? mapped
            : itemKey.Trim().ToLowerInvariant();

        // Thumbnail PNGs are stored under Sim1 for both briefing panels.
        return $"Briefing/Thumbnails/Sim1/{fileName}";
    }

    public static Sprite LoadThumbnail(Simulation simulation, string itemKey)
    {
        var path = ThumbnailResourcePath(simulation, itemKey);
        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        var sprites = Resources.LoadAll<Sprite>(path);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }
}
