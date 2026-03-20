namespace PicoGK_Run.Geometry;

/// <summary>
/// Distinct viewer materials per nozzle segment (hex + PicoGK roughness / metallic).
/// </summary>
public static class NozzleViewerSegmentColors
{
    public const string InletHex = "#5DADE2";
    public const string SwirlChamberHex = "#48BF84";
    public const string InjectorReferenceMarkersHex = "#FF6B6B";
    public const string ExpanderHex = "#FFD166";
    public const string StatorSectionHex = "#9B5DE5";
    public const string ExitHex = "#A8A8A8";

    public const float Roughness = 0.02f;
    public const float Metallic = 0.42f;

    public static readonly (string Name, string Hex)[] Segments =
    {
        ("Inlet", InletHex),
        ("Swirl chamber", SwirlChamberHex),
        ("Injector ref. markers", InjectorReferenceMarkersHex),
        ("Expander", ExpanderHex),
        ("Stator section", StatorSectionHex),
        ("Exit", ExitHex)
    };
}
