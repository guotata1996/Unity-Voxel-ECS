﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using System.Linq;

[AlwaysUpdateSystem]
public class VoxelizationSystem : JobComponentSystem
{
    /*Setup */
    protected Mesh modelMesh;
    protected GameObject voxelPrefab, modelPrefab;
    protected VoxelUtility voxelUtility;
    protected NativeArray<int> surfacePosArray, volumePosArray;
    protected NativeHashMap<int,bool> hashMap;
    
    /*Parameters*/
    const float voxelSize = 0.1f;
    const float minDuration = 5.0f;
    const int maxMovingNumber = 25000;
    public static readonly string modelName = "Human";
    readonly Color bottomColor = Color.red;
    readonly Color topColor = Color.grey;
    const bool volumetric = false;
    readonly bool randomOrder = false;


    /*Runtime */
    Unity.Mathematics.Random randomGenerater;

    EntityQuery newGroup, movingGroup, finishedGroup;

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
            int3 aVoxelIndex = VoxelUtility.PosToIndex3(A);
            int3 bVoxelIndex = VoxelUtility.PosToIndex3(B);
            int aVoxelIndex1 = VoxelUtility.Index3ToIndex1(aVoxelIndex);
            int bVoxeklndex1 = VoxelUtility.Index3ToIndex1(bVoxelIndex);
            float3 aVoxelCenter = VoxelUtility.Index3ToPos(aVoxelIndex);
            float3 bVoxelCenter = VoxelUtility.Index3ToPos(bVoxelIndex);

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
                    connector = VoxelUtility.Index3ToIndex1(new int3(aVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z));
                }
                else{
                    connector = VoxelUtility.Index3ToIndex1(new int3(aVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z));
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
                    connector = VoxelUtility.Index3ToIndex1(new int3(bVoxelIndex.x, aVoxelIndex.y, aVoxelIndex.z));
                }
                else{
                    connector = VoxelUtility.Index3ToIndex1(new int3(aVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z));
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
                    connector = VoxelUtility.Index3ToIndex1(new int3(aVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z));
                }
                else{
                    connector = VoxelUtility.Index3ToIndex1(new int3(bVoxelIndex.x, aVoxelIndex.y, aVoxelIndex.z));
                }
                voxels.TryAdd(connector, true);
                return;
            }

            /*3D diagonal */
            float2 _yz = LinePlaneIntersect(A, B, 'x', 0.5f * (aVoxelCenter.x + bVoxelCenter.x));
            if (RectContains(new float2(aVoxelCenter.y, aVoxelCenter.z), _yz)){
                /*baa */
                voxels.TryAdd(VoxelUtility.Index3ToIndex1(new int3(bVoxelIndex.x, aVoxelIndex.y, aVoxelIndex.z)), true);
            }
            else{
                if (RectContains(new float2(bVoxelCenter.y, bVoxelCenter.z), _yz)){
                    /*abb */
                    voxels.TryAdd(VoxelUtility.Index3ToIndex1(new int3(aVoxelIndex.x, bVoxelIndex.y, bVoxelIndex.z)), true);
                }
            }

            float2 _xz = LinePlaneIntersect(A, B, 'y', 0.5f * (aVoxelCenter.y + bVoxelCenter.y));
            if (RectContains(new float2(aVoxelCenter.x, aVoxelCenter.z), _xz)){
                /*aba */
                voxels.TryAdd(VoxelUtility.Index3ToIndex1(new int3(aVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z)), true);
            }
            else{
                if (RectContains(new float2(bVoxelCenter.x, bVoxelCenter.z), _xz)){
                    /*bab */
                    voxels.TryAdd(VoxelUtility.Index3ToIndex1(new int3(bVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z)), true);
                }
            }

            float2 _xy = LinePlaneIntersect(A, B, 'z', 0.5f * (aVoxelCenter.z + bVoxelCenter.z));
            if (RectContains(new float2(aVoxelCenter.x, aVoxelCenter.y), _xy)){
                /*aab */
                voxels.TryAdd(VoxelUtility.Index3ToIndex1(new int3(aVoxelIndex.x, aVoxelIndex.y, bVoxelIndex.z)), true);
            }
            else{
                if (RectContains(new float2(bVoxelCenter.x, bVoxelCenter.y), _xy)){
                    /*bba */
                    voxels.TryAdd(VoxelUtility.Index3ToIndex1(new int3(bVoxelIndex.x, bVoxelIndex.y, aVoxelIndex.z)), true);
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
    
    struct MoveVoxel : IJobForEachWithEntity<LocalToWorld, Voxel>{
        public NativeHashMap<Entity, bool>.Concurrent finishedOutput;
        public float deltaTime;
        public float tolerance;
        public Unity.Mathematics.Random lerpSpeed;
        public void Execute(Entity entity, int index, ref LocalToWorld localToWorld, [ReadOnly]ref Voxel voxel){
            float3 p = localToWorld.Position;
            float3 targetP = voxel.targetPosition;
            Quaternion r = Quaternion.LookRotation(localToWorld.Forward, localToWorld.Up);
            Quaternion targetR = Quaternion.identity;
            float speed = lerpSpeed.NextFloat(1f, 10f);

            localToWorld = new LocalToWorld
            {
                Value = float4x4.TRS(
                    math.lerp(p, targetP, deltaTime * speed),
                    Quaternion.Lerp(r, targetR, deltaTime * speed),
                    new float3(voxelSize, voxelSize, voxelSize))
            };
            
            if (math.distance(p, targetP) < tolerance){
                finishedOutput.TryAdd(entity, true);
            }
        }
    }

    struct HashSurfaceVoxelUnCommonFace : IJobParallelFor{
        [ReadOnly]
        public NativeHashMap<int, bool> surfaceVoxels;
        [ReadOnly]
        public NativeArray<int> surfaceVoxelsIndex;

        public NativeHashMap<int, bool>.Concurrent surfaceFaces;
        public void Execute(int index){
            int position = surfaceVoxelsIndex[index];
            int3 position3 = VoxelUtility.Index1ToIndex3(position);
            Debug.Assert(surfaceVoxels.TryGetValue(position, out bool v));

            int noNeighborCount_Debug = 0;

            int3[] neighborPosition3 = new int3[]{
                position3 + new int3(1,0,0),
                position3 + new int3(-1,0,0),
                position3 + new int3(0,1,0),
                position3 + new int3(0,-1,0),
                position3 + new int3(0,0,1),
                position3 + new int3(0,0,-1)};

            foreach(var neighborPos in neighborPosition3){
                if (!surfaceVoxels.TryGetValue(VoxelUtility.Index3ToIndex1(neighborPos), out bool v1)){
                    int surface = VoxelUtility.GetSurfaceIndex(position3, neighborPos);
                    surfaceFaces.TryAdd(surface, true);
                    noNeighborCount_Debug++;
                }
            }

            if (noNeighborCount_Debug == 6){
                Debug.LogError("No neighboring voxel for " + position3);
            }
        }
    }

    void RemoveOneConsecutiveSurface(ref NativeHashMap<int, bool> hashmap){
        NativeHashMap<int, bool> removedSurface = new NativeHashMap<int, bool>(hashmap.Capacity, Allocator.TempJob);

        var keys = hashmap.GetKeyArray(Allocator.TempJob);
        int index1 = keys[0];
        keys.Dispose();

        Queue<int> toBeRemoved = new Queue<int>();
        toBeRemoved.Enqueue(index1);
        //Debug.Log("Removal starts from " + VoxelUtility.getFacePosition(index1));

        while (toBeRemoved.Count > 0){
            int workingIndex = toBeRemoved.Dequeue();
            hashmap.Remove(workingIndex);
            removedSurface.TryAdd(workingIndex, true);
            int3 voxel1, voxel2;
            (voxel1, voxel2) = VoxelUtility.SurfaceIndexToVoxels(workingIndex);
            
            int3[] neighborInc = new int3[4];
            if (voxel1.x != voxel2.x){
                neighborInc[0] = new int3(0,1,0);
                neighborInc[1] = new int3(0,-1,0);
                neighborInc[2] = new int3(0,0,1);
                neighborInc[3] = new int3(0,0,-1);
            }
            if (voxel1.y != voxel2.y){
                neighborInc[0] = new int3(1,0,0);
                neighborInc[1] = new int3(-1,0,0);
                neighborInc[2] = new int3(0,0,1);
                neighborInc[3] = new int3(0,0,-1);
            }
            if (voxel1.z != voxel2.z){
                neighborInc[0] = new int3(1,0,0);
                neighborInc[1] = new int3(-1,0,0);
                neighborInc[2] = new int3(0,1,0);
                neighborInc[3] = new int3(0,-1,0);
            }
            foreach (int3 voxel in new int3[]{voxel1, voxel2}){
                foreach (int3 inc in neighborInc){
                    int3 candidateNeighbor = voxel + inc;
                    int candidateFace = VoxelUtility.GetSurfaceIndex(voxel, candidateNeighbor);
                    if (hashmap.TryGetValue(candidateFace, out bool v)){
                        if (v){
                            // only enqueue newly discovered neighbor
                            toBeRemoved.Enqueue(candidateFace);

                            // set face's flag to False: it has already be put into queue
                            hashmap.Remove(candidateFace);
                            hashmap.TryAdd(candidateFace, false);
                        }
                    }
                }
            }
            foreach(int3 inc in neighborInc){
                int3 voxel1Offseted = voxel1 + inc;
                int3 voxel2Offseted = voxel2 + inc;
                int candidateFace = VoxelUtility.GetSurfaceIndex(voxel1Offseted, voxel2Offseted);
                if (hashmap.TryGetValue(candidateFace, out bool v)){
                    if (v){
                        // only enqueue newly discovered neighbor
                        toBeRemoved.Enqueue(candidateFace);

                        // set face's flag to False: it has already be put into queue
                        hashmap.Remove(candidateFace);
                        hashmap.TryAdd(candidateFace, false);
                    }
                }
            }
        }
        if (hashmap.Length < removedSurface.Length){
            hashmap.Dispose();
            hashmap = removedSurface;
        }
        else{
            removedSurface.Dispose();
        }
    }

    struct CastRayToVolume : IJobParallelFor{
        [ReadOnly] 
        public NativeHashMap<int, bool> surfaceFaces;
        public NativeHashMap<int, bool>.Concurrent volumeVoxels;
        public int3 totalGridCount;
        public void Execute(int index){
            int xGrid = index / totalGridCount.y; //[0,totalGridCount.x)
            int yGrid = index % totalGridCount.y;

            bool inside = false;
            for (int z = 0; z != totalGridCount.z - 1; ++z){
                int faceIndex = VoxelUtility.GetSurfaceIndex(new int3(xGrid, yGrid, z), new int3(xGrid, yGrid, z+1));
                if (surfaceFaces.TryGetValue(faceIndex, out bool v)){
                    inside = !inside;
                }
                if (inside){
                    volumeVoxels.TryAdd(VoxelUtility.Index3ToIndex1(new int3(xGrid, yGrid, z + 1)), true);
                }
            }
        }
        
    }

    protected override void OnCreateManager(){
        //Comment this in Build version
        // UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));

        randomGenerater = new Unity.Mathematics.Random(1);

        modelPrefab = Resources.Load<GameObject>(modelName);
        modelMesh = ((MeshFilter)modelPrefab.GetComponentInChildren(typeof(MeshFilter))).sharedMesh;
        
        VoxelUtility.Init(modelMesh, voxelSize, bottomColor, topColor);
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

        JobHandle toSurfaceVoxelHandle = HashTriangleToVoxelJob.Schedule(i3Triangle.Length, 64);
        toSurfaceVoxelHandle.Complete();
        surfacePosArray = hashMap.GetKeyArray(Allocator.Persistent); // Length = # surface voxels

        /*End of step a */
        NativeHashMap<int, bool> surfaceHashMap = new NativeHashMap<int, bool>(VoxelUtility.TotalGridsCount * 3, Allocator.TempJob);
        var HashVoxelCommonFaceJob = new HashSurfaceVoxelUnCommonFace{
            surfaceVoxels = hashMap,
            surfaceVoxelsIndex = surfacePosArray,
            surfaceFaces = surfaceHashMap.ToConcurrent()
        };
        JobHandle toSurfaceFaceHandle = HashVoxelCommonFaceJob.Schedule(surfacePosArray.Length, 64, toSurfaceVoxelHandle);
                
        toSurfaceFaceHandle.Complete();

        Debug.Log("# Face Before removal: NumFace = " + surfaceHashMap.Length + " \nNumV= " + hashMap.Length);
        RemoveOneConsecutiveSurface(ref surfaceHashMap);
        Debug.Log("Faces Count After removal: " + surfaceHashMap.Length);

        /*End of step c*/
        int3 totalGridsCount = VoxelUtility.CalculateTotalGrids;
        NativeHashMap<int, bool> volumeHashMap = new NativeHashMap<int, bool>(VoxelUtility.TotalGridsCount, Allocator.TempJob);
        var castRayToVolumeJob = new CastRayToVolume{
            surfaceFaces = surfaceHashMap,
            volumeVoxels = volumeHashMap.ToConcurrent(),
            totalGridCount = totalGridsCount
        };
        JobHandle castRayHandle = castRayToVolumeJob.Schedule(totalGridsCount.x * totalGridsCount.y, 64, toSurfaceFaceHandle);
        castRayHandle.Complete();

        volumePosArray = volumeHashMap.GetKeyArray(Allocator.Persistent);
        Debug.Log("Surface voxels Count: " + surfacePosArray.Length);
        Debug.Log("Volume voxels Count: " + volumePosArray.Length);

        meshVertexs.Dispose();
        meshTriangles.Dispose();
        hashMap.Dispose();
        surfaceHashMap.Dispose();
        volumeHashMap.Dispose();

        if (randomOrder){
            Shuffle(surfacePosArray);
            Shuffle(volumePosArray);
        }
        voxelPrefab = Resources.Load<GameObject>("Voxel");

        newGroup = GetEntityQuery(new EntityQueryDesc
        {
            All = new [] { ComponentType.ReadWrite<Voxel>(), ComponentType.ReadWrite<LocalToWorld>(), ComponentType.ReadWrite<NewVoxel>() },
            Options = EntityQueryOptions.Default
        });

        movingGroup = GetEntityQuery(new EntityQueryDesc
        {
            All = new [] { ComponentType.ReadWrite<Voxel>(), ComponentType.ReadWrite<LocalToWorld>(), ComponentType.ReadWrite<MovingVoxel>() },
            Options = EntityQueryOptions.Default
        });

        finishedGroup = GetEntityQuery(new EntityQueryDesc
        {
            All = new [] {ComponentType.ReadWrite<Voxel>(), ComponentType.ReadWrite<LocalToWorld>()},
            None = new ComponentType[] {ComponentType.ReadWrite<MovingVoxel>(), ComponentType.ReadWrite<NewVoxel>()},
            Options = EntityQueryOptions.Default
        });
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps){
        var voxelArray = volumetric ? volumePosArray : surfacePosArray;

        int existingCount = newGroup.CalculateLength() + movingGroup.CalculateLength() + finishedGroup.CalculateLength();
        int incAmount = math.min((int)math.ceil(voxelArray.Length / minDuration * Time.deltaTime), voxelArray.Length - existingCount);

        //Debug.Log(finishedGroup.CalculateLength() + " finished");

        if (finishedGroup.CalculateLength() == voxelArray.Length){
            Debug.Log("Finished");
            return inputDeps;
        }

        //When there's too much moving job, stop adding new objects
        if (movingGroup.CalculateLength() >= maxMovingNumber){
            incAmount = 0;
        }

        // Move
        NativeHashMap<Entity, bool> reachedTargetVoxel = new NativeHashMap<Entity, bool>(movingGroup.CalculateLength(), Allocator.TempJob);
        var moveJob = new MoveVoxel{
            finishedOutput = reachedTargetVoxel.ToConcurrent(),
            deltaTime = Time.deltaTime,
            lerpSpeed = randomGenerater,
            tolerance = voxelSize * 0.1f,
        };
        JobHandle moveHandle = moveJob.Schedule(movingGroup, inputDeps);
        moveHandle.Complete();

        // Spawn
        var entities = new NativeArray<Entity>(incAmount, Allocator.TempJob);
        EntityManager.Instantiate(voxelPrefab, entities);
        var spawnPositions = new NativeArray<float3>(incAmount, Allocator.TempJob);
        GeneratePoints.RandomPointsInUnitSphere(spawnPositions);

        int spawnBase = finishedGroup.CalculateLength() + movingGroup.CalculateLength();
        for(int i = 0; i != incAmount; ++i){
            RenderMesh m = EntityManager.GetSharedComponentData<RenderMesh>(entities[i]);
            m.material = VoxelUtility.GetMaterial(VoxelUtility.Index1ToPos(voxelArray[i + spawnBase]).y);
            EntityManager.SetSharedComponentData(entities[i], m);

            EntityManager.SetComponentData(entities[i], new Voxel{
                targetPosition = VoxelUtility.Index1ToPos(voxelArray[i + spawnBase])
            });
            
            Vector3 rdnCenter = spawnPositions[i];
            float3 rdnCenterf = VoxelUtility.BBoxCenter + (float3)rdnCenter * VoxelUtility.DiagonalLength;

            var localToWorld = new LocalToWorld
            {
                Value = float4x4.TRS(
                    rdnCenterf,
                    quaternion.Euler(
                        UnityEngine.Random.Range(0.0f, 360f),
                        UnityEngine.Random.Range(0.0f, 360f),
                        UnityEngine.Random.Range(0.0f, 360f)),
                    new float3(voxelSize, voxelSize, voxelSize))
            };

            EntityManager.SetComponentData(entities[i], localToWorld);
        }

        EntityManager.RemoveComponent(entities, ComponentType.ReadWrite<NewVoxel>());
        EntityManager.AddComponent(entities, ComponentType.ReadWrite<MovingVoxel>());

        var justReachedVoxels = reachedTargetVoxel.GetKeyArray(Allocator.TempJob);
        EntityManager.RemoveComponent(justReachedVoxels, ComponentType.ReadWrite<MovingVoxel>());

        entities.Dispose();
        reachedTargetVoxel.Dispose();
        justReachedVoxels.Dispose();
        spawnPositions.Dispose();
        
        return moveHandle;

    }

    protected override void OnDestroyManager(){
        surfacePosArray.Dispose();
        volumePosArray.Dispose();
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
