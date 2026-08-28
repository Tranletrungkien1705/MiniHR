using Microsoft.AspNetCore.Mvc;
using MiniHR.Data;
using MiniHR.Models;
using MiniHR.Services;

namespace MiniHR.Controllers;

/// <summary>
/// API JSON cho SPA React. DTO phẳng. Dashboard cache Redis 30s theo tenant (X-Cache).
/// Nhân sự: phòng ban, nhân viên, nghỉ phép (Pending→Approved/Rejected), bảng lương theo kỳ (Net = lương + phụ cấp − khấu trừ).
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ApiV1Controller(IHrService svc, ICache cache, ITenantContext tenant) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var key = $"hr:dash:{tenant.OrgId}";
        var hit = await cache.GetAsync<DashDto>(key);
        if (hit != null) { Response.Headers["X-Cache"] = "HIT"; return Ok(hit); }
        var d = await svc.DashboardAsync();
        var dto = new DashDto(d.Headcount, d.OnLeave, d.PendingLeaves, d.PayrollMonth,
            d.ByDept.Select(x => new ByDeptDto(x.Item1, x.Item2)).ToList());
        await cache.SetAsync(key, dto, TimeSpan.FromSeconds(30));
        Response.Headers["X-Cache"] = "MISS";
        return Ok(dto);
    }

    [HttpGet("departments")]
    public async Task<IActionResult> Departments()
    {
        var depts = await svc.DepartmentsAsync();
        var hc = await svc.HeadcountByDeptAsync();
        return Ok(depts.Select(d => new { d.Id, d.Code, d.Name, headcount = hc.GetValueOrDefault(d.Id) }));
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDept([FromBody] DeptReq r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { error = "Cần tên phòng ban." });
        var id = await svc.CreateDepartmentAsync(new Department { Name = r.Name.Trim(), Code = r.Code ?? "" });
        return Ok(new { id });
    }

    [HttpGet("employees")]
    public async Task<IActionResult> Employees([FromQuery] string? q, [FromQuery] int? deptId)
        => Ok((await svc.EmployeesAsync(q, deptId)).Select(e => new
        {
            e.Id, e.Code, e.FullName, e.Position, dept = e.Department?.Name, e.Phone, e.Email, e.JoinDate, e.BaseSalary, e.AnnualLeaveDays,
            status = (int)e.Status, statusText = Ui.Emp(e.Status).t, statusCss = Ui.Emp(e.Status).css
        }));

    [HttpGet("employees/{id:int}")]
    public async Task<IActionResult> Employee(int id)
    {
        var e = await svc.GetEmployeeAsync(id);
        if (e == null) return NotFound(new { error = "Không tìm thấy nhân viên." });
        return Ok(new
        {
            e.Id, e.Code, e.FullName, e.Position, deptId = e.DepartmentId, dept = e.Department?.Name, e.Phone, e.Email, e.JoinDate, e.BaseSalary, e.AnnualLeaveDays,
            status = (int)e.Status, statusText = Ui.Emp(e.Status).t,
            leaves = e.Leaves.OrderByDescending(l => l.FromDate).Select(l => new { l.Id, type = Ui.Leave(l.Type), l.FromDate, l.ToDate, days = l.Days, status = Ui.LeaveSt(l.Status).t })
        });
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] EmpReq r)
    {
        if (string.IsNullOrWhiteSpace(r.FullName)) return BadRequest(new { error = "Cần họ tên." });
        var id = await svc.CreateEmployeeAsync(new Employee
        {
            FullName = r.FullName.Trim(), Code = r.Code ?? "", Position = r.Position, DepartmentId = r.DepartmentId,
            Phone = r.Phone, Email = r.Email, BaseSalary = r.BaseSalary, AnnualLeaveDays = r.AnnualLeaveDays <= 0 ? 12 : r.AnnualLeaveDays,
            JoinDate = r.JoinDate == default ? DateTime.Today : r.JoinDate
        });
        return Ok(new { id });
    }

    [HttpGet("leaves")]
    public async Task<IActionResult> Leaves([FromQuery] LeaveStatus? status)
        => Ok((await svc.LeavesAsync(status)).Select(l => new
        {
            l.Id, employee = l.Employee?.FullName, type = Ui.Leave(l.Type), l.FromDate, l.ToDate, days = l.Days, l.Reason,
            status = (int)l.Status, statusText = Ui.LeaveSt(l.Status).t, statusCss = Ui.LeaveSt(l.Status).css
        }));

    [HttpPost("leaves")]
    public async Task<IActionResult> FileLeave([FromBody] LeaveReq r)
    {
        var id = await svc.FileLeaveAsync(new LeaveRequest
        {
            EmployeeId = r.EmployeeId, Type = (LeaveType)r.Type,
            FromDate = r.FromDate == default ? DateTime.Today : r.FromDate,
            ToDate = r.ToDate == default ? DateTime.Today : r.ToDate, Reason = r.Reason
        });
        return Ok(new { id });
    }

    [HttpPost("leaves/{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveReq r)
    {
        var (ok, msg) = await svc.ApproveLeaveAsync(id, r.Approve);
        return ok ? Ok(new { ok, msg }) : BadRequest(new { ok, error = msg });
    }

    [HttpGet("payrolls")]
    public async Task<IActionResult> Payrolls()
        => Ok((await svc.PayrollsAsync()).Select(p => new { p.Id, p.Period, p.Closed, lines = p.Lines.Count, total = p.Total, p.CreatedAt }));

    [HttpGet("payrolls/{id:int}")]
    public async Task<IActionResult> Payroll(int id)
    {
        var p = await svc.GetPayrollAsync(id);
        if (p == null) return NotFound(new { error = "Không tìm thấy bảng lương." });
        return Ok(new
        {
            p.Id, p.Period, p.Closed, total = p.Total,
            lines = p.Lines.Select(l => new { l.EmployeeName, l.BaseSalary, l.Allowance, l.Deduction, net = l.Net })
        });
    }

    [HttpPost("payrolls")]
    public async Task<IActionResult> RunPayroll([FromBody] PayrollReq r)
    {
        var (ok, msg, id) = await svc.RunPayrollAsync(r.Period ?? DateTime.Today.ToString("yyyy-MM"));
        return ok ? Ok(new { id }) : BadRequest(new { error = msg });
    }
}

public record DashDto(int Headcount, int OnLeave, int PendingLeaves, decimal PayrollMonth, List<ByDeptDto> ByDept);
public record ByDeptDto(string Dept, int Count);

public class DeptReq { public string Name { get; set; } = ""; public string? Code { get; set; } }
public class EmpReq
{
    public string FullName { get; set; } = ""; public string? Code { get; set; } public string? Position { get; set; }
    public int? DepartmentId { get; set; } public string? Phone { get; set; } public string? Email { get; set; }
    public decimal BaseSalary { get; set; } public int AnnualLeaveDays { get; set; } public DateTime JoinDate { get; set; }
}
public class LeaveReq { public int EmployeeId { get; set; } public int Type { get; set; } public DateTime FromDate { get; set; } public DateTime ToDate { get; set; } public string? Reason { get; set; } }
public class ApproveReq { public bool Approve { get; set; } }
public class PayrollReq { public string? Period { get; set; } }
