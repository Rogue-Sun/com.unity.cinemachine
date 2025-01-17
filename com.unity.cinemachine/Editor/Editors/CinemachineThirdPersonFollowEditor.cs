using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Cinemachine.Editor
{
    [CustomEditor(typeof(CinemachineThirdPersonFollow))]
    [CanEditMultipleObjects]
    class CinemachineThirdPersonFollowEditor : UnityEditor.Editor
    {
        CinemachineThirdPersonFollow Target => target as CinemachineThirdPersonFollow;
        CmPipelineComponentInspectorUtility m_PipelineUtility;
        
        protected virtual void OnEnable()
        {
            m_PipelineUtility = new CmPipelineComponentInspectorUtility(this);
            CinemachineSceneToolUtility.RegisterTool(typeof(FollowOffsetTool));
        }

        protected virtual void OnDisable()
        {
            m_PipelineUtility.OnDisable();
            CinemachineSceneToolUtility.UnregisterTool(typeof(FollowOffsetTool));
        }
        
        public override VisualElement CreateInspectorGUI()
        {
            var ux = new VisualElement();

            m_PipelineUtility.AddMissingCmCameraHelpBox(ux, CmPipelineComponentInspectorUtility.RequiredTargets.Follow);
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.Damping)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.ShoulderOffset)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.VerticalArmLength)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.CameraSide)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.CameraDistance)));
#if CINEMACHINE_PHYSICS
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.AvoidObstacles)));
#endif

            m_PipelineUtility.UpdateState();
            return ux;
        }
        
        [DrawGizmo(GizmoType.Active | GizmoType.Selected, typeof(CinemachineThirdPersonFollow))]
        static void DrawThirdPersonGizmos(CinemachineThirdPersonFollow target, GizmoType selectionType)
        {
            if (CinemachineSceneToolUtility.IsToolActive(typeof(FollowOffsetTool)))
                return; // don't draw gizmo when using handles
            
            if (target.IsValid)
            {
                var isLive = CinemachineCore.Instance.IsLive(target.VirtualCamera);
                Color originalGizmoColour = Gizmos.color;
                Gizmos.color = isLive
                    ? CinemachineCorePrefs.ActiveGizmoColour.Value
                    : CinemachineCorePrefs.InactiveGizmoColour.Value;

                target.GetRigPositions(out Vector3 root, out Vector3 shoulder, out Vector3 hand);
                Gizmos.DrawLine(root, shoulder);
                Gizmos.DrawLine(shoulder, hand);
                Gizmos.DrawLine(hand, target.VirtualCamera.State.RawPosition);
                
                var sphereRadius = 0.1f;
                Gizmos.DrawSphere(root, sphereRadius);
                Gizmos.DrawSphere(shoulder, sphereRadius);
#if CINEMACHINE_PHYSICS
                sphereRadius = target.AvoidObstacles.Enabled ? target.AvoidObstacles.CameraRadius : sphereRadius;
#endif
                Gizmos.DrawSphere(hand, sphereRadius);
                Gizmos.DrawSphere(target.VirtualCamera.State.RawPosition, sphereRadius);

                Gizmos.color = originalGizmoColour;
            }
        }
        
        void OnSceneGUI()
        {
            var thirdPerson = Target;
            if (thirdPerson == null || !thirdPerson.IsValid)
                return;

            if (CinemachineSceneToolUtility.IsToolActive(typeof(FollowOffsetTool)))
            {
                var originalColor = Handles.color;
                
                thirdPerson.GetRigPositions(out var followTargetPosition, out var shoulderPosition, 
                    out var armPosition);
                var followTargetRotation = thirdPerson.FollowTargetRotation;
                var targetForward = followTargetRotation * Vector3.forward;
                var heading = CinemachineThirdPersonFollow.GetHeading(
                    followTargetRotation, thirdPerson.VirtualCamera.State.ReferenceUp);

                EditorGUI.BeginChangeCheck();

                // shoulder handle
                var sHandleIds = Handles.PositionHandleIds.@default;
                var newShoulderPosition = Handles.PositionHandle(sHandleIds, shoulderPosition, heading);

                Handles.color = Handles.preselectionColor;
                // arm handle
                var followUp = followTargetRotation * Vector3.up;
                var aHandleId = GUIUtility.GetControlID(FocusType.Passive); 
                var newArmPosition = Handles.Slider(aHandleId, armPosition, followUp, 
                    CinemachineSceneToolHelpers.CubeHandleCapSize(armPosition), Handles.CubeHandleCap, 0.5f);

                // cam distance handle
                var camDistance = thirdPerson.CameraDistance;
                var camPos = armPosition - targetForward * camDistance;
                var cdHandleId = GUIUtility.GetControlID(FocusType.Passive);
                var newCamPos = Handles.Slider(cdHandleId, camPos, targetForward, 
                    CinemachineSceneToolHelpers.CubeHandleCapSize(camPos), Handles.CubeHandleCap, 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    // Modify via SerializedProperty for OnValidate to get called automatically, and scene repainting too
                    var so = new SerializedObject(thirdPerson);
                    
                    var shoulderOffset = so.FindProperty(() => thirdPerson.ShoulderOffset);
                    shoulderOffset.vector3Value += 
                        CinemachineSceneToolHelpers.PositionHandleDelta(heading, newShoulderPosition, shoulderPosition);
                    var verticalArmLength = so.FindProperty(() => thirdPerson.VerticalArmLength);
                    verticalArmLength.floatValue += 
                        CinemachineSceneToolHelpers.SliderHandleDelta(newArmPosition, armPosition, followUp);
                    var cameraDistance = so.FindProperty(() => thirdPerson.CameraDistance);
                    cameraDistance.floatValue -= 
                        CinemachineSceneToolHelpers.SliderHandleDelta(newCamPos, camPos, targetForward);
                    
                    so.ApplyModifiedProperties();
                }

                var isDragged = IsHandleDragged(sHandleIds.x, sHandleIds.xyz, shoulderPosition, "Shoulder Offset " 
                    + thirdPerson.ShoulderOffset.ToString("F1"), followTargetPosition, shoulderPosition);
                isDragged |= IsHandleDragged(aHandleId, aHandleId, armPosition, "Vertical Arm Length (" 
                    + thirdPerson.VerticalArmLength.ToString("F1") + ")", shoulderPosition, armPosition);
                isDragged |= IsHandleDragged(cdHandleId, cdHandleId, camPos, "Camera Distance (" 
                    + camDistance.ToString("F1") + ")", armPosition, camPos);

                CinemachineSceneToolHelpers.SoloOnDrag(isDragged, thirdPerson.VirtualCamera, sHandleIds.xyz);

                Handles.color = originalColor;
            }
            
            // local function that draws label and guide lines, and returns true if a handle has been dragged
            static bool IsHandleDragged
                (int handleMinId, int handleMaxId, Vector3 labelPos, string text, Vector3 lineStart, Vector3 lineEnd)
            {
                var handleIsDragged = handleMinId <= GUIUtility.hotControl && GUIUtility.hotControl <= handleMaxId;
                var handleIsDraggedOrHovered = handleIsDragged ||
                    (handleMinId <= HandleUtility.nearestControl && HandleUtility.nearestControl <= handleMaxId);

                if (handleIsDraggedOrHovered)
                    CinemachineSceneToolHelpers.DrawLabel(labelPos, text);
                    
                Handles.color = handleIsDraggedOrHovered ? 
                    Handles.selectedColor : CinemachineSceneToolHelpers.HelperLineDefaultColor;
                    Handles.DrawLine(lineStart, lineEnd, CinemachineSceneToolHelpers.LineThickness);
                    
                return handleIsDragged;
            }
        }
    }
}

