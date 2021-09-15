using Unity.Netcode;
using UnityEngine;
using static AKCondinoO.core;
using static AKCondinoO.Voxels.voxelTerrain;
using static AKCondinoO.Voxels.voxelTerrainChunk;
namespace AKCondinoO{internal class UNetPrefab:NetworkBehaviour{
internal Bounds bounds;
void Awake(){
bounds=new Bounds(Vector3.zero,new Vector3((instantiationDistance.x*2+1)*Width,Height,(instantiationDistance.y*2+1)*Depth));
}
#if UNITY_EDITOR
void OnDrawGizmos(){
DrawBounds(bounds,Color.blue);
}
#endif
}}