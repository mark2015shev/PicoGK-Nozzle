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
    /// Axial span matches physical <see cref="NozzleDesignInputs.SwirlChamberLengthMm"/> (same as voxel main chamber).
    /// </summary>
    public static VectorField BuildSwirlChamberHelicalFlowField(
        this NozzleGeometryResult geometry,
        NozzleDesignInputs design,
        float xSwirlStartMm,
        RunConfiguration? run = null)
    {
        _ = run;
        double lenMm = System.Math.Max(design.SwirlChamberLengthMm, 1.0);
        float x1 = xSwirlStartMm + (float)lenMm;
        float rRef = 0.5f * (float)design.SwirlChamberDiameterMm;
        return VortexEntrainmentPhysics.BuildHelicalFlowVectorFieldFromVoxelSdf(
            geometry.SwirlChamber,
            xSwirlStartMm,
            x1,
            rRef);
    }
}
