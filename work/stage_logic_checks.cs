using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public class Object {} public class MonoBehaviour:Object {} public class ScriptableObject:Object {} public class GameObject:Object { public Transform transform=new Transform(); } public class Transform {public Vector3 position;}
 public struct Vector3 {public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public struct Vector2Int {public int x,y;public Vector2Int(int a,int b){x=a;y=b;}}
 public static class Mathf {public static int Abs(int v)=>Math.Abs(v);}
 public static class Random {static System.Random r=new System.Random(7);public static int Range(int a,int b)=>r.Next(a,b);}
 public static class Debug {public static void LogError(object m,object c){}}
 public class SerializeField:Attribute {} public class MinAttribute:Attribute {public MinAttribute(int x){}} public class TooltipAttribute:Attribute {public TooltipAttribute(string x){}} public class InspectorNameAttribute:Attribute {public InspectorNameAttribute(string x){}} public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int x){}}
 public class CreateAssetMenuAttribute:Attribute {public string fileName;public string menuName;}
}
public enum TileType {Road,Store,Box}
public enum BattleEnemyRank {Totem,Normal,Elite,Boss}
public enum SpawnRole {Enemy,NPC,Mercenary}
public class BattleEnemyData { public string id;public GameObject prefab=new GameObject();public BattleEnemyRank rank;public SpawnRole spawnRole; }
public class BattleEnemyDatabase { public List<BattleEnemyData> entries=new List<BattleEnemyData>();public int Count=>entries.Count;public BattleEnemyData GetAt(int i)=>entries[i];}
public class MapInfo {public Vector2Int Index;public bool IsWalkable=true;public TileType Type;public Transform transform=new Transform();}
public class NewMapGenerator { public MapInfo[,] tiles=new MapInfo[7,7];public bool IsGenerateEnd()=>true;public int GetMapSizeX()=>7;public int GetMapSizeZ()=>7;public MapInfo GetMapInfo(Vector2Int p)=>tiles[p.x,p.y];}
public class EnemySpawner {public List<GameObject> SpawnedEnemies=new List<GameObject>();public List<Transform> positions=new List<Transform>();public GameObject SpawnEnemy(Transform t,BattleEnemyData d){positions.Add(t);var g=new GameObject();SpawnedEnemies.Add(g);return g;}}
public static class BattleTileLocator {public static MapInfo FindClosestXZ(Vector3 p,List<MapInfo> tiles){MapInfo best=null;double dist=double.MaxValue;foreach(var t in tiles){double d=Math.Pow(t.transform.position.x-p.x,2)+Math.Pow(t.transform.position.z-p.z,2);if(d<dist){dist=d;best=t;}}return best;}}
public static class StageChecks {
 static int checks;static void Check(bool x,string msg){if(!x)throw new Exception(msg);checks++;}
 static void Set(object o,string f,object v)=>o.GetType().GetField(f,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(o,v);
 public static string Run(){
 var normal=new BattleEnemyData{id="slime",rank=BattleEnemyRank.Normal};var boss=new BattleEnemyData{id="boss",rank=BattleEnemyRank.Boss};var db=new BattleEnemyDatabase();db.entries.Add(normal);db.entries.Add(boss);
 var stage=new StageObjData{enemyDatabase=db};stage.normalEnemies.count=5;stage.normalEnemies.enemyIds=new[]{"slime"};var roster=new List<BattleEnemyData>();string error;
 Check(stage.TryBuildEnemyRoster(roster,out error)&&roster.Count==5,"count");Check(roster.TrueForAll(x=>x==normal),"boss leak");
 db.entries.Reverse();Check(stage.TryBuildEnemyRoster(roster,out error)&&roster[0]==normal,"stable IDs after reorder");
 stage.normalEnemies.enemyIds=new[]{"boss"};Check(!stage.TryBuildEnemyRoster(roster,out error)&&roster.Count==0,"rank mismatch rejected");stage.normalEnemies.enemyIds=new[]{"missing"};Check(!stage.TryBuildEnemyRoster(roster,out error),"unknown ID");stage.normalEnemies.enemyIds=new[]{"slime"};
 var map=new NewMapGenerator();var tiles=new List<MapInfo>();for(int x=0;x<7;x++)for(int z=0;z<7;z++){var t=new MapInfo{Index=new Vector2Int(x,z)};t.transform.position=new Vector3(x,0,z);map.tiles[x,z]=t;tiles.Add(t);}
 var result=new List<MapInfo>();var center=map.tiles[3,3];var occupied=new HashSet<MapInfo>{map.tiles[2,2]};map.tiles[2,3].Type=TileType.Store;map.tiles[2,4].Type=TileType.Box;map.tiles[3,2].IsWalkable=false;
 StagePlacement.CollectSummonTiles(tiles,center,occupied,1,result);Check(result.Count==4,"3x3 exclusions");Check(!result.Contains(center)&&!result.Contains(map.tiles[1,1]),"exclude center and outside");Check(result.Contains(map.tiles[4,4]),"3x3 diagonal included");
 var spawner=new StageSpawner();var enemy=new EnemySpawner();Set(spawner,"mapGenerator",map);Set(spawner,"enemySpawner",enemy);Set(spawner,"stages",new[]{stage});Check(spawner.TrySelectStage(1),"select stage");Check(!spawner.TrySelectStage(99),"invalid selection");var player=new Transform{position=new Vector3(3,0,3)};Check(spawner.TrySpawnStageEnemies(player)&&enemy.SpawnedEnemies.Count==5,"spawn after rejected selection");Check(new HashSet<Transform>(enemy.positions).Count==5,"no duplicate tiles");foreach(var t in enemy.positions)Check(!StagePlacement.IsInsideSquare(new Vector2Int((int)t.position.x,(int)t.position.z),center.Index,1),"safe area");Check(spawner.TrySpawnStageEnemies(player)&&enemy.SpawnedEnemies.Count==5,"idempotent spawn");
 var few=new NewMapGenerator();few.tiles[3,3]=center;var s2=new StageSpawner();var e2=new EnemySpawner();Set(s2,"mapGenerator",few);Set(s2,"enemySpawner",e2);Set(s2,"stages",new[]{stage});Check(s2.TrySelectStage(1)&&!s2.TrySpawnStageEnemies(player)&&e2.SpawnedEnemies.Count==0,"insufficient tiles atomic failure");
 return checks+" checks passed (logic harness; Unity runtime not simulated).";
 }
}
