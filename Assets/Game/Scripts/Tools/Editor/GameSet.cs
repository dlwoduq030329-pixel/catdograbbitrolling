using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Unity.VisualScripting;
using Unity.EditorCoroutines.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using System.Linq;
using TMPro;
using System;
using static System.Net.WebRequestMethods;
public class GameSet
{
    static string lastUrl = "https://sheets.googleapis.com/v4/spreadsheets/1l76KPkKcAxVP8Cok59hh2yoElw9EeNzpQt2h7Rp-l4c/values/Sheet1!A1:l30?key=AIzaSyC-0qhmXJ8Rp48PPb1Apb08VUo7erGuU9Q";


    //values/Sheet1!A1:F52?key=AIzaSyC-0qhmXJ8Rp48PPb1Apb08VUo7erGuU9Q
    static string augme1 = "https://sheets.googleapis.com/v4/spreadsheets/1zLmBUAJJGSI6KOx0IL-PVV9_uZU3UHu7swVPRwtSLeE/values/Sheet1!A1:G23?key=AIzaSyC-0qhmXJ8Rp48PPb1Apb08VUo7erGuU9Q";
    static string augme2 = "https://sheets.googleapis.com/v4/spreadsheets/1k1QkS-w1tZhYBowdjpU7gFiEiA5YDCRAZ9mI3xpti4I/values/Sheet1!A1:G23?key=AIzaSyC-0qhmXJ8Rp48PPb1Apb08VUo7erGuU9Q";
    static string augme3 = "https://sheets.googleapis.com/v4/spreadsheets/15R99iN7RHkMurPdtxJ0dHKPm6mh6y3R7IyFRkQ9mOdM/values/Sheet1!A1:G16?key=AIzaSyC-0qhmXJ8Rp48PPb1Apb08VUo7erGuU9Q";
    static string weapon = "https://sheets.googleapis.com/v4/spreadsheets/1BZjpY-mQ-hz2lVjyInvdQ1yk-lGXXQ53wHDJ7LW76hg/values/Sheet1!A1:k53?key=AIzaSyC-0qhmXJ8Rp48PPb1Apb08VUo7erGuU9Q";
    [MenuItem("CustomMenu/SetIndex")]
    static void SetIndex()
    {
        //CardSystem.Instance.SetDeck();
        EditorCoroutineUtility.StartCoroutineOwnerless(
       ConnectGoogle());
        EditorCoroutineUtility.StartCoroutineOwnerless(
       ConnectGoogle_Power1());
        EditorCoroutineUtility.StartCoroutineOwnerless(
       ConnectGoogle_Power2());
        EditorCoroutineUtility.StartCoroutineOwnerless(
       ConnectGoogle_Power3());
        EditorCoroutineUtility.StartCoroutineOwnerless(
      weaponPool());

        Debug.Log("풀 제작 완료");
    }

    public static IEnumerator ConnectGoogle()   //cardPool
    {
        using (UnityWebRequest request = UnityWebRequest.Get(lastUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {


                JObject json = JObject.Parse(request.downloadHandler.text);
                JToken items = json["values"];


                JArray rows = (JArray)items;


                CardDatabase db = AssetDatabase.LoadAssetAtPath<CardDatabase>(
                          "Assets/Game/Data/CardDatabase.asset");
                if (db == null)
                {
                    db = ScriptableObject.CreateInstance<CardDatabase>();
                    AssetDatabase.CreateAsset(db, "Assets/Game/Data/CardDatabase.asset");
                } // 해당 경로에 데이터 베이스가 없을경우

                db.cards.Clear();
                //반복해서 누를 경우 이전의 정보가 더해지기 때문에 일단 삭제

                for (int i = 1; i < rows.Count; i++)
                {

                    JArray row = (JArray)rows[i];
                    CardData card = new CardData
                    {
                        index = int.Parse(row[0].ToString()),
                        name = row[2].ToString(),
                        cost = int.Parse(row[3].ToString()),
                        rare = row[4].ToString(),
                        cardInfo = row[7].ToString(),
                        //damage = int.Parse(row[5].ToString()),
                        //heal = int.Parse(row[6].ToString()),
                    };
                    db.cards.Add(card);


                }

                EditorUtility.SetDirty(db); // 수정했음을 알림
                AssetDatabase.SaveAssets(); // 저장
            }
        }
    }

    public static IEnumerator weaponPool()   //cardPool
    {
        using (UnityWebRequest request = UnityWebRequest.Get(weapon))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {


                JObject json = JObject.Parse(request.downloadHandler.text);
                JToken items = json["values"];


                JArray rows = (JArray)items;


                EquipDatabase eb = AssetDatabase.LoadAssetAtPath<EquipDatabase>(
                          "Assets/EquipDatabase.asset");
                if (eb == null)
                {
                    eb = ScriptableObject.CreateInstance<EquipDatabase>();
                    AssetDatabase.CreateAsset(eb, "Assets/EquipDatabase.asset");
                } // 해당 경로에 데이터 베이스가 없을경우

                eb.equip.Clear();
                //반복해서 누를 경우 이전의 정보가 더해지기 때문에 일단 삭제

                for (int i = 1; i < rows.Count; i++)
                {

                    JArray row = (JArray)rows[i];
                    EquipData equip = new EquipData
                    {
                        weaponIndex = int.Parse(row[0].ToString()),
                        cardname = row[1].ToString(),
                        attackRange = float.Parse(row[2].ToString()),
                        //rare = row[3].ToString(),
                        stroffset = int.Parse(row[3].ToString()),
                        wisoffset = int.Parse(row[4].ToString()),
                        dexoffset = int.Parse(row[5].ToString()),
                        vitoffset = int.Parse(row[6].ToString()),
                    };
                    eb.equip.Add(equip);


                }

                EditorUtility.SetDirty(eb); // 수정했음을 알림
                AssetDatabase.SaveAssets(); // 저장
            }
        }
    }

    public static IEnumerator ConnectGoogle_Power1() 
    {
        using (UnityWebRequest request = UnityWebRequest.Get(augme1))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {


                JObject json = JObject.Parse(request.downloadHandler.text);
                JToken items = json["values"];


                JArray rows = (JArray)items;


                PowerDatabase1 pb1 = AssetDatabase.LoadAssetAtPath<PowerDatabase1>(
                          "Assets/Power1Database.asset");
                if (pb1 == null)
                {
                    pb1 = ScriptableObject.CreateInstance<PowerDatabase1>();
                    AssetDatabase.CreateAsset(pb1, "Assets/Power1Database.asset");
                } // 해당 경로에 데이터 베이스가 없을경우

                pb1.power1.Clear();
                //반복해서 누를 경우 이전의 정보가 더해지기 때문에 일단 삭제

                for (int i = 1; i < rows.Count; i++)
                {

                    JArray row = (JArray)rows[i];
                    Power1Data power = new Power1Data
                    {
                        index = int.Parse(row[0].ToString()),
                        title = row[1].ToString(),
                        strUP = int.Parse(row[2].ToString()),
                        wisUP = int.Parse(row[3].ToString()),
                        dexUP = int.Parse(row[4].ToString()),
                        vitUP = int.Parse(row[5].ToString()),
                        korName = row[6].ToString(),
                    };
                    pb1.power1.Add(power);


                }

                EditorUtility.SetDirty(pb1); // 수정했음을 알림
                AssetDatabase.SaveAssets(); // 저장
            }
        }
    }
    public static IEnumerator ConnectGoogle_Power2()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(augme2))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {


                JObject json = JObject.Parse(request.downloadHandler.text);
                JToken items = json["values"];


                JArray rows = (JArray)items;


                PowerDatabase2 pb2 = AssetDatabase.LoadAssetAtPath<PowerDatabase2>(
                          "Assets/Power2Database.asset");
                if (pb2 == null)
                {
                    pb2 = ScriptableObject.CreateInstance<PowerDatabase2>();
                    AssetDatabase.CreateAsset(pb2, "Assets/Power2Database.asset");
                } // 해당 경로에 데이터 베이스가 없을경우

                pb2.power2.Clear();
                //반복해서 누를 경우 이전의 정보가 더해지기 때문에 일단 삭제

                for (int i = 1; i < rows.Count; i++)
                {

                    JArray row = (JArray)rows[i];
                    Power2Data power = new Power2Data
                    {
                        index = int.Parse(row[0].ToString()),
                        title = row[1].ToString(),
                        strUp = int.Parse(row[2].ToString()),
                        wisUP = int.Parse(row[3].ToString()),
                        dexUP = int.Parse(row[4].ToString()),
                        vitUP = int.Parse(row[5].ToString()),
                        
                    addCardIndex = row[6].ToString() == "NULL" ? -1 : int.Parse(row[6].ToString()),
                    };
                   pb2.power2.Add(power);
                   

                }

                EditorUtility.SetDirty(pb2); // 수정했음을 알림
                AssetDatabase.SaveAssets(); // 저장
            }
        }
    }

    public static IEnumerator ConnectGoogle_Power3()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(augme3))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {


                JObject json = JObject.Parse(request.downloadHandler.text);
                JToken items = json["values"];


                JArray rows = (JArray)items;


                PowerDatabase3 pb3 = AssetDatabase.LoadAssetAtPath<PowerDatabase3>(
                          "Assets/Power3Database.asset");
                if (pb3 == null)
                {
                    pb3 = ScriptableObject.CreateInstance<PowerDatabase3>();
                    AssetDatabase.CreateAsset(pb3, "Assets/Power3Database.asset");
                } // 해당 경로에 데이터 베이스가 없을경우

                pb3.power3.Clear();
                //반복해서 누를 경우 이전의 정보가 더해지기 때문에 일단 삭제

                for (int i = 1; i < rows.Count; i++)
                {

                    JArray row = (JArray)rows[i];
                    Power3Data power = new Power3Data
                    {
                        index = int.Parse(row[0].ToString()),
                        title = row[1].ToString(),
                        strUp = int.Parse(row[2].ToString()),
                        wisUP = int.Parse(row[3].ToString()),
                        dexUP = int.Parse(row[4].ToString()),
                        vitUP = int.Parse(row[5].ToString()),
                        activeFuncName = row[6].ToString(),
                    };
                    pb3.power3.Add(power);


                }

                EditorUtility.SetDirty(pb3); // 수정했음을 알림
                AssetDatabase.SaveAssets(); // 저장
            }
        }
    }

}

