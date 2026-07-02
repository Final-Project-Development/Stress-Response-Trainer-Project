using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays panel voice-over one speech unit at a time with a short pause between units.
/// </summary>
public class PanelNarrationSequencePlayer
{
    readonly MonoBehaviour _host;
    readonly AudioSource _audioSource;
    readonly float _pauseBetweenUnitsSeconds;

    Coroutine _routine;

    public bool IsPlaying => _routine != null;

    public PanelNarrationSequencePlayer(
        MonoBehaviour host,
        AudioSource audioSource,
        float pauseBetweenUnitsSeconds)
    {
        _host = host;
        _audioSource = audioSource;
        _pauseBetweenUnitsSeconds = Mathf.Max(0f, pauseBetweenUnitsSeconds);
    }

    public void Play(
        AudioClip legacyClip,
        AudioClip[] sentenceClips,
        string sourceText,
        TextMeshProUGUI subtitleTarget,
        string[] explicitSubtitleLines)
    {
        Stop();

        AudioClip[] clips = ResolveClips(legacyClip, sentenceClips);
        if (clips == null || clips.Length == 0 || _audioSource == null)
            return;

        if (clips.Length == 1)
        {
            ApplySubtitle(subtitleTarget, explicitSubtitleLines, sourceText, 0);
            PlayClip(clips[0]);
            return;
        }

        string[] subtitleLines = BuildSubtitleLines(sourceText, explicitSubtitleLines, clips.Length);
        _routine = _host.StartCoroutine(PlaySequenceRoutine(clips, subtitleLines, subtitleTarget));
    }

    public void Stop()
    {
        if (_routine != null)
        {
            _host.StopCoroutine(_routine);
            _routine = null;
        }

        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }

    static AudioClip[] ResolveClips(AudioClip legacyClip, AudioClip[] sentenceClips)
    {
        if (sentenceClips != null && sentenceClips.Length > 0)
        {
            int valid = 0;
            for (int i = 0; i < sentenceClips.Length; i++)
            {
                if (sentenceClips[i] != null)
                    valid++;
            }

            if (valid > 0)
                return sentenceClips;
        }

        return legacyClip != null ? new[] { legacyClip } : Array.Empty<AudioClip>();
    }

    static string[] BuildSubtitleLines(string sourceText, string[] explicitSubtitleLines, int clipCount)
    {
        if (explicitSubtitleLines != null && explicitSubtitleLines.Length > 0)
            return explicitSubtitleLines;

        var units = PanelNarrationTextUtil.SplitIntoSpeechUnits(sourceText);
        if (units.Count == 0)
            return Array.Empty<string>();

        if (units.Count == clipCount)
        {
            var lines = new string[units.Count];
            for (int i = 0; i < units.Count; i++)
                lines[i] = units[i];
            return lines;
        }

        if (units.Count > clipCount)
        {
            var trimmed = new string[clipCount];
            for (int i = 0; i < clipCount; i++)
                trimmed[i] = units[i];
            return trimmed;
        }

        var fallback = new string[units.Count];
        for (int i = 0; i < units.Count; i++)
            fallback[i] = units[i];
        return fallback;
    }

    IEnumerator PlaySequenceRoutine(AudioClip[] clips, string[] subtitleLines, TextMeshProUGUI subtitleTarget)
    {
        int played = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;

            ApplySubtitle(subtitleTarget, subtitleLines, null, i);
            PlayClip(clip);

            while (_audioSource != null && _audioSource.isPlaying)
                yield return null;

            played++;
            if (played < CountValidClips(clips) && _pauseBetweenUnitsSeconds > 0f)
                yield return new WaitForSeconds(_pauseBetweenUnitsSeconds);
        }

        _routine = null;
    }

    static int CountValidClips(AudioClip[] clips)
    {
        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                count++;
        }

        return count;
    }

    static void ApplySubtitle(
        TextMeshProUGUI subtitleTarget,
        string[] subtitleLines,
        string fallbackSourceText,
        int index)
    {
        if (subtitleTarget == null)
            return;

        if (subtitleLines != null && index >= 0 && index < subtitleLines.Length &&
            !string.IsNullOrWhiteSpace(subtitleLines[index]))
        {
            subtitleTarget.text = subtitleLines[index].Trim();
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackSourceText) && index == 0)
            subtitleTarget.text = fallbackSourceText.Trim();
    }

    void PlayClip(AudioClip clip)
    {
        _audioSource.loop = false;
        _audioSource.clip = clip;
        _audioSource.Play();
    }
}
