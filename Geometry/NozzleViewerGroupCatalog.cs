namespace PicoGK_Run.Geometry;

/// <summary>
/// Order and naming match the primary six segments in <see cref="Infrastructure.AppPipeline.DisplayGeometryInViewer"/>.
/// When <see cref="NozzleGeometryResult.JetTrajectoryDebug"/> is present, the viewer adds a seventh group (teal) after these.
/// </summary>
public static class NozzleViewerGroupCatalog
{
    public sealed record Entry(int GroupId, string DisplayName, string NozzleGeometryResultProperty, string ColorHex);

    public static IReadOnlyList<Entry> Ordered => new[]
    {
        new Entry(1, "Inlet", nameof(NozzleGeometryResult.Inlet), NozzleViewerSegmentColors.InletHex),
        new Entry(2, "Swirl chamber", nameof(NozzleGeometryResult.SwirlChamber), NozzleViewerSegmentColors.SwirlChamberHex),
        new Entry(3, "Injector reference markers", nameof(NozzleGeometryResult.InjectorReferenceMarkers), NozzleViewerSegmentColors.InjectorReferenceMarkersHex),
        new Entry(4, "Expander", nameof(NozzleGeometryResult.Expander), NozzleViewerSegmentColors.ExpanderHex),
        new Entry(5, "Stator section", nameof(NozzleGeometryResult.StatorSection), NozzleViewerSegmentColors.StatorSectionHex),
        new Entry(6, "Exit", nameof(NozzleGeometryResult.Exit), NozzleViewerSegmentColors.ExitHex)
    };
}
