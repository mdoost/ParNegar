using ParNegar.Shared.DTOs.Auth;
using ParNegar.Shared.DTOs.Common;

namespace ParNegar.Application.Interfaces.Services.Auth;

/// <summary>
/// User service interface
/// </summary>
public interface IUserService
{
    Task<UserDto> GetByGuidAsync(Guid guid, CancellationToken cancellationToken = default);
    Task<PagedResult<UserDto>> GetPagedAsync(UserFilterDto filter, CancellationToken cancellationToken = default);
    Task<List<UserComboboxDto>> GetComboboxAsync(CancellationToken cancellationToken = default);
    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid guid, UpdateUserDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid guid, CancellationToken cancellationToken = default);
}
