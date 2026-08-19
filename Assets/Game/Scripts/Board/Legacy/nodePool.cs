using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class nodePool : MonoBehaviour
{
    [SerializeField] public List<Node> nodeorigin = new List<Node>();
    public static nodePool Instance;

    private void Awake()
    {
        if(Instance==null)
        {
            Instance = this;
        }
    }

  
}
