using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParNegar.Application.Interfaces.Services.Auth;
using ParNegar.Shared.DTOs.Auth;

namespace ParNegar.API.Controllers.Auth;

/// <summary>
/// Users management controller
/// Route: /api/Auth/Users
/// </summary>
[Authorize]
[ApiController]
[Route("api/Auth/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get user by GUID
    /// </summary>
    /// <param name="guid">User GUID</param>
    [HttpGet("{guid:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGuid(Guid guid, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByGuidAsync(guid, cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// Get users with pagination and filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ParNegar.Shared.DTOs.Common.PagedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] UserFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _userService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get users for combobox (ID + GUID + Name)
    /// </summary>
    [HttpGet("combobox")]
    [ProducesResponseType(typeof(List<UserComboboxDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCombobox(CancellationToken cancellationToken)
    {
        var result = await _userService.GetComboboxAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create new user
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetByGuid), new { guid = user.GUID }, user);
    }

    /// <summary>
    /// Update user by GUID
    /// ⚠️ Uses GUID from URL, not from body
    /// </summary>
    [HttpPut("{guid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid guid, [FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        await _userService.UpdateAsync(guid, dto, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Delete user by GUID (Soft Delete)
    /// </summary>
    [HttpDelete("{guid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid guid, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(guid, cancellationToken);
        return NoContent();
    }
}
