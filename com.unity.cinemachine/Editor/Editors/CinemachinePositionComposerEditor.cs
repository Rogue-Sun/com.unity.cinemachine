using UnityEngine;
using UnityEditor;
using Cinemachine.Utility;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Cinemachine.Editor
{
    [CustomEditor(typeof(CinemachinePositionComposer))]
    [CanEditMultipleObjects]
    class CinemachinePositionComposerEditor : UnityEditor.Editor
    {
        CmPipelineComponentInspectorUtility m_PipelineUtility;
        GameViewComposerGuides m_GameViewGuides = new();

        CinemachinePositionComposer Target => target as CinemachinePositionComposer;

        protected virtual void OnEnable()
        {
            m_PipelineUtility = new (this);
            m_GameViewGuides.GetComposition = () => Target.Composition;
            m_GameViewGuides.SetComposition = (s) => Target.Composition = s;
            m_GameViewGuides.Target = () => serializedObject;
            m_GameViewGuides.OnEnable();

            CinemachineDebug.OnGUIHandlers -= OnGuiHandler;
            CinemachineDebug.OnGUIHandlers += OnGuiHandler;
            if (CinemachineCorePrefs.ShowInGameGuides.Value)
                InspectorUtility.RepaintGameView();
            
            CinemachineSceneToolUtility.RegisterTool(typeof(FollowOffsetTool));
            CinemachineSceneToolUtility.RegisterTool(typeof(TrackedObjectOffsetTool));
        }

        protected virtual void OnDisable()
        {
            m_PipelineUtility.OnDisable();
            m_GameViewGuides.OnDisable();
            CinemachineDebug.OnGUIHandlers -= OnGuiHandler;
            if (CinemachineCorePrefs.ShowInGameGuides.Value)
                InspectorUtility.RepaintGameView();

            CinemachineSceneToolUtility.UnregisterTool(typeof(FollowOffsetTool));
            CinemachineSceneToolUtility.UnregisterTool(typeof(TrackedObjectOffsetTool));
        }

        public override VisualElement CreateInspectorGUI()
        {
            var ux = new VisualElement();

            m_PipelineUtility.AddMissingCmCameraHelpBox(ux, CmPipelineComponentInspectorUtility.RequiredTargets.Follow);
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.TrackedObjectOffset)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.Damping)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.CameraDistance)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.DeadZoneDepth)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.CenterOnActivate)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.Composition)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.Lookahead)));

            m_PipelineUtility.UpdateState();
            return ux;
        }

        protected virtual void OnGuiHandler(CinemachineBrain brain)
        {
            if (Target == null || !CinemachineCorePrefs.ShowInGameGuides.Value || !Target.isActiveAndEnabled)
                return;

            if (brain == null || (brain.OutputCamera.activeTexture != null && CinemachineCore.Instance.BrainCount > 1))
                return;

            var vcam = Target.VirtualCamera;
            if (!brain.IsValidChannel(vcam))
                return;

            // Screen guides
            bool isLive = targets.Length <= 1 && brain.IsLive(vcam, true);
            m_GameViewGuides.OnGUI_DrawGuides(isLive, brain.OutputCamera, vcam.State.Lens);

            // Draw an on-screen gizmo for the target
            if (Target.FollowTarget != null && isLive)
            {
                CmPipelineComponentInspectorUtility.OnGUI_DrawOnscreenTargetMarker(
                    null, Target.TrackedPoint, 
                    vcam.State.GetFinalOrientation(), brain.OutputCamera);
            }
        }

        void OnSceneGUI()
        {
            if (Target == null || !Target.IsValid)
                return;
            
            if (CinemachineSceneToolUtility.IsToolActive(typeof(TrackedObjectOffsetTool)))
            {
                CinemachineSceneToolHelpers.TrackedObjectOffsetTool(
                    Target.VirtualCamera, 
                    new SerializedObject(Target).FindProperty(() => Target.TrackedObjectOffset),
                    CinemachineCore.Stage.Body);
            }
            else if (CinemachineSceneToolUtility.IsToolActive(typeof(FollowOffsetTool)))
            {
                var property = new SerializedObject(Target).FindProperty(() => Target.CameraDistance);

                var originalColor = Handles.color;
                var camPos = Target.VcamState.RawPosition;
                var targetForward = Target.VirtualCamera.State.GetFinalOrientation() * Vector3.forward;
                EditorGUI.BeginChangeCheck();
                Handles.color = CinemachineSceneToolHelpers.HelperLineDefaultColor;
                var cdHandleId = GUIUtility.GetControlID(FocusType.Passive);
                var newHandlePosition = Handles.Slider(cdHandleId, camPos, targetForward,
                    CinemachineSceneToolHelpers.CubeHandleCapSize(camPos), Handles.CubeHandleCap, 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    // Modify via SerializedProperty for OnValidate to get called automatically, and scene repainting too
                    property.floatValue -= CinemachineSceneToolHelpers.SliderHandleDelta(newHandlePosition, camPos, targetForward);
                    property.serializedObject.ApplyModifiedProperties();
                }

                var isDragged = GUIUtility.hotControl == cdHandleId;
                var isDraggedOrHovered = isDragged || HandleUtility.nearestControl == cdHandleId;
                if (isDraggedOrHovered)
                {
                    CinemachineSceneToolHelpers.DrawLabel(camPos, 
                        property.displayName + " (" + Target.CameraDistance.ToString("F1") + ")");
                }
                
                Handles.color = isDraggedOrHovered ? 
                    Handles.selectedColor : CinemachineSceneToolHelpers.HelperLineDefaultColor;
                Handles.DrawLine(camPos, Target.FollowTargetPosition + Target.TrackedObjectOffset);

                CinemachineSceneToolHelpers.SoloOnDrag(isDragged, Target.VirtualCamera, cdHandleId);
                
                Handles.color = originalColor;
            }
        }
    }
}
