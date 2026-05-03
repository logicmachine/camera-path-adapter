using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using dev.logilabo.camera_path_adapter.runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using VirtualLens2;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace dev.logilabo.camera_path_adapter.editor
{
    public class CameraPathAdapterPass : Pass<CameraPathAdapterPass>
    {
        private static T LoadAssetByGUID<T>(string guid) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
        
        private static void EditAnimatorController(
            VirtualAnimatorController controller, IDictionary<string, VirtualMotion> template)
        {
            VirtualMotion EditMotion(VirtualMotion motion)
            {
                if (template.TryGetValue(motion.Name, out var vm)) { return vm; }
                if (motion is VirtualBlendTree tree)
                {
                    foreach (var child in tree.Children) { child.Motion = EditMotion(child.Motion); }
                }
                return motion;
            }
            
            foreach (var layer in controller.Layers)
            {
                if (layer.StateMachine == null) { continue; }
                foreach (var state in layer.StateMachine.States)
                {
                    state.State.Motion = EditMotion(state.State.Motion);
                }
            }
        }
        
        private static void ModifyVirtualLens2(CameraPathAdapter config)
        {
            var virtualLens = config.virtualLensSettings.GetComponent<VirtualLensSettings>();
            var target = HierarchyUtility.PathToObject(config.cameraPathObject.transform, "Bezie 2/Camera");
            virtualLens.externalPoseSource = target;
            if (config.enableLiveLink)
            {
                virtualLens.synchronizeFocalLength = true;
            }
        }

        private static AnimatorController FilterAnimatorControllerLayer(AnimatorController controller,
            Predicate<AnimatorControllerLayer> predicate)
        {
            var result = new AnimatorController();
            result.parameters = controller.parameters;
            foreach (var layer in controller.layers)
            {
                if (predicate(layer)) { result.AddLayer(layer); }
            }
            return result;
        }

        private static VRCExpressionsMenu FilterVRCExpressionsMenu(VRCExpressionsMenu menu,
            Predicate<VRCExpressionsMenu.Control> predicate)
        {
            var result = Object.Instantiate(menu);
            result.controls = result.controls.Where(control => predicate(control)).ToList();
            return result;
        }

        private static void ModifyCameraPath(CameraPathAdapter config)
        {
            // - Remove animator controller layer `Fov`
            // - Add animator controller to expose parameter `Enable` and `Replace`
            // - Remove menu item `Fov`
            var guid = "5f81635a3749ee847aa13694406f2e72";
            var controllerPath = AssetDatabase.GUIDToAssetPath(guid);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            foreach (var component in config.cameraPathObject.GetComponents<Component>())
            {
                var so = new SerializedObject(component);
                if (so.targetObject.GetType().FullName != "VF.Model.VRCFury") { continue; }
                var content = so.FindProperty("content");
                if (content.propertyType != SerializedPropertyType.ManagedReference) { continue; }
                if (content.managedReferenceValue.GetType().FullName != "VF.Model.Feature.FullController") { continue; }
                {
                    var controllers = content.FindPropertyRelative("controllers");
                    // Modify existing controller
                    {
                        var item = controllers.GetArrayElementAtIndex(0);
                        var original =
                            (AnimatorController)item.FindPropertyRelative("controller.objRef").objectReferenceValue;
                        var filtered = FilterAnimatorControllerLayer(original, layer => layer.name != "Fov");
                        item.FindPropertyRelative("controller.objRef").objectReferenceValue = filtered;
                    }
                    // Add controller
                    {
                        var index = controllers.arraySize;
                        controllers.InsertArrayElementAtIndex(index);
                        var item = controllers.GetArrayElementAtIndex(index);
                        item.FindPropertyRelative("controller.version").intValue = 1;
                        item.FindPropertyRelative("controller.fileID").longValue = 0;
                        item.FindPropertyRelative("controller.id").stringValue = $"{guid}|{controllerPath}";
                        item.FindPropertyRelative("controller.objRef").objectReferenceValue = controller;
                        item.FindPropertyRelative("type").enumValueIndex = 5; // FX
                    }
                }
                {
                    // Modify existing menu item
                    var menus = content.FindPropertyRelative("menus");
                    var item = menus.GetArrayElementAtIndex(0);
                    var original =
                        (VRCExpressionsMenu)item.FindPropertyRelative("menu.objRef").objectReferenceValue;
                    var filtered = FilterVRCExpressionsMenu(original, control => control.name != "Fov");
                    item.FindPropertyRelative("menu.objRef").objectReferenceValue = filtered;
                }
                {
                    // Add global params
                    var globalParams = content.FindPropertyRelative("globalParams");
                    foreach (var name in new[] { "Enable", "Replace" })
                    {
                        var index = globalParams.arraySize;
                        globalParams.InsertArrayElementAtIndex(index);
                        var item = globalParams.GetArrayElementAtIndex(index);
                        item.stringValue = $"CameraPathAdapter/{name}";
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
        }

        private static VirtualClip CreateControlClip(
            BuildContext context, CameraPathAdapter config, bool path, bool replace, Material cameraMat)
        {
            var clip = VirtualClip.Create("");
            
            foreach (var renderer in config.cameraPathObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var shaderName = renderer?.material?.shader?.name;
                if (shaderName is "Custom/OnlyCamera V2" or "Unlit/Path Camera")
                {
                    clip.SetFloatCurve(
                        HierarchyUtility.RelativePath(context.AvatarRootObject, renderer.gameObject),
                        typeof(SkinnedMeshRenderer), "m_Enabled",
                        new AnimationCurve(new Keyframe(0.0f, path ? 1.0f : 0.0f)));
                }
            }
            
            var replaceObject = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie Replace");
            clip.SetFloatCurve(
                HierarchyUtility.RelativePath(context.AvatarRootObject, replaceObject),
                typeof(MeshRenderer), "m_Enabled",
                new AnimationCurve(new Keyframe(0.0f, replace ? 1.0f : 0.0f)));
            
            var cameraObject = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie 2/Camera/Camera");
            clip.SetObjectCurve(
                HierarchyUtility.RelativePath(context.AvatarRootObject, cameraObject),
                typeof(SkinnedMeshRenderer), "m_Materials.Array.data[0]", 
                new[] { new ObjectReferenceKeyframe { time = 0.0f, value = cameraMat } });

            if (config.enableLiveLink)
            {
                var captureRoot = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie 2/Camera");
                var captureObjects = captureRoot.GetComponentsInChildren<Camera>(true);
                foreach (var captureObject in captureObjects)
                {
                    clip.SetFloatCurve(
                        HierarchyUtility.RelativePath(context.AvatarRootObject, captureObject.gameObject),
                        typeof(Camera), "m_Enabled",
                        new AnimationCurve(new Keyframe(0.0f, replace ? 1.0f : 0.0f)));
                }
            }

            return clip;
        }
        
        private static float Fov2Zoom(float fov)
        {
            return Mathf.Tan(30.0f * Mathf.Deg2Rad) / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        }
        
        private static float Focal2Fov(float focal)
        {
            return 2.0f * Mathf.Atan(10.125f / focal) * Mathf.Rad2Deg;
        }
        
        private static float Focal2Zoom(float focal)
        {
            return Fov2Zoom(Focal2Fov(focal));
        }
        
        private static float Zoom2Fov(float zoom)
        {
            return 2.0f * Mathf.Atan(Mathf.Tan(30.0f * Mathf.Deg2Rad) / zoom) * Mathf.Rad2Deg;
        }

        private static VirtualClip CreateZoomClip(BuildContext context, CameraPathAdapter config)
        {
            const int numZoomSteps = 100;
            var virtualLens = config.virtualLensSettings.GetComponent<VirtualLensSettings>();
            var values = new List<float>();
            var minLogZoom = Mathf.Log(Focal2Zoom(virtualLens.minFocalLength));
            var maxLogZoom = Mathf.Log(Focal2Zoom(virtualLens.maxFocalLength));
            for (int i = 0; i <= numZoomSteps; ++i)
            {
                var x = (float)i / numZoomSteps;
                var logZoom = minLogZoom + (maxLogZoom - minLogZoom) * x;
                values.Add(Zoom2Fov(Mathf.Exp(logZoom)));
            }

            var clip = VirtualClip.Create("");
            var keyframes = new List<Keyframe>();
            for (int i = 0; i <= numZoomSteps; ++i)
            {
                keyframes.Add(new Keyframe(i, values[i]));
            }
            var captureRoot = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie 2/Camera");
            var captureObjects = captureRoot.GetComponentsInChildren<Camera>(true);
            foreach (var captureObject in captureObjects)
            {
                clip.SetFloatCurve(
                    HierarchyUtility.RelativePath(context.AvatarRootObject, captureObject.gameObject),
                    typeof(Camera), "field of view",
                    new AnimationCurve(keyframes.ToArray()));
            }

            return clip;
        }

        private static void ModifyAnimations(BuildContext context, CameraPathAdapter config)
        {
            // _DX/Camera Path/Assets/Material/Camera Preview.mat
            var remoteCameraPreviewMat = LoadAssetByGUID<Material>("c65b680a97b35f744a667c70c058f8eb");
            // Materials/LocalCameraPreview.mat
            var localCameraPreviewMat = LoadAssetByGUID<Material>("2bd6dfad3972f4449aacaca830bee9d1");
            
            var template = new Dictionary<string, VirtualMotion>
            {
                { "LocalDisable", CreateControlClip(context, config, true, false, localCameraPreviewMat) },
                { "LocalEnable", CreateControlClip(context, config, true, false, localCameraPreviewMat) },
                { "MirrorDisable", CreateControlClip(context, config, false, false, localCameraPreviewMat) },
                { "MirrorEnable", CreateControlClip(context, config, false, false, localCameraPreviewMat) },
                { "RemoteDisable", CreateControlClip(context, config, true, false, remoteCameraPreviewMat) },
                { "RemoteEnable", CreateControlClip(context, config, true, true, remoteCameraPreviewMat) }
            };
            if (config.enableLiveLink)
            {
                template.Add(
                    "Zoom",
                    CreateZoomClip(context, config));
            }

            var vac = context.Extension<VirtualControllerContext>();
            foreach (var mergeAnimator in config.GetComponents<ModularAvatarMergeAnimator>())
            {
                if (vac.Controllers.TryGetValue(mergeAnimator, out var animator))
                {
                    EditAnimatorController(animator, template);
                }
            }
        }
        
        private static void ExecuteSingle(BuildContext context, CameraPathAdapter config)
        {
            // TODO validate configurations
            ModifyVirtualLens2(config);
            ModifyCameraPath(config);
            ModifyAnimations(context, config);
        }

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<CameraPathAdapter>();
            foreach (var config in components)
            {
                ExecuteSingle(context, config);
                Object.DestroyImmediate(config);
            }
        }
    }
}
