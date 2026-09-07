using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UnityEngine {
 public class Object {} public class MonoBehaviour:Object {} public class ScriptableObject:Object {}
 public class GameObject:Object {public Transform transform=new Transform();public bool activeSelf=true;public void SetActive(bool active){activeSelf=active;}}
 public class Transform {public Vector3 position;}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public struct Vector2Int {public int x,y;public Vector2Int(int a,int b){x=a;y=b;}}
 public static class Mathf {public static int Abs(int x)=>Math.Abs(x);public static int Min(int a,int b)=>Math.Min(a,b);}
 public static class Random {static System.Random random=new System.Random(71);public static int Range(int a,int b)=>random.Next(a,b);}
 public static class Debug {public static void LogError(object x,object c){}public static void LogWarning(object x,object c){}}
 public class SerializeField:Attribute {} public class MinAttribute:Attribute {public MinAttribute(int x){}}
 public class TooltipAttribute:Attribute {public TooltipAttribute(string x){}} public class InspectorNameAttribute:Attribute {public InspectorNameAttribute(string x){}}
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int x){}} public class CreateAssetMenuAttribute:Attribute {public string fileName,menuName;}
}
public enum TileType {Road,Store,Box,Start,Exit}
public enum BattleEnemyRank {Totem,Normal,Elite,Boss}
public enum SpawnRole {Enemy,NPC,Mercenary}
public class BattleEnemyData {public string id;public GameObject prefab=new GameObject();public BattleEnemyRank rank;public SpawnRole spawnRole;}
public class BattleEnemyDatabase {public List<BattleEnemyData> entries=new List<BattleEnemyData>();public int Count=>entries.Count;public BattleEnemyData GetAt(int i)=>entries[i];}
public class MapInfo {public Vector2Int Index;public TileType Type=TileType.Road;public bool IsWalkable=true;public Transform transform=new Transform();}
public class NewMapGenerator {public MapInfo[,] tiles=new MapInfo[7,7];public bool IsGenerateEnd()=>true;public int GetMapSizeX()=>7;public int GetMapSizeZ()=>7;public MapInfo GetMapInfo(Vector2Int p)=>tiles[p.x,p.y];}
public class EnemySpawner {
 public EnemyPool pool=new EnemyPool();public int created;public List<GameObject> SpawnedEnemies=new List<GameObject>();public List<Transform> positions=new List<Transform>();
 public void PreparePool(BattleEnemyDatabase db,Transform t){pool.Prepare(db,d=>{created++;return new GameObject();});}
 public int AvailableCount(BattleEnemyData d)=>pool.Available(d);
 public GameObject SpawnEnemy(Transform t,BattleEnemyData d){var g=pool.Take(d);if(g==null)return null;g.SetActive(true);positions.Add(t);SpawnedEnemies.Add(g);return g;}
 public void ReleaseAll(){foreach(var g in SpawnedEnemies)g.SetActive(false);SpawnedEnemies.Clear();positions.Clear();}
}
public static class BattleTileLocator {public static MapInfo FindClosestXZ(Vector3 p,List<MapInfo> tiles)=>tiles.OrderBy(t=>Math.Pow(t.transform.position.x-p.x,2)+Math.Pow(t.transform.position.z-p.z,2)).FirstOrDefault();}
public static class StagePoolChecks {
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
 static void Set(object o,string field,object value)=>o.GetType().GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(o,value);
 public static string Run(){
  var db=new BattleEnemyDatabase();for(int i=0;i<8;i++)db.entries.Add(new BattleEnemyData{id="enemy"+i,rank=i<5?BattleEnemyRank.Normal:i<7?BattleEnemyRank.Elite:BattleEnemyRank.Boss});
  var pool=new EnemyPool();int created=0;pool.Prepare(db,d=>{created++;return new GameObject();});
  Check(created==30,"expected 25 normal + 4 elite + 1 boss");pool.Prepare(db,d=>{created++;return new GameObject();});Check(created==30,"prewarm is idempotent");
  var normal=db.entries[0];var leased=new List<GameObject>();for(int i=0;i<5;i++){var g=pool.Take(normal);Check(g!=null,"rent within capacity");g.SetActive(true);leased.Add(g);}
  Check(pool.Take(normal)==null&&created==30,"exhausted pool must not instantiate");leased[2].SetActive(false);Check(object.ReferenceEquals(pool.Take(normal),leased[2]),"return uses same instance");foreach(var g in leased)g.SetActive(false);
  var stage=new StageObjData{enemyDatabase=db};stage.normalEnemies.count=8;stage.normalEnemies.enemyIds=new[]{"enemy0","enemy1"};var roster=new List<BattleEnemyData>();string message;
  for(int i=0;i<200;i++){Check(StageEnemyBridge.TryBuild(stage,roster,out message,pool.Available),"build");Check(roster.Count==8&&roster.GroupBy(d=>d).All(g=>g.Count()<=5),"draw respects per-type capacity");}
  stage.normalEnemies.count=12;StageEnemyBridge.TryBuild(stage,roster,out message,pool.Available);Check(roster.Count==10&&message!=null,"shortage clamps and warns");
  stage.normalEnemies.count=4;stage.normalEnemies.enemyIds=new[]{"typo","enemy7","enemy0","enemy0"};StageEnemyBridge.TryBuild(stage,roster,out message,pool.Available);Check(roster.Count==4&&roster.All(d=>d==normal),"bad candidate and rank do not stop valid candidates");
  db.entries.Reverse();StageEnemyBridge.TryBuild(stage,roster,out message,pool.Available);Check(roster[0]==normal,"ID stable after reordering");db.entries.Reverse();
  db.entries.Add(new BattleEnemyData{id="enemy0",rank=BattleEnemyRank.Normal});StageEnemyBridge.TryBuild(stage,roster,out message,pool.Available);Check(roster.Count==0,"ambiguous ID skipped");db.entries.RemoveAt(8);
  var map=new NewMapGenerator();var tiles=new List<MapInfo>();for(int x=0;x<7;x++)for(int z=0;z<7;z++){var t=new MapInfo{Index=new Vector2Int(x,z)};t.transform.position=new Vector3(x,0,z);map.tiles[x,z]=t;tiles.Add(t);}
  var result=new List<MapInfo>();var center=map.tiles[3,3];var occupied=new HashSet<MapInfo>{map.tiles[2,2]};map.tiles[2,3].Type=TileType.Store;map.tiles[2,4].Type=TileType.Box;map.tiles[3,2].IsWalkable=false;
  StagePlacement.CollectSummonTiles(tiles,center,occupied,1,result);Check(result.Count==4,"summon 3x3 excludes center, occupied and special tiles");
  StagePlacement.CollectSummonTiles(null,center,occupied,1,result);Check(result.Count==0,"invalid map means no summon");StagePlacement.CollectSummonTiles(tiles,center,occupied,1,null);
  var stages=new StageObjData[4];for(int i=0;i<4;i++){stages[i]=new StageObjData{stageNumber=i+1,enemyDatabase=db};stages[i].normalEnemies.count=i==3?0:5+i;stages[i].normalEnemies.enemyIds=new[]{"enemy0","enemy1","enemy2","enemy3","enemy4"};stages[i].bossEnemies.count=i==3?1:0;stages[i].bossEnemies.enemyIds=new[]{"enemy7"};}
  var stageSpawner=new StageSpawner();var enemies=new EnemySpawner();Set(stageSpawner,"mapGenerator",map);Set(stageSpawner,"enemySpawner",enemies);Set(stageSpawner,"stages",stages);var player=new Transform{position=new Vector3(3,0,3)};
  for(int pass=0;pass<3;pass++)for(int i=1;i<=4;i++){
   stageSpawner.ReleaseStageEnemies();Check(stageSpawner.CanSelectStage(i)&&stageSpawner.TrySelectStage(i),"select stage");Check(stageSpawner.TrySpawnStageEnemies(player),"spawn stage");Check(enemies.created==30,"stage cycling never grows pool");Check(enemies.SpawnedEnemies.Count==(i==4?1:4+i),"stage counts");Check(enemies.positions.Distinct().Count()==enemies.positions.Count,"no tile overlap");Check(enemies.positions.All(t=>!StagePlacement.IsInsideSquare(new Vector2Int((int)t.position.x,(int)t.position.z),center.Index,1)),"safe area");Check(stageSpawner.TrySpawnStageEnemies(player)&&enemies.created==30,"duplicate spawn is idempotent");
  }
  Check(!stageSpawner.HasStage(5)&&!stageSpawner.CanSelectStage(5),"no implicit stage5");stageSpawner.ReleaseStageEnemies();Check(enemies.SpawnedEnemies.Count==0,"release clears active list");
  return checks+" assertions passed (actual Stage/Bridge/Pool sources with Unity stubs; not Play Mode).";
 }
}
