using TMPro;
using UnityEngine;

public class HeartRateUI : MonoBehaviour
{
    public UDPReceiver receiver;
    public TextMeshProUGUI mainText;

    void Update()
    {
        if (receiver == null || mainText == null) return;
        if (receiver.hrvMs > 0.01f)
            mainText.text = $"Heart Rate: {receiver.heartRate} BPM\nHRV: {receiver.hrvMs:F1} ms";
        else
            mainText.text = $"Heart Rate: {receiver.heartRate} BPM\n";
                     
    }

    void Cal()
    {
        if (receiver == null || mainText == null) return;
        mainText.text = $"Heart Rate: {receiver.heartRate/100} CAl\n";
    }
    
}
