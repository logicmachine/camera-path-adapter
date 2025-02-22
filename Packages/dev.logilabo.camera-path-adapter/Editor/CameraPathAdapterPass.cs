using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using dev.logilabo.camera_path_adapter.runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
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
        
        private static AnimatorController CloneAnimatorController(
            AnimatorController controller, IDictionary<Motion, Motion> template)
        {
            Motion DuplicateMotion(Motion src)
            {
                if (template.TryGetValue(src, out var motion)) { return motion; }
                if (src is BlendTree tree)
                {
                    var result = new BlendTree()
                    {
                        hideFlags = tree.hideFlags,
                        name = tree.name,
                        blendParameter = tree.blendParameter,
                        blendParameterY = tree.blendParameterY,
                        blendType = tree.blendType,
                        maxThreshold = tree.maxThreshold,
                        minThreshold = tree.minThreshold,
                        useAutomaticThresholds = tree.useAutomaticThresholds
                    };
                    result.children = tree.children
                        .Select(c => new ChildMotion()
                        {
                            mirror = c.mirror,
                            motion = DuplicateMotion(c.motion),
                            cycleOffset = c.cycleOffset,
                            directBlendParameter = c.directBlendParameter,
                            position = c.position,
                            threshold = c.threshold,
                            timeScale = c.timeScale
                        })
                        .ToArray();
                    return result;
                }
                return src;
            }

            StateMachineBehaviour DuplicateBehaviour(StateMachineBehaviour src)
            {
                var result = Object.Instantiate(src);
                result.name = src.name;
                result.hideFlags = src.hideFlags;
                return result;
            }

            AnimatorState DuplicateState(AnimatorState src)
            {
                var result = new AnimatorState()
                {
                    name = src.name,
                    hideFlags = src.hideFlags,
                    behaviours = src.behaviours.Select(DuplicateBehaviour).ToArray(),
                    cycleOffset = src.cycleOffset,
                    cycleOffsetParameter = src.cycleOffsetParameter,
                    cycleOffsetParameterActive = src.cycleOffsetParameterActive,
                    iKOnFeet = src.iKOnFeet,
                    mirror = src.mirror,
                    mirrorParameter = src.mirrorParameter,
                    mirrorParameterActive = src.mirrorParameterActive,
                    motion = DuplicateMotion(src.motion),
                    speed = src.speed,
                    speedParameter = src.speedParameter,
                    speedParameterActive = src.speedParameterActive,
                    tag = src.tag,
                    timeParameter = src.timeParameter,
                    timeParameterActive = src.timeParameterActive,
                    transitions = new AnimatorStateTransition[] { },
                    writeDefaultValues = src.writeDefaultValues
                };
                return result;
            }

            AnimatorStateTransition DuplicateStateTransition(
                AnimatorStateTransition src,
                IDictionary<AnimatorState, AnimatorState> stateMap,
                IDictionary<AnimatorStateMachine, AnimatorStateMachine> stateMachineMap)
            {
                var state = src.destinationState ? stateMap[src.destinationState] : null;
                var stateMachine = src.destinationStateMachine ? stateMachineMap[src.destinationStateMachine] : null;
                var result = new AnimatorStateTransition()
                {
                    conditions = (AnimatorCondition[]) src.conditions.Clone(),
                    destinationState = state,
                    destinationStateMachine = stateMachine,
                    isExit = src.isExit,
                    mute = src.mute,
                    solo = src.solo,
                    hideFlags = src.hideFlags,
                    name = src.name,
                    canTransitionToSelf = src.canTransitionToSelf,
                    duration = src.duration,
                    exitTime = src.exitTime,
                    hasExitTime = src.hasExitTime,
                    hasFixedDuration = src.hasFixedDuration,
                    interruptionSource = src.interruptionSource,
                    offset = src.offset,
                    orderedInterruption = src.orderedInterruption
                };
                return result;
            }

            AnimatorTransition DuplicateTransition(
                AnimatorTransition src,
                IDictionary<AnimatorState, AnimatorState> stateMap,
                IDictionary<AnimatorStateMachine, AnimatorStateMachine> stateMachineMap)
            {
                var state = src.destinationState ? stateMap[src.destinationState] : null;
                var stateMachine = src.destinationStateMachine ? stateMachineMap[src.destinationStateMachine] : null;
                var result = new AnimatorTransition()
                {
                    conditions = (AnimatorCondition[]) src.conditions.Clone(),
                    destinationState = state,
                    destinationStateMachine = stateMachine,
                    isExit = src.isExit,
                    mute = src.mute,
                    solo = src.solo,
                    hideFlags = src.hideFlags,
                    name = src.name,
                };
                return result;
            }

            AnimatorStateMachine DuplicateStateMachine(AnimatorStateMachine src)
            {
                var stateMap = new Dictionary<AnimatorState, AnimatorState>(
                    new ReferenceEqualityComparer<AnimatorState>());
                var states = new List<ChildAnimatorState>();
                foreach (var s in src.states)
                {
                    var state = DuplicateState(s.state);
                    states.Add(new ChildAnimatorState() {position = s.position, state = state});
                    stateMap.Add(s.state, state);
                }
                var stateMachineMap = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>(
                    new ReferenceEqualityComparer<AnimatorStateMachine>());
                var stateMachines = new List<ChildAnimatorStateMachine>();
                foreach (var s in src.stateMachines)
                {
                    var stateMachine = DuplicateStateMachine(s.stateMachine);
                    stateMachines.Add(new ChildAnimatorStateMachine()
                    {
                        position = s.position,
                        stateMachine = stateMachine
                    });
                    stateMachineMap.Add(s.stateMachine, stateMachine);
                }
                foreach (var e in stateMap)
                {
                    e.Value.transitions = e.Key.transitions
                        .Select(t => DuplicateStateTransition(t, stateMap, stateMachineMap))
                        .ToArray();
                }
                var dst = new AnimatorStateMachine()
                {
                    name = src.name,
                    hideFlags = src.hideFlags,
                    anyStatePosition = src.anyStatePosition,
                    anyStateTransitions = src.anyStateTransitions
                        .Select(t => DuplicateStateTransition(t, stateMap, stateMachineMap))
                        .ToArray(),
                    behaviours = src.behaviours.Select(DuplicateBehaviour).ToArray(),
                    defaultState = src.defaultState ? stateMap[src.defaultState] : null,
                    entryPosition = src.entryPosition,
                    entryTransitions = src.entryTransitions
                        .Select(t => DuplicateTransition(t, stateMap, stateMachineMap))
                        .ToArray(),
                    exitPosition = src.exitPosition,
                    parentStateMachinePosition = src.parentStateMachinePosition,
                    stateMachines = stateMachines.ToArray(),
                    states = states.ToArray()
                };
                return dst;
            }

            AnimatorController clone = new AnimatorController();
            foreach (var parameter in controller.parameters)
            {
                clone.AddParameter(new AnimatorControllerParameter
                {
                    defaultBool = parameter.defaultBool,
                    defaultFloat = parameter.defaultFloat,
                    defaultInt = parameter.defaultInt,
                    name = parameter.name,
                    type = parameter.type
                });
            }
            foreach (var src in controller.layers)
            {
                var dst = new AnimatorControllerLayer()
                {
                    name = src.name,
                    avatarMask = src.avatarMask,
                    blendingMode = src.blendingMode,
                    defaultWeight = src.defaultWeight,
                    iKPass = src.iKPass,
                    stateMachine = DuplicateStateMachine(src.stateMachine),
                    syncedLayerAffectsTiming = src.syncedLayerAffectsTiming,
                    syncedLayerIndex = src.syncedLayerIndex
                };
                clone.AddLayer(dst);
            }
            return clone;
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

        private static AnimationClip CreateControlClip(
            BuildContext context, CameraPathAdapter config, bool path, bool replace, Material cameraMat)
        {
            var clip = new AnimationClip();
            
            foreach (var renderer in config.cameraPathObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var shaderName = renderer?.material?.shader?.name;
                if (shaderName is "Custom/OnlyCamera V2" or "Unlit/Path Camera")
                {
                    clip.SetCurve(
                        HierarchyUtility.RelativePath(context.AvatarRootObject, renderer.gameObject),
                        typeof(SkinnedMeshRenderer), "m_Enabled",
                        new AnimationCurve(new Keyframe(0.0f, path ? 1.0f : 0.0f)));
                }
            }
            
            var replaceObject = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie Replace");
            clip.SetCurve(
                HierarchyUtility.RelativePath(context.AvatarRootObject, replaceObject),
                typeof(MeshRenderer), "m_Enabled",
                new AnimationCurve(new Keyframe(0.0f, replace ? 1.0f : 0.0f)));
            
            var cameraObject = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie 2/Camera/Camera");
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                new EditorCurveBinding
                {
                    path = HierarchyUtility.RelativePath(context.AvatarRootObject, cameraObject),
                    propertyName = "m_Materials.Array.data[0]", type = typeof(SkinnedMeshRenderer)
                },
                new[] { new ObjectReferenceKeyframe { time = 0.0f, value = cameraMat } });

            if (config.enableLiveLink)
            {
                var captureRoot = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie 2/Camera");
                var captureObjects = captureRoot.GetComponentsInChildren<Camera>(true);
                foreach (var captureObject in captureObjects)
                {
                    clip.SetCurve(
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

        private static AnimationClip CreateZoomClip(BuildContext context, CameraPathAdapter config)
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

            var clip = new AnimationClip();
            var keyframes = new List<Keyframe>();
            for (int i = 0; i <= numZoomSteps; ++i)
            {
                keyframes.Add(new Keyframe(i, values[i]));
            }
            var captureRoot = HierarchyUtility.PathToObject(config.cameraPathObject, "Bezie 2/Camera");
            var captureObjects = captureRoot.GetComponentsInChildren<Camera>(true);
            foreach (var captureObject in captureObjects)
            {
                clip.SetCurve(
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
            
            IDictionary<Motion, Motion> template = new Dictionary<Motion, Motion>();
            // Animations/Placeholders/LocalDisable.anim
            template.Add(
                LoadAssetByGUID<AnimationClip>("9ef91486cc2589641ae77ecd8c78a9a2"),
                CreateControlClip(context, config, true, false, localCameraPreviewMat));
            // Animations/Placeholders/LocalEnable.anim
            template.Add(
                LoadAssetByGUID<AnimationClip>("54d3e6668a74cb5449177c2010be70c3"),
                CreateControlClip(context, config, true, false, localCameraPreviewMat));
            // Animations/Placeholders/MirrorDisable.anim
            template.Add(
                LoadAssetByGUID<AnimationClip>("e648e9dcc9776194182b36ac9e1faca7"),
                CreateControlClip(context, config, false, false, localCameraPreviewMat));
            // Animations/Placeholders/MirrorEnable.anim
            template.Add(
                LoadAssetByGUID<AnimationClip>("7331351476cbfeb488be5ae56eeca392"),
                CreateControlClip(context, config, false, false, localCameraPreviewMat));
            // Animations/Placeholders/RemoteDisable.anim
            template.Add(
                LoadAssetByGUID<AnimationClip>("52d5a9e8c42514a4681b15b7e0a7c96c"),
                CreateControlClip(context, config, true, false, remoteCameraPreviewMat));
            // Animations/Placeholders/RemoteEnable.anim
            template.Add(
                LoadAssetByGUID<AnimationClip>("6908817be32960b4aaa4ff23214d296d"),
                CreateControlClip(context, config, true, true, remoteCameraPreviewMat));
            // Animations/Placeholders/Zoom.anim
            if (config.enableLiveLink)
            {
                template.Add(
                    LoadAssetByGUID<AnimationClip>("a52c5016cb1e95f448aeb3f05c649367"),
                    CreateZoomClip(context, config));
            }

            foreach (var mergeAnimator in config.GetComponents<ModularAvatarMergeAnimator>())
            {
                mergeAnimator.animator = CloneAnimatorController(
                    (AnimatorController)mergeAnimator.animator, template);
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
