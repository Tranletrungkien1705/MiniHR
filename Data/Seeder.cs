using Microsoft.EntityFrameworkCore;
using MiniHR.Models;
namespace MiniHR.Data;
public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await MigratePostgresAsync(db);
        if (!await db.Orgs.AnyAsync(o => o.Id == TenantContext.DefaultOrgId))
        { db.Orgs.Add(new Org { Id = TenantContext.DefaultOrgId, Name = "Demo HR", ApiKey = TenantContext.DefaultApiKey }); await db.SaveChangesAsync(); }
        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(new Department { Code = "KD", Name = "Kinh doanh" }, new Department { Code = "KT", Name = "Kế toán" }, new Department { Code = "KHO", Name = "Kho vận" });
            await db.SaveChangesAsync();
        }
        if (!await db.Employees.AnyAsync())
        {
            var depts = await db.Departments.ToListAsync();
            int D(string c) => depts.First(x => x.Code == c).Id;
            db.Employees.AddRange(
                new Employee { Code = "NV0001", FullName = "Nguyễn Văn An", Position = "Trưởng phòng KD", DepartmentId = D("KD"), Phone = "0901111111", BaseSalary = 25_000_000, JoinDate = DateTime.Today.AddYears(-3) },
                new Employee { Code = "NV0002", FullName = "Trần Thị Bình", Position = "NV Kinh doanh", DepartmentId = D("KD"), Phone = "0902222222", BaseSalary = 12_000_000, JoinDate = DateTime.Today.AddYears(-1) },
                new Employee { Code = "NV0003", FullName = "Lê Hoàng Cường", Position = "Kế toán viên", DepartmentId = D("KT"), Phone = "0903333333", BaseSalary = 14_000_000, JoinDate = DateTime.Today.AddMonths(-8) },
                new Employee { Code = "NV0004", FullName = "Phạm Thu Dung", Position = "Thủ kho", DepartmentId = D("KHO"), Phone = "0904444444", BaseSalary = 10_000_000, JoinDate = DateTime.Today.AddMonths(-4) });
            await db.SaveChangesAsync();
            var e = await db.Employees.FirstAsync(x => x.Code == "NV0002");
            db.Leaves.Add(new LeaveRequest { EmployeeId = e.Id, Type = LeaveType.Annual, FromDate = DateTime.Today.AddDays(3), ToDate = DateTime.Today.AddDays(4), Reason = "Việc gia đình" });
            await db.SaveChangesAsync();
        }
    }
    private static async Task MigratePostgresAsync(AppDbContext db)
    {
        if (!db.Database.IsNpgsql()) return;
        var def = TenantContext.DefaultOrgId;
        var tables = new[] { "Departments", "Employees", "Leaves", "PayrollRuns", "PayrollLines" };
        var sql = new List<string> {
            "CREATE TABLE IF NOT EXISTS minihr.\"Orgs\" (\"Id\" uuid PRIMARY KEY, \"Name\" text NOT NULL DEFAULT '', \"ApiKey\" text NOT NULL DEFAULT '', \"CreatedAt\" timestamp NOT NULL DEFAULT now())",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Orgs_ApiKey\" ON minihr.\"Orgs\" (\"ApiKey\")" };
        foreach (var t in tables) sql.Add($"ALTER TABLE minihr.\"{t}\" ADD COLUMN IF NOT EXISTS \"OrgId\" uuid NOT NULL DEFAULT '{def}'");
        foreach (var s in sql) try { await db.Database.ExecuteSqlRawAsync(s); } catch { }
    }
}
