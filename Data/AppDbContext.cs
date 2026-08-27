using Microsoft.EntityFrameworkCore;
using MiniHR.Models;

namespace MiniHR.Data;

public class AppDbContext : DbContext
{
    private readonly Guid _orgId;
    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : base(options) => _orgId = tenant.OrgId;

    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveRequest> Leaves => Set<LeaveRequest>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        if (Database.IsNpgsql()) b.HasDefaultSchema("minihr");
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<Department>(e => { e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique(); e.HasQueryFilter(x => x.OrgId == _orgId); });
        b.Entity<Employee>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
            e.Property(x => x.BaseSalary).HasPrecision(18, 2);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<LeaveRequest>(e =>
        {
            e.Ignore(x => x.Days);
            e.HasOne(x => x.Employee).WithMany(x => x.Leaves).HasForeignKey(x => x.EmployeeId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<PayrollRun>(e =>
        {
            e.HasIndex(x => new { x.OrgId, x.Period }).IsUnique();
            e.Ignore(x => x.Total);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
        b.Entity<PayrollLine>(e =>
        {
            e.Ignore(x => x.Net);
            e.Property(x => x.BaseSalary).HasPrecision(18, 2);
            e.Property(x => x.Allowance).HasPrecision(18, 2);
            e.Property(x => x.Deduction).HasPrecision(18, 2);
            e.HasOne(x => x.Run).WithMany(x => x.Lines).HasForeignKey(x => x.RunId);
            e.HasQueryFilter(x => x.OrgId == _orgId);
        });
    }

    public override int SaveChanges() { StampOrg(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default) { StampOrg(); return base.SaveChangesAsync(ct); }
    private void StampOrg()
    {
        foreach (var e in ChangeTracker.Entries<IOrgOwned>())
            if (e.State == EntityState.Added && e.Entity.OrgId == Guid.Empty) e.Entity.OrgId = _orgId;
    }
}
