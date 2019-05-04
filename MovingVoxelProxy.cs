using System;
using Unity.Entities;

public struct MovingVoxel : IComponentData { }

[UnityEngine.DisallowMultipleComponent]
public class MovingVoxelProxy : ComponentDataProxy<MovingVoxel> { }
