using System;
using System.Collections.Generic;
using PicoGK;
using PicoGK_Run.Core;
using PicoGK_Run.Geometry;
using PicoGK_Run.Infrastructure.Pipeline;
using PicoGK_Run.Parameters;
using PicoGK_Run.Physics;
using PicoGK_Run.Physics.JetTrajectory;
using PicoGK_Run.Physics.Solvers;

namespace PicoGK_Run.Infrastructure.Services;

internal static class JetTrajectorySolveService
{
    public sealed record Outcome(JetTrajectoryResult? Trajectory, Voxels? DebugVoxels);

    public static Outcome TrySolve(UnifiedEvaluationResult unified, NozzleInput input, Action<string> logError)
    {
        if (!input.Run.UsePhysicsTracedJetTrajectory || unified.PhysicsStages?.Stage1Injector == null)
            return new Outcome(null, null);

        try
        {
            GeometryAssemblyPath asmPath = GeometryAssemblyPath.Compute(unified.DrivenDesign, input.Run);
            double yawPhysicsDeg = input.Run.LockInjectorYawTo90Degrees
                ? 90.0
                : unified.DrivenDesign.InjectorYawAngleDeg;
            InjectorDischargeResult discharge = unified.PhysicsStages.Stage1Injector;
            double pInj = unified.SiDiagnostics.InjectorPressureVelocity?.ChamberStaticPressureNearInjectorPa
                ?? discharge.ChamberReferenceStaticPressurePa;
            double tK = unified.SiDiagnostics.PhysicsStepStates.Count > 0
                ? unified.SiDiagnostics.PhysicsStepStates[0].TStaticK
                : 300.0;
            IReadOnlyList<InjectorInitialState> initials = JetTrajectorySolver.BuildInitialStates(
                unified.DrivenDesign,
                asmPath.SwirlPlacement,
                discharge,
                yawPhysicsDeg,
                pInj,
                tK,
                discharge.DensityKgM3,
                input.Source.MassFlowKgPerSec);
            JetTrajectoryResult jetTrajectory = JetTrajectorySolver.Solve(
                unified.DrivenDesign,
                input.Run,
                asmPath,
                initials,
                asmPath.SwirlPlacement);
            Voxels? debug = input.Run.BuildJetTrajectoryDebugVoxels
                ? TrajectoryGeometryBuilder.BuildDebugVoxels(jetTrajectory, input.Run)
                : null;
            return new Outcome(jetTrajectory, debug);
        }
        catch (Exception ex)
        {
            logError(ex.Message);
            return new Outcome(null, null);
        }
    }
}
