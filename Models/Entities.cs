namespace MiniHR.Models;

public class Org
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
public interface IOrgOwned { Guid OrgId { get; set; } }

public enum EmpStatus { Active = 0, OnLeave = 1, Resigned = 2 }
public enum LeaveType { Annual = 0, Sick = 1, Unpaid = 2, Maternity = 3 }
public enum LeaveStatus { Pending = 0, Approved = 1, Rejected = 2 }

public class Department : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Employee : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
    public int? DepartmentId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.Today;
    public decimal BaseSalary { get; set; }
    public int AnnualLeaveDays { get; set; } = 12;   // phép năm
    public EmpStatus Status { get; set; } = EmpStatus.Active;

    public Department? Department { get; set; }
    public List<LeaveRequest> Leaves { get; set; } = [];
}

public class LeaveRequest : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int EmployeeId { get; set; }
    public LeaveType Type { get; set; }
    public DateTime FromDate { get; set; } = DateTime.Today;
    public DateTime ToDate { get; set; } = DateTime.Today;
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Employee Employee { get; set; } = null!;
    public int Days => (ToDate.Date - FromDate.Date).Days + 1;
}

/// <summary>Kỳ bảng lương.</summary>
public class PayrollRun : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public string Period { get; set; } = "";      // yyyy-MM
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool Closed { get; set; }
    public List<PayrollLine> Lines { get; set; } = [];
    public decimal Total => Lines.Sum(l => l.Net);
}

public class PayrollLine : IOrgOwned
{
    public int Id { get; set; }
    public Guid OrgId { get; set; }
    public int RunId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public decimal BaseSalary { get; set; }
    public decimal Allowance { get; set; }
    public decimal Deduction { get; set; }        // BHXH/thuế/nghỉ không lương
    public decimal Net => BaseSalary + Allowance - Deduction;
    public PayrollRun Run { get; set; } = null!;
}
