using UnityEngine;

public class SetUnityProps : MonoBehaviour
{
    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 1000;
        Application.runInBackground = true;
        Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
    }
}
