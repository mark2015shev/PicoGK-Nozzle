namespace PicoGK_Run.Physics.Continuous;

/// <summary>Axial segment tag for the reduced-order continuous path (engineering stations, not CFD zones).</summary>
public enum NozzleSegmentKind
{
    InletCoupled = 0,
    SwirlChamber = 1,
    Expander = 2,
    Stator = 3,
    Exit = 4
}
