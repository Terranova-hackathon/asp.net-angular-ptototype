using System.ComponentModel.DataAnnotations;

namespace GameHub.Shared.Validation;

/// <summary>Minimal APIs have no built-in model validation in .NET 9, so we do it with DataAnnotations.</summary>
public static class RequestValidator
{
    public static bool TryValidate<T>(T instance, out Dictionary<string, string[]> errors) where T : notnull
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        errors = results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [string.Empty])
                .Select(member => (member, message: r.ErrorMessage ?? "Invalid value.")))
            .GroupBy(x => x.member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.message).ToArray());

        return errors.Count == 0;
    }
}
