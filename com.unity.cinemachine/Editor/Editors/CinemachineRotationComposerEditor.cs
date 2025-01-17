using UnityEditor;
using Cinemachine.Utility;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Cinemachine.Editor
{
    [CustomEditor(typeof(CinemachineRotationComposer))]
    [CanEditMultipleObjects]
    class CinemachineRotationComposerEditor : UnityEditor.Editor
    {
        CmPipelineComponentInspectorUtility m_PipelineUtility;
        GameViewComposerGuides m_GameViewGuides = new();

        CinemachineRotationComposer Target => target as CinemachineRotationComposer;

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
   
            CinemachineSceneToolUtility.RegisterTool(typeof(TrackedObjectOffsetTool));
        }

        protected virtual void OnDisable()
        {
            m_PipelineUtility.OnDisable();
            m_GameViewGuides.OnDisable();
            CinemachineDebug.OnGUIHandlers -= OnGuiHandler;
            if (CinemachineCorePrefs.ShowInGameGuides.Value)
                InspectorUtility.RepaintGameView();
  
            CinemachineSceneToolUtility.UnregisterTool(typeof(TrackedObjectOffsetTool));
        }

        public override VisualElement CreateInspectorGUI()
        {
            var ux = new VisualElement();

            m_PipelineUtility.AddMissingCmCameraHelpBox(ux, CmPipelineComponentInspectorUtility.RequiredTargets.LookAt);
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.TrackedObjectOffset)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.Damping)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.CenterOnActivate)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.Composition)));
            ux.Add(new PropertyField(serializedObject.FindProperty(() => Target.Lookahead)));
            m_PipelineUtility.UpdateState();
            return ux;
        }

        protected virtual void OnGuiHandler(CinemachineBrain brain)
        {
            // Draw the camera guides
            if (Target == null || !CinemachineCorePrefs.ShowInGameGuides.Value || !Target.isActiveAndEnabled)
                return;

            // Don't draw the guides if rendering to texture
            if (brain == null || (brain.OutputCamera.activeTexture != null && CinemachineCore.Instance.BrainCount > 1))
                return;

            var vcam = Target.VirtualCamera;
            if (!brain.IsValidChannel(vcam))
                return;

            // Screen guides
            bool isLive = targets.Length <= 1 && brain.IsLive(vcam, true);
            m_GameViewGuides.OnGUI_DrawGuides(isLive, brain.OutputCamera, vcam.State.Lens);

            // Draw an on-screen gizmo for the target
            if (Target.LookAtTarget != null && isLive)
            {
                CmPipelineComponentInspectorUtility.OnGUI_DrawOnscreenTargetMarker(
                    null, Target.TrackedPoint, 
                    vcam.State.GetFinalOrientation(), brain.OutputCamera);
            }
        }

        void OnSceneGUI()
        {
            var composer = Target;
            if (composer == null || !composer.IsValid)
                return;

            if (CinemachineSceneToolUtility.IsToolActive(typeof(TrackedObjectOffsetTool)))
            {
                CinemachineSceneToolHelpers.TrackedObjectOffsetTool(
                    Target.VirtualCamera, 
                    new SerializedObject(Target).FindProperty(() => Target.TrackedObjectOffset),
                    CinemachineCore.Stage.Aim);
            }
        }
    }
}
