using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Opens the in-VR keyboard when VR users point and click login/register input fields.
/// </summary>
public sealed class VrLoginInputSupport : MonoBehaviour
{
    [SerializeField] private TMP_InputField[] inputFields;
    private bool listenersWired;

    private void Awake()
    {
        if (inputFields == null || inputFields.Length == 0)
            inputFields = GetComponentsInChildren<TMP_InputField>(true);
    }

    private void OnEnable()
    {
        if (listenersWired)
            return;

        for (int i = 0; i < inputFields.Length; i++)
        {
            TMP_InputField field = inputFields[i];
            if (field == null)
                continue;

            UnityAction<string> handler = _ => HandleFieldSelected(field);
            field.onSelect.AddListener(handler);
        }

        listenersWired = true;
    }

    private void HandleFieldSelected(TMP_InputField field)
    {
        if (!VrGameplayInput.ShouldUseVrControls || field == null)
            return;

        VrVirtualKeyboard.Show(field);
    }
}
