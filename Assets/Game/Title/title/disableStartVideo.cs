using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class disableStartVideo : MonoBehaviour
{
    [SerializeField]
    GameObject video;
    VideoPlayer videoPlayer;

    private void Awake()
    {
        DataConfig.Init();
        if (video != null) videoPlayer = video.GetComponent<VideoPlayer>();
    }

    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) SkipVideo();
    }

    public void SkipVideo()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        if (video != null) video.SetActive(false);
    }
}
