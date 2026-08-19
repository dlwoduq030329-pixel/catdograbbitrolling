using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NodeBasePlayerMov : MonoBehaviour
{
    private int nowNodeIndex = 0;
    private int targetNodeIndex;
    [SerializeField]
    nodeManagerInMain nodemanager;
    [SerializeField]
    float movSpeed;
    [SerializeField]
    LinkSelect ls;
    [SerializeField]
    cameraChase camChase;
    CheckEvent checkEvent;
    [SerializeField]
    CamMoving cam;

    MainSceneAni main;

    private void Awake()
    {
        checkEvent = GetComponent<CheckEvent>();
        this.transform.localRotation = DataConfig.Rot;
    }

    private void Start()
    {
       // if (nodemanager.nodes[nowNodeIndex].state != nodeState.start) return;
        //checkEvent.check(nodemanager.nodes[nowNodeIndex].state);
    }

    public nodeState returnST()
    {
        return nodemanager.nodes[nowNodeIndex].state;
    }

    public void MovCor(int target)
    {
        
        StartCoroutine(startMov(target));
        ls.selectUI(false);
    }

    public void SetNodeIndex(int x)
    {
        nowNodeIndex = x;
    }

    public IEnumerator startMov(int x)
    {
        cam.DiceEnd();
        main = GetComponentInChildren<MainSceneAni>();
        
        yield return new WaitForSeconds(1f);

        int movCount = x;
        int nowCount = nowNodeIndex + x;
        bool handledStartNode = false;

        for (int i = 0; i < x; i++)
        {
            main.StartWalk();
            int targetIndex = nodemanager.nodes[nowNodeIndex].NextTileid;
            SoundManager.Instance.Walk();
          

            Vector3 targetPos = nodemanager.nodes[targetIndex].transform.position;
            targetPos.y = 0;

            while (Vector3.Distance(transform.position, targetPos) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPos,
                    movSpeed * Time.deltaTime
                );

                yield return null;
            }
            yield return null;
            if (targetIndex == 0)
            {
                nowNodeIndex = 0;
                DataConfig.hard = 7;
                DataConfig.count = 1;
                //
                checkEvent.check(nodeState.start);
                handledStartNode = true;
                break;
            }
            nowNodeIndex = targetIndex;

            Check(nowNodeIndex);
        }

        Debug.Log(nowNodeIndex);
        main.ChangeIdle();

        //nowNodeIndex += x;
        DataConfig.nowPos = nowNodeIndex;

        //if (!handledStartNode)
        {
            checkEvent.check(nodemanager.nodes[nowNodeIndex].state);
            //checkEvent.check(nodeState.start);
        }
    }

    public void Check(int x)
    {

        if(x==0)
        {
            this.transform.Rotate(0, 60, 0);


            //움직임을 잠시 멈추고 다음 선택창을 만듦.
        }

        if (x == 11)
        {
            this.transform.Rotate(0, 60, 0);

           // camChase.RotCor(60f);
        }

        if(x == 23)
        {

            this.transform.Rotate(0, 60, 0);

           // camChase.RotCor(60f);

        }

        if (x == 35)
        {

            this.transform.Rotate(0, 60, 0);

           // camChase.RotCor(60f);

        }

        if (x == 47)
        {

            this.transform.Rotate(0, 60, 0);

            //camChase.RotCor(60f);

        }

        if (x == 59)
        {

            this.transform.Rotate(0, 60, 0);

           // camChase.RotCor(60f);

        }

        if (x == 71)
        {
            this.transform.Rotate(0, 60, 0);

           // camChase.RotCor(60f);
        }
        DataConfig.Rot = this.transform.localRotation;

    }
}
