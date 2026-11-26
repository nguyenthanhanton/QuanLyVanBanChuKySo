import {useEffect,useState} from 'react';

type Row={maVanBanDen:string;tieuDe:string;soDen:number|null;trangThai:string;
  maPhongChuTri:number|null;tenPhongChuTri:string|null;tenPhongPhoiHop:string|null;
  trangThaiXuLy:string|null;soBaoCao:number;soPhong:number;chiDao:string|null;ketQuaTongHop:string|null};
type Department={maPhongBan:number;tenPhongBan:string};
type Report={maPhongBan:number;tenPhongBan:string;nguoiBaoCao:string;noiDung:string;thoiDiemBaoCao:string};
type Inbox={rows:Row[];departments:Department[];leader:boolean;clerk:boolean};
const empty:Inbox={rows:[],departments:[],leader:false,clerk:false};

export default function DieuHanhVanBanDenPanel({token,onUpdated}:{token:string;onUpdated:()=>Promise<void>}){
  const [data,setData]=useState<Inbox>(empty);
  const [selected,setSelected]=useState('');
  const [lead,setLead]=useState('');
  const [partners,setPartners]=useState<number[]>([]);
  const [note,setNote]=useState('');
  const [deadline,setDeadline]=useState('');
  const [result,setResult]=useState('');
  const [reports,setReports]=useState<Report[]>([]);
  const [page,setPage]=useState(1);
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState('');
  const [error,setError]=useState('');
  const api=async(path:string,body?:object)=>{
    const response=await fetch('/api/v1/incoming-work'+path,{method:body?'POST':'GET',
      headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json'},
      body:body?JSON.stringify(body):undefined});
    const content=await response.json().catch(()=>({}));
    if(!response.ok)throw new Error(content.message??`Máy chủ trả về ${response.status}`);
    return content;
  };
  const load=async()=>setData(await api(''));
  useEffect(()=>{load().catch(e=>setError(e.message));},[token]);
  const choose=async(row:Row)=>{
    setSelected(row.maVanBanDen);setLead(row.maPhongChuTri?String(row.maPhongChuTri):'');
    setPartners([]);setResult('');setReports([]);setMessage('');setError('');
    if(row.maPhongChuTri)try{setReports(await api(`/${row.maVanBanDen}/reports`));}
      catch(e){setError((e as Error).message);}
  };
  const submit=async(path:string,body:object={})=>{
    setBusy(true);setError('');setMessage('');
    try{const response=await api(path,body);setMessage(response.message??'Đã cập nhật.');
      await load();await onUpdated();
      if(selected&&path.includes('/report'))setReports(await api(`/${selected}/reports`));
    }catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  const picked=data.rows.find(row=>row.maVanBanDen===selected);
  const status=(row:Row)=>row.trangThaiXuLy==='HOAN_THANH'?'Hoàn thành':
    row.trangThaiXuLy==='DANG_XU_LY'?'Đang xử lý':
    row.trangThai==='CHO_LANH_DAO'?'Chờ lãnh đạo giao':'Chờ văn thư trình';
  return <section className="panel incoming-work-panel">
    <div className="panel-head"><div><h3>Điều hành xử lý văn bản đến</h3>
      <p>Mỗi văn bản có một phòng chủ trì và có thể có nhiều phòng phối hợp.</p></div>
      <button className="secondary small" onClick={()=>load().catch(e=>setError(e.message))}>Tải lại</button></div>
    {message&&<p className="incoming-work-success">{message}</p>}
    {error&&<p className="form-error">{error}</p>}
    {data.rows.slice((page-1)*15,page*15).map(row=><div className="incoming-work-item" key={row.maVanBanDen}>
      <div><strong>{row.tieuDe}</strong><small>Số đến: {row.soDen??'Chưa cấp'} · {status(row)}</small>
        {row.tenPhongChuTri&&<small>Chủ trì: {row.tenPhongChuTri} · Phối hợp: {row.tenPhongPhoiHop||'Không'}</small>}
      </div><div className="incoming-work-actions">
        {data.clerk&&row.trangThai==='CHO_DANG_KY'&&<button className="primary small" disabled={busy}
          onClick={()=>submit(`/${row.maVanBanDen}/submit`)}>Trình lãnh đạo</button>}
        <button className="secondary small" onClick={()=>choose(row)}>{selected===row.maVanBanDen?'Đang xem':'Xử lý / Chi tiết'}</button>
      </div></div>)}
    {data.rows.length===0&&<p className="subtle" style={{padding:20}}>Chưa có văn bản đến thuộc phạm vi xử lý của bạn.</p>}
    {data.rows.length>15&&<div className="incoming-work-actions" style={{padding:16}}>
      <button className="secondary small" disabled={page===1} onClick={()=>setPage(p=>p-1)}>Trang trước</button>
      <span>Trang {page}/{Math.ceil(data.rows.length/15)}</span>
      <button className="secondary small" disabled={page*15>=data.rows.length} onClick={()=>setPage(p=>p+1)}>Trang sau</button>
    </div>}
    {picked&&<div className="incoming-work-detail">
      <h3>{picked.tieuDe}</h3>
      {picked.chiDao&&<p><strong>Chỉ đạo:</strong> {picked.chiDao}</p>}
      {data.leader&&picked.trangThai==='CHO_LANH_DAO'&&<div className="incoming-work-form">
        <label>Phòng chủ trì (bắt buộc)<select value={lead} onChange={e=>{
          setLead(e.target.value);setPartners(p=>p.filter(x=>x!==Number(e.target.value)));}}>
          <option value="">Chọn phòng chịu trách nhiệm chính</option>
          {data.departments.map(d=><option key={d.maPhongBan} value={d.maPhongBan}>{d.tenPhongBan}</option>)}
        </select></label>
        <div><strong>Phòng phối hợp (có thể chọn nhiều)</strong><div className="incoming-work-checks">
          {data.departments.filter(d=>String(d.maPhongBan)!==lead).map(d=><label key={d.maPhongBan}>
            <input type="checkbox" checked={partners.includes(d.maPhongBan)} onChange={e=>setPartners(p=>
              e.target.checked?[...p,d.maPhongBan]:p.filter(x=>x!==d.maPhongBan))}/>{d.tenPhongBan}</label>)}
        </div></div>
        <label>Hạn xử lý<input type="datetime-local" value={deadline} onChange={e=>setDeadline(e.target.value)}/></label>
        <label>Ý kiến chỉ đạo<textarea value={note} onChange={e=>setNote(e.target.value)} maxLength={2000}/></label>
        <button className="primary small" disabled={busy||!lead} onClick={()=>submit(`/${selected}/assign`,{
          maPhongChuTri:Number(lead),maPhongPhoiHop:partners,chiDao:note,
          hanXuLy:deadline?new Date(deadline).toISOString():null})}>Giao xử lý</button>
      </div>}
      {picked.trangThaiXuLy&&<><h4>Báo cáo các phòng ({picked.soBaoCao}/{picked.soPhong})</h4>
        {reports.map(r=><div className="incoming-work-report" key={r.maPhongBan}><strong>{r.tenPhongBan} — {r.nguoiBaoCao}</strong>
          <p>{r.noiDung}</p></div>)}
        {reports.length===0&&<p className="subtle">Chưa có báo cáo từ các phòng.</p>}
        {picked.trangThaiXuLy==='DANG_XU_LY'&&<div className="incoming-work-form">
          <label>Kết quả thực hiện (chỉ người thuộc phòng được giao mới gửi được)
            <textarea value={result} onChange={e=>setResult(e.target.value)} maxLength={4000}/></label>
          <button className="secondary small" disabled={busy||!result.trim()} onClick={()=>submit(`/${selected}/report`,{noiDung:result})}>
            Gửi báo cáo của phòng</button>
          {data.leader&&<button className="primary small" disabled={busy||!result.trim()||picked.soBaoCao===0}
            onClick={()=>submit(`/${selected}/complete`,{ketQuaTongHop:result})}>Kết thúc xử lý (kết quả tổng hợp)</button>}
        </div>}
        {picked.ketQuaTongHop&&<p><strong>Kết quả tổng hợp:</strong> {picked.ketQuaTongHop}</p>}
      </>}
    </div>}
  </section>;
}
