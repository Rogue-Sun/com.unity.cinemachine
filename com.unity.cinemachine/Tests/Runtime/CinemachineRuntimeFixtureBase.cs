using System.Collections;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    public class CinemachineRuntimeFixtureBase : CinemachineFixtureBase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            
            // force a uniform deltaTime, otherwise tests will be unstable
            CinemachineCore.UniformDeltaTimeOverride = 0.1f;
            
            // disable delta time compensation for deterministic test results
            CinemachineCore.FrameDeltaCompensationEnabled = false;
        }
        
        [TearDown]
        public override void TearDown()
        {
            CinemachineCore.UniformDeltaTimeOverride = -1f;
            CinemachineCore.FrameDeltaCompensationEnabled = true;
            
            base.TearDown();
        }
        
        /// <summary>Ensures to wait until at least one physics frame.</summary>
        protected static IEnumerator WaitForOnePhysicsFrame()
        {
            yield return new WaitForFixedUpdate(); // this is needed to ensure physics system is up-to-date
            yield return null; // this is so that the frame is completed (since physics frames are not aligned)
        }
    }
}
