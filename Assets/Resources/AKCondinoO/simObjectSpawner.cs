using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.simObject;
using static AKCondinoO.Voxels.voxelTerrain;
namespace AKCondinoO{internal class simObjectSpawner:MonoBehaviour{internal static simObjectSpawner singleton;
internal static readonly Dictionary<Type,GameObject>prefabs=new Dictionary<Type,GameObject>();internal static readonly Dictionary<(Type type,ulong id),simObject>active=new Dictionary<(Type,ulong),simObject>();internal static readonly List<simObject>all=new List<simObject>();internal static readonly Dictionary<Type,LinkedList<simObject>>pool=new Dictionary<Type,LinkedList<simObject>>();
readonly persistentDataMultithreaded[]persistentDataThreads=new persistentDataMultithreaded[Environment.ProcessorCount];
void OnDisable(){Debug.Log("spawner disabled");
if(instantiation!=null){Debug.Log("spawner disconnected");
StopCoroutine(instantiation);instantiation=null;
fileSearchMultithreaded.Stop=true;filesThread?.Wait();fileSearchMultithreaded.Clear();
List<ManualResetEvent>handles=new List<ManualResetEvent>();foreach(var sO in all){sO.OnExitSave(persistentDataThreads,handles);}foreach(var handle in handles)handle.WaitOne();
persistentDataMultithreaded.Stop=true;for(int i=0;i<persistentDataThreads.Length;++i){persistentDataThreads[i]?.Wait();}persistentDataMultithreaded.Clear();
ids.OnExitSave(idsThread);
uniqueIdsMultithreaded.Stop=true;idsThread?.Wait();uniqueIdsMultithreaded.Clear();
}
}
void OnDestroy(){Debug.Log("on destroy sim object spawner");
ids.backgroundData.Dispose();
ids.foregroundData.Dispose();
}
void Awake(){singleton=this;
foreach(var o in Resources.LoadAll("AKCondinoO/",typeof(GameObject))){var gO=(GameObject)o;var sO=gO.GetComponent<simObject>();if(sO==null)continue;
Type t=sO.GetType();
prefabs[t]=gO;pool[t]=new LinkedList<simObject>();
}
waitUntilInstantiationRequested=new WaitUntil(()=>instantiating);waitUntilIdsSaved=new WaitUntil(()=>ids.backgroundData.WaitOne(0));waitUntilFilesSearched=new WaitUntil(()=>files.backgroundData.WaitOne(0));
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
if(!NetworkManager.Singleton.IsServer
 &&!NetworkManager.Singleton.IsClient){
if(instantiation!=null){Debug.Log("spawner disconnected");
StopCoroutine(instantiation);instantiation=null;
fileSearchMultithreaded.Stop=true;filesThread?.Wait();fileSearchMultithreaded.Clear();
List<ManualResetEvent>handles=new List<ManualResetEvent>();foreach(var sO in all){sO.OnExitSave(persistentDataThreads,handles);}foreach(var handle in handles)handle.WaitOne();
persistentDataMultithreaded.Stop=true;for(int i=0;i<persistentDataThreads.Length;++i){persistentDataThreads[i]?.Wait();}persistentDataMultithreaded.Clear();
ids.OnExitSave(idsThread);
uniqueIdsMultithreaded.Stop=true;idsThread?.Wait();uniqueIdsMultithreaded.Clear();
foreach(var sO in all){
sO.fileData.backgroundData.Dispose();
sO.fileData.foregroundData.Dispose();
DestroyImmediate(sO);
}all.Clear();
active.Clear();
foreach(var p in pool)p.Value.Clear();
}
}
if(NetworkManager.Singleton.IsServer){
if(instantiation==null&&!string.IsNullOrEmpty(saveName)){Debug.Log("spawner connected");
uniqueIdsMultithreaded.Stop=false;idsThread=new uniqueIdsMultithreaded();
ids.Init();
instantiation=StartCoroutine(Instantiation());
fileSearchMultithreaded.Stop=false;filesThread=new fileSearchMultithreaded();
persistentDataMultithreaded.Stop=false;for(int i=0;i<persistentDataThreads.Length;++i){persistentDataThreads[i]=new persistentDataMultithreaded();}
foreach(var sO in all){
sO.OnSpawnerConnected();
}
}
//Debug.Log("instantiating:"+instantiating);
if(!instantiating){
if(loadingRequired){Debug.Log("loadingRequired");
reloadTimer=reloadInterval;
files.Inbounds(bounds);
fileSearchMultithreaded.Schedule(files);
}
instantiating=loadingRequired||spawnerQueue.Count>0;//Debug.Log("instantiation requested:"+instantiating);
loadingRequired=false;
}
}
}
bool instantiating;WaitUntil waitUntilInstantiationRequested;WaitUntil waitUntilIdsSaved;WaitUntil waitUntilFilesSearched;
internal static Coroutine instantiation;IEnumerator Instantiation(){
Loop:{}yield return waitUntilInstantiationRequested;/*Debug.Log("loading ids");*/yield return waitUntilIdsSaved;yield return waitUntilFilesSearched;Debug.Log("begin instantiation");
while(spawnerQueue.Count>0){var toSpawn=spawnerQueue.Dequeue();
foreach(var at in toSpawn.at){
Place(at.position,at.rotation,at.scale,at.type);
}
}
foreach(var fileIndex in files.foundFileIndexes){
if(active.ContainsKey((fileIndex.type,fileIndex.id))){Debug.Log("sim object already loaded");continue;}
Debug.Log("place sim object for file found:"+fileIndex);
Place(Vector3.zero,Vector3.zero,Vector3.one,fileIndex.type,(fileIndex.id,fileIndex.cnkIdx));
}
instantiating=false;
goto Loop;}
internal static void OnDisabledSim(simObject sO){Debug.Log("OnDisabledSim");
Type type=sO.GetType();
singleton.files.unloadedFilesSyn.Add(sO.fileData.syn);
active.Remove((type,sO.id.Value));
sO.id=null;
sO.disabled=pool[type].AddLast(sO);
}
internal static void OnUnplace(simObject sO){Debug.Log("OnUnplace");
singleton.ids.Recycle(sO.id.Value,forType:sO.GetType());Debug.Log("id "+sO.id.Value+" recycled");
}
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
files.loadedFilesSyn.Add(result.fileData.syn);
active[(type,id)]=result;
return result;}
internal readonly uniqueIds ids=new uniqueIds();
internal class uniqueIds:backgroundObject{
internal Dictionary<Type,ulong>usedIds;internal Dictionary<Type,List<ulong>>deadIds;
internal ulong Generate(Type forType){
ulong id=0;if(!usedIds.ContainsKey(forType)){usedIds.Add(forType,1);}else{
if(deadIds.ContainsKey(forType)&&deadIds[forType].Count>0){var deadForType=deadIds[forType];
id=deadForType[deadForType.Count-1];deadForType.RemoveAt(deadForType.Count-1);
}else{
id=usedIds[forType]++;
}
}
return id;}
internal void Recycle(ulong id,Type forType){
if(!deadIds.ContainsKey(forType)){deadIds.Add(forType,new List<ulong>());}deadIds[forType].Add(id);
}
internal void Init(){
usedIds=null;
deadIds=null;
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
readonly JsonSerializer jsonSerializer=new JsonSerializer();
protected override void Renew(uniqueIds next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){
string usedIdsFile=string.Format("{0}{1}",savePath,"usedIds.JsonSerializer");Debug.Log("used ids file: "+usedIdsFile);
string deadIdsFile=string.Format("{0}{1}",savePath,"deadIds.JsonSerializer");Debug.Log("dead ids file: "+deadIdsFile);
if(current.usedIds==null){Debug.Log("load used ids");
using(var file=new FileStream(usedIdsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
if(file.Length>0){
using(var reader=new StreamReader(file)){using(var json=new JsonTextReader(reader)){
current.usedIds=(Dictionary<Type,ulong>)jsonSerializer.Deserialize(json,typeof(Dictionary<Type,ulong>));
}}
}else{
current.usedIds=new Dictionary<Type,ulong>();
}
}
using(var file=new FileStream(deadIdsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
if(file.Length>0){
using(var reader=new StreamReader(file)){using(var json=new JsonTextReader(reader)){
current.deadIds=(Dictionary<Type,List<ulong>>)jsonSerializer.Deserialize(json,typeof(Dictionary<Type,List<ulong>>));
}}
}else{
current.deadIds=new Dictionary<Type,List<ulong>>();
}
}
}else{Debug.Log("save used ids");
using(var file=new FileStream(usedIdsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
file.SetLength(0);
file.Flush(true);
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,current.usedIds,typeof(Dictionary<Type,ulong>));
}}
}
using(var file=new FileStream(deadIdsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
file.SetLength(0);
file.Flush(true);
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,current.deadIds,typeof(Dictionary<Type,List<ulong>>));
}}
}
}
}
}
internal readonly fileSearch files=new fileSearch();
internal class fileSearch:backgroundObject{
internal readonly List<object>unloadedFilesSyn=new List<object>();
  internal readonly List<object>loadedFilesSyn=new List<object>();
internal readonly HashSet<Vector2Int>searchWherabouts=new HashSet<Vector2Int>();
internal void Inbounds(Dictionary<UNetPrefab,(Vector2Int cCoord,Vector2Int cCoord_Pre)?>bounds){
searchWherabouts.Clear();
foreach(var b in bounds){
searchWherabouts.Add(b.Key.cCoord);
}
foreach(var syn in unloadedFilesSyn){
loadedFilesSyn.Remove(syn);
}
unloadedFilesSyn.Clear();
}
internal readonly HashSet<(Type type,ulong id,int cnkIdx)>foundFileIndexes=new HashSet<(Type,ulong,int)>();
}
internal fileSearchMultithreaded filesThread;
internal class fileSearchMultithreaded:baseMultithreaded<fileSearch>{
protected override void Renew(fileSearch next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){Debug.Log("Execute()");
foreach(var syn in current.loadedFilesSyn)Monitor.Enter(syn);try{
current.foundFileIndexes.Clear();
Debug.Log("current.searchWherabouts.Count:"+current.searchWherabouts.Count);
foreach(Vector2Int bCoord in current.searchWherabouts){//Debug.Log("bCoord:"+bCoord);
for(Vector2Int iCoord=new Vector2Int(),cCoord1=new Vector2Int();iCoord.y<=instantiationDistance.y;iCoord.y++){for(cCoord1.y=-iCoord.y+bCoord.y;cCoord1.y<=iCoord.y+bCoord.y;cCoord1.y+=iCoord.y*2){
for(           iCoord.x=0                                      ;iCoord.x<=instantiationDistance.x;iCoord.x++){for(cCoord1.x=-iCoord.x+bCoord.x;cCoord1.x<=iCoord.x+bCoord.x;cCoord1.x+=iCoord.x*2){
if(Math.Abs(cCoord1.x)>=MaxcCoordx||
   Math.Abs(cCoord1.y)>=MaxcCoordy){//Debug.Log("do not try to load sim objects at out of world cCoord:.."+cCoord1);
goto _skip;
}
//Debug.Log("try to load sim objects at:.."+cCoord1);
       int cnkIdx1=GetcnkIdx(cCoord1.x,cCoord1.y);
string transformPath=string.Format("{0}{1}/",perChunkSavePath,cnkIdx1);
if(
Directory.Exists(transformPath)){
foreach(var transformFile in 
Directory.GetFiles(transformPath)){
//Debug.Log("sim object file:.."+transformFile);
string transformFileName=
Path.GetFileName(transformFile);
string typeAndId=transformFileName.Split('(',')')[1];/*Debug.Log("typeAndId:"+typeAndId);*/string[]typeAndIdSplit=typeAndId.Split(',');string typeString=typeAndIdSplit[0];string idString=typeAndIdSplit[1];/*Debug.Log("typeString:"+typeString+";idString:"+idString);*/
Type type=Type.GetType(typeString);ulong id=ulong.Parse(idString);
current.foundFileIndexes.Add((type,id,cnkIdx1));
}
}
_skip:{}
if(iCoord.x==0){break;}}}
if(iCoord.y==0){break;}}}
}
Debug.Log("current.foundFileIndexes.Count:"+current.foundFileIndexes.Count);
}catch{throw;}finally{foreach(var syn in current.loadedFilesSyn)Monitor.Exit(syn);}
}
}
}}