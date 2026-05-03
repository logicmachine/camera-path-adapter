using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

[assembly: ExportsPlugin(typeof(dev.logilabo.camera_path_adapter.editor.PluginDefinition))]

// ReSharper disable once CheckNamespace
namespace dev.logilabo.camera_path_adapter.editor
{
    public class PluginDefinition : Plugin<PluginDefinition>
    {
        public override string QualifiedName => "dev.logilabo.camera-path-adapter";
        public override string DisplayName => "Camera Path Adapter";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("dev.logilabo.virtuallens2.apply-non-destructive")
                .WithRequiredExtension(typeof(VirtualControllerContext), sequence =>
                {
                    sequence.Run(CameraPathAdapterPass.Instance);
                });
        }
    }
}
