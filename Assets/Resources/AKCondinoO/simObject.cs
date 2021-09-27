using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.simObject.persistentData;
using static AKCondinoO.Voxels.voxelTerrain;
using static Utils;
namespace AKCondinoO{internal class simObject:NetworkBehaviour{
internal LinkedListNode<simObject>disabled;
internal ulong?id;internal(ulong id,int?cnkIdx)?fileIndex;
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
fileData.transform.rotation=transform.rotation;
fileData.transform.position=transform.position;
fileData.transform.scale=transform.localScale;
loading=true;
persistentDataMultithreaded.Schedule(fileData);
}
}
}
}
}
internal readonly persistentData fileData=new persistentData();
internal class persistentData:backgroundObject{
internal Type type;internal(ulong id,int?cnkIdx)fileIndex_bg;
internal readonly serializableTransform transform=new serializableTransform();[Serializable]internal class serializableTransform{
public SerializableQuaternion rotation{get;set;}
public SerializableVector3    position{get;set;}
public SerializableVector3    scale{get;set;}
}
internal readonly serializableSpecsData specsData=new serializableSpecsData();[Serializable]internal class serializableSpecsData{
public string transformFile;
}
}
internal class persistentDataMultithreaded:baseMultithreaded<persistentData>{
readonly JsonSerializer jsonSerializer=new JsonSerializer();
(ulong id,int?cnkIdx)fileIndex{get{return current.fileIndex_bg;}}
serializableTransform transform{get{return current.transform;}}
serializableSpecsData specsData{get{return current.specsData;}}
protected override void Renew(persistentData next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){//Debug.Log("Execute()");
string transformFile;
string specsDataFile=string.Format("{0}({1},{2}).JsonSerializer",sObjectsSavePath,current.type,fileIndex.id);Debug.Log("specifications data file: "+specsDataFile);
using(var file=new FileStream(specsDataFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
if(file.Length>0){Debug.Log("I already exist");
using(var reader=new StreamReader(file)){using(var json=new JsonTextReader(reader)){
serializableSpecsData last=(serializableSpecsData)jsonSerializer.Deserialize(json,typeof(serializableSpecsData));
if(!fileIndex.cnkIdx.HasValue){/*  :spawned! ...Or else just being loaded  */Debug.Log("sim object is being spawned in a new place, not just loaded; delete last transform data file: "+last.transformFile);
if(
File.Exists(last.transformFile)){
File.Delete(last.transformFile);
}
transformFile=nexttransformFile();
}else{Debug.Log("sim object is being loaded, set transform data file to last: "+last.transformFile);
transformFile=last.transformFile;
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
specsData.transformFile=transformFile;
using(var file=new FileStream(specsDataFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,specsData,typeof(serializableSpecsData));
}}
}
using(var file=new FileStream(transformFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
using(var writer=new StreamWriter(file)){using(var json=new JsonTextWriter(writer)){
jsonSerializer.Serialize(json,transform,typeof(serializableTransform));
}}
}
}
}
}}