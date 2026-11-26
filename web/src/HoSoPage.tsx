import {useEffect,useMemo,useState,type FormEvent} from 'react';
import {Archive,ChevronLeft,ChevronRight,FileText,FolderPlus,RefreshCw,Search} from 'lucide-react';
import type {VanBan} from './types';
import {formatDate} from './types';
import NopLuuPanel from './NopLuuPanel';

type Department={MaPhongBan:number;TenPhongBan:string};
type Dossier={MaHoSo:string;MaHoSoNoiBo:string;TenHoSo:string;LoaiHoSo:string;NamHoSo:number;
  MaPhongBan:number|null;TenPhongBan:string|null;SoNamBaoQuan:number|null;GhiChu:string|null;
  DaDong:boolean;NguoiTao:string;MaNguoiTao:number;SoVanBan:number;ThoiDiemTao:string};
type Attached={MaVanBan:string;MaNoiBo:string;TieuDe:string;SoPhatHanh:number|null;
  KyHieu:string|null;ThoiDiemThem:string;NguoiThem:string};
type History={MaLichSu:number;HanhDong:string;MoTa:string;ThoiDiem:string;NguoiThucHien:string};
type Details={HoSo:Dossier;VanBan:Attached[];LichSu:History[]};
const BLANK={MaHoSoNoiBo:'',TenHoSo:'',LoaiHoSo:'Văn bản hành chính',NamHoSo:new Date().getFullYear(),
  MaPhongBan:'',SoNamBaoQuan:'',GhiChu:''};
type FormState=typeof BLANK;

async function api(path:string,token:string,init?:RequestInit){
  const response=await fetch(`/api/v1/dossiers${path}`,{...init,headers:{'Content-Type':'application/json',Authorization:`Bearer ${token}`,...init?.headers}});
  if(!response.ok){const data=await response.json().catch(()=>({}));throw new Error(data.message??`Máy chủ trả lỗi ${response.status}`);}
  // ASP.NET Core serializes anonymous objects and DTOs with camelCase by default.
  // Normalize recursively so the view consumes the SQL column names consistently.
  const normalize=(value:unknown):unknown=>{
    if(Array.isArray(value))return value.map(normalize);
    if(value!==null&&typeof value==='object')return Object.fromEntries(
      Object.entries(value).map(([key,item])=>[key[0].toUpperCase()+key.slice(1),normalize(item)]));
    return value;
  };
  return normalize(await response.json());
}
function Pages({count,page,change}:{count:number;page:number;change:(n:number)=>void}){
  const total=Math.ceil(count/15);if(total<=1)return null;
  return <div className="archive-pagination"><span>{(page-1)*15+1}–{Math.min(count,page*15)} / {count}</span>
    <button className="secondary small" disabled={page===1} onClick={()=>change(page-1)}><ChevronLeft size={14}/> Trước</button>
    <span>Trang {page}/{total}</span><button className="secondary small" disabled={page===total} onClick={()=>change(page+1)}>Sau <ChevronRight size={14}/></button></div>;
}

export default function HoSoPage({token,documents,onOpen}:{token:string;documents:VanBan[];onOpen:(doc:VanBan)=>void}){
  const [dossiers,setDossiers]=useState<Dossier[]>([]);
  const [departments,setDepartments]=useState<Department[]>([]);
  const [selected,setSelected]=useState<string|null>(null);
  const [details,setDetails]=useState<Details|null>(null);
  const [draft,setDraft]=useState<FormState>(BLANK);
  const [editing,setEditing]=useState(false);
  const [creating,setCreating]=useState(false);
  const [term,setTerm]=useState('');
  const [year,setYear]=useState('');
  const [department,setDepartment]=useState('');
  const [status,setStatus]=useState('ALL');
  const [docSearch,setDocSearch]=useState('');
  const [chosenDoc,setChosenDoc]=useState('');
  const [page,setPage]=useState(1);
  const [historyPage,setHistoryPage]=useState(1);
  const [docPage,setDocPage]=useState(1);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');

  const load=async()=>{
    const [rows,depts]=await Promise.all([api('/',token),api('/departments',token)]);
    setDossiers(rows as Dossier[]);setDepartments(depts as Department[]);
  };
  const loadDetails=async(id:string)=>{const value=await api(`/${id}`,token) as Details;setDetails(value);};
  useEffect(()=>{if(!token)return;load().catch(e=>setError((e as Error).message));},[token]);
  const perform=async(action:()=>Promise<void>,message:string)=>{
    setBusy(true);setError('');setNotice('');
    try{await action();await load();if(selected)await loadDetails(selected);setNotice(message);}
    catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  const filtered=useMemo(()=>dossiers.filter(d=>{
    const q=term.trim().toLocaleLowerCase('vi-VN');
    return(!q||`${d.MaHoSoNoiBo} ${d.TenHoSo} ${d.LoaiHoSo}`.toLocaleLowerCase('vi-VN').includes(q))
      &&(!year||String(d.NamHoSo)===year)&&(!department||String(d.MaPhongBan)===department)
      &&(status==='ALL'||String(d.DaDong)===status);
  }),[dossiers,term,year,department,status]);
  const available=documents.filter(d=>d.type==='DI'&&['DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH'].includes(d.status)
    &&d.secrecy==='THUONG'&&!details?.VanBan.some(v=>v.MaVanBan===d.id)
    &&`${d.code} ${d.title}`.toLocaleLowerCase('vi-VN').includes(docSearch.toLocaleLowerCase('vi-VN')));
  const resetForm=(d?:Dossier)=>setDraft(d?{MaHoSoNoiBo:d.MaHoSoNoiBo,TenHoSo:d.TenHoSo,
    LoaiHoSo:d.LoaiHoSo,NamHoSo:d.NamHoSo,MaPhongBan:String(d.MaPhongBan??''),
    SoNamBaoQuan:String(d.SoNamBaoQuan??''),GhiChu:d.GhiChu??''}:BLANK);
  const body=()=>({MaHoSoNoiBo:draft.MaHoSoNoiBo,TenHoSo:draft.TenHoSo,LoaiHoSo:draft.LoaiHoSo,
    NamHoSo:Number(draft.NamHoSo),MaPhongBan:draft.MaPhongBan?Number(draft.MaPhongBan):null,
    SoNamBaoQuan:draft.SoNamBaoQuan?Number(draft.SoNamBaoQuan):null,GhiChu:draft.GhiChu});
  const save=async(event:FormEvent)=>{
    event.preventDefault();const newDossier=!editing;
    await perform(async()=>{
      const result=await api(editing?`/${selected}`:'/',token,{method:editing?'PUT':'POST',body:JSON.stringify(body())}) as {MaHoSo:string};
      if(newDossier){setSelected(result.MaHoSo);await loadDetails(result.MaHoSo);}
      else if(selected)await loadDetails(selected);
      setCreating(false);setEditing(false);
    },newDossier?'Đã tạo hồ sơ lưu trữ.':'Đã cập nhật hồ sơ.');
  };
  const selectDossier=async(id:string)=>{
    setSelected(id);setDetails(null);setError('');setNotice('');setEditing(false);setCreating(false);
    setDocPage(1);setHistoryPage(1);setChosenDoc('');setDocSearch('');
    try{await loadDetails(id);}catch(e){setError((e as Error).message);}
  };

  return <div className="archive-page">
    <div className="page-intro compact"><div><div className="overline">LƯU TRỮ ĐIỆN TỬ · 0.7.1</div>
      <h1>Hồ sơ lưu trữ</h1><p>Lập hồ sơ theo năm, phòng ban; lưu liên kết văn bản đã phát hành và lịch sử thao tác.</p></div>
      <button className="primary" onClick={()=>{setSelected(null);setDetails(null);resetForm();setEditing(false);setCreating(true);}}>
        <FolderPlus size={17}/> Tạo hồ sơ</button></div>
    {error&&<div className="form-error">{error}</div>}{notice&&<div className="archive-notice">{notice}</div>}
    <div className="panel archive-panel"><div className="panel-head"><h3>Danh sách hồ sơ ({filtered.length})</h3>
      <button className="secondary small" onClick={()=>load().catch(e=>setError((e as Error).message))}><RefreshCw size={15}/> Tải lại</button></div>
      <div className="archive-filters"><label><Search size={15}/><input placeholder="Tìm mã, tên, loại hồ sơ" value={term} onChange={e=>{setTerm(e.target.value);setPage(1);}}/></label>
        <input type="number" min="1900" max="2200" placeholder="Năm" value={year} onChange={e=>{setYear(e.target.value);setPage(1);}}/>
        <select value={department} onChange={e=>{setDepartment(e.target.value);setPage(1);}}><option value="">Tất cả phòng ban</option>
          {departments.map(d=><option value={d.MaPhongBan} key={d.MaPhongBan}>{d.TenPhongBan}</option>)}</select>
        <select value={status} onChange={e=>{setStatus(e.target.value);setPage(1);}}>
          <option value="ALL">Tất cả trạng thái</option><option value="false">Đang mở</option><option value="true">Đã đóng</option></select></div>
      {filtered.length===0?<div className="archive-empty"><Archive size={26}/><p>Chưa có hồ sơ phù hợp.</p></div>:
        <div className="archive-list">{filtered.slice((page-1)*15,page*15).map(d=><button key={d.MaHoSo}
          className={`archive-item ${selected===d.MaHoSo?'selected':''}`} onClick={()=>selectDossier(d.MaHoSo)}>
          <span className="archive-item-main"><strong>{d.TenHoSo}</strong><small>{d.MaHoSoNoiBo} · {d.LoaiHoSo} · {d.NamHoSo}</small>
            <small>{d.TenPhongBan??'Toàn cơ quan'} · {d.SoNamBaoQuan?`${d.SoNamBaoQuan} năm`:'Bảo quản vĩnh viễn'}</small></span>
          <span className="archive-item-meta"><span className={d.DaDong?'archive-closed':'archive-open'}>{d.DaDong?'Đã đóng':'Đang mở'}</span>
            <small>{d.SoVanBan} văn bản</small></span><ChevronRight size={17}/></button>)}</div>}
      <Pages count={filtered.length} page={page} change={setPage}/></div>

    {(creating||editing)&&<form className="panel archive-form" onSubmit={save}>
      <div className="panel-head"><h3>{editing?'Sửa thông tin hồ sơ':'Tạo hồ sơ mới'}</h3></div>
      <div className="archive-form-grid">
        <label>Mã hồ sơ<input required maxLength={60} disabled={editing} value={draft.MaHoSoNoiBo} onChange={e=>setDraft({...draft,MaHoSoNoiBo:e.target.value})} placeholder="HS-2026-001"/></label>
        <label>Tên hồ sơ<input required maxLength={250} value={draft.TenHoSo} onChange={e=>setDraft({...draft,TenHoSo:e.target.value})}/></label>
        <label>Loại hồ sơ<input required maxLength={100} value={draft.LoaiHoSo} onChange={e=>setDraft({...draft,LoaiHoSo:e.target.value})}/></label>
        <label>Năm hồ sơ<input type="number" required min={1900} max={2200} value={draft.NamHoSo} onChange={e=>setDraft({...draft,NamHoSo:Number(e.target.value)})}/></label>
        <label>Phòng ban<select value={draft.MaPhongBan} onChange={e=>setDraft({...draft,MaPhongBan:e.target.value})}><option value="">Dùng chung cơ quan</option>
          {departments.map(d=><option key={d.MaPhongBan} value={d.MaPhongBan}>{d.TenPhongBan}</option>)}</select></label>
        <label>Bảo quản (năm)<input type="number" min={1} max={200} placeholder="Để trống = vĩnh viễn" value={draft.SoNamBaoQuan}
          onChange={e=>setDraft({...draft,SoNamBaoQuan:e.target.value})}/></label>
        <label className="archive-wide">Ghi chú<textarea maxLength={1000} rows={2} value={draft.GhiChu} onChange={e=>setDraft({...draft,GhiChu:e.target.value})}/></label>
      </div><div className="archive-actions"><button className="primary" disabled={busy}>{editing?'Lưu thay đổi':'Tạo hồ sơ'}</button>
        <button type="button" className="secondary" onClick={()=>{setEditing(false);setCreating(false);}}>Hủy</button></div></form>}

    {selected&&details&&!creating&&<div className="panel archive-detail">
      <div className="panel-head"><div><h3>{details.HoSo.TenHoSo}</h3><p>{details.HoSo.MaHoSoNoiBo} · {details.HoSo.NguoiTao} · Tạo {formatDate(details.HoSo.ThoiDiemTao)}</p></div>
        <div className="archive-actions"><button className="secondary small" disabled={details.HoSo.DaDong||busy}
          onClick={()=>{resetForm(details.HoSo);setEditing(true);}}>Sửa hồ sơ</button>
          <button className="secondary small" disabled={busy} onClick={()=>perform(async()=>{
            await api(`/${selected}/close`,token,{method:'POST',body:JSON.stringify({DaDong:!details.HoSo.DaDong})});
          },details.HoSo.DaDong?'Đã mở lại hồ sơ.':'Đã đóng hồ sơ.')}>{details.HoSo.DaDong?'Mở lại':'Đóng hồ sơ'}</button></div></div>
      <div className="archive-detail-body">
        <div className="archive-detail-meta"><span>Phòng ban: <b>{details.HoSo.TenPhongBan??'Toàn cơ quan'}</b></span>
          <span>Bảo quản: <b>{details.HoSo.SoNamBaoQuan?`${details.HoSo.SoNamBaoQuan} năm`:'Vĩnh viễn'}</b></span>
          <span>Trạng thái: <b>{details.HoSo.DaDong?'Đã đóng':'Đang mở'}</b></span></div>
        {details.HoSo.GhiChu&&<p className="fine-note">{details.HoSo.GhiChu}</p>}
        <h3>Văn bản trong hồ sơ ({details.VanBan.length})</h3>
        {!details.HoSo.DaDong&&<div className="archive-add"><input placeholder="Tìm văn bản đã phát hành" value={docSearch}
            onChange={e=>{setDocSearch(e.target.value);setChosenDoc('');}}/>
          <select value={chosenDoc} onChange={e=>setChosenDoc(e.target.value)}><option value="">Chọn văn bản để đưa vào hồ sơ</option>
            {available.slice(0,100).map(d=><option key={d.id} value={d.id}>{d.code} — {d.title}</option>)}</select>
          <button className="primary" disabled={!chosenDoc||busy} onClick={()=>perform(async()=>{
            await api(`/${selected}/documents`,token,{method:'POST',body:JSON.stringify({MaVanBan:chosenDoc})});setChosenDoc('');
          },'Đã thêm văn bản vào hồ sơ.')}>Thêm văn bản</button></div>}
        <div className="archive-docs">{details.VanBan.slice((docPage-1)*15,docPage*15).map(d=><div className="archive-document" key={d.MaVanBan}>
          <FileText size={19}/><div><strong>{d.TieuDe}</strong><small>{d.MaNoiBo} · Thêm bởi {d.NguoiThem} lúc {formatDate(d.ThoiDiemThem)}</small></div>
          <button className="secondary small" onClick={()=>{const doc=documents.find(v=>v.id===d.MaVanBan);if(doc)onOpen(doc);else setError('Văn bản không nằm trong danh sách được phép xem.');}}>Xem</button>
          {!details.HoSo.DaDong&&<button className="secondary small" disabled={busy} onClick={()=>{
            if(!window.confirm('Gỡ văn bản khỏi hồ sơ? Văn bản gốc sẽ không bị xóa.'))return;
            void perform(async()=>{await api(`/${selected}/documents/${d.MaVanBan}`,token,{method:'DELETE'});},'Đã gỡ liên kết văn bản.');
          }}>Gỡ</button>}</div>)}</div>
        {details.VanBan.length===0&&<p className="archive-empty">Chưa có văn bản. Chỉ thêm văn bản thường đã phát hành.</p>}
        <Pages count={details.VanBan.length} page={docPage} change={setDocPage}/>
        <h3>Lịch sử hồ sơ ({details.LichSu.length})</h3><div className="archive-history">{details.LichSu.slice((historyPage-1)*15,historyPage*15).map(e=><div key={e.MaLichSu}>
          <strong>{e.HanhDong.replaceAll('_',' ')}</strong><span>{e.MoTa}</span>
          <small>{e.NguoiThucHien} · {formatDate(e.ThoiDiem)}</small></div>)}</div>
        <Pages count={details.LichSu.length} page={historyPage} change={setHistoryPage}/>
      </div></div>}
    <NopLuuPanel token={token} selected={selected} closed={details?.HoSo.DaDong} documents={documents} onOpen={onOpen}
      onChanged={async()=>{await load();if(selected)await loadDetails(selected);}}/>
    <p className="fine-note">Hồ sơ chỉ lưu liên kết; không sao chép, sửa hay xóa PDF đã ký. Quyền truy cập văn bản tiếp tục được kiểm tra ở máy chủ.</p>
  </div>;
}
