using TMPro;
using UnityEngine;

/// <summary>Applies catalog text/icons to briefing item cards. Layout is manual in the Editor.</summary>
[DisallowMultipleComponent]
public class SimulationBriefingPanelController : MonoBehaviour
{
    public MissionBriefingCatalog.Simulation simulation = MissionBriefingCatalog.Simulation.Simulation1;

    [Tooltip("Hide legacy long-form briefing text when visual cards are present.")]
    public bool hideLegacyBodyText = true;

    public string[] legacyBodyObjectNames = { "Sim2BriefingBody" };

    void Awake() => Refresh();

    void OnEnable() => Refresh();

    public void Refresh()
    {
        EnsureItemCards();
        ApplyAllCards();
        HideLegacyBodyIfNeeded();
    }

    void EnsureItemCards()
    {
        foreach (var card in GetComponentsInChildren<MissionBriefingItemCard>(true))
            card.ConfigureSimulation(simulation);

        var grids = GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true);
        for (int g = 0; g < grids.Length; g++)
        {
            var grid = grids[g];
            for (int i = 0; i < grid.transform.childCount; i++)
            {
                var child = grid.transform.GetChild(i);
                if (child.GetComponent<MissionBriefingItemCard>() == null)
                    child.gameObject.AddComponent<MissionBriefingItemCard>();
            }
        }
    }

    void ApplyAllCards()
    {
        var cards = GetComponentsInChildren<MissionBriefingItemCard>(true);
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].ConfigureSimulation(simulation);
            cards[i].ApplyContent();
        }
    }

    void HideLegacyBodyIfNeeded()
    {
        if (!hideLegacyBodyText)
            return;

        for (int i = 0; i < legacyBodyObjectNames.Length; i++)
        {
            var legacy = FindChildRecursive(transform, legacyBodyObjectNames[i]);
            if (legacy == null)
                continue;

            var tmp = legacy.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.gameObject.SetActive(false);
        }
    }

    static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
