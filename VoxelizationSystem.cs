using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Linq;

[AlwaysUpdateSystem]
public class VoxelizationSystem : JobComponentSystem
{
    /*Setup */
    protected Mesh modelMesh;
    protected GameObject voxelPrefab, modelPrefab;
    EntityQuery voxelGroup;
    protected VoxelUtility voxelUtility;
    protected NativeArray<int> posIndex;
    protected NativeHashMap<int,bool> hashMap;
    
    /*Parameters*/
    const float voxelSize = 0.05f;
    const float duration = 6.0f;
    public static readonly string modelName = "Human";

    /*Runtime */
    int completedVoxel = 0;

    struct HashTriangleToVoxel : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float3> vertexs;  // vx, vy, vz

        [ReadOnly]
        public NativeArray<int3> triangles;  //idx_A, idx_B, idx_C

        public NativeHashMap<int, bool>.Concurrent voxels; 

        public void Execute(int index){
            int3 triangle = triangles[index];
            float3 vertexA = vertexs[triangle.x];
            float3 vertexB = vertexs[triangle.y];
            float3 vertexC = vertexs[triangle.z];
            coverVoxelsWithTriangle(vertexA, vertexB, vertexC);
        }

        private void coverVoxelsWithTriangle(float3 A, float3 B, float3 C){
            //If any edge is longer than voxelSize, should subdivide

            if (math.distancesq(A, B) > voxelSize * voxelSize){
                float3 AMB = (A + B) * 0.5f;
                coverVoxelsWithTriangle(A, AMB, C);
                coverVoxelsWithTriangle(B, AMB, C);
                return;
            }

            if (math.distancesq(A, C) > voxelSize * voxelSize){
                float3 AMC = (A + C) * 0.5f;
                coverVoxelsWithTriangle(A, AMC, B);
                coverVoxelsWithTriangle(C, AMC, B);
                return;
            }

            if (math.distancesq(B, C) > voxelSize * voxelSize){
                float3 BMC = (B + C) * 0.5f;
                coverVoxelsWithTriangle(B, BMC, A);
                coverVoxelsWithTriangle(C, BMC, A);
                return;
            }

            //Now all three edges < voxelSize
            coverVoxelsWithTriangleEdge(A, B);
            coverVoxelsWithTriangleEdge(B, C);
            coverVoxelsWithTriangleEdge(C, A);
        }

        private void coverVoxelsWithTriangleEdge(float3 A, float3 B){
            int3 aVoxelIndex = VoxelUtility.GetGrid(A);
            int3 bVoxelIndex = VoxelUtility.GetGrid(B);
            int aVoxelIndex1 = VoxelUtility.GetGridIndex(aVoxelIndex);
            int bVoxeklndex1 = VoxelUtility.GetGridIndex(bVoxelIndex);
            float3 aVoxelCenter = VoxelUtility.GetVoxelPosition(aVoxelIndex);
            float3 bVoxelCenter = VoxelUtility.GetVoxelPosition(bVoxelIndex);

            if (aVoxelIndex.x == bVoxelIndex.x && aVoxelIndex.y == bVoxelIndex.y
            || aVoxelIndex.x == bVoxelIndex.x && aVoxelIndex.z == bVoxelIndex.z
            || aVoxelIndex.y == bVoxelIndex.y && aVoxelIndex.z == bVoxelIndex.z){
                /*Same Grid or 6-neighbor*/
                voxels.TryAdd(aVoxelIndex1, true);
                voxels.TryAdd(bVoxeklndex1, true);
                return;
            }

            if (aVoxelIndex.x == bVoxelIndex.x){
                /*yz plane neighbor */
                float2 xy = LinePlaneIntersect(A, B, 'z', 0.5f * (aVoxelCenter.z + bVoxelCenter.z));
                float x = xy.x;
                float y = xy.y;

                //if (!(A.x <= x && x <= B.x || B.x <= x && x <= A.x)){
                //    Debug.Log(A + "<>" + B + "<>" + aVoxelCenter + "<>" + bVoxelCenter + " -> " + x);
                //}
                //Debug.Assert(A.x <= x && x <= B.x || B.x <= x && x <= A.x);
                int connector;
                if (math.abs(y - A.y) < math.abs(y - B.y)){
                    connector = VoxelUtility.GetGridIndex(new int3(aVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z));
                }
                else{
                    connector = VoxelUtility.GetGridIndex(new int3(aVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z));
                }
                voxels.TryAdd(connector, true);
                return;
            }

            if (aVoxelIndex.y == bVoxelIndex.y){
                /*xz plane neighbor */
                float2 yz = LinePlaneIntersect(A, B, 'x', 0.5f * (aVoxelCenter.x + bVoxelCenter.x));
                float y = yz.x;
                float z = yz.y;
                //Debug.Assert(A.y <= y && y <= B.y || B.y <= y && y <= A.y);
                int connector;
                if (math.abs(z - A.z) < math.abs(z - B.z)){
                    connector = VoxelUtility.GetGridIndex(new int3(bVoxelIndex.x, aVoxelIndex.y, aVoxelIndex.z));
                }
                else{
                    connector = VoxelUtility.GetGridIndex(new int3(aVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z));
                }
                voxels.TryAdd(connector, true);
                return;
            }

            if (aVoxelIndex.z == bVoxelIndex.z){
                /*xy plane neighbor */
                float2 xz = LinePlaneIntersect(A, B, 'y', 0.5f * (aVoxelCenter.y + bVoxelCenter.y));
                float x = xz.x;
                float z = xz.y;
                //Debug.Assert(A.z <= z && z <= B.z || B.z <= z && z <= A.z);
                int connector;
                if (math.abs(x - A.x) < math.abs(x - B.x)){
                    connector = VoxelUtility.GetGridIndex(new int3(aVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z));
                }
                else{
                    connector = VoxelUtility.GetGridIndex(new int3(bVoxelIndex.x, aVoxelIndex.y, aVoxelIndex.z));
                }
                voxels.TryAdd(connector, true);
                return;
            }

            /*3D diagonal */
            float2 _yz = LinePlaneIntersect(A, B, 'x', 0.5f * (aVoxelCenter.x + bVoxelCenter.x));
            if (RectContains(new float2(aVoxelCenter.y, aVoxelCenter.z), _yz)){
                /*baa */
                voxels.TryAdd(VoxelUtility.GetGridIndex(new int3(bVoxelIndex.x, aVoxelIndex.y, aVoxelIndex.z)), true);
            }
            else{
                if (RectContains(new float2(bVoxelCenter.y, bVoxelCenter.z), _yz)){
                    /*abb */
                    voxels.TryAdd(VoxelUtility.GetGridIndex(new int3(aVoxelIndex.x, bVoxelIndex.y, bVoxelIndex.z)), true);
                }
            }

            float2 _xz = LinePlaneIntersect(A, B, 'y', 0.5f * (aVoxelCenter.y + bVoxelCenter.y));
            if (RectContains(new float2(aVoxelCenter.x, aVoxelCenter.z), _xz)){
                /*aba */
                voxels.TryAdd(VoxelUtility.GetGridIndex(new int3(aVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z)), true);
            }
            else{
                if (RectContains(new float2(bVoxelCenter.x, bVoxelCenter.z), _xz)){
                    /*bab */
                    voxels.TryAdd(VoxelUtility.GetGridIndex(new int3(bVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z)), true);
                }
            }

            float2 _xy = LinePlaneIntersect(A, B, 'z', 0.5f * (aVoxelCenter.z + bVoxelCenter.z));
            if (RectContains(new float2(aVoxelCenter.x, aVoxelCenter.y), _xy)){
                /*aab */
                voxels.TryAdd(VoxelUtility.GetGridIndex(new int3(aVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z)), true);
            }
            else{
                if (RectContains(new float2(bVoxelCenter.x, bVoxelCenter.y), _xy)){
                    /*bba */
                    voxels.TryAdd(VoxelUtility.GetGridIndex(new int3(bVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z)), true);
                }
            }
        }

        /*Should guarantee: AB is not parallel to plane*/
        private float2 LinePlaneIntersect(float3 A, float3 B, char P, float PValue){
            float x1 = A.x;
            float y1 = A.y;
            float z1 = A.z;
            float x2 = B.x;
            float y2 = B.y;
            float z2 = B.z;

            switch(P){
                case 'x':
                    float fractionx = (PValue - x1) / (x2 - x1);
                    return new float2(fractionx * (y2 - y1) + y1, fractionx * (z2 - z1) + z1);
                case 'y':
                    float fractiony = (PValue - y1) / (y2 - y1);
                    return new float2(fractiony * (x2 - x1) + x1, fractiony * (z2 - z1) + z1);
                default :
                    float fractionz = (PValue - z1) / (z2 - z1);
                    return new float2(fractionz * (x2 - x1) + x1, fractionz * (y2 - y1) + y1);
            }
        }

        /*including boundary */
        private bool RectContains(float2 center, float2 point){
            float x_min = center.x - voxelSize * 0.5f;
            float x_max = center.x + voxelSize * 0.5f;
            float y_min = center.y - voxelSize * 0.5f;
            float y_max = center.y + voxelSize * 0.5f;
            return x_min <= point.x && point.x <= x_max && y_min <= point.y && point.y <= y_max;
        }
    }

    struct MoveVoxel : IJobForEachWithEntity<LocalToWorld>{
        [ReadOnly]
        public NativeArray<int> positionIndex; 
        public NativeArray<Entity> entity;
        public int baseIndex;
        public void Execute(Entity entity, int index, ref LocalToWorld localToWorld){
            if (index < baseIndex){
                return;
            }

            localToWorld = new LocalToWorld
            {
                Value = float4x4.TRS(
                    VoxelUtility.GetVoxelPosition(positionIndex[index]),
                    quaternion.identity,
                    new float3(voxelSize, voxelSize, voxelSize))
            };
        }
    }

    protected override void OnCreateManager(){
        modelPrefab = Resources.Load<GameObject>(modelName);
        modelMesh = ((MeshFilter)modelPrefab.GetComponentInChildren(typeof(MeshFilter))).sharedMesh;
        
        VoxelUtility.Init(modelMesh, voxelSize);
        VoxelUtility.Describe();
        
        float3[] f3Vertex = modelMesh.vertices.ToList().ConvertAll(input => new float3(input.x, input.y, input.z)).ToArray();
        var meshVertexs = new NativeArray<float3>(f3Vertex, Allocator.TempJob);
        int3[] i3Triangle = new int3[modelMesh.triangles.Length / 3];
        
        int[] tri = modelMesh.triangles;
        for (int i = 0; i != i3Triangle.Length; ++i){
            i3Triangle[i].x = tri[i * 3];
            i3Triangle[i].y = tri[i * 3 + 1];
            i3Triangle[i].z = tri[i * 3 + 2];
        }

        var meshTriangles = new NativeArray<int3>(i3Triangle, Allocator.TempJob);

        hashMap = new NativeHashMap<int,bool>(VoxelUtility.TotalGridsCount, Allocator.TempJob);

        var HashTriangleToVoxelJob = new HashTriangleToVoxel{
            vertexs = meshVertexs,
            triangles = meshTriangles,
            voxels = hashMap.ToConcurrent(),
        };

        HashTriangleToVoxelJob.Schedule(i3Triangle.Length, 64).Complete();

        meshVertexs.Dispose();
        meshTriangles.Dispose();
        
        voxelPrefab = Resources.Load<GameObject>("Voxel");

        posIndex = hashMap.GetKeyArray(Allocator.Persistent);
        hashMap.Dispose();

        Debug.Log(posIndex.Length + " hashed out of " + VoxelUtility.TotalGridsCount);
        Shuffle(posIndex);

        voxelGroup = GetEntityQuery(new EntityQueryDesc
        {
            All = new [] { ComponentType.ReadWrite<LocalToWorld>() },
            Options = EntityQueryOptions.Default
        });
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps){
        int incAmount = math.min((int)math.ceil(posIndex.Length / duration * Time.deltaTime), posIndex.Length - completedVoxel);

        if (incAmount == 0){
            Debug.Log("Finished");
            return inputDeps;
        }
        var entities = new NativeArray<Entity>(incAmount, Allocator.TempJob);
        EntityManager.Instantiate(voxelPrefab, entities);
        
        var moveJob = new MoveVoxel{
            positionIndex = posIndex,
            entity = entities,
            baseIndex = completedVoxel
        };
        
        var moveHandle = moveJob.Schedule(voxelGroup, inputDeps);
        moveHandle.Complete();

        completedVoxel += incAmount;
        entities.Dispose();
        return moveHandle;
    }

    protected override void OnDestroyManager(){
        posIndex.Dispose();
    }
    
    public static void Shuffle(NativeArray<int> list)  
    {  
        System.Random rng = new System.Random();
        int n = list.Length;  
        while (n > 1) {  
            n--;  
            int k = rng.Next(n + 1);  
            int value = list[k];  
            list[k] = list[n];  
            list[n] = value;  
        }  
    }

}
