using System.Collections.Generic;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.Voxels.voxelTerrain;
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
}
Vector2Int cCoord;
Vector2Int cnkRgn;
internal int?cnkIdx=null;
internal void OncCoordChanged(Vector2Int cCoord1,int cnkIdx1){
cCoord=cCoord1;
cnkRgn=cCoordTocnkRgn(cCoord);localBounds.center=transform.position=new Vector3(cnkRgn.x,0,cnkRgn.y);
cnkIdx=cnkIdx1;
}
bool bake;bool baking;JobHandle bakingHandle;BakerJob bakeJob;struct BakerJob:IJob{public int meshId;public void Execute(){Physics.BakeMesh(meshId,false);}}
internal class marchingCubes:backgroundObject{
Vector2Int cCoord_bg;
Vector2Int cnkRgn_bg;
}
internal class marchingCubesMultithreaded:baseMultithreaded<marchingCubes>{
protected override void Renew(marchingCubes next){
}
protected override void Release(){
}
protected override void Cleanup(){
}
protected override void Execute(){
}
}
#if UNITY_EDITOR
void OnDrawGizmos(){
DrawBounds(localBounds,Color.white);
}
#endif
}}