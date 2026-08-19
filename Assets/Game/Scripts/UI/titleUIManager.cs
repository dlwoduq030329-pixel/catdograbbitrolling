using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class titleUIManager : MonoBehaviour
{
    public static titleUIManager Instance;

    public void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
    }
    [SerializeField] GameObject nameOBJ;
    public bool isSingle = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void setNameOBJ(bool nameOBJac)
    {
        nameOBJ.SetActive(nameOBJac);
    }
}
