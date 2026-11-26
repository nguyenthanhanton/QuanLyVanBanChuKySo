import {useEffect,useState,type FormEvent} from 'react';
import {LockKeyhole,UserRound,X} from 'lucide-react';

type Profile={maNguoiDung:number;tenDangNhap:string;hoTen:string;chucVu:string|null;
  tenCoQuan:string|null;tenPhongBan:string|null;vaiTro:string|null};

export default function ThongTinTaiKhoan({token,onClose,onNameChanged,onPasswordChanged}:{
  token:string;onClose:()=>void;onNameChanged:(name:string)=>void;onPasswordChanged:()=>void}){
  const [profile,setProfile]=useState<Profile|null>(null);
  const [name,setName]=useState('');
  const [currentPassword,setCurrentPassword]=useState('');
  const [newPassword,setNewPassword]=useState('');
  const [confirmPassword,setConfirmPassword]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const api=async(path:string,method:'GET'|'PUT'='GET',data?:object)=>{
    let response:Response;
    try{response=await fetch(`/api/v1/auth/profile${path}`,{
      method,headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json'},
      body:data?JSON.stringify(data):undefined});}
    catch{throw new Error('Không thể kết nối máy chủ.');}
    const result=await response.json().catch(()=>({}));
    if(!response.ok)throw new Error(result.message??(response.status===401?'Phiên đăng nhập đã hết hạn.':
      response.status===403?'Bạn không có quyền thực hiện.':`Máy chủ trả lỗi ${response.status}.`));
    return result;
  };
  useEffect(()=>{
    let active=true;
    api('').then(row=>{
      if(!active)return;
      const normalized=Object.fromEntries(Object.entries(row as Record<string,unknown>).map(([key,value])=>[
        key.charAt(0).toLowerCase()+key.slice(1),value])) as Profile;
      setProfile(normalized);setName(normalized.hoTen);
    }).catch(e=>{if(active)setError((e as Error).message);});
    return()=>{active=false;};
  },[token]);
  const saveName=async(event:FormEvent)=>{
    event.preventDefault();setError('');setNotice('');
    if(name.trim().length<3||name.trim().length>150){setError('Họ tên phải có 3–150 ký tự.');return;}
    setBusy(true);
    try{const response=await api('/name','PUT',{hoTen:name.trim()});
      setProfile(old=>old?{...old,hoTen:response.hoTen}:old);
      onNameChanged(response.hoTen);setNotice(response.message);
    }catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  const savePassword=async(event:FormEvent)=>{
    event.preventDefault();setError('');setNotice('');
    if(newPassword.length<12||newPassword.length>128){setError('Mật khẩu mới phải có 12–128 ký tự.');return;}
    if(newPassword!==confirmPassword){setError('Xác nhận mật khẩu mới không khớp.');return;}
    setBusy(true);
    try{await api('/password','PUT',{matKhauHienTai:currentPassword,matKhauMoi:newPassword});
      setCurrentPassword('');setNewPassword('');setConfirmPassword('');onPasswordChanged();
    }catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  return <div className="incoming-work-preview-overlay" role="dialog" aria-modal="true" aria-label="Thông tin tài khoản">
    <div className="account-profile-modal">
      <div className="account-profile-heading"><div><small>TÀI KHOẢN CÁ NHÂN</small><h2>Thông tin tài khoản</h2></div>
        <button className="secondary small" onClick={onClose}><X size={16}/> Đóng</button></div>
      {error&&<p className="form-error" role="alert">{error}</p>}
      {notice&&<p className="incoming-work-success" role="status">{notice}</p>}
      {!profile?<p className="subtle">Đang tải thông tin tài khoản...</p>:<>
        <section className="account-profile-section"><h3><UserRound size={17}/> Thông tin cơ quan và chức vụ</h3>
          <p className="subtle">Thông tin do quản trị viên phân công, chỉ có thể xem.</p>
          <div className="account-profile-grid">
            {([['Tên đăng nhập',profile.tenDangNhap],['Cơ quan',profile.tenCoQuan],
              ['Phòng ban',profile.tenPhongBan],['Chức vụ',profile.chucVu],['Vai trò',profile.vaiTro]] as const)
              .map(([label,value])=><div className="account-profile-field" key={label}>
                <span>{label}</span><div className="account-profile-readonly">{value||'Chưa phân công'}</div>
              </div>)}
          </div>
        </section>
        <section className="account-profile-section"><h3><UserRound size={17}/> Đổi họ tên</h3>
          <form onSubmit={saveName} className="account-profile-form">
            <label>Họ tên<input value={name} onChange={e=>setName(e.target.value)} maxLength={150} required/></label>
            <button className="primary small" type="submit" disabled={busy||name.trim()===profile.hoTen}>Lưu họ tên</button>
          </form>
        </section>
        <section className="account-profile-section"><h3><LockKeyhole size={17}/> Đổi mật khẩu</h3>
          <form onSubmit={savePassword} className="account-profile-form">
            <label>Mật khẩu hiện tại<input type="password" autoComplete="current-password" value={currentPassword}
              onChange={e=>setCurrentPassword(e.target.value)} required/></label>
            <label>Mật khẩu mới (tối thiểu 12 ký tự)<input type="password" autoComplete="new-password" value={newPassword}
              onChange={e=>setNewPassword(e.target.value)} minLength={12} maxLength={128} required/></label>
            <label>Xác nhận mật khẩu mới<input type="password" autoComplete="new-password" value={confirmPassword}
              onChange={e=>setConfirmPassword(e.target.value)} required/></label>
            <button className="primary small" type="submit" disabled={busy||!currentPassword||!newPassword||!confirmPassword}>Đổi mật khẩu</button>
            <small>Đổi mật khẩu thành công sẽ đăng xuất để bạn đăng nhập lại.</small>
          </form>
        </section>
      </>}
    </div>
  </div>;
}
