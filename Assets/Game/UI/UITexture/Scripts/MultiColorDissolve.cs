using Coffee.UIEffects;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

[ExecuteAlways]
public class MultiColorDissolve : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float m_Duration = 2f;

    [SerializeField] private UIEffect m_Base;

    [Header("Dissolve 1")]
    [SerializeField] private UIEffect m_Dissolve1;

    [ColorUsage(true, true)]
    [SerializeField]
    private Color m_DissolveColor1 = Color.red;

    [Range(0, 0.1f)]
    [SerializeField]
    private float m_Delay1 = 0.05f;

    [Range(0, 0.2f)]
    [SerializeField]
    private float m_Width1 = 0.1f;

    [Range(0, 1f)]
    [SerializeField]
    private float m_Softness1 = 0.5f;

    [Header("Dissolve 2")]
    [SerializeField]
    private UIEffect m_Dissolve2;

    [ColorUsage(true, true)]
    [SerializeField]
    private Color m_DissolveColor2 = Color.magenta;

    [Range(0, 0.1f)]
    [SerializeField]
    private float m_Delay2 = 0.1f;

    [Range(0, 0.2f)]
    [SerializeField]
    private float m_Width2 = 0.1f;

    [Range(0, 1f)]
    [SerializeField]
    private float m_Softness2 = 0.5f;

    [Space]
    [Range(0, 1)] [SerializeField] private float m_Rate;

    private Coroutine m_Coroutine;

    [SerializeField]
    public Image mainIMG;

    [SerializeField]
    public Image disIMG;

    private void OnEnable()
    {
       // Play();
    }

    public void Play()
    {
        if (Application.isPlaying == false) return;

        if (m_Coroutine != null)
            StopCoroutine(m_Coroutine);
        mainIMG.gameObject.SetActive(false);
        disIMG.gameObject.SetActive(true);
        m_Coroutine = StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        float time = 0f;

        while (time < m_Duration)
        {
            time += Time.deltaTime;
            float rate = Mathf.Clamp01(time / m_Duration);
            SetRate(rate);
            yield return null;
        }
        CardUse cu = GetComponent<CardUse>();
        cu.GoOrigin();
        disIMG.gameObject.SetActive(false);
        mainIMG.gameObject.SetActive(true);
        this.gameObject.SetActive(false);

        SetRate(1f);
    }

    public void SetRate(float rate)
    {
        m_Rate = rate;

        if (m_Base)
            m_Base.transitionRate = rate;

        if (m_Dissolve1)
        {
            m_Dissolve1.transitionRate = rate - m_Delay1;
            m_Dissolve1.transitionColor = m_DissolveColor1;
            m_Dissolve1.transitionWidth = m_Width1;
            m_Dissolve1.transitionSoftness = m_Softness1;
        }

        if (m_Dissolve2)
        {
            m_Dissolve2.transitionRate = rate - m_Delay2 * 2;
            m_Dissolve2.transitionColor = m_DissolveColor2;
            m_Dissolve2.transitionWidth = m_Width2;
            m_Dissolve2.transitionSoftness = m_Softness2;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            SetRate(m_Rate);
    }
#endif
}
