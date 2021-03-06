//  AIO game
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.simObjectSpawner;
using static AKCondinoO.Voxels.voxelTerrain;
namespace AKCondinoO{internal class core:MonoBehaviour{
internal static readonly string saveLocation=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("\\","/")+"/AbSolitudeV5271OSaneador/";
internal static string saveName;
internal static string savePath;
internal static string perChunkSavePath;
internal static string sObjectsSavePath;
void Awake(){
QualitySettings.vSyncCount=0;
}
void Update(){
if(Application.targetFrameRate!=60){Application.targetFrameRate=60;}
if(!NetworkManager.Singleton.IsServer
 &&!NetworkManager.Singleton.IsClient){
if(!string.IsNullOrEmpty(saveName)&&/*  terrain stopped:  */poolSize==0&&/*  sim objects spawner stopped:  */instantiation==null){Debug.Log("game closed to main menu");
saveName=null;
savePath=null;
perChunkSavePath=null;
sObjectsSavePath=null;
}
}
if(NetworkManager.Singleton.IsServer){
if(string.IsNullOrEmpty(saveName)){
saveName="terra";
savePath=string.Format("{0}{1}/",saveLocation,saveName);Debug.Log("save path: "+savePath);
perChunkSavePath=string.Format("{0}{1}/",savePath,"chunks");Debug.Log("per chunk save path: "+perChunkSavePath);
sObjectsSavePath=string.Format("{0}{1}/",savePath,"sObjpd");Debug.Log("simObject save path: "+sObjectsSavePath);
Directory.CreateDirectory(savePath);
Directory.CreateDirectory(perChunkSavePath);
Directory.CreateDirectory(sObjectsSavePath);
}
}
}
internal abstract class baseMultithreaded<T>where T:backgroundObject{
static bool Stop_v=false;internal static bool Stop{get{bool tmp;lock(Stop_Syn){tmp=Stop_v;      }return tmp;}
                                                   set{         lock(Stop_Syn){    Stop_v=value;}if(value){enqueued.Set();}}}static readonly object Stop_Syn=new object();
static readonly ConcurrentQueue<T>queued=new ConcurrentQueue<T>();static readonly AutoResetEvent enqueued=new AutoResetEvent(false);internal static void Schedule(T next){next.backgroundData.Reset();next.foregroundData.Set();queued.Enqueue(next);enqueued.Set();}internal static void Clear(){while(queued.TryDequeue(out T dequeued)){dequeued.foregroundData.WaitOne(0);dequeued.backgroundData.Set();}}
readonly Task task;internal baseMultithreaded(){task=Task.Factory.StartNew(BG,TaskCreationOptions.LongRunning);}internal void Wait(){try{task.Wait();Debug.Log("task completed successfully");}catch(Exception e){Debug.LogError(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);}}internal bool IsRunning(){return Stop==false&&task!=null&&!task.IsCompleted;}
void BG(){Thread.CurrentThread.IsBackground=false;Thread.CurrentThread.Priority=System.Threading.ThreadPriority.BelowNormal;
//Debug.Log("begin bg task");
AutoResetEvent foregroundData;ManualResetEvent backgroundData;
while(!Stop){enqueued.WaitOne();if(Stop){enqueued.Set();goto _Stop;}if(queued.TryDequeue(out T dequeued)){current=dequeued;foregroundData=current.foregroundData;backgroundData=current.backgroundData;Renew(dequeued);}else{continue;};if(queued.Count>0){enqueued.Set();}foregroundData.WaitOne();
try{
Execute();
}catch(Exception e){Debug.LogError(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);}
backgroundData.Set();Release();foregroundData=null;backgroundData=null;current=null;Cleanup();}_Stop:{}
//Debug.Log("bg task ended gracefully");
}
protected abstract void Execute();protected T current{get;private set;}protected abstract void Renew(T next);protected abstract void Release();protected abstract void Cleanup();
}
internal abstract class backgroundObject{
internal readonly AutoResetEvent foregroundData=new AutoResetEvent(false);internal readonly ManualResetEvent backgroundData=new ManualResetEvent(true);
}
#if UNITY_EDITOR
internal static void DrawBounds(Bounds b,Color color,float duration=0){//[https://gist.github.com/unitycoder/58f4b5d80f423d29e35c814a9556f9d9]
var p1=new Vector3(b.min.x,b.min.y,b.min.z);// bottom
var p2=new Vector3(b.max.x,b.min.y,b.min.z);
var p3=new Vector3(b.max.x,b.min.y,b.max.z);
var p4=new Vector3(b.min.x,b.min.y,b.max.z);
var p5=new Vector3(b.min.x,b.max.y,b.min.z);// top
var p6=new Vector3(b.max.x,b.max.y,b.min.z);
var p7=new Vector3(b.max.x,b.max.y,b.max.z);
var p8=new Vector3(b.min.x,b.max.y,b.max.z);
Debug.DrawLine(p1,p2,color,duration);
Debug.DrawLine(p2,p3,color,duration);
Debug.DrawLine(p3,p4,color,duration);
Debug.DrawLine(p4,p1,color,duration);
Debug.DrawLine(p5,p6,color,duration);
Debug.DrawLine(p6,p7,color,duration);
Debug.DrawLine(p7,p8,color,duration);
Debug.DrawLine(p8,p5,color,duration);
Debug.DrawLine(p1,p5,color,duration);// sides
Debug.DrawLine(p2,p6,color,duration);
Debug.DrawLine(p3,p7,color,duration);
Debug.DrawLine(p4,p8,color,duration);
}
#endif
}}