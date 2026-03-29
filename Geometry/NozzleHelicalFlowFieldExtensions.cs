using PicoGK;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Geometry;

/// <summary>
/// Optional <see cref="VectorField"/> generation on top of voxelized nozzle segments (PicoGK OpenVDB workflow).
/// </summary>
public static class NozzleHelicalFlowFieldExtensions
{
    /// <summary>
    /// Builds a unit-direction helical field on the swirl-chamber voxel set: tangential in Y–Z decaying along +X, axial +X increasing.
    /// Axial span matches enforced chamber length (including Rule-of-6 when <paramref name="run"/> requests it).
    /// </summary>
    public static VectorField BuildSwirlChamberHelicalFlowField(
        this NozzleGeometryResult geometry,
        NozzleDesignInputs design,
        float xSwirlStartMm,
        RunConfiguration? run = null)
    {
        double lenMm = design.SwirlChamberLengthMm;
        if (run?.EnforceEjectorMixingRuleOfSix == true)
            lenMm = System.Math.Max(lenMm, VortexEntrainmentPhysics.MixingLengthMinimumMmRuleOfSix(design));
        float x1 = xSwirlStartMm + (float)lenMm;
        float rRef = 0.5f * (float)design.SwirlChamberDiameterMm;
        return VortexEntrainmentPhysics.BuildHelicalFlowVectorFieldFromVoxelSdf(
            geometry.SwirlChamber,
            xSwirlStartMm,
            x1,
            rRef);
    }
}
