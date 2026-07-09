using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Shared.Validation;

/// <summary>
/// Rejects Guid.Empty — DataAnnotations has no built-in way to require a non-default Guid.
/// </summary>
public class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("{0} must not be an empty GUID.")
    {
    }

    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;
}
