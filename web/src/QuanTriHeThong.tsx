import {useCallback,useEffect,useState,type FormEvent} from 'react';
import './QuanTriHeThong.css';

type CoQuan={MaCoQuan:number;MaDinhDanh:string;TenCoQuan:string;DiaChi:string|null;DangHoatDong:boolean;SoTaiKhoan:number;SoQuanTri:number};
type QuanTri={MaNguoiDung:number;TenDangNhap:string;HoTen:string;DangHoatDong:boolean};
type Phong={MaPhongBan:number;TenPhongBan:string};
type NhatKy={MaNhatKy:number;HanhDong:string;MoTa:string;NguoiThucHien:string;ThoiDiem:string};

async function api<T>(token:string,path:string,init?:RequestInit):Promise<T>{
  let res:Response;
  try{
    res=await fetch(`/api/v1/system${path}`,{...init,headers:{Authorization:`Bearer ${token}`,
      'Content-Type':'application/json',...init?.headers}});
  }catch{throw new Error('Không kết nối được API. Hãy kiểm tra backend.');}
  if(!res.ok){
    const data=await res.json().catch(()=>({}));
    const message=data.message??data.title??({401:'Cần đăng nhập lại.',403:'Bạn không có quyền quản trị toàn hệ thống.',404:'Không tìm thấy cơ quan.',409:'Dữ liệu đã tồn tại hoặc không phù hợp.'} as Record<number,string>)[res.status];
    throw new Error(message??`Yêu cầu thất bại (HTTP ${res.status}).`);
  }
  return res.json() as Promise<T>;
}

export default function QuanTriHeThong({token}:{token:string}){
  const [agencies,setAgencies]=useState<CoQuan[]>([]);
  const [admins,setAdmins]=useState<QuanTri[]>([]);
  const [departments,setDepartments]=useState<Phong[]>([]);
  const [audit,setAudit]=useState<NhatKy[]>([]);
  const [selected,setSelected]=useState<number|null>(null);
  const [code,setCode]=useState('');const [name,setName]=useState('');const [address,setAddress]=useState('');
  const [username,setUsername]=useState('');const [fullname,setFullname]=useState('');
  const [password,setPassword]=useState('');const [department,setDepartment]=useState('');
  const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [message,setMessage]=useState('');

  const reload=useCallback(async()=>{
    const [items,logs]=await Promise.all([api<CoQuan[]>(token,'/agencies'),api<NhatKy[]>(token,'/audit?page=1')]);
    setAgencies(items);setAudit(logs);
  },[token]);
  const loadSelected=useCallback(async(id:number)=>{
    const [users,rooms]=await Promise.all([api<QuanTri[]>(token,`/agencies/${id}/admins`),
      api<Phong[]>(token,`/agencies/${id}/departments`)]);
    setAdmins(users);setDepartments(rooms);
    setDepartment(String(rooms.find(p=>p.TenPhongBan==='Văn phòng')?.MaPhongBan??rooms[0]?.MaPhongBan??''));
  },[token]);
  useEffect(()=>{void reload().catch(e=>setError((e as Error).message));},[reload]);
  useEffect(()=>{if(selected!=null)void loadSelected(selected).catch(e=>setError((e as Error).message));},[selected,loadSelected]);

  const submit=async<T,>(path:string,body:unknown,method='POST'):Promise<T|undefined>=>{
    setBusy(true);setError('');setMessage('');
    try{
      const result=await api<T&{message?:string}>(token,path,{method,body:JSON.stringify(body)});
      setMessage(result.message??'Đã lưu thành công.');await reload();
      if(selected!=null)await loadSelected(selected);
      return result;
    }catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };

  const create=async(e:FormEvent)=>{
    e.preventDefault();
    const result=await submit<{maCoQuan:number}>('/agencies',{maDinhDanh:code,tenCoQuan:name,diaChi:address});
    if(result){setCode('');setName('');setAddress('');setSelected(result.maCoQuan);}
  };
  const createAdmin=async(e:FormEvent)=>{
    e.preventDefault();if(selected==null)return;
    const result=await submit(`/agencies/${selected}/admins`,{
      maPhongBan:Number(department),tenDangNhap:username,hoTen:fullname,matKhau:password});
    if(result){setUsername('');setFullname('');setPassword('');}
  };
  const toggleAgency=async(c:CoQuan)=>{
    const action=c.DangHoatDong?'ngừng hoạt động':'mở lại';
    const reason=window.prompt(`Lý do ${action} cơ quan ${c.TenCoQuan} (tối thiểu 5 ký tự):`);
    if(reason===null)return;
    if(!window.confirm(`Xác nhận ${action} cơ quan ${c.TenCoQuan}?`))return;
    await submit(`/agencies/${c.MaCoQuan}/status`,{dangHoatDong:!c.DangHoatDong,lyDo:reason},'PUT');
  };
  const updateAgency=async(c:CoQuan)=>{
    const newName=window.prompt('Tên cơ quan:',c.TenCoQuan);
    if(newName===null)return;
    const newAddress=window.prompt('Địa chỉ:',c.DiaChi??'');
    if(newAddress===null)return;
    await submit(`/agencies/${c.MaCoQuan}`,{tenCoQuan:newName,diaChi:newAddress},'PUT');
  };
  const toggleAdmin=async(u:QuanTri)=>{
    if(selected==null)return;
    const action=u.DangHoatDong?'khóa':'mở lại';
    const reason=window.prompt(`Lý do ${action} tài khoản ${u.TenDangNhap} (tối thiểu 5 ký tự):`);
    if(reason===null)return;
    if(!window.confirm(`Xác nhận ${action} tài khoản ${u.TenDangNhap}?`))return;
    await submit(`/agencies/${selected}/admins/${u.MaNguoiDung}/status`,
      {dangHoatDong:!u.DangHoatDong,lyDo:reason},'PUT');
  };
  const selectedAgency=agencies.find(a=>a.MaCoQuan===selected);

  return <div className="system-admin-page">
    {error&&<div className="system-admin-alert error" role="alert">{error}</div>}
    {message&&<div className="system-admin-alert success" role="status">{message}</div>}
    <section className="panel system-admin-section"><div className="panel-head"><div><h3>Thêm cơ quan</h3>
      <p>Khởi tạo Ban lãnh đạo, Văn phòng và cấu hình quy trình mặc định.</p></div>
      <button className="secondary small" onClick={()=>void reload().catch(e=>setError(e.message))}>Tải lại</button></div>
      <form className="system-admin-form" onSubmit={e=>void create(e)}>
        <label>Mã cơ quan<input required maxLength={40} pattern="[A-Za-z0-9_-]{2,40}" placeholder="VD: CQ_D"
          value={code} onChange={e=>setCode(e.target.value.toUpperCase())}/></label>
        <label>Tên cơ quan<input required minLength={3} maxLength={200} placeholder="Tên cơ quan"
          value={name} onChange={e=>setName(e.target.value)}/></label>
        <label>Địa chỉ<input maxLength={400} placeholder="Không bắt buộc" value={address} onChange={e=>setAddress(e.target.value)}/></label>
        <button className="primary" disabled={busy}>Tạo cơ quan</button>
      </form></section>
    <section className="panel system-admin-section"><div className="panel-head"><div><h3>Danh sách cơ quan</h3>
      <p>{agencies.length} cơ quan • Chọn một cơ quan để cấp quản trị viên</p></div></div>
      <div className="system-admin-table"><table><thead><tr><th>Cơ quan</th><th>Tài khoản</th><th>Quản trị</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
        <tbody>{agencies.map(c=><tr key={c.MaCoQuan} className={selected===c.MaCoQuan?'system-admin-selected':''}>
          <td><strong>{c.TenCoQuan}</strong><small>{c.MaDinhDanh} · {c.DiaChi||'Chưa nhập địa chỉ'}</small></td>
          <td>{c.SoTaiKhoan}</td><td>{c.SoQuanTri}</td><td>{c.DangHoatDong?'Đang hoạt động':'Ngừng hoạt động'}</td>
          <td><div className="system-admin-actions"><button className="secondary small" onClick={()=>setSelected(c.MaCoQuan)}>Quản trị viên</button>
            <button className="secondary small" disabled={busy} onClick={()=>void updateAgency(c)}>Sửa</button>
            <button className="secondary small" disabled={busy} onClick={()=>void toggleAgency(c)}>{c.DangHoatDong?'Ngừng':'Mở lại'}</button>
          </div></td></tr>)}</tbody></table></div></section>
    {selectedAgency&&<section className="panel system-admin-section"><div className="panel-head"><div>
      <h3>Quản trị viên — {selectedAgency.TenCoQuan}</h3><p>Cấp tài khoản quản trị riêng; không cấp quyền xem văn bản nghiệp vụ.</p>
    </div><button className="secondary small" onClick={()=>setSelected(null)}>Đóng</button></div>
      <form className="system-admin-form" onSubmit={e=>void createAdmin(e)}>
        <label>Tên đăng nhập<input required minLength={4} maxLength={100} value={username} onChange={e=>setUsername(e.target.value)}/></label>
        <label>Họ và tên<input required minLength={3} maxLength={150} value={fullname} onChange={e=>setFullname(e.target.value)}/></label>
        <label>Phòng ban<select value={department} onChange={e=>setDepartment(e.target.value)} required>
          {departments.map(p=><option key={p.MaPhongBan} value={p.MaPhongBan}>{p.TenPhongBan}</option>)}</select></label>
        <label>Mật khẩu ban đầu<input type="password" autoComplete="new-password" minLength={12} required
          value={password} onChange={e=>setPassword(e.target.value)}/></label>
        <button className="primary" disabled={busy||!selectedAgency.DangHoatDong||!department}>Cấp quản trị viên</button>
      </form><div className="system-admin-users">{admins.map(u=><div key={u.MaNguoiDung} className="system-admin-user">
        <div><strong>{u.HoTen}</strong><small>{u.TenDangNhap} · {u.DangHoatDong?'Đang hoạt động':'Đã khóa'}</small></div>
        <button className="secondary small" disabled={busy} onClick={()=>void toggleAdmin(u)}>{u.DangHoatDong?'Khóa':'Mở lại'}</button>
      </div>)}</div></section>}
    <section className="panel system-admin-section"><div className="panel-head"><h3>Nhật ký quản trị gần đây</h3></div>
      <div className="system-admin-users">{audit.map(log=><div key={log.MaNhatKy} className="system-admin-user">
        <div><strong>{log.HanhDong}</strong><small>{log.MoTa}</small></div>
        <time>{new Date(log.ThoiDiem).toLocaleString('vi-VN')}</time>
      </div>)}{audit.length===0&&<p className="system-admin-empty">Chưa có thao tác quản trị.</p>}</div>
    </section>
  </div>;
}
