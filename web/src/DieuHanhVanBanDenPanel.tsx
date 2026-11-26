import {useEffect,useState} from 'react';

type Row={maVanBanDen:string;tieuDe:string;soDen:number|null;trangThai:string;
  maPhongChuTri:number|null;tenPhongChuTri:string|null;tenPhongPhoiHop:string|null;
  trangThaiXuLy:string|null;soBaoCao:number;soPhong:number;chiDao:string|null;ketQuaTongHop:string|null};
type Department={maPhongBan:number;tenPhongBan:string};
type Report={maPhongBan:number;tenPhongBan:string;nguoiBaoCao:string;noiDung:string;thoiDiemBaoCao:string};
type Person={maNguoiDung:number;maPhongBan:number;hoTen:string;tenChucVu:string|null};
type Assignment={maPhongBan:number;maNguoiChuTri:number;tenNguoiChuTri:string;chucVuChuTri:string|null;tenNguoiPhoiHop:string|null;ghiChu:string|null};
type Evidence={maMinhChung:string;maPhongBan:number;tenTep:string;nguoiTaiLen:string};
type Reassignment={maLichSu:number;maPhongBan:number;nguoiCu:string;nguoiMoi:string;nguoiDieuChinh:string;lyDo:string};
type People={rooms:{maPhongBan:number;tenPhongBan:string;laPhongChuTri:boolean}[];
  employees:Person[];assigned:Assignment[];changes:Reassignment[];actorDepartment:number|null;canAssign:boolean;canReport:boolean};
type Inbox={rows:Row[];departments:Department[];leader:boolean;clerk:boolean};
const empty:Inbox={rows:[],departments:[],leader:false,clerk:false};
// Dapper tra ve ten cot PascalCase, con record C# duoc serialize camelCase.
// Chuan hoa ca hai de maVanBanDen va cac key danh sach luon co gia tri.
const normalizeKeys=(value:unknown):unknown=>{
  if(Array.isArray(value))return value.map(normalizeKeys);
  if(value!==null&&typeof value==='object')return Object.fromEntries(
    Object.entries(value).map(([key,item])=>[key.charAt(0).toLowerCase()+key.slice(1),normalizeKeys(item)]));
  return value;
};

export default function DieuHanhVanBanDenPanel({token,onUpdated}:{token:string;onUpdated:()=>Promise<void>}){
  const [data,setData]=useState<Inbox>(empty);
  const [selected,setSelected]=useState('');
  const [lead,setLead]=useState('');
  const [partners,setPartners]=useState<number[]>([]);
  const [note,setNote]=useState('');
  const [deadline,setDeadline]=useState('');
  const [result,setResult]=useState('');
  const [reports,setReports]=useState<Report[]>([]);
  const [people,setPeople]=useState<People|null>(null);
  const [personalOwner,setPersonalOwner]=useState('');
  const [personalHelpers,setPersonalHelpers]=useState<number[]>([]);
  const [personalNote,setPersonalNote]=useState('');
  const [reason,setReason]=useState('');
  const [reassignOpen,setReassignOpen]=useState(false);
  const [evidence,setEvidence]=useState<Evidence[]>([]);
  const [evidenceFile,setEvidenceFile]=useState<File|null>(null);
  const [localEvidencePreview,setLocalEvidencePreview]=useState('');
  const [savedEvidencePreviews,setSavedEvidencePreviews]=useState<Record<string,string>>({});
  const [pdfUrl,setPdfUrl]=useState('');
  const [page,setPage]=useState(1);
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState('');
  const [error,setError]=useState('');
  useEffect(()=>()=>{if(pdfUrl)URL.revokeObjectURL(pdfUrl);},[pdfUrl]);
  useEffect(()=>{
    if(!evidenceFile){setLocalEvidencePreview('');return;}
    if(!['image/png','image/jpeg','image/webp'].includes(evidenceFile.type)){
      setError('Chỉ chọn ảnh PNG, JPG hoặc WEBP.');setEvidenceFile(null);return;
    }
    const url=URL.createObjectURL(evidenceFile);
    setLocalEvidencePreview(url);
    return ()=>URL.revokeObjectURL(url);
  },[evidenceFile]);
  useEffect(()=>{
    let active=true;
    const generated:string[]=[];
    setSavedEvidencePreviews({});
    if(!selected||!evidence.length)return;
    (async()=>{
      const images:Record<string,string>={};
      for(const file of evidence){
        try{
          const response=await fetch(`/api/v1/incoming-work/${selected}/evidence/${file.maMinhChung}`,{
            headers:{Authorization:`Bearer ${token}`}});
          if(!response.ok)continue;
          const url=URL.createObjectURL(await response.blob());
          generated.push(url);images[file.maMinhChung]=url;
        }catch{/* từng ảnh có thể tạm thời không truy cập được */}
      }
      if(active)setSavedEvidencePreviews(images);
      else generated.forEach(url=>URL.revokeObjectURL(url));
    })();
    return ()=>{active=false;generated.forEach(url=>URL.revokeObjectURL(url));};
  },[evidence,selected,token]);
  const api=async(path:string,body?:object):Promise<any>=>{
    const response=await fetch('/api/v1/incoming-work'+path,{method:body?'POST':'GET',
      headers:{Authorization:`Bearer ${token}`,'Content-Type':'application/json'},
      body:body?JSON.stringify(body):undefined});
    const content=await response.json().catch(()=>({}));
    if(!response.ok)throw new Error(content.message??`Máy chủ trả về ${response.status}`);
    return normalizeKeys(content) as any;
  };
  const load=async()=>setData(await api(''));
  useEffect(()=>{load().catch(e=>setError(e.message));},[token]);
  const choose=async(row:Row)=>{
    setSelected(row.maVanBanDen);setLead(row.maPhongChuTri?String(row.maPhongChuTri):'');
    setPartners([]);setResult('');setReports([]);setPeople(null);setPersonalOwner('');
    setPersonalHelpers([]);setPersonalNote('');setReason('');setReassignOpen(false);
    setEvidence([]);setEvidenceFile(null);setMessage('');setError('');
    if(row.maPhongChuTri)try{
      const [existing,assignment,files]=await Promise.all([
        api(`/${row.maVanBanDen}/reports`),api(`/${row.maVanBanDen}/people`),api(`/${row.maVanBanDen}/evidence`)]);
      setReports(existing);setPeople(assignment);setEvidence(files);
    }
      catch(e){setError((e as Error).message);}
  };
  const submit=async(path:string,body:object={})=>{
    setBusy(true);setError('');setMessage('');
    try{const response=await api(path,body);setMessage(response.message??'Đã cập nhật.');
      await load();await onUpdated();
      if(selected&&(path.includes('/report')||path.includes('/people'))){
        setReports(await api(`/${selected}/reports`));setPeople(await api(`/${selected}/people`));
        setEvidence(await api(`/${selected}/evidence`));setReassignOpen(false);setReason('');
      }
    }catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  const picked=data.rows.find(row=>row.maVanBanDen===selected);
  const myRoom=people?.rooms.find(room=>room.maPhongBan===people.actorDepartment);
  const myMembers=people?.employees.filter(person=>person.maPhongBan===people.actorDepartment)??[];
  const myAssignment=people?.assigned.find(item=>item.maPhongBan===people.actorDepartment);
  const personLabel=(person:Person)=>`${person.hoTen}${person.tenChucVu?` — ${person.tenChucVu}`:''}`;
  const uploadEvidence=async()=>{
    if(!evidenceFile||!selected)return;
    if(evidenceFile.size>5*1024*1024){setError('Ảnh minh chứng tối đa 5 MB.');return;}
    setBusy(true);setError('');
    try{
      const form=new FormData();form.append('tepMinhChung',evidenceFile);
      const response=await fetch(`/api/v1/incoming-work/${selected}/evidence`,{
        method:'POST',headers:{Authorization:`Bearer ${token}`},body:form});
      const value=await response.json().catch(()=>({}));
      if(!response.ok)throw new Error(value.message??`Lỗi ${response.status}`);
      setMessage(value.message);setEvidenceFile(null);
      setEvidence(await api(`/${selected}/evidence`));
    }catch(err){setError((err as Error).message);}finally{setBusy(false);}
  };
  const viewEvidence=async(id:string,name:string)=>{
    try{
      const response=await fetch(`/api/v1/incoming-work/${selected}/evidence/${id}`,{
        headers:{Authorization:`Bearer ${token}`}});
      if(!response.ok)throw new Error(`Không thể xem ảnh ${name}: ${response.status}`);
      const url=URL.createObjectURL(await response.blob());
      window.open(url,'_blank','noopener,noreferrer');
      window.setTimeout(()=>URL.revokeObjectURL(url),60000);
    }catch(err){setError((err as Error).message);}
  };
  const viewDocument=async()=>{
    if(!selected)return;
    setBusy(true);setError('');
    try{
      const response=await fetch(`/api/v1/incoming-work/${selected}/pdf`,{
        headers:{Authorization:`Bearer ${token}`}});
      if(!response.ok){
        const body=await response.json().catch(()=>({}));
        throw new Error(body.message??`Không thể mở PDF văn bản đến (${response.status}).`);
      }
      setPdfUrl(URL.createObjectURL(await response.blob()));
    }catch(err){setError((err as Error).message);}finally{setBusy(false);}
  };
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
        {data.clerk&&row.soDen==null&&<button className="secondary small" disabled={busy}
          onClick={()=>submit(`/${row.maVanBanDen}/register`)}>Cấp số đến</button>}
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
      <button className="secondary small" disabled={busy} onClick={viewDocument}>Xem bản PDF đã ký số và đóng dấu</button>
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
        <div className="incoming-work-report"><strong>Phân công cá nhân thực hiện</strong>
          {people?.rooms.map(room=>{
            const assigned=people.assigned.find(a=>a.maPhongBan===room.maPhongBan);
            return <p key={room.maPhongBan}><strong>{room.tenPhongBan}</strong> ({room.laPhongChuTri?'Chủ trì':'Phối hợp'}): {' '}
              {assigned?`${assigned.tenNguoiChuTri}${assigned.chucVuChuTri?` (${assigned.chucVuChuTri})`:''} — chủ trì cá nhân${assigned.tenNguoiPhoiHop?`; phối hợp: ${assigned.tenNguoiPhoiHop}`:''}`:'Chưa phân công cá nhân'}</p>;
          })}
        </div>
        {people?.changes.length? <div className="incoming-work-report"><strong>Lịch sử điều chỉnh phân công</strong>
          {people.changes.map(item=><p key={item.maLichSu}>{item.nguoiCu} → {item.nguoiMoi}
            {' · '}{item.nguoiDieuChinh}: {item.lyDo}</p>)}
        </div>:null}
        {picked.trangThaiXuLy==='DANG_XU_LY'&&people?.canAssign&&myRoom&&<>
          {myAssignment&&!reassignOpen&&<div className="incoming-work-report">
            <strong>Đã giao cho {myAssignment.tenNguoiChuTri}{myAssignment.chucVuChuTri?` — ${myAssignment.chucVuChuTri}`:''}</strong>
            <p>Phân công được khóa sau khi giao. Muốn thay đổi phải ghi lý do.</p>
            <button className="secondary small" disabled={busy||reports.some(r=>r.maPhongBan===myRoom.maPhongBan)}
              onClick={()=>{setReassignOpen(true);setPersonalOwner(String(myAssignment.maNguoiChuTri));}}>
              Đổi phân công</button></div>}
          {(!myAssignment||reassignOpen)&&<div className="incoming-work-form">
          <h4>Phân công người thực hiện — {myRoom.tenPhongBan}</h4>
          <label>Cá nhân chủ trì thực hiện
            <select value={personalOwner} onChange={e=>{
              setPersonalOwner(e.target.value);setPersonalHelpers(p=>p.filter(x=>x!==Number(e.target.value)));}}>
              <option value="">Chọn một người chịu trách nhiệm</option>
              {myMembers.map(member=><option key={member.maNguoiDung} value={member.maNguoiDung}>{personLabel(member)}</option>)}
            </select></label>
          <div><strong>Cá nhân phối hợp trong phòng (không bắt buộc)</strong>
            <div className="incoming-work-checks">{myMembers.filter(member=>String(member.maNguoiDung)!==personalOwner)
              .map(member=><label key={member.maNguoiDung}>
                <input type="checkbox" checked={personalHelpers.includes(member.maNguoiDung)} onChange={e=>
                  setPersonalHelpers(list=>e.target.checked?[...list,member.maNguoiDung]:list.filter(x=>x!==member.maNguoiDung))}/>
                {personLabel(member)}</label>)}</div></div>
          <label>Ghi chú giao việc<textarea maxLength={2000} value={personalNote} onChange={e=>setPersonalNote(e.target.value)}/></label>
          {myAssignment&&<label>Lý do thay đổi phân công (bắt buộc)
            <textarea maxLength={1000} value={reason} onChange={e=>setReason(e.target.value)}
              placeholder="Giải thích vì sao cần giao lại, lưu trong lịch sử điều chỉnh"/></label>}
          <button className="primary small" disabled={busy||!personalOwner||(Boolean(myAssignment)&&!reason.trim())} onClick={()=>submit(`/${selected}/people`,{
            maPhongBan:myRoom.maPhongBan,maNguoiChuTri:Number(personalOwner),
            maNguoiPhoiHop:personalHelpers,ghiChu:personalNote,lyDoGiaoLai:reason})}>
            {myAssignment?'Xác nhận giao lại':'Giao cho cá nhân'}</button>
          {myAssignment&&<button className="secondary small" onClick={()=>setReassignOpen(false)}>Hủy thay đổi</button>}
        </div>}</>}
        {reports.map(r=><div className="incoming-work-report" key={r.maPhongBan}><strong>{r.tenPhongBan} — {r.nguoiBaoCao}</strong>
          <p>{r.noiDung}</p></div>)}
        {reports.length===0&&<p className="subtle">Chưa có báo cáo từ các phòng.</p>}
        {evidence.length>0&&<div className="incoming-work-report"><strong>Ảnh minh chứng đã lưu</strong>
          <div className="incoming-evidence-grid">{evidence.map(file=><div key={file.maMinhChung} className="incoming-evidence-card">
            {savedEvidencePreviews[file.maMinhChung]
              ?<img src={savedEvidencePreviews[file.maMinhChung]} alt={file.tenTep}/>
              :<span className="subtle">Đang tải ảnh...</span>}
            <small>{file.tenTep} — {file.nguoiTaiLen}</small>
            <button className="secondary small" onClick={()=>viewEvidence(file.maMinhChung,file.tenTep)}>Xem lớn</button>
          </div>)}</div>
        </div>}
        {picked.trangThaiXuLy==='DANG_XU_LY'&&<div className="incoming-work-form">
          <label>Kết quả thực hiện (chỉ cá nhân chủ trì được phân công mới gửi được)
            <textarea value={result} onChange={e=>setResult(e.target.value)} maxLength={4000}/></label>
          <button className="secondary small" disabled={busy||!result.trim()||!people?.canReport} onClick={()=>submit(`/${selected}/report`,{noiDung:result})}>
            Gửi báo cáo của phòng</button>
          {people?.canReport&&reports.some(report=>report.maPhongBan===people.actorDepartment)&&<div>
            <label>Đính kèm ảnh minh chứng (PNG/JPG/WEBP, tối đa 5 MB/ảnh)
              <input type="file" accept="image/png,image/jpeg,image/webp" onChange={e=>setEvidenceFile(e.target.files?.[0]??null)}/></label>
            {localEvidencePreview&&<div className="incoming-evidence-selection"><img src={localEvidencePreview} alt="Ảnh minh chứng chuẩn bị tải lên"/>
              <small>Xem trước: {evidenceFile?.name} — Chưa lưu lên máy chủ</small></div>}
            <button className="secondary small" disabled={busy||!evidenceFile} onClick={uploadEvidence}>
              {busy?'Đang tải ảnh...':'Tải ảnh minh chứng'}</button>
          </div>}
          {data.leader&&<button className="primary small" disabled={busy||!result.trim()||picked.soBaoCao===0}
            onClick={()=>submit(`/${selected}/complete`,{ketQuaTongHop:result})}>Kết thúc xử lý (Kết luận cấp trên)</button>}
        </div>}
        {picked.ketQuaTongHop&&<p><strong>Kết luận cấp trên:</strong> {picked.ketQuaTongHop}</p>}
      </>}
    </div>}
    {pdfUrl&&<div className="incoming-work-preview-overlay" role="dialog" aria-modal="true" aria-label="Xem văn bản đến">
      <div className="incoming-work-preview">
        <div className="incoming-work-preview-head"><strong>Văn bản đến — {picked?.tieuDe??''}</strong>
          <button className="secondary small" onClick={()=>setPdfUrl('')}>Đóng</button></div>
        <iframe src={pdfUrl} title="PDF văn bản đến"/>
      </div>
    </div>}
  </section>;
}
