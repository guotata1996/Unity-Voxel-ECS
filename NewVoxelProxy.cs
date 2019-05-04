using System;
using Unity.Entities;

public struct NewVoxel : IComponentData { }

[UnityEngine.DisallowMultipleComponent]
public class NewVoxelProxy : ComponentDataProxy<NewVoxel> { }
