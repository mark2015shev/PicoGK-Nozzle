namespace PicoGK_Run.Physics;

/// <summary>
/// SI-path constants (composition root + injector discharge). Legacy <see cref="NozzlePhysicsSolver"/> mirrors the blend for parity.
/// </summary>
public static class SiFlowPhysicsConstants
{
    /// <summary>Weight on V_core×(A_source/A_inj) in jet-speed driver; remainder ṁ/(ρ A_inj).</summary>
    public const double InjectorJetVelocityDriverBlend = 0.88;
}
