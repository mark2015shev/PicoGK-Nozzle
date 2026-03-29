using System;
using System.Numerics;
using PicoGK;
using PicoGK_Run.Parameters;

namespace PicoGK_Run.Physics;

/// <summary>
/// First-order vortex / ejector entrainment helpers for tangential injection into a conical path.
/// <para>
/// <b>PicoGK nozzle frame:</b> chamber and cone are built along <b>+X</b> (see <see cref="Geometry.SwirlChamberBuilder"/>).
/// “Axial down the cone” = +X. Tangential spin lives in the Y–Z plane (analogous to classical (-y,x,0) about Z when X is axial).
/// </para>
/// </summary>
public static class VortexEntrainmentPhysics
{
    /// <summary>Round turbulent jet entrainment coefficient (order 0.3); used as the baseline multiplier.</summary>
    public const double TurbulentJetEntrainmentCoefficient = 0.32;

    /// <summary>Ejector-style rule: mixing axial length ≥ this factor × primary equivalent jet diameter.</summary>
    public const double EjectorMixingLengthToJetDiameterRatio = 6.0;

    /// <summary>
    /// Mass-flow–ratio style estimate ṁ_secondary / ṁ_primary ~ k ε sin(θ) √(A_exit / A_throat).
    /// Areas in m²; <paramref name="coneHalfAngleRadians"/> is the geometric half-angle of the diverging cone (rad).
    /// </summary>
    public static double CalculateEntrainmentRatio(
        double throatAreaM2,
        double exitAreaM2,
        double coneHalfAngleRadians)
    {
        throatAreaM2 = Math.Max(throatAreaM2, 1e-12);
        exitAreaM2 = Math.Max(exitAreaM2, 1e-12);
        double areaRatio = Math.Sqrt(exitAreaM2 / throatAreaM2);
        double axialFactor = Math.Sin(Math.Clamp(coneHalfAngleRadians, 1e-4, Math.PI * 0.5 - 1e-4));
        double er = TurbulentJetEntrainmentCoefficient * axialFactor * areaRatio;
        return Math.Clamp(er, 0.02, 1.6);
    }

    /// <summary>
    /// Equivalent circular diameter from total injector area, then 6× for mixing length [mm].
    /// </summary>
    public static double MixingLengthMinimumMmRuleOfSix(double totalInjectorAreaMm2)
    {
        double a = Math.Max(totalInjectorAreaMm2, 1e-6);
        double dEqMm = 2.0 * Math.Sqrt(a / Math.PI);
        return EjectorMixingLengthToJetDiameterRatio * dEqMm;
    }

    /// <summary>Overload for <see cref="NozzleDesignInputs"/>.</summary>
    public static double MixingLengthMinimumMmRuleOfSix(NozzleDesignInputs d) =>
        MixingLengthMinimumMmRuleOfSix(d.TotalInjectorAreaMm2);

    /// <summary>
    /// Safety check: require V_ax &gt; V_t / tan(coneHalfAngle) so the cone can convert swirl into downstream axial motion.
    /// </summary>
    public static bool TryVerifyAxialDominatesSwirl(
        double axialVelocityMps,
        double tangentialVelocityMps,
        double coneHalfAngleRadians,
        out string? remediationHint)
    {
        double theta = Math.Max(coneHalfAngleRadians, 1e-4);
        double tanA = Math.Tan(theta);
        bool ok = axialVelocityMps > tangentialVelocityMps / tanA - 1e-9;
        if (ok)
        {
            remediationHint = null;
            return true;
        }

        remediationHint =
            "Axial velocity does not exceed tangential/tan(cone half-angle) — tangential stall risk. "
            + "Consider a center plug / hub to block the forced vortex core, or a steeper cone (larger expander half-angle), then re-check in CFD.";
        return false;
    }

    /// <summary>
    /// Unit direction: helical blend from tangential (Y–Z) toward +X axial; tangential weight decays with X and ~1/r (v·r).
    /// </summary>
    /// <param name="positionMm">World position in mm (PicoGK frame).</param>
    /// <param name="axialStartMm">Chamber upstream X.</param>
    /// <param name="axialEndMm">Chamber downstream X (exit end of mixing zone).</param>
    /// <param name="referenceRadiusMm">Reference radius for angular-momentum scaling (e.g. 0.5× chamber bore).</param>
    public static Vector3 EvaluateHelicalFlowDirectionPicoGkMm(
        Vector3 positionMm,
        float axialStartMm,
        float axialEndMm,
        float referenceRadiusMm)
    {
        float x = positionMm.X;
        float denom = MathF.Max(axialEndMm - axialStartMm, 1e-3f);
        float t = (x - axialStartMm) / denom;
        if (t < 0f)
            t = 0f;
        else if (t > 1f)
            t = 1f;
        float tSmooth = t * t * (3f - 2f * t);

        float y = positionMm.Y;
        float z = positionMm.Z;
        float r = MathF.Sqrt(y * y + z * z);
        r = MathF.Max(r, 1e-3f);

        Vector3 eSpin = new(0f, -z / r, y / r);
        Vector3 eAx = Vector3.UnitX;

        float rRef = MathF.Max(referenceRadiusMm, 1e-3f);
        float momentumScale = rRef / r;
        float wSpin = (1f - tSmooth) * momentumScale;
        float wAx = 0.15f + 0.85f * tSmooth;

        Vector3 v = wSpin * eSpin + wAx * eAx;
        float len = v.Length();
        if (len < 1e-6f)
            return eAx;
        return v / len;
    }

    /// <summary>
    /// Fills a <see cref="VectorField"/> at every active voxel of the SDF derived from <paramref name="regionVoxels"/>.
    /// Caller owns the returned field; <paramref name="regionVoxels"/> is not retained.
    /// </summary>
    public static VectorField BuildHelicalFlowVectorFieldFromVoxelSdf(
        Voxels regionVoxels,
        float axialStartMm,
        float axialEndMm,
        float referenceRadiusMm)
    {
        using ScalarField sdf = new(regionVoxels);
        VectorField vf = new();
        var filler = new HelicalTraverse(vf, axialStartMm, axialEndMm, referenceRadiusMm);
        sdf.TraverseActive(filler);
        return vf;
    }

    private sealed class HelicalTraverse : ITraverseScalarField
    {
        private readonly VectorField _field;
        private readonly float _x0;
        private readonly float _x1;
        private readonly float _rRef;

        public HelicalTraverse(VectorField field, float axialStartMm, float axialEndMm, float referenceRadiusMm)
        {
            _field = field;
            _x0 = axialStartMm;
            _x1 = axialEndMm;
            _rRef = referenceRadiusMm;
        }

        public void InformActiveValue(in Vector3 vecPosition, float fValue)
        {
            _ = fValue;
            Vector3 dir = EvaluateHelicalFlowDirectionPicoGkMm(vecPosition, _x0, _x1, _rRef);
            _field.SetValue(vecPosition, dir);
        }
    }
}
