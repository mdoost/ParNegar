using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParNegar.Application.Interfaces.Services.Core;
using ParNegar.Shared.DTOs.Core;

namespace ParNegar.API.Controllers.Core;

/// <summary>
/// Branches management controller
/// Route: /api/Core/Branches
/// </summary>
[Authorize]
[ApiController]
[Route("api/Core/[controller]")]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;
    private readonly ILogger<BranchesController> _logger;

    public BranchesController(
        IBranchService branchService,
        ILogger<BranchesController> logger)
    {
        _branchService = branchService;
        _logger = logger;
    }

    /// <summary>
    /// Get branch by GUID
    /// </summary>
    /// <param name="guid">Branch GUID</param>
    [HttpGet("{guid:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGuid(Guid guid, CancellationToken cancellationToken)
    {
        var branch = await _branchService.GetByGuidAsync(guid, cancellationToken);
        return Ok(branch);
    }

    /// <summary>
    /// Get branches with pagination and filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ParNegar.Shared.DTOs.Common.PagedResult<BranchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] BranchFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _branchService.GetPagedAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get branches for combobox (ID + GUID + Name + NameFa)
    /// </summary>
    [HttpGet("combobox")]
    [ProducesResponseType(typeof(List<BranchComboboxDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCombobox(CancellationToken cancellationToken)
    {
        var result = await _branchService.GetComboboxAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create new branch
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBranchDto dto, CancellationToken cancellationToken)
    {
        var branch = await _branchService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetByGuid), new { guid = branch.GUID }, branch);
    }

    /// <summary>
    /// Update branch by GUID
    /// ⚠️ Uses GUID from URL, not from body
    /// </summary>
    [HttpPut("{guid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid guid, [FromBody] UpdateBranchDto dto, CancellationToken cancellationToken)
    {
        await _branchService.UpdateAsync(guid, dto, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Delete branch by GUID (Soft Delete)
    /// </summary>
    [HttpDelete("{guid:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid guid, CancellationToken cancellationToken)
    {
        await _branchService.DeleteAsync(guid, cancellationToken);
        return NoContent();
    }
}
