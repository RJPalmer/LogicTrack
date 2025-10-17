using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;

/// <summary>
/// Data Transfer Object for user registration requests.
/// </summary>
public class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public IEnumerable<string> Roles { get; set; } = new List<string>();
}

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    public IEnumerable<string> Roles { get; internal set; } = new List<string>();
}
