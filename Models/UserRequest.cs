using System.ComponentModel.DataAnnotations;
namespace UserManagementAPI.Models;
public sealed class UserRequest
{
    [Required, StringLength(100)]
    [RegularExpression(@"[\s\S]*\p{L}[\s\S]*", ErrorMessage = "First name must contain at least one letter.")]
    public string FirstName { get; init; } = string.Empty;
    [Required, StringLength(100)]
    [RegularExpression(@"[\s\S]*\p{L}[\s\S]*", ErrorMessage = "Last name must contain at least one letter.")]
    public string LastName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;
}
