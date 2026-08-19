using NUnit.Framework.Constraints;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerChoose : MonoBehaviour
{
    [SerializeField]
    private Animator[] charactorAnimator;
    [SerializeField]
    private GameObject setBtn;
    [SerializeField]
    private Vector3 cameraPositionOffset;
    [SerializeField]
    GameObject[] UI;
    [SerializeField]
    GameObject statusUI;
    [SerializeField]
    CharactorTag[] tags;

    Vector3 camMovPos;

    int charactorIndex;
    bool Choose = true;

    GameObject target;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(GameStart());
    }



    // Update is called once per frame
    void Update()
    {
        ShootRayForCharactor();
        ShootRayForOnMouse();
    }

    public IEnumerator GameStart()
    {
        Vector3 startpos =Camera.main.transform.position;
        Vector3 targetPos = new Vector3(100.5f, 101.830002f, 109.519997f);
        float duration = 1f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            Camera.main.transform.position = Vector3.Lerp(startpos, targetPos, time / duration);
            yield return null;
        }
        Camera.main.transform.position = targetPos;
        Choose = false;  
    }

    public void ShootRayForOnMouse()
    {
        if (Choose) return;
        if (EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 레이가 충돌하는지 확인
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Choose")))
        {
            if (target == hit.transform.gameObject) return;
            target = hit.transform.gameObject;
            Debug.Log("냥");
            int x = hit.transform.gameObject.GetComponent<ChooseCharactorIndex>().CharactorIndex;
            tags[x].SetTag();

        }
        else
        {
            if (target == null) return;
            for(int i =0;i<tags.Length;i++)
            {
                tags[i].SetIdle();
            }

            target = null;
        }

    }

    void ShootRayForCharactor()
    {
        if (Choose) return;
        if (EventSystem.current.IsPointerOverGameObject())
            return;
        if (Input.GetMouseButtonDown(0))
        {
            // 마우스 위치에서 카메라 기준 레이 생성
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 레이가 충돌하는지 확인
            if (Physics.Raycast(ray, out hit, Mathf.Infinity,LayerMask.GetMask("Choose")))
            {
                foreach(var anim in charactorAnimator)
                {
                    if (anim.GetCurrentAnimatorStateInfo(0).IsName("Sit_Floor_Down") == true) continue;

                    anim.Play("Sit_Floor_Down");
                }
                Debug.Log(hit.collider.gameObject.name);
                //hit.collider.GetComponent<Renderer>().material.color = Color.red;

                charactorIndex = hit.collider.GetComponent<ChooseCharactorIndex>().charactorIndex;
                if (charactorAnimator[charactorIndex].GetCurrentAnimatorStateInfo(0).IsName("Skeletons_Awaken_Standing") == true) return;
                charactorAnimator[charactorIndex].Play("Skeletons_Awaken_Standing");
                setBtn.gameObject.SetActive(true);
            }
            else
            {
                setBtn.gameObject.SetActive (false);
                foreach (var anim in charactorAnimator)
                {
                    if (anim.GetCurrentAnimatorStateInfo(0).IsName("Sit_Floor_Down") == true) continue;

                    anim.Play("Sit_Floor_Down");
                }
                charactorIndex = -1;
            }

        }
    }

    public void SetCharactor()
    {
        //Camera.main.orthographic = false;
        //Camera.main.orthographicSize = 0.5f;
        Choose = true;
        StartCoroutine(cameraClose());
        foreach(var temp in UI)
        {
            temp.gameObject.SetActive(false);
        }
    }

    public IEnumerator cameraClose()
    {

        for(int i =0;i<charactorAnimator.Length;i++)
        {
            if(i==charactorIndex)
            {
                charactorAnimator[i].Play("Waving");
                charactorAnimator[i].GetComponent<ChooseCharactorIndex>().TurnFront();
            }
            else
            {
                charactorAnimator[i].Play("Cheering");

            }
        }

        yield return new WaitForSeconds(1f);
        float duration = 1f;
        float time = 0f;

        Vector3 startPos = Camera.main.transform.position;
        camMovPos = startPos;
        Vector3 targetPos = charactorAnimator[charactorIndex].transform.position + cameraPositionOffset;

        while (time < duration)
        {
            time += Time.deltaTime;

            Camera.main.transform.position =
                Vector3.Lerp(startPos, targetPos, time / duration);
            Camera.main.orthographicSize = Mathf.Lerp(2.32f, 0.8f, time / duration);


            yield return null;
        }

        // 마지막 위치 보정
        Camera.main.transform.position = targetPos;
        statusUI.SetActive(true);
        statusUI.GetComponent<StatusUI>().TextSet(charactorIndex);
    }

    public void BackChoose()
    {
        Choose = false;
        statusUI.GetComponent<StatusUI>().playerIndex = -1;
        StartCoroutine(cameraFar());
        
    }

    public IEnumerator cameraFar()
    {
        for (int i = 0; i < charactorAnimator.Length; i++)
        {
             charactorAnimator[i].Play("Sit_Floor_Down");
             charactorAnimator[i].GetComponent<ChooseCharactorIndex>().TurnOrigin();
        }
        statusUI.SetActive(false);

        yield return new WaitForSeconds(1f);
        float duration = 1f;
        float time = 0f;

        Vector3 startPos = Camera.main.transform.position;

        Vector3 targetPos =  camMovPos;

        while (time < duration)
        {
            time += Time.deltaTime;

            Camera.main.transform.position =
                Vector3.Lerp(startPos, targetPos, time / duration);
            //Camera.main.orthographicSize = Mathf.Lerp(0.8f,2.32f, time / duration);


            yield return null;
        }

        // 마지막 위치 보정
        Camera.main.transform.position = targetPos;
        foreach (var temp in UI)
        {
            temp.gameObject.SetActive(true);
        }
        //statusUI.GetComponent<StatusUI>().TextSet(charactorIndex);

    }
}
