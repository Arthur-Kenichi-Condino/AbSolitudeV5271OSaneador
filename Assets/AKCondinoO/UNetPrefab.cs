using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.Voxels.voxelTerrain;
using static AKCondinoO.Voxels.voxelTerrainChunk;
namespace AKCondinoO{internal class UNetPrefab:NetworkBehaviour{
void OnDisable(){
bounds.Remove(this);
OnPlayerDisconnected(this);
}
internal Bounds worldBounds;
void Awake(){
worldBounds=new Bounds(Vector3.zero,new Vector3((instantiationDistance.x*2+1)*Width,Height,(instantiationDistance.y*2+1)*Depth));
}
bool firstLoop=true;
Vector3 pos,pos_Pre;internal Vector2Int cCoord,cCoord_Pre;Vector2Int cnkRgn;
void Update(){
if(NetworkManager.Singleton.IsServer){
bounds[this]=null;
pos=transform.position;
if(firstLoop||pos!=pos_Pre){
cCoord_Pre=cCoord;
if(firstLoop|cCoord!=(cCoord=vecPosTocCoord(pos))){
cnkRgn=cCoordTocnkRgn(cCoord);
worldBounds.center=new Vector3(cnkRgn.x,0,cnkRgn.y);//Debug.Log("new player bounds center at "+worldBounds.center,this);
if(firstLoop){cCoord_Pre=cCoord;}
bounds[this]=(cCoord,cCoord_Pre);
}
pos_Pre=pos;}
firstLoop=false;
}
}
#if UNITY_EDITOR
void OnDrawGizmos(){
DrawBounds(worldBounds,Color.blue);
}
#endif
}}