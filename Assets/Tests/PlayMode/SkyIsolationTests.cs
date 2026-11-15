using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Play Mode must not write into the project's sky materials (tracked assets): ValVertClock drives runtime copies.</summary>
public class SkyIsolationTests
{
    Material _src; GameObject _go; Material _previous;

    [SetUp] public void SetUp()
    {
        _previous = RenderSettings.skybox;
        var sh = Shader.Find("Skybox/Panoramic") ?? Shader.Find("Skybox/Procedural");
        Assert.IsNotNull(sh, "a skybox shader exists");
        _src = new Material(sh);
        if (_src.HasProperty("_Exposure")) _src.SetFloat("_Exposure", 1.3f);
        if (_src.HasProperty("_Tint")) _src.SetColor("_Tint", new Color(.5f, .5f, .5f, 1f));
        RenderSettings.skybox = _src;
    }

    [TearDown] public void TearDown()
    {
        if (_go) Object.DestroyImmediate(_go);
        var moon = GameObject.Find("Moon"); if (moon) Object.DestroyImmediate(moon);
        RenderSettings.skybox = _previous;
        if (_src) Object.DestroyImmediate(_src);
    }

    [UnityTest] public IEnumerator ClockNeverMutatesTheSourceSkyMaterial_AndRestoresItOnDestroy()
    {
        float exposure = _src.GetFloat("_Exposure"); Color tint = _src.GetColor("_Tint");
        _go = new GameObject("ClockTest");
        var clock = _go.AddComponent<ValVertClock>(); clock.running = false;   // daySkybox defaults to the current RenderSettings.skybox
        yield return null;
        foreach (var hour in new[] { 3f, 12f, 21f }) { clock.Set(7, 14, hour); yield return null; }

        Assert.AreEqual(exposure, _src.GetFloat("_Exposure"), 1e-6f, "source exposure untouched");
        Assert.AreEqual(tint, _src.GetColor("_Tint"), "source tint untouched");
        Assert.AreNotSame(_src, RenderSettings.skybox, "the scene uses a runtime copy");
        Assert.Greater(Mathf.Abs(exposure - RenderSettings.skybox.GetFloat("_Exposure")), 1e-3f, "the copy is the one that gets animated (night)");

        Object.DestroyImmediate(_go); _go = null;
        Assert.AreSame(_src, RenderSettings.skybox, "original sky restored when the clock goes away");
    }
}
