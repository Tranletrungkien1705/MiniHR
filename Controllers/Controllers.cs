using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniHR.Data;
using MiniHR.Models;
using MiniHR.Services;

namespace MiniHR.Controllers;

public class HomeController(IHrService svc) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.Dash = await svc.DashboardAsync(); return View(); }
}

public class DeptController(IHrService svc) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.Counts = await svc.HeadcountByDeptAsync(); return View(await svc.DepartmentsAsync()); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? code)
    {
        if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Cần tên phòng ban."; return RedirectToAction(nameof(Index)); }
        await svc.CreateDepartmentAsync(new Department { Name = name.Trim(), Code = code ?? "" });
        TempData["Success"] = "Đã thêm phòng ban."; return RedirectToAction(nameof(Index));
    }
}

public class EmployeeController(IHrService svc) : Controller
{
    public async Task<IActionResult> Index(string? q, int? deptId)
    {
        ViewBag.Q = q; ViewBag.DeptId = deptId; ViewBag.Depts = await svc.DepartmentsAsync();
        return View(await svc.EmployeesAsync(q, deptId));
    }
    public async Task<IActionResult> Create() { ViewBag.Depts = await svc.DepartmentsAsync(); return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string fullName, string? code, string? position, int? departmentId, string? phone, string? email, decimal baseSalary, DateTime joinDate)
    {
        if (string.IsNullOrWhiteSpace(fullName)) { TempData["Error"] = "Cần tên nhân viên."; ViewBag.Depts = await svc.DepartmentsAsync(); return View(); }
        await svc.CreateEmployeeAsync(new Employee { FullName = fullName.Trim(), Code = code ?? "", Position = position, DepartmentId = departmentId, Phone = phone, Email = email, BaseSalary = baseSalary, JoinDate = joinDate == default ? DateTime.Today : joinDate });
        TempData["Success"] = "Đã thêm nhân viên."; return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var e = await svc.GetEmployeeAsync(id);
        if (e == null) return NotFound();
        return View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FileLeave(int id, LeaveType type, DateTime fromDate, DateTime toDate, string? reason)
    {
        await svc.FileLeaveAsync(new LeaveRequest { EmployeeId = id, Type = type, FromDate = fromDate == default ? DateTime.Today : fromDate, ToDate = toDate == default ? DateTime.Today : toDate, Reason = reason });
        TempData["Success"] = "Đã gửi đơn nghỉ phép."; return RedirectToAction(nameof(Detail), new { id });
    }
}

public class LeaveController(IHrService svc) : Controller
{
    public async Task<IActionResult> Index(LeaveStatus? status) { ViewBag.Status = status; return View(await svc.LeavesAsync(status)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Act(int id, bool approve)
    {
        var (ok, msg) = await svc.ApproveLeaveAsync(id, approve);
        TempData[ok ? "Success" : "Error"] = msg; return RedirectToAction(nameof(Index));
    }
}

public class PayrollController(IHrService svc) : Controller
{
    public async Task<IActionResult> Index() => View(await svc.PayrollsAsync());
    public async Task<IActionResult> Detail(int id) { var p = await svc.GetPayrollAsync(id); return p == null ? NotFound() : View(p); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(string period)
    {
        var (ok, msg, id) = await svc.RunPayrollAsync(period);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Detail), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        await svc.ClosePayrollAsync(id);
        TempData["Success"] = "Đã chốt bảng lương."; return RedirectToAction(nameof(Detail), new { id });
    }
}

public class OrgController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var orgs = await db.Orgs.IgnoreQueryFilters().OrderBy(o => o.CreatedAt).ToListAsync();
        Request.Cookies.TryGetValue(TenantContext.CookieName, out var curKey);
        ViewBag.CurrentKey = curKey ?? TenantContext.DefaultApiKey;
        return View(orgs);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Cần tên tổ chức."; return RedirectToAction(nameof(Index)); }
        var org = new Org { Name = name.Trim(), ApiKey = "hr_" + Guid.NewGuid().ToString("N") };
        db.Orgs.Add(org); await db.SaveChangesAsync();
        SetCookies(org.ApiKey, org.Name);
        TempData["Success"] = $"Đã tạo & chuyển sang \"{org.Name}\"."; return RedirectToAction("Index", "Home");
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch(string apiKey)
    {
        var org = await db.Orgs.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.ApiKey == apiKey);
        if (org == null) { TempData["Error"] = "Không tìm thấy."; return RedirectToAction(nameof(Index)); }
        SetCookies(org.ApiKey, org.Name); return RedirectToAction("Index", "Home");
    }
    public IActionResult Reset() { Response.Cookies.Delete(TenantContext.CookieName); Response.Cookies.Delete("org_name"); return RedirectToAction("Index", "Home"); }
    private void SetCookies(string k, string n)
    {
        var o = new CookieOptions { IsEssential = true, Expires = DateTimeOffset.UtcNow.AddDays(30) };
        Response.Cookies.Append(TenantContext.CookieName, k, o); Response.Cookies.Append("org_name", n, o);
    }
}
