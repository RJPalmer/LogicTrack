namespace LogiTrack.Controllers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using LogiTrack.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly ILogger<AuthController> _logger;
    private readonly LogiTrack.Services.JwtService _jwtService;

    public AuthController(UserManager<ApplicationUser> userManager, ILogger<AuthController> logger, LogiTrack.Services.JwtService jwtService, RoleManager<IdentityRole<int>> roleManager)
    {
        _userManager = userManager;
        _logger = logger;
        _jwtService = jwtService;
        _roleManager = roleManager;
    }

    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { Message = "Invalid registration data." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return StatusCode(500, new { Message = "Internal server error while creating user." });
        }
        //retrieve the user from the database
        user = await _userManager.FindByEmailAsync(request.Email);
        foreach (var role in request.Roles)
        {

            if (user == null)
            {
                _logger.LogError("User {Email} not found when assigning role {Role}", request.Email, role);
                return StatusCode(500, new { Message = "Internal server error while assigning roles." });
            }
            if (!await _userManager.IsInRoleAsync(user, role))
            {
                try
                {
                    //check that the role exists
                    if (!await _roleManager.RoleExistsAsync(role))
                    {
                        _logger.LogWarning("Role {Role} does not exist.", role);

                        //add the role and continue
                        var createRoleResult = await _roleManager.CreateAsync(new IdentityRole<int>(role));
                        if (!createRoleResult.Succeeded)
                        {
                            _logger.LogError("Error creating role {Role}: {Errors}", role, string.Join(", ", createRoleResult.Errors.Select(e => e.Description)));
                            return StatusCode(500, new { Message = "Internal server error while creating roles." });
                        }
                    }
                    var roleResult = await _userManager.AddToRoleAsync(user, role);
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError("Error adding user {Email} to role {Role}: {Errors}", request.Email, role, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                        return StatusCode(500, new { Message = "Internal server error while assigning roles." });
                    }
                    //return Ok(new { Message = "User registered successfully." });
                    //save changes
                    await _userManager.UpdateAsync(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error assigning role {Role} to user {Email}", role, request.Email);
                    return StatusCode(500, new { Message = "Internal server error while assigning roles." });
                }
            }
        }
        _logger.LogInformation("User {Email} registered successfully with roles: {Roles}", request.Email, string.Join(", ", request.Roles));
        return Ok(new { Message = "User registered successfully." });
        

        // Log detailed errors server-side but return a sanitized message to the client
        // _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, string.Join(';', result.Errors.Select(e => e.Code)));
        // return BadRequest(new { Message = "Could not create account. Please check the submitted data." });
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(new { Message = "Invalid login data." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal whether user exists
            _logger.LogInformation("Login attempt for unknown email: {Email}", request.Email);
            return Unauthorized();
        }

        // Check password and optionally check email confirmed if configured
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogInformation("Invalid password for {Email}", request.Email);
            return Unauthorized();
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        var authClaims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.UserName),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),

        };

        foreach(var role in userRoles){
            authClaims.Add(new Claim(ClaimTypes.Role, role));
        }
        var token = _jwtService.CreateToken(user, authClaims);
        return Ok(new { Token = token });
    }
}