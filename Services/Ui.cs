using MiniHR.Models;
namespace MiniHR.Services;
public static class Ui
{
    public static (string t, string css) Emp(EmpStatus s) => s switch {
        EmpStatus.Active => ("Đang làm", "success"), EmpStatus.OnLeave => ("Nghỉ phép", "warning"),
        EmpStatus.Resigned => ("Đã nghỉ việc", "secondary"), _ => (s.ToString(), "secondary") };
    public static string Leave(LeaveType t) => t switch {
        LeaveType.Annual => "Phép năm", LeaveType.Sick => "Nghỉ ốm", LeaveType.Unpaid => "Không lương",
        LeaveType.Maternity => "Thai sản", _ => t.ToString() };
    public static (string t, string css) LeaveSt(LeaveStatus s) => s switch {
        LeaveStatus.Pending => ("Chờ duyệt", "warning"), LeaveStatus.Approved => ("Đã duyệt", "success"),
        LeaveStatus.Rejected => ("Từ chối", "danger"), _ => (s.ToString(), "secondary") };
}
