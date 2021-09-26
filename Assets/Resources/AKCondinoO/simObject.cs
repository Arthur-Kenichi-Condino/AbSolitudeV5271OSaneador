using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
namespace AKCondinoO{internal class simObject:NetworkBehaviour{
internal LinkedListNode<simObject>disabled;
internal ulong?id=null;internal(ulong id,int?cnkIdx)?fileIndex=null;
internal new Collider[]collider;internal new Rigidbody rigidbody;
internal new Renderer[]renderer;
void Awake(){Debug.Log("simObject Awake");
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
if(loading){Debug.Log("background loading in progress");
if(fileData.backgroundData.WaitOne(0)){Debug.Log("got loaded data to set");
loading=false;
}
}else if(fileIndex!=null){Debug.Log("I need to load file data");
if(fileData.backgroundData.WaitOne(0)){Debug.Log("start loading");
fileData.type=GetType();
fileData.fileIndex_bg=fileIndex;
fileIndex=null;
loading=true;
persistentDataMultithreaded.Schedule(fileData);
}
}
}
}
}
internal readonly persistentData fileData=new persistentData();
internal class persistentData:backgroundObject{
internal Type type;
internal(ulong id,int?cnkIdx)?fileIndex_bg=null;
}
internal class persistentDataMultithreaded:baseMultithreaded<persistentData>{
(ulong id,int?cnkIdx)?fileIndex{get{return current.fileIndex_bg;}}
protected override void Renew(persistentData next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){Debug.Log("Execute()");
string specsDataFile=string.Format("{0}({1},{2}){3}",sObjectsSavePath,current.type,fileIndex.Value.id,".JsonSerializer");Debug.Log("specifications data file: "+specsDataFile);
}
}
}}