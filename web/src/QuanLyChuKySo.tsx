import { useEffect,useState } from 'react';

type Credential={MaChungThu?:string;maChungThu?:string;LoaiChungThu?:string;loaiChungThu?:string;TenChungThu?:string;tenChungThu?:string;TenMauChuKy?:string;tenMauChuKy?:string;DangHoatDong?:boolean;dangHoatDong?:boolean;CoAnhChuKy?:boolean;coAnhChuKy?:boolean;NgayHetHan?:string;ngayHetHan?:string};
const value=(c:Credential,k:keyof Credential)=>c[k]??c[(k[0].toLowerCase()+k.slice(1)) as keyof Credential];
const api=async(path:string,token:string,options?:RequestInit)=>{
  const response=await fetch('/api/v1'+path,{...options,headers:{Authorization:`Bearer ${token}`,...options?.headers}});
  const result=await response.json().catch(()=>({}));
  if(!response.ok)throw new Error(result.message??result.title??`API ${response.status}`);
  return result;
};

export default function QuanLyChuKySo({token,capabilities}:{token:string;capabilities:string[]}){
  const [items,setItems]=useState<Credential[]>([]);
  const [name,setName]=useState('');const [label,setLabel]=useState('');
  const [type,setType]=useState('CA_NHAN');const [image,setImage]=useState<File>();
  const [test,setTest]=useState(true);const [pfx,setPfx]=useState<File>();
  const [pfxPassword,setPfxPassword]=useState('');const [password,setPassword]=useState('');
  const [busy,setBusy]=useState(false);const [message,setMessage]=useState('');
  const mayAgency=capabilities.includes('signatures.manage-agency');
  const mayPersonal=capabilities.includes('signatures.manage-own');
  const load=async()=>setItems(await api('/signing-credentials',token));
  useEffect(()=>{void load().catch(e=>setMessage(e.message));},[token]);
  const active=items.filter(c=>value(c,'DangHoatDong')===true);
  const personal=active.filter(c=>value(c,'LoaiChungThu')==='CA_NHAN');
  const submit=async()=>{
    setBusy(true);setMessage('');
    try{
      if(!image||image.type!=='image/png'||image.size>5*1024*1024)throw Error('Chọn ảnh PNG tối đa 5 MB.');
      if(!password)throw Error('Nhập mật khẩu tài khoản để xác nhận.');
      const form=new FormData();form.set('loaiChungThu',type);form.set('tenChungThu',name);
      form.set('tenMau',label||name);form.set('anhChuKy',image);form.set('matKhau',password);
      form.set('thuNghiem',String(test));form.set('matKhauPfx',pfxPassword);
      if(pfx)form.set('tepPfx',pfx);
      await api('/signing-credentials',token,{method:'POST',body:form});
      await load();setPassword('');setImage(undefined);setPfx(undefined);
      setMessage('Đã đăng ký chữ ký. Có thể chọn khi ký văn bản.');
    }catch(e){setMessage((e as Error).message);}finally{setBusy(false);}
  };
  const changeImage=async(c:Credential,file?:File)=>{
    if(!file)return;
    setBusy(true);setMessage('');
    try{
      const form=new FormData();form.set('anhChuKy',file);form.set('tenMau',String(value(c,'TenMauChuKy')||value(c,'TenChungThu')));
      await api(`/signing-credentials/${value(c,'MaChungThu')}/image`,token,{method:'POST',body:form});
      await load();setMessage('Đã cập nhật mẫu ảnh của chữ ký đã đăng ký.');
    }catch(e){setMessage((e as Error).message);}finally{setBusy(false);}
  };
  const deactivate=async(c:Credential)=>{
    if(!window.confirm('Vô hiệu hóa chữ ký này? Văn bản đã ký vẫn được giữ nguyên.'))return;
    setBusy(true);setMessage('');
    try{await api(`/signing-credentials/${value(c,'MaChungThu')}/deactivate`,token,{method:'POST'});await load();}
    catch(e){setMessage((e as Error).message);}finally{setBusy(false);}
  };
  return <><div className="page-intro compact"><div><div className="overline">VERSION 2.1 · QUẢN LÝ CHỮ KÝ</div>
    <h1>Quản lý chữ ký số</h1><p>Đăng ký chữ ký trước khi soạn và duyệt văn bản; ảnh PNG gắn với chứng thư, không tải lại ở mỗi lần ký.</p></div></div>
    <div className="panel" style={{padding:24,marginBottom:24}}><h3>Chữ ký đã đăng ký ({personal.length}/3 chữ ký cá nhân đang hoạt động)</h3>
      {items.map(c=><div className="version-row" key={String(value(c,'MaChungThu'))} style={{display:'flex',gap:15,alignItems:'center',flexWrap:'wrap'}}>
        <div style={{flex:'1 1 260px'}}><strong>{String(value(c,'TenMauChuKy')||value(c,'TenChungThu'))}</strong>
          <span>{value(c,'LoaiChungThu')==='CO_QUAN'?'Chữ ký cơ quan':'Chữ ký cá nhân'} · {value(c,'DangHoatDong')?'Hoạt động':'Đã vô hiệu hóa'} · {value(c,'CoAnhChuKy')?'Đã có mẫu PNG':'Cần bổ sung mẫu PNG'}</span></div>
        {value(c,'DangHoatDong')&&((value(c,'LoaiChungThu')==='CO_QUAN'&&mayAgency)||(value(c,'LoaiChungThu')==='CA_NHAN'&&mayPersonal))&&<>
          <label className="secondary small" style={{padding:8,cursor:'pointer'}}>Đổi ảnh PNG<input style={{display:'none'}} type="file" accept="image/png" onChange={e=>void changeImage(c,e.target.files?.[0])}/></label>
          <button className="secondary small" disabled={busy} onClick={()=>void deactivate(c)}>Vô hiệu hóa</button></>}
      </div>)}
      {!items.length&&<p className="fine-note">Chưa có chữ ký nào. Đăng ký bên dưới để sử dụng.</p>}
    </div>
    {(mayPersonal||mayAgency)&&<div className="panel" style={{padding:24}}><h3>Đăng ký chữ ký mới</h3>
      <div className="form-grid"><label>Loại chữ ký<select value={type} onChange={e=>setType(e.target.value)}>
        {mayPersonal&&<option value="CA_NHAN">Cá nhân (tối đa 3 đang hoạt động)</option>}
        {mayAgency&&<option value="CO_QUAN">Cơ quan (lãnh đạo cấp trưởng đăng ký)</option>}
      </select></label>
      <label>Tên chứng thư<input value={name} onChange={e=>setName(e.target.value)} placeholder="Tên chứng thư"/></label>
      <label>Tên mẫu chữ ký<input value={label} onChange={e=>setLabel(e.target.value)} placeholder="Ví dụ: Chữ ký chính, ký nháy"/></label>
      <label>Ảnh ký / con dấu PNG, tối đa 5 MB<input type="file" accept="image/png,.png" onChange={e=>setImage(e.target.files?.[0])}/></label></div>
      <label className="form-check"><input type="checkbox" checked={test} onChange={e=>setTest(e.target.checked)}/>Tạo chứng thư tự ký thử nghiệm (không phải chứng thư CA được công nhận)</label>
      {!test&&<div className="form-grid"><label>Tệp chứng thư PFX/P12<input type="file" accept=".pfx,.p12" onChange={e=>setPfx(e.target.files?.[0])}/></label>
        <label>Mật khẩu PFX<input type="password" value={pfxPassword} onChange={e=>setPfxPassword(e.target.value)}/></label></div>}
      <label>Mật khẩu tài khoản xác nhận<input type="password" value={password} onChange={e=>setPassword(e.target.value)}/></label>
      <button className="primary small" disabled={busy||name.trim().length<3||!image||(type==='CA_NHAN'&&personal.length>=3)} onClick={()=>void submit()}>
        {busy?'Đang lưu...':'Đăng ký chữ ký số'}</button>
      <p className="fine-note">Chứng thư cũ vẫn giữ để xác minh PDF đã ký. Vô hiệu hóa không xóa khóa hoặc lịch sử ký.</p>
    </div>}
    {message&&<div className="form-error" role="status" style={{marginTop:12}}>{message}</div>}
  </>;
}
