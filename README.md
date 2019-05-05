# Unity ECS-Style Voxelization
## Installation
- Clone [Unity Official ECS Samples](https://github.com/Unity-Technologies/EntityComponentSystemSamples).
- Clone this repository to `Samples/Assets` folder.

## Parameters overview
The following are adjustable parameters in `VoxelizationSystem`.
- `voxelSize` Voxelization resolution. Small value leads to higher memory use.
- `minDuration` A lower bound of duration. Used for small-scale voxelization to delay the spawning process.
- `maxMovingNumber` Maximum allowed number of simultaneously moving voxels. Smaller value leads to longer duration and better frame rate.
- `modelName` The name of model to be loaded in `Resources/` folder.
- `bottomColor` & `topColor` The color of bottom and top of voxelized model. The rest will be interpolated.
- `volumetric` Whether to perform volumetric voxelization (expansive).
- `randomOrder` Whether to randomize spawn order.

## Demo
Model Size (Bounding box): 11.7 * 20.7 * 3.8
### Surface voxelization
- Resolution: 0.1
- Resulting voxels count: 38k
- Time: < 1sec (with Core i7-8850H)

[![Voxel](https://img.youtube.com/vi/lYBrxwrWP3E/0.jpg)](https://www.youtube.com/watch?v=lYBrxwrWP3E)

### Volumetric voxelization with random initial position
- Resolution: 0.1
- Resulting voxels count: 140k
- Time: ~ 1sec (with Core i7-8850H)

[![Voxel](https://img.youtube.com/vi/RgVAQs-Cyos/0.jpg)](https://www.youtube.com/watch?v=RgVAQs-Cyos)
