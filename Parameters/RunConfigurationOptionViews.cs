namespace PicoGK_Run.Parameters;

/// <summary>Typed slices over <see cref="RunConfiguration"/> so callers depend on a narrow surface.</summary>
public readonly struct AutotunePipelineOptions
{
    private readonly RunConfiguration _r;

    public AutotunePipelineOptions(RunConfiguration run) => _r = run;

    public bool UseAutotune => _r.UseAutotune;

    public bool UseDerivedSwirlChamberDiameter => _r.UseDerivedSwirlChamberDiameter;

    public bool AutotuneUseSynthesisBaseline => _r.AutotuneUseSynthesisBaseline;

    public bool AllowAutotuneDirectChamberDiameterOverride => _r.AllowAutotuneDirectChamberDiameterOverride;
}

public readonly struct GeometryPipelineOptions
{
    private readonly RunConfiguration _r;

    public GeometryPipelineOptions(RunConfiguration run) => _r = run;

    public bool RunGeometryContinuityCheck => _r.RunGeometryContinuityCheck;

    public bool EvaluateGeometryContinuityDuringAutotune => _r.EvaluateGeometryContinuityDuringAutotune;

    public bool UsePhysicsInformedGeometry => _r.UsePhysicsInformedGeometry;
}

public readonly struct DiagnosticsPipelineOptions
{
    private readonly RunConfiguration _r;

    public DiagnosticsPipelineOptions(RunConfiguration run) => _r = run;

    public SiVerbosityLevel SiVerbosityLevel => _r.SiVerbosityLevel;

    public bool EnablePipelineProfiling => _r.EnablePipelineProfiling;
}
