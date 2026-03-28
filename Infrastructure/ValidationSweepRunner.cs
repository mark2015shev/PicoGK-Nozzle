using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PicoGK_Run.Core;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;

namespace PicoGK_Run.Infrastructure;

/// <summary>
/// Deterministic one-at-a-time parameter sweeps over the <b>coupled</b> SI path (no voxels, no viewer).
/// For engineering sanity checks only — not CFD validation or proof of monotonicity.
/// </summary>
public static class ValidationSweepRunner
{
    public const double DefaultDiscontinuityRelativeJump = 0.42;
    private const double BreakdownAlertThreshold = 0.52;
    private const double SeparationAlertThreshold = 0.55;
    private static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// K320 baseline sweeps; CSVs under <paramref name="csvOutputDirectory"/> (created if missing).
    /// </summary>
    public static IReadOnlyList<ValidationSweepResult> RunDefaultK320Validation(
        Action<string>? log = null,
        string? csvOutputDirectory = null,
        double discontinuityRelativeJumpThreshold = DefaultDiscontinuityRelativeJump)
    {
        log ??= Console.WriteLine;
        SourceInputs source = K320Baseline.CreateSource();
        NozzleDesignInputs baseline = K320Baseline.CreateDesign();
        RunConfiguration validationRun = K320Baseline.CreateValidationRun();
        log(
            $"Validation batch: SiVerbosityLevel={validationRun.SiVerbosityLevel} (Low skips SOURCE + dual swirl-capacity console blocks per SI case).");

        string root = csvOutputDirectory ?? Path.Combine(Environment.CurrentDirectory, "Output", "ValidationSweeps");
        Directory.CreateDirectory(root);

        var results = new List<ValidationSweepResult>
        {
            RunSweep(
                "InjectorYawDeg",
                "Injector yaw [deg]",
                baseline.InjectorYawAngleDeg,
                new[] { 35.0, 40, 45, 50, 55, 60, 65, 70, 75, 80 },
                v => Alter(baseline, injectorYawDeg: v),
                source,
                root,
                log,
                validationRun,
                discontinuityRelativeJumpThreshold),
            RunSweep(
                "SwirlChamberLengthMm",
                "Swirl chamber length [mm] (× baseline)",
                baseline.SwirlChamberLengthMm,
                new[] { 0.7, 0.85, 1.0, 1.15, 1.3, 1.5 },
                m => Alter(baseline, swirlChamberLengthMm: baseline.SwirlChamberLengthMm * m),
                source,
                root,
                log,
                validationRun,
                discontinuityRelativeJumpThreshold,
                reportedParameterValue: m => baseline.SwirlChamberLengthMm * m),
            RunSweep(
                "SwirlChamberDiameterMm",
                "Swirl chamber diameter [mm] (× baseline)",
                baseline.SwirlChamberDiameterMm,
                new[] { 0.75, 0.9, 1.0, 1.1, 1.25 },
                m => Alter(baseline, swirlChamberDiameterMm: baseline.SwirlChamberDiameterMm * m),
                source,
                root,
                log,
                validationRun,
                discontinuityRelativeJumpThreshold,
                reportedParameterValue: m => baseline.SwirlChamberDiameterMm * m),
            RunSweep(
                "ExpanderHalfAngleDeg",
                "Expander half-angle [deg]",
                baseline.ExpanderHalfAngleDeg,
                new[] { 3.0, 4, 5, 6, 7, 8, 9, 10, 11 },
                v => Alter(baseline, expanderHalfAngleDeg: v),
                source,
                root,
                log,
                validationRun,
                discontinuityRelativeJumpThreshold),
            RunSweep(
                "StatorVaneAngleDeg",
                "Stator vane angle [deg]",
                baseline.StatorVaneAngleDeg,
                new[] { 20.0, 25, 30, 35, 40, 45, 50, 55 },
                v => Alter(baseline, statorVaneAngleDeg: v),
                source,
                root,
                log,
                validationRun,
                discontinuityRelativeJumpThreshold)
        };

        log("");
        log("=== Validation sweep batch complete (first-order SI — not CFD proof) ===");
        log($"CSV folder: {root}");
        return results;
    }

    private static ValidationSweepResult RunSweep(
        string sweepKey,
        string parameterDisplayName,
        double baselineValue,
        IReadOnlyList<double> sweepCoordinates,
        Func<double, NozzleDesignInputs> designForCoordinate,
        SourceInputs source,
        string csvDirectory,
        Action<string> log,
        RunConfiguration validationRun,
        double jumpThreshold,
        Func<double, double>? reportedParameterValue = null)
    {
        var cases = new List<ValidationSweepCaseResult>(sweepCoordinates.Count);
        for (int i = 0; i < sweepCoordinates.Count; i++)
        {
            double coord = sweepCoordinates[i];
            NozzleDesignInputs d = designForCoordinate(coord);
            double paramVal = reportedParameterValue?.Invoke(coord) ?? coord;
            ValidationSweepCaseResult row = EvaluateToCase(sweepKey, paramVal, d, source, validationRun);
            cases.Add(row);
        }

        ApplyDiscontinuityFlags(cases, jumpThreshold);
        ValidationSweepResult aggregated = AggregateSweep(
            sweepKey,
            parameterDisplayName,
            baselineValue,
            cases,
            jumpThreshold);

        string csvName = SanitizeFileName(sweepKey) + ".csv";
        string csvPath = Path.Combine(csvDirectory, csvName);
        WriteCsv(csvPath, aggregated);
        aggregated = aggregated with { CsvPath = csvPath };

        LogSweepToConsole(aggregated, log);
        return aggregated;
    }

    private static ValidationSweepCaseResult EvaluateToCase(
        string parameterName,
        double parameterValue,
        NozzleDesignInputs design,
        SourceInputs source,
        RunConfiguration validationRun)
    {
        SiPathValidationPack p = NozzleFlowCompositionRoot.EvaluateSiPathForValidation(source, design, validationRun);
        NozzleSolvedState s = p.Solved;
        SiFlowDiagnostics si = p.SiDiag;
        ChamberFirstOrderPhysics? ch = si.Chamber;
        SiVortexCouplingDiagnostics? cpl = si.Coupling;

        int hardErrors = p.HealthMessages.Count(m => m.StartsWith("DESIGN ERROR", StringComparison.Ordinal));
        bool hasDesignError = hardErrors > 0;
        string warnings = SummarizeHealth(p.HealthMessages);

        double vq = ch?.TuningCompositeQuality ?? si.Vortex?.VortexQualityMetric ?? double.NaN;
        double breakdown = ch?.VortexStructure.BreakdownRiskScore ?? double.NaN;
        double separation = ch?.DiffuserRecovery.SeparationRiskScore ?? double.NaN;
        double loss01 = ch?.NormalizedTotalPressureLoss01 ?? double.NaN;

        double vEffInj = cpl?.InjectorJetVelocityEffectiveMps ?? double.NaN;
        double etaStatorEff = cpl?.StatorEtaEffective ?? double.NaN;
        double diffRec = cpl?.DiffuserRecoveryMultiplier
                         ?? ch?.DiffuserRecovery.EffectivePressureRecoveryEfficiency
                         ?? double.NaN;

        var row = new ValidationSweepCaseResult
        {
            ParameterName = parameterName,
            ParameterValue = parameterValue,
            NetThrustN = si.NetThrustN,
            SourceOnlyThrustN = s.SourceOnlyThrustN,
            ThrustGainRatio = s.ThrustGainRatio,
            EntrainmentRatio = s.EntrainmentRatio,
            MixedMassFlowKgS = s.MixedMassFlowKgPerSec,
            ExitVelocityMps = s.ExitVelocityMps,
            InjectorSwirlNumber = s.InjectorSwirlNumber,
            FluxStyleSwirlMetric = ch?.VortexStructure.SwirlNumberFluxStyle ?? double.NaN,
            ChamberSlendernessLD = p.CriticalRatios.ChamberSlendernessLD,
            ChamberSwirlForStator = s.ChamberSwirlNumberForStator,
            ResidualExitSwirlMps = si.FinalTangentialVelocityMps,
            CorePressureDropPa = ch?.RadialPressure.CorePressureDropPa ?? double.NaN,
            WallPressureRisePa = ch?.RadialPressure.WallPressureRisePa ?? double.NaN,
            RadialPressureDeltaPa = ch?.RadialPressure.EstimatedRadialPressureDeltaPa ?? double.NaN,
            VortexClassification = ch?.VortexStructure.ClassificationLabel ?? "n/a",
            VortexQuality = vq,
            BreakdownRisk = breakdown,
            EffectiveInjectorVelocityMps = vEffInj,
            EffectiveStatorEfficiency = etaStatorEff,
            EffectiveDiffuserRecovery = diffRec,
            SeparationRisk = separation,
            TotalLossMetric01 = loss01,
            EjectorRegime = ch?.EjectorRegime.Regime.ToString() ?? "n/a",
            HealthCount = p.HealthMessages.Count,
            HasDesignError = hasDesignError,
            KeyWarningsSummary = warnings,
            ImpossibleOrInvalidState = DetectInvalidState(si, s, vEffInj, vq),
            Notes = ""
        };

        var noteParts = new List<string>();
        if (row.ImpossibleOrInvalidState)
            noteParts.Add("invalid_or_nan");
        if (hasDesignError)
            noteParts.Add("design_error");
        row = row with { Notes = string.Join(";", noteParts) };
        return row;
    }

    private static bool DetectInvalidState(SiFlowDiagnostics si, NozzleSolvedState s, double vEff, double vq)
    {
        if (Bad(si.NetThrustN) || Bad(s.MixedMassFlowKgPerSec) || Bad(s.ExitVelocityMps))
            return true;
        if (s.MixedMassFlowKgPerSec < -1e-9)
            return true;
        if (Bad(vEff) && si.Coupling != null)
            return true;
        if (Bad(vq) && si.Chamber != null)
            return true;
        if (si.ChamberMarch?.SwirlEntranceCapacityStations is { CombinedClassification: var cc }
            && (cc == SwirlEntranceCapacityClassification.FailRestrictive
                || cc == SwirlEntranceCapacityClassification.FailChoking))
            return true;
        return false;
    }

    private static bool Bad(double x) => double.IsNaN(x) || double.IsInfinity(x);

    private static void ApplyDiscontinuityFlags(List<ValidationSweepCaseResult> cases, double threshold)
    {
        if (cases.Count < 2)
            return;

        double eps = 1e-9;
        for (int i = 1; i < cases.Count; i++)
        {
            ValidationSweepCaseResult prev = cases[i - 1];
            ValidationSweepCaseResult cur = cases[i];
            bool jump = RelativeJump(prev.NetThrustN, cur.NetThrustN, eps) > threshold
                        || RelativeJump(prev.EntrainmentRatio, cur.EntrainmentRatio, eps) > threshold
                        || RelativeJump(prev.ExitVelocityMps, cur.ExitVelocityMps, eps) > threshold
                        || RelativeJump(prev.VortexQuality, cur.VortexQuality, eps) > threshold;
            if (jump)
            {
                string extra = "rapid_adjacent_change";
                cur = cur with
                {
                    RapidChangeFromPrevious = true,
                    Notes = string.IsNullOrEmpty(cur.Notes) ? extra : cur.Notes + ";" + extra
                };
                cases[i] = cur;
            }
        }
    }

    private static double RelativeJump(double a, double b, double eps)
    {
        if (Bad(a) || Bad(b))
            return 0.0;
        double den = Math.Max(Math.Abs(a), eps);
        return Math.Abs(b - a) / den;
    }

    private static ValidationSweepResult AggregateSweep(
        string sweepName,
        string parameterDisplayName,
        double baselineValue,
        IReadOnlyList<ValidationSweepCaseResult> cases,
        double jumpThreshold)
    {
        int n = cases.Count;
        int bestThrust = -1;
        double bestThrustVal = double.NegativeInfinity;
        int bestEr = -1;
        double bestErVal = double.NegativeInfinity;
        int bestVq = -1;
        double bestVqVal = double.NegativeInfinity;
        int firstBd = -1;
        int firstSep = -1;
        int bestLowRisk = -1;
        double bestLowRiskThrust = double.NegativeInfinity;

        for (int i = 0; i < n; i++)
        {
            ValidationSweepCaseResult c = cases[i];
            if (!Bad(c.NetThrustN) && c.NetThrustN > bestThrustVal)
            {
                bestThrustVal = c.NetThrustN;
                bestThrust = i;
            }

            if (!Bad(c.EntrainmentRatio) && c.EntrainmentRatio > bestErVal)
            {
                bestErVal = c.EntrainmentRatio;
                bestEr = i;
            }

            if (!Bad(c.VortexQuality) && c.VortexQuality > bestVqVal)
            {
                bestVqVal = c.VortexQuality;
                bestVq = i;
            }

            if (firstBd < 0 && !Bad(c.BreakdownRisk) && c.BreakdownRisk >= BreakdownAlertThreshold)
                firstBd = i;
            if (firstSep < 0 && !Bad(c.SeparationRisk) && c.SeparationRisk >= SeparationAlertThreshold)
                firstSep = i;

            bool lowRisk = !Bad(c.BreakdownRisk) && !Bad(c.SeparationRisk)
                           && c.BreakdownRisk < BreakdownAlertThreshold
                           && c.SeparationRisk < SeparationAlertThreshold;
            if (lowRisk && !Bad(c.NetThrustN) && c.NetThrustN > bestLowRiskThrust)
            {
                bestLowRiskThrust = c.NetThrustN;
                bestLowRisk = i;
            }
        }

        bool anyBad = cases.Any(c => c.ImpossibleOrInvalidState);
        bool anyJump = cases.Any(c => c.RapidChangeFromPrevious);

        List<ValidationSweepCaseResult> list = cases is List<ValidationSweepCaseResult> l ? l : cases.ToList();
        IReadOnlyList<string> interp = BuildInterpretation(
            sweepName,
            parameterDisplayName,
            list,
            bestThrust,
            bestEr,
            bestVq,
            firstBd,
            firstSep,
            bestLowRisk,
            jumpThreshold);

        return new ValidationSweepResult
        {
            SweepName = sweepName,
            ParameterDisplayName = parameterDisplayName,
            BaselineParameterValue = baselineValue,
            Cases = list,
            BestNetThrustIndex = bestThrust,
            BestEntrainmentRatioIndex = bestEr,
            BestVortexQualityIndex = bestVq,
            FirstHighBreakdownRiskIndex = firstBd,
            FirstHighSeparationRiskIndex = firstSep,
            BestThrustAmongLowRiskIndex = bestLowRisk,
            AnyImpossibleState = anyBad,
            AnyRapidChange = anyJump,
            InterpretationLines = interp
        };
    }

    private static IReadOnlyList<string> BuildInterpretation(
        string sweepKey,
        string parameterDisplayName,
        IReadOnlyList<ValidationSweepCaseResult> cases,
        int bestThrust,
        int bestEr,
        int bestVq,
        int firstBd,
        int firstSep,
        int bestLowRisk,
        double jumpThreshold)
    {
        var lines = new List<string>
        {
            $"Sweep '{sweepKey}' ({parameterDisplayName}): heuristic readout only — not CFD-validated."
        };

        if (bestThrust >= 0)
            lines.Add($"Max NetThrustN at index {bestThrust}, value={cases[bestThrust].ParameterValue.ToString("F3", CsvCulture)} (param units as per sweep).");
        if (bestEr >= 0)
            lines.Add($"Max entrainment ratio at index {bestEr}, ER={cases[bestEr].EntrainmentRatio:F4}.");
        if (bestVq >= 0)
            lines.Add($"Best tuning/vortex composite at index {bestVq}, quality={cases[bestVq].VortexQuality:F3}.");
        if (firstBd >= 0)
            lines.Add($"Breakdown risk first reaches ≥{BreakdownAlertThreshold:F2} at index {firstBd} (param={cases[firstBd].ParameterValue.ToString("F3", CsvCulture)}).");
        if (firstSep >= 0)
            lines.Add($"Separation risk first reaches ≥{SeparationAlertThreshold:F2} at index {firstSep}.");
        if (bestLowRisk >= 0)
            lines.Add($"Best thrust among low breakdown+separation risk: index {bestLowRisk}, NetThrustN={cases[bestLowRisk].NetThrustN:F2} N.");
        else
            lines.Add("No case met low-risk thresholds for combined breakdown+separation; review metrics manually.");

        if (cases.Any(c => c.RapidChangeFromPrevious))
            lines.Add($"Adjacent relative jump > {jumpThreshold:F2} on thrust/ER/exit-V/vortex-quality — review for model stiffness (not necessarily physical).");

        // Simple pattern hints
        switch (sweepKey)
        {
            case "InjectorYawDeg":
                lines.Add("Yaw trades tangential vs axial injection; expect thrust to move with |Vt|/|Va| and stator coupling.");
                break;
            case "SwirlChamberLengthMm":
                lines.Add("Longer chamber usually increases decay path; watch entrainment vs dissipation in metrics.");
                break;
            case "SwirlChamberDiameterMm":
                lines.Add("Diameter changes L/D and capture ratio; radial pressure model scales with R.");
                break;
            case "ExpanderHalfAngleDeg":
                lines.Add("Steep half-angles typically raise separation risk in the diffuser heuristic.");
                break;
            case "StatorVaneAngleDeg":
                lines.Add("Stator angle vs implied swirl affects incidence loss and effective η.");
                break;
        }

        return lines;
    }

    private static void LogSweepToConsole(ValidationSweepResult sw, Action<string> log)
    {
        log("");
        log($"--- Sweep: {sw.SweepName} ({sw.ParameterDisplayName}) baseline≈{sw.BaselineParameterValue.ToString(CsvCulture)} ---");
        log(" idx | paramVal | NetThr[N] |  ER   | Vexit | VortQ | BrkDn |  Sep  | health | notes");
        log("-----+----------+-----------+-------+-------+-------+-------+-------+--------+------");
        for (int i = 0; i < sw.Cases.Count; i++)
        {
            ValidationSweepCaseResult c = sw.Cases[i];
            string fl = c.RapidChangeFromPrevious ? "JUMP" : "";
            string inv = c.ImpossibleOrInvalidState ? "BAD" : "";
            log(
                $" {i,2} | {c.ParameterValue,8:F3} | {c.NetThrustN,9:F2} | {c.EntrainmentRatio,5:F3} | {c.ExitVelocityMps,5:F1} | {c.VortexQuality,5:F3} | {c.BreakdownRisk,5:F3} | {c.SeparationRisk,5:F3} | {c.HealthCount,6} | {fl}{inv}");
        }

        log("Interpretation:");
        foreach (string line in sw.InterpretationLines)
            log("  " + line);
        log($"CSV: {sw.CsvPath}");
    }

    private static void WriteCsv(string path, ValidationSweepResult sw)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "ParameterName,ParameterValue,NetThrustN,ThrustGainRatio,EntrainmentRatio,ExitVelocityMps,InjectorSwirlNumber,VortexQuality,BreakdownRisk,SeparationRisk,EffectiveInjectorVelocity,EffectiveStatorEfficiency,EffectiveDiffuserRecovery,EjectorRegime,HealthCount,Notes");
        foreach (ValidationSweepCaseResult c in sw.Cases)
        {
            sb.Append(EscapeCsv(c.ParameterName)).Append(',');
            sb.Append(c.ParameterValue.ToString(CsvCulture)).Append(',');
            sb.Append(c.NetThrustN.ToString(CsvCulture)).Append(',');
            sb.Append(c.ThrustGainRatio.ToString(CsvCulture)).Append(',');
            sb.Append(c.EntrainmentRatio.ToString(CsvCulture)).Append(',');
            sb.Append(c.ExitVelocityMps.ToString(CsvCulture)).Append(',');
            sb.Append(c.InjectorSwirlNumber.ToString(CsvCulture)).Append(',');
            sb.Append(c.VortexQuality.ToString(CsvCulture)).Append(',');
            sb.Append(c.BreakdownRisk.ToString(CsvCulture)).Append(',');
            sb.Append(c.SeparationRisk.ToString(CsvCulture)).Append(',');
            sb.Append(c.EffectiveInjectorVelocityMps.ToString(CsvCulture)).Append(',');
            sb.Append(c.EffectiveStatorEfficiency.ToString(CsvCulture)).Append(',');
            sb.Append(c.EffectiveDiffuserRecovery.ToString(CsvCulture)).Append(',');
            sb.Append(EscapeCsv(c.EjectorRegime)).Append(',');
            sb.Append(c.HealthCount.ToString(CsvCulture)).Append(',');
            sb.AppendLine(EscapeCsv(c.Notes + (c.RapidChangeFromPrevious ? ";rapid_change" : "") + (c.ImpossibleOrInvalidState ? ";invalid" : "")));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "\"\"";
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return s;
    }

    private static string SummarizeHealth(IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
            return "";
        var take = messages.Take(4).ToArray();
        string j = string.Join(" | ", take);
        return j.Length > 220 ? j[..217] + "..." : j;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_", StringComparison.Ordinal);
        return name;
    }

    private static NozzleDesignInputs Alter(
        NozzleDesignInputs b,
        double? injectorYawDeg = null,
        double? swirlChamberLengthMm = null,
        double? swirlChamberDiameterMm = null,
        double? expanderHalfAngleDeg = null,
        double? statorVaneAngleDeg = null) =>
        new()
        {
            InletDiameterMm = b.InletDiameterMm,
            SwirlChamberDiameterMm = swirlChamberDiameterMm ?? b.SwirlChamberDiameterMm,
            SwirlChamberLengthMm = swirlChamberLengthMm ?? b.SwirlChamberLengthMm,
            InjectorAxialPositionRatio = b.InjectorAxialPositionRatio,
            TotalInjectorAreaMm2 = b.TotalInjectorAreaMm2,
            InjectorCount = b.InjectorCount,
            InjectorWidthMm = b.InjectorWidthMm,
            InjectorHeightMm = b.InjectorHeightMm,
            InjectorYawAngleDeg = injectorYawDeg ?? b.InjectorYawAngleDeg,
            InjectorPitchAngleDeg = b.InjectorPitchAngleDeg,
            InjectorRollAngleDeg = b.InjectorRollAngleDeg,
            ExpanderLengthMm = b.ExpanderLengthMm,
            ExpanderHalfAngleDeg = expanderHalfAngleDeg ?? b.ExpanderHalfAngleDeg,
            ExitDiameterMm = b.ExitDiameterMm,
            StatorVaneAngleDeg = statorVaneAngleDeg ?? b.StatorVaneAngleDeg,
            StatorVaneCount = b.StatorVaneCount,
            StatorHubDiameterMm = b.StatorHubDiameterMm,
            StatorAxialLengthMm = b.StatorAxialLengthMm,
            StatorBladeChordMm = b.StatorBladeChordMm,
            WallThicknessMm = b.WallThicknessMm
        };
}
