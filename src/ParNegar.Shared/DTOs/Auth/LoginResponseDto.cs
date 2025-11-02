using ParNegar.Shared.DTOs.Common;

namespace ParNegar.Shared.DTOs.Auth;

public class LoginResponseDto
{
    public TokenResponseDto Token { get; set; } = new();
    public UserDto User { get; set; } = new();
}
