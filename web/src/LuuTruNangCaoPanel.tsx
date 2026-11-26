import {useEffect,useMemo,useState} from 'react';
import {Archive,CheckCircle2,Clock3,FileCheck2,FileSearch,LockKeyhole,RefreshCw,Search} from 'lucide-react';
import {formatDate} from './types';

type ArchiveItem={MaHoSo:string;MaHoSoNoiBo:string;TenHoSo:string;LoaiHoSo:string;NamHoSo:number;
  TenPhongBan:string|null;NgayTiepNhan:string;NguoiLap:string;NguoiNhan:string;
  SoNamBaoQuan:number|null;NgayHetHan:string|null;TinhTrang:string;SoVanBan:number};
type Stat={NamHoSo:number;PhongBan:string;Tong:number;VinhVien:number;HetHan:number;SapHetHan:number};
type Request={MaDeXuat?:number;MaYeuCau?:number;MaHoSo:string;MaHoSoNoiBo:string;TenHoSo:string;
  SoNamDeXuat:number|null;LyDo:string;TrangThai:string;NguoiDeXuat?:string;NguoiYeuCau?:string;
  HieuLucDen?:string;GhiChuDuyet?:string};
type Audit={MaNhatKy:number;MaHoSoNoiBo:string;NguoiThucHien:string;HanhDong:string;
  ThoiDiem:string;MaVanBan:string|null;MaVanBanDen:string|null};
type Doc={MaVanBan:string;MaNoiBo:string;TieuDe:string;Nguon:'DI'|'DEN'};
type Attachment={MaTep:string;TenTep:string;LoaiNoiDung:string;KichThuoc:number};
type Integrity={Tong:number;HopLe:number;CanKiemTra:number;ThieuPdfDaKy:number;
  KetQua:{MaBan:string;CoTep:boolean;MaBamDung:boolean;ChuKyToanVen:boolean|null}[];GhiChu:string};
const termLabel:Record<string,string>={VINH_VIEN:'Vĩnh viễn',CON_HAN:'Còn hạn',SAP_HET_HAN:'Sắp hết hạn (90 ngày)',HET_HAN:'Đã hết hạn'};
function normalize(value:unknown):unknown{if(Array.isArray(value))return value.map(normalize);
  if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k[0].toUpperCase()+k.slice(1),normalize(v)]));
  return value;}
async function api(path:string,token:string,init?:RequestInit){const res=await fetch('/api/v1/archive'+path,{...init,
  headers:{'Content-Type':'application/json',Authorization:`Bearer ${token}`,...init?.headers}});
  if(!res.ok){const data=await res.json().catch(()=>({}));throw new Error(data.message??`HTTP ${res.status}`);}return normalize(await res.json());}

export default function LuuTruNangCaoPanel({token}:{token:string}){
  const [tab,setTab]=useState<'search'|'retention'|'access'|'audit'>('search');
  const [rows,setRows]=useState<ArchiveItem[]>([]);const [stats,setStats]=useState<Stat[]>([]);
  const [retention,setRetention]=useState<Request[]>([]);const [access,setAccess]=useState<Request[]>([]);
  const [audit,setAudit]=useState<Audit[]>([]);const [clerk,setClerk]=useState(false);
  const [term,setTerm]=useState('');const [year,setYear]=useState('');const [dept,setDept]=useState('');
  const [type,setType]=useState('');const [author,setAuthor]=useState('');const [recipient,setRecipient]=useState('');
  const [expiry,setExpiry]=useState('');const [page,setPage]=useState(1);
  const [reason,setReason]=useState('');const [years,setYears]=useState('');const [hours,setHours]=useState('24');
  const [target,setTarget]=useState('');const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [notice,setNotice]=useState('');
  const [selectedAccess,setSelectedAccess]=useState<number|null>(null);const [docs,setDocs]=useState<Doc[]>([]);
  const [attachments,setAttachments]=useState<Record<string,Attachment[]>>({});
  const [integrity,setIntegrity]=useState<Integrity|null>(null);const [pdf,setPdf]=useState('');
  const refresh=async()=>{const [r,s,t,a,j,p]=await Promise.all([
    api('/search',token),api('/stats',token),api('/retention',token),api('/access',token),api('/audit',token),
    fetch('/api/v1/dossiers/submissions/permissions',{headers:{Authorization:`Bearer ${token}`}}).then(x=>x.json())]);
    setRows(r as ArchiveItem[]);setStats(s as Stat[]);setRetention(t as Request[]);setAccess(a as Request[]);
    setAudit(j as Audit[]);setClerk(!!p.laVanThu);};
  useEffect(()=>{if(token)void refresh().catch(e=>setError((e as Error).message));},[token]);
  useEffect(()=>{setPage(1);},[tab,term,year,dept,type,author,recipient,expiry]);
  useEffect(()=>()=>{if(pdf)URL.revokeObjectURL(pdf);},[pdf]);
  const filtered=useMemo(()=>rows.filter(r=>{
    const has=(value:string,q:string)=>value.toLocaleLowerCase('vi-VN').includes(q.trim().toLocaleLowerCase('vi-VN'));
    return(!term||has(`${r.MaHoSoNoiBo} ${r.TenHoSo}`,term))&&(!year||String(r.NamHoSo)===year)
      &&(!dept||has(r.TenPhongBan??'',dept))&&(!type||has(r.LoaiHoSo,type))
      &&(!author||has(r.NguoiLap,author))&&(!recipient||has(r.NguoiNhan,recipient))
      &&(!expiry||r.TinhTrang===expiry);
  }),[rows,term,year,dept,type,author,recipient,expiry]);
  const collection=tab==='search'?filtered:tab==='retention'?retention:tab==='access'?access:audit;
  const total=Math.max(1,Math.ceil(collection.length/15));const current=Math.min(page,total);
  const action=async(path:string,body?:unknown)=>{setError('');setNotice('');setBusy(true);
    try{await api(path,token,{method:'POST',...(body===undefined?{}:{body:JSON.stringify(body)})});await refresh();
      setNotice('Đã ghi nhận thao tác trên máy chủ.');setReason('');setTarget('');}
    catch(e){setError((e as Error).message);}finally{setBusy(false);}};
  const getDocs=async(id:number)=>{setError('');setSelectedAccess(id);setDocs([]);setAttachments({});
    try{setDocs(await api(`/access/${id}/documents`,token) as Doc[]);}catch(e){setError((e as Error).message);}};
  const getAttachments=async(req:number,doc:Doc)=>{setError('');try{
    const items=await api(`/access/${req}/attachments/${doc.MaVanBan}?nguon=${doc.Nguon}`,token) as Attachment[];
    setAttachments(previous=>({...previous,[doc.MaVanBan]:items}));
  }catch(e){setError((e as Error).message);}};
  const downloadAttachment=async(req:number,doc:Doc,file:Attachment)=>{setError('');try{
    const res=await fetch(`/api/v1/archive/access/${req}/attachments/${doc.MaVanBan}/${file.MaTep}?nguon=${doc.Nguon}`,
      {headers:{Authorization:`Bearer ${token}`}});
    if(!res.ok)throw new Error('Quyền đã hết hạn hoặc tệp đính kèm không còn toàn vẹn.');
    const url=URL.createObjectURL(await res.blob());const anchor=document.createElement('a');
    anchor.href=url;anchor.download=file.TenTep;anchor.click();setTimeout(()=>URL.revokeObjectURL(url),30000);
  }catch(e){setError((e as Error).message);}};
  const csv=(name:string,headers:string[],data:(string|number|null|undefined)[][])=>{
    const safe=(value:string|number|null|undefined)=>{let text=String(value??'');
      if(/^[=+\-@]/.test(text))text="'"+text;
      return '"'+text.replaceAll('"','""')+'"';};
    const source='\uFEFF'+[headers,...data].map(row=>row.map(safe).join(',')).join('\r\n');
    const url=URL.createObjectURL(new Blob([source],{type:'text/csv;charset=utf-8'}));
    const anchor=document.createElement('a');anchor.href=url;anchor.download=name;anchor.click();
    setTimeout(()=>URL.revokeObjectURL(url),30000);
  };
  const openPdf=async(req:number,doc:Doc)=>{setError('');try{
    const res=await fetch(`/api/v1/archive/access/${req}/files/${doc.MaVanBan}?nguon=${doc.Nguon}`,{headers:{Authorization:`Bearer ${token}`}});
    if(!res.ok)throw new Error('Không tải được PDF. Quyền có thể đã hết hạn hoặc tệp không toàn vẹn.');
    setPdf(URL.createObjectURL(await res.blob()));
  }catch(e){setError((e as Error).message);}};
  const verify=async(id:string)=>{setError('');setIntegrity(null);
    try{setIntegrity(await api(`/${id}/integrity`,token) as Integrity);}catch(e){setError((e as Error).message);}};
  return <div className="panel archive-advanced">
    <div className="panel-head"><div><h3>Lưu trữ và khai thác hồ sơ · 0.7</h3>
      <p>Tra cứu, cảnh báo hạn, khai thác có duyệt và kiểm toán; không tự động tiêu hủy.</p></div>
      <button className="secondary small" onClick={()=>void refresh().catch(e=>setError((e as Error).message))}><RefreshCw size={15}/> Tải lại</button></div>
    <div className="archive-advanced-tabs">{([['search','Tra cứu & thống kê'],['retention','Bảo quản'],['access','Khai thác'],['audit','Nhật ký truy cập']] as const).map(([key,label])=>
      <button key={key} className={tab===key?'active':''} onClick={()=>setTab(key)}>{label}</button>)}</div>
    {error&&<div className="form-error">{error}</div>}{notice&&<div className="archive-notice">{notice}</div>}
    {tab==='search'&&<>
      <div className="archive-export-row"><button className="secondary small" onClick={()=>csv('ho-so-luu-tru.csv',
        ['Mã hồ sơ','Tên hồ sơ','Năm','Phòng ban','Loại','Người lập','Người nhận','Ngày tiếp nhận','Thời hạn','Tình trạng'],
        filtered.map(r=>[r.MaHoSoNoiBo,r.TenHoSo,r.NamHoSo,r.TenPhongBan,r.LoaiHoSo,r.NguoiLap,r.NguoiNhan,r.NgayTiepNhan,r.SoNamBaoQuan??'Vĩnh viễn',termLabel[r.TinhTrang]]))}>Xuất danh sách CSV</button></div>
      <div className="archive-summary">
        {([['Đã tiếp nhận',rows.length],['Vĩnh viễn',rows.filter(r=>r.TinhTrang==='VINH_VIEN').length],
          ['Sắp hết hạn',rows.filter(r=>r.TinhTrang==='SAP_HET_HAN').length],['Đã hết hạn',rows.filter(r=>r.TinhTrang==='HET_HAN').length]] as const).map(([label,num])=>
          <div key={label}><small>{label}</small><strong>{num}</strong></div>)}</div>
      <div className="archive-advanced-filters">
        <input aria-label="Tìm hồ sơ" value={term} placeholder="Mã hoặc tên hồ sơ" onChange={e=>setTerm(e.target.value)}/>
        <input aria-label="Năm" type="number" value={year} placeholder="Năm" onChange={e=>setYear(e.target.value)}/>
        <input aria-label="Phòng ban" value={dept} placeholder="Phòng ban" onChange={e=>setDept(e.target.value)}/>
        <input aria-label="Loại hồ sơ" value={type} placeholder="Loại hồ sơ" onChange={e=>setType(e.target.value)}/>
        <input aria-label="Người lập" value={author} placeholder="Người lập" onChange={e=>setAuthor(e.target.value)}/>
        <input aria-label="Người nhận" value={recipient} placeholder="Người tiếp nhận" onChange={e=>setRecipient(e.target.value)}/>
        <select aria-label="Tình trạng hạn" value={expiry} onChange={e=>setExpiry(e.target.value)}>
          <option value="">Tất cả thời hạn</option>{Object.entries(termLabel).map(([code,label])=><option key={code} value={code}>{label}</option>)}</select></div>
      {filtered.slice((current-1)*15,current*15).map(r=><div className="archive-advanced-item" key={r.MaHoSo}>
        <div className="archive-advanced-row"><div><strong>{r.MaHoSoNoiBo} — {r.TenHoSo}</strong>
          <small>{r.TenPhongBan??'Toàn cơ quan'} · {r.LoaiHoSo} · {r.NamHoSo} · {r.SoVanBan} văn bản</small>
          <small>Người lập: {r.NguoiLap} · Nhận: {r.NguoiNhan} ({formatDate(r.NgayTiepNhan)})</small></div>
          <span className={`archive-retention-badge ${r.TinhTrang}`}>{termLabel[r.TinhTrang]??r.TinhTrang}</span></div>
        <p className="fine-note">{r.SoNamBaoQuan?`Bảo quản ${r.SoNamBaoQuan} năm · Hết hạn dự kiến: ${formatDate(r.NgayHetHan??undefined)}`:'Bảo quản vĩnh viễn'}</p>
        <div className="archive-actions"><button className="secondary small" onClick={()=>{setTarget(r.MaHoSo);setTab('retention');}}>Đề xuất thời hạn</button>
          <button className="secondary small" onClick={()=>{setTarget(r.MaHoSo);setTab('access');}}>Xin khai thác</button>
          <button className="secondary small" onClick={()=>void verify(r.MaHoSo)}><FileCheck2 size={15}/> Kiểm tra toàn vẹn</button></div></div>)}
      {filtered.length===0&&<p className="archive-empty">Không có hồ sơ đã tiếp nhận phù hợp.</p>}
      <div className="archive-stats"><h4>Thống kê theo năm và phòng ban</h4><div className="table-scroll"><table className="doc-table">
        <thead><tr><th>Năm</th><th>Phòng ban</th><th>Tổng</th><th>Vĩnh viễn</th><th>Sắp hết hạn</th><th>Hết hạn</th></tr></thead>
        <tbody>{stats.map((s,i)=><tr key={`${s.NamHoSo}-${i}`}><td>{s.NamHoSo}</td><td>{s.PhongBan}</td><td>{s.Tong}</td>
          <td>{s.VinhVien}</td><td>{s.SapHetHan}</td><td>{s.HetHan}</td></tr>)}</tbody></table></div></div>
    </>}
    {tab==='retention'&&<>
      <div className="archive-advanced-form"><h4>Đề xuất thời hạn bảo quản (cần Văn thư duyệt)</h4>
        <select value={target} onChange={e=>setTarget(e.target.value)}><option value="">Chọn hồ sơ đã tiếp nhận</option>
          {rows.map(r=><option key={r.MaHoSo} value={r.MaHoSo}>{r.MaHoSoNoiBo} — {r.TenHoSo}</option>)}</select>
        <input type="number" min="1" max="200" value={years} onChange={e=>setYears(e.target.value)} placeholder="Số năm mới; trống = vĩnh viễn"/>
        <textarea value={reason} onChange={e=>setReason(e.target.value)} placeholder="Lý do thay đổi thời hạn (bắt buộc)" rows={2}/>
        <button className="primary" disabled={busy||!target||!reason.trim()} onClick={()=>void action(`/${target}/retention`,{SoNamBaoQuan:years?Number(years):null,LyDo:reason})}>Gửi đề xuất</button></div>
      {retention.slice((current-1)*15,current*15).map(r=><div className="archive-advanced-item" key={r.MaDeXuat}>
        <strong>{r.MaHoSoNoiBo} — {r.TenHoSo}</strong><small>{r.NguoiDeXuat} · {r.SoNamDeXuat?`${r.SoNamDeXuat} năm`:'Vĩnh viễn'} · {r.TrangThai}</small>
        <p>{r.LyDo}</p>{clerk&&r.TrangThai==='CHO_DUYET'&&<div className="archive-actions">
          <button className="primary" disabled={busy} onClick={()=>void action(`/retention/${r.MaDeXuat}/decision`,{ChapThuan:true})}>Duyệt thời hạn</button>
          <button className="secondary" disabled={busy} onClick={()=>void action(`/retention/${r.MaDeXuat}/decision`,{ChapThuan:false})}>Từ chối</button></div>}</div>)}
    </>}
    {tab==='access'&&<>
      <div className="archive-advanced-form"><h4>Yêu cầu khai thác hồ sơ đã tiếp nhận</h4>
        <select value={target} onChange={e=>setTarget(e.target.value)}><option value="">Chọn hồ sơ</option>
          {rows.map(r=><option key={r.MaHoSo} value={r.MaHoSo}>{r.MaHoSoNoiBo} — {r.TenHoSo}</option>)}</select>
        <textarea rows={2} value={reason} onChange={e=>setReason(e.target.value)} placeholder="Lý do khai thác (bắt buộc)"/>
        <button className="primary" disabled={busy||!target||!reason.trim()} onClick={()=>void action(`/${target}/access`,{LyDo:reason})}>Gửi yêu cầu</button></div>
      {clerk&&<div className="archive-advanced-form"><label>Thời gian cấp quyền: <input type="number" min="1" max="72" value={hours} onChange={e=>setHours(e.target.value)}/> giờ (tối đa 72)</label></div>}
      {access.slice((current-1)*15,current*15).map(r=><div className="archive-advanced-item" key={r.MaYeuCau}>
        <strong>{r.MaHoSoNoiBo} — {r.TenHoSo}</strong>
        <small>{r.NguoiYeuCau} · {r.TrangThai} {r.HieuLucDen?`· Hết quyền: ${formatDate(r.HieuLucDen)}`:''}</small><p>{r.LyDo}</p>
        <div className="archive-actions">{clerk&&r.TrangThai==='CHO_DUYET'&&<>
          <button className="primary" disabled={busy||Number(hours)<1||Number(hours)>72} onClick={()=>void action(`/access/${r.MaYeuCau}/decision`,{ChapThuan:true,SoGio:Number(hours)})}>Cấp quyền</button>
          <button className="secondary" disabled={busy} onClick={()=>void action(`/access/${r.MaYeuCau}/decision`,{ChapThuan:false})}>Từ chối</button></>}
          {clerk&&r.TrangThai==='DA_DUYET'&&<button className="secondary" disabled={busy} onClick={()=>void action(`/access/${r.MaYeuCau}/revoke`)}>Thu hồi</button>}
          {r.TrangThai==='DA_DUYET'&&<button className="secondary small" onClick={()=>void getDocs(r.MaYeuCau!)}><FileSearch size={15}/> Xem tài liệu được cấp</button>}</div>
        {selectedAccess===r.MaYeuCau&&<div className="archive-submission-docs">{docs.length===0&&<p className="fine-note">Không có tài liệu được phép xem hoặc quyền chưa có hiệu lực.</p>}
          {docs.map(d=><div key={`${d.Nguon}-${d.MaVanBan}`} className="archive-grant-file"><span>{d.Nguon==='DEN'?'Văn bản đến':'Văn bản đi'} · {d.MaNoiBo} — {d.TieuDe}</span>
            <div className="archive-actions"><button className="secondary small" onClick={()=>void openPdf(r.MaYeuCau!,d)}>Xem PDF đã ký</button>
              <button className="secondary small" onClick={()=>void getAttachments(r.MaYeuCau!,d)}>Tệp đính kèm</button></div>
            {attachments[d.MaVanBan]&&<div className="archive-grant-attachments">{attachments[d.MaVanBan].length===0&&<small>Không có tệp đính kèm.</small>}
              {attachments[d.MaVanBan].map(f=><button className="secondary small" key={f.MaTep} onClick={()=>void downloadAttachment(r.MaYeuCau!,d,f)}>Tải {f.TenTep}</button>)}</div>}</div>)}</div>}</div>)}
    </>}
    {tab==='audit'&&<><div className="archive-export-row"><button className="secondary small" onClick={()=>csv('nhat-ky-khai-thac-ho-so.csv',
      ['Mã hồ sơ','Hành động','Người thực hiện','Thời gian','Nguồn','Mã văn bản'],audit.map(a=>[
        a.MaHoSoNoiBo,a.HanhDong,a.NguoiThucHien,a.ThoiDiem,a.MaVanBanDen?'Đến':a.MaVanBan?'Đi':'Hồ sơ',a.MaVanBanDen??a.MaVanBan
      ]))}>Xuất nhật ký CSV</button></div>
      {audit.slice((current-1)*15,current*15).map(a=><div className="archive-advanced-item" key={a.MaNhatKy}>
      <strong>{a.HanhDong.replaceAll('_',' ')}</strong><small>{a.MaHoSoNoiBo} · {a.NguoiThucHien} · {formatDate(a.ThoiDiem)}</small>
      {a.MaVanBanDen&&<small>Văn bản đến: {a.MaVanBanDen}</small>}
      {a.MaVanBan&&<small>Văn bản đi: {a.MaVanBan}</small>}</div>)}
      {audit.length===0&&<p className="archive-empty">Chưa có lượt truy cập được ghi nhận.</p>}</>}
    {collection.length>15&&<div className="archive-pagination"><button className="secondary small" disabled={current===1} onClick={()=>setPage(current-1)}>Trước</button>
      Trang {current}/{total} <button className="secondary small" disabled={current===total} onClick={()=>setPage(current+1)}>Sau</button></div>}
    {integrity&&<div className="preview-backdrop" onClick={()=>setIntegrity(null)}><div className="archive-audit-modal" onClick={e=>e.stopPropagation()}>
      <h3>Kết quả kiểm tra toàn vẹn</h3><p>Tổng: {integrity.Tong} · Hợp lệ: {integrity.HopLe} · Cần kiểm tra: {integrity.CanKiemTra} · Thiếu PDF ký: {integrity.ThieuPdfDaKy}</p>
      <p className="fine-note">{integrity.GhiChu}</p><div className="archive-audit-list">{integrity.KetQua.map((r,i)=><div key={i}>
        {r.MaBan} — {r.CoTep?'Có tệp':'Thiếu tệp'} · {r.MaBamDung?'SHA-256 đúng':'SHA-256 sai'} · {r.ChuKyToanVen==null?'Tệp đính kèm':r.ChuKyToanVen?'Chữ ký toàn vẹn':'Chữ ký lỗi'}</div>)}</div>
      <button className="secondary" onClick={()=>setIntegrity(null)}>Đóng</button></div></div>}
    {pdf&&<div className="preview-backdrop" onClick={()=>setPdf('')}><div className="archive-pdf-dialog" onClick={e=>e.stopPropagation()}>
      <button className="secondary small" onClick={()=>setPdf('')}>Đóng PDF</button><iframe title="PDF lưu trữ được cấp quyền" src={pdf} /></div></div>}
    <p className="fine-note archive-advanced-footnote"><LockKeyhole size={13}/> Không tự động xóa hồ sơ. Quyền khai thác chỉ có hiệu lực sau khi Văn thư duyệt và hết hạn tối đa sau 72 giờ.</p>
  </div>;
}
