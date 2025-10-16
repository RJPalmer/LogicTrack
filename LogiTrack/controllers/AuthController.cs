namespace LogiTrack.Controllers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using LogiTrack.Models;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthController> _logger;
    private readonly LogiTrack.Services.JwtService _jwtService;

    public AuthController(UserManager<ApplicationUser> userManager, ILogger<AuthController> logger, LogiTrack.Services.JwtService jwtService)
    {
        _userManager = userManager;
        _logger = logger;
        _jwtService = jwtService;
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
        if (result.Succeeded)
        {
            // Optionally: send confirmation email here via IEmailSender
            return Ok(new { Message = "Registration successful. Please verify your email if required." });
        }

        // Log detailed errors server-side but return a sanitized message to the client
        _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, string.Join(';', result.Errors.Select(e => e.Code)));
        return BadRequest(new { Message = "Could not create account. Please check the submitted data." });
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

        var token = _jwtService.CreateToken(user);
        return Ok(new { Token = token });
    }
}