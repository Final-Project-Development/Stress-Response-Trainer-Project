using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Add to each button in your manually designed tour sidebar.
/// Assign the scene object name; the tour guide wires the click at runtime.
/// </summary>
[RequireComponent(typeof(Button))]
public class EnvironmentLearningTourNavButton : MonoBehaviour
{
    [Tooltip("Hierarchy object name to visit, e.g. Map, PhoneBox, PFB_DoorDouble.")]
    public string sceneObjectName;

    [Tooltip("Meters from the item focus point. 0 = use EnvironmentLearningTourGuide default.")]
    public float standoffMeters;
}
