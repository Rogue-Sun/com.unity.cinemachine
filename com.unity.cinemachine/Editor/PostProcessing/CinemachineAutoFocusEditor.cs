using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;

namespace Cinemachine.Editor
{
    [CustomEditor(typeof(CinemachineAutoFocus))]
    class CinemachineAutoFocusEditor : UnityEditor.Editor
    {
#if !CINEMACHINE_HDRP
        public override VisualElement CreateInspectorGUI()
        {
            var ux = new VisualElement();
            ux.AddChild(new HelpBox("This component is only valid for HDRP projects.", HelpBoxMessageType.Warning));
            return ux;
        }
#else
        CinemachineAutoFocus Target => target as CinemachineAutoFocus;

        const string k_ComputeShaderName = "CinemachineFocusDistanceCompute";

        public override VisualElement CreateInspectorGUI()
        {
            var ux = new VisualElement();
            ux.AddChild(new HelpBox(
                "Note: focus control requires an active Volume containing a Depth Of Field override "
                    + "having Focus Mode activated and set to Physical Camera, "
                    + "and Focus Distance Mode activated and set to Camera", 
                HelpBoxMessageType.Info));

            var focusTargetProp = serializedObject.FindProperty(() => Target.FocusTarget);
            ux.Add(new PropertyField(focusTargetProp));
            var customTarget = ux.AddChild(new PropertyField(serializedObject.FindProperty(() => Target.CustomTarget)));
            var offset = ux.AddChild(new PropertyField(serializedObject.FindProperty(() => Target.FocusDepthOffset)));
            var damping = ux.AddChild(new PropertyField(serializedObject.FindProperty(() => Target.Damping)));
            var radius = ux.AddChild(new PropertyField(serializedObject.FindProperty(() => Target.AutoDetectionRadius)));

            var computeShaderProp = serializedObject.FindProperty("m_ComputeShader");
            var shaderDisplay = ux.AddChild(new PropertyField(computeShaderProp));
            shaderDisplay.SetEnabled(false);

            var importHelp = ux.AddChild(InspectorUtility.CreateHelpBoxWithButton(
                $"The {k_ComputeShaderName} shader needs to be imported into "
                    + "the project. It will be imported by default into the Assets root.  "
                    + "After importing, you can move it elsewhere but don't rename it.",
                    HelpBoxMessageType.Warning,
                "Import\nShader", () =>
            {
                // Check if it's already imported, just in case
                var shader = FindShader();
                if (shader == null)
                {
                    // Import the asset from the package
                    var shaderAssetPath = $"Assets/{k_ComputeShaderName}.compute";
                    FileUtil.CopyFileOrDirectory(
                        $"{ScriptableObjectUtility.kPackageRoot}/Runtime/PostProcessing/HDRP Resources~/{k_ComputeShaderName}.compute",
                        shaderAssetPath);
                    AssetDatabase.ImportAsset(shaderAssetPath);
                    shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderAssetPath);
                }
                AssignShaderToTarget(shader);
            }));

            TrackFocusTarget(focusTargetProp);
            ux.TrackPropertyValue(focusTargetProp, TrackFocusTarget);

            void TrackFocusTarget(SerializedProperty p)
            {
                var mode = (CinemachineAutoFocus.FocusTrackingMode)p.intValue;
                customTarget.SetVisible(mode == CinemachineAutoFocus.FocusTrackingMode.CustomTarget);
                offset.SetVisible(mode != CinemachineAutoFocus.FocusTrackingMode.None);
                damping.SetVisible(mode != CinemachineAutoFocus.FocusTrackingMode.None);
                radius.SetVisible(mode == CinemachineAutoFocus.FocusTrackingMode.ScreenCenter);
                shaderDisplay.SetVisible(mode == CinemachineAutoFocus.FocusTrackingMode.ScreenCenter);
                bool importHelpVisible = false;
                if (mode == CinemachineAutoFocus.FocusTrackingMode.ScreenCenter && computeShaderProp.objectReferenceValue == null)
                {
                    var shader = FindShader(); // slow!!!!
                    if (shader != null)
                        AssignShaderToTarget(shader);
                    else
                        importHelpVisible = true;
                }
                importHelp.SetVisible(importHelpVisible);
            }

            // Make the import box disappear after import
            ux.TrackPropertyValue(computeShaderProp, (p) =>
            {
                var mode = (CinemachineAutoFocus.FocusTrackingMode)focusTargetProp.intValue;
                importHelp.SetVisible(mode == CinemachineAutoFocus.FocusTrackingMode.ScreenCenter
                    && p.objectReferenceValue == null);
            });

            return ux;
        }

        static ComputeShader FindShader()
        {
            var guids = AssetDatabase.FindAssets($"{k_ComputeShaderName}, t:ComputeShader", new [] { "Assets" });
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<ComputeShader>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }

        void AssignShaderToTarget(ComputeShader shader)
        {
            for (int i = 0; i < targets.Length; ++i)
            {
                var t = new SerializedObject(targets[i]);
                t.FindProperty("m_ComputeShader").objectReferenceValue = shader;
                t.ApplyModifiedProperties();
            }
        }
#endif
    }
}