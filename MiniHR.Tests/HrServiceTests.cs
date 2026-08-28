using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniHR.Data;
using MiniHR.Models;
using MiniHR.Services;
using Xunit;

namespace MiniHR.Tests;

/// <summary>Test nhân sự: duyệt nghỉ phép, chạy bảng lương (Net=CB+PC−KT), quân số theo phòng, số ngày nghỉ.</summary>
public class HrServiceTests
{
    private static (AppDbContext db, IHrService svc, SqliteConnection conn) NewSvc()
    {
        var conn = new SqliteConnection("DataSource=:memory:"); conn.Open();
        var opt = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(opt, new TenantContext { OrgId = TenantContext.DefaultOrgId });
        db.Database.EnsureCreated();
        return (db, new HrService(db), conn);
    }

    private static async Task<int> NewEmp(IHrService svc, decimal salary = 15_000_000, int? deptId = null)
        => await svc.CreateEmployeeAsync(new Employee { FullName = "NV A", Code = "E1", BaseSalary = salary, DepartmentId = deptId });

    [Fact]
    public async Task ApproveLeave_SetsApproved()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var eid = await NewEmp(svc);
            var lid = await svc.FileLeaveAsync(new LeaveRequest { EmployeeId = eid, Type = LeaveType.Annual, FromDate = DateTime.Today, ToDate = DateTime.Today.AddDays(2) });
            var (ok, _) = await svc.ApproveLeaveAsync(lid, true);
            Assert.True(ok);
            var l = (await svc.LeavesAsync(LeaveStatus.Approved)).First();
            Assert.Equal(3, l.Days);   // 3 ngày (from..to inclusive)
        }
    }

    [Fact]
    public async Task RejectLeave_SetsRejected()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var eid = await NewEmp(svc);
            var lid = await svc.FileLeaveAsync(new LeaveRequest { EmployeeId = eid, Type = LeaveType.Sick, FromDate = DateTime.Today, ToDate = DateTime.Today });
            await svc.ApproveLeaveAsync(lid, false);
            Assert.Empty(await svc.LeavesAsync(LeaveStatus.Approved));
            Assert.Single(await svc.LeavesAsync(LeaveStatus.Rejected));
        }
    }

    [Fact]
    public async Task RunPayroll_CreatesLinesWithNet()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await NewEmp(svc, 15_000_000);
            var (ok, _, id) = await svc.RunPayrollAsync("2026-08");
            Assert.True(ok);
            var p = await svc.GetPayrollAsync(id);
            Assert.Single(p!.Lines);
            Assert.Equal(p.Lines[0].BaseSalary + p.Lines[0].Allowance - p.Lines[0].Deduction, p.Lines[0].Net);
            Assert.True(p.Total > 0);
        }
    }

    [Fact]
    public async Task RunPayroll_SamePeriodTwice_Blocked()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await NewEmp(svc);
            await svc.RunPayrollAsync("2026-08");
            var (ok, _, _) = await svc.RunPayrollAsync("2026-08");
            Assert.False(ok);   // đã có kỳ này
        }
    }

    [Fact]
    public async Task Headcount_ByDepartment()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            var dept = await svc.CreateDepartmentAsync(new Department { Code = "IT", Name = "CNTT" });
            await NewEmp(svc, deptId: dept);
            await svc.CreateEmployeeAsync(new Employee { FullName = "NV B", Code = "E2", DepartmentId = dept });
            var hc = await svc.HeadcountByDeptAsync();
            Assert.Equal(2, hc[dept]);
        }
    }

    [Fact]
    public async Task Dashboard_HeadcountAndPayroll()
    {
        var (db, svc, conn) = NewSvc(); using (conn)
        {
            await NewEmp(svc, 10_000_000);
            var d = await svc.DashboardAsync();
            Assert.Equal(1, d.Headcount);
        }
    }
}
