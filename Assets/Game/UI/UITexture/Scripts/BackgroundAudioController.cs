using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundAudioController : MonoBehaviour
{
    const string KEY = "PlayInBackgroundAudio";

    GraphicsSettingsRuntime runtime;
    bool isMutedByFocus;

    void Awake()
    {
        runtime = FindObjectOfType<GraphicsSettingsRuntime>();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (runtime == null)
            return;
        
        int allow = runtime.GetEnum(KEY); // 0 or 1

        if (!hasFocus && allow == 0)
        {
            // 포커스 잃음 + 비활성화
            MuteAll();
            isMutedByFocus = true;
        }
        else if (hasFocus && isMutedByFocus)
        {
            // 포커스 복귀
            RestoreVolume();
            isMutedByFocus = false;
        }
    }

    void MuteAll()
    {
        AudioListener.volume = 0f;
    }

    void RestoreVolume()
    {
        AudioListener.volume = 1f;
    }
}
