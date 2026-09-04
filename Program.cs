using Microsoft.EntityFrameworkCore;
using MiniHR.Data;
using MiniHR.Models;
using MiniHR.Services;
using Serilog;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
FleetObs.ConfigureLogger("minihr");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=minihr.db";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (DbUtil.IsPostgres(conn)) o.UseNpgsql(DbUtil.ToNpgsql(conn));
    else o.UseSqlite(conn);
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IHrService, HrService>();
builder.Services.AddFleetObs();
builder.Services.AddControllersWithViews();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await Seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

app.UseFleetObs();
FleetObs.ReportLicense(Environment.GetEnvironmentVariable("SSO_AUTHORITY") ?? "https://minisso.onrender.com", "minihr");

app.Use(async (ctx, next) =>
{
    var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(key)) ctx.Request.Cookies.TryGetValue(TenantContext.CookieName, out key);
    if (!string.IsNullOrWhiteSpace(key))
    {
        using var lookup = app.Services.CreateScope();
        var ldb = lookup.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await ldb.Orgs.FirstOrDefaultAsync(o => o.ApiKey == key);
        if (org != null) ctx.RequestServices.GetRequiredService<ITenantContext>().OrgId = org.Id;
    }
    await next();
});

app.UseStaticFiles();
app.MapGet("/healthz", () => "ok");
app.MapGet("/api/headcount", async (IHrService svc) => Results.Ok(new { headcount = (await svc.DashboardAsync()).Headcount }));

app.MapPost("/api/orgs/register", async (RegisterOrgDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Cần Name." });
    var org = new Org { Name = dto.Name.Trim(), ApiKey = "hr_" + Guid.NewGuid().ToString("N") };
    db.Orgs.Add(org); await db.SaveChangesAsync();
    return Results.Ok(new { orgId = org.Id, apiKey = org.ApiKey });
});

// Import phòng ban thật (dedupe theo Code)
app.MapPost("/api/import/departments", async (List<ImportDeptDto> rows, AppDbContext db, ITenantContext tc) =>
{
    if (rows == null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu." });
    int added = 0, skipped = 0;
    var orgId = tc.OrgId;
    var existCodes = db.Departments.Where(d => d.OrgId == orgId).Select(d => d.Code).ToHashSet();
    foreach (var row in rows)
    {
        if (string.IsNullOrWhiteSpace(row.Code)) { skipped++; continue; }
        if (existCodes.Contains(row.Code.Trim())) { skipped++; continue; }
        db.Departments.Add(new Department { OrgId = orgId, Code = row.Code.Trim(), Name = row.Name?.Trim() ?? row.Code.Trim() });
        existCodes.Add(row.Code.Trim()); added++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { added, skipped, total = added + skipped });
});

// Import nhân viên thật từ Mst_SalesMan (dedupe theo Code)
app.MapPost("/api/import/employees", async (List<ImportEmpDto> rows, AppDbContext db, ITenantContext tc) =>
{
    if (rows == null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu." });
    int added = 0, skipped = 0;
    var orgId = tc.OrgId;
    var existCodes = db.Employees.Where(e => e.OrgId == orgId).Select(e => e.Code).ToHashSet();
    foreach (var row in rows)
    {
        if (string.IsNullOrWhiteSpace(row.Code)) { skipped++; continue; }
        if (existCodes.Contains(row.Code.Trim())) { skipped++; continue; }
        int? deptId = null;
        if (!string.IsNullOrWhiteSpace(row.DeptCode))
        {
            var dept = db.Departments.FirstOrDefault(d => d.OrgId == orgId && d.Code == row.DeptCode.Trim());
            deptId = dept?.Id;
        }
        db.Employees.Add(new Employee
        {
            OrgId = orgId, Code = row.Code.Trim(), FullName = row.FullName?.Trim() ?? row.Code.Trim(),
            Position = row.Position, DepartmentId = deptId, Phone = row.Phone, Email = row.Email,
            JoinDate = row.JoinDate ?? DateTime.Today, BaseSalary = row.BaseSalary,
            Status = EmpStatus.Active
        });
        existCodes.Add(row.Code.Trim()); added++;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { added, skipped, total = added + skipped });
});

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

record RegisterOrgDto(string Name);
record ImportDeptDto(string? Code, string? Name);
record ImportEmpDto(string? Code, string? FullName, string? Position, string? DeptCode, string? Phone, string? Email, DateTime? JoinDate, decimal BaseSalary);
