namespace Content.Shared.RussStation.MedicalScanner;

/// <summary>
/// Base type for the reagent-scanner extension to the health analyzer. Exists so
/// <see cref="HealthAnalyzerReagentScannerComponent"/>'s <c>[Access]</c> attribute
/// can reference a shared type while the actual logic lives server-side.
/// </summary>
public abstract class SharedHealthAnalyzerReagentSystem : EntitySystem
{
}
