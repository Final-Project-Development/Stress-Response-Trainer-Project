using System;
using UnityEngine;

/// <summary>Runtime sprite lookup for briefing item icons (filled by editor from prefabs).</summary>
[CreateAssetMenu(fileName = "MissionBriefingIconLibrary", menuName = "Stress Trainer/Mission Briefing Icon Library")]
public class MissionBriefingIconLibrary : ScriptableObject
{
    [Serializable]
    public struct ItemIcon
    {
        public string itemKey;
        public Sprite sprite;
    }

    public ItemIcon[] simulation1Icons;
    public ItemIcon[] simulation2Icons;

    static MissionBriefingIconLibrary _cached;

    public static MissionBriefingIconLibrary Instance
    {
        get
        {
            if (_cached != null)
                return _cached;

            var all = Resources.FindObjectsOfTypeAll<MissionBriefingIconLibrary>();
            if (all.Length > 0)
                _cached = all[0];
            return _cached;
        }
    }

    public Sprite GetIcon(MissionBriefingCatalog.Simulation simulation, string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return null;

        var icons = simulation == MissionBriefingCatalog.Simulation.Simulation1
            ? simulation1Icons
            : simulation2Icons;

        if (icons == null)
            return null;

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i].sprite == null)
                continue;
            if (string.Equals(icons[i].itemKey, itemKey, StringComparison.OrdinalIgnoreCase))
                return icons[i].sprite;
        }

        return null;
    }
}
