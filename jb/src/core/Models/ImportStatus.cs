namespace Prism.Contracts;

/// <summary> Import outcome for a single image. </summary>
public enum ImportStatus {
    /// <summary>Not yet processed by the Imported stage.</summary>
    Pending = 0,

    /// <summary>Successfully imported and normalized.</summary>
    Ok = 1,

    /// <summary>Failed during import; excluded from downstream stages.</summary>
    KO = 2
}
