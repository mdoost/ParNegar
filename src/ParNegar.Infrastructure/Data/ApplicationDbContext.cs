using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ParNegar.Application.Interfaces.Services;
using ParNegar.Domain.Entities;

namespace ParNegar.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        IDateTime dateTime) : base(options)
    {
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    // Auth Schema DbSets
    public DbSet<ParNegar.Domain.Entities.Auth.User> Users { get; set; }
    public DbSet<ParNegar.Domain.Entities.Auth.RefreshToken> RefreshTokens { get; set; }
    public DbSet<ParNegar.Domain.Entities.Auth.TokenBlacklist> TokenBlacklists { get; set; }
    public DbSet<ParNegar.Domain.Entities.Auth.UserRole> UserRoles { get; set; }
    public DbSet<ParNegar.Domain.Entities.Auth.UserLoginLog> UserLoginLogs { get; set; }

    // Core Schema DbSets
    public DbSet<ParNegar.Domain.Entities.Core.Branch> Branches { get; set; }

    // AuthBase Schema DbSets
    public DbSet<ParNegar.Domain.Entities.AuthBase.SystemRole> SystemRoles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply configurations will be added later
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void HandleAuditFields()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var userId = _currentUserService.UserId ?? 0;
        var now = _dateTime.OffsetUtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.CreatedDate = now;
                    entry.Entity.IsActive = true;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedBy = userId;
                    entry.Entity.UpdatedDate = now;
                    break;
            }
        }
    }
}
