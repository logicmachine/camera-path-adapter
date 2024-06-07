# Camera Path Adapter for VirtualLens2

An adapter module for [DX's Camera Path System](https://dx30.gumroad.com/l/CameraPath) and [VirtualLens2](https://logilabo.booth.pm/items/2280136).

## How to use
- Import and setup Camera Path System and VirtualLens2 for your avatar.
    - You have to use non-destructive setup for VirtualLens2.
- Install [Logilabo Avatar Tools VPM repository](https://vpm.logilabo.dev) and import "Camera Path Adapter".
- Put `Packages/Camera Path Adapter/CameraPathAdapter.prefab` into your avatar.
- Set properties of Camera Path Adapter component.
    - Virtual Lens Settings: VirtualLens Settings object created by its setup helper.
    - Camera Path Object: Camera Path object you have placed.
    - Enable Live Link: If you'd like to use LiveLink, you have to check it to synchronize VirtualLens2 parameters too.

