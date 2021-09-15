using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UNET;
using UnityEngine;
using static AKCondinoO.Voxels.voxelTerrainChunk;
namespace AKCondinoO.Voxels{internal class voxelTerrain:MonoBehaviour{
[NonSerialized]internal const double IsoLevel=-50.0d;
internal const int MaxcCoordx=6250;
internal const int MaxcCoordy=6250;
internal static Vector2Int instantiationDistance{get;}=new Vector2Int(5,5);
internal static Vector2Int expropriationDistance{get;}=new Vector2Int(6,6);
internal static Vector2Int vecPosTocCoord(Vector3 pos){
                                                pos.x/=(float)Width;
                                                pos.z/=(float)Depth;
return new Vector2Int((pos.x>0)?(pos.x-(int)pos.x==0.5f?Mathf.FloorToInt(pos.x):Mathf.RoundToInt(pos.x)):(int)Math.Round(pos.x,MidpointRounding.AwayFromZero),
                      (pos.z>0)?(pos.z-(int)pos.z==0.5f?Mathf.FloorToInt(pos.z):Mathf.RoundToInt(pos.z)):(int)Math.Round(pos.z,MidpointRounding.AwayFromZero));
}
internal static Vector2Int vecPosTocnkRgn(Vector3 pos){Vector2Int coord=vecPosTocCoord(pos);
return new Vector2Int(coord.x*Width,coord.y*Depth);
}
internal static Vector2Int cnkRgnTocCoord(Vector2Int cnkRgn){return new Vector2Int(cnkRgn.x/Width,cnkRgn.y/Depth);}
internal static Vector2Int cCoordTocnkRgn(Vector2Int cCoord){return new Vector2Int(cCoord.x*Width,cCoord.y*Depth);}
internal static int GetcnkIdx(int cx,int cy){return cy+cx*(MaxcCoordy+1);}
internal static Vector3Int vecPosTovCoord(Vector3 pos){
Vector2Int rgn=vecPosTocnkRgn(pos);
pos.x=(pos.x>0)?(pos.x-(int)pos.x==0.5f?Mathf.FloorToInt(pos.x):Mathf.RoundToInt(pos.x)):(int)Math.Round(pos.x,MidpointRounding.AwayFromZero);
pos.y=(pos.y>0)?(pos.y-(int)pos.y==0.5f?Mathf.FloorToInt(pos.y):Mathf.RoundToInt(pos.y)):(int)Math.Round(pos.y,MidpointRounding.AwayFromZero);
pos.z=(pos.z>0)?(pos.z-(int)pos.z==0.5f?Mathf.FloorToInt(pos.z):Mathf.RoundToInt(pos.z)):(int)Math.Round(pos.z,MidpointRounding.AwayFromZero);
Vector3Int coord=new Vector3Int((int)pos.x-rgn.x,(int)pos.y,(int)pos.z-rgn.y);
coord.x+=Mathf.FloorToInt(Width /2.0f);coord.x=Mathf.Clamp(coord.x,0,Width -1);
coord.y+=Mathf.FloorToInt(Height/2.0f);coord.y=Mathf.Clamp(coord.y,0,Height-1);
coord.z+=Mathf.FloorToInt(Depth /2.0f);coord.z=Mathf.Clamp(coord.z,0,Depth -1);
return coord;}
internal static int GetvxlIdx(int vcx,int vcy,int vcz){return vcy*FlattenOffset+vcx*Depth+vcz;}
internal static void ValidateCoord(ref Vector2Int region,ref Vector3Int vxlCoord){int a,c;
a=region.x;c=vxlCoord.x;ValidateCoordAxis(ref a,ref c,Width);region.x=a;vxlCoord.x=c;
a=region.y;c=vxlCoord.z;ValidateCoordAxis(ref a,ref c,Depth);region.y=a;vxlCoord.z=c;
}
internal static void ValidateCoordAxis(ref int axis,ref int coord,int axisLength){
      if(coord<0){          axis-=axisLength*Mathf.CeilToInt (Math.Abs(coord)/(float)axisLength);coord=(coord%axisLength)+axisLength;
}else if(coord>=axisLength){axis+=axisLength*Mathf.FloorToInt(Math.Abs(coord)/(float)axisLength);coord=(coord%axisLength);}
}
internal static readonly Dictionary<UNetPrefab,(Vector2Int cCoord,Vector2Int cCoord_Pre)?>bounds=new Dictionary<UNetPrefab,(Vector2Int,Vector2Int)?>();
[SerializeField]internal voxelTerrainChunk prefab;internal static readonly Dictionary<int,voxelTerrainChunk>active=new Dictionary<int,voxelTerrainChunk>();internal static readonly List<voxelTerrainChunk>all=new List<voxelTerrainChunk>();internal static readonly LinkedList<voxelTerrainChunk>pool=new LinkedList<voxelTerrainChunk>();int poolSize=0;
readonly marchingCubesMultithreaded[]marchingCubesThreads=new marchingCubesMultithreaded[Environment.ProcessorCount];
void OnDisable(){
marchingCubesMultithreaded.Stop=true;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i].Wait();}
foreach(var cnk in all){cnk.Dispose();}
}
void OnEnable(){
foreach(var cnk in all){cnk.Prepare();}
marchingCubesMultithreaded.Stop=false;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i]=new marchingCubesMultithreaded();}
}
void Update(){
if(NetworkManager.Singleton.IsServer){
int requiredPoolSize=NetworkManager.Singleton.GetComponent<UNetTransport>().MaxConnections*(expropriationDistance.x*2+1)*(expropriationDistance.y*2+1);
for(int i=poolSize;i<requiredPoolSize;poolSize=++i){
voxelTerrainChunk cnk;all.Add(cnk=Instantiate(prefab));cnk.expropriated=pool.AddLast(cnk);cnk.network.Spawn();
}
foreach(var movement in bounds){if(movement.Value==null)continue;var moved=movement.Value;Vector2Int pCoord_Pre=moved.Value.cCoord_Pre;Vector2Int pCoord=moved.Value.cCoord;
for(Vector2Int eCoord=new Vector2Int(),cCoord1=new Vector2Int();eCoord.y<=expropriationDistance.y;eCoord.y++){for(cCoord1.y=-eCoord.y+pCoord_Pre.y;cCoord1.y<=eCoord.y+pCoord_Pre.y;cCoord1.y+=eCoord.y*2){
for(           eCoord.x=0                                      ;eCoord.x<=expropriationDistance.x;eCoord.x++){for(cCoord1.x=-eCoord.x+pCoord_Pre.x;cCoord1.x<=eCoord.x+pCoord_Pre.x;cCoord1.x+=eCoord.x*2){
if(Math.Abs(cCoord1.x)>=MaxcCoordx||
   Math.Abs(cCoord1.y)>=MaxcCoordy){//Debug.Log("do not try to expropriate at out of world cCoord:.."+cCoord1);
goto _skip;
}
//Debug.Log("try to expropriate chunk at:.."+cCoord1);
if(bounds.All(b=>{return(Mathf.Abs(cCoord1.x-b.Key.cCoord.x)>instantiationDistance.x||
                         Mathf.Abs(cCoord1.y-b.Key.cCoord.y)>instantiationDistance.y);})){/*Debug.Log("expropriation needed for chunk at:.."+cCoord1);*/int cnkIdx1=GetcnkIdx(cCoord1.x,cCoord1.y);if(active.TryGetValue(cnkIdx1,out voxelTerrainChunk cnk)){/*Debug.Log("do expropriate chunk of index:.."+cnkIdx1);*/if(cnk.expropriated==null){cnk.expropriated=pool.AddLast(cnk);/*Debug.Log("expropriated chunk of index:.."+cnkIdx1);*/
}else{//Debug.Log("but it is already expropriated, the chunk of index:.."+cnkIdx1);
}
}else{//Debug.Log("no chunk to expropriate of index:.."+cnkIdx1);
}
}else{//Debug.Log("no need to expropriate chunk at:.."+cCoord1);
}
_skip:{}
if(eCoord.x==0){break;}}}
if(eCoord.y==0){break;}}}
for(Vector2Int iCoord=new Vector2Int(),cCoord1=new Vector2Int();iCoord.y<=instantiationDistance.y;iCoord.y++){for(cCoord1.y=-iCoord.y+pCoord.y;cCoord1.y<=iCoord.y+pCoord.y;cCoord1.y+=iCoord.y*2){
for(           iCoord.x=0                                      ;iCoord.x<=instantiationDistance.x;iCoord.x++){for(cCoord1.x=-iCoord.x+pCoord.x;cCoord1.x<=iCoord.x+pCoord.x;cCoord1.x+=iCoord.x*2){
if(Math.Abs(cCoord1.x)>=MaxcCoordx||
   Math.Abs(cCoord1.y)>=MaxcCoordy){//Debug.Log("do not try to activate a chunk at out of world cCoord:.."+cCoord1);
goto _skip;
}
//Debug.Log("try to activate chunk at:.."+cCoord1);
int cnkIdx1=GetcnkIdx(cCoord1.x,cCoord1.y);if(!active.TryGetValue(cnkIdx1,out voxelTerrainChunk cnk)){/*Debug.Log("do activate a chunk for index:.."+cnkIdx1+";[current chunk pool.Count:.."+pool.Count);*/cnk=pool.First.Value;pool.RemoveFirst();cnk.expropriated=(null);if(cnk.cnkIdx!=null&&active.ContainsKey(cnk.cnkIdx.Value)){active.Remove(cnk.cnkIdx.Value);}active.Add(cnkIdx1,cnk);cnk.OncCoordChanged(cCoord1,cnkIdx1);
}else{//Debug.Log("but it is already active, the chunk of index:.."+cnkIdx1);
if(cnk.expropriated!=null){pool.Remove(cnk.expropriated);cnk.expropriated=(null);/*Debug.Log("but it was expropriated, the chunk of index:.."+cnkIdx1);*/}
}
_skip:{}
if(iCoord.x==0){break;}}}
if(iCoord.y==0){break;}}}
}
}
}
}}