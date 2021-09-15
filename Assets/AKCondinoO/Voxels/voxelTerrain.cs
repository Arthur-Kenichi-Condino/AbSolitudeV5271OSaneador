using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UNET;
using UnityEngine;
using static AKCondinoO.Voxels.voxelTerrainChunk;
namespace AKCondinoO.Voxels{internal class voxelTerrain:MonoBehaviour{
[SerializeField]internal voxelTerrainChunk prefab;internal static readonly Dictionary<int,voxelTerrainChunk>active=new Dictionary<int,voxelTerrainChunk>();internal static readonly LinkedList<voxelTerrainChunk>pool=new LinkedList<voxelTerrainChunk>();int poolSize=0;
internal static Vector2Int instantiationDistance{get;}=new Vector2Int(4,4);
internal static Vector2Int expropriationDistance{get;}=new Vector2Int(5,5);
readonly marchingCubesMultithreaded[]marchingCubesThreads=new marchingCubesMultithreaded[Environment.ProcessorCount];
void OnDisable(){
marchingCubesMultithreaded.Stop=true;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i].Wait();}
}
void OnEnable(){
marchingCubesMultithreaded.Stop=false;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i]=new marchingCubesMultithreaded();}
}
void Update(){
if(NetworkManager.Singleton.IsServer){
int requiredPoolSize=NetworkManager.Singleton.GetComponent<UNetTransport>().MaxConnections*(expropriationDistance.x*2+1)*(expropriationDistance.y*2+1);
for(int i=poolSize;i<requiredPoolSize;poolSize=++i){
voxelTerrainChunk cnk=Instantiate(prefab);cnk.expropriated=pool.AddLast(cnk);cnk.network.Spawn();
}
}
}
}}