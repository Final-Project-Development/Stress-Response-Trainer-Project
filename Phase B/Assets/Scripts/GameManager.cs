using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public int itemToCollect = 3;
    public TextMeshProUGUI objectiveText;
    public TextMeshProUGUI pickupFeedbackText;
    public float pickupFeedbackDuration = 1.4f;

    [Header("Shelter target (Simulation 1)")]
    [Tooltip("New shelter root object name in the scene.")]
    public string mamadObjectName = "mamad";
    [Tooltip("Legacy shelter object name to disable during play so only mamad is used.")]
    public string legacyOutdoorShelterObjectName = "OutdoorShelter";
    public bool disableLegacyOutdoorShelter = true;

    [Header("Voice (optional)")]
    [Tooltip("e.g. same Narration Audio Source as TrainingFlowController, or a dedicated UI voice source.")]
    public AudioSource voiceAudioSource;
    [Tooltip("Played once when all items are collected — \"run to the Mamad\" instruction.")]
    public AudioClip allItemsCollectedRunToMamadClip;

    /// <summary>Raised once when both goals are complete: collected all items + reached shelter.</summary>
    public event Action OnAllItemsCollected;
    /// <summary>Raised when item collection goal is complete and player should go to shelter.</summary>
    public event Action OnItemsCollectionComplete;

    /// <summary>Raised when <see cref="OnFirstAidFinished"/> completes the first-aid interaction.</summary>
    public event Action OnFirstAidComplete;

    private int itemCollected = 0;
    private bool firstAidDone = false;
    private bool _itemsCollectionComplete;
    private bool _shelterReached;
    private bool _allItemsCollectedRaised;
    private Coroutine _pickupFeedbackRoutine;

    void Start()
    {
        ConfigureShelterTargets();
        _allItemsCollectedRaised = false;
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        UpdateObjectiveText();
    }

    private void ConfigureShelterTargets()
    {
        var mamad = FindSceneObjectByName(mamadObjectName);
        if (mamad == null)
        {
            Debug.LogWarning($"GameManager: Could not find mamad object '{mamadObjectName}'. Shelter objective trigger may not fire.");
            return;
        }

        if (mamad.GetComponent<ShelterTrigger>() == null)
        {
            mamad.AddComponent<ShelterTrigger>();
            Debug.Log($"GameManager: Added ShelterTrigger to '{mamadObjectName}'.");
        }

        if (!disableLegacyOutdoorShelter)
            return;

        var legacy = FindSceneObjectByName(legacyOutdoorShelterObjectName);
        if (legacy != null && legacy != mamad)
        {
            legacy.SetActive(false);
            Debug.Log($"GameManager: Disabled legacy shelter '{legacyOutdoorShelterObjectName}'.");
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t != null && t.name == objectName)
                return t.gameObject;
        }

        return null;
    }

    public void AddItem()
    {
        AddItem("Item");
    }

    public void AddItem(string itemName)
    {
        if (_itemsCollectionComplete)
            return;

        itemCollected++;

        ShowPickupFeedback(itemName);
        UpdateObjectiveText();

        if (itemCollected >= itemToCollect)
        {
            if (objectiveText != null)
            {
                objectiveText.text = "All three items collected. Now run to the Mamad (shelter) outside!";
            }
            else
            {
                ShowFeedbackMessage("All three items collected. Now run to the Mamad (shelter) outside!", 7f);
            }

            _itemsCollectionComplete = true;
            PlayAllItemsCollectedVoice();
            OnItemsCollectionComplete?.Invoke();
        }

        TryCompleteSimulation1Goals();
    }

    private void ShowPickupFeedback(string itemName)
    {
        if (pickupFeedbackText == null)
            return;

        if (_pickupFeedbackRoutine != null)
            StopCoroutine(_pickupFeedbackRoutine);

        pickupFeedbackText.text = $"{itemName} collected.";
        _pickupFeedbackRoutine = StartCoroutine(HidePickupFeedbackAfterDelay());
    }

    private void ShowFeedbackMessage(string message, float durationSeconds)
    {
        if (pickupFeedbackText == null)
            return;

        if (_pickupFeedbackRoutine != null)
            StopCoroutine(_pickupFeedbackRoutine);

        pickupFeedbackText.text = message;
        _pickupFeedbackRoutine = StartCoroutine(HideFeedbackAfterDelay(durationSeconds));
    }

    private IEnumerator HidePickupFeedbackAfterDelay()
    {
        pickupFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(pickupFeedbackDuration);
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        _pickupFeedbackRoutine = null;
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        pickupFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(delay);
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        _pickupFeedbackRoutine = null;
    }

    public void ShowMissionMessage(string message, float durationSeconds = 2.5f)
    {
        if (objectiveText != null)
        {
            objectiveText.text = message;
            return;
        }

        ShowFeedbackMessage(message, durationSeconds);
    }

    public void OnFirstAidFinished()
    {
        firstAidDone = true;
        if (objectiveText != null)
        {
            objectiveText.text = "First aid complete. Simulation 2 mission finished.";
        }
        else
        {
            ShowFeedbackMessage("First aid complete. Simulation 2 mission finished.", 3f);
        }
        OnFirstAidComplete?.Invoke();
        Debug.Log("First aid Completed");
    }

    public void UpdateObjectiveText()
    {
        if (objectiveText != null)
        {
            if (!_itemsCollectionComplete)
            {
                objectiveText.text =
                    $"Collect supplies: {itemCollected}/{itemToCollect} — water, first aid kit, emergency bag.";
            }
            else if (!_shelterReached)
            {
                objectiveText.text = "Objective: Go to the shelter outside (Mamad).";
            }
            else
            {
                objectiveText.text = "Shelter reached. Mission complete.";
            }
        }
    }

    /// <returns>True if the shelter objective was completed now; false if prerequisites are not met yet.</returns>
    public bool ReachShelter()
    {
        if (_shelterReached) return false;
        if (!_itemsCollectionComplete) return false;

        _shelterReached = true;
        UpdateObjectiveText();
        TryCompleteSimulation1Goals();
        return true;
    }

    private void TryCompleteSimulation1Goals()
    {
        if (_allItemsCollectedRaised) return;
        if (!_itemsCollectionComplete) return;
        if (!_shelterReached) return;

        _allItemsCollectedRaised = true;
        OnAllItemsCollected?.Invoke();
    }

    private void PlayAllItemsCollectedVoice()
    {
        if (voiceAudioSource == null || allItemsCollectedRunToMamadClip == null) return;
        voiceAudioSource.loop = false;
        voiceAudioSource.clip = allItemsCollectedRunToMamadClip;
        voiceAudioSource.Play();
    }
}
