using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DrawState : Graphic
{
    [SerializeField]
    private float maxStat = 20f;
    [SerializeField]
    private CharactorStatus status;

    [SerializeField]
    TextMeshProUGUI[] statusText;

    float[] angles =
{
    90,
    30,
    -30,
    -90,
    -150,
    150
};

    // 장비 스탯 보너스(순서는 values[]와 동일: STR, DEX, INT, WIS, CAR, VIT). CharactorStatus는
    // 캐릭터 선택 화면에서 배분한 기본 스탯만 담고 있어 장비를 갈아껴도 값이 그대로였다.
    private readonly int[] equipmentBonus = new int[6];

    public override Texture mainTexture => Texture2D.whiteTexture;

    /// <summary>런타임에 스폰된 CharactorStatus를 연결한다. Inspector에는 미리 연결해둘 대상이
    /// 없어서(플레이어가 스폰돼야 존재) status가 계속 비어 있었고, 그 결과 이 레이더 차트는
    /// OnPopulateMesh에서 조용히 아무것도 그리지 못했다.</summary>
    public void SetStatus(CharactorStatus newStatus)
    {
        status = newStatus;
        Refresh();
    }

    /// <summary>현재 장착 중인 장비의 스탯 보너스를 합산해서 반영한다(기본 스탯 위에 더해짐).
    /// 순서: str, dex, int, wis, car, vit.</summary>
    public void SetEquipmentBonus(int str, int dex, int intStat, int wis, int car, int vit)
    {
        equipmentBonus[0] = str;
        equipmentBonus[1] = dex;
        equipmentBonus[2] = intStat;
        equipmentBonus[3] = wis;
        equipmentBonus[4] = car;
        equipmentBonus[5] = vit;
        Refresh();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (status == null) return;

        float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        Vector2 center = Vector2.zero;

        float[] values =
        {
        status.STR + equipmentBonus[0],
        status.DEX + equipmentBonus[1],
        status.INT + equipmentBonus[2],
        status.WIS + equipmentBonus[3],
        status.CAR + equipmentBonus[4],
        status.VIT + equipmentBonus[5]

    };

        maxStat = Mathf.Max(values[0], Mathf.Max(values[1], Mathf.Max(values[2],
                            Mathf.Max(values[3], Mathf.Max(values[4], values[5])))));

        if (maxStat <= 0)
            maxStat = 1;

        for(int i =0; i<values.Length;i++)
        {
            statusText[i].text = ((int)values[i]).ToString();
            statusText[i].ForceMeshUpdate();
        }
        //Canvas.ForceUpdateCanvases();
        AddVertex(vh, center);

        // ������ �����ؼ� �ð����
        for (int i = 0; i < 6; i++)
        {
            float value = values[i] <= 0 ? 0.5f : values[i];
            float ratio = Mathf.Clamp01(value / maxStat);

            float angle = Mathf.Deg2Rad * (90f - i * 60f);
            if(angle == 4)
            {
                angle -= 4f;
            }

            Vector2 pos = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius * ratio;

            AddVertex(vh, pos);
        }

        for (int i = 1; i <= 6; i++)
        {
            int next = (i == 6) ? 1 : i + 1;
            vh.AddTriangle(0, i, next);
        }
    }
    private void AddVertex(VertexHelper vh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }

    public void Refresh()
    {
        SetVerticesDirty();
    }
}