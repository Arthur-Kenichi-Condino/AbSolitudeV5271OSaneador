using LibNoise;
using LibNoise.Generator;
using LibNoise.Operator;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UNET;
using UnityEngine;
using static AKCondinoO.Voxels.voxelTerrain.atlasHelper;
using static AKCondinoO.Voxels.voxelTerrainChunk;
using static AKCondinoO.Voxels.voxelTerrainChunk.marchingCubesMultithreaded;
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
marchingCubesMultithreaded.Stop=true;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i]?.Wait();}marchingCubesMultithreaded.Clear();
foreach(var cnk in all){cnk.Dispose();}
}
void OnDestroy(){Debug.Log("on destroy terrain");
foreach(var cnk in all){Debug.Log("destroy terrain chunk");
cnk.mC.backgroundData.Dispose();
cnk.mC.foregroundData.Dispose();
}
}
void OnEnable(){
biome.Seed=0;
GetAtlasData(prefab.GetComponent<MeshRenderer>().sharedMaterial);
foreach(var cnk in all){cnk.Prepare();}
marchingCubesMultithreaded.Stop=false;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i]=new marchingCubesMultithreaded();}
}
void Update(){
if(!NetworkManager.Singleton.IsServer
 &&!NetworkManager.Singleton.IsClient){
if(poolSize!=0){
marchingCubesMultithreaded.Stop=true;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i]?.Wait();}marchingCubesMultithreaded.Clear();
foreach(var cnk in all){cnk.Dispose();
cnk.mC.backgroundData.Dispose();
cnk.mC.foregroundData.Dispose();
DestroyImmediate(cnk);
}all.Clear();
active.Clear();
pool.Clear();
poolSize=0;
marchingCubesMultithreaded.Stop=false;for(int i=0;i<marchingCubesThreads.Length;++i){marchingCubesThreads[i]=new marchingCubesMultithreaded();}
}
}
if(NetworkManager.Singleton.IsServer){
if(poolSize==0){
int requiredPoolSize=NetworkManager.Singleton.GetComponent<UNetTransport>().MaxConnections*(expropriationDistance.x*2+1)*(expropriationDistance.y*2+1);
for(int i=poolSize;i<requiredPoolSize;poolSize=++i){
voxelTerrainChunk cnk;all.Add(cnk=Instantiate(prefab));cnk.expropriated=pool.AddLast(cnk);cnk.network.Spawn();
}
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
internal static void OnPlayerDisconnected(UNetPrefab player){Debug.Log("OnPlayerDisconnected:");
var pCoord=vecPosTocCoord(player.transform.position);
for(Vector2Int iCoord=new Vector2Int(),cCoord1=new Vector2Int();iCoord.y<=instantiationDistance.y;iCoord.y++){for(cCoord1.y=-iCoord.y+pCoord.y;cCoord1.y<=iCoord.y+pCoord.y;cCoord1.y+=iCoord.y*2){
for(           iCoord.x=0                                      ;iCoord.x<=instantiationDistance.x;iCoord.x++){for(cCoord1.x=-iCoord.x+pCoord.x;cCoord1.x<=iCoord.x+pCoord.x;cCoord1.x+=iCoord.x*2){
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
if(iCoord.x==0){break;}}}
if(iCoord.y==0){break;}}}
}
internal static readonly baseBiome biome=new baseBiome();
internal class baseBiome{
int seed_v;internal int Seed{get{return seed_v;}
set{seed_v=value;Debug.Log("seed set: "+seed_v);
random[0]=new System.Random(seed_v);
random[1]=new System.Random(random[0].Next());
SetModules();
}
}
protected readonly System.Random[]random=new System.Random[2];
protected virtual int rndIdx{get{return 1;}}
protected virtual int hgtIdx{get{return 5;}}//  Base Height Result Module
readonly protected Select[]selectors=new Select[1];
protected readonly List<ModuleBase>modules=new List<ModuleBase>();
protected virtual void SetModules(){
modules.Add(new Const( 0));
modules.Add(new Const( 1));
modules.Add(new Const(-1));
modules.Add(new Const(.5));
modules.Add(new Const(128));
ModuleBase module1=new Const(5);
// 2
ModuleBase module2a=new RidgedMultifractal(frequency:Mathf.Pow(2,-8),lacunarity:2.0,octaves:6,seed:random[rndIdx].Next(),quality:QualityMode.Low);
ModuleBase module2b=new Turbulence(input:module2a); 
((Turbulence)module2b).Seed=random[rndIdx].Next();
((Turbulence)module2b).Frequency=Mathf.Pow(2,-2);
((Turbulence)module2b).Power=1;
ModuleBase module2c=new ScaleBias(scale:1.0,bias:30.0,input:module2b);  
// 3
ModuleBase module3a=new Billow(frequency:Mathf.Pow(2,-7)*1.6,lacunarity:2.0,persistence:0.5,octaves:8,seed:random[rndIdx].Next(),quality:QualityMode.Low);
ModuleBase module3b=new Turbulence(input:module3a);
((Turbulence)module3b).Seed=random[rndIdx].Next();
((Turbulence)module3b).Frequency=Mathf.Pow(2,-2);  
((Turbulence)module3b).Power=1.8;
ModuleBase module3c=new ScaleBias(scale:1.0,bias:31.0,input:module3b);
// 4
ModuleBase module4a=new Perlin(frequency:Mathf.Pow(2,-6),lacunarity:2.0,persistence:0.5,octaves:6,seed:random[rndIdx].Next(),quality:QualityMode.Low);
ModuleBase module4b=new Select(inputA:module2c,inputB:module3c,controller:module4a);
((Select)module4b).SetBounds(min:-.2,max:.2);
((Select)module4b).FallOff=.25;
ModuleBase module4c=new Multiply(lhs:module4b,rhs:module1);
modules.Add(module4c);
selectors[0]=(Select)module4b;
}
internal virtual int CacheCount{get{return 1;}}
protected Vector3 deround{get;}=new Vector3(.5f,.5f,.5f);
internal void Setvxl(Vector3Int noiseInputRounded,double[][][]nCache,materialId[][][]mCache,int oftIdx,int noiseIndex,ref voxel vxl){if(nCache!=null&&nCache[0][oftIdx]==null)nCache[0][oftIdx]=new double[FlattenOffset];if(mCache!=null&&mCache[0][oftIdx]==null)mCache[0][oftIdx]=new materialId[FlattenOffset];
             Vector3 noiseInput=noiseInputRounded+deround;
double noiseValue=(nCache!=null&&nCache[0][oftIdx][noiseIndex]!=0)?nCache[0][oftIdx][noiseIndex]:(nCache!=null?(nCache[0][oftIdx][noiseIndex]=Get()):Get());double Get(){return modules[hgtIdx].GetValue(noiseInput.z,noiseInput.x,0);}
if(noiseInput.y<=noiseValue){
double d;vxl=new voxel(d=density(100,noiseInput,noiseValue),Vector3.zero,material(d,noiseInput,mCache,oftIdx,noiseIndex));return;
}
vxl=voxel.Air;}
protected virtual double density(double density,Vector3 noiseInput,double noiseValue,float smoothing=3f){double value=density;
double delta=(noiseValue-noiseInput.y);//  noiseInput.y sempre será menor ou igual a noiseValue
if(delta<=smoothing){
double smoothingValue=(smoothing-delta)/smoothing;
value*=1d-smoothingValue;
if(value<0)
   value=0;
else if(value>100)
        value=100;
}
return value;}
protected virtual int ground(Vector3 noiseInput){
double min=selectors[0].Minimum;
double max=selectors[0].Maximum;
double fallOff=selectors[0].FallOff*.5;
var selectValue=selectors[0].Controller.GetValue(noiseInput.z,noiseInput.x,0);
if(selectValue<=min-fallOff||selectValue>=max+fallOff){
return 1;
}else{
return 0;
}
}
readonly protected materialId[]materialIdPicking=new materialId[2]{
materialId.Rock,
materialId.Dirt,
};
protected virtual materialId material(double density,Vector3 noiseInput,materialId[][][]mCache,int oftIdx,int noiseIndex){if(-density>=IsoLevel){return materialId.Air;}materialId m;
if(mCache!=null&&mCache[0][oftIdx][noiseIndex]!=0){return mCache[0][oftIdx][noiseIndex];}
m=materialIdPicking[ground(noiseInput)];
return mCache!=null?mCache[0][oftIdx][noiseIndex]=m:m;}
}
internal static class atlasHelper{
internal static Material material{get;private set;}
internal static void GetAtlasData(Material material){atlasHelper.material=material;
uv[(int)materialId.Dirt]=new Vector2(1,0);
uv[(int)materialId.Rock]=new Vector2(0,0);
}
internal static readonly Vector2[]uv=new Vector2[Enum.GetNames(typeof(materialId)).Length];
internal enum materialId:ushort{
Air=0,//  Default value
Bedrock=1,//  Indestrutível
Dirt=2,
Rock=3,
Sand=4,
}
}
}}