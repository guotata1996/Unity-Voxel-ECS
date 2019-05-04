using System;
using Unity.Mathematics;
using Unity.Entities;

[Serializable]
public struct Voxel : IComponentData
{
    public float3 targetPosition;
}

public class VoxelProxy : ComponentDataProxy<Voxel> { }
