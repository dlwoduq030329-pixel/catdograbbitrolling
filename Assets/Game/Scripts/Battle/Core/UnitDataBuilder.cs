using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/UnitDataBuilder")]
public class UnitDataBuilder : ScriptableObject
{
   public List<UnitData> unitDatas = new List<UnitData>();
}
