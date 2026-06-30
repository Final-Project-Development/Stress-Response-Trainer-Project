using System.Text;

/// <summary>
/// Rewrites desktop key prompts ("press E", "press 1") into Quest controller wording when VR is active.
/// Mapping mirrors <see cref="XRInputBridge"/>: trigger = E, A = 1, B = 0/2, grip = 3.
/// </summary>
public static class VrInputPrompts
{
    public static string Localize(string text)
    {
        if (string.IsNullOrEmpty(text) || !VrGameplayInput.ShouldUseVrControls)
            return text;

        var sb = new StringBuilder(text);

        // Interact key.
        sb.Replace("press E", "pull the trigger");
        sb.Replace("Press E", "Pull the trigger");

        // Phone dial sequences (do the full sequence before shorter fragments).
        sb.Replace("1, 0, 1", "A, B, A");
        sb.Replace("0, 1", "B, A");

        // Treatment sequences.
        sb.Replace("1, then 2, then 3", "A, then B, then grip");
        sb.Replace("1 -> 2 -> 3", "A -> B -> grip");
        sb.Replace("1, 2, 3", "A, B, grip");

        // Single keys.
        sb.Replace("press 1", "press A");
        sb.Replace("Press 1", "Press A");
        sb.Replace("press 0", "press B");
        sb.Replace("Press 0", "Press B");
        sb.Replace("press 2", "press B");
        sb.Replace("Press 2", "Press B");
        sb.Replace("press 3", "squeeze the grip");
        sb.Replace("Press 3", "Squeeze the grip");

        // Leftover phone remaining-digit hints.
        sb.Replace("Remaining: 1 ", "Remaining: A ");
        sb.Replace("number keys only", "A / B buttons");

        return sb.ToString();
    }
}
