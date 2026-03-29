using System;
using System.Collections.Generic;
using System.Linq;

namespace PicoGK_Run.Physics;

public enum EjectorOperatingRegime
{
    SubcriticalEntrainment,
    CriticalEntrainment,
    SecondaryChokeLimited,
    CompoundChokingRisk,
    OverexpandedPoorAdmittance
}

public sealed class EjectorRegimeResult
{
    public EjectorOperatingRegime Regime { get; init; }
    public double RegimeScore { get; init; }
    public string Notes { get; init; } = "";
}

public static class EjectorRegimeModel
{
    public static EjectorRegimeResult Compute(
        IReadOnlyList<FlowMarchStepResult> steps,
        double pressureRatio,
        double ambientPressurePa,
        double minInletStaticPa,
        double entrainmentShortfallSumKgS,
        double sumRequestedKgS,
        double sumActualKgS)
    {
        double pr = Math.Max(pressureRatio, 1.0);
        double pAmb = Math.Max(ambientPressurePa, 1.0);
        double depression = Math.Max(0.0, pAmb - minInletStaticPa) / pAmb;

        bool anyChoked = steps.Any(s => s.InletIsChoked);
        double maxM = steps.Count > 0 ? steps.Max(s => s.InletMach) : 0.0;

        double req = Math.Max(sumRequestedKgS, 1e-12);
        double shortfallRatio = Math.Clamp(entrainmentShortfallSumKgS / req, 0.0, 2.0);
        double admit = sumActualKgS / req;

        EjectorOperatingRegime regime;
        double score;

        if (pr > 3.2 && depression > 0.22 && admit < 0.62)
        {
            regime = EjectorOperatingRegime.OverexpandedPoorAdmittance;
            score = 0.78;
        }
        else if (anyChoked && maxM > 0.92 && shortfallRatio > ChamberPhysicsCoefficients.EjectorShortfallCriticalRatio)
        {
            regime = EjectorOperatingRegime.CompoundChokingRisk;
            score = 0.88;
        }
        else if (anyChoked && shortfallRatio > 0.22)
        {
            regime = EjectorOperatingRegime.SecondaryChokeLimited;
            score = 0.62;
        }
        else if (maxM > 0.82 || shortfallRatio > 0.12)
        {
            regime = EjectorOperatingRegime.CriticalEntrainment;
            score = 0.42;
        }
        else
        {
            regime = EjectorOperatingRegime.SubcriticalEntrainment;
            score = 0.18;
        }

        string notes = $"PR={pr:F2}, max entr. Mach≈{maxM:F3}, admit={admit:F2}, choked steps={anyChoked}. Reduced-order regime classification only.";

        return new EjectorRegimeResult
        {
            Regime = regime,
            RegimeScore = score,
            Notes = notes
        };
    }
}
