using Microsoft.EntityFrameworkCore;
using MiniHR.Data;
using MiniHR.Models;

namespace MiniHR.Services;

public record HrDash(int Headcount, int OnLeave, int PendingLeaves, decimal PayrollMonth, List<(string Dept, int Count)> ByDept);

public interface IHrService
{
    Task<List<Department>> DepartmentsAsync();
    Task<Dictionary<int, int>> HeadcountByDeptAsync();
    Task<int> CreateDepartmentAsync(Department d);
    Task<List<Employee>> EmployeesAsync(string? q, int? deptId);
    Task<Employee?> GetEmployeeAsync(int id);
    Task<int> CreateEmployeeAsync(Employee e);
    Task<List<LeaveRequest>> LeavesAsync(LeaveStatus? status);
    Task<int> FileLeaveAsync(LeaveRequest l);
    Task<(bool ok, string msg)> ApproveLeaveAsync(int id, bool approve);
    Task<List<PayrollRun>> PayrollsAsync();
    Task<PayrollRun?> GetPayrollAsync(int id);
    Task<(bool ok, string msg, int id)> RunPayrollAsync(string period);
    Task ClosePayrollAsync(int id);
    Task<HrDash> DashboardAsync();
}

public class HrService(AppDbContext db) : IHrService
{
    public Task<List<Department>> DepartmentsAsync() => db.Departments.OrderBy(d => d.Name).ToListAsync();
    public async Task<Dictionary<int, int>> HeadcountByDeptAsync() =>
        await db.Employees.Where(e => e.DepartmentId != null && e.Status == EmpStatus.Active)
            .GroupBy(e => e.DepartmentId!.Value).Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C);
    public async Task<int> CreateDepartmentAsync(Department d)
    {
        if (string.IsNullOrWhiteSpace(d.Code)) d.Code = $"PB{await db.Departments.CountAsync() + 1:D2}";
        db.Departments.Add(d); await db.SaveChangesAsync(); return d.Id;
    }

    public async Task<List<Employee>> EmployeesAsync(string? q, int? deptId)
    {
        var query = db.Employees.Include(e => e.Department).AsQueryable();
        if (deptId.HasValue) query = query.Where(e => e.DepartmentId == deptId.Value);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(e => e.FullName.Contains(q) || e.Code.Contains(q) || (e.Phone ?? "").Contains(q));
        return await query.OrderBy(e => e.Code).ToListAsync();
    }
    public Task<Employee?> GetEmployeeAsync(int id) =>
        db.Employees.Include(e => e.Department).Include(e => e.Leaves).FirstOrDefaultAsync(e => e.Id == id);
    public async Task<int> CreateEmployeeAsync(Employee e)
    {
        if (string.IsNullOrWhiteSpace(e.Code)) e.Code = $"NV{await db.Employees.CountAsync() + 1:D4}";
        db.Employees.Add(e); await db.SaveChangesAsync(); return e.Id;
    }

    public async Task<List<LeaveRequest>> LeavesAsync(LeaveStatus? status)
    {
        var q = db.Leaves.Include(l => l.Employee).AsQueryable();
        if (status.HasValue) q = q.Where(l => l.Status == status.Value);
        var list = await q.ToListAsync();
        return list.OrderByDescending(l => l.CreatedAt).ToList();
    }
    public async Task<int> FileLeaveAsync(LeaveRequest l) { db.Leaves.Add(l); await db.SaveChangesAsync(); return l.Id; }

    public async Task<(bool ok, string msg)> ApproveLeaveAsync(int id, bool approve)
    {
        var l = await db.Leaves.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return (false, "Không tìm thấy đơn.");
        if (l.Status != LeaveStatus.Pending) return (false, "Đơn đã xử lý.");
        l.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        if (approve && l.Type == LeaveType.Annual) l.Employee.AnnualLeaveDays = Math.Max(0, l.Employee.AnnualLeaveDays - l.Days);
        await db.SaveChangesAsync();
        return (true, approve ? "Đã duyệt nghỉ phép." : "Đã từ chối.");
    }

    public async Task<List<PayrollRun>> PayrollsAsync() =>
        (await db.PayrollRuns.Include(p => p.Lines).ToListAsync()).OrderByDescending(p => p.Period).ToList();
    public Task<PayrollRun?> GetPayrollAsync(int id) => db.PayrollRuns.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<(bool ok, string msg, int id)> RunPayrollAsync(string period)
    {
        if (string.IsNullOrWhiteSpace(period)) return (false, "Cần kỳ (yyyy-MM).", 0);
        if (await db.PayrollRuns.AnyAsync(p => p.Period == period)) return (false, "Kỳ này đã có bảng lương.", 0);
        if (!DateTime.TryParseExact(period + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var monthStart))
            return (false, "Kỳ không hợp lệ (yyyy-MM).", 0);
        var monthEnd = monthStart.AddMonths(1);
        var emps = await db.Employees.Where(e => e.Status == EmpStatus.Active).ToListAsync();
        // Nghỉ không lương đã duyệt có ngày bắt đầu trong kỳ.
        var unpaidLeaves = await db.Leaves.Where(l => l.Status == LeaveStatus.Approved && l.Type == LeaveType.Unpaid
                && l.FromDate >= monthStart && l.FromDate < monthEnd).ToListAsync();
        var run = new PayrollRun { Period = period };
        foreach (var e in emps)
        {
            var days = unpaidLeaves.Where(l => l.EmployeeId == e.Id).Sum(x => x.Days);
            var perDay = e.BaseSalary / 26m;
            run.Lines.Add(new PayrollLine { EmployeeId = e.Id, EmployeeName = e.FullName, BaseSalary = e.BaseSalary,
                Allowance = 0, Deduction = Math.Round(perDay * days + e.BaseSalary * 0.105m, 0) });   // 10.5% BHXH/BHYT/BHTN
        }
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();
        return (true, $"Đã tính lương kỳ {period} ({emps.Count} NV).", run.Id);
    }

    public async Task ClosePayrollAsync(int id)
    {
        var p = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException();
        p.Closed = true; await db.SaveChangesAsync();
    }

    public async Task<HrDash> DashboardAsync()
    {
        var emps = await db.Employees.Include(e => e.Department).ToListAsync();
        var monthStart = DateTime.Today.ToString("yyyy-MM");
        var lastRun = await db.PayrollRuns.Include(p => p.Lines).OrderByDescending(p => p.Period).FirstOrDefaultAsync();
        var byDept = emps.Where(e => e.Status == EmpStatus.Active).GroupBy(e => e.Department?.Name ?? "(chưa PB)")
            .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList();
        return new HrDash(
            emps.Count(e => e.Status == EmpStatus.Active),
            emps.Count(e => e.Status == EmpStatus.OnLeave),
            await db.Leaves.CountAsync(l => l.Status == LeaveStatus.Pending),
            lastRun?.Total ?? 0, byDept);
    }
}
