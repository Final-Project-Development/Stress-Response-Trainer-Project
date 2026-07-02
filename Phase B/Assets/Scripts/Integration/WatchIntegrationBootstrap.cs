using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime wiring for the watch -> game path.
///
/// It forces one shared UDP receiver on port 5055, then connects that receiver to:
/// - MockPhysiologySource, so watch HR drives SCI/stress calculations.
/// - WorkoutHeartRateChartReceiver, so the chart shows the same samples.
/// - TrainingFlowController, so disconnect warnings use the real gateway.
///
/// This avoids manual scene reference mistakes and prevents two scripts from trying to bind UDP 5055.
/// </summary>
[DefaultExecutionOrder(-9000)]
public sealed class WatchIntegrationBootstrap : MonoBehaviour
{
    private static WatchIntegrationBootstrap instance;

    public const int UnityWatchPort = 5055;
    public const float LiveDataStaleSeconds = 12f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject("Watch Integration Bootstrap");
        go.AddComponent<WatchIntegrationBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private IEnumerator Start()
    {
        // Run once before most scene Start methods finish, then again after UI/services have had a frame to initialize.
        ConfigureScene();
        yield return null;
        ConfigureScene();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ConfigureSceneNextFrame());
    }

    private IEnumerator ConfigureSceneNextFrame()
    {
        ConfigureScene();
        yield return null;
        ConfigureScene();
    }

    private void ConfigureScene()
    {
        UDPReceiver udp = FindOrCreateUdpReceiver();
        WatchSessionDataStore sessionStore = FindOrCreateSessionStore();
        MockPhysiologySource physiology = FindBestPhysiologySource();
        TrainingFlowController flow = TrainingFlowController.Instance != null
            ? TrainingFlowController.Instance
            : FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (physiology != null)
        {
            physiology.udpReceiver = udp;
            physiology.SetUdpReceiver(udp);
            physiology.useLiveUdpWhenAvailable = true;
            physiology.useSyntheticFallback = false;
            physiology.liveDataStaleSeconds = LiveDataStaleSeconds;
        }

        sessionStore.Configure(udp);

        if (flow != null)
        {
            flow.udpReceiver = udp;
            if (flow.physiology == null && physiology != null)
                flow.physiology = physiology;

            flow.hubConnectionStatusDemo =
                "Smartwatch: waiting for Fit3 workout HR (syncs after workout ends)\n" +
                "Unity UDP: 5055 | PC bridge: 7777\n" +
                "SCI source: real watch HR (HRV proxy when HRV unavailable)";
            flow.ApplyDefaultCopyToUi();
        }

        WorkoutHeartRateChartReceiver[] charts = FindObjectsByType<WorkoutHeartRateChartReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < charts.Length; i++)
        {
            WorkoutHeartRateChartReceiver chart = charts[i];
            if (chart == null)
                continue;

            chart.SetExternalUdpReceiver(udp);
            chart.receiveUdpDirectly = false;
            chart.unityListenPort = UnityWatchPort;
            chart.usePhysiologyFallbackWhenIdle = false;
            if (chart.physiologyFallback == null && physiology != null)
                chart.physiologyFallback = physiology;
        }
    }

    private static UDPReceiver FindOrCreateUdpReceiver()
    {
        UDPReceiver udp = FindFirstObjectByType<UDPReceiver>(FindObjectsInactive.Include);
        if (udp == null)
        {
            var go = new GameObject("Watch UDP Receiver");
            udp = go.AddComponent<UDPReceiver>();
        }

        udp.ConfigurePort(UnityWatchPort, true);
        return udp;
    }

    private static WatchSessionDataStore FindOrCreateSessionStore()
    {
        WatchSessionDataStore store = WatchSessionDataStore.Instance;
        if (store != null)
            return store;

        store = FindFirstObjectByType<WatchSessionDataStore>(FindObjectsInactive.Include);
        if (store == null)
        {
            var go = new GameObject("Watch Session Data Store");
            store = go.AddComponent<WatchSessionDataStore>();
        }

        return store;
    }

    private static MockPhysiologySource FindBestPhysiologySource()
    {
        TrainingFlowController flow = TrainingFlowController.Instance != null
            ? TrainingFlowController.Instance
            : FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);

        if (flow != null && flow.physiology != null)
            return flow.physiology;

        return FindFirstObjectByType<MockPhysiologySource>(FindObjectsInactive.Include);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }
}
