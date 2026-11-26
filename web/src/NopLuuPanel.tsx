import {useEffect,useState} from 'react';
import {CheckCircle2,FileCheck2,FileText,Inbox,RefreshCw,Send} from 'lucide-react';
import type {VanBan} from './types';
import {formatDate} from './types';

type Submission={MaLuot:number;MaHoSo:string;MaHoSoNoiBo:string;TenHoSo:string;
  NamHoSo:number;TrangThai:string;ThoiDiemNop:string;ThoiDiemXuLy:string|null;
  LyDoTraLai:string|null;MaBienNhan:string|null;NguoiNop:string;NguoiXuLy:string|null;TenPhongBan:string|null};
type SubDocument={MaVanBan:string;MaNoiBo:string;TieuDe:string;TrangThai:string};
type Receipt={MaLuot:number;MaBienNhan:string;MaHoSoNoiBo:string;TenHoSo:string;NamHoSo:number;
  SoNamBaoQuan:number|null;SoVanBan:number;NguoiGiao:string;NguoiNhan:string;TenCoQuan:string;
  ThoiDiemNop:string;ThoiDiemXuLy:string};
type Props={token:string;selected:string|null;closed:boolean|undefined;documents:VanBan[];
  onOpen:(d:VanBan)=>void;onChanged:()=>Promise<void>};

const title:Record<string,string>={CHO_TIEP_NHAN:'Chờ tiếp nhận',TRA_LAI:'Đã trả lại',DA_TIEP_NHAN:'Đã tiếp nhận'};
// JSON API of ASP.NET Core uses camelCase; normalize to stable display property names.
function normalize(value:unknown):unknown{
  if(Array.isArray(value))return value.map(normalize);
  if(value!==null&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([key,v])=>
    [key[0].toUpperCase()+key.slice(1),normalize(v)]));
  return value;
}
async function api(path:string,token:string,init?:RequestInit){
  const res=await fetch('/api/v1/dossiers'+path,{...init,headers:{'Content-Type':'application/json',Authorization:`Bearer ${token}`,...init?.headers}});
  if(!res.ok){const data=await res.json().catch(()=>({}));throw new Error(data.message??`HTTP ${res.status}`);}
  return normalize(await res.json());
}
export default function NopLuuPanel({token,selected,closed,documents,onOpen,onChanged}:Props){
  const [rows,setRows]=useState<Submission[]>([]);
  const [clerk,setClerk]=useState(false);
  const [expanded,setExpanded]=useState<number|null>(null);
  const [files,setFiles]=useState<SubDocument[]>([]);
  const [receipt,setReceipt]=useState<Receipt|null>(null);
  const [incomingPdf,setIncomingPdf]=useState('');
  const [reason,setReason]=useState('');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [page,setPage]=useState(1);
  const [filter,setFilter]=useState('ALL');
  useEffect(()=>()=>{if(incomingPdf)URL.revokeObjectURL(incomingPdf);},[incomingPdf]);
  const openIncoming=async(dossierId:string,docId:string)=>{
    try{
      const response=await fetch(`/api/v1/dossiers/${dossierId}/incoming-documents/${docId}/pdf`,{
        headers:{Authorization:`Bearer ${token}`}});
      if(!response.ok){const data=await response.json().catch(()=>({}));
        throw new Error(data.message??`Không thể xem PDF (${response.status}).`);}
      setIncomingPdf(URL.createObjectURL(await response.blob()));
    }catch(e){setError((e as Error).message);}
  };
  const reload=async()=>{
    const [submissions,permission]=await Promise.all([
      api('/submissions',token),api('/submissions/permissions',token)]);
    setRows(submissions as Submission[]);setClerk((permission as {LaVanThu:boolean}).LaVanThu);
  };
  useEffect(()=>{if(token)void reload().catch(e=>setError((e as Error).message));},[token]);
  const mutate=async(path:string,body?:unknown)=>{
    setError('');setNotice('');setBusy(true);
    try{await api(path,token,{method:'POST',...(body===undefined?{}:{body:JSON.stringify(body)})});
      await reload();await onChanged();setReason('');setNotice('Đã ghi nhận thao tác nộp lưu trên máy chủ.');}
    catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  const openFiles=async(luot:number)=>{
    if(expanded===luot){setExpanded(null);return;}
    setError('');setExpanded(luot);setFiles([]);
    try{setFiles(await api(`/submissions/${luot}/documents`,token) as SubDocument[]);}
    catch(e){setError((e as Error).message);}
  };
  const openReceipt=async(luot:number)=>{
    try{setReceipt(await api(`/submissions/${luot}/receipt`,token) as Receipt);}catch(e){setError((e as Error).message);}
  };
  const current=selected?rows.filter(r=>r.MaHoSo===selected):[];
  const latest=current[0];
  const eligible=!!closed&&(!latest||latest.TrangThai==='TRA_LAI');
  const filtered=rows.filter(r=>filter==='ALL'||r.TrangThai===filter);
  const pages=Math.max(1,Math.ceil(filtered.length/15));
  const safePage=Math.min(page,pages);
  return <div className="archive-submissions">
    {selected&&<div className="panel archive-submit-card">
      <div className="panel-head"><h3>Nộp lưu hồ sơ · 0.7.1</h3></div>
      <div className="archive-detail-body"><p className="fine-note">Hồ sơ phải được đóng và có văn bản đã phát hành trước khi nộp lưu. Văn thư cơ quan sẽ kiểm tra và xác nhận.</p>
        <div className="archive-submit-row"><span>{latest?`Trạng thái: ${title[latest.TrangThai]??latest.TrangThai}`:'Chưa nộp lưu'}</span>
          <button className="primary" disabled={!eligible||busy} onClick={()=>void mutate(`/${selected}/submit`)}>
            <Send size={16}/> {latest?.TrangThai==='TRA_LAI'?'Nộp lại hồ sơ':'Nộp lưu hồ sơ'}</button></div>
        {latest?.LyDoTraLai&&<p className="form-error">Lý do trả lại: {latest.LyDoTraLai}</p>}
        {latest?.TrangThai==='DA_TIEP_NHAN'&&<button className="secondary small" onClick={()=>void openReceipt(latest.MaLuot)}>
          <FileCheck2 size={15}/> Xem biên nhận điện tử</button>}
        {!closed&&<p className="fine-note">Cần đóng hồ sơ trước khi nộp.</p>}
      </div></div>}
    <div className="panel archive-submission-list"><div className="panel-head"><div><h3>{clerk?'Hộp tiếp nhận hồ sơ của Văn thư':'Các lượt nộp lưu liên quan'}</h3>
        <p>{rows.length} lượt nộp lưu; chỉ xử lý hồ sơ trong cơ quan.</p></div>
        <button className="secondary small" onClick={()=>void reload().catch(e=>setError((e as Error).message))}><RefreshCw size={15}/> Tải lại</button></div>
      <div className="archive-submission-filter"><label>Lọc trạng thái <select value={filter} onChange={e=>{setFilter(e.target.value);setPage(1);}}>
        <option value="ALL">Tất cả</option>{Object.entries(title).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label></div>
      {filtered.length===0&&<div className="archive-empty"><Inbox size={24}/><p>Chưa có lượt nộp lưu phù hợp.</p></div>}
      {filtered.slice((safePage-1)*15,safePage*15).map(row=><div className="archive-submission-item" key={row.MaLuot}>
        <div className="archive-submission-heading"><div><strong>{row.MaHoSoNoiBo} — {row.TenHoSo}</strong>
          <small>{row.TenPhongBan??'Toàn cơ quan'} · {row.NguoiNop} · Nộp {formatDate(row.ThoiDiemNop)}</small></div>
          <span className={`archive-transfer-tag ${row.TrangThai}`}>{title[row.TrangThai]??row.TrangThai}</span></div>
        {row.NguoiXuLy&&<p className="fine-note">{row.NguoiXuLy} · {formatDate(row.ThoiDiemXuLy??undefined)}</p>}
        {row.LyDoTraLai&&<p className="fine-note">Lý do trả lại: {row.LyDoTraLai}</p>}
        <div className="archive-actions"><button className="secondary small" onClick={()=>void openFiles(row.MaLuot)}>
          <FileText size={14}/> {expanded===row.MaLuot?'Ẩn văn bản':'Kiểm tra văn bản'}</button>
          {row.TrangThai==='DA_TIEP_NHAN'&&<button className="secondary small" onClick={()=>void openReceipt(row.MaLuot)}>
            <FileCheck2 size={14}/> Biên nhận</button>}</div>
        {expanded===row.MaLuot&&<div className="archive-submission-docs">{files.length===0&&<p className="fine-note">Không có văn bản được phép xem.</p>}
          {files.map(f=><div key={f.MaVanBan}><span>{f.MaNoiBo} · {f.TieuDe}</span>
            <button className="secondary small" onClick={()=>{if(f.MaNoiBo.startsWith('Số đến')){
                void openIncoming(row.MaHoSo,f.MaVanBan);return;}
              const doc=documents.find(d=>d.id===f.MaVanBan);
              if(doc)onOpen(doc);else setError('Văn bản không nằm trong danh sách được phép xem.');}}>Xem</button></div>)}</div>}
        {clerk&&row.TrangThai==='CHO_TIEP_NHAN'&&<div className="archive-decision">
          <textarea placeholder="Ghi lý do nếu trả lại hồ sơ (bắt buộc)" value={reason} maxLength={1000} rows={2} onChange={e=>setReason(e.target.value)}/>
          <div className="archive-actions"><button className="primary" disabled={busy} onClick={()=>void mutate(`/submissions/${row.MaLuot}/decision`,{TiepNhan:true})}>
            <CheckCircle2 size={16}/> Tiếp nhận</button>
          <button className="secondary" disabled={busy||!reason.trim()} onClick={()=>void mutate(`/submissions/${row.MaLuot}/decision`,{TiepNhan:false,LyDo:reason})}>Trả lại</button></div></div>}
      </div>)}
      {filtered.length>15&&<div className="archive-pagination"><button className="secondary small" disabled={safePage<=1} onClick={()=>setPage(safePage-1)}>Trước</button>
        Trang {safePage}/{pages} <button className="secondary small" disabled={safePage>=pages} onClick={()=>setPage(safePage+1)}>Sau</button></div>}
    </div>
    {error&&<div className="form-error">{error}</div>}{notice&&<div className="archive-notice">{notice}</div>}
    {receipt&&<div className="preview-backdrop" onClick={()=>setReceipt(null)}><div className="archive-receipt preview-window" onClick={e=>e.stopPropagation()}>
      <div className="archive-receipt-body"><h2>BIÊN NHẬN HỒ SƠ ĐIỆN TỬ</h2><p>{receipt.TenCoQuan}</p>
        <p><b>Mã biên nhận:</b> {receipt.MaBienNhan}</p><p><b>Hồ sơ:</b> {receipt.MaHoSoNoiBo} — {receipt.TenHoSo}</p>
        <p><b>Năm:</b> {receipt.NamHoSo} · <b>Văn bản:</b> {receipt.SoVanBan} · <b>Bảo quản:</b> {receipt.SoNamBaoQuan?`${receipt.SoNamBaoQuan} năm`:'Vĩnh viễn'}</p>
        <p><b>Người giao:</b> {receipt.NguoiGiao} · {formatDate(receipt.ThoiDiemNop)}</p>
        <p><b>Người nhận:</b> {receipt.NguoiNhan} · {formatDate(receipt.ThoiDiemXuLy)}</p>
        <p><b>Trạng thái:</b> Đã tiếp nhận lưu trữ</p>
        <p className="fine-note">Biên nhận điện tử ghi nhận trên hệ thống, không phải chữ ký số CA.</p></div>
      <div className="archive-actions archive-receipt-actions"><button className="secondary" onClick={()=>setReceipt(null)}>Đóng</button>
        <button className="primary" onClick={()=>window.print()}>In / Lưu PDF</button></div></div></div>}
    {incomingPdf&&<div className="incoming-work-preview-overlay" role="dialog" aria-modal="true" aria-label="Văn bản đến nộp lưu">
      <div className="incoming-work-preview"><div className="incoming-work-preview-head"><strong>PDF văn bản đến đã ký</strong>
        <button className="secondary small" onClick={()=>setIncomingPdf('')}>Đóng</button></div>
        <iframe src={incomingPdf} title="PDF văn bản đến nộp lưu"/>
      </div>
    </div>}
  </div>;
}
