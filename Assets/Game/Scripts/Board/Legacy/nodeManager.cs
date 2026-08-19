using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

public class nodeManager : MonoBehaviour
{
    public static nodeManager Instance;

    //[SerializeField]
   // public List<Node> nodes = new List<Node>();

    int battlenodeCount = 38;
    int storenodeCount = 12;
    int discoverynodeCount = 12;
    int eventNodeCount = 14;
    public List<int> nodeIndex = new List<int>();
    public List<nodeState> nodeStates = new List<nodeState>();

    public bool isInit = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Start()
    {

    }
    public void OnEnable()
    {
        //loadNodeInfo();
        //SetNodeState();

        
    }

    public void Setting()
    {

        if (!isInit)
        {
            isInit = true;
            InitNodeState();
        }
        StartCoroutine(nodeWait());
    }


    public void SetNodeState()
    {

        if(nodePool.Instance!=null)
        {
           // nodePool.Instance.SetNodeState(nodeStates);
            Debug.Log("노드 값 전달");
        }
    }

    public void InitNodeState()
    {
        battlenodeCount = 76;
        storenodeCount = 12;
        discoverynodeCount = 12;
        eventNodeCount = 14;
        nodeStates.Clear();
        nodeIndex.Clear();
        nodeIndex.Add(2);
        nodeIndex.Add(3);
        nodeIndex.Add(4);
        nodeIndex.Add(5);


        for (int i = 0; i < 76; i++)
        {
            if(i==0)
            {
                nodeStates.Add(nodeState.None);
            }
            nodeStates.Add(randomState());
        }
        Debug.Log("노드 초기화 완료");
    }
    

    public nodeState randomState()
    {
        int temp = 0;
        //Random.Range(0, nodeIndex.Count);
        int index = nodeIndex[temp];
        switch(index)
        {
            case 2:
                {
                    if(battlenodeCount >= 1)
                    {
                        battlenodeCount--;
                        if(battlenodeCount == 0)
                        {
                            nodeIndex.Remove(2);
                        }
                    }
                }
                break;
            case 3:
                {
                    if (storenodeCount >= 1)
                    {
                        storenodeCount--;
                        if (storenodeCount == 0)
                        {
                            nodeIndex.Remove(3);
                        }
                    }
                }
                break;
            case 4:
                {
                    if (discoverynodeCount >= 1)
                    {
                        discoverynodeCount--;
                        if (discoverynodeCount == 0)
                        {
                            nodeIndex.Remove(4);
                        }
                    }
                }
                break;
            case 5:
                {
                    if (eventNodeCount >= 1)
                    {
                        eventNodeCount--;
                        if (eventNodeCount == 0)
                        {
                            nodeIndex.Remove(5);
                        }
                    }
                }
                break;
        }

        return (nodeState)index;
    }

    public IEnumerator nodeWait()
    {
        yield return new WaitUntil(() => nodePool.Instance != null);
        SetNodeState();

    }



}
