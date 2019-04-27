using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;

public class VoxelUtility
{
    static float min_x = float.MaxValue, max_x = float.MinValue, 
    min_y = float.MaxValue, max_y = float.MinValue, 
    min_z = float.MaxValue, max_z = float.MinValue;
    
    static float voxelSize;

    static int3 TotalGrids;

    public static void Init(Mesh mesh, float _voxelSize){
        var vPositions = mesh.vertices;
        foreach(var p in vPositions){
            min_x = math.min(p.x, min_x);
            max_x = math.max(p.x, max_x);
            min_y = math.min(p.y, min_y);
            max_y = math.max(p.y, max_y);
            min_z = math.min(p.z, min_z);
            max_z = math.max(p.z, max_z);
        }
        voxelSize = _voxelSize;
        TotalGrids = CalculateTotalGrids;
    }

    public static int3 GetGrid(float3 position){
        return new int3(
                (int)(math.floor((position.x - min_x) / voxelSize)),
                (int)(math.floor((position.y - min_y) / voxelSize)),
                (int)(math.floor((position.z - min_z) / voxelSize))
            );
    }
    
    /*Since we have rather dense mesh, this is okay for hashing*/
    public static int GetGridIndex(float3 position){
        int3 gridIndex3 = GetGrid(position);
        return gridIndex3.x * TotalGrids.y * TotalGrids.z + gridIndex3.y * TotalGrids.z + gridIndex3.z;
    }

    public static int GetGridIndex(int3 gridIndex3){
        return gridIndex3.x * TotalGrids.y * TotalGrids.z + gridIndex3.y * TotalGrids.z + gridIndex3.z;
    }

    private static int3 CalculateTotalGrids{
        get{
            return GetGrid(new float3(max_x, max_y, max_z)) - GetGrid(new float3(min_x, min_y, min_z))
             + new int3(1,1,1);
        }
    }

    public static int TotalGridsCount{
        get{
            return CalculateTotalGrids.x * CalculateTotalGrids.y * CalculateTotalGrids.z;
        }
    }

    /*returns the center position*/
    public static float3 GetVoxelPosition(int voxelIndex){
        int x_index = voxelIndex / (TotalGrids.y * TotalGrids.z);
        voxelIndex -= x_index * (TotalGrids.y * TotalGrids.z);
        int y_index = voxelIndex / TotalGrids.z;
        int z_index = voxelIndex - y_index * TotalGrids.z;
        return GetVoxelPosition(new int3(x_index, y_index, z_index));
    }

    public static float3 GetVoxelPosition(int3 index3){
        return new float3(
            min_x + (index3.x + 0.5f) * voxelSize,
            min_y + (index3.y + 0.5f) * voxelSize,
            min_z + (index3.z + 0.5f) * voxelSize
        );
    }

    public static void Describe(){
        Debug.Log("Xrange " + min_x + "~" + max_x + "\nYRange " + min_y + "~" + max_y + 
        "\nZrange " + min_z + "~" + max_z + "\ntotalGrids " + TotalGrids);
    }
}
