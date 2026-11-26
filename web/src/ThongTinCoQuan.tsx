import {useEffect,useState,type FormEvent} from 'react';
import './ThongTinCoQuan.css';

type AgencyInfo={MaDinhDanh:string;TenCoQuan:string;DiaChi:string|null};

export default function ThongTinCoQuan({token,onNameChanged}:{token:string;onNameChanged:(name:string)=>void}){
  const [agency,setAgency]=useState<AgencyInfo|null>(null);
  const [name,setName]=useState('');
  const [address,setAddress]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [message,setMessage]=useState('');

  useEffect(()=>{
    let active=true;
    fetch('/api/v1/admin/agency',{headers:{Authorization:`Bearer ${token}`}})
      .then(async res=>{
        if(!res.ok)throw new Error(res.status===403?'Không có quyền quản lý thông tin cơ quan.':'Không tải được thông tin cơ quan.');
        return res.json() as Promise<Record<string,unknown>>;
      })
      .then(data=>{if(!active)return;
        const info:AgencyInfo={
          MaDinhDanh:String(data.MaDinhDanh??data.maDinhDanh??''),
          TenCoQuan:String(data.TenCoQuan??data.tenCoQuan??''),
          DiaChi:data.DiaChi==null&&data.diaChi==null?null:String(data.DiaChi??data.diaChi)
        };
        setAgency(info);setName(info.TenCoQuan);setAddress(info.DiaChi??'');
      }).catch(err=>{if(active)setError((err as Error).message);});
    return()=>{active=false;};
  },[token]);

  const save=async(event:FormEvent)=>{
    event.preventDefault();setError('');setMessage('');setBusy(true);
    try{
      const res=await fetch('/api/v1/admin/agency/details',{
        method:'PUT',headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json'},
        body:JSON.stringify({tenCoQuan:name,diaChi:address})
      });
      const data=await res.json().catch(()=>({})) as Record<string,unknown>;
      if(!res.ok)throw new Error(String(data.message??data.title??`Không thể lưu (HTTP ${res.status}).`));
      const updated=String(data.tenCoQuan??data.TenCoQuan??name);
      const updatedAddress=String(data.diaChi??data.DiaChi??address);
      setAgency(old=>old?{...old,TenCoQuan:updated,DiaChi:updatedAddress}:old);
      setName(updated);setAddress(updatedAddress);onNameChanged(updated);
      setMessage('Đã cập nhật tên và địa chỉ cơ quan.');
    }catch(err){setError((err as Error).message);}finally{setBusy(false);}
  };

  return <section className="panel agency-settings"><div className="panel-head"><div><h3>Thông tin cơ quan</h3>
    <p>Quản trị viên được sửa tên và địa chỉ của cơ quan mình; mã cơ quan không thay đổi.</p></div></div>
    {agency?<form className="agency-settings-form" onSubmit={event=>void save(event)}>
      <label>Mã cơ quan<input disabled value={agency.MaDinhDanh} readOnly/></label>
      <label>Tên cơ quan<input required minLength={3} maxLength={200} value={name}
        onChange={event=>setName(event.target.value)}/></label>
      <label>Địa chỉ<input maxLength={400} value={address} placeholder="Nhập địa chỉ cơ quan"
        onChange={event=>setAddress(event.target.value)}/></label>
      <button className="primary" disabled={busy||name.trim().length<3||
        (name.trim()===agency.TenCoQuan&&address.trim()===(agency.DiaChi??''))}>
        {busy?'Đang lưu...':'Lưu thông tin cơ quan'}</button>
    </form>:!error&&<p className="agency-settings-note">Đang tải thông tin cơ quan...</p>}
    {error&&<p className="form-error agency-settings-note" role="alert">{error}</p>}
    {message&&<p className="success-note agency-settings-note" role="status">{message}</p>}
  </section>;
}
