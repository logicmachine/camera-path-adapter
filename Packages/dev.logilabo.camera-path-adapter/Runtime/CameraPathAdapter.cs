using UnityEngine;
using VRC.SDKBase;

namespace dev.logilabo.camera_path_adapter.runtime
{
    [AddComponentMenu("Logilabo Avatar Tools/Camera Path Adapter")]
    [DisallowMultipleComponent]
    public class CameraPathAdapter : MonoBehaviour, IEditorOnly
    {
        public GameObject virtualLensSettings;
        public GameObject cameraPathObject;
        public bool enableLiveLink;
    }
}