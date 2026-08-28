import React, { useEffect, useState } from 'react'
import { Routes, Route, NavLink, Outlet } from 'react-router-dom'
import { api, fmtMoney, fmtDate, LEAVETYPES, LSTATUS } from './api'

function Badge({ text, css }) { return <span className={`badge ${css || 'secondary'}`}>{text}</span> }
function Flash({ msg }) { return msg ? <div className={`flash ${msg.ok ? 'ok' : 'err'}`}>{msg.text}</div> : null }
function Modal({ title, onClose, wide, children }) {
  return (
    <div className="modal-bg" onClick={onClose}>
      <div className="modal" style={wide ? { maxWidth: 720 } : undefined} onClick={e => e.stopPropagation()}>
        <div className="row" style={{ marginBottom: 12 }}><h2 style={{ flex: 1, margin: 0 }}>{title}</h2>
          <button className="btn gray sm" style={{ flex: 'none' }} onClick={onClose}>Đóng</button></div>{children}
      </div>
    </div>
  )
}
function Field({ label, children }) { return <div style={{ flex: 1 }}><label>{label}</label>{children}</div> }

function Layout() {
  return (
    <>
      <nav className="nav"><span className="brand">👥 MiniHR</span>
        <NavLink to="/" end>Tổng quan</NavLink><NavLink to="/employees">Nhân viên</NavLink>
        <NavLink to="/leaves">Nghỉ phép</NavLink><NavLink to="/payrolls">Bảng lương</NavLink><NavLink to="/departments">Phòng ban</NavLink></nav>
      <div className="wrap"><Outlet /></div>
    </>
  )
}

function Dashboard() {
  const [d, setD] = useState(null); const [cache, setCache] = useState('')
  useEffect(() => { api.dashboard().then(r => { setD(r.data); setCache(r.cache) }) }, [])
  if (!d) return <p className="muted">Đang tải…</p>
  const max = Math.max(1, ...d.byDept.map(x => x.count))
  return (
    <>
      <h1>Tổng quan nhân sự {cache && <span className="pill">cache: {cache}</span>}</h1>
      <div className="grid kpis" style={{ marginBottom: 18 }}>
        <div className="kpi"><div className="v">{d.headcount}</div><div className="l">Quân số</div></div>
        <div className="kpi"><div className="v" style={{ color: 'var(--warning)' }}>{d.onLeave}</div><div className="l">Đang nghỉ phép</div></div>
        <div className="kpi"><div className="v">{d.pendingLeaves}</div><div className="l">Đơn chờ duyệt</div></div>
        <div className="kpi"><div className="v" style={{ fontSize: 18, color: 'var(--success)' }}>{fmtMoney(d.payrollMonth)}</div><div className="l">Quỹ lương tháng</div></div>
      </div>
      <div className="card funnel"><h2>Quân số theo phòng ban</h2>
        {d.byDept.map((x, i) => (<div className="bar" key={i}><div className="lbl">{x.dept}</div>
          <div className="track"><div className="fill" style={{ width: `${(x.count / max) * 100}%` }} /></div><div className="n">{x.count}</div></div>))}
      </div>
    </>
  )
}

function Employees() {
  const [rows, setRows] = useState([]); const [q, setQ] = useState(''); const [dept, setDept] = useState(''); const [depts, setDepts] = useState([])
  const [open, setOpen] = useState(null); const [show, setShow] = useState(false)
  const load = () => api.employees(q, dept || null).then(r => setRows(r.data))
  useEffect(() => { load() }, [dept])
  useEffect(() => { api.departments().then(r => setDepts(r.data)) }, [])
  return (
    <>
      <div className="toolbar"><h1 style={{ margin: 0, flex: 'none' }}>Nhân viên</h1><div className="sp" />
        <select style={{ maxWidth: 160 }} value={dept} onChange={e => setDept(e.target.value)}><option value="">— Phòng ban —</option>{depts.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select>
        <input style={{ maxWidth: 180 }} placeholder="Tìm tên/mã…" value={q} onChange={e => setQ(e.target.value)} onKeyDown={e => e.key === 'Enter' && load()} />
        <button className="btn ghost sm" style={{ flex: 'none' }} onClick={load}>Tìm</button>
        <button className="btn sm" style={{ flex: 'none' }} onClick={() => setShow(true)}>+ Thêm NV</button></div>
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>Mã</th><th>Họ tên</th><th>Chức vụ</th><th>Phòng ban</th><th className="right">Lương CB</th><th>Trạng thái</th></tr></thead>
          <tbody>{rows.map(e => (
            <tr key={e.id} style={{ cursor: 'pointer' }} onClick={() => setOpen(e.id)}>
              <td>{e.code}</td><td>{e.fullName}</td><td>{e.position || '—'}</td><td>{e.dept || '—'}</td>
              <td className="right">{fmtMoney(e.baseSalary)}</td><td><Badge text={e.statusText} css={e.statusCss} /></td></tr>))}
            {rows.length === 0 && <tr><td colSpan={6} className="muted" style={{ padding: 20 }}>Chưa có nhân viên.</td></tr>}</tbody></table>
      </div>
      {open && <EmpDetail id={open} onClose={() => setOpen(null)} />}
      {show && <EmpForm depts={depts} onClose={() => setShow(false)} onSaved={() => { setShow(false); load() }} />}
    </>
  )
}

function EmpDetail({ id, onClose }) {
  const [e, setE] = useState(null)
  useEffect(() => { api.employee(id).then(r => setE(r.data)) }, [id])
  if (!e) return <Modal title="…" onClose={onClose}><p className="muted">Đang tải…</p></Modal>
  return (
    <Modal title={`${e.fullName} (${e.code})`} onClose={onClose}>
      <dl className="dl"><dt>Chức vụ</dt><dd>{e.position || '—'}</dd><dt>Phòng ban</dt><dd>{e.dept || '—'}</dd>
        <dt>Vào làm</dt><dd>{fmtDate(e.joinDate)}</dd><dt>Lương CB</dt><dd>{fmtMoney(e.baseSalary)}</dd>
        <dt>Phép năm</dt><dd>{e.annualLeaveDays} ngày</dd><dt>SĐT</dt><dd>{e.phone || '—'}</dd></dl>
      {e.leaves.length > 0 && <><div className="section-t">Lịch sử nghỉ phép</div>
        <table><tbody>{e.leaves.map(l => <tr key={l.id}><td>{l.type}</td><td>{fmtDate(l.fromDate)}–{fmtDate(l.toDate)}</td><td>{l.days} ngày</td><td className="right"><span className="pill">{l.status}</span></td></tr>)}</tbody></table></>}
    </Modal>
  )
}

function EmpForm({ depts, onClose, onSaved }) {
  const [f, setF] = useState({ fullName: '', code: '', position: '', departmentId: '', phone: '', baseSalary: 0, annualLeaveDays: 12 }); const [err, setErr] = useState('')
  const up = (k, v) => setF({ ...f, [k]: v })
  const save = async () => { try { if (!f.fullName) { setErr('Cần họ tên'); return } await api.createEmployee({ ...f, departmentId: f.departmentId ? Number(f.departmentId) : null, baseSalary: Number(f.baseSalary), annualLeaveDays: Number(f.annualLeaveDays) }); onSaved() } catch (e) { setErr(e.message) } }
  return (
    <Modal title="Thêm nhân viên" onClose={onClose}>
      {err && <Flash msg={{ ok: false, text: err }} />}
      <div className="row"><Field label="Họ tên *"><input value={f.fullName} onChange={e => up('fullName', e.target.value)} /></Field>
        <Field label="Mã"><input value={f.code} onChange={e => up('code', e.target.value)} /></Field></div>
      <div className="row"><Field label="Chức vụ"><input value={f.position} onChange={e => up('position', e.target.value)} /></Field>
        <Field label="Phòng ban"><select value={f.departmentId} onChange={e => up('departmentId', e.target.value)}><option value="">—</option>{depts.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select></Field></div>
      <div className="row"><Field label="Lương cơ bản"><input type="number" value={f.baseSalary} onChange={e => up('baseSalary', e.target.value)} /></Field>
        <Field label="Phép năm"><input type="number" value={f.annualLeaveDays} onChange={e => up('annualLeaveDays', e.target.value)} /></Field>
        <Field label="SĐT"><input value={f.phone} onChange={e => up('phone', e.target.value)} /></Field></div>
      <div style={{ marginTop: 16 }}><button className="btn" onClick={save}>Lưu</button></div>
    </Modal>
  )
}

function Leaves() {
  const [rows, setRows] = useState([]); const [status, setStatus] = useState(''); const [msg, setMsg] = useState(null); const [show, setShow] = useState(false)
  const load = () => api.leaves(status === '' ? null : Number(status)).then(r => setRows(r.data))
  useEffect(() => { load() }, [status])
  const approve = async (id, ok) => { try { const r = await api.approve(id, ok); setMsg({ ok: true, text: r.data.msg }); load() } catch (e) { setMsg({ ok: false, text: e.message }) } }
  return (
    <>
      <div className="toolbar"><h1 style={{ margin: 0, flex: 'none' }}>Nghỉ phép</h1><div className="sp" />
        <select style={{ maxWidth: 150 }} value={status} onChange={e => setStatus(e.target.value)}><option value="">— Trạng thái —</option>{LSTATUS.map((s, i) => <option key={i} value={i}>{s}</option>)}</select>
        <button className="btn sm" style={{ flex: 'none' }} onClick={() => setShow(true)}>+ Tạo đơn</button></div>
      <Flash msg={msg} />
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>Nhân viên</th><th>Loại</th><th>Thời gian</th><th className="right">Số ngày</th><th>Lý do</th><th>Trạng thái</th></tr></thead>
          <tbody>{rows.map(l => (
            <tr key={l.id}><td>{l.employee}</td><td>{l.type}</td><td>{fmtDate(l.fromDate)}–{fmtDate(l.toDate)}</td><td className="right">{l.days}</td><td>{l.reason || '—'}</td>
              <td>{l.status === 0 ? <div className="row" style={{ gap: 4 }}><button className="btn sm" style={{ flex: 'none' }} onClick={() => approve(l.id, true)}>Duyệt</button><button className="btn gray sm" style={{ flex: 'none' }} onClick={() => approve(l.id, false)}>Từ chối</button></div> : <Badge text={l.statusText} css={l.statusCss} />}</td></tr>))}
            {rows.length === 0 && <tr><td colSpan={6} className="muted" style={{ padding: 20 }}>Chưa có đơn.</td></tr>}</tbody></table>
      </div>
      {show && <LeaveForm onClose={() => setShow(false)} onSaved={() => { setShow(false); load() }} />}
    </>
  )
}

function LeaveForm({ onClose, onSaved }) {
  const [emps, setEmps] = useState([]); const [f, setF] = useState({ employeeId: '', type: 0, fromDate: '', toDate: '', reason: '' }); const [err, setErr] = useState('')
  useEffect(() => { api.employees().then(r => { setEmps(r.data); if (r.data[0]) setF(s => ({ ...s, employeeId: r.data[0].id })) }) }, [])
  const save = async () => { try { if (!f.employeeId) { setErr('Chọn NV'); return } await api.fileLeave({ ...f, employeeId: Number(f.employeeId), type: Number(f.type), fromDate: f.fromDate || null, toDate: f.toDate || null }); onSaved() } catch (e) { setErr(e.message) } }
  return (
    <Modal title="Tạo đơn nghỉ phép" onClose={onClose}>
      {err && <Flash msg={{ ok: false, text: err }} />}
      <div className="row"><Field label="Nhân viên"><select value={f.employeeId} onChange={e => setF({ ...f, employeeId: e.target.value })}>{emps.map(e => <option key={e.id} value={e.id}>{e.fullName}</option>)}</select></Field>
        <Field label="Loại"><select value={f.type} onChange={e => setF({ ...f, type: e.target.value })}>{LEAVETYPES.map((t, i) => <option key={i} value={i}>{t}</option>)}</select></Field></div>
      <div className="row"><Field label="Từ ngày"><input type="date" value={f.fromDate} onChange={e => setF({ ...f, fromDate: e.target.value })} /></Field>
        <Field label="Đến ngày"><input type="date" value={f.toDate} onChange={e => setF({ ...f, toDate: e.target.value })} /></Field></div>
      <Field label="Lý do"><input value={f.reason} onChange={e => setF({ ...f, reason: e.target.value })} /></Field>
      <div style={{ marginTop: 16 }}><button className="btn" onClick={save}>Gửi đơn</button></div>
    </Modal>
  )
}

function Payrolls() {
  const [rows, setRows] = useState([]); const [open, setOpen] = useState(null); const [period, setPeriod] = useState(new Date().toISOString().slice(0, 7)); const [msg, setMsg] = useState(null)
  const load = () => api.payrolls().then(r => setRows(r.data))
  useEffect(() => { load() }, [])
  const run = async () => { try { await api.runPayroll(period); setMsg({ ok: true, text: 'Đã tính bảng lương ' + period }); load() } catch (e) { setMsg({ ok: false, text: e.message }) } }
  return (
    <>
      <div className="toolbar"><h1 style={{ margin: 0, flex: 'none' }}>Bảng lương</h1><div className="sp" />
        <input type="month" value={period} onChange={e => setPeriod(e.target.value)} style={{ maxWidth: 160 }} />
        <button className="btn sm" style={{ flex: 'none' }} onClick={run}>Tính lương kỳ này</button></div>
      <Flash msg={msg} />
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>Kỳ</th><th className="right">Số NV</th><th className="right">Tổng chi</th><th>Trạng thái</th></tr></thead>
          <tbody>{rows.map(p => (<tr key={p.id} style={{ cursor: 'pointer' }} onClick={() => setOpen(p.id)}>
            <td>{p.period}</td><td className="right">{p.lines}</td><td className="right"><b>{fmtMoney(p.total)}</b></td><td>{p.closed ? <Badge text="Đã chốt" css="dark" /> : <Badge text="Mở" css="info" />}</td></tr>))}
            {rows.length === 0 && <tr><td colSpan={4} className="muted" style={{ padding: 20 }}>Chưa có bảng lương.</td></tr>}</tbody></table>
      </div>
      {open && <PayrollDetail id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

function PayrollDetail({ id, onClose }) {
  const [p, setP] = useState(null)
  useEffect(() => { api.payroll(id).then(r => setP(r.data)) }, [id])
  if (!p) return <Modal title="…" onClose={onClose}><p className="muted">Đang tải…</p></Modal>
  return (
    <Modal title={`Bảng lương ${p.period}`} onClose={onClose} wide>
      <table><thead><tr><th>Nhân viên</th><th className="right">Lương CB</th><th className="right">Phụ cấp</th><th className="right">Khấu trừ</th><th className="right">Thực nhận</th></tr></thead>
        <tbody>{p.lines.map((l, i) => <tr key={i}><td>{l.employeeName}</td><td className="right">{fmtMoney(l.baseSalary)}</td><td className="right">{fmtMoney(l.allowance)}</td><td className="right">{fmtMoney(l.deduction)}</td><td className="right"><b>{fmtMoney(l.net)}</b></td></tr>)}</tbody>
        <tfoot><tr><td colSpan={4} className="right" style={{ fontWeight: 700 }}>TỔNG</td><td className="right" style={{ fontWeight: 700, color: 'var(--brand)' }}>{fmtMoney(p.total)}</td></tr></tfoot>
      </table>
    </Modal>
  )
}

function Departments() {
  const [rows, setRows] = useState([]); const [f, setF] = useState({ name: '', code: '' }); const [err, setErr] = useState('')
  const load = () => api.departments().then(r => setRows(r.data))
  useEffect(() => { load() }, [])
  const add = async () => { try { if (!f.name) return; await api.createDept(f); setF({ name: '', code: '' }); load() } catch (e) { setErr(e.message) } }
  return (
    <>
      <h1>Phòng ban</h1>{err && <Flash msg={{ ok: false, text: err }} />}
      <div className="card"><div className="row">
        <Field label="Mã"><input value={f.code} onChange={e => setF({ ...f, code: e.target.value })} /></Field>
        <Field label="Tên"><input value={f.name} onChange={e => setF({ ...f, name: e.target.value })} /></Field>
        <div style={{ flex: 'none', alignSelf: 'flex-end' }}><button className="btn" onClick={add}>+ Thêm</button></div></div></div>
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>Mã</th><th>Tên</th><th className="right">Quân số</th></tr></thead>
          <tbody>{rows.map(d => <tr key={d.id}><td>{d.code}</td><td>{d.name}</td><td className="right">{d.headcount}</td></tr>)}</tbody></table>
      </div>
    </>
  )
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="employees" element={<Employees />} />
        <Route path="leaves" element={<Leaves />} />
        <Route path="payrolls" element={<Payrolls />} />
        <Route path="departments" element={<Departments />} />
      </Route>
    </Routes>
  )
}
