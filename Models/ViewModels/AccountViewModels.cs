using System.ComponentModel.DataAnnotations;

namespace DeanFlow.Models.ViewModels;

public sealed class LoginInput
{
    [Required(ErrorMessage = "Электрондық поштаңызды енгізіңіз.")]
    [EmailAddress(ErrorMessage = "Электрондық пошта пішімін тексеріңіз.")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Құпиясөзді енгізіңіз."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ProfileInput
{
    [Required, StringLength(70)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(70)] public string LastName { get; set; } = string.Empty;
    [StringLength(70)] public string? MiddleName { get; set; }
    [Phone, StringLength(30)] public string? Phone { get; set; }
    [DataType(DataType.Password)] public string? CurrentPassword { get; set; }
    [DataType(DataType.Password), MinLength(8)] public string? NewPassword { get; set; }
    [DataType(DataType.Password), Compare(nameof(NewPassword))] public string? ConfirmPassword { get; set; }
}
