using System;
using System.Collections.Generic;
using System.Numerics;
using PicoGK_Run.Geometry;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics.Solvers;

namespace PicoGK_Run.Physics.JetTrajectory;

/// <summary>
/// Reduced-order, step-based jet trajectory through approximate nozzle duct geometry.
/// Wall handling is slip-heavy normal damping (not specular reflection); jet–jet is soft blending with dissipation.
/// </summary>
public static class JetTrajectorySolver
{
    private const double WallInfluenceScaleMm = 4.0;

    /// <summary>
    /// Builds per-port initial states from discharge solution and <see cref="SwirlChamberPlacement"/> (matches reference marker frame).
    /// </summary>
    public static IReadOnlyList<InjectorInitialState> BuildInitialStates(
        NozzleDesignInputs design,
        in SwirlChamberPlacement placement,
        InjectorDischargeResult discharge,
        double yawPhysicsDeg,
        double staticPressurePa,
        double temperatureK,
        double densityKgM3,
        double coreMassFlowKgS)
    {
        int n = Math.Max(1, design.InjectorCount);
        float x = (float)placement.InjectorPlaneXMm;
        float chamberR = 0.5f * (float)design.SwirlChamberDiameterMm;
        float wallT = (float)design.WallThicknessMm;
        float outerR = chamberR + wallT;

        float yawRad = (float)(yawPhysicsDeg * Math.PI / 180.0);
        float pitchRad = (float)(design.InjectorPitchAngleDeg * Math.PI / 180.0);
        float rollRad = (float)(design.InjectorRollAngleDeg * Math.PI / 180.0);

        double speed = Math.Max(discharge.EffectiveVelocityMagnitudeMps, 1e-6);
        var list = new List<InjectorInitialState>(n);
        float dPhi = (2f * MathF.PI) / n;
        double mdotEach = coreMassFlowKgS / n;

        for (int i = 0; i < n; i++)
        {
            float phi = i * dPhi;
            Vector3 radial = new(0f, MathF.Cos(phi), MathF.Sin(phi));
            Vector3 tangent = new(0f, -MathF.Sin(phi), MathF.Cos(phi));
            Vector3 axial = Vector3.UnitX;

            Vector3 baseDirection = Vector3.Normalize((MathF.Cos(yawRad) * axial) + (MathF.Sin(yawRad) * tangent));
            Vector3 pitchedDirection = Vector3.Normalize((MathF.Cos(pitchRad) * baseDirection) + (MathF.Sin(pitchRad) * -radial));
            Vector3 rollDirection = Vector3.Normalize((MathF.Cos(rollRad) * pitchedDirection) + (MathF.Sin(rollRad) * tangent));
            Vector3 dir = rollDirection;

            Vector3 pos = new Vector3(x, 0f, 0f) + outerR * radial;
            Vector3 vel = dir * (float)speed;

            list.Add(new InjectorInitialState
            {
                InjectorIndex = i,
                PositionMm = pos,
                VelocityMps = vel,
                SpeedMps = speed,
                StaticPressurePa = staticPressurePa,
                TemperatureK = temperatureK,
                DensityKgM3 = densityKgM3,
                MassFlowKgS = mdotEach
            });
        }

        return list;
    }

    /// <summary>
    /// March all injectors synchronously; each step uses other jets’ previous-step state for interaction.
    /// </summary>
    public static JetTrajectoryResult Solve(
        NozzleDesignInputs design,
        RunConfiguration run,
        GeometryAssemblyPath path,
        IReadOnlyList<InjectorInitialState> initialStates,
        in SwirlChamberPlacement placement)
    {
        if (!run.UsePhysicsTracedJetTrajectory || initialStates.Count == 0)
            throw new InvalidOperationException("Trajectory solver requires UsePhysicsTracedJetTrajectory and at least one injector state.");

        double stepMm = Math.Clamp(run.TrajectoryStepMm, 0.15, 8.0);
        double maxLen = Math.Clamp(run.MaxTrajectoryLengthMm, 5.0, 600.0);
        double slip = Math.Clamp(run.WallSlipRetention, 0.0, 1.0);
        double dampN = Math.Clamp(run.WallNormalDamping, 0.0, 1.0);
        double jj = Math.Clamp(run.JetInteractionStrength, 0.0, 1.0);
        double sigmaMm = Math.Max(6.0, 3.0 * stepMm);

        double legacyLen = Math.Max(0.0, placement.MainChamberEndXMm - placement.InjectorPlaneXMm);

        int n = initialStates.Count;
        var pos = new Vector3[n];
        var vel = new Vector3[n];
        var envR = new double[n];
        var traj = new List<JetTrajectorySample>[n];
        for (int i = 0; i < n; i++)
        {
            pos[i] = initialStates[i].PositionMm;
            vel[i] = initialStates[i].VelocityMps;
            envR[i] = 0.5 * Math.Min(design.InjectorWidthMm, design.InjectorHeightMm);
            traj[i] = new List<JetTrajectorySample>();
        }

        int wallHits = 0;
        int jetHits = 0;

        for (int i = 0; i < n; i++)
        {
            double vm0 = vel[i].Length();
            Vector3 d0 = vm0 > 1e-6f ? vel[i] / (float)vm0 : Vector3.UnitX;
            traj[i].Add(new JetTrajectorySample
            {
                StepIndex = 0,
                PositionMm = pos[i],
                DirectionUnit = d0,
                SpeedMps = vm0,
                StaticPressurePa = initialStates[i].StaticPressurePa,
                EnvelopeRadiusMm = envR[i],
                HadWallInteraction = false,
                HadJetInteraction = false
            });
        }

        for (int step = 1; step < 6000; step++)
        {
            var posPrev = new Vector3[n];
            for (int i = 0; i < n; i++)
                posPrev[i] = pos[i];

            bool anyMoved = false;

            for (int i = 0; i < n; i++)
            {
                double vm0 = vel[i].Length();
                if (vm0 < 2.0)
                    continue;

                double arc = 0.0;
                for (int k = 1; k < traj[i].Count; k++)
                    arc += Vector3.Distance(traj[i][k - 1].PositionMm, traj[i][k].PositionMm);

                if (arc >= maxLen || !DomainContains(path, pos[i]))
                {
                    vel[i] = Vector3.Zero;
                    continue;
                }

                anyMoved = true;

                Vector3 p = pos[i];
                Vector3 v = vel[i];
                bool wallFlag = false;
                bool jetFlag = false;

                if (run.UseWallDeflection
                    && TryBoreWallInteraction(path, design, p, out Vector3 nOutFluid, out double clearanceMm))
                {
                    double w = Math.Exp(-Math.Max(0.0, clearanceMm) / WallInfluenceScaleMm);
                    if (clearanceMm < 0.0)
                        w = 1.0;
                    if (w > 0.02)
                    {
                        wallFlag = true;
                        float vn = Vector3.Dot(v, nOutFluid);
                        Vector3 vNormal = vn * nOutFluid;
                        Vector3 vTan = v - vNormal;
                        v = (float)slip * vTan + (float)dampN * vNormal;
                        float vm1 = v.Length();
                        if (vm1 > 1e-6f)
                            v *= (float)(0.97 * vm0 / vm1);
                    }
                }

                if (run.UseJetJetInteraction && jj > 1e-6)
                {
                    Vector3 blend = Vector3.Zero;
                    double wSum = 0.0;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i)
                            continue;
                        double dMm = Vector3.Distance(p, posPrev[j]);
                        double wij = Math.Exp(-(dMm * dMm) / (2.0 * sigmaMm * sigmaMm));
                        if (wij < 1e-4)
                            continue;
                        double vj = vel[j].Length();
                        if (vj < 1e-6)
                            continue;
                        blend += (float)(wij * initialStates[j].MassFlowKgS) * (vel[j] / (float)vj);
                        wSum += wij * initialStates[j].MassFlowKgS;
                    }

                    if (wSum > 1e-12)
                    {
                        jetFlag = true;
                        Vector3 targetDir = Vector3.Normalize(blend);
                        Vector3 curDir = Vector3.Normalize(v);
                        Vector3 mixed = Vector3.Normalize(Vector3.Lerp(curDir, targetDir, (float)(jj * 0.35)));
                        v = mixed * (float)(v.Length() * (1.0 - 0.04 * jj));
                    }
                }

                if (wallFlag)
                    wallHits++;
                if (jetFlag)
                    jetHits++;

                float vm = v.Length();
                if (vm < 2f)
                {
                    vel[i] = Vector3.Zero;
                    continue;
                }

                Vector3 dir = v / vm;
                pos[i] = p + dir * (float)stepMm;

                double pPa = traj[i][^1].StaticPressurePa * (1.0 - 0.0008 * stepMm);

                if (run.UseTrajectoryExpansionEnvelope)
                {
                    envR[i] += 0.04 * stepMm;
                    if (TryBoreWallInteraction(path, design, pos[i], out _, out double cl))
                        envR[i] = Math.Min(envR[i], Math.Max(0.5, Math.Max(0.0, cl) * 0.85));
                }

                traj[i].Add(new JetTrajectorySample
                {
                    StepIndex = step,
                    PositionMm = pos[i],
                    DirectionUnit = dir,
                    SpeedMps = vm,
                    StaticPressurePa = pPa,
                    EnvelopeRadiusMm = envR[i],
                    HadWallInteraction = wallFlag,
                    HadJetInteraction = jetFlag
                });

                vel[i] = dir * vm;
            }

            if (!anyMoved)
                break;
        }

        var byInj = new List<IReadOnlyList<JetTrajectorySample>>(n);
        double tracedSum = 0.0;
        double alignSum = 0.0;
        int alignN = 0;
        for (int i = 0; i < n; i++)
        {
            byInj.Add(traj[i]);
            double L = 0.0;
            for (int k = 1; k < traj[i].Count; k++)
                L += Vector3.Distance(traj[i][k - 1].PositionMm, traj[i][k].PositionMm);
            tracedSum += L;
            if (traj[i].Count > 0)
            {
                Vector3 d = traj[i][^1].DirectionUnit;
                alignSum += d.X;
                alignN++;
            }
        }

        double meanTraced = n > 0 ? tracedSum / n : 0.0;
        double meanAlign = alignN > 0 ? alignSum / alignN : 0.0;

        var hints = new List<string>();
        if (run.UseTrajectoryForGeometryGuidance)
        {
            if (meanAlign < 0.35)
                hints.Add("Mean exit jet direction is still strongly off-axis — review tangential injection vs mixing length before expander.");
            if (wallHits > 25 * n)
                hints.Add("High wall interaction count — jets may be hugging the bore; check injector clocking / pitch or chamber L/D.");
            if (meanTraced > legacyLen * 1.45)
                hints.Add("Traced paths are much longer than axial legacy span — strong swirl / deflection; swirl-to-expander transition may need design attention.");
        }

        return new JetTrajectoryResult
        {
            TrajectoriesByInjector = byInj,
            LegacyMeanPathLengthMm = legacyLen,
            TracedMeanPathLengthMm = meanTraced,
            TotalWallDeflectionSteps = wallHits,
            TotalJetInteractionSteps = jetHits,
            MeanFinalAxisAlignment = meanAlign,
            GeometryGuidanceHints = hints
        };
    }

    /// <summary>
    /// Approximate gas-side bore: interior of inner wall = r &lt; R_inner(x). Normal points from wall into fluid (radially outward in cross-section).
    /// </summary>
    private static bool TryInnerBoreRadiusMm(GeometryAssemblyPath p, double x, out double rInnerMm)
    {
        rInnerMm = 0.0;
        if (x < p.XInletStart - 1e-3 || x > p.XAfterExit + 2.0)
            return false;

        if (x < p.XAfterInlet)
        {
            double span = Math.Max(p.XAfterInlet - p.XInletStart, 1e-6);
            double t = Math.Clamp((x - p.XInletStart) / span, 0.0, 1.0);
            rInnerMm = p.EntranceInnerRadiusMm * (1.0 - t) + p.ChamberInnerRadiusMm * t;
            return true;
        }

        if (x <= p.XAfterSwirl)
        {
            rInnerMm = p.ChamberInnerRadiusMm;
            return true;
        }

        if (x <= p.XAfterExpander)
        {
            double len = Math.Max(p.XAfterExpander - p.XExpanderStart, 1e-6);
            double t = Math.Clamp((x - p.XExpanderStart) / len, 0.0, 1.0);
            rInnerMm = p.ChamberInnerRadiusMm * (1.0 - t) + p.ExpanderEndInnerRadiusMm * t;
            return true;
        }

        rInnerMm = p.RecoveryAnnulusInnerRadiusMm;
        return true;
    }

    private static bool DomainContains(GeometryAssemblyPath path, Vector3 pMm)
    {
        if (pMm.X < path.XInletStart - 2.0 || pMm.X > path.XAfterExit + 3.0)
            return false;
        return TryInnerBoreRadiusMm(path, pMm.X, out double ri)
            && RadiusMm(pMm) <= ri + 0.5;
    }

    private static double RadiusMm(Vector3 p)
    {
        double y = p.Y;
        double z = p.Z;
        return Math.Sqrt(y * y + z * z);
    }

    /// <summary>
    /// When the point lies near the cylindrical/conical bore wall, returns clearance [mm] and outward (into-fluid) normal.
    /// </summary>
    private static bool TryBoreWallInteraction(
        GeometryAssemblyPath path,
        NozzleDesignInputs design,
        Vector3 pMm,
        out Vector3 normalIntoFluid,
        out double clearanceMm)
    {
        normalIntoFluid = Vector3.UnitX;
        clearanceMm = 1e9;
        if (!TryInnerBoreRadiusMm(path, pMm.X, out double rWall))
            return false;

        double r = RadiusMm(pMm);
        if (r < 1e-6)
            return false;

        clearanceMm = rWall - r;
        double ny = pMm.Y / r;
        double nz = pMm.Z / r;
        normalIntoFluid = new Vector3(0f, (float)ny, (float)nz);

        return clearanceMm < WallInfluenceScaleMm * 3.0 || clearanceMm < 0.0;
    }
}
