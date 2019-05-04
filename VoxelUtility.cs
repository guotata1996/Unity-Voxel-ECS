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

    public static int3 PosToIndex3(float3 position){
        return new int3(
                (int)(math.floor((position.x - min_x) / voxelSize)),
                (int)(math.floor((position.y - min_y) / voxelSize)),
                (int)(math.floor((position.z - min_z) / voxelSize))
            );
    }
    
    /*Since we have rather dense mesh, this is okay for hashing*/
    public static int PosToIndex1(float3 position){
        int3 gridIndex3 = PosToIndex3(position);
        return Index3ToIndex1(gridIndex3);
    }

    public static int Index3ToIndex1(int3 gridIndex3){
        return gridIndex3.x * TotalGrids.y * TotalGrids.z + gridIndex3.y * TotalGrids.z + gridIndex3.z;
    }

    public static int GetSurfaceIndex(int3 grid1index3, int3 grid2index3){
        int3 diff = grid1index3 - grid2index3;
        Debug.Assert(math.abs(diff.x) + math.abs(diff.y) + math.abs(diff.z) == 1);

        //normal direction along X
        if (grid1index3.x < grid2index3.x){
            return 0 * TotalGridsCount + Index3ToIndex1(grid1index3);
        }
        if (grid1index3.x > grid2index3.x){
            return 0 * TotalGridsCount + Index3ToIndex1(grid2index3);
        }

        //normal direction along Y
        if (grid1index3.y < grid2index3.y){
            return 1 * TotalGridsCount + Index3ToIndex1(grid1index3);
        }
        if (grid1index3.y > grid2index3.y){
            return 1 * TotalGridsCount + Index3ToIndex1(grid2index3);
        }

        //normal direction along Z
        if (grid1index3.z < grid2index3.z){
            return 2 * TotalGridsCount + Index3ToIndex1(grid1index3);
        }
        return 2 * TotalGridsCount + Index3ToIndex1(grid2index3);
    }

    public static float3 getFacePosition(int faceIndex){
        int3 va, vb;
        (va, vb) = SurfaceIndexToVoxels(faceIndex);
        return 0.5f * (Index3ToPos(va) + Index3ToPos(vb));
    }

    public static (int3, int3) SurfaceIndexToVoxels(int faceIndex){
        int normalDirection = faceIndex / TotalGridsCount;
        int smallerGridIndex = faceIndex % TotalGridsCount;
        int3 smallerGridIndex3 = Index1ToIndex3(smallerGridIndex);
        int3 biggerGridIndex3;
        switch(normalDirection){
            case 0:
                biggerGridIndex3 = smallerGridIndex3 + new int3(1,0,0);
                break;
            case 1:
                biggerGridIndex3 = smallerGridIndex3 + new int3(0,1,0);
                break;
            default:
                biggerGridIndex3 = smallerGridIndex3 + new int3(0,0,1);
                break;
        }
        return (smallerGridIndex3, biggerGridIndex3);
    }

    public static int3 CalculateTotalGrids{
        get{
            return PosToIndex3(new float3(max_x, max_y, max_z)) - PosToIndex3(new float3(min_x, min_y, min_z))
             + new int3(1,1,1);
        }
    }

    public static int TotalGridsCount{
        get{
            return CalculateTotalGrids.x * CalculateTotalGrids.y * CalculateTotalGrids.z;
        }
    }

    /*returns the center position*/
    public static float3 Index1ToPos(int voxelIndex){
        return Index3ToPos(Index1ToIndex3(voxelIndex));
    }

    public static int3 Index1ToIndex3(int voxelIndex){
        int x_index = voxelIndex / (TotalGrids.y * TotalGrids.z);
        voxelIndex -= x_index * (TotalGrids.y * TotalGrids.z);
        int y_index = voxelIndex / TotalGrids.z;
        int z_index = voxelIndex - y_index * TotalGrids.z;
        return new int3(x_index, y_index, z_index);
    }

    public static float3 Index3ToPos(int3 index3){
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

    public static float3 BBoxCenter{
        get{
            return new float3((max_x + min_x) / 2, (max_y + min_y) / 2, (max_z + min_z) / 2);
        }
    }

    public static float DiagonalLength{
        get{
            return math.distance(new float3(min_x, min_y, min_z), new float3(max_x, max_y, max_z));
        }
    }
}
