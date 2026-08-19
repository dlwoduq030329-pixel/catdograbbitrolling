using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class animSet : MonoBehaviour
{
    Vector3 originPos;
    Animator anim;
    string animKey = "Dice_Red_Result_";

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }
    // Start is called before the first frame update
    void Start()
    {
        originPos = transform.position;
    }
    public void animRoll(int x)
    {
        //anim.Play("Idle");
        StartCoroutine(animCor(x));
    }

    public IEnumerator animCor(int x)
    {
        anim.Rebind();
        anim.Update(0);
        yield return null;
        this.transform.position = originPos;
        this.transform.rotation = Quaternion.Euler(37.109f, 0, 0);
        yield return null;
        anim.Play(animKey + x.ToString());
    }

    public void Sound()
    {
        SoundManager.Instance.RollDice();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
