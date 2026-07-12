namespace Prism.Config;

/// <summary>
/// Optional self-validation hook for config classes loaded via <see cref="ConfigLoader"/>.
/// Validate() runs immediately after deserialization and must throw when any value is out of range.
/// </summary>
public interface IValidatableConfig {
    void Validate();
}
