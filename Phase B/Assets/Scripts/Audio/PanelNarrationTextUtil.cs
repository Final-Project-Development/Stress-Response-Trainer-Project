using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Splits panel copy into short speech units for VoiceGPT and runtime subtitle sync.
/// </summary>
public static class PanelNarrationTextUtil
{
    public static List<string> SplitIntoSpeechUnits(string text)
    {
        var units = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return units;

        text = text.Replace("\r\n", "\n").Trim();
        var paragraphs = Regex.Split(text, @"\n\s*\n");
        for (int p = 0; p < paragraphs.Length; p++)
        {
            var paragraph = paragraphs[p].Trim();
            if (string.IsNullOrEmpty(paragraph))
                continue;

            var lines = paragraph.Split('\n');
            for (int l = 0; l < lines.Length; l++)
            {
                var line = lines[l].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.EndsWith("...") || line.EndsWith("…"))
                {
                    units.Add(line);
                    continue;
                }

                var sentences = Regex.Split(line, @"(?<=[.!?])\s+");
                for (int s = 0; s < sentences.Length; s++)
                {
                    var sentence = sentences[s].Trim();
                    if (!string.IsNullOrEmpty(sentence))
                        units.Add(sentence);
                }
            }
        }

        return units;
    }
}
