using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static AKCondinoO.core;
using static AKCondinoO.Voxels.voxelTerrain;
using static AKCondinoO.Voxels.voxelTerrainChunk;
namespace AKCondinoO{internal class UNetPrefab:NetworkBehaviour{
void OnDisable(){
NavMesh.RemoveNavMeshData(navMesh);
bounds.Remove(this);
OnPlayerDisconnected(this);
}
internal Bounds worldBounds;
internal NavMeshDataInstance navMesh;internal NavMeshData navMeshData;internal static readonly NavMeshBuildSettings navMeshBuildSettings=new NavMeshBuildSettings{
agentTypeID=0,//  Humanoid agent
agentHeight=1.75f,
agentRadius=0.28125f,
agentClimb=0.75f,
agentSlope=60f,
overrideTileSize=true,
        tileSize=Width*Depth,
overrideVoxelSize=true,
        voxelSize=0.1406f,
minRegionArea=0.31640625f,
debug=new NavMeshBuildDebugSettings{
    flags=NavMeshBuildDebugFlags.None,
},
};
void Awake(){
worldBounds=new Bounds(Vector3.zero,new Vector3((instantiationDistance.x*2+1)*Width,Height,(instantiationDistance.y*2+1)*Depth));
var navMeshValidation=navMeshBuildSettings.ValidationReport(worldBounds);
foreach(var s in navMeshValidation){Debug.LogError(s);}
navMeshData=new NavMeshData(0){//  Humanoid agent
hideFlags=HideFlags.None,
};
navMesh=NavMesh.AddNavMeshData(navMeshData);
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
internal AsyncOperation BuildNavMesh(List<NavMeshBuildSource>sources){
return NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData,navMeshBuildSettings,sources,worldBounds);
}
#if UNITY_EDITOR
void OnDrawGizmos(){
DrawBounds(worldBounds,Color.blue);
}
#endif
}}