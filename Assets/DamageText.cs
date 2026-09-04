using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    [SerializeField]
    Sprite physical;
    [SerializeField]
    Sprite magic;
    [SerializeField]
    GameObject criticalIMG;
    [SerializeField]
    Color physicalColor;
    [SerializeField]
    Color magicColor;
    [SerializeField]
    Color physicalCriticalColor;
    [SerializeField]
    Color magicCriticalColor;
    Color missColor = Color.white;
    [SerializeField]
    float delTime = 3f;
    float lifeTime;
    TextMeshPro text;
    SpriteRenderer critical;
    Rigidbody rb;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        text = GetComponent<TextMeshPro>();
        critical = criticalIMG.GetComponent<SpriteRenderer>();
    }


    private void Update()
    {
        lifeTime += Time.deltaTime;

        float alpha = 1f - lifeTime/delTime;

        critical.color = new Color(critical.color.r, critical.color.g, critical.color.b, alpha);
        text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);

        if(lifeTime >= delTime)
        {
            Destroy(this.gameObject);
        }
    }


    public void DamageTextInit(float damage,damType dam,bool isCritical,bool isMiss)
    {
        text.text = damage.ToString();


        
            if(isCritical)
            {
                switch(dam)
                {
                    case damType.physical:
                        {
                            text.color = physicalCriticalColor;
                            critical.sprite = physical;
                            criticalIMG.gameObject.SetActive(true);
                            text.fontSize = 72;
                        }break;
                        case damType.magic:
                        {
                            text.color = magicCriticalColor;
                            critical.sprite = magic;
                            criticalIMG.gameObject.SetActive(true);

                            text.fontSize = 72;
                        }
                        break;
                }
            }else
            {
                switch (dam)
                {
                    case damType.physical:
                        {
                            text.color = physicalColor;
                            text.fontSize =  36;

                        }
                        break;
                    case damType.magic:
                        {
                            text.color = magicColor;
                            text.fontSize = 36;


                        }
                        break;
                }
            }

        if (isMiss)
        {
            text.color = missColor;
        }
        AddForceText();
    }

    public void AddForceText()
    {
        float x = Random.Range(-0.5f, 0.5f);
        rb.AddForce((Vector3.up + x*transform.right) * 4f, ForceMode.Impulse);
    }
}
