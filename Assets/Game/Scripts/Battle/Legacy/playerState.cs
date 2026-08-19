using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class playerState : MonoBehaviour
{
    public static playerState Instance;

    public delegate void SetPlayer(int x,int playerAttackRange);
    public SetPlayer setplayer;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Start()
    {
        if (canPower)
        {
            UIManager.Instance.SetPowerUI();
            canPower = false;
        }
    }

    public void OnEnable()
    {
       
    }

   

    public int powerIndex = 0;
    public string tribe = string.Empty;
    public string title;
    public string myname;
    public float attackRange = 1000000000f;

    public int enemyCount = 3;
    public int enemyIndex = 3;

  

    public int playerPositionIndex = 0;
    bool canPower = true;

    public void ChangeWeapon()
    {

    }
    
}
