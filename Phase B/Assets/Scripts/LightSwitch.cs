using UnityEngine;

/// <summary>
/// Home light switch — turns off interior lights during Simulation 1.
/// </summary>
public class LightSwitch : MonoBehaviour
{
    [SerializeField] private string homeObjectName = "Home";
    private GameManager _gameManager;
    private Light[] _lights;
    private bool _lightsOff;

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
        CacheLights();
    }

    public void ResetSwitch()
    {
        _lightsOff = false;
        SetLightsEnabled(true);
    }

    public void OnInteract()
    {
        if (_lightsOff)
            return;

        if (_gameManager != null && !_gameManager.CanTurnOffLights())
        {
            _gameManager.ShowMissionMessage("Collect all supplies first, then turn off the lights.", 3.5f);
            return;
        }

        _lightsOff = true;
        SetLightsEnabled(false);
        _gameManager?.OnLightsTurnedOff();
    }

    private void CacheLights()
    {
        var home = FindSceneObjectByName(homeObjectName);
        if (home == null)
        {
            _lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            return;
        }

        _lights = home.GetComponentsInChildren<Light>(true);
    }

    private void SetLightsEnabled(bool enabled)
    {
        if (_lights == null || _lights.Length == 0)
            CacheLights();

        for (int i = 0; i < _lights.Length; i++)
        {
            if (_lights[i] != null)
                _lights[i].enabled = enabled;
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
}
