using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParNegar.Application.Extensions;
using ParNegar.Application.Interfaces.Services;
using ParNegar.Application.Interfaces.Services.Auth;
using ParNegar.Domain.Entities.Auth;
using ParNegar.Domain.Interfaces;
using ParNegar.Shared.DTOs.Auth;
using ParNegar.Shared.DTOs.Common;
using ParNegar.Shared.Exceptions;
using ParNegar.Shared.Utilities;

namespace ParNegar.Infrastructure.Services.Auth;

public class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUser,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<UserDto> GetByGuidAsync(Guid guid, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.Query()
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.GUID == guid, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException(nameof(User), guid);
        }

        var dto = user.Adapt<UserDto>();
        dto.BranchName = user.Branch?.NameFa ?? string.Empty;

        return dto;
    }

    public async Task<PagedResult<UserDto>> GetPagedAsync(
        UserFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = _userRepository.Query().Include(u => u.Branch);

        // Apply filters
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            query = query.Where(u =>
                u.Username.Contains(filter.SearchTerm) ||
                u.Email.Contains(filter.SearchTerm) ||
                u.FirstName.Contains(filter.SearchTerm) ||
                u.LastName.Contains(filter.SearchTerm));
        }

        if (filter.BranchID.HasValue)
        {
            query = query.Where(u => u.BranchID == filter.BranchID.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == filter.IsActive.Value);
        }

        if (filter.IsLocked.HasValue)
        {
            query = query.Where(u => u.IsLocked == filter.IsLocked.Value);
        }

        // Apply sorting
        query = filter.SortBy?.ToLower() switch
        {
            "username" => filter.SortDescending
                ? query.OrderByDescending(u => u.Username)
                : query.OrderBy(u => u.Username),
            "email" => filter.SortDescending
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email),
            "firstname" => filter.SortDescending
                ? query.OrderByDescending(u => u.FirstName)
                : query.OrderBy(u => u.FirstName),
            _ => filter.SortDescending
                ? query.OrderByDescending(u => u.CreatedDate)
                : query.OrderBy(u => u.CreatedDate)
        };

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var users = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = users.Select(u =>
        {
            var dto = u.Adapt<UserDto>();
            dto.BranchName = u.Branch?.NameFa ?? string.Empty;
            return dto;
        }).ToList();

        return new PagedResult<UserDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
    }

    public async Task<List<UserComboboxDto>> GetComboboxAsync(CancellationToken cancellationToken = default)
    {
        return await _userRepository.Query()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .Select(u => new UserComboboxDto
            {
                ID = u.ID,
                GUID = u.GUID,
                Username = u.Username,
                FullName = $"{u.FirstName} {u.LastName}",
                IsActive = u.IsActive
            })
            .ToListAsync(cancellationToken)
            .CachedList("Entity:User:combobox");
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        // Check duplicate username
        if (await _userRepository.ExistsAsync(u => u.Username == dto.Username, cancellationToken))
        {
            throw new BusinessValidationException("Username already exists");
        }

        // Check duplicate email
        if (await _userRepository.ExistsAsync(u => u.Email == dto.Email, cancellationToken))
        {
            throw new BusinessValidationException("Email already exists");
        }

        var user = dto.Adapt<User>();

        // Hash password
        user.PasswordHash = _passwordHasher.HashPassword(dto.Password);
        user.PasswordSalt = Guid.NewGuid().ToString(); // Simple salt for now

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {Username} created by {CurrentUser}", user.Username, _currentUser.Username);

        return await GetByGuidAsync(user.GUID, cancellationToken);
    }

    public async Task UpdateAsync(Guid guid, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByGuidAsync(guid, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException(nameof(User), guid);
        }

        // Check duplicate username if changed
        if (user.Username != dto.Username)
        {
            if (await _userRepository.ExistsAsync(u => u.Username == dto.Username && u.GUID != guid, cancellationToken))
            {
                throw new BusinessValidationException("Username already exists");
            }
        }

        // Check duplicate email if changed
        if (user.Email != dto.Email)
        {
            if (await _userRepository.ExistsAsync(u => u.Email == dto.Email && u.GUID != guid, cancellationToken))
            {
                throw new BusinessValidationException("Email already exists");
            }
        }

        // Update properties
        user.BranchID = dto.BranchID;
        user.Username = dto.Username;
        user.Email = dto.Email;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Phone = dto.Phone;
        user.Mobile = dto.Mobile;
        user.IsActive = dto.IsActive;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {Username} updated by {CurrentUser}", user.Username, _currentUser.Username);
    }

    public async Task DeleteAsync(Guid guid, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByGuidAsync(guid, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException(nameof(User), guid);
        }

        // Prevent self-deletion
        if (_currentUser.UserId == user.ID)
        {
            throw new BusinessValidationException("Cannot delete your own account");
        }

        await _userRepository.DeleteAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {Username} deleted by {CurrentUser}", user.Username, _currentUser.Username);
    }

    /// <summary>
    /// Calculate Persian date fields for User entity
    /// این متد برای تبدیل تاریخ‌های میلادی به شمسی استفاده می‌شود
    /// </summary>
    private static void CalculatePersianDateFields(User user)
    {
        if (user.PasswordChangeDate.HasValue)
        {
            user.sPasswordChangeDate = PersianDateHelper.ConvertToPersianDateSafe(user.PasswordChangeDate);
        }

        if (user.FirstFailedLoginDate.HasValue)
        {
            user.sFirstFailedLoginDate = PersianDateHelper.ConvertToPersianDateSafe(user.FirstFailedLoginDate);
        }
    }
}
