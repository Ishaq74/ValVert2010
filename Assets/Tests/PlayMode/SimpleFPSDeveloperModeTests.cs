using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SimpleFPSDeveloperModeTests
{
    [UnityTest]
    public IEnumerator FlightIsOnlyAvailableInsideDeveloperMode()
    {
        ValVertServices.ResetForDomain();
        var player = new GameObject("DevExplorerTest");
        var controller = player.AddComponent<CharacterController>();
        var camera = new GameObject("Camera");
        camera.transform.SetParent(player.transform, false);
        var explorer = player.AddComponent<SimpleFPS>();
        explorer.cam = camera.transform;

        try
        {
            yield return null;
            Assert.IsFalse(explorer.DeveloperModeActive);
            explorer.ToggleFlightMode();
            Assert.IsFalse(explorer.IsFlying, "flight cannot be enabled outside dev mode");

            explorer.ToggleDeveloperMode();
            Assert.IsTrue(explorer.DeveloperModeActive);
            explorer.ToggleFlightMode();
            Assert.IsTrue(explorer.IsFlying);
            Assert.IsFalse(controller.enabled);

            explorer.ToggleDeveloperMode();
            Assert.IsFalse(explorer.DeveloperModeActive);
            Assert.IsFalse(explorer.IsFlying, "leaving dev mode exits flight");
            Assert.IsTrue(controller.enabled);
        }
        finally { Object.Destroy(player); }
    }
}
