using paulbourke.MarchingCubes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using static AKCondinoO.core;
using static AKCondinoO.Voxels.voxelTerrain;
using static AKCondinoO.Voxels.voxelTerrain.atlasHelper;
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
MeshUpdateFlags meshFlags=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontRecalculateBounds;static readonly VertexAttributeDescriptor[]layout=new[]{
new VertexAttributeDescriptor(VertexAttribute.Position ,VertexAttributeFormat.Float32,3),
new VertexAttributeDescriptor(VertexAttribute.Normal   ,VertexAttributeFormat.Float32,3),
new VertexAttributeDescriptor(VertexAttribute.Color    ,VertexAttributeFormat.Float32,4),
new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),
new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,2),
new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,2),
new VertexAttributeDescriptor(VertexAttribute.TexCoord3,VertexAttributeFormat.Float32,2),
};
internal new MeshRenderer renderer;
internal new MeshCollider collider;
void Awake(){
//Debug.Log("ready components",this);
network=GetComponent<NetworkObject>();
mesh=new Mesh(){bounds=localBounds=new Bounds(Vector3.zero,new Vector3(Width,Height,Depth))};GetComponent<MeshFilter>().mesh=mesh;
bakeJob=new BakerJob(){meshId=mesh.GetInstanceID(),};
renderer=GetComponent<MeshRenderer>();
collider=GetComponent<MeshCollider>();
Prepare();
}
internal bool isPrepared;
internal void Prepare(){//Debug.Log("Prepare");
if(!isPrepared){
isPrepared=true;
mC.Assign(toChunk:this);
}
}
internal void Dispose(){//Debug.Log("Dispose");
if(isPrepared){
isPrepared=false;
bakingHandle.Complete();
mC.OnStop();/*  :mC has invalid data now:  */if(meshDirty){cnkIdxChanged=true;marchingCubesRunning=false;baking=false;}
}
}
internal bool meshDirty;
bool marchingCubesRunning;
NativeList<vertex>TempVer{get{return mC.TempVer;}}
NativeList<UInt32>TempTri{get{return mC.TempTri;}}
void Update(){
if(NetworkManager.Singleton.IsServer){
if(isPrepared){
if(baking){
if(bakingHandle.IsCompleted){bakingHandle.Complete();//Debug.Log("bakingHandle.IsCompleted");
baking=false;
if(!cnkIdxChanged){//Debug.Log("mesh is built now");
renderer.enabled=true;
collider.enabled=true;
collider.sharedMesh=null;
collider.sharedMesh=mesh;
meshDirty=false;
}
}
}else if(marchingCubesRunning){
if(mC.backgroundData.WaitOne(0)){//Debug.Log("marchingCubesMultithreaded done");
marchingCubesRunning=false;
bool resize;if(resize=TempVer.Length>mesh.vertexCount){mesh.SetVertexBufferParams(TempVer.Length,layout);}mesh.SetVertexBufferData(TempVer.AsArray(),0,0,TempVer.Length,0,meshFlags);if(resize){mesh.SetIndexBufferParams(TempTri.Length,IndexFormat.UInt32);}mesh.SetIndexBufferData(TempTri.AsArray(),0,0,TempTri.Length,meshFlags);mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,TempTri.Length){firstVertex=0,vertexCount=TempVer.Length},meshFlags); 
baking=true;
bakingHandle=bakeJob.Schedule();
}
}else if(meshDirty){
if(mC.backgroundData.WaitOne(0)){
cnkIdxChanged=false;
mC.cCoord_bg=cCoord;
mC.cnkRgn_bg=cnkRgn;
marchingCubesRunning=true;
marchingCubesMultithreaded.Schedule(mC);
}
}
}
}
}
Vector2Int cCoord;
Vector2Int cnkRgn;
internal int?cnkIdx=null;bool cnkIdxChanged;
internal void OncCoordChanged(Vector2Int cCoord1,int cnkIdx1){
cCoord=cCoord1;
cnkRgn=cCoordTocnkRgn(cCoord);localBounds.center=transform.position=new Vector3(cnkRgn.x,0,cnkRgn.y);
cnkIdx=cnkIdx1;cnkIdxChanged=true;
meshDirty=true;
}
bool baking;JobHandle bakingHandle;BakerJob bakeJob;struct BakerJob:IJob{public int meshId;public void Execute(){Physics.BakeMesh(meshId,false);}}
internal readonly marchingCubes mC=new marchingCubes();
internal class marchingCubes:backgroundObject{
internal NativeList<vertex>TempVer;[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]internal struct vertex{
internal Vector3 pos;
internal Vector3 normal;
internal Color color;
internal Vector2 texCoord0;
internal Vector2 texCoord1;
internal Vector2 texCoord2;
internal Vector2 texCoord3;
                        internal vertex(Vector3 p,Vector3 n,Vector2 uv0){
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
TempVer=new NativeList<vertex>(Allocator.Persistent);
TempTri=new NativeList<UInt32>(Allocator.Persistent);
}
internal void OnStop(){
if(TempVer.IsCreated)TempVer.Dispose();
if(TempTri.IsCreated)TempTri.Dispose();
}
internal Vector2Int cCoord_bg;
internal Vector2Int cnkRgn_bg;
}
internal class marchingCubesMultithreaded:baseMultithreaded<marchingCubes>{
static Vector3 trianglePosAdj{get;}=new Vector3((Width/2.0f)-0.5f,(Height/2.0f)-0.5f,(Depth/2.0f)-0.5f);static Vector2 emptyUV{get;}=new Vector2(-1,-1);
static readonly ReadOnlyCollection<Vector3>corners=new ReadOnlyCollection<Vector3>(new Vector3[8]{
new Vector3(-.5f,-.5f,-.5f),
new Vector3( .5f,-.5f,-.5f),
new Vector3( .5f, .5f,-.5f),
new Vector3(-.5f, .5f,-.5f),
new Vector3(-.5f,-.5f, .5f),
new Vector3( .5f,-.5f, .5f),
new Vector3( .5f, .5f, .5f),
new Vector3(-.5f, .5f, .5f),
});
internal marchingCubesMultithreaded(){
for(int i=0;i<neighbors.Length;++i){neighbors[i]=new Dictionary<int,voxel>();}
for(int i=0;i<biome.CacheCount;++i){
nCache[i]=new     double[9][];
mCache[i]=new materialId[9][];
}
for(int i=0;i<voxelsBuffer1[2].Length;++i){voxelsBuffer1[2][i]=new voxel[4];if(i<voxelsBuffer1[1].Length){voxelsBuffer1[1][i]=new voxel[4];}}
for(int i=0;i<verticesBuffer[2].Length;++i){verticesBuffer[2][i]=new Vector3[4];if(i<verticesBuffer[1].Length){verticesBuffer[1][i]=new Vector3[4];}}
}
NativeList<vertex>TempVer;
NativeList<UInt32>TempTri;
Vector2Int cCoord1{get{return current.cCoord_bg;}}
Vector2Int cnkRgn1{get{return current.cnkRgn_bg;}}
protected override void Renew(marchingCubes next){
TempVer=next.TempVer;
TempTri=next.TempTri;
}
protected override void Release(){
}
protected override void Cleanup(){
Array.Clear(voxels,0,voxels.Length);for(int i=0;i<neighbors.Length;++i){neighbors[i].Clear();}
for(int i=0;i<biome.CacheCount;++i){
for(int j=0;j<nCache[i].Length;++j){if(nCache[i][j]!=null)Array.Clear(nCache[i][j],0,nCache[i][j].Length);}
for(int j=0;j<mCache[i].Length;++j){if(mCache[i][j]!=null)Array.Clear(mCache[i][j],0,mCache[i][j].Length);}
}
for(int i=0;i<voxelsBuffer1[0].Length;++i){Array.Clear(voxelsBuffer1[0][i],0,voxelsBuffer1[0][i].Length);}
for(int i=0;i<voxelsBuffer1[1].Length;++i){Array.Clear(voxelsBuffer1[1][i],0,voxelsBuffer1[1][i].Length);}
for(int i=0;i<voxelsBuffer1[2].Length;++i){Array.Clear(voxelsBuffer1[2][i],0,voxelsBuffer1[2][i].Length);}
for(int i=0;i<voxelsBuffer2.Length;++i){if(voxelsBuffer2[i]!=null)Array.Clear(voxelsBuffer2[i],0,voxelsBuffer2[i].Length);}
for(int i=0;i<verticesBuffer[0].Length;++i){Array.Clear(verticesBuffer[0][i],0,verticesBuffer[0][i].Length);}
for(int i=0;i<verticesBuffer[1].Length;++i){Array.Clear(verticesBuffer[1][i],0,verticesBuffer[1][i].Length);}
for(int i=0;i<verticesBuffer[2].Length;++i){Array.Clear(verticesBuffer[2][i],0,verticesBuffer[2][i].Length);}
vertexUV.Clear();
}
readonly voxel[]voxels=new voxel[VoxelsPerChunk];readonly Dictionary<int,voxel>[]neighbors=new Dictionary<int,voxel>[8];internal struct voxel{
       internal voxel(double d,Vector3 n,materialId m){
Density=d;Normal=n;Material=m;IsCreated=true;
       }
internal double Density;internal Vector3 Normal;internal materialId Material;internal bool IsCreated;
internal static voxel Air    {get;}=new voxel(  0.0,Vector3.zero,materialId.Air    );
internal static voxel Bedrock{get;}=new voxel(101.0,Vector3.zero,materialId.Bedrock);
}
readonly double[][][]nCache=new double[biome.CacheCount][][];readonly materialId[][][]mCache=new materialId[biome.CacheCount][][];
readonly voxel[][][]voxelsBuffer1=new voxel[3][][]{new voxel[1][]{new voxel[4],},new voxel[Depth][],new voxel[FlattenOffset][],};
  readonly voxel[][]voxelsBuffer2=new voxel[3][]{new voxel[1],new voxel[Depth],new voxel[FlattenOffset],};
readonly voxel[]polygonCell=new voxel[8];readonly voxel[]tmpvxl=new voxel[6];
readonly materialId[]materials=new materialId[12];
   readonly Vector3[] vertices=new Vector3[12];[NonSerialized]readonly Vector3[][][]verticesBuffer=new Vector3[3][][]{new Vector3[1][]{new Vector3[4],},new Vector3[Depth][],new Vector3[FlattenOffset][],};
   readonly Vector3[]  normals=new Vector3[12];
readonly double[]density=new double[2];readonly Vector3[]vertex=new Vector3[2];readonly materialId[]material=new materialId[2];readonly float[]distance=new float[2];
readonly int[]idx=new int[3];readonly Vector3[]verPos=new Vector3[3];
readonly Dictionary<Vector3,List<Vector2>>vertexUV=new Dictionary<Vector3,List<Vector2>>();readonly Dictionary<int,int>weights=new Dictionary<int,int>(4);
protected override void Execute(){//Debug.Log("Execute");
TempVer.Clear();UInt32 vertexCount=0;
TempTri.Clear();
Vector2Int posOffset=Vector2Int.zero;
Vector2Int crdOffset=Vector2Int.zero;
Vector3Int vCoord1;
for(vCoord1=new Vector3Int();vCoord1.y<Height;vCoord1.y++){
for(vCoord1.x=0             ;vCoord1.x<Width ;vCoord1.x++){
for(vCoord1.z=0             ;vCoord1.z<Depth ;vCoord1.z++){
int corner=0;Vector3Int vCoord2=vCoord1;                        if(vCoord1.z>0)polygonCell[corner]=voxelsBuffer1[0][0][0];else if(vCoord1.x>0)polygonCell[corner]=voxelsBuffer1[1][vCoord1.z][0];else if(vCoord1.y>0)polygonCell[corner]=voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][0];else SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;                          if(vCoord1.z>0)polygonCell[corner]=voxelsBuffer1[0][0][1];                                                                       else if(vCoord1.y>0)polygonCell[corner]=voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][1];else SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;vCoord2.y+=1;             if(vCoord1.z>0)polygonCell[corner]=voxelsBuffer1[0][0][2];                                                                                                                                                              else SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;             vCoord2.y+=1;             if(vCoord1.z>0)polygonCell[corner]=voxelsBuffer1[0][0][3];else if(vCoord1.x>0)polygonCell[corner]=voxelsBuffer1[1][vCoord1.z][1];                                                                                       else SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;                          vCoord2.z+=1;                                                               if(vCoord1.x>0)polygonCell[corner]=voxelsBuffer1[1][vCoord1.z][2];else if(vCoord1.y>0)polygonCell[corner]=voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][2];else SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;             vCoord2.z+=1;                                                                                                                                      if(vCoord1.y>0)polygonCell[corner]=voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][3];else SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;vCoord2.y+=1;vCoord2.z+=1;                                                                                                                                                                                                                             SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;             vCoord2.y+=1;vCoord2.z+=1;                                                               if(vCoord1.x>0)polygonCell[corner]=voxelsBuffer1[1][vCoord1.z][3];                                                                                       else SetpolygonCellVoxel();
voxelsBuffer1[0][0][0]=polygonCell[4];
voxelsBuffer1[0][0][1]=polygonCell[5];
voxelsBuffer1[0][0][2]=polygonCell[6];
voxelsBuffer1[0][0][3]=polygonCell[7];
voxelsBuffer1[1][vCoord1.z][0]=polygonCell[1];
voxelsBuffer1[1][vCoord1.z][1]=polygonCell[2];
voxelsBuffer1[1][vCoord1.z][2]=polygonCell[5];
voxelsBuffer1[1][vCoord1.z][3]=polygonCell[6];
voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][0]=polygonCell[3];
voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][1]=polygonCell[2];
voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][2]=polygonCell[7];
voxelsBuffer1[2][vCoord1.z+vCoord1.x*Depth][3]=polygonCell[6];
void SetpolygonCellVoxel(){
if(vCoord2.y<=0){polygonCell[corner]=voxel.Bedrock;/*  :fora do mundo, baixo  */}else if(vCoord2.y>=Height){polygonCell[corner]=voxel.Air;/*  :fora do mundo, cima  */}else{
Vector2Int cnkRgn2=cnkRgn1;
Vector2Int cCoord2=cCoord1;
if(vCoord2.x<0||vCoord2.x>=Width||
   vCoord2.z<0||vCoord2.z>=Depth){ValidateCoord(ref cnkRgn2,ref vCoord2);cCoord2=cnkRgnTocCoord(cnkRgn2);}
       int vxlIdx2=GetvxlIdx(vCoord2.x,vCoord2.y,vCoord2.z); 
int oftIdx2=GetoftIdx(cCoord2-cCoord1);/*  já construído:  */if(oftIdx2==0&&voxels[vxlIdx2].IsCreated){polygonCell[corner]=voxels[vxlIdx2];}else if(oftIdx2>0&&neighbors[oftIdx2-1].ContainsKey(vxlIdx2)){polygonCell[corner]=neighbors[oftIdx2-1][vxlIdx2];}else{
//  pegar valor do bioma:
Vector3Int noiseInput=vCoord2;noiseInput.x+=cnkRgn2.x;
                              noiseInput.z+=cnkRgn2.y;
biome.Setvxl(noiseInput,nCache,mCache,oftIdx2,vCoord2.z+vCoord2.x*Depth,ref polygonCell[corner]);
}
if(polygonCell[corner].Material!=materialId.Air&&polygonCell[corner].Normal==Vector3.zero){
//  calcular normal:
int tmpIdx=0;Vector3Int vCoord3=vCoord2;vCoord3.x++;                                                                                                                                                                SetpolygonCellNormalSettmpvxl();
tmpIdx++;vCoord3=vCoord2;               vCoord3.x--;if(vCoord2.z>1&&vCoord2.x>1&&vCoord2.y>1&&voxelsBuffer2[1][vCoord2.z].IsCreated)                tmpvxl[tmpIdx]=voxelsBuffer2[1][vCoord2.z];                else SetpolygonCellNormalSettmpvxl();
tmpIdx++;vCoord3=vCoord2;               vCoord3.y++;                                                                                                                                                                SetpolygonCellNormalSettmpvxl();
tmpIdx++;vCoord3=vCoord2;               vCoord3.y--;if(vCoord2.z>1&&vCoord2.x>1&&vCoord2.y>1&&voxelsBuffer2[2][vCoord2.z+vCoord2.x*Depth].IsCreated)tmpvxl[tmpIdx]=voxelsBuffer2[2][vCoord2.z+vCoord2.x*Depth];else SetpolygonCellNormalSettmpvxl();
tmpIdx++;vCoord3=vCoord2;               vCoord3.z++;                                                                                                                                                                SetpolygonCellNormalSettmpvxl();
tmpIdx++;vCoord3=vCoord2;               vCoord3.z--;if(vCoord2.z>1&&vCoord2.x>1&&vCoord2.y>1&&voxelsBuffer2[0][0].IsCreated)                        tmpvxl[tmpIdx]=voxelsBuffer2[0][0];                        else SetpolygonCellNormalSettmpvxl();
void SetpolygonCellNormalSettmpvxl(){
if(vCoord3.y<=0){tmpvxl[tmpIdx]=voxel.Bedrock;}else if(vCoord3.y>=Height){tmpvxl[tmpIdx]=voxel.Air;}else{
Vector2Int cnkRgn3=cnkRgn2;
Vector2Int cCoord3=cCoord2;
if(vCoord3.x<0||vCoord3.x>=Width||
   vCoord3.z<0||vCoord3.z>=Depth){ValidateCoord(ref cnkRgn3,ref vCoord3);cCoord3=cnkRgnTocCoord(cnkRgn3);}
       int vxlIdx3=GetvxlIdx(vCoord3.x,vCoord3.y,vCoord3.z);
int oftIdx3=GetoftIdx(cCoord3-cCoord1);if(oftIdx3==0&&voxels[vxlIdx3].IsCreated){tmpvxl[tmpIdx]=voxels[vxlIdx3];}else if(oftIdx3>0&&neighbors[oftIdx3-1].ContainsKey(vxlIdx3)){tmpvxl[tmpIdx]=neighbors[oftIdx3-1][vxlIdx3];}else{
Vector3Int noiseInput=vCoord3;noiseInput.x+=cnkRgn3.x;
                              noiseInput.z+=cnkRgn3.y;
biome.Setvxl(noiseInput,nCache,mCache,oftIdx3,vCoord3.z+vCoord3.x*Depth,ref tmpvxl[tmpIdx]);
}
if(oftIdx3==0){voxels[vxlIdx3]=tmpvxl[tmpIdx];}else if(oftIdx3>0){neighbors[oftIdx3-1][vxlIdx3]=tmpvxl[tmpIdx];}
}
}
Vector3 polygonCellNormal=new Vector3{
x=(float)(tmpvxl[1].Density-tmpvxl[0].Density),
y=(float)(tmpvxl[3].Density-tmpvxl[2].Density),
z=(float)(tmpvxl[5].Density-tmpvxl[4].Density)};
polygonCell[corner].Normal=polygonCellNormal;
if(polygonCell[corner].Normal!=Vector3.zero){
polygonCell[corner].Normal.Normalize();
}
if(oftIdx2==0){voxels[vxlIdx2]=polygonCell[corner];}else if(oftIdx2>0){neighbors[oftIdx2-1][vxlIdx2]=polygonCell[corner];}//  :salvar valor construído
}
voxelsBuffer2[0][0]=polygonCell[corner];
voxelsBuffer2[1][vCoord2.z]=polygonCell[corner];
voxelsBuffer2[2][vCoord2.z+vCoord2.x*Depth]=polygonCell[corner];
}
}
int edgeIndex;
/*
    Determine the index into the edge table which
    tells us which vertices are inside of the surface
*/
                                    edgeIndex =  0;
if(-polygonCell[0].Density<IsoLevel)edgeIndex|=  1;
if(-polygonCell[1].Density<IsoLevel)edgeIndex|=  2;
if(-polygonCell[2].Density<IsoLevel)edgeIndex|=  4;
if(-polygonCell[3].Density<IsoLevel)edgeIndex|=  8;
if(-polygonCell[4].Density<IsoLevel)edgeIndex|= 16;
if(-polygonCell[5].Density<IsoLevel)edgeIndex|= 32;
if(-polygonCell[6].Density<IsoLevel)edgeIndex|= 64;
if(-polygonCell[7].Density<IsoLevel)edgeIndex|=128;
if(Tables.EdgeTable[edgeIndex]!=0){/*  Cube is not entirely in/out of the surface  */
//  Use buffered data if available
vertices[ 0]=(vCoord1.z>0?verticesBuffer[0][0][0]:(vCoord1.y>0?verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][0]:Vector3.zero));
vertices[ 1]=(vCoord1.z>0?verticesBuffer[0][0][1]:Vector3.zero);
vertices[ 2]=(vCoord1.z>0?verticesBuffer[0][0][2]:Vector3.zero);
vertices[ 3]=(vCoord1.z>0?verticesBuffer[0][0][3]:(vCoord1.x>0?verticesBuffer[1][vCoord1.z][0]:Vector3.zero));
vertices[ 4]=(vCoord1.y>0?verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][1]:Vector3.zero);
vertices[ 7]=(vCoord1.x>0?verticesBuffer[1][vCoord1.z][1]:Vector3.zero);
vertices[ 8]=(vCoord1.x>0?verticesBuffer[1][vCoord1.z][2]:(vCoord1.y>0?verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][3]:Vector3.zero));
vertices[ 9]=(vCoord1.y>0?verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][2]:Vector3.zero);
vertices[11]=(vCoord1.x>0?verticesBuffer[1][vCoord1.z][3]:Vector3.zero);
if(0!=(Tables.EdgeTable[edgeIndex]&   1)){vertexInterp(0,1,ref vertices[ 0],ref normals[ 0],ref materials[ 0]);}
if(0!=(Tables.EdgeTable[edgeIndex]&   2)){vertexInterp(1,2,ref vertices[ 1],ref normals[ 1],ref materials[ 1]);}
if(0!=(Tables.EdgeTable[edgeIndex]&   4)){vertexInterp(2,3,ref vertices[ 2],ref normals[ 2],ref materials[ 2]);}
if(0!=(Tables.EdgeTable[edgeIndex]&   8)){vertexInterp(3,0,ref vertices[ 3],ref normals[ 3],ref materials[ 3]);}
if(0!=(Tables.EdgeTable[edgeIndex]&  16)){vertexInterp(4,5,ref vertices[ 4],ref normals[ 4],ref materials[ 4]);}
if(0!=(Tables.EdgeTable[edgeIndex]&  32)){vertexInterp(5,6,ref vertices[ 5],ref normals[ 5],ref materials[ 5]);}
if(0!=(Tables.EdgeTable[edgeIndex]&  64)){vertexInterp(6,7,ref vertices[ 6],ref normals[ 6],ref materials[ 6]);}
if(0!=(Tables.EdgeTable[edgeIndex]& 128)){vertexInterp(7,4,ref vertices[ 7],ref normals[ 7],ref materials[ 7]);}
if(0!=(Tables.EdgeTable[edgeIndex]& 256)){vertexInterp(0,4,ref vertices[ 8],ref normals[ 8],ref materials[ 8]);}
if(0!=(Tables.EdgeTable[edgeIndex]& 512)){vertexInterp(1,5,ref vertices[ 9],ref normals[ 9],ref materials[ 9]);}
if(0!=(Tables.EdgeTable[edgeIndex]&1024)){vertexInterp(2,6,ref vertices[10],ref normals[10],ref materials[10]);}
if(0!=(Tables.EdgeTable[edgeIndex]&2048)){vertexInterp(3,7,ref vertices[11],ref normals[11],ref materials[11]);}
void vertexInterp(int c0,int c1,ref Vector3 p,ref Vector3 n,ref materialId m){
density[0]=-polygonCell[c0].Density;vertex[0]=corners[c0];material[0]=polygonCell[c0].Material;
density[1]=-polygonCell[c1].Density;vertex[1]=corners[c1];material[1]=polygonCell[c1].Material;
if(Math.Abs(IsoLevel-density[0])<double.Epsilon){p=vertex[0];goto _Normal;}
if(Math.Abs(IsoLevel-density[1])<double.Epsilon){p=vertex[1];goto _Normal;}
if(Math.Abs(density[0]-density[1])<double.Epsilon){p=vertex[0];goto _Normal;}
double marchingUnit=(IsoLevel-density[0])/(density[1]-density[0]);
p.x=(float)(vertex[0].x+marchingUnit*(vertex[1].x-vertex[0].x));
p.y=(float)(vertex[0].y+marchingUnit*(vertex[1].y-vertex[0].y));
p.z=(float)(vertex[0].z+marchingUnit*(vertex[1].z-vertex[0].z));
_Normal:{
distance[0]=Vector3.Distance(vertex[0],vertex[1]);
distance[1]=Vector3.Distance(vertex[1],p);
n=Vector3.Lerp(
polygonCell[c1].Normal,
polygonCell[c0].Normal,distance[1]/distance[0]);
n=n!=Vector3.zero?n.normalized:Vector3.down;
}
m=material[0];if(density[1]<density[0]){m=material[1];}else if(density[1]==density[0]&&(int)material[1]>(int)material[0]){m=material[1];}
}
/*  Create the triangle  */
for(int i=0;Tables.TriangleTable[edgeIndex][i]!=-1;i+=3){idx[0]=Tables.TriangleTable[edgeIndex][i  ];
                                                         idx[1]=Tables.TriangleTable[edgeIndex][i+1];
                                                         idx[2]=Tables.TriangleTable[edgeIndex][i+2];
                         Vector3 pos=vCoord1-trianglePosAdj;pos.x+=posOffset.x;
                                                            pos.z+=posOffset.y;
                                                              Vector2 materialUV=uv[Mathf.Max((int)materials[idx[0]],
                                                                                              (int)materials[idx[1]],
                                                                                              (int)materials[idx[2]])];
TempVer.Add(new vertex(verPos[0]=pos+vertices[idx[0]],normals[idx[0]],materialUV));if(!vertexUV.ContainsKey(verPos[0])){vertexUV.Add(verPos[0],new List<Vector2>());}vertexUV[verPos[0]].Add(materialUV);
TempVer.Add(new vertex(verPos[1]=pos+vertices[idx[1]],normals[idx[1]],materialUV));if(!vertexUV.ContainsKey(verPos[1])){vertexUV.Add(verPos[1],new List<Vector2>());}vertexUV[verPos[1]].Add(materialUV);
TempVer.Add(new vertex(verPos[2]=pos+vertices[idx[2]],normals[idx[2]],materialUV));if(!vertexUV.ContainsKey(verPos[2])){vertexUV.Add(verPos[2],new List<Vector2>());}vertexUV[verPos[2]].Add(materialUV);
TempTri.Add((ushort)(vertexCount+2));
TempTri.Add((ushort)(vertexCount+1));
TempTri.Add(         vertexCount   );
vertexCount+=3;}
//  Buffer the data
verticesBuffer[0][0][0]=vertices[ 4]+Vector3.back;//  Adiciona um valor "negativo" porque o voxelCoord próximo vai usar esse valor mas precisa obter "uma posição anterior"
verticesBuffer[0][0][1]=vertices[ 5]+Vector3.back;
verticesBuffer[0][0][2]=vertices[ 6]+Vector3.back;
verticesBuffer[0][0][3]=vertices[ 7]+Vector3.back;
verticesBuffer[1][vCoord1.z][0]=vertices[ 1]+Vector3.left;
verticesBuffer[1][vCoord1.z][1]=vertices[ 5]+Vector3.left;
verticesBuffer[1][vCoord1.z][2]=vertices[ 9]+Vector3.left;
verticesBuffer[1][vCoord1.z][3]=vertices[10]+Vector3.left;
verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][0]=vertices[ 2]+Vector3.down;
verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][1]=vertices[ 6]+Vector3.down;
verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][2]=vertices[10]+Vector3.down;
verticesBuffer[2][vCoord1.z+vCoord1.x*Depth][3]=vertices[11]+Vector3.down;
}
}}}
for(crdOffset.y=0,
    posOffset.y=0,
    vCoord1.y=0;vCoord1.y<Height;vCoord1.y++){
for(vCoord1.z=0;vCoord1.z<Depth ;vCoord1.z++){
    vCoord1.x=0;
    crdOffset.x=1;
    posOffset.x=Width;
AddEdgesvertexUV();
    vCoord1.x=Width-1;
    crdOffset.x=-1;
    posOffset.x=-Width;
AddEdgesvertexUV();
}}
for(crdOffset.x=0,
    posOffset.x=0,
    vCoord1.y=0;vCoord1.y<Height;vCoord1.y++){
for(vCoord1.x=0;vCoord1.x<Width ;vCoord1.x++){
    vCoord1.z=0;
    crdOffset.y=1;
    posOffset.y=Depth;
AddEdgesvertexUV();
    vCoord1.z=Depth-1;
    crdOffset.y=-1;
    posOffset.y=-Depth;
AddEdgesvertexUV();
}}
void AddEdgesvertexUV(){
int corner=0;Vector3Int vCoord2=vCoord1;                        SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;                          SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;vCoord2.y+=1;             SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;             vCoord2.y+=1;             SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;                          vCoord2.z+=1;SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;             vCoord2.z+=1;SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;vCoord2.x+=1;vCoord2.y+=1;vCoord2.z+=1;SetpolygonCellVoxel();
corner++;vCoord2=vCoord1;             vCoord2.y+=1;vCoord2.z+=1;SetpolygonCellVoxel();
void SetpolygonCellVoxel(){
if(vCoord2.y<=0){polygonCell[corner]=voxel.Bedrock;/*  :fora do mundo, baixo  */}else if(vCoord2.y>=Height){polygonCell[corner]=voxel.Air;/*  :fora do mundo, cima  */}else{
Vector2Int cnkRgn2=cnkRgn1+posOffset;
Vector2Int cCoord2=cCoord1+crdOffset;
if(vCoord2.x<0||vCoord2.x>=Width||
   vCoord2.z<0||vCoord2.z>=Depth){ValidateCoord(ref cnkRgn2,ref vCoord2);cCoord2=cnkRgnTocCoord(cnkRgn2);}
       int vxlIdx2=GetvxlIdx(vCoord2.x,vCoord2.y,vCoord2.z);
int oftIdx2=GetoftIdx(cCoord2-cCoord1);/*  já construído:  */if(oftIdx2==0&&voxels[vxlIdx2].IsCreated){polygonCell[corner]=voxels[vxlIdx2];}else if(oftIdx2>0&&neighbors[oftIdx2-1].ContainsKey(vxlIdx2)){polygonCell[corner]=neighbors[oftIdx2-1][vxlIdx2];}else{
Vector3Int noiseInput=vCoord2;noiseInput.x+=cnkRgn2.x;
                              noiseInput.z+=cnkRgn2.y;
biome.Setvxl(noiseInput,nCache,mCache,oftIdx2,vCoord2.z+vCoord2.x*Depth,ref polygonCell[corner]);
}
}
}
int edgeIndex;
/*
    Determine the index into the edge table which
    tells us which vertices are inside of the surface
*/
                                    edgeIndex =  0;
if(-polygonCell[0].Density<IsoLevel)edgeIndex|=  1;
if(-polygonCell[1].Density<IsoLevel)edgeIndex|=  2;
if(-polygonCell[2].Density<IsoLevel)edgeIndex|=  4;
if(-polygonCell[3].Density<IsoLevel)edgeIndex|=  8;
if(-polygonCell[4].Density<IsoLevel)edgeIndex|= 16;
if(-polygonCell[5].Density<IsoLevel)edgeIndex|= 32;
if(-polygonCell[6].Density<IsoLevel)edgeIndex|= 64;
if(-polygonCell[7].Density<IsoLevel)edgeIndex|=128;
if(Tables.EdgeTable[edgeIndex]!=0){
if(0!=(Tables.EdgeTable[edgeIndex]&   1)){vertexInterp(0,1,ref vertices[ 0],ref materials[ 0]);}
if(0!=(Tables.EdgeTable[edgeIndex]&   2)){vertexInterp(1,2,ref vertices[ 1],ref materials[ 1]);}
if(0!=(Tables.EdgeTable[edgeIndex]&   4)){vertexInterp(2,3,ref vertices[ 2],ref materials[ 2]);}
if(0!=(Tables.EdgeTable[edgeIndex]&   8)){vertexInterp(3,0,ref vertices[ 3],ref materials[ 3]);}
if(0!=(Tables.EdgeTable[edgeIndex]&  16)){vertexInterp(4,5,ref vertices[ 4],ref materials[ 4]);}
if(0!=(Tables.EdgeTable[edgeIndex]&  32)){vertexInterp(5,6,ref vertices[ 5],ref materials[ 5]);}
if(0!=(Tables.EdgeTable[edgeIndex]&  64)){vertexInterp(6,7,ref vertices[ 6],ref materials[ 6]);}
if(0!=(Tables.EdgeTable[edgeIndex]& 128)){vertexInterp(7,4,ref vertices[ 7],ref materials[ 7]);}
if(0!=(Tables.EdgeTable[edgeIndex]& 256)){vertexInterp(0,4,ref vertices[ 8],ref materials[ 8]);}
if(0!=(Tables.EdgeTable[edgeIndex]& 512)){vertexInterp(1,5,ref vertices[ 9],ref materials[ 9]);}
if(0!=(Tables.EdgeTable[edgeIndex]&1024)){vertexInterp(2,6,ref vertices[10],ref materials[10]);}
if(0!=(Tables.EdgeTable[edgeIndex]&2048)){vertexInterp(3,7,ref vertices[11],ref materials[11]);}
void vertexInterp(int c0,int c1,ref Vector3 p,ref materialId m){
density[0]=-polygonCell[c0].Density;vertex[0]=corners[c0];material[0]=polygonCell[c0].Material;
density[1]=-polygonCell[c1].Density;vertex[1]=corners[c1];material[1]=polygonCell[c1].Material;
if(Math.Abs(IsoLevel-density[0])<double.Epsilon){p=vertex[0];goto _Material;}
if(Math.Abs(IsoLevel-density[1])<double.Epsilon){p=vertex[1];goto _Material;}
if(Math.Abs(density[0]-density[1])<double.Epsilon){p=vertex[0];goto _Material;}
double marchingUnit=(IsoLevel-density[0])/(density[1]-density[0]);
p.x=(float)(vertex[0].x+marchingUnit*(vertex[1].x-vertex[0].x));
p.y=(float)(vertex[0].y+marchingUnit*(vertex[1].y-vertex[0].y));
p.z=(float)(vertex[0].z+marchingUnit*(vertex[1].z-vertex[0].z));
_Material:{
m=material[0];if(density[1]<density[0]){m=material[1];}else if(density[1]==density[0]&&(int)material[1]>(int)material[0]){m=material[1];}
}
}
/*  Create the triangle  */
for(int i=0;Tables.TriangleTable[edgeIndex][i]!=-1;i+=3){idx[0]=Tables.TriangleTable[edgeIndex][i  ];
                                                         idx[1]=Tables.TriangleTable[edgeIndex][i+1];
                                                         idx[2]=Tables.TriangleTable[edgeIndex][i+2];
                         Vector3 pos=vCoord1-trianglePosAdj;pos.x+=posOffset.x;
                                                            pos.z+=posOffset.y;
                                                              Vector2 materialUV=uv[Mathf.Max((int)materials[idx[0]],
                                                                                              (int)materials[idx[1]],
                                                                                              (int)materials[idx[2]])];
                       verPos[0]=pos+vertices[idx[0]]                             ;if(!vertexUV.ContainsKey(verPos[0])){vertexUV.Add(verPos[0],new List<Vector2>());}vertexUV[verPos[0]].Add(materialUV);
                       verPos[1]=pos+vertices[idx[1]]                             ;if(!vertexUV.ContainsKey(verPos[1])){vertexUV.Add(verPos[1],new List<Vector2>());}vertexUV[verPos[1]].Add(materialUV);
                       verPos[2]=pos+vertices[idx[2]]                             ;if(!vertexUV.ContainsKey(verPos[2])){vertexUV.Add(verPos[2],new List<Vector2>());}vertexUV[verPos[2]].Add(materialUV);
}
}
}
for(int i=0;i<TempVer.Length/3;i++){idx[0]=i*3;idx[1]=i*3+1;idx[2]=i*3+2;for(int j=0;j<3;j++){
var materialIdGroupingOrdered=vertexUV[verPos[j]=TempVer[idx[j]].pos].ToArray().Select(uv=>{return (materialId)Array.IndexOf(atlasHelper.uv,uv);}).GroupBy(value=>value).OrderByDescending(group=>group.Key).ThenByDescending(group=>group.Count());weights.Clear();int total=0;
Vector2 uv0=TempVer[idx[j]].texCoord0;foreach(var materialIdGroup in materialIdGroupingOrdered){bool add;
Vector2 uv=atlasHelper.uv[(int)materialIdGroup.First()];
if(uv0==uv){
total+=weights[0]=materialIdGroup.Count();
}else if(((add=TempVer[idx[j]].texCoord1==emptyUV)&&TempVer[idx[j]].texCoord2!=uv&&TempVer[idx[j]].texCoord3!=uv)||TempVer[idx[j]].texCoord1==uv){
if(add){var v1=TempVer[idx[0]];v1.texCoord1=uv;TempVer[idx[0]]=v1;
            v1=TempVer[idx[1]];v1.texCoord1=uv;TempVer[idx[1]]=v1;
            v1=TempVer[idx[2]];v1.texCoord1=uv;TempVer[idx[2]]=v1;
}
total+=weights[1]=materialIdGroup.Count();
}else if(((add=TempVer[idx[j]].texCoord2==emptyUV)&&TempVer[idx[j]].texCoord3!=uv                               )||TempVer[idx[j]].texCoord2==uv){
if(add){var v1=TempVer[idx[0]];v1.texCoord2=uv;TempVer[idx[0]]=v1;
            v1=TempVer[idx[1]];v1.texCoord2=uv;TempVer[idx[1]]=v1;
            v1=TempVer[idx[2]];v1.texCoord2=uv;TempVer[idx[2]]=v1;
}
total+=weights[2]=materialIdGroup.Count();
}else if(((add=TempVer[idx[j]].texCoord3==emptyUV)                                                              )||TempVer[idx[j]].texCoord3==uv){
if(add){var v1=TempVer[idx[0]];v1.texCoord3=uv;TempVer[idx[0]]=v1;
            v1=TempVer[idx[1]];v1.texCoord3=uv;TempVer[idx[1]]=v1;
            v1=TempVer[idx[2]];v1.texCoord3=uv;TempVer[idx[2]]=v1;
}
total+=weights[3]=materialIdGroup.Count();
}
}
if(weights.Count>1){var v2=TempVer[idx[j]];
        Color col=v2.color;col.r=(weights[0]/(float)total);
if(weights.ContainsKey(1)){col.g=(weights[1]/(float)total);}
if(weights.ContainsKey(2)){col.b=(weights[2]/(float)total);}
if(weights.ContainsKey(3)){col.a=(weights[3]/(float)total);}
                  v2.color=col;TempVer[idx[j]]=v2;
}
}}
int GetoftIdx(Vector2Int offset){//  ..for neighbors
if(offset.x== 0&&offset.y== 0)return 0;
if(offset.x==-1&&offset.y== 0)return 1;
if(offset.x== 1&&offset.y== 0)return 2;
if(offset.x== 0&&offset.y==-1)return 3;
if(offset.x==-1&&offset.y==-1)return 4;
if(offset.x== 1&&offset.y==-1)return 5;
if(offset.x== 0&&offset.y== 1)return 6;
if(offset.x==-1&&offset.y== 1)return 7;
if(offset.x== 1&&offset.y== 1)return 8;
return -1;}
}
}
#if UNITY_EDITOR
void OnDrawGizmos(){
//DrawBounds(localBounds,Color.white);
}
#endif
}}
namespace paulbourke.MarchingCubes{
    internal static class Tables{
        internal static readonly ReadOnlyCollection<int>EdgeTable=new ReadOnlyCollection<int>(new int[256]{
            0x0  ,0x109,0x203,0x30a,0x406,0x50f,0x605,0x70c,
            0x80c,0x905,0xa0f,0xb06,0xc0a,0xd03,0xe09,0xf00,
            0x190,0x99 ,0x393,0x29a,0x596,0x49f,0x795,0x69c,
            0x99c,0x895,0xb9f,0xa96,0xd9a,0xc93,0xf99,0xe90,
            0x230,0x339,0x33 ,0x13a,0x636,0x73f,0x435,0x53c,
            0xa3c,0xb35,0x83f,0x936,0xe3a,0xf33,0xc39,0xd30,
            0x3a0,0x2a9,0x1a3,0xaa ,0x7a6,0x6af,0x5a5,0x4ac,
            0xbac,0xaa5,0x9af,0x8a6,0xfaa,0xea3,0xda9,0xca0,
            0x460,0x569,0x663,0x76a,0x66 ,0x16f,0x265,0x36c,
            0xc6c,0xd65,0xe6f,0xf66,0x86a,0x963,0xa69,0xb60,
            0x5f0,0x4f9,0x7f3,0x6fa,0x1f6,0xff ,0x3f5,0x2fc,
            0xdfc,0xcf5,0xfff,0xef6,0x9fa,0x8f3,0xbf9,0xaf0,
            0x650,0x759,0x453,0x55a,0x256,0x35f,0x55 ,0x15c,
            0xe5c,0xf55,0xc5f,0xd56,0xa5a,0xb53,0x859,0x950,
            0x7c0,0x6c9,0x5c3,0x4ca,0x3c6,0x2cf,0x1c5,0xcc ,
            0xfcc,0xec5,0xdcf,0xcc6,0xbca,0xac3,0x9c9,0x8c0,
            0x8c0,0x9c9,0xac3,0xbca,0xcc6,0xdcf,0xec5,0xfcc,
            0xcc ,0x1c5,0x2cf,0x3c6,0x4ca,0x5c3,0x6c9,0x7c0,
            0x950,0x859,0xb53,0xa5a,0xd56,0xc5f,0xf55,0xe5c,
            0x15c,0x55 ,0x35f,0x256,0x55a,0x453,0x759,0x650,
            0xaf0,0xbf9,0x8f3,0x9fa,0xef6,0xfff,0xcf5,0xdfc,
            0x2fc,0x3f5,0xff ,0x1f6,0x6fa,0x7f3,0x4f9,0x5f0,
            0xb60,0xa69,0x963,0x86a,0xf66,0xe6f,0xd65,0xc6c,
            0x36c,0x265,0x16f,0x66 ,0x76a,0x663,0x569,0x460,
            0xca0,0xda9,0xea3,0xfaa,0x8a6,0x9af,0xaa5,0xbac,
            0x4ac,0x5a5,0x6af,0x7a6,0xaa ,0x1a3,0x2a9,0x3a0,
            0xd30,0xc39,0xf33,0xe3a,0x936,0x83f,0xb35,0xa3c,
            0x53c,0x435,0x73f,0x636,0x13a,0x33 ,0x339,0x230,
            0xe90,0xf99,0xc93,0xd9a,0xa96,0xb9f,0x895,0x99c,
            0x69c,0x795,0x49f,0x596,0x29a,0x393,0x99 ,0x190,
            0xf00,0xe09,0xd03,0xc0a,0xb06,0xa0f,0x905,0x80c,
            0x70c,0x605,0x50f,0x406,0x30a,0x203,0x109,0x0
        });
        #region TriangleTable
        internal static readonly ReadOnlyCollection<int[]>TriangleTable=new ReadOnlyCollection<int[]>(new int[256][]{
        new int[16]{-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 1, 9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 8, 3, 9, 8, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 3, 1, 2,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 2,10, 0, 2, 9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 8, 3, 2,10, 8,10, 9, 8,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3,11, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0,11, 2, 8,11, 0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 9, 0, 2, 3,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1,11, 2, 1, 9,11, 9, 8,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3,10, 1,11,10, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0,10, 1, 0, 8,10, 8,11,10,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 9, 0, 3,11, 9,11,10, 9,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 8,10,10, 8,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 7, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 3, 0, 7, 3, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 1, 9, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 1, 9, 4, 7, 1, 7, 3, 1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2,10, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 4, 7, 3, 0, 4, 1, 2,10,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 2,10, 9, 0, 2, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2,10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4,-1,-1,-1,-1},
        new int[16]{ 8, 4, 7, 3,11, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{11, 4, 7,11, 2, 4, 2, 0, 4,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 0, 1, 8, 4, 7, 2, 3,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 7,11, 9, 4,11, 9,11, 2, 9, 2, 1,-1,-1,-1,-1},
        new int[16]{ 3,10, 1, 3,11,10, 7, 8, 4,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1,11,10, 1, 4,11, 1, 0, 4, 7,11, 4,-1,-1,-1,-1},
        new int[16]{ 4, 7, 8, 9, 0,11, 9,11,10,11, 0, 3,-1,-1,-1,-1},
        new int[16]{ 4, 7,11, 4,11, 9, 9,11,10,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 5, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 5, 4, 0, 8, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 5, 4, 1, 5, 0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 5, 4, 8, 3, 5, 3, 1, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2,10, 9, 5, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 0, 8, 1, 2,10, 4, 9, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5, 2,10, 5, 4, 2, 4, 0, 2,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2,10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8,-1,-1,-1,-1},
        new int[16]{ 9, 5, 4, 2, 3,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0,11, 2, 0, 8,11, 4, 9, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 5, 4, 0, 1, 5, 2, 3,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 1, 5, 2, 5, 8, 2, 8,11, 4, 8, 5,-1,-1,-1,-1},
        new int[16]{10, 3,11,10, 1, 3, 9, 5, 4,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 9, 5, 0, 8, 1, 8,10, 1, 8,11,10,-1,-1,-1,-1},
        new int[16]{ 5, 4, 0, 5, 0,11, 5,11,10,11, 0, 3,-1,-1,-1,-1},
        new int[16]{ 5, 4, 8, 5, 8,10,10, 8,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 7, 8, 5, 7, 9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 3, 0, 9, 5, 3, 5, 7, 3,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 7, 8, 0, 1, 7, 1, 5, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 5, 3, 3, 5, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 7, 8, 9, 5, 7,10, 1, 2,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3,-1,-1,-1,-1},
        new int[16]{ 8, 0, 2, 8, 2, 5, 8, 5, 7,10, 5, 2,-1,-1,-1,-1},
        new int[16]{ 2,10, 5, 2, 5, 3, 3, 5, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 7, 9, 5, 7, 8, 9, 3,11, 2,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7,11,-1,-1,-1,-1},
        new int[16]{ 2, 3,11, 0, 1, 8, 1, 7, 8, 1, 5, 7,-1,-1,-1,-1},
        new int[16]{11, 2, 1,11, 1, 7, 7, 1, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 5, 8, 8, 5, 7,10, 1, 3,10, 3,11,-1,-1,-1,-1},
        new int[16]{ 5, 7, 0, 5, 0, 9, 7,11, 0, 1, 0,10,11,10, 0,-1},
        new int[16]{11,10, 0,11, 0, 3,10, 5, 0, 8, 0, 7, 5, 7, 0,-1},
        new int[16]{11,10, 5, 7,11, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 6, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 3, 5,10, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 0, 1, 5,10, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 8, 3, 1, 9, 8, 5,10, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 6, 5, 2, 6, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 6, 5, 1, 2, 6, 3, 0, 8,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 6, 5, 9, 0, 6, 0, 2, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8,-1,-1,-1,-1},
        new int[16]{ 2, 3,11,10, 6, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{11, 0, 8,11, 2, 0,10, 6, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 1, 9, 2, 3,11, 5,10, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5,10, 6, 1, 9, 2, 9,11, 2, 9, 8,11,-1,-1,-1,-1},
        new int[16]{ 6, 3,11, 6, 5, 3, 5, 1, 3,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8,11, 0,11, 5, 0, 5, 1, 5,11, 6,-1,-1,-1,-1},
        new int[16]{ 3,11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9,-1,-1,-1,-1},
        new int[16]{ 6, 5, 9, 6, 9,11,11, 9, 8,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5,10, 6, 4, 7, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 3, 0, 4, 7, 3, 6, 5,10,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 9, 0, 5,10, 6, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4,-1,-1,-1,-1},
        new int[16]{ 6, 1, 2, 6, 5, 1, 4, 7, 8,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7,-1,-1,-1,-1},
        new int[16]{ 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6,-1,-1,-1,-1},
        new int[16]{ 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9,-1},
        new int[16]{ 3,11, 2, 7, 8, 4,10, 6, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5,10, 6, 4, 7, 2, 4, 2, 0, 2, 7,11,-1,-1,-1,-1},
        new int[16]{ 0, 1, 9, 4, 7, 8, 2, 3,11, 5,10, 6,-1,-1,-1,-1},
        new int[16]{ 9, 2, 1, 9,11, 2, 9, 4,11, 7,11, 4, 5,10, 6,-1},
        new int[16]{ 8, 4, 7, 3,11, 5, 3, 5, 1, 5,11, 6,-1,-1,-1,-1},
        new int[16]{ 5, 1,11, 5,11, 6, 1, 0,11, 7,11, 4, 0, 4,11,-1},
        new int[16]{ 0, 5, 9, 0, 6, 5, 0, 3, 6,11, 6, 3, 8, 4, 7,-1},
        new int[16]{ 6, 5, 9, 6, 9,11, 4, 7, 9, 7,11, 9,-1,-1,-1,-1},
        new int[16]{10, 4, 9, 6, 4,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4,10, 6, 4, 9,10, 0, 8, 3,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 0, 1,10, 6, 0, 6, 4, 0,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1,10,-1,-1,-1,-1},
        new int[16]{ 1, 4, 9, 1, 2, 4, 2, 6, 4,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4,-1,-1,-1,-1},
        new int[16]{ 0, 2, 4, 4, 2, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 3, 2, 8, 2, 4, 4, 2, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 4, 9,10, 6, 4,11, 2, 3,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 2, 2, 8,11, 4, 9,10, 4,10, 6,-1,-1,-1,-1},
        new int[16]{ 3,11, 2, 0, 1, 6, 0, 6, 4, 6, 1,10,-1,-1,-1,-1},
        new int[16]{ 6, 4, 1, 6, 1,10, 4, 8, 1, 2, 1,11, 8,11, 1,-1},
        new int[16]{ 9, 6, 4, 9, 3, 6, 9, 1, 3,11, 6, 3,-1,-1,-1,-1},
        new int[16]{ 8,11, 1, 8, 1, 0,11, 6, 1, 9, 1, 4, 6, 4, 1,-1},
        new int[16]{ 3,11, 6, 3, 6, 0, 0, 6, 4,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 6, 4, 8,11, 6, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 7,10, 6, 7, 8,10, 8, 9,10,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 7, 3, 0,10, 7, 0, 9,10, 6, 7,10,-1,-1,-1,-1},
        new int[16]{10, 6, 7, 1,10, 7, 1, 7, 8, 1, 8, 0,-1,-1,-1,-1},
        new int[16]{10, 6, 7,10, 7, 1, 1, 7, 3,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7,-1,-1,-1,-1},
        new int[16]{ 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9,-1},
        new int[16]{ 7, 8, 0, 7, 0, 6, 6, 0, 2,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 7, 3, 2, 6, 7, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 3,11,10, 6, 8,10, 8, 9, 8, 6, 7,-1,-1,-1,-1},
        new int[16]{ 2, 0, 7, 2, 7,11, 0, 9, 7, 6, 7,10, 9,10, 7,-1},
        new int[16]{ 1, 8, 0, 1, 7, 8, 1,10, 7, 6, 7,10, 2, 3,11,-1},
        new int[16]{11, 2, 1,11, 1, 7,10, 6, 1, 6, 7, 1,-1,-1,-1,-1},
        new int[16]{ 8, 9, 6, 8, 6, 7, 9, 1, 6,11, 6, 3, 1, 3, 6,-1},
        new int[16]{ 0, 9, 1,11, 6, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 7, 8, 0, 7, 0, 6, 3,11, 0,11, 6, 0,-1,-1,-1,-1},
        new int[16]{ 7,11, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 7, 6,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 0, 8,11, 7, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 1, 9,11, 7, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 1, 9, 8, 3, 1,11, 7, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 1, 2, 6,11, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2,10, 3, 0, 8, 6,11, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 9, 0, 2,10, 9, 6,11, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 6,11, 7, 2,10, 3,10, 8, 3,10, 9, 8,-1,-1,-1,-1},
        new int[16]{ 7, 2, 3, 6, 2, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 7, 0, 8, 7, 6, 0, 6, 2, 0,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 7, 6, 2, 3, 7, 0, 1, 9,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6,-1,-1,-1,-1},
        new int[16]{10, 7, 6,10, 1, 7, 1, 3, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 7, 6, 1, 7,10, 1, 8, 7, 1, 0, 8,-1,-1,-1,-1},
        new int[16]{ 0, 3, 7, 0, 7,10, 0,10, 9, 6,10, 7,-1,-1,-1,-1},
        new int[16]{ 7, 6,10, 7,10, 8, 8,10, 9,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 6, 8, 4,11, 8, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 6,11, 3, 0, 6, 0, 4, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 6,11, 8, 4, 6, 9, 0, 1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 4, 6, 9, 6, 3, 9, 3, 1,11, 3, 6,-1,-1,-1,-1},
        new int[16]{ 6, 8, 4, 6,11, 8, 2,10, 1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2,10, 3, 0,11, 0, 6,11, 0, 4, 6,-1,-1,-1,-1},
        new int[16]{ 4,11, 8, 4, 6,11, 0, 2, 9, 2,10, 9,-1,-1,-1,-1},
        new int[16]{10, 9, 3,10, 3, 2, 9, 4, 3,11, 3, 6, 4, 6, 3,-1},
        new int[16]{ 8, 2, 3, 8, 4, 2, 4, 6, 2,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 4, 2, 4, 6, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8,-1,-1,-1,-1},
        new int[16]{ 1, 9, 4, 1, 4, 2, 2, 4, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 1, 3, 8, 6, 1, 8, 4, 6, 6,10, 1,-1,-1,-1,-1},
        new int[16]{10, 1, 0,10, 0, 6, 6, 0, 4,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 6, 3, 4, 3, 8, 6,10, 3, 0, 3, 9,10, 9, 3,-1},
        new int[16]{10, 9, 4, 6,10, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 9, 5, 7, 6,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 3, 4, 9, 5,11, 7, 6,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5, 0, 1, 5, 4, 0, 7, 6,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5,-1,-1,-1,-1},
        new int[16]{ 9, 5, 4,10, 1, 2, 7, 6,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 6,11, 7, 1, 2,10, 0, 8, 3, 4, 9, 5,-1,-1,-1,-1},
        new int[16]{ 7, 6,11, 5, 4,10, 4, 2,10, 4, 0, 2,-1,-1,-1,-1},
        new int[16]{ 3, 4, 8, 3, 5, 4, 3, 2, 5,10, 5, 2,11, 7, 6,-1},
        new int[16]{ 7, 2, 3, 7, 6, 2, 5, 4, 9,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7,-1,-1,-1,-1},
        new int[16]{ 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0,-1,-1,-1,-1},
        new int[16]{ 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8,-1},
        new int[16]{ 9, 5, 4,10, 1, 6, 1, 7, 6, 1, 3, 7,-1,-1,-1,-1},
        new int[16]{ 1, 6,10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4,-1},
        new int[16]{ 4, 0,10, 4,10, 5, 0, 3,10, 6,10, 7, 3, 7,10,-1},
        new int[16]{ 7, 6,10, 7,10, 8, 5, 4,10, 4, 8,10,-1,-1,-1,-1},
        new int[16]{ 6, 9, 5, 6,11, 9,11, 8, 9,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 6,11, 0, 6, 3, 0, 5, 6, 0, 9, 5,-1,-1,-1,-1},
        new int[16]{ 0,11, 8, 0, 5,11, 0, 1, 5, 5, 6,11,-1,-1,-1,-1},
        new int[16]{ 6,11, 3, 6, 3, 5, 5, 3, 1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2,10, 9, 5,11, 9,11, 8,11, 5, 6,-1,-1,-1,-1},
        new int[16]{ 0,11, 3, 0, 6,11, 0, 9, 6, 5, 6, 9, 1, 2,10,-1},
        new int[16]{11, 8, 5,11, 5, 6, 8, 0, 5,10, 5, 2, 0, 2, 5,-1},
        new int[16]{ 6,11, 3, 6, 3, 5, 2,10, 3,10, 5, 3,-1,-1,-1,-1},
        new int[16]{ 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2,-1,-1,-1,-1},
        new int[16]{ 9, 5, 6, 9, 6, 0, 0, 6, 2,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8,-1},
        new int[16]{ 1, 5, 6, 2, 1, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 3, 6, 1, 6,10, 3, 8, 6, 5, 6, 9, 8, 9, 6,-1},
        new int[16]{10, 1, 0,10, 0, 6, 9, 5, 0, 5, 6, 0,-1,-1,-1,-1},
        new int[16]{ 0, 3, 8, 5, 6,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 5, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{11, 5,10, 7, 5,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{11, 5,10,11, 7, 5, 8, 3, 0,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5,11, 7, 5,10,11, 1, 9, 0,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{10, 7, 5,10,11, 7, 9, 8, 1, 8, 3, 1,-1,-1,-1,-1},
        new int[16]{11, 1, 2,11, 7, 1, 7, 5, 1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2,11,-1,-1,-1,-1},
        new int[16]{ 9, 7, 5, 9, 2, 7, 9, 0, 2, 2,11, 7,-1,-1,-1,-1},
        new int[16]{ 7, 5, 2, 7, 2,11, 5, 9, 2, 3, 2, 8, 9, 8, 2,-1},
        new int[16]{ 2, 5,10, 2, 3, 5, 3, 7, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 2, 0, 8, 5, 2, 8, 7, 5,10, 2, 5,-1,-1,-1,-1},
        new int[16]{ 9, 0, 1, 5,10, 3, 5, 3, 7, 3,10, 2,-1,-1,-1,-1},
        new int[16]{ 9, 8, 2, 9, 2, 1, 8, 7, 2,10, 2, 5, 7, 5, 2,-1},
        new int[16]{ 1, 3, 5, 3, 7, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 7, 0, 7, 1, 1, 7, 5,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 0, 3, 9, 3, 5, 5, 3, 7,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9, 8, 7, 5, 9, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5, 8, 4, 5,10, 8,10,11, 8,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 5, 0, 4, 5,11, 0, 5,10,11,11, 3, 0,-1,-1,-1,-1},
        new int[16]{ 0, 1, 9, 8, 4,10, 8,10,11,10, 4, 5,-1,-1,-1,-1},
        new int[16]{10,11, 4,10, 4, 5,11, 3, 4, 9, 4, 1, 3, 1, 4,-1},
        new int[16]{ 2, 5, 1, 2, 8, 5, 2,11, 8, 4, 5, 8,-1,-1,-1,-1},
        new int[16]{ 0, 4,11, 0,11, 3, 4, 5,11, 2,11, 1, 5, 1,11,-1},
        new int[16]{ 0, 2, 5, 0, 5, 9, 2,11, 5, 4, 5, 8,11, 8, 5,-1},
        new int[16]{ 9, 4, 5, 2,11, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 5,10, 3, 5, 2, 3, 4, 5, 3, 8, 4,-1,-1,-1,-1},
        new int[16]{ 5,10, 2, 5, 2, 4, 4, 2, 0,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3,10, 2, 3, 5,10, 3, 8, 5, 4, 5, 8, 0, 1, 9,-1},
        new int[16]{ 5,10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2,-1,-1,-1,-1},
        new int[16]{ 8, 4, 5, 8, 5, 3, 3, 5, 1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 4, 5, 1, 0, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5,-1,-1,-1,-1},
        new int[16]{ 9, 4, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4,11, 7, 4, 9,11, 9,10,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 8, 3, 4, 9, 7, 9,11, 7, 9,10,11,-1,-1,-1,-1},
        new int[16]{ 1,10,11, 1,11, 4, 1, 4, 0, 7, 4,11,-1,-1,-1,-1},
        new int[16]{ 3, 1, 4, 3, 4, 8, 1,10, 4, 7, 4,11,10,11, 4,-1},
        new int[16]{ 4,11, 7, 9,11, 4, 9, 2,11, 9, 1, 2,-1,-1,-1,-1},
        new int[16]{ 9, 7, 4, 9,11, 7, 9, 1,11, 2,11, 1, 0, 8, 3,-1},
        new int[16]{11, 7, 4,11, 4, 2, 2, 4, 0,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{11, 7, 4,11, 4, 2, 8, 3, 4, 3, 2, 4,-1,-1,-1,-1},
        new int[16]{ 2, 9,10, 2, 7, 9, 2, 3, 7, 7, 4, 9,-1,-1,-1,-1},
        new int[16]{ 9,10, 7, 9, 7, 4,10, 2, 7, 8, 7, 0, 2, 0, 7,-1},
        new int[16]{ 3, 7,10, 3,10, 2, 7, 4,10, 1,10, 0, 4, 0,10,-1},
        new int[16]{ 1,10, 2, 8, 7, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 9, 1, 4, 1, 7, 7, 1, 3,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1,-1,-1,-1,-1},
        new int[16]{ 4, 0, 3, 7, 4, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 4, 8, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9,10, 8,10,11, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 0, 9, 3, 9,11,11, 9,10,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 1,10, 0,10, 8, 8,10,11,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 1,10,11, 3,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 2,11, 1,11, 9, 9,11, 8,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 0, 9, 3, 9,11, 1, 2, 9, 2,11, 9,-1,-1,-1,-1},
        new int[16]{ 0, 2,11, 8, 0,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 3, 2,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 3, 8, 2, 8,10,10, 8, 9,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 9,10, 2, 0, 9, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 2, 3, 8, 2, 8,10, 0, 1, 8, 1,10, 8,-1,-1,-1,-1},
        new int[16]{ 1,10, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 1, 3, 8, 9, 1, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 9, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{ 0, 3, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
        new int[16]{-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1}});
        #endregion
    }
}