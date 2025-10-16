
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Application user class extending IdentityUser with int as the key type.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    
    /// <summary>
    /// Default constructor.
    /// </summary>
    public ApplicationUser() : base() { }
}