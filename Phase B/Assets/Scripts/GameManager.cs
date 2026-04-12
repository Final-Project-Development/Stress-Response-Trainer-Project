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

    private int itemCollected =0;
    private bool firstAidDone = false;
    private bool _itemsCollectionComplete;
    private bool _shelterReached;
    private bool _allItemsCollectedRaised;
    private Coroutine _pickupFeedbackRoutine;

    void Start()
    {
        _allItemsCollectedRaised = false;
        if (pickupFeedbackText != null)
            pickupFeedbackText.gameObject.SetActive(false);
        UpdateObjectiveText();
    }

    public void AddItem()
    {
        AddItem("Item");
    }

    public void AddItem(string itemName)
    {
        itemCollected++;
        ShowPickupFeedback(itemName);
        UpdateObjectiveText();

        if (itemCollected >= itemToCollect && !firstAidDone)
        {
            if (objectiveText != null)
            {
                objectiveText.text = "All three items collected. Now run to the Mamad (shelter) outside!";
            }
            else
            {
                ShowFeedbackMessage("All three items collected. Now run to the Mamad (shelter) outside!", 7f);
            }

            if (!_itemsCollectionComplete)
            {
                _itemsCollectionComplete = true;
                PlayAllItemsCollectedVoice();
                OnItemsCollectionComplete?.Invoke();
            }
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
        if (objectiveText != null )
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

    public void ReachShelter()
    {
        if (_shelterReached) return;
        _shelterReached = true;
        UpdateObjectiveText();
        TryCompleteSimulation1Goals();
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
