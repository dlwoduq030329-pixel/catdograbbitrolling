using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChooseCharactorIndex : MonoBehaviour
{
    [SerializeField]
    public int charactorIndex;
    [SerializeField]
    Vector3 rotPos;
    [SerializeField]
    Vector3 rotTarget;

    public int CharactorIndex => charactorIndex;

    public void TurnOrigin()
    {
        StartCoroutine(Origin());
    }

    public void TurnFront()
    {
        StartCoroutine(Front());
    }

    public IEnumerator Origin()
    {
        Vector3 startPos = this.transform.eulerAngles;
        Vector3 targetPos = rotPos;
        float duration = 1f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            transform.eulerAngles = new Vector3(
                        Mathf.LerpAngle(startPos.x, targetPos.x, time / duration),
                        Mathf.LerpAngle(startPos.y, targetPos.y, time / duration),
                        Mathf.LerpAngle(startPos.z, targetPos.z, time / duration));
            yield return null;
        }

        transform.eulerAngles = targetPos;

    }

    public IEnumerator Front()
    {
        Vector3 startPos = this.transform.eulerAngles;
        Vector3 targetPos = rotTarget;
        float duration = 1f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            transform.eulerAngles = new Vector3(
                        Mathf.LerpAngle(startPos.x, targetPos.x, time / duration),
                        Mathf.LerpAngle(startPos.y, targetPos.y, time / duration),
                        Mathf.LerpAngle(startPos.z, targetPos.z, time / duration));            
             yield return null;
        }

        transform.eulerAngles = targetPos;

    }

}
