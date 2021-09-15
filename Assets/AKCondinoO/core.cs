using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace AKCondinoO{internal class core:MonoBehaviour{
internal abstract class baseMultithreaded<T>where T:backgroundObject{
static bool Stop_v=false;internal static bool Stop{get{bool tmp;lock(Stop_Syn){tmp=Stop_v;      }return tmp;}
                                                   set{         lock(Stop_Syn){    Stop_v=value;}if(value){enqueued.Set();}}}static readonly object Stop_Syn=new object();
static readonly ConcurrentQueue<T>queued=new ConcurrentQueue<T>();static readonly AutoResetEvent enqueued=new AutoResetEvent(false);internal static void Schedule(T state){queued.Enqueue(state);enqueued.Set();}
readonly Task task;internal baseMultithreaded(){task=Task.Factory.StartNew(BG,TaskCreationOptions.LongRunning);}internal void Wait(){try{task.Wait();}catch(Exception e){Debug.LogError(e?.Message+"\n"+e?.StackTrace+"\n"+e?.Source);}}
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
public static void DrawBounds(Bounds b,Color color,float duration=0){//[https://gist.github.com/unitycoder/58f4b5d80f423d29e35c814a9556f9d9]
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