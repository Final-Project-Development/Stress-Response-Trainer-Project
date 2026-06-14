using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds mission panel objective text: navigation when far, explicit Press E / key hints only when in range.
/// </summary>
public static class MissionProximityObjectiveResolver
{
    public static string Resolve(GameManager gameManager, TrainingFlowController flow, Camera cam)
    {
        if (gameManager == null || flow == null)
            return string.Empty;

        if (flow.CurrentPhase == TrainingFlowController.Phase.Simulation1Active)
            return ResolveSimulation1(gameManager, flow, cam);

        if (flow.CurrentPhase == TrainingFlowController.Phase.Simulation2Active)
            return ResolveSimulation2(gameManager, flow, cam);

        return string.Empty;
    }

    static string ResolveSimulation1(GameManager gameManager, TrainingFlowController flow, Camera cam)
    {
        switch (gameManager.GetSim1Phase())
        {
            case GameManager.Sim1MissionPhase.CollectItems:
                return ResolveSim1Collect(gameManager, flow, cam);
            case GameManager.Sim1MissionPhase.TurnOffLights:
                return ResolveSim1LightSwitch(gameManager, flow, cam);
            case GameManager.Sim1MissionPhase.CloseDoor:
                return ResolveSim1CloseDoor(gameManager, flow, cam);
            case GameManager.Sim1MissionPhase.RunToShelter:
                return ResolveSim1Shelter(gameManager, flow, cam);
            default:
                return string.Empty;
        }
    }

    static string ResolveSim1Collect(GameManager gameManager, TrainingFlowController flow, Camera cam)
    {
        var bootstrap = gameManager.GetMissionBootstrap();
        PickUpItem[] pickups = bootstrap != null
            ? bootstrap.GetRemainingSimulation1Pickups()
            : System.Array.Empty<PickUpItem>();

        if (pickups.Length == 0)
        {
            return flow != null
                ? flow.sim1ObjectiveTurnOffLightsApproach
                : "Turn off the lights using the light switch inside the home.";
        }

        float interactDistance = MissionInteractProximity.GetDefaultInteractDistance() + 1f;
        PickUpItem nearestInRange = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < pickups.Length; i++)
        {
            var pickup = pickups[i];
            if (pickup == null || !pickup.isActiveAndEnabled)
                continue;

            if (!MissionInteractProximity.CanPressEOn(cam, pickup, interactDistance))
                continue;

            float dist = Vector3.Distance(cam.transform.position, pickup.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestInRange = pickup;
            }
        }

        if (nearestInRange != null)
        {
            return flow != null
                ? flow.BuildSim1CollectActionObjective(nearestInRange.ItemDisplayName)
                : $"Press E to collect {nearestInRange.ItemDisplayName}.";
        }

        IReadOnlyList<string> remaining = bootstrap != null
            ? bootstrap.GetRemainingSim1PickupDisplayNames()
            : System.Array.Empty<string>();

        return flow != null
            ? flow.BuildSim1CollectApproachObjective(remaining, gameManager.GetSim1ItemsCollected(), gameManager.itemToCollect)
            : BuildFallbackSim1CollectApproach(remaining);
    }

    static string BuildFallbackSim1CollectApproach(IReadOnlyList<string> remaining)
    {
        if (remaining == null || remaining.Count == 0)
            return "Collect supplies inside the home.";

        return $"Collect supplies inside the home.\nRemaining: {string.Join(", ", remaining)}.";
    }

    static string ResolveSim1LightSwitch(GameManager gameManager, TrainingFlowController flow, Camera cam)
    {
        LightSwitch lightSwitch = ResolveLightSwitch(gameManager);
        float interactDistance = MissionInteractProximity.GetDefaultInteractDistance();

        if (lightSwitch != null
            && MissionInteractProximity.CanPressEOn(cam, lightSwitch, interactDistance))
        {
            return flow != null
                ? flow.sim1ObjectiveTurnOffLightsAction
                : "Press E on the light switch to turn off the lights.";
        }

        return flow != null
            ? flow.sim1ObjectiveTurnOffLightsApproach
            : "Turn off the lights using the light switch inside the home.";
    }

    static string ResolveSim1CloseDoor(GameManager gameManager, TrainingFlowController flow, Camera cam)
    {
        Door exitDoor = gameManager.GetMissionExitDoor();
        float interactDistance = MissionInteractProximity.GetDefaultInteractDistance();

        if (exitDoor != null
            && exitDoor.enabled
            && MissionInteractProximity.CanPressEOn(cam, exitDoor, interactDistance))
        {
            return flow != null
                ? flow.sim1ObjectiveCloseDoorAction
                : "Press E to close the entrance door.";
        }

        return flow != null
            ? flow.sim1ObjectiveCloseDoorApproach
            : "Close the entrance door before going to the Mamad shelter.";
    }

    static string ResolveSim1Shelter(GameManager gameManager, TrainingFlowController flow, Camera cam)
    {
        ShelterTrigger shelter = ResolveShelterTrigger(gameManager);
        if (shelter != null && shelter.IsPlayerNearCompletionZone())
        {
            return flow != null
                ? flow.sim1ObjectiveRunToShelterAction
                : "Enter the Mamad shelter.";
        }

        return flow != null
            ? flow.sim1ObjectiveRunToShelterApproach
            : "Run to the Mamad shelter outside.";
    }

    static string ResolveSimulation2(GameManager gameManager, TrainingFlowController flow, Camera cam)
    {
        if (!gameManager.HasFirstAidKit())
            return ResolveSim2FirstAidKit(flow, cam);

        if (!gameManager.HasContactedCasualty())
            return ResolveSim2FindWounded(flow, cam);

        if (!gameManager.HasReportedEmergency())
            return ResolveSim2Phone(flow, cam);

        if (!gameManager.IsSim2TreatmentComplete())
            return ResolveSim2TreatWounded(flow, cam);

        return flow != null ? flow.sim2CompletedHint : "First aid complete.";
    }

    static string ResolveSim2FirstAidKit(TrainingFlowController flow, Camera cam)
    {
        var kits = Object.FindObjectsByType<FirstAidKitPickup>(FindObjectsSortMode.None);
        float interactDistance = MissionInteractProximity.GetDefaultInteractDistance() + 1f;

        for (int i = 0; i < kits.Length; i++)
        {
            var kit = kits[i];
            if (kit == null || !kit.isActiveAndEnabled)
                continue;

            if (MissionInteractProximity.CanPressEOn(cam, kit, interactDistance))
            {
                return flow != null
                    ? flow.sim2ObjectiveFindKitAction
                    : "Press E to collect the first aid kit.";
            }
        }

        return flow != null
            ? flow.sim2ObjectiveFindKitApproach
            : "Find the first aid kit in the city.";
    }

    static string ResolveSim2FindWounded(TrainingFlowController flow, Camera cam)
    {
        var wounded = Object.FindFirstObjectByType<WoundedMan>(FindObjectsInactive.Include);
        if (wounded != null
            && wounded.isActiveAndEnabled
            && wounded.IsFacingForInteract(cam))
        {
            return flow != null
                ? flow.sim2ObjectiveFindWoundedAction
                : "Press E on the wounded person.";
        }

        return flow != null
            ? flow.sim2ObjectiveFindWoundedApproach
            : "Find the wounded person in the city.";
    }

    static string ResolveSim2Phone(TrainingFlowController flow, Camera cam)
    {
        var booths = Object.FindObjectsByType<PublicPhoneBoothMission>(FindObjectsSortMode.None);
        PublicPhoneBoothMission nearestInRange = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < booths.Length; i++)
        {
            var booth = booths[i];
            if (booth == null || !booth.isActiveAndEnabled)
                continue;

            if (!booth.IsInPlayerInteractRange(cam))
                continue;

            float dist = Vector3.Distance(cam.transform.position, booth.GetInteractCenter());
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestInRange = booth;
            }
        }

        if (nearestInRange != null)
            return nearestInRange.GetProximityActionObjective(flow);

        return flow != null
            ? flow.sim2ObjectiveGoToPhoneApproach
            : "Go to the public telephone.";
    }

    static string ResolveSim2TreatWounded(TrainingFlowController flow, Camera cam)
    {
        var wounded = Object.FindFirstObjectByType<WoundedMan>(FindObjectsInactive.Include);
        if (wounded == null || !wounded.isActiveAndEnabled)
        {
            return flow != null
                ? flow.sim2TreatWoundedApproach
                : "Return to the wounded person for treatment.";
        }

        if (!wounded.IsFacingForInteract(cam))
        {
            return flow != null
                ? flow.sim2TreatWoundedApproach
                : "Return to the wounded person for treatment.";
        }

        if (!wounded.IsTreatmentActive)
        {
            return flow != null
                ? flow.sim2TreatWoundedPressEAction
                : "Press E on the wounded person to start treatment.";
        }

        return wounded.GetProximityTreatmentObjective(flow);
    }

    static LightSwitch ResolveLightSwitch(GameManager gameManager)
    {
        var bootstrap = gameManager.GetMissionBootstrap();
        if (bootstrap != null && bootstrap.lightSwitchObject != null)
            return bootstrap.lightSwitchObject.GetComponent<LightSwitch>();

        return Object.FindFirstObjectByType<LightSwitch>(FindObjectsInactive.Include);
    }

    static ShelterTrigger ResolveShelterTrigger(GameManager gameManager)
    {
        var mamad = GameObject.Find(gameManager.mamadObjectName);
        if (mamad == null)
            return Object.FindFirstObjectByType<ShelterTrigger>(FindObjectsInactive.Include);

        return mamad.GetComponent<ShelterTrigger>()
            ?? mamad.GetComponentInChildren<ShelterTrigger>(true);
    }
}
