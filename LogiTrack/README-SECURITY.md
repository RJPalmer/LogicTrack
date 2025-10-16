JWT secret setup and environment/user-secrets guidance

1) Recommended: use dotnet user-secrets (during development)

# from project folder
dotnet user-secrets init
# set secret
dotnet user-secrets set "JwtSettings:Secret" "<your-very-strong-secret>"

2) Recommended for production: set an environment variable (on host/container) named JwtSettings__Secret

# macOS / bash / zsh example:
export JwtSettings__Secret="<your-very-strong-secret>"

# systemd unit file example (on Linux):
# Environment=JwtSettings__Secret=<your-very-strong-secret>

3) Optional: set Issuer / Audience and ExpMinutes via appsettings.json, user-secrets, or environment variables.
Example appsettings.json snippet:

"JwtSettings": {
  "Issuer": "LogiTrack",
  "Audience": "LogiTrackClients",
  "Secret": "(do not store production secrets in source)",
  "ExpMinutes": 60
}

Notes:
- Never commit production secrets into source control.
- Prefer a secrets manager (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) for production.
- Use a long random secret (recommended >= 32 bytes) and rotate keys periodically.
- Consider asymmetric keys (RSA) for signing if multiple services need to validate tokens without sharing a symmetric key.
