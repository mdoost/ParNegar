using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParNegar.Application.Extensions;
using ParNegar.Application.Interfaces.Services;
using ParNegar.Application.Interfaces.Services.Core;
using ParNegar.Domain.Entities.Core;
using ParNegar.Domain.Interfaces;
using ParNegar.Shared.DTOs.Common;
using ParNegar.Shared.DTOs.Core;
using ParNegar.Shared.Exceptions;

namespace ParNegar.Infrastructure.Services.Core;

public class BranchService : IBranchService
{
    private readonly IRepository<Branch> _branchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<BranchService> _logger;

    public BranchService(
        IRepository<Branch> branchRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILogger<BranchService> logger)
    {
        _branchRepository = branchRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<BranchDto> GetByGuidAsync(Guid guid, CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.Query()
            .FirstOrDefaultAsync(b => b.GUID == guid, cancellationToken);

        if (branch == null)
        {
            throw new EntityNotFoundException(nameof(Branch), guid);
        }

        return branch.Adapt<BranchDto>();
    }

    public async Task<PagedResult<BranchDto>> GetPagedAsync(
        BranchFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Branch> query = _branchRepository.Query();

        // Apply filters
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            query = query.Where(b =>
                b.Name.Contains(filter.SearchTerm) ||
                b.NameFa.Contains(filter.SearchTerm) ||
                b.Code.Contains(filter.SearchTerm));
        }

        // Apply sorting
        query = filter.SortBy?.ToLower() switch
        {
            "name" => filter.SortDescending
                ? query.OrderByDescending(b => b.Name)
                : query.OrderBy(b => b.Name),
            "namefa" => filter.SortDescending
                ? query.OrderByDescending(b => b.NameFa)
                : query.OrderBy(b => b.NameFa),
            "code" => filter.SortDescending
                ? query.OrderByDescending(b => b.Code)
                : query.OrderBy(b => b.Code),
            _ => filter.SortDescending
                ? query.OrderByDescending(b => b.CreatedDate)
                : query.OrderBy(b => b.CreatedDate)
        };

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var branches = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = branches.Select(b => b.Adapt<BranchDto>()).ToList();

        return new PagedResult<BranchDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
    }

    public async Task<List<BranchComboboxDto>> GetComboboxAsync(CancellationToken cancellationToken = default)
    {
        return await _branchRepository.Query()
            .OrderBy(b => b.NameFa)
            .Select(b => new BranchComboboxDto
            {
                ID = b.ID,
                GUID = b.GUID,
                Name = b.Name,
                NameFa = b.NameFa
            })
            .ToListAsync(cancellationToken)
            .CachedList("Entity:Branch:combobox");
    }

    public async Task<BranchDto> CreateAsync(CreateBranchDto dto, CancellationToken cancellationToken = default)
    {
        // Check duplicate code
        if (await _branchRepository.ExistsAsync(b => b.Code == dto.Code, cancellationToken))
        {
            throw new BusinessValidationException("Branch code already exists");
        }

        var branch = dto.Adapt<Branch>();

        await _branchRepository.AddAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Branch {BranchName} created by {CurrentUser}", branch.NameFa, _currentUser.Username);

        return await GetByGuidAsync(branch.GUID, cancellationToken);
    }

    public async Task UpdateAsync(Guid guid, UpdateBranchDto dto, CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.GetByGuidAsync(guid, cancellationToken);

        if (branch == null)
        {
            throw new EntityNotFoundException(nameof(Branch), guid);
        }

        // Check duplicate code if changed
        if (branch.Code != dto.Code)
        {
            if (await _branchRepository.ExistsAsync(b => b.Code == dto.Code && b.GUID != guid, cancellationToken))
            {
                throw new BusinessValidationException("Branch code already exists");
            }
        }

        // Update properties
        branch.Name = dto.Name;
        branch.NameFa = dto.NameFa;
        branch.Code = dto.Code;
        branch.Address = dto.Address;
        branch.Phone = dto.Phone;
        branch.Email = dto.Email;
        branch.ManagerName = dto.ManagerName;

        await _branchRepository.UpdateAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Branch {BranchName} updated by {CurrentUser}", branch.NameFa, _currentUser.Username);
    }

    public async Task DeleteAsync(Guid guid, CancellationToken cancellationToken = default)
    {
        var branch = await _branchRepository.GetByGuidAsync(guid, cancellationToken);

        if (branch == null)
        {
            throw new EntityNotFoundException(nameof(Branch), guid);
        }

        await _branchRepository.DeleteAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Branch {BranchName} deleted by {CurrentUser}", branch.NameFa, _currentUser.Username);
    }
}
