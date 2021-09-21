using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.Voxels.voxelTerrain;
namespace AKCondinoO{internal class simObjectSpawner:MonoBehaviour{
internal static readonly Dictionary<Type,GameObject>prefabs=new Dictionary<Type,GameObject>();internal static readonly List<simObject>all=new List<simObject>();internal static readonly Dictionary<Type,LinkedList<simObject>>pool=new Dictionary<Type,LinkedList<simObject>>();
void OnDisable(){
StopCoroutine(instantiation);instantiation=null;
ids.OnExitSave(idsThread);
uniqueIdsMultithreaded.Stop=true;idsThread?.Wait();
}
void Awake(){
foreach(var o in Resources.LoadAll("AKCondinoO/",typeof(GameObject))){var gO=(GameObject)o;var sO=gO.GetComponent<simObject>();if(sO==null)continue;
Type t=sO.GetType();
prefabs[t]=gO;pool[t]=new LinkedList<simObject>();
}
waitUntilInstantiationRequested=new WaitUntil(()=>instantiating);waitUntilIdsSaved=new WaitUntil(()=>ids.backgroundData.WaitOne(0));
}
void OnEnable(){
uniqueIdsMultithreaded.Stop=false;idsThread=new uniqueIdsMultithreaded();
ids.Init();
instantiation=StartCoroutine(Instantiation());
}
[SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_ROTATION;
[SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_POSITION;
[SerializeField]Vector3   DEBUG_CREATE_SIM_OBJECT_SCALE=Vector3.one;
[SerializeField]simObject DEBUG_CREATE_SIM_OBJECT=null;
internal static readonly Queue<spawnData>spawnerQueue=new Queue<spawnData>();internal class spawnData{internal bool dequeued;internal readonly List<(Vector3 position,Vector3 rotation,Vector3 scale,Type type)>at=new List<(Vector3,Vector3,Vector3,Type)>(1);}
bool loadingRequired;[SerializeField]float reloadInterval=5f;float reloadTimer=0f;
void Update(){
if(DEBUG_CREATE_SIM_OBJECT){
var spawn=new spawnData();spawn.at.Add((DEBUG_CREATE_SIM_OBJECT_POSITION,DEBUG_CREATE_SIM_OBJECT_ROTATION,DEBUG_CREATE_SIM_OBJECT_SCALE,DEBUG_CREATE_SIM_OBJECT.GetType()));
spawnerQueue.Enqueue(spawn);
DEBUG_CREATE_SIM_OBJECT=null;
}
if(reloadTimer>0){reloadTimer-=Time.deltaTime;}
loadingRequired=loadingRequired||reloadTimer<=0||bounds.Any(b=>b.Value!=null);
if(NetworkManager.Singleton.IsServer){
//Debug.Log("instantiating:"+instantiating);
if(!instantiating){
if(loadingRequired){Debug.Log("loadingRequired");
reloadTimer=reloadInterval;
}
instantiating=loadingRequired||spawnerQueue.Count>0;//Debug.Log("instantiation requested:"+instantiating);
loadingRequired=false;
}
}
}
bool instantiating;WaitUntil waitUntilInstantiationRequested;WaitUntil waitUntilIdsSaved;
Coroutine instantiation;IEnumerator Instantiation(){
Loop:{}yield return waitUntilInstantiationRequested;/*Debug.Log("loading ids");*/yield return waitUntilIdsSaved;Debug.Log("begin instantiation");
while(spawnerQueue.Count>0){var toSpawn=spawnerQueue.Dequeue();
foreach(var at in toSpawn.at){
Place(at.position,at.rotation,at.scale,at.type);
}
}
instantiating=false;
goto Loop;}
simObject Place(Vector3 position,Vector3 rotation,Vector3 scale,Type type,(ulong id,int?cnkIdx)?fromFoundFile=null){simObject result;
if(pool[type].Count<=0){
simObject sO;all.Add(sO=Instantiate(prefabs[type]).GetComponent<simObject>());sO.disabled=pool[sO.GetType()].AddLast(sO);
}
result=pool[type].First.Value;pool[type].RemoveFirst();result.disabled=null;
result.transform.rotation=Quaternion.Euler(rotation);
result.transform.position=position;
result.transform.localScale=scale;
ulong id;
if(fromFoundFile==null){
id=ids.Generate(forType:type);
fromFoundFile=(id,null);
}else{
id=fromFoundFile.Value.id;
}
result.id=id;
result.fileIndex=fromFoundFile;
return result;}
internal readonly uniqueIds ids=new uniqueIds();
internal class uniqueIds:backgroundObject{
internal Dictionary<Type,ulong>used;internal Dictionary<Type,List<ulong>>unplaced;
internal ulong Generate(Type forType){
ulong id=0;
return id;}
internal void Init(){
used=null;
unplaced=null;
uniqueIdsMultithreaded.Schedule(this);
}
internal void OnExitSave(uniqueIdsMultithreaded thread){
if(thread!=null&&thread.IsRunning()){
backgroundData.WaitOne();uniqueIdsMultithreaded.Schedule(this);backgroundData.WaitOne();Debug.Log("ids exit save successful");
}
}
}
internal uniqueIdsMultithreaded idsThread;
internal class uniqueIdsMultithreaded:baseMultithreaded<uniqueIds>{
protected override void Renew(uniqueIds next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){
if(current.used==null){Debug.Log("load ids");
}else{
}
}
}
}}