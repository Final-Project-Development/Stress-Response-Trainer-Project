using UnityEngine;

/// <summary>
/// Pre-generated VoiceGPT (or other) narration clips for UI panels.
/// Generate clips via Window → VoiceGPT → Panel Narration Setup.
/// </summary>
[CreateAssetMenu(fileName = "PanelNarrationLibrary", menuName = "VR Stress/Panel Narration Library")]
public class PanelNarrationLibrary : ScriptableObject
{
    [Header("Legacy single clips")]
    public AudioClip introClip;
    public AudioClip calibrationClip;
    public AudioClip learnBriefingClip;
    public AudioClip sim1MissionBriefingClip;
    public AudioClip sim2BriefingClip;

    [Header("Per-sentence clips (preferred)")]
    public AudioClip[] introSentenceClips;
    public AudioClip[] calibrationSentenceClips;
    public AudioClip[] learnBriefingSentenceClips;
    public AudioClip[] sim1MissionBriefingSentenceClips;
    public AudioClip[] sim2BriefingSentenceClips;

    [Header("Optional in-mission prompts")]
    public AudioClip allItemsCollectedRunToMamadClip;
}
