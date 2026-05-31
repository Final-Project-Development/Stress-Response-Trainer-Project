using UnityEngine;

/// <summary>
/// Pre-generated VoiceGPT (or other) narration clips for UI panels.
/// Generate clips via Window → VoiceGPT → Panel Narration Setup.
/// </summary>
[CreateAssetMenu(fileName = "PanelNarrationLibrary", menuName = "VR Stress/Panel Narration Library")]
public class PanelNarrationLibrary : ScriptableObject
{
    [Header("Panel voice-overs")]
    public AudioClip introClip;
    public AudioClip calibrationClip;
    public AudioClip sim1MissionBriefingClip;
    public AudioClip sim2BriefingClip;

    [Header("Optional in-mission prompts")]
    public AudioClip allItemsCollectedRunToMamadClip;
}
