using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps the player on the ground for a few frames after environment-learning teleport.
/// </summary>
public class EnvironmentLearningSpawnStabilizer : MonoBehaviour
{
    [SerializeField] int stabilizeFrames = 8;
    [SerializeField] float downwardProbe = 0.5f;

    public void StabilizeNow(Transform playerRoot)
    {
        if (playerRoot == null || !isActiveAndEnabled)
            return;

        StopAllCoroutines();
        StartCoroutine(StabilizeRoutine(playerRoot));
    }

    IEnumerator StabilizeRoutine(Transform playerRoot)
    {
        var cc = playerRoot.GetComponent<CharacterController>();
        if (cc == null)
            yield break;

        for (int i = 0; i < stabilizeFrames; i++)
        {
            if (!cc.enabled)
                cc.enabled = true;

            if (!cc.isGrounded)
            {
                if (PlayerGroundSnap.TrySnapToGround(playerRoot, rayHeight: 6f, maxRayDistance: 40f))
                {
                    yield return null;
                    continue;
                }

                cc.Move(Vector3.down * downwardProbe);
            }

            var fps = playerRoot.GetComponent<SimpleFPSController>();
            fps?.ResetVerticalVelocity();

            yield return null;
        }
    }
}
