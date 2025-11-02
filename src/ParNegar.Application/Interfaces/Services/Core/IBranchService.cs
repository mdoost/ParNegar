using ParNegar.Shared.DTOs.Common;
using ParNegar.Shared.DTOs.Core;

namespace ParNegar.Application.Interfaces.Services.Core;

/// <summary>
/// Branch service interface
/// </summary>
public interface IBranchService
{
    Task<BranchDto> GetByGuidAsync(Guid guid, CancellationToken cancellationToken = default);
    Task<PagedResult<BranchDto>> GetPagedAsync(BranchFilterDto filter, CancellationToken cancellationToken = default);
    Task<List<BranchComboboxDto>> GetComboboxAsync(CancellationToken cancellationToken = default);
    Task<BranchDto> CreateAsync(CreateBranchDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid guid, UpdateBranchDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid guid, CancellationToken cancellationToken = default);
}
