using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.Voxels.voxelTerrain;
using static AKCondinoO.Voxels.voxelTerrainChunk.marchingCubes;
namespace AKCondinoO.Voxels{internal class voxelTerrainChunk:NetworkBehaviour{
internal const ushort Height=(256);
internal const ushort Width=(16);
internal const ushort Depth=(16);
internal const ushort FlattenOffset=(Width*Depth);
internal const int VoxelsPerChunk=(FlattenOffset*Height);
internal LinkedListNode<voxelTerrainChunk>expropriated;
internal NetworkObject network;
internal Bounds localBounds;
internal Mesh mesh;
void Awake(){
//Debug.Log("ready components",this);
network=GetComponent<NetworkObject>();
mesh=new Mesh(){bounds=localBounds=new Bounds(Vector3.zero,new Vector3(Width,Height,Depth))};GetComponent<MeshFilter>().mesh=mesh;
Prepare();
}
internal void Prepare(){Debug.Log("Prepare");
mC.Assign(toChunk:this);
}
internal void Dispose(){Debug.Log("Dispose");
mC.OnStop();
}
bool meshDirty;
void Update(){
if(NetworkManager.Singleton.IsServer){
if(meshDirty){
if(mC.backgroundData.WaitOne(0)){
meshDirty=false;
mC.cCoord_bg=cCoord;
mC.cnkRgn_bg=cnkRgn;
marchingCubesMultithreaded.Schedule(mC);
}
}
}
}
Vector2Int cCoord;
Vector2Int cnkRgn;
internal int?cnkIdx=null;
internal void OncCoordChanged(Vector2Int cCoord1,int cnkIdx1){
cCoord=cCoord1;
cnkRgn=cCoordTocnkRgn(cCoord);localBounds.center=transform.position=new Vector3(cnkRgn.x,0,cnkRgn.y);
cnkIdx=cnkIdx1;
meshDirty=true;
}
bool bake;bool baking;JobHandle bakingHandle;BakerJob bakeJob;struct BakerJob:IJob{public int meshId;public void Execute(){Physics.BakeMesh(meshId,false);}}
internal readonly marchingCubes mC=new marchingCubes();
internal class marchingCubes:backgroundObject{
internal NativeList<Vertex>TempVer;[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]public struct Vertex{
public Vector3 pos;
public Vector3 normal;
public Color color;
public Vector2 texCoord0;
public Vector2 texCoord1;
public Vector2 texCoord2;
public Vector2 texCoord3;
                        public Vertex(Vector3 p,Vector3 n,Vector2 uv0){
pos=p;
normal=n;
color=new Color(1f,0f,0f,0f);
texCoord0=uv0;
texCoord1=new Vector2(-1f,-1f);
texCoord2=new Vector2(-1f,-1f);
texCoord3=new Vector2(-1f,-1f);
                        }
}
internal NativeList<UInt32>TempTri;
internal void Assign(voxelTerrainChunk toChunk){
TempVer=new NativeList<Vertex>(Allocator.Persistent);
TempTri=new NativeList<UInt32>(Allocator.Persistent);
}
internal void OnStop(){
TempVer.Dispose();
TempTri.Dispose();
}
internal Vector2Int cCoord_bg;
internal Vector2Int cnkRgn_bg;
}
internal class marchingCubesMultithreaded:baseMultithreaded<marchingCubes>{
NativeList<Vertex>TempVer;
NativeList<UInt32>TempTri;
protected override void Renew(marchingCubes next){
TempVer=next.TempVer;
TempTri=next.TempTri;
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){//Debug.Log("Execute");
}
}
#if UNITY_EDITOR
void OnDrawGizmos(){
DrawBounds(localBounds,Color.white);
}
#endif
}}