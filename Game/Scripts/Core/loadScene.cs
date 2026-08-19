using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class loadScene : MonoBehaviour
{
    [SerializeField]
    VideoPlayer video;
    [SerializeField]
    VideoClip win;
    [SerializeField]
    VideoClip lose;
    [SerializeField]
    GameObject text;

    bool temp = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void OnEnable()
    {
        Invoke(nameof(temptrue), 5f);
    }

    public void temptrue()
    {
        temp = true;
        text.SetActive(true);
    }

    public void Init(bool _win)
    {
        if(_win)
        {
            video.clip = win;
        }else
        {
            video.clip = lose;
        }


    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(0)&&temp)
        {

            SceneManager.LoadScene(0);
        }
    }
}
