const base = '/api/v1'
async function req(path, opts = {}) {
  const res = await fetch(base + path, {
    headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin',
    ...opts, body: opts.body ? JSON.stringify(opts.body) : undefined
  })
  const text = await res.text(); const data = text ? JSON.parse(text) : null
  if (!res.ok) throw new Error(data?.error || `Lỗi ${res.status}`)
  return { data, cache: res.headers.get('X-Cache') }
}
export const api = {
  dashboard: () => req('/dashboard'),
  departments: () => req('/departments'),
  createDept: (b) => req('/departments', { method: 'POST', body: b }),
  employees: (q, deptId) => req(`/employees?${q ? `q=${encodeURIComponent(q)}&` : ''}${deptId ? `deptId=${deptId}` : ''}`),
  employee: (id) => req(`/employees/${id}`),
  createEmployee: (b) => req('/employees', { method: 'POST', body: b }),
  leaves: (status) => req(`/leaves${status != null ? `?status=${status}` : ''}`),
  fileLeave: (b) => req('/leaves', { method: 'POST', body: b }),
  approve: (id, approve) => req(`/leaves/${id}/approve`, { method: 'POST', body: { approve } }),
  payrolls: () => req('/payrolls'),
  payroll: (id) => req(`/payrolls/${id}`),
  runPayroll: (period) => req('/payrolls', { method: 'POST', body: { period } })
}
export const fmtMoney = (n) => (n ?? 0).toLocaleString('vi-VN') + 'đ'
export const fmtDate = (s) => s ? new Date(s).toLocaleDateString('vi-VN') : '—'
export const LEAVETYPES = ['Phép năm', 'Nghỉ ốm', 'Không lương', 'Thai sản']
export const LSTATUS = ['Chờ duyệt', 'Đã duyệt', 'Từ chối']
