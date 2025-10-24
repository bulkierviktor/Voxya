using NUnit.Framework;
using Voxya.Voxel.Core;

public class NoiseTests
{
    [Test]
    public void OpenSimplex_Deterministic()
    {
        var n1 = new OpenSimplex2D(); n1.SetSeed(123);
        var n2 = new OpenSimplex2D(); n2.SetSeed(123);
        Assert.AreEqual(n1.Sample01(10.5f, 42.3f), n2.Sample01(10.5f, 42.3f), 1e-5f);
    }
}