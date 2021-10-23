using AKCondinoO.Voxels;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.simObject.persistentData;
using static AKCondinoO.simObjectSpawner;
using static AKCondinoO.Voxels.voxelTerrain;
using static AKCondinoO.Voxels.voxelTerrainChunk;
using static utils;
namespace AKCondinoO{internal class simObject:NetworkBehaviour{
internal LinkedListNode<simObject>disabled;
internal ulong?id;internal(ulong id,int?cnkIdx)?fileIndex{get{return fileIndex_v;}
set{
if(value!=null){
fileData.Copytransform(transform);
}
fileIndex_v=value;
}
}(ulong id,int?cnkIdx)?fileIndex_v;
internal void OnExitSave(persistentDataMultithreaded[]threads,List<ManualResetEvent>handles){//Debug.Log("OnExitSave()");
if(this!=null&&gameObject!=null){//Debug.Log("OnExitSave gameObject not destroyed yet, call DisableSim");
DisableSim();
}else{
//Debug.Log("OnExitSave gameObject already destroyed");
}
if(threads[0]!=null&&threads[0].IsRunning()){//Debug.Log("salvar antes de sair");
if(id!=null){
fileData.backgroundData.WaitOne();
if(unloading){
unloading=false;
if(fileData.unplace){
fileData.unplace=false;
OnUnplace(this);
}
OnDisabledSim(this);
}else{
if(loading){
fileData.Getserializable();
}else if(fileIndex!=null){
fileData.fileIndex_bg=fileIndex.Value;
fileIndex=null;
loading=true;
}
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);handles.Add(fileData.backgroundData);
}
}
}
}
internal NetworkObject network;internal NetworkTransform networkTransform;
internal Bounds localBounds;readonly Vector3[]boundsVertices=new Vector3[8];bool boundsVerticesTransformed;
internal new Collider[]collider;internal new Rigidbody rigidbody;internal readonly List<Collider>volumeCollider=new List<Collider>();
internal new Renderer[]renderer;
void Awake(){//Debug.Log("simObject Awake");
fileData.type=GetType();
network=GetComponent<NetworkObject>();
networkTransform=GetComponent<NetworkTransform>();
collider=GetComponentsInChildren<Collider>();rigidbody=GetComponent<Rigidbody>();foreach(var col in collider)if(col.CompareTag("volume"))volumeCollider.Add(col);
renderer=GetComponentsInChildren<Renderer>();
localBounds=new Bounds(Vector3.zero,Vector3.zero);
foreach(var col in collider){
localBounds.Encapsulate(col.bounds);
}
DisableSim();
}
internal bool isSimEnabled=true;
void DisableSim(){
if(isSimEnabled){//Debug.Log("DisableSim");
isSimEnabled=false;
networkTransform.enabled=false;
foreach(var col in collider){if(col.CompareTag("volume"))continue;col.enabled=false;}if(rigidbody){rigidbody.constraints=RigidbodyConstraints.FreezeAll;}
foreach(var ren in renderer){ren.enabled=false;}
}
}
void EnableSim(){
if(!isSimEnabled&&validated){//Debug.Log("EnableSim");
isSimEnabled=true;
networkTransform.enabled=true;
foreach(var col in collider){col.enabled=true;}if(rigidbody){rigidbody.constraints=RigidbodyConstraints.None;}
foreach(var ren in renderer){if(ren.name.Equals("tree"))continue;ren.enabled=true;}
}
}
bool loading;bool unloading;
       internal Vector2Int cCoord,cCoord_Pre;
              internal int cnkIdx;
internal voxelTerrainChunk cnk;bool cnkMoved;
protected Quaternion previousRotation;
   protected Vector3 previousPosition;
   protected Vector3 previousScale;
protected bool validating,validated,noOverlaps;
bool outOfTerrain;
protected bool sleeping;
[SerializeField]bool DEBUG_UNLOAD=false;
[SerializeField]bool DEBUG_UNPLACE=false;
void Update(){
if(NetworkManager.Singleton.IsServer){
if(sleeping){
if(cnk==null||cnk.gameObject==null||(cnkMoved=cnk.moved)){Debug.Log("wake up! cnkMoved!");
sleeping=false;
}else 
if(transform.rotation!=previousRotation
 ||transform.position!=previousPosition
 ||transform.localScale!=previousScale){
sleeping=false;
}else{
if(overlappingRemoved.Count>0)overlappingRemoved.Clear();
return;
}
}
if(id!=null){//Debug.Log("I exist");
if(loading){//Debug.Log("background loading in progress...");
if(fileData.backgroundData.WaitOne(0)){//Debug.Log("got loaded data to set");
loading=false;
fileData.Getserializable();
fileData.Filltransform(transform);
transformBoundsVertices();
previousPosition=transform.position;
cCoord=cCoord_Pre=vecPosTocCoord(transform.position);
cnkIdx=GetcnkIdx(cCoord.x,cCoord.y);
}
}else if(fileIndex!=null){//Debug.Log("I need to load file data");
if(fileData.backgroundData.WaitOne(0)){//Debug.Log("start loading");
fileData.fileIndex_bg=fileIndex.Value;
fileIndex=null;
loading=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}else{
if(unloading){//Debug.Log("unloading: background saving in progress...");
if(fileData.backgroundData.WaitOne(0)){//Debug.Log("saved: now unload myself");
unloading=false;
if(fileData.unplace){
fileData.unplace=false;
OnUnplace(this);
}
OnDisabledSim(this);
}
}else if(DEBUG_UNPLACE){Debug.Log("DEBUG_UNPLACE");
DisableSim();
if(fileData.backgroundData.WaitOne(0)){Debug.Log("I need to be unloaded because:DEBUG_UNPLACE");
DEBUG_UNPLACE=false;
unloading=true;
fileData.unplace=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}else if(DEBUG_UNLOAD){Debug.Log("DEBUG_UNLOAD");
DisableSim();
if(fileData.backgroundData.WaitOne(0)){Debug.Log("I need to be unloaded because:DEBUG_UNLOAD");
DEBUG_UNLOAD=false;
unloading=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}else if(transform.position.y<-Height/2f){
if(previousPosition.y<-Height/2f){transform.position=previousPosition;}
DisableSim();
if(fileData.backgroundData.WaitOne(0)){Debug.Log("I need to be unloaded because:transform.position.y<-Height/2f:transform.position.y:"+transform.position.y);
unloading=true;
fileData.unplace=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}else if(validating&&!noOverlaps&&(isOverlapping||IsOverlappingNonAlloc())){//Debug.Log("Overlapping",this);
DisableSim();
if(fileData.backgroundData.WaitOne(0)){Debug.Log("I need to be unloaded because:isOverlapping");
isOverlapping=false;
unloading=true;
fileData.unplace=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}else if(cnkMoved){
DisableSim();
if(fileData.backgroundData.WaitOne(0)){Debug.Log("I need to be unloaded because:cnkMoved");
cnkMoved=false;
unloading=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}else if(outOfTerrain||(outOfTerrain=(boundsVerticesTransformed&&boundsVertices.Any(v=>{Vector2Int cCoord=vecPosTocCoord(v);int cnkIdx=GetcnkIdx(cCoord.x,cCoord.y);return(!voxelTerrain.active.TryGetValue(cnkIdx,out voxelTerrainChunk cnk)||cnk.meshDirty);})))){
transform.position=previousPosition;
DisableSim();
if(fileData.backgroundData.WaitOne(0)){Debug.Log("I need to be unloaded because:cnk==null or cnk.meshDirty");
outOfTerrain=false;
unloading=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}else if(instantiation!=null){
EnableSim();
boundsVerticesTransformed=false;
if(transform.rotation!=previousRotation
 ||transform.position!=previousPosition
 ||transform.localScale!=previousScale){
transformBoundsVertices();
cCoord_Pre=cCoord;
if(cCoord!=(cCoord=vecPosTocCoord(transform.position))){Debug.Log("I moved to cCoord:"+cCoord);
cnkIdx=GetcnkIdx(cCoord.x,cCoord.y);
}
validating=true;
}else if(validating){
if(noOverlaps){noOverlaps=false;
validating=false;
validated=true;
}
}else if(rigidbody==null&&voxelTerrain.active.TryGetValue(cnkIdx,out cnk)&&!(cnkMoved=cnk.moved)){Debug.Log("sleep...");
sleeping=true;
}
fileData.Copytransform(transform);
previousRotation=transform.rotation;
previousPosition=transform.position;
previousScale=transform.localScale;
}
}
}
}
void transformBoundsVertices(){
boundsVertices[0]=transform.TransformPoint(localBounds.min.x,localBounds.min.y,localBounds.min.z);
boundsVertices[1]=transform.TransformPoint(localBounds.max.x,localBounds.min.y,localBounds.min.z);
boundsVertices[2]=transform.TransformPoint(localBounds.max.x,localBounds.min.y,localBounds.max.z);
boundsVertices[3]=transform.TransformPoint(localBounds.min.x,localBounds.min.y,localBounds.max.z);
boundsVertices[4]=transform.TransformPoint(localBounds.min.x,localBounds.max.y,localBounds.min.z);
boundsVertices[5]=transform.TransformPoint(localBounds.max.x,localBounds.max.y,localBounds.min.z);
boundsVertices[6]=transform.TransformPoint(localBounds.max.x,localBounds.max.y,localBounds.max.z);
boundsVertices[7]=transform.TransformPoint(localBounds.min.x,localBounds.max.y,localBounds.max.z);
boundsVerticesTransformed=true;
}
if(overlappingRemoved.Count>0)overlappingRemoved.Clear();
}
internal readonly Dictionary<Collider,simObject>overlappingRemoved=new Dictionary<Collider,simObject>();
void OnOverlappingRemoved(List<Collider>volumeCollider,simObject sO){Debug.Log("overlapping object removed itself because of me:"+name+" at "+transform.position,this);Debug.Log("mark that I have this one overlapping removed:"+sO.name+" at "+sO.transform.position,sO);
for(int i=0;i<volumeCollider.Count;++i){
overlappingRemoved[volumeCollider[i]]=sO;
}
}
bool isOverlapping;
Collider[]overlappingNonAllocColliders=new Collider[1];
bool IsOverlappingNonAlloc(){
if(rigidbody!=null){return false;}
bool result=false;
int overlappingsLength;for(int i=0;i<volumeCollider.Count;++i){var size=volumeCollider[i].bounds.size;var center=volumeCollider[i].bounds.center;//Debug.Log("center:"+center);
if((overlappingsLength=Physics.OverlapBoxNonAlloc(center,size/2f,overlappingNonAllocColliders,transform.rotation,physHelper.volumeCollider))>0){
while(overlappingNonAllocColliders.Length<=overlappingsLength){
Array.Resize(ref overlappingNonAllocColliders,overlappingsLength*2);
Debug.Log("overlappingNonAllocColliders resized to:"+overlappingNonAllocColliders.Length);
overlappingsLength=Physics.OverlapBoxNonAlloc(center,size/2f,overlappingNonAllocColliders,transform.rotation,physHelper.volumeCollider);
}
for(int j=0;j<overlappingsLength;++j){var overlapping=overlappingNonAllocColliders[j];
if(overlapping.GetComponent<Rigidbody>()!=null){continue;}
if(overlapping.transform.root!=transform.root){Debug.Log("overlapping.transform.root:"+overlapping.transform.root.position,overlapping.transform.root);Debug.Log("transform.root:"+transform.root.position,transform.root);
if(!overlappingRemoved.ContainsKey(overlapping)){
result=true;
}
}
}
if(result){
for(int j=0;j<overlappingsLength;++j){var overlapping=overlappingNonAllocColliders[j];
simObject sO;if((sO=overlapping.transform.parent.GetComponent<simObject>())!=null){
sO.OnOverlappingRemoved(volumeCollider,this);
}
}
}
}
}
noOverlaps=!result;
return isOverlapping=result;}
internal readonly persistentData fileData=new persistentData();
internal class persistentData:backgroundObject{
internal readonly object syn=new object();
internal readonly updatedTransform transform=new updatedTransform();internal class updatedTransform{
public Quaternion rotation{get;set;}
public Vector3    position{get;set;}
public Vector3    scale{get;set;}
}
internal void Copytransform(Transform transform){
this.transform.rotation=transform.rotation;
this.transform.position=transform.position;
this.transform.scale=transform.localScale;
}
internal void Filltransform(Transform transform){
transform.rotation=this.transform.rotation;
transform.position=this.transform.position;
transform.localScale=this.transform.scale;
}
internal Type type;internal(ulong id,int?cnkIdx)fileIndex_bg;internal bool unplace;
internal readonly serializableTransform transform_bg=new serializableTransform();[Serializable]internal class serializableTransform{
public SerializableQuaternion rotation{get;set;}
public SerializableVector3    position{get;set;}
public SerializableVector3    scale{get;set;}
}
internal readonly serializableSpecsData specsData_bg=new serializableSpecsData();[Serializable]internal class serializableSpecsData{
public string transformFile;
}
internal void Setserializable(){
transform_bg.rotation=transform.rotation;
transform_bg.position=transform.position;
transform_bg.scale=transform.scale;
}
internal void Getserializable(){
transform.rotation=transform_bg.rotation;
transform.position=transform_bg.position;
transform.scale=transform_bg.scale;
}
}
internal class persistentDataMultithreaded:baseMultithreaded<persistentData>{
readonly JsonSerializer jsonSerializer=new JsonSerializer();
(ulong id,int?cnkIdx)fileIndex{get{return current.fileIndex_bg;}set{current.fileIndex_bg=value;}}bool unplace{get{return current.unplace;}}
serializableTransform transform{get{return current.transform_bg;}}
serializableSpecsData specsData{get{return current.specsData_bg;}}
protected override void Renew(persistentData next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){//Debug.Log("Execute()");
lock(current.syn){
bool loaded=false;
string transformFile;
string specsDataFile=string.Format("{0}({1},{2}).JsonSerializer",sObjectsSavePath,current.type,fileIndex.id);//Debug.Log("specifications data file: "+specsDataFile);
using(var file=new FileStream(specsDataFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
if(file.Length>0){//Debug.Log("I already exist");
using(var reader=new StreamReader(file)){using(var json=new JsonTextReader(reader)){
serializableSpecsData last=(serializableSpecsData)jsonSerializer.Deserialize(json,typeof(serializableSpecsData));
if(!fileIndex.cnkIdx.HasValue){/*  :saving! ...Or else just being loaded  *///Debug.Log("sim object is being saved in a new place, not just loaded; delete last transform data file: "+last.transformFile);
if(
File.Exists(last.transformFile)){
File.Delete(last.transformFile);
}
transformFile=nexttransformFile();
}else{//Debug.Log("sim object is being loaded, set transform data file to last: "+last.transformFile);
transformFile=last.transformFile;
loaded=true;
}
}}
}else{//Debug.Log("I just came to existence");
transformFile=nexttransformFile();
}
string nexttransformFile(){
Vector2Int cnkRgn1=vecPosTocnkRgn(transform.position);
Vector2Int cCoord1=cnkRgnTocCoord(cnkRgn1);
       int cnkIdx1=GetcnkIdx(cCoord1.x,cCoord1.y);
string transformPath=string.Format("{0}{1}/",perChunkSavePath,cnkIdx1);
Directory.CreateDirectory(transformPath);
return string.Format("{0}({1},{2}).JsonSerializer",transformPath,current.type,fileIndex.id);
}
}
//Debug.Log("transform data file: "+transformFile);
if(loaded){//Debug.Log("load transform data file");
using(var file=new FileStream(transformFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
using(var reader=new StreamReader(file)){using(var json=new JsonTextReader(reader)){
serializableTransform load=(serializableTransform)jsonSerializer.Deserialize(json,typeof(serializableTransform));
transform.rotation=load.rotation;
transform.position=load.position;
transform.scale=load.scale;
}}
}
}
if(unplace){
specsData.transformFile="";
}else{
using(var file=new FileStream(transformFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
file.SetLength(0);
file.Flush(true);
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,transform,typeof(serializableTransform));
}}
}
specsData.transformFile=transformFile;
}
using(var file=new FileStream(specsDataFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
file.SetLength(0);
file.Flush(true);
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,specsData,typeof(serializableSpecsData));
}}
}
var f=fileIndex;f.cnkIdx=null;fileIndex=f;//Debug.Log("set persistent data to be saved from now on");
}
}
}
#if UNITY_EDITOR
void OnDrawGizmos(){
//DrawBounds(localBounds,Color.yellow);
Debug.DrawLine(boundsVertices[0],boundsVertices[1],Color.white);
Debug.DrawLine(boundsVertices[1],boundsVertices[2],Color.white);
Debug.DrawLine(boundsVertices[2],boundsVertices[3],Color.white);
Debug.DrawLine(boundsVertices[3],boundsVertices[0],Color.white);
Debug.DrawLine(boundsVertices[4],boundsVertices[5],Color.white);
Debug.DrawLine(boundsVertices[5],boundsVertices[6],Color.white);
Debug.DrawLine(boundsVertices[6],boundsVertices[7],Color.white);
Debug.DrawLine(boundsVertices[7],boundsVertices[4],Color.white);
Debug.DrawLine(boundsVertices[0],boundsVertices[4],Color.white);// sides
Debug.DrawLine(boundsVertices[1],boundsVertices[5],Color.white);
Debug.DrawLine(boundsVertices[2],boundsVertices[6],Color.white);
Debug.DrawLine(boundsVertices[3],boundsVertices[7],Color.white);
}
#endif
}}