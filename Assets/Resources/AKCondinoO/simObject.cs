using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.simObject.persistentData;
using static AKCondinoO.Voxels.voxelTerrain;
using static Utils;
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
if(threads[0]!=null&&threads[0].IsRunning()){Debug.Log("salvar antes de sair");
if(id!=null){
fileData.backgroundData.WaitOne();
if(fileIndex!=null){
fileData.fileIndex_bg=fileIndex.Value;
fileIndex=null;
}
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);handles.Add(fileData.backgroundData);
}
}
}
internal new Collider[]collider;internal new Rigidbody rigidbody;
internal new Renderer[]renderer;
void Awake(){Debug.Log("simObject Awake");
fileData.type=GetType();
collider=GetComponentsInChildren<Collider>();rigidbody=GetComponent<Rigidbody>();
renderer=GetComponentsInChildren<Renderer>();
DisableSim();
}
internal bool isSimEnabled=true;
void DisableSim(){
if(isSimEnabled){Debug.Log("DisableSim");
isSimEnabled=false;
foreach(var col in collider){col.enabled=false;}if(rigidbody){rigidbody.constraints=RigidbodyConstraints.FreezeAll;}
foreach(var ren in renderer){ren.enabled=false;}
}
}
bool loading;
void Update(){
if(NetworkManager.Singleton.IsServer){
if(id!=null){//Debug.Log("I exist");
if(loading){Debug.Log("background loading in progress...");
if(fileData.backgroundData.WaitOne(0)){Debug.Log("got loaded data to set");
loading=false;
}
}else if(fileIndex!=null){Debug.Log("I need to load file data");
if(fileData.backgroundData.WaitOne(0)){Debug.Log("start loading");
fileData.fileIndex_bg=fileIndex.Value;
fileIndex=null;
loading=true;
fileData.Setserializable();
persistentDataMultithreaded.Schedule(fileData);
}
}
fileData.Copytransform(transform);
}
}
}
internal readonly persistentData fileData=new persistentData();
internal class persistentData:backgroundObject{
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
internal Type type;internal(ulong id,int?cnkIdx)fileIndex_bg;
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
(ulong id,int?cnkIdx)fileIndex{get{return current.fileIndex_bg;}set{current.fileIndex_bg=value;}}
serializableTransform transform{get{return current.transform_bg;}}
serializableSpecsData specsData{get{return current.specsData_bg;}}
protected override void Renew(persistentData next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){//Debug.Log("Execute()");
bool loaded=false;
string transformFile;
string specsDataFile=string.Format("{0}({1},{2}).JsonSerializer",sObjectsSavePath,current.type,fileIndex.id);Debug.Log("specifications data file: "+specsDataFile);
using(var file=new FileStream(specsDataFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
if(file.Length>0){Debug.Log("I already exist");
using(var reader=new StreamReader(file)){using(var json=new JsonTextReader(reader)){
serializableSpecsData last=(serializableSpecsData)jsonSerializer.Deserialize(json,typeof(serializableSpecsData));
if(!fileIndex.cnkIdx.HasValue){/*  :saving! ...Or else just being loaded  */Debug.Log("sim object is being saved in a new place, not just loaded; delete last transform data file: "+last.transformFile);
if(
File.Exists(last.transformFile)){
File.Delete(last.transformFile);
}
transformFile=nexttransformFile();
}else{Debug.Log("sim object is being loaded, set transform data file to last: "+last.transformFile);
transformFile=last.transformFile;
loaded=true;
}
}}
}else{Debug.Log("I just came to existence");
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
Debug.Log("transform data file: "+transformFile);
if(loaded){Debug.Log("load transform data file");
using(var file=new FileStream(transformFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
using(var reader=new StreamReader(file)){using(var json=new JsonTextReader(reader)){
serializableTransform load=(serializableTransform)jsonSerializer.Deserialize(json,typeof(serializableTransform));
transform.rotation=load.rotation;
transform.position=load.position;
transform.scale=load.scale;
}}
}
}
using(var file=new FileStream(transformFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,transform,typeof(serializableTransform));
}}
}
specsData.transformFile=transformFile;
using(var file=new FileStream(specsDataFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,specsData,typeof(serializableSpecsData));
}}
}
var f=fileIndex;f.cnkIdx=null;fileIndex=f;Debug.Log("set persistent data to be saved from now on");
}
}
}}