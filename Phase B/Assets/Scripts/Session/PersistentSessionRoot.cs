using UnityEngine;

/// <summary>
/// Keeps the UDP + physiology + recorder services alive when loading the outdoor / first-aid scene.
/// Attach to the same GameObject as <see cref="UDPReceiver"/>, <see cref="MockPhysiologySource"/>, and <see cref="SessionStressRecorder"/>.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PersistentSessionRoot : MonoBehaviour
{
    public static PersistentSessionRoot Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
