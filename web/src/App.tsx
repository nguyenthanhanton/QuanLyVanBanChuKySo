import { useEffect, useMemo, useState, type FormEvent } from 'react';
import type { LucideIcon } from 'lucide-react';
import {
  Activity, Archive, ArrowLeft, ArrowRight, ArrowUpRight, Bell, Building2,
  CalendarDays, Check, CheckCheck, CheckCircle2, ChevronDown, ChevronRight,
  CircleAlert, Clock3, CloudUpload, FileCheck2, FileClock, FilePlus2,
  Files, FileSignature, FileText, Filter, FolderClosed, Inbox, LayoutDashboard,
  ListTodo, LockKeyhole, LogOut, Menu, MoreHorizontal, Network, Plus,
  RefreshCw, Route, Search, Send, Settings2, ShieldCheck, UserRound, Users,
  X
} from 'lucide-react';
import { demoDocuments, demoUsers } from './demo';
import type { Actor, ChuyenPhat, SuKien, Trang, TrangThai, VanBan } from './types';
import { formatDate, stageLabel, transferLabel } from './types';

const DEMO = import.meta.env.VITE_DEMO_MODE !== 'false';
const APP_TITLE = 'Văn thư số';
const storedDocsKey = 'qlvb-demo-documents-v1';
const field = (object: Record<string,unknown>, key:string):unknown => object[key] ?? object[key[0].toLowerCase()+key.slice(1)];

type Navigation = { group: string; items: { id: Trang; label: string; icon: LucideIcon; permission: string }[] }[];
const navigation: Navigation = [
  { group: 'KHÔNG GIAN LÀM VIỆC', items: [
    { id:'tong-quan',label:'Tổng quan',icon:LayoutDashboard,permission:'documents.read' },
    { id:'van-ban-di',label:'Văn bản đi',icon:Send,permission:'documents.read' },
    { id:'van-ban-den',label:'Văn bản đến',icon:Inbox,permission:'incoming.manage' },
    { id:'cong-viec',label:'Việc cần xử lý',icon:ListTodo,permission:'workflow.queue' },
  ] },
  { group: 'THEO DÕI & LƯU TRỮ', items: [
    { id:'hanh-trinh',label:'Hành trình văn bản',icon:Route,permission:'documents.read' },
    { id:'giao-nhan',label:'Giao nhận liên cơ quan',icon:Network,permission:'publications.deliver' },
    { id:'ho-so',label:'Hồ sơ lưu trữ',icon:FolderClosed,permission:'dossiers.manage' },
    { id:'bao-cao',label:'Báo cáo thống kê',icon:Activity,permission:'reports.read' },
  ] },
  { group: 'QUẢN TRỊ', items: [
    { id:'quan-tri',label:'Quản trị hệ thống',icon:Settings2,permission:'agency.admins.manage' },
  ] },
];

const names: Record<Trang, string> = {
  'tong-quan':'Tổng quan', 'van-ban-di':'Quản lý văn bản đi', 'van-ban-den':'Quản lý văn bản đến',
  'cong-viec':'Việc cần xử lý', 'hanh-trinh':'Hành trình văn bản', 'giao-nhan':'Theo dõi giao nhận',
  'ho-so':'Hồ sơ lưu trữ', 'bao-cao':'Báo cáo thống kê', 'quan-tri':'Quản trị hệ thống'
};

function defaultDocs(): VanBan[] {
  try {
    const value = localStorage.getItem(storedDocsKey);
    if (value) return JSON.parse(value) as VanBan[];
  } catch { /* corrupted demo storage: restore packaged examples */ }
  return demoDocuments;
}

function tone(status: TrangThai | ChuyenPhat['status']) {
  if (['HOAN_THANH','DA_TIEP_NHAN','DA_PHAT_HANH','DA_KY'].includes(status)) return 'green';
  if (['TRA_SUA','TU_CHOI'].includes(status)) return 'red';
  if (['CHO_DUYET','CHO_CAP_SO','CHO_KY','CHO_GUI'].includes(status)) return 'orange';
  if (['DANG_CHUYEN','DA_CAP_SO','DA_GUI'].includes(status)) return 'blue';
  return 'gray';
}

function Status({ status, small=false }: { status: TrangThai | ChuyenPhat['status']; small?: boolean }) {
  const name = (stageLabel as Partial<Record<string,string>>)[status] ??
    (transferLabel as Partial<Record<string,string>>)[status] ?? status;
  return <span className={`status status-${tone(status)} ${small ? 'status-small' : ''}`}><span className="status-dot" />{name}</span>;
}

function isAllowed(user: Actor | null, permission: string) {
  return user?.capabilities.includes(permission) ?? false;
}

function getAccessibleDocs(docs: VanBan[], actor: Actor | null): VanBan[] {
  if (!actor || !isAllowed(actor,'documents.read')) return [];
  if (!DEMO) return docs; // Live API already enforces tenant/role scope server-side.
  if (actor.agency === 'Cơ quan A') return docs.filter(d => d.agency === actor.agency);
  // Recipients only see released snapshot + receipt; never sender's draft/review/signing events.
  const own = docs.filter(d => d.agency === actor.agency);
  const inbound = docs.filter(d => d.agency !== actor.agency && d.transfers.some(t => t.agency === actor.agency))
    .map(d => ({...d,type:'DEN' as const,department:'Văn thư',creator:d.agency,assignee:'Văn thư tiếp nhận',
      events:d.events.filter(e => e.action==='PHAT_HANH' ||
        (['TIEP_NHAN','DANG_CHUYEN'].includes(e.action) && e.agency===actor.agency))
        .map(e => ({...e, detail: e.action === 'PHAT_HANH' ? 'Bản đã phát hành được gửi đến cơ quan' : 'Trạng thái giao nhận công khai'})),
      transfers:d.transfers.filter(t => t.agency === actor.agency), version:1 }));
  return [...own,...inbound];
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/); return (parts.at(-2)?.[0] ?? '') + (parts.at(-1)?.[0] ?? '');
}

async function requestApi(path:string, token:string, init?:RequestInit) {
  const res=await fetch('/api/v1'+path,{...init,headers:{ ...(init?.body instanceof FormData ? {} : {'Content-Type':'application/json'}),
    Authorization:`Bearer ${token}`,...init?.headers }});
  if(!res.ok) {
    const problem=await res.json().catch(()=>({message:'Không thể kết nối máy chủ.'}));
    throw new Error(problem.message ?? `Máy chủ trả lỗi ${res.status}`);
  }
  return res.json();
}

async function downloadApi(path:string,token:string,filename:string) {
  const res=await fetch('/api/v1'+path,{headers:{Authorization:`Bearer ${token}`}});
  if(!res.ok){const problem=await res.json().catch(()=>({message:'Không tải được tệp.'}));
    throw new Error(problem.message??`Máy chủ trả lỗi ${res.status}`);}
  const url=URL.createObjectURL(await res.blob());
  const anchor=document.createElement('a');anchor.href=url;anchor.download=filename;anchor.click();
  window.setTimeout(()=>URL.revokeObjectURL(url),30000);
}

function FilePreview({path,token,filename,onClose}:{path:string;token:string;filename:string;onClose:()=>void}) {
  const [url,setUrl]=useState('');const [content,setContent]=useState('');const [error,setError]=useState('');
  useEffect(()=>{
    let alive=true;let temporary='';setError('');setUrl('');setContent('');
    const openXml=/\.(docx|xlsx)$/i.test(filename);
    fetch('/api/v1'+path+(openXml?'?preview=1':''),{headers:{Authorization:`Bearer ${token}`}}).then(async response=>{
      if(!response.ok)throw new Error('Không có quyền xem hoặc tệp không còn nguyên vẹn.');
      if(openXml){const plain=await response.text();if(alive)setContent(plain);return;}
      const data=await response.blob();temporary=URL.createObjectURL(data);
      if(alive)setUrl(temporary);else URL.revokeObjectURL(temporary);
    }).catch(err=>{if(alive)setError((err as Error).message);});
    return ()=>{alive=false;if(temporary)URL.revokeObjectURL(temporary);};
  },[path,token]);
  return <div className="preview-backdrop" onClick={onClose}><div className="preview-window" onClick={event=>event.stopPropagation()}>
    <div className="preview-header"><strong><FileText size={18}/>{filename}</strong><button className="icon-button" onClick={onClose} aria-label="Đóng xem trước"><X size={20}/></button></div>
    {error&&<div className="form-error">{error}</div>}
    {!error&&!url&&<p className="subtle">Đang tải tệp an toàn...</p>}
    {content&&<div className="preview-text"><p className="fine-note">Bản xem trước nội dung chữ/bảng tính; bố cục có thể khác Microsoft Office.</p><pre>{content}</pre></div>}
    {url&&(filename.toLowerCase().endsWith('.pdf')?<iframe title={`Xem ${filename}`} src={url+'#toolbar=1'} className="preview-pdf"/>:
      <div className="preview-other"><p>Định dạng Office cũ (.doc/.xls) cần bộ chuyển đổi riêng để xem trực tiếp đúng bố cục.</p>
        <button className="primary" onClick={()=>downloadApi(path,token,filename).catch(err=>setError((err as Error).message))}>Tải tệp để xem<ArrowRight size={15}/></button></div>)}
  </div></div>;
}

function adaptLiveDocument(d: Record<string,unknown>):VanBan {
  const s = (k:string) => String(field(d,k) ?? '');
  return {
    id:s('MaVanBan'),code:s('MaNoiBo'),
    number:field(d,'SoPhatHanh')?`${s('SoPhatHanh')}/${s('KyHieu')}`:'',
    title:s('TieuDe'),excerpt:s('TrichYeu'),
    type:(s('LoaiVanBan') || 'DI') as VanBan['type'],status:(s('TrangThai') || 'NHAP') as TrangThai,
    urgency:(s('DoKhan') || 'THUONG') as VanBan['urgency'],secrecy:(s('DoMat') || 'THUONG') as VanBan['secrecy'],
    agency:'Cơ quan hiện tại',department:'',creator:s('NguoiTao'),assignee:'Theo quy trình',
    createdAt:s('ThoiDiemTao'),version:Number(field(d,'PhienBanHienTai') ?? 1),to:[],events:[],transfers:[]
  };
}

function Login({onLogin}:{onLogin:(name:string,password:string)=>Promise<void>}) {
  const [name,setName]=useState('vanthu_a');
  const [password,setPassword]=useState(DEMO?'Password123!':'');
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const submit=async(e:FormEvent)=>{
    e.preventDefault();setBusy(true);setError('');
    try{await onLogin(name,password);}catch(err){setError((err as Error).message);}finally{setBusy(false);}
  };
  return <div className="login-wrap">
    <div className="login-brand"><div className="brand-symbol large"><Files size={27}/></div>
      <span>{APP_TITLE}</span><div className="login-decor"><FileSignature size={190}/></div>
      <h1>Quản lý văn bản<br/>đồng bộ, minh bạch.</h1>
      <p>Mọi quyết định, chữ ký và hành trình luân chuyển được ghi nhận tại một nơi.</p>
      <div className="login-feature"><ShieldCheck size={19}/> Phân quyền đa cơ quan</div>
      <div className="login-feature"><Route size={19}/> Theo dõi đường đi từng văn bản</div>
      <div className="login-feature"><FileCheck2 size={19}/> Lưu lịch sử và phiên bản đầy đủ</div>
    </div>
    <div className="login-side"><form className="login-form" onSubmit={submit}>
      <div className="login-eyebrow">CHÀO MỪNG TRỞ LẠI</div><h2>Đăng nhập hệ thống</h2>
      <p>Nhập thông tin tài khoản được cấp để tiếp tục công việc.</p>
      <label>Tên đăng nhập</label><input value={name} onChange={e=>setName(e.target.value)} autoComplete="username" required />
      <label>Mật khẩu</label><input type="password" value={password} onChange={e=>setPassword(e.target.value)} autoComplete="current-password" required />
      {error && <div className="form-error"><CircleAlert size={17}/>{error}</div>}
      <button className="primary full" disabled={busy}>{busy?'Đang xác thực...':'Đăng nhập'}<ArrowRight size={18}/></button>
      {DEMO && <div className="demo-login"><strong>Chế độ minh họa</strong><br/>
        Dùng <code>vanthu_a</code>, <code>truongphong_a</code>, <code>lanhdao_a</code>, <code>vanthu_b</code> hoặc <code>quantrihethong</code> với mật khẩu <code>Password123!</code>.
        <div>Không có chữ ký số thật hoặc dữ liệu sản xuất.</div>
      </div>}
    </form></div>
  </div>;
}

function ProgressLine({doc}:{doc:VanBan}) {
  const stages = [
    {label:'Soạn thảo',done:true},
    {label:'Phê duyệt',done:!['NHAP','TRA_SUA','CHO_DUYET'].includes(doc.status)},
    {label:'Ký số',done:['DA_KY','DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH'].includes(doc.status)},
    {label:'Phát hành',done:['DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH'].includes(doc.status)},
    {label:'Giao nhận',done:doc.status==='HOAN_THANH'}
  ];
  const count=stages.filter(s=>s.done).length;
  return <div className="progress-flow">{stages.map((stage,i)=><div className="progress-stage" key={stage.label}>
    <div className={`progress-node ${stage.done?'complete':i===count?'active':''}`}>
      {stage.done?<Check size={15}/>:<span>{i+1}</span>}
    </div><span>{stage.label}</span>{i<stages.length-1 && <span className={`progress-connector ${stage.done && stages[i+1]?.done?'complete':''}`}/>}
  </div>)}</div>;
}

function Timeline({doc}:{doc:VanBan}) {
  return <div className="timeline">
    {[...doc.events].reverse().map((event,i)=><div className="timeline-entry" key={event.id}>
      <div className="timeline-rail"><span className={`timeline-node ${event.state}`}>
        {event.state==='warning'?<CircleAlert size={15}/>:event.state==='current'?<Clock3 size={15}/>:<Check size={15}/>}</span>
        {i!==doc.events.length-1&&<span className="timeline-stem"/>}</div>
      <div className="timeline-content"><div className="timeline-top">
        <strong>{event.title}</strong><time>{formatDate(event.time)}</time></div>
        <p>{event.detail}</p><div className="timeline-by"><UserRound size={13}/>{event.by}<span className="dot-separator"/>
        <Building2 size={13}/>{event.agency}{event.version&&<span className="version-chip">v{event.version}</span>}</div>
      </div>
    </div>)}
    {doc.events.length===0 && <div className="empty-state">Chưa có sự kiện hành trình.</div>}
  </div>;
}

function TransferCards({doc}:{doc:VanBan}) {
  if(doc.transfers.length===0) return <div className="empty-card"><Network size={26}/><strong>Chưa phát hành liên cơ quan</strong><span>Khi văn bản được phát hành, nơi nhận và biên nhận sẽ xuất hiện ở đây.</span></div>;
  return <div className="transfer-cards">{doc.transfers.map(t=><div className="transfer-card" key={t.id}>
    <div className="transfer-card-top"><div className="agency-icon"><Building2 size={18}/></div><div className="transfer-name"><strong>{t.agency}</strong><span>{t.contact}</span></div><Status status={t.status} small/></div>
    <div className="transfer-meta"><span>Thời gian gửi</span><strong>{formatDate(t.sentAt)}</strong></div>
    <div className="transfer-meta"><span>Tiếp nhận</span><strong>{formatDate(t.receivedAt)}</strong></div>
    {t.note && <p className="transfer-note">{t.note}</p>}
  </div>)}</div>;
}

function SignaturePanel({doc,actor,token,onUpdated}:{doc:VanBan;actor:Actor|null;token:string;onUpdated:()=>Promise<void>}) {
  const [accountPassword,setAccountPassword]=useState('');
  const [pfxPassword,setPfxPassword]=useState('');
  const [certName,setCertName]=useState('');
  const [certType,setCertType]=useState('CA_NHAN');
  const [testCert,setTestCert]=useState(false);
  const [pfxFile,setPfxFile]=useState<File>();
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState('');
  const [verify,setVerify]=useState<Record<string,unknown>|null>(null);
  const [credentials,setCredentials]=useState<Record<string,unknown>[]>([]);
  useEffect(()=>{
    if(DEMO||!token)return;
    requestApi('/signing-credentials',token).then((x:Record<string,unknown>[])=>setCredentials(x)).catch(()=>{});
    requestApi(`/documents/${doc.id}/signatures/verification`,token).then((x:Record<string,unknown>)=>setVerify(x)).catch(()=>{});
  },[doc.id,token]);
  const operation=async(kind:'prepare'|'sign'|'publish'|'verify'|'download',signature?:string)=>{
    setBusy(true);setMessage('');
    try {
      if(kind==='download'){
        await downloadApi(`/documents/${doc.id}/signed-pdf`,token,`van-ban-${doc.code}.pdf`);
      } else if(kind==='verify'){
        setVerify(await requestApi(`/documents/${doc.id}/signatures/verification`,token));
      } else {
        if(kind==='sign'&&!accountPassword)throw new Error('Cần nhập mật khẩu để xác nhận ký.');
        const endpoint=kind==='prepare'?`/documents/${doc.id}/prepared-pdf`:
          kind==='sign'?`/documents/${doc.id}/signatures`:`/documents/${doc.id}/publications`;
        const payload=kind==='sign'?{loaiChuKy:signature,matKhau:accountPassword,maYeuCau:crypto.randomUUID()}:
          {maYeuCau:crypto.randomUUID()};
        await requestApi(endpoint,token,{method:'POST',body:JSON.stringify(payload)});
        setAccountPassword('');
        await onUpdated();
        try {setVerify(await requestApi(`/documents/${doc.id}/signatures/verification`,token));}catch{/* no signed PDF yet */}
        setMessage('Đã lưu thao tác và cập nhật hành trình trên máy chủ.');
      }
    } catch(err){setMessage((err as Error).message);}finally{setBusy(false);}
  };
  const createCredential=async()=>{
    setBusy(true);setMessage('');
    try {
      if(!accountPassword)throw new Error('Nhập mật khẩu tài khoản để xác nhận.');
      if(!testCert&&!pfxFile)throw new Error('Chọn tệp PFX/P12 của bạn.');
      const form=new FormData();form.set('loaiChungThu',certType);form.set('tenChungThu',certName);
      form.set('matKhau',accountPassword);form.set('matKhauPfx',pfxPassword);
      form.set('thuNghiem',String(testCert));if(pfxFile)form.set('tepPfx',pfxFile);
      const result=await requestApi('/signing-credentials',token,{method:'POST',body:form});
      setMessage((result.noiBoThuNghiem?'Đã tạo chứng thư thử nghiệm nội bộ.':'Đã nhập chứng thư;')+
        ' Hệ thống chưa thẩm định chuỗi CA/thu hồi.');
      setAccountPassword('');setPfxPassword('');setPfxFile(undefined);
      setCredentials(await requestApi('/signing-credentials',token));
    }catch(err){setMessage((err as Error).message);}finally{setBusy(false);}
  };
  const activeCreds=credentials.filter(c=>field(c,'DangHoatDong')===true);
  const signatures=(field(verify??{},'ChuKy') as Record<string,unknown>[]|undefined)??[];
  return <section className="drawer-section signature-live">
    <div className="signature-header"><div className="signature-icon"><ShieldCheck size={30}/></div>
      <h3>Ký số PDF và xác minh</h3><p>Ký CMS được nhúng vào PDF, lần lượt từng vai trò; không dùng ảnh chữ ký.</p></div>
    <div className="form-section"><h4>Chứng thư đang sử dụng</h4>
      {activeCreds.map(c=><div className="version-row" key={String(field(c,'MaChungThu'))}>
        <ShieldCheck size={18}/><div><strong>{String(field(c,'TenChungThu'))}</strong><span>{String(field(c,'LoaiChungThu'))} · {field(c,'NguonChungThu')==='TU_KY_THU_NGHIEM'?'Thử nghiệm nội bộ':'PFX nhập'} · Hết hạn {formatDate(String(field(c,'NgayHetHan')))}</span></div>
      </div>)}
      <div className="form-grid"><label>Loại chứng thư<select value={certType} onChange={e=>setCertType(e.target.value)}>
        <option value="CA_NHAN">Cá nhân</option><option value="CO_QUAN">Cơ quan (văn thư)</option></select></label>
        <label>Tên chứng thư<input value={certName} onChange={e=>setCertName(e.target.value)} placeholder="Tên cá nhân / cơ quan"/></label></div>
      <label className="form-check"><input type="checkbox" checked={testCert} onChange={e=>setTestCert(e.target.checked)}/>
        Tạo chứng thư tự ký để thử nghiệm (không được coi là chữ ký số được CA công nhận)</label>
      {!testCert&&<><label>Nhập chứng thư PFX/P12<input type="file" accept=".pfx,.p12" onChange={e=>setPfxFile(e.target.files?.[0])}/></label>
        <label>Mật khẩu tệp PFX<input type="password" value={pfxPassword} onChange={e=>setPfxPassword(e.target.value)}/></label></>}
      <label>Mật khẩu tài khoản để ký / nhập chứng thư<input type="password" value={accountPassword} onChange={e=>setAccountPassword(e.target.value)}/></label>
      <button className="secondary small" disabled={busy||certName.trim().length<3||
        !isAllowed(actor,certType==='CA_NHAN'?'signatures.manage-own':'signatures.manage-agency')}
        onClick={createCredential}>Lưu chứng thư</button></div>
    <div className="form-section"><h4>Các bước ký và phát hành</h4>
      {doc.status==='DA_CAP_SO'&&isAllowed(actor,'signing.prepare')&&<button className="primary small" disabled={busy}
        onClick={()=>operation('prepare')}>Chuẩn bị PDF và chèn số văn bản</button>}
      {doc.status==='CHO_KY'&&isAllowed(actor,'signing.sign')&&<div className="signature-step-actions">
        <button className="secondary small" disabled={busy||signatures.length!==0} onClick={()=>operation('sign','KY_NHAY')}>1. Ký nháy</button>
        <button className="secondary small" disabled={busy||signatures.length!==1} onClick={()=>operation('sign','KY_LANH_DAO')}>2. Lãnh đạo ký</button>
        <button className="secondary small" disabled={busy||signatures.length!==2} onClick={()=>operation('sign','KY_CO_QUAN')}>3. Cơ quan ký</button></div>}
      {doc.status==='DA_KY'&&isAllowed(actor,'publications.issue')&&<button className="primary small" disabled={busy}
        onClick={()=>operation('publish')}>Phát hành đến các cơ quan dự kiến</button>}
      <div className="signature-step-actions"><button className="secondary small" disabled={busy} onClick={()=>operation('verify')}>Xác minh chữ ký</button>
        <button className="secondary small" disabled={busy} onClick={()=>operation('download')}>Tải PDF đã ký</button></div>
      {signatures.map((s,i)=><div className="signature-row" key={String(field(s,'TenTruongKy'))}>
        <div className="signature-order">{i+1}</div><span>{String(field(s,'TenTruongKy'))}</span>
        <span className="signature-state">{field(s,'ToanVenMatMa')?'Toàn vẹn mật mã':'Không hợp lệ'}</span></div>)}
      {message&&<div className="warning-box"><CircleAlert size={18}/><span>{message}</span></div>}
      <p className="fine-note">Kiểm tra mật mã không đồng nghĩa với xác thực pháp lý của nhà cung cấp CA; chứng thư tự ký chỉ để thử nghiệm.</p>
    </div>
  </section>;
}

function LiveDeliveries({token,direction,onUpdated}:{token:string;direction:'out'|'in';onUpdated:()=>Promise<void>}) {
  const [rows,setRows]=useState<Record<string,unknown>[]>([]);
  const [busy,setBusy]=useState('');const [error,setError]=useState('');
  const [expanded,setExpanded]=useState('');
  const [attachments,setAttachments]=useState<Record<string,Record<string,unknown>[]>>({});
  const [preview,setPreview]=useState<{path:string;name:string}|null>(null);
  const load=async()=>setRows(await requestApi(direction==='out'?'/deliveries':'/deliveries/inbox',token));
  useEffect(()=>{load().catch(err=>setError((err as Error).message));},[token,direction]);
  const perform=async(row:Record<string,unknown>,type:'deliver'|'accept'|'reject'|'pdf')=>{
    const id=String(field(row,'MaLuotChuyen'));
    let reason='';
    if(type==='reject'){
      const answer=window.prompt('Lý do từ chối tiếp nhận:', '');
      if(!answer?.trim())return;reason=answer.trim();
    }
    setBusy(id);setError('');
    try {
      if(type==='pdf')await downloadApi(`/deliveries/${id}/pdf`,token,`van-ban-phat-hanh-${id}.pdf`);
      else {
        await requestApi(`/deliveries/${id}/${type==='deliver'?'deliver':type}`,token,
          {method:'POST',body:JSON.stringify({maYeuCau:crypto.randomUUID(),lyDo:type==='reject'?reason:null})});
        await load();await onUpdated();
      }
    }catch(err){setError((err as Error).message);}finally{setBusy('');}
  };
  const showAttachments=async(id:string)=>{
    if(expanded===id){setExpanded('');return;}
    setError('');setBusy(id);
    try{const found=await requestApi(`/deliveries/${id}/attachments`,token);
      setAttachments(previous=>({...previous,[id]:found}));setExpanded(id);
    }catch(err){setError((err as Error).message);}finally{setBusy('');}
  };
  return <div className="panel deliveries-panel live-deliveries">
    <div className="panel-head"><div><h3>{direction==='out'?'Sổ chuyển phát đi':'Hộp nhận văn bản đã phát hành'}</h3>
      <p>{rows.length} lượt chuyển phát của cơ quan</p></div><button className="secondary small" onClick={()=>load().catch(err=>setError((err as Error).message))}><RefreshCw size={15}/> Tải lại</button></div>
    {error&&<div className="form-error"><CircleAlert size={17}/>{error}</div>}
    {rows.map(row=>{
      const id=String(field(row,'MaLuotChuyen'));
      const status=String(field(row,'TrangThai')) as ChuyenPhat['status'];
      return <div className="delivery-live-row" key={id}>
        <div className="delivery-live-main"><div className="eyebrow">{String(field(row,direction==='out'?'CoQuanNhan':'CoQuanGui'))}</div>
          <strong>{String(field(row,'TieuDe'))}</strong><small>{String(field(row,'MaNoiBo')??field(row,'KyHieu')??'Bản đã phát hành')}
            {' · Gửi: '}{formatDate(String(field(row,'ThoiDiemGui')??''))}</small></div>
        <Status status={status} small/>
        <div className="delivery-live-actions">
          <button className="secondary small" disabled={busy===id} onClick={()=>perform(row,'pdf')}>Tải PDF</button>
          {status!=='CHO_GUI'&&<button className="secondary small" disabled={busy===id} onClick={()=>showAttachments(id)}>
            <Files size={14}/> {expanded===id?'Ẩn tệp':'Tệp đính kèm'}</button>}
          {direction==='out'&&status==='CHO_GUI'&&<button className="primary small" disabled={busy===id} onClick={()=>perform(row,'deliver')}>
            <Send size={15}/> Gửi văn bản</button>}
          {direction==='in'&&status==='DA_GUI'&&<><button className="primary small" disabled={busy===id} onClick={()=>perform(row,'accept')}>
            <Check size={15}/> Tiếp nhận</button><button className="secondary small" disabled={busy===id} onClick={()=>perform(row,'reject')}>Từ chối</button></>}
        </div>
        {expanded===id&&<div className="attachment-list delivery-attachments">
          {(attachments[id]??[]).map(file=>{const fileId=String(field(file,'MaTep'));const filename=String(field(file,'TenTep'));
            return <div className="attachment-entry" key={fileId}><FileText size={16}/><span>{filename}</span>
              <button className="secondary small" onClick={()=>setPreview({path:`/deliveries/${id}/attachments/${fileId}`,name:filename})}>Xem trước</button>
              <button className="secondary small" onClick={()=>downloadApi(`/deliveries/${id}/attachments/${fileId}`,token,filename).catch(err=>setError((err as Error).message))}>Tải về</button></div>;})}
          {(attachments[id]??[]).length===0&&<p className="subtle">Không có tệp đính kèm trong bản phát hành.</p>}
        </div>}
        {Boolean(field(row,'LyDoTuChoi'))&&<p className="transfer-note">Lý do từ chối: {String(field(row,'LyDoTuChoi'))}</p>}
      </div>;
    })}
    {rows.length===0&&<EmptyView icon={Inbox} title="Chưa có lượt chuyển" message={direction==='in'?'Cơ quan gửi chưa chuyển văn bản nào đến bạn.':'Hãy phát hành một văn bản đã ký để tạo lượt chuyển.'}/>}
    {preview&&<FilePreview path={preview.path} filename={preview.name} token={token} onClose={()=>setPreview(null)}/>}
  </div>;
}

function DraftAttachmentEditor({doc,actor,token,onUpdated}:{doc:VanBan;actor:Actor|null;token:string;onUpdated:()=>Promise<void>}) {
  const [attachments,setAttachments]=useState<Record<string,unknown>[]>([]);
  const [versionToken,setVersionToken]=useState('');
  const [excerpt,setExcerpt]=useState(doc.excerpt);
  const [removed,setRemoved]=useState<string[]>([]);
  const [newFiles,setNewFiles]=useState<File[]>([]);
  const [newMain,setNewMain]=useState<File|null>(null);
  const [preview,setPreview]=useState<{path:string;name:string}|null>(null);
  const [reason,setReason]=useState('');
  const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [message,setMessage]=useState('');
  const editable=isAllowed(actor,'documents.manage')&&['NHAP','TRA_SUA'].includes(doc.status)&&doc.creator===actor?.name;
  const load=async()=>{
    const [details,files]=await Promise.all([
      requestApi(`/documents/${doc.id}`,token),requestApi(`/documents/${doc.id}/attachments`,token)
    ]);
    setAttachments(files);setVersionToken(String(field(details,'PhienBanDong')??''));
    setExcerpt(String(field(details,'TrichYeu')??''));setRemoved([]);setNewFiles([]);setNewMain(null);
  };
  useEffect(()=>{load().catch(err=>setError((err as Error).message));},[doc.id,doc.version,token]);
  const save=async()=>{
    setBusy(true);setError('');setMessage('');
    try{
      if(!newMain&&!attachments.some(f=>Boolean(field(f,'LaVanBanChinh'))&&!removed.includes(String(field(f,'MaTep')))))
        throw new Error('Phải giữ lại hoặc chọn PDF văn bản chính.');
      const form=new FormData();form.set('phienBanDong',versionToken);form.set('trichYeu',excerpt);form.set('lyDo',reason);
      for(const id of removed)form.append('maTepLoaiBo',id);
      if(newMain)form.set('tepVanBanChinh',newMain);
      for(const file of newFiles)form.append('tepDinhKem',file);
      await requestApi(`/documents/${doc.id}/draft-files`,token,{method:'POST',body:form});
      setReason('');await onUpdated();await load();setMessage('Đã lưu phiên bản mới. Tên văn bản vẫn giữ nguyên.');
    }catch(err){setError((err as Error).message);}finally{setBusy(false);}
  };
  return <div className="draft-files-editor">
    <h3>Tệp đính kèm hiện tại</h3>
    <p className="subtle">Word, Excel và PDF được giữ cùng văn bản. Tên văn bản «{doc.title}» không thể sửa sau khi tạo.</p>
    {([true,false] as const).map(isMain=><div key={String(isMain)}>
      <h4>{isMain?'Văn bản chính — PDF dùng để ký':'Tệp đính kèm — Word / Excel / PDF'}</h4>
      <div className="attachment-list">
      {attachments.filter(file=>Boolean(field(file,'LaVanBanChinh'))===isMain).map(file=>{const id=String(field(file,'MaTep'));const filename=String(field(file,'TenTep'));
        return <div className="attachment-entry" key={id}><FileText size={17}/><span className={removed.includes(id)?'marked-removal':''}>{filename}{Boolean(field(file,'LaVanBanChinh'))?' · Văn bản chính':''}</span>
          <button className="secondary small" onClick={()=>setPreview({path:`/documents/${doc.id}/files/${id}`,name:filename})}>Xem trước</button>
          <button className="secondary small" onClick={()=>downloadApi(`/documents/${doc.id}/files/${id}`,token,filename).catch(err=>setError((err as Error).message))}>Tải về</button>
          {editable&&<label className="remove-file"><input type="checkbox" checked={removed.includes(id)}
            onChange={e=>setRemoved(old=>e.target.checked?[...old,id]:old.filter(x=>x!==id))}/> Bỏ ở bản mới</label>}
        </div>;})}
      {!attachments.some(file=>Boolean(field(file,'LaVanBanChinh'))===isMain)&&<p className="subtle">Chưa có tệp ở mục này.</p>}
      </div>
    </div>)}
    {editable&&<div className="draft-edit-controls">
      <label>Trích yếu (có thể sửa)</label><textarea rows={3} value={excerpt} onChange={e=>setExcerpt(e.target.value)}/>
      <label>Thay văn bản chính (PDF)</label><input type="file" accept=".pdf,application/pdf"
        onChange={e=>setNewMain(e.target.files?.[0]??null)}/>
      {newMain&&<p className="subtle">PDF chính mới: {newMain.name}. Hãy đánh dấu bỏ PDF chính cũ.</p>}
      <label>Thêm tệp đính kèm (Word / Excel / PDF)</label><input type="file" multiple accept=".pdf,.doc,.docx,.xls,.xlsx"
        onChange={e=>setNewFiles(Array.from(e.target.files??[]))}/>
      {newFiles.length>0&&<p className="subtle">Tệp mới: {newFiles.map(f=>f.name).join(', ')}</p>}
      <label>Lý do cập nhật</label><input value={reason} maxLength={1000} onChange={e=>setReason(e.target.value)} placeholder="Ví dụ: thay bảng tính dự toán"/>
      <button className="primary small" disabled={busy||!versionToken} onClick={save}>{busy?'Đang lưu...':'Lưu phiên bản mới'}<ArrowRight size={15}/></button>
      <p className="fine-note">Muốn thay tệp: đánh dấu bỏ tệp cũ, chọn tệp mới, rồi lưu. Các phiên bản cũ được bảo toàn.</p>
    </div>}
    {!editable&&<p className="fine-note">Chỉ người tạo được thay đổi tệp khi văn bản còn ở trạng thái nháp hoặc trả sửa.</p>}
    {error&&<div className="form-error">{error}</div>}{message&&<p className="success-note">{message}</p>}
    {preview&&<FilePreview path={preview.path} filename={preview.name} token={token} onClose={()=>setPreview(null)}/>}
  </div>;
}

function DocumentDetails({doc,onClose,onAction,allowed,canDecide,liveTimeline,token,onUpdated}:
  {doc:VanBan,onClose:()=>void,onAction:(action:'submit'|'approve'|'revise'|'register')=>void,allowed:Actor|null,canDecide:boolean,
    liveTimeline?:{ events:SuKien[];transfers:ChuyenPhat[] };token:string;onUpdated:()=>Promise<void>}) {
  const [tab,setTab]=useState<'journey'|'info'|'versions'|'signatures'>('journey');
  const viewDoc=liveTimeline?{...doc,events:liveTimeline.events,transfers:liveTimeline.transfers}:doc;
  return <div className="drawer-backdrop" onClick={onClose}>
    <div className="drawer" role="dialog" aria-modal="true" aria-label="Chi tiết văn bản" onClick={e=>e.stopPropagation()}>
      <div className="drawer-head"><div><div className="eyebrow">CHI TIẾT VĂN BẢN</div><div className="drawer-code">{doc.code}</div></div>
        <button className="icon-button" aria-label="Đóng" onClick={onClose}><X size={20}/></button></div>
      <div className="drawer-body"><h2>{doc.title}</h2><div className="drawer-pills"><Status status={doc.status}/>
        {doc.urgency!=='THUONG'&&<span className="priority-pill">{doc.urgency==='HOA_TOC'?'Hỏa tốc':'Khẩn'}</span>}
        <span className="neutral-pill">Phiên bản {doc.version}</span></div>
      <div className="drawer-actions">
        {doc.status==='NHAP' && isAllowed(allowed,'documents.submit') && <button className="primary small" onClick={()=>onAction('submit')}>Trình duyệt<ArrowRight size={15}/></button>}
        {doc.status==='CHO_DUYET' && canDecide && isAllowed(allowed,'workflow.decide') && <><button className="primary small" onClick={()=>onAction('approve')}>Phê duyệt<Check size={15}/></button><button className="secondary small" onClick={()=>onAction('revise')}>Trả sửa</button></>}
        {doc.status==='CHO_CAP_SO' && isAllowed(allowed,'registers.manage') && <button className="primary small" onClick={()=>onAction('register')}>Cấp số văn bản <ArrowRight size={15}/></button>}
      </div>
      <div className="drawer-tabs">
        {([['journey','Hành trình'],['info','Thông tin'],['versions','Phiên bản'],['signatures','Chữ ký số']] as const).map(([key,text])=><button key={key}
          className={tab===key?'selected':''} onClick={()=>setTab(key)}>{text}</button>)}
      </div>
      {tab==='journey' && <>
        <section className="drawer-section"><div className="section-label">TIẾN ĐỘ XỬ LÝ</div><ProgressLine doc={doc}/></section>
        <section className="drawer-section"><div className="section-head"><h3>Hành trình luân chuyển</h3><span className="subtle">{viewDoc.events.length} sự kiện</span></div><Timeline doc={viewDoc}/></section>
        <section className="drawer-section"><div className="section-head"><h3>Nơi nhận & biên nhận</h3><span className="subtle">{viewDoc.transfers.length} cơ quan</span></div><TransferCards doc={viewDoc}/></section>
      </>}
      {tab==='info' && <section className="drawer-section"><div className="info-grid">
        <span>Mã quản lý</span><strong>{doc.code}</strong><span>Số / ký hiệu</span><strong>{doc.number||'Chưa cấp số'}</strong>
        <span>Cơ quan</span><strong>{doc.agency}</strong><span>Phòng ban</span><strong>{doc.department||'—'}</strong>
        <span>Người tạo</span><strong>{doc.creator}</strong><span>Người phụ trách</span><strong>{doc.assignee}</strong>
        <span>Ngày tạo</span><strong>{formatDate(doc.createdAt)}</strong><span>Hạn xử lý</span><strong>{formatDate(doc.dueAt)}</strong>
        <span>Độ mật</span><strong>{doc.secrecy==='MAT'?'Mật':'Thường'}</strong><span>Độ khẩn</span><strong>{doc.urgency==='THUONG'?'Thường':doc.urgency==='KHAN'?'Khẩn':'Hỏa tốc'}</strong>
      </div><h3 className="info-heading">Trích yếu nội dung</h3><p className="info-excerpt">{doc.excerpt}</p></section>}
      {tab==='versions' && <section className="drawer-section">
        {!DEMO&&<DraftAttachmentEditor doc={doc} actor={allowed} token={token} onUpdated={onUpdated}/>}
        <h3>Lịch sử phiên bản</h3><p className="subtle">Các bản đã nộp được giữ riêng, không ghi đè.</p>
        {Array.from({length:doc.version},(_,idx)=>doc.version-idx).map(v=><div className="version-row" key={v}>
          <div className="version-icon"><Files size={18}/></div><div><strong>Phiên bản {v}</strong><span>{v===doc.version?'Đang sử dụng':'Bản lịch sử'}</span></div>
          <span className="version-chip">v{v}</span></div>)}
        {DEMO&&<p className="fine-note">Dữ liệu mô phỏng. Bản thật lưu các tệp PDF, Word, Excel theo từng phiên bản ở máy chủ.</p>}
      </section>}
      {tab==='signatures' && !DEMO && <SignaturePanel doc={doc} actor={allowed} token={token} onUpdated={onUpdated}/>}
      {tab==='signatures' && DEMO && <section className="drawer-section">
        <div className="signature-header"><div className="signature-icon"><ShieldCheck size={30}/></div>
          <h3>Kiểm tra chữ ký số</h3><p>Phân biệt chữ ký mật mã học và ảnh hiển thị chữ ký.</p></div>
        {['Trưởng phòng – ký nháy','Lãnh đạo – ký ban hành','Văn thư – ký cơ quan'].map((label,i)=><div className="signature-row" key={label}>
          <div className="signature-order">{i+1}</div><span>{label}</span><span className="signature-state">Chưa có kết quả xác minh thật</span></div>)}
        <div className="warning-box"><LockKeyhole size={18}/><span>
          Chức năng ký PDF/CMS và xác minh chứng thư thật chưa được tích hợp trong bản đầu. Các mốc ký của dữ liệu mẫu chỉ là minh họa; không phải bằng chứng chữ ký số hợp lệ.
        </span></div>
      </section>}
      </div>
    </div>
  </div>;
}

function NewDocument({onClose,onCreate,live,recipients}:{onClose:()=>void,onCreate:(input:{title:string;excerpt:string;to:string[];urgency:VanBan['urgency'];mainFile:File;files:File[]})=>Promise<void>,live:boolean,recipients:{value:string;label:string}[]}) {
  const [title,setTitle]=useState('');const [excerpt,setExcerpt]=useState('');
  const [to,setTo]=useState<string[]>([]);const [urgency,setUrgency]=useState<VanBan['urgency']>('THUONG');
  const [mainFile,setMainFile]=useState<File|null>(null);
  const [files,setFiles]=useState<File[]>([]);const [busy,setBusy]=useState(false);const [error,setError]=useState('');
  const submit=async(e:FormEvent)=>{e.preventDefault();setBusy(true);setError('');
    try{if(to.length===0)throw new Error('Chọn ít nhất một cơ quan nhận.');
      if(!mainFile)throw new Error('Chọn văn bản chính dạng PDF để ký số.');
      if(!mainFile.name.toLowerCase().endsWith('.pdf'))throw new Error('Văn bản chính phải là PDF.');
      await onCreate({title,excerpt,to,urgency,mainFile,files});onClose();}catch(err){setError((err as Error).message);}finally{setBusy(false);}};
  return <div className="modal-backdrop" onClick={onClose}><form className="create-modal" onClick={e=>e.stopPropagation()} onSubmit={submit}>
    <div className="modal-header"><div><div className="eyebrow">VĂN BẢN ĐI</div><h2>Tạo văn bản mới</h2></div><button type="button" className="icon-button" onClick={onClose}><X size={20}/></button></div>
    <div className="modal-content"><label>Tiêu đề văn bản <span>*</span></label><input required minLength={3} maxLength={300} placeholder="Nhập tên văn bản..." value={title} onChange={e=>setTitle(e.target.value)}/>
      <label>Trích yếu nội dung</label><textarea placeholder="Mô tả ngắn nội dung văn bản..." value={excerpt} onChange={e=>setExcerpt(e.target.value)} rows={4}/>
      <div className="modal-grid"><div><label>Cơ quan nhận dự kiến (có thể chọn nhiều)</label><div className="recipient-picks">
        {recipients.map(r=><label key={r.value}><input type="checkbox" checked={to.includes(r.value)}
          onChange={e=>setTo(prev=>e.target.checked?[...prev,r.value]:prev.filter(x=>x!==r.value))}/>{r.label}</label>)}
        {recipients.length===0&&<span className="subtle">Chưa có cơ quan nhận được cấu hình.</span>}
        </div></div>
        <div><label>Độ khẩn</label><select value={urgency} onChange={e=>setUrgency(e.target.value as VanBan['urgency'])}><option value="THUONG">Thường</option><option value="KHAN">Khẩn</option><option value="HOA_TOC">Hỏa tốc</option></select></div></div>
      <div className="upload-part"><label>1. Văn bản chính — PDF dùng để ký <span>*</span></label>
        <label className="file-upload"><CloudUpload size={22}/><span>{mainFile?.name??'Chọn một tệp PDF chính'}</span><small>Bản này sẽ được phê duyệt và ký số</small>
          <input type="file" accept=".pdf,application/pdf" required onChange={e=>setMainFile(e.target.files?.[0]??null)}/></label></div>
      <div className="upload-part"><label>2. Tệp đính kèm — Word / Excel / PDF (không bắt buộc)</label>
        <label className="file-upload"><CloudUpload size={22}/><span>{files.length?`${files.length} tệp: ${files.map(f=>f.name).join(', ')}`:'Chọn các tệp đi kèm văn bản chính'}</span><small>Tối đa 20 tệp tính cả PDF chính; 25 MB/tệp</small>
          <input type="file" multiple accept=".pdf,.doc,.docx,.xls,.xlsx" onChange={e=>setFiles(Array.from(e.target.files??[]))}/></label></div>
      <p className="fine-note">Trong lúc soạn có thể thay PDF chính hoặc thêm/bỏ tệp đính kèm. Tiêu đề đã tạo không được đổi.</p>
      {error&&<div className="form-error">{error}</div>}
    </div><div className="modal-footer"><button type="button" className="secondary" onClick={onClose}>Hủy</button><button className="primary" disabled={busy}>{busy?'Đang lưu...':'Tạo bản dự thảo'}<ArrowRight size={17}/></button></div>
  </form></div>;
}

function EmptyView({title,message,icon:Icon}:{title:string;message:string;icon:LucideIcon}) {
  return <div className="empty-view"><div className="empty-icon"><Icon size={28}/></div><h3>{title}</h3><p>{message}</p></div>;
}

export default function App() {
  const [actor,setActor]=useState<Actor|null>(null);
  const [token,setToken]=useState('');
  const [page,setPage]=useState<Trang>('tong-quan');
  const [docs,setDocs]=useState<VanBan[]>(defaultDocs);
  const [selected,setSelected]=useState<string|null>(null);
  const [journeyPick,setJourneyPick]=useState<string|null>('demo-001');
  const [createOpen,setCreateOpen]=useState(false);
  const [query,setQuery]=useState('');
  const [filter,setFilter]=useState('ALL');
  const [menuOpen,setMenuOpen]=useState(false);
  const [profileMenu,setProfileMenu]=useState(false);
  const [notice,setNotice]=useState('');
  const [remoteTimeline,setRemoteTimeline]=useState<{events:SuKien[];transfers:ChuyenPhat[]}|undefined>();
  const [journeyTimeline,setJourneyTimeline]=useState<{events:SuKien[];transfers:ChuyenPhat[]}|undefined>();
  const [workqueue,setWorkqueue]=useState<Record<string,unknown>[]>([]);
  const [agencyOptions,setAgencyOptions]=useState<{value:string;label:string}[]>(DEMO?[{value:'Cơ quan B',label:'Cơ quan B'},{value:'Cơ quan C',label:'Cơ quan C'}]:[]);
  const [error,setError]=useState('');

  useEffect(()=>{if(DEMO)localStorage.setItem(storedDocsKey,JSON.stringify(docs));},[docs]);

  const menuItems=useMemo(()=>navigation.flatMap(g=>g.items).filter(i=>isAllowed(actor,i.permission)),[actor]);
  const visibleDocs=useMemo(()=>getAccessibleDocs(docs,actor),[docs,actor]);
  const filtered=useMemo(()=>visibleDocs.filter(d=>{
    if(page==='van-ban-di'&&d.type!=='DI')return false;
    if(page==='van-ban-den'&&d.type!=='DEN')return false;
    if(!DEMO&&page==='hanh-trinh'&&d.type!=='DI')return false;
    if(filter!=='ALL'&&d.status!==filter)return false;
    const term=query.toLocaleLowerCase('vi-VN').trim();
    return !term || `${d.code} ${d.title} ${d.number||''}`.toLocaleLowerCase('vi-VN').includes(term);
  }).sort((a,b)=>new Date(b.createdAt).getTime()-new Date(a.createdAt).getTime()),[visibleDocs,page,filter,query]);
  const currentDoc=visibleDocs.find(d=>d.id===selected);
  const fetchTimeline=async(d:VanBan)=>{
    const resp=await requestApi(`/documents/${d.id}/timeline`,token);
    const transferRows:ChuyenPhat[]=(resp.deliveries||[]).map((t:Record<string,unknown>)=>({
      id:String(field(t,'MaLuotChuyen')),agency:String(field(t,'CoQuanNhan')),contact:'Văn thư',
      status:String(field(t,'TrangThai')) as ChuyenPhat['status'],
      sentAt:field(t,'ThoiDiemGui')?String(field(t,'ThoiDiemGui')):undefined,
      receivedAt:field(t,'ThoiDiemNhan')?String(field(t,'ThoiDiemNhan')):undefined
    }));
    for(const recipient of (resp.planned||[]) as Record<string,unknown>[]){
      const agency=String(field(recipient,'TenCoQuan'));
      if(!transferRows.some(t=>t.agency===agency))transferRows.push({
        id:`planned-${field(recipient,'MaCoQuan')}`,agency,contact:'Dự kiến gửi sau khi ký',status:'CHO_GUI'
      });
    }
    return {
          events:(resp.events||[]).map((e:Record<string,unknown>)=>({
            id:String(field(e,'MaLichSu')),action:String(field(e,'HanhDong')),
            title:String(field(e,'HanhDong')).replace(/_/g,' '),
            detail:String(field(e,'MoTa')),by:String(field(e,'NguoiThucHien')??'Hệ thống'),agency:String(field(e,'TenCoQuan')),
            time:String(field(e,'ThoiDiem')),state:'done' as const,version:Number(field(e,'SoPhienBan')||1)
          })),
          transfers:transferRows
    };
  };
  useEffect(()=>{
    if(DEMO||!token||page!=='hanh-trinh')return;
    const chosen=visibleDocs.find(d=>d.id===journeyPick)??visibleDocs.find(d=>d.type==='DI');
    if(!chosen)return;
    if(journeyPick!==chosen.id)setJourneyPick(chosen.id);
    fetchTimeline(chosen).then(setJourneyTimeline).catch(err=>setError((err as Error).message));
  },[page,token,journeyPick,visibleDocs]);
  const openDoc=async(d:VanBan)=>{
    setSelected(d.id);setRemoteTimeline(undefined);
    if(!DEMO&&token){
      try{setRemoteTimeline(await fetchTimeline(d));}catch(err){setError((err as Error).message);}
    }
  };
  const pickJourney=async(d:VanBan)=>{
    setJourneyPick(d.id);setJourneyTimeline(undefined);
    if(!DEMO&&token){
      try{setJourneyTimeline(await fetchTimeline(d));}catch(err){setError((err as Error).message);}
    }
  };

  const refresh=async(authToken:string)=>{
    const [documents,tasks,incoming]=await Promise.all([
      requestApi('/documents',authToken),
      requestApi('/workqueue',authToken).catch(()=>[]),
      requestApi('/incoming',authToken).catch(()=>[])
    ]);
    const inbound=(incoming as Record<string,unknown>[]).map(d=>({
      id:String(field(d,'MaVanBanDen')),code:`DEN-${field(d,'SoDen')??'CHO_DANG_KY'}`,
      title:String(field(d,'TieuDe')),excerpt:'Bản phát hành đã tiếp nhận',type:'DEN' as const,
      status:'DANG_CHUYEN' as const,urgency:'THUONG' as const,secrecy:'THUONG' as const,
      agency:'Cơ quan của tôi',department:'Văn thư',creator:String(field(d,'CoQuanGui')??'Bên ngoài'),
      assignee:'Văn thư',createdAt:String(field(d,'ThoiDiemTiepNhan')),version:1,to:[],events:[],transfers:[]
    }));
    setDocs([...(documents as Record<string,unknown>[]).map(adaptLiveDocument),...inbound]);
    setWorkqueue(tasks as Record<string,unknown>[]);
  };
  const login=async(name:string,password:string)=>{
    if(DEMO){
      if(password!=='Password123!'||!demoUsers[name]) throw new Error('Sai tài khoản hoặc mật khẩu demo.');
      const u=demoUsers[name];setActor(u);setPage(u.capabilities.includes('documents.read')?'tong-quan':'quan-tri');setFilter('ALL');setQuery('');return;
    }
    const res=await fetch('/api/v1/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({tenDangNhap:name,matKhau:password})});
    if(!res.ok)throw new Error(res.status===423?'Tài khoản đang bị khóa.':'Không thể đăng nhập, hãy kiểm tra tài khoản.');
    const body=await res.json();const caps=body.capabilities as string[];
    const liveActor:Actor={username:name,name:body.user.hoTen??body.user.HoTen,agency:'Cơ quan của tôi',position:'Người dùng',capabilities:caps};
    setActor(liveActor);setToken(body.accessToken);
    const start=navigation.flatMap(g=>g.items).find(i=>caps.includes(i.permission));
    setPage(start?.id??'quan-tri');
    try{
      const agencies=await requestApi('/agencies/recipients',body.accessToken) as Record<string,unknown>[];
      setAgencyOptions(agencies.map(a=>({value:String(field(a,'MaCoQuan')),label:String(field(a,'TenCoQuan'))})));
    }catch{setAgencyOptions([]);}
    if(caps.includes('documents.read'))await refresh(body.accessToken);
  };

  const logout=()=>{setActor(null);setToken('');setSelected(null);setQuery('');setProfileMenu(false);setError('');};
  const go=(next:Trang)=>{if(!menuItems.find(i=>i.id===next))return;setPage(next);setSelected(null);setFilter('ALL');setQuery('');setMenuOpen(false);};
  const create=async(input:{title:string;excerpt:string;to:string[];urgency:VanBan['urgency'];mainFile:File;files:File[]})=>{
    if(!actor)return;
    if(DEMO){
      const now=new Date().toISOString();const number=Math.floor(Math.random()*9000+1000);
      const document:VanBan={id:crypto.randomUUID(),code:`VB-${new Date().getFullYear()}-${number}`,title:input.title,excerpt:input.excerpt,
        type:'DI',status:'NHAP',urgency:input.urgency,secrecy:'THUONG',agency:actor.agency,department:'Văn thư',creator:actor.name,
        assignee:actor.name,createdAt:now,version:1,to:input.to,events:[{id:crypto.randomUUID(),action:'TAO_MOI',title:'Tạo bản dự thảo',
          detail:'Phiên bản 1 đã được lưu',by:actor.name,agency:actor.agency,time:now,state:'done',version:1}],transfers:[]};
      setDocs(prev=>[document,...prev]);setPage('van-ban-di');setNotice('Đã tạo bản dự thảo mô phỏng.');return;
    }
    const form=new FormData();form.set('tieuDe',input.title);form.set('trichYeu',input.excerpt);
    form.set('doMat','THUONG');form.set('doKhan',input.urgency);
    for(const recipient of input.to)form.append('maCoQuanNhan',recipient);
    form.set('tepVanBanChinh',input.mainFile);
    for(const file of input.files)form.append('tepDinhKem',file);
    await requestApi('/documents',token,{method:'POST',body:form});
    await refresh(token);setPage('van-ban-di');setNotice('Đã tạo văn bản và lưu các tệp đính kèm trên máy chủ.');
  };
  const action=async(kind:'submit'|'approve'|'revise'|'register')=>{
    if(!actor||!currentDoc)return;
    const reason=kind==='revise'?window.prompt('Nhập lý do trả sửa (bắt buộc):',''):null;
    if(kind==='revise'&&(!reason || !reason.trim()))return;
    const symbol=kind==='register'?window.prompt('Nhập ký hiệu văn bản (ví dụ KH-CQA):','KH-CQA'):null;
    if(kind==='register'&&(!symbol || !symbol.trim()))return;
    if(!DEMO){
      try{
        if(kind==='submit')await requestApi(`/documents/${currentDoc.id}/submit`,token,{method:'POST',body:JSON.stringify({maYeuCau:crypto.randomUUID()})});
        else if(kind==='register')await requestApi('/registrations',token,{method:'POST',body:JSON.stringify({maVanBan:currentDoc.id,maYeuCau:crypto.randomUUID(),kyHieu:symbol})});
        else{
          const job=workqueue.find(t=>String(field(t,'MaVanBan'))===currentDoc.id);
          if(!job)throw new Error('Không tìm thấy nhiệm vụ đang giao cho tài khoản.');
          await requestApi(`/workflow/tasks/${field(job,'MaTacVu')}/decisions`,token,{method:'POST',body:JSON.stringify({ketQua:kind==='approve'?'DUYET':'TRA_SUA',lyDo:reason,maYeuCau:crypto.randomUUID()})});
        }
        await refresh(token);setSelected(null);setNotice('Đã ghi nhận thao tác trên máy chủ.');
      }catch(err){setError((err as Error).message);}
      return;
    }
    const when=new Date().toISOString();
    const step=actor.username==='truongphong_a'?'Lê Thu Hà':actor.username==='truongvanphong_a'?'Phạm Văn Nam':'Nguyễn Thị Minh';
    const state:TrangThai=kind==='submit'?'CHO_DUYET':kind==='register'?'DA_CAP_SO':kind==='approve'&&actor.username!=='lanhdao_a'?'CHO_DUYET':kind==='approve'?'CHO_CAP_SO':'TRA_SUA';
    const labels={submit:'Trình duyệt văn bản',approve:'Phê duyệt và chuyển bước',revise:'Trả lại chỉnh sửa',register:'Cấp số văn bản'};
    const event:SuKien={id:crypto.randomUUID(),action:kind.toUpperCase(),title:labels[kind],
      detail:kind==='revise'?`Lý do: ${reason}`:kind==='register'?`Đã cấp số /${symbol} (mô phỏng)`:kind==='approve'?`Chuyển bước tiếp theo đến ${step}`:'Đã chuyển trưởng phòng kiểm tra',
      by:actor.name,agency:actor.agency,time:when,state:kind==='revise'?'warning':'done'};
    setDocs(prev=>prev.map(d=>d.id===currentDoc.id?{...d,status:state,
      number:kind==='register'?`129/${symbol}`:d.number,
      assignee:kind==='submit'?'Trần Văn Hùng':kind==='approve'?step:kind==='register'?'Nguyễn Thị Minh':d.creator,
      events:[...d.events,event]}:d));
    setNotice('Đã cập nhật hành trình trong chế độ mô phỏng.');
  };

  const updateAfterOperation=async()=>{
    await refresh(token);
    const selectedDoc=visibleDocs.find(d=>d.id===selected);
    if(selectedDoc){
      try {setRemoteTimeline(await fetchTimeline(selectedDoc));}catch(err){setError((err as Error).message);}
    }
    const journeyDoc=visibleDocs.find(d=>d.id===journeyPick);
    if(journeyDoc){
      try {setJourneyTimeline(await fetchTimeline(journeyDoc));}catch{/* user can reload timeline */}
    }
  };

  if(!actor)return <Login onLogin={login}/>;
  const hasDocs=isAllowed(actor,'documents.read');
  const urgent=visibleDocs.filter(d=>d.urgency!=='THUONG'&&!['HOAN_THANH'].includes(d.status));
  const waiting=visibleDocs.filter(d=>d.status==='CHO_DUYET').length;
  const signing=visibleDocs.filter(d=>d.status==='CHO_KY'||d.status==='DA_CAP_SO').length;
  const ongoing=visibleDocs.filter(d=>d.status==='DANG_CHUYEN').length;
  const totalTransfer=visibleDocs.reduce((s,d)=>s+d.transfers.length,0);

  const documentList = (heading:string,showFilter=true)=> <div className="panel documents-panel">
    <div className="panel-head"><div><h3>{heading}</h3><p>{filtered.length} văn bản trong phạm vi truy cập</p></div>
      <div className="panel-filters">{showFilter&&<div className="filter-select"><Filter size={16}/><select aria-label="Lọc trạng thái" value={filter} onChange={e=>setFilter(e.target.value)}>
        <option value="ALL">Tất cả trạng thái</option>{Object.entries(stageLabel).map(([id,label])=><option key={id} value={id}>{label}</option>)}
      </select><ChevronDown size={15}/></div>}</div>
    </div>
    <div className="table-scroll"><table className="doc-table"><thead><tr><th>VĂN BẢN</th><th>NGÀY TẠO</th><th>TRẠNG THÁI</th><th>NGƯỜI XỬ LÝ</th><th></th></tr></thead>
      <tbody>{filtered.map(d=><tr key={d.id} onClick={()=>openDoc(d)} tabIndex={0} onKeyDown={e=>{if(e.key==='Enter')openDoc(d);}}>
        <td><div className="doc-cell"><div className={`file-icon ${d.type==='DEN'?'incoming':''}`}><FileText size={21}/></div>
          <div className="doc-title"><strong>{d.title}</strong><span>{d.number||d.code}{d.urgency!=='THUONG'&&<b className="urgent-label">Khẩn</b>}</span></div></div></td>
        <td className="muted-cell">{formatDate(d.createdAt).slice(0,10)}</td><td><Status status={d.status} small/></td>
        <td><div className="assignee"><span className="tiny-avatar">{initials(d.assignee)}</span><span>{d.assignee}</span></div></td>
        <td><button className="row-open" aria-label={`Xem ${d.title}`}><ChevronRight size={18}/></button></td></tr>)}</tbody></table>
      {filtered.length===0&&<EmptyView icon={Files} title="Chưa có văn bản" message="Không tìm thấy văn bản phù hợp với bộ lọc hiện tại."/>}</div>
    <div className="table-footer"><span>Hiển thị {filtered.length} kết quả</span><span>Chọn một văn bản để xem hành trình <ArrowUpRight size={14}/></span></div>
  </div>;

  return <div className="app-shell">
    <aside className={`sidebar ${menuOpen?'show':''}`}>
      <div className="brand"><div className="brand-symbol"><Files size={25}/></div><div><strong>VĂN THƯ SỐ</strong><span>QUẢN LÝ VĂN BẢN</span></div></div>
      <div className="agency-card"><div className="agency-icon"><Building2 size={19}/></div><div><span>Đơn vị đang làm việc</span><strong>{actor.agency}</strong></div><ChevronDown size={15}/></div>
      <nav>{navigation.map(group=>{
        const items=group.items.filter(i=>isAllowed(actor,i.permission));
        return items.length>0&&<div className="nav-group" key={group.group}><div className="nav-heading">{group.group}</div>
          {items.map(item=><button key={item.id} className={`nav-item ${page===item.id?'active':''}`} onClick={()=>go(item.id)}>
            <item.icon size={20} strokeWidth={1.85}/><span>{item.label}</span>{page===item.id&&<span className="nav-active-bar"/>}</button>)}
        </div>;
      })}</nav>
      <div className="sidebar-bottom"><div className="support-card"><div className="support-icon"><ShieldCheck size={20}/></div><strong>Quyền truy cập an toàn</strong>
        <p>Văn bản được bảo vệ theo cơ quan và vai trò người dùng.</p></div>
        <div className="sidebar-version">Phiên bản nền tảng 0.2 <span>·</span> 2026</div>
      </div>
    </aside>
    {menuOpen&&<button className="mobile-scrim" aria-label="Đóng menu" onClick={()=>setMenuOpen(false)}/>}
    <main className="main"><header className="topbar">
      <button className="icon-button hamburger" aria-label="Mở menu" onClick={()=>setMenuOpen(true)}><Menu size={23}/></button>
      <div className="breadcrumb">Hệ thống <ChevronRight size={15}/> <strong>{names[page]}</strong></div>
      <div className="header-right">
        {hasDocs&&<div className="global-search"><Search size={19}/><input aria-label="Tìm kiếm văn bản" placeholder="Tìm văn bản, số ký hiệu..." value={query} onChange={e=>setQuery(e.target.value)}/>
          <span>⌘ K</span></div>}
        <button className="icon-button notification-button" title="Thông báo" onClick={()=>setNotice('Thông báo công việc sẽ được cập nhật theo quyền truy cập.')}><Bell size={20}/><span className="notification-dot"/></button>
        <div className="header-divider"/><button className="user-button" onClick={()=>setProfileMenu(!profileMenu)}>
          <span className="avatar">{initials(actor.name)}</span><span className="user-name"><strong>{actor.name}</strong><small>{actor.position}</small></span><ChevronDown size={16}/></button>
        {profileMenu&&<div className="profile-menu"><div className="profile-menu-title">{actor.username}</div>
          {DEMO&&<><div className="profile-caption">Đổi tài khoản minh họa</div>{Object.values(demoUsers).map(u=><button key={u.username}
            onClick={()=>{setActor(u);setPage(u.capabilities.includes('documents.read')?'tong-quan':'quan-tri');setSelected(null);setProfileMenu(false);}}>
            <UserRound size={16}/>{u.username}</button>)}</>}
          <button className="logout" onClick={logout}><LogOut size={16}/>Đăng xuất</button></div>}
      </div>
    </header>
    {DEMO&&<div className="demo-banner"><CircleAlert size={16}/><span><strong>Chế độ minh họa:</strong> dữ liệu được lưu trong trình duyệt; các mốc ký số chưa phải chữ ký mật mã học.</span></div>}
    {error&&<div className="error-banner"><CircleAlert size={16}/>{error}<button onClick={()=>setError('')}><X size={15}/></button></div>}
    <div className="page-content">
      {page==='tong-quan'&&hasDocs&&<>
        <div className="page-intro"><div><div className="overline">{new Intl.DateTimeFormat('vi-VN',{weekday:'long',day:'numeric',month:'long',year:'numeric'}).format(new Date()).toLocaleUpperCase('vi-VN')}</div><h1>Xin chào, {actor.name.split(' ').at(-1)} <span className="wave">✳</span></h1>
          <p>Đây là tình hình xử lý văn bản của đơn vị bạn hôm nay.</p></div>
          {isAllowed(actor,'documents.manage')&&<button className="primary" onClick={()=>setCreateOpen(true)}><Plus size={19}/>Tạo văn bản mới</button>}</div>
        <div className="stat-grid">
          {[
            {name:'Tổng văn bản',value:visibleDocs.length,delta:'Đã ghi nhận',icon:Files,color:'navy'},
            {name:'Chờ phê duyệt',value:waiting,delta:'Cần theo dõi',icon:Clock3,color:'amber'},
            {name:'Chờ ký số',value:signing,delta:'Trong quy trình',icon:FileSignature,color:'indigo'},
            {name:'Đang luân chuyển',value:ongoing,delta:'Liên cơ quan',icon:Network,color:'green'}
          ].map(card=><div className="stat-card" key={card.name}><div className="stat-top"><span>{card.name}</span><span className={`stat-icon ${card.color}`}><card.icon size={22}/></span></div>
            <strong className="stat-number">{card.value.toString().padStart(2,'0')}</strong><div className="stat-bottom"><span className="small-indicator"/>{card.delta}</div></div>)}
        </div>
        <div className="dashboard-grid"><div className="dashboard-main">
          <div className="section-title"><div><h2>Văn bản gần đây</h2><p>Danh sách văn bản đang được xử lý trong đơn vị</p></div>
            <button className="text-button" onClick={()=>go('van-ban-di')}>Xem tất cả <ArrowRight size={17}/></button></div>
          {documentList('Danh sách mới cập nhật',false)}
        </div><div className="dashboard-side">
          <div className="panel quick-panel"><div className="quick-title"><div className="quick-icon"><Route size={21}/></div><div><h3>Theo dõi hành trình</h3><p>Tra cứu luồng xử lý chi tiết</p></div></div>
            <div className="quick-route"><span className="quick-step done"><Check size={13}/></span><div><strong>Soạn thảo & duyệt</strong><small>Theo dõi từng người xử lý</small></div></div>
            <div className="quick-route"><span className="quick-step done"><Check size={13}/></span><div><strong>Ký số & phát hành</strong><small>Ghi lại thời gian, phiên bản</small></div></div>
            <div className="quick-route"><span className="quick-step active"><Network size={13}/></span><div><strong>Gửi đến cơ quan nhận</strong><small>Biên nhận và trạng thái chuyển</small></div></div>
            <button className="quick-button" onClick={()=>go('hanh-trinh')}>Mở hành trình văn bản <ArrowRight size={16}/></button>
          </div>
          <div className="panel priority-panel"><div className="section-head"><h3>Ưu tiên xử lý</h3><span className="count-bubble">{urgent.length}</span></div>
            {urgent.slice(0,3).map(d=><button key={d.id} className="priority-item" onClick={()=>openDoc(d)}><div className="priority-mark"><Clock3 size={17}/></div>
              <div><strong>{d.title}</strong><small>{d.code}</small></div><ChevronRight size={15}/></button>)}
            {urgent.length===0&&<p className="subtle">Không có văn bản khẩn đang chờ.</p>}
          </div>
        </div></div>
      </>}

      {['van-ban-di','van-ban-den'].includes(page)&&<>
        <div className="page-intro compact"><div><div className="overline">QUẢN LÝ NGHIỆP VỤ</div><h1>{names[page]}</h1><p>{page==='van-ban-di'?'Soạn thảo, trình duyệt và theo dõi văn bản phát hành.':'Tiếp nhận và xử lý văn bản gửi đến đơn vị.'}</p></div>
          {page==='van-ban-di'&&isAllowed(actor,'documents.manage')&&<button className="primary" onClick={()=>setCreateOpen(true)}><Plus size={18}/>Tạo văn bản mới</button>}</div>
        <div className="mini-stats"><span><Files size={17}/>Tổng: <strong>{filtered.length}</strong></span><span><Clock3 size={17}/>Đang xử lý: <strong>{filtered.filter(d=>d.status!=='HOAN_THANH').length}</strong></span><span><CheckCircle2 size={17}/>Hoàn thành: <strong>{filtered.filter(d=>d.status==='HOAN_THANH').length}</strong></span></div>
        {!DEMO&&page==='van-ban-den'?<LiveDeliveries token={token} direction="in" onUpdated={updateAfterOperation}/>:
          documentList(page==='van-ban-di'?'Danh sách văn bản đi':'Danh sách văn bản đến')}
      </>}

      {page==='hanh-trinh'&&<><div className="page-intro compact"><div><div className="overline">MINH BẠCH QUY TRÌNH</div><h1>Hành trình văn bản</h1>
        <p>Xem từng bước chuyển, người đang giữ việc và trạng thái tiếp nhận của từng cơ quan.</p></div></div>
        <div className="journey-workspace"><div className="panel journey-list"><div className="panel-head"><div><h3>Chọn văn bản</h3><p>{filtered.length} văn bản</p></div></div>
          {filtered.map(d=><button className={`journey-list-item ${journeyPick===d.id?'chosen':''}`} key={d.id} onClick={()=>pickJourney(d)}>
            <div className="journey-list-top"><small>{d.number||d.code}</small><Status status={d.status} small/></div>
            <strong>{d.title}</strong><div><span><CalendarDays size={14}/>{formatDate(d.createdAt).slice(0,10)}</span><span>{d.to.length} nơi nhận</span></div>
          </button>)}{filtered.length===0&&<EmptyView title="Chưa có dữ liệu" message="Hãy tạo một văn bản mới để bắt đầu." icon={Route}/>}</div>
          <div className="panel journey-preview">{filtered.find(d=>d.id===journeyPick)||filtered[0]?(()=>{
            const chosen=filtered.find(d=>d.id===journeyPick)||filtered[0];
            const d=!DEMO&&journeyPick===chosen.id&&journeyTimeline?{...chosen,events:journeyTimeline.events,transfers:journeyTimeline.transfers}:chosen;
            return <><div className="journey-preview-top"><div><span className="overline">HÀNH TRÌNH ĐANG THEO DÕI</span><h3>{d.title}</h3><span className="subtle">{d.number||d.code}</span></div>
              <button className="secondary small" onClick={()=>openDoc(d)}>Xem chi tiết <ArrowUpRight size={16}/></button></div>
              <div className="preview-summary"><div><small>TRẠNG THÁI</small><Status status={d.status}/></div><div><small>NGƯỜI GIỮ VIỆC</small><strong>{d.assignee}</strong></div>
                <div><small>SỐ PHIÊN BẢN</small><strong>{d.version}</strong></div></div>
              <div className="journey-timeline-preview"><h4>Lịch sử sự kiện</h4><Timeline doc={d}/></div>
              <div className="journey-deliveries"><h4>Phát hành đến các cơ quan</h4><TransferCards doc={d}/></div>
            </>})():<EmptyView title="Chọn một văn bản" message="Thông tin hành trình sẽ xuất hiện tại đây." icon={Route}/>}</div>
        </div></>}

      {page==='cong-viec'&&<><div className="page-intro compact"><div><div className="overline">HÀNG ĐỢI CÔNG VIỆC</div><h1>Việc cần xử lý</h1><p>Các văn bản đang chờ bạn kiểm tra, phê duyệt hoặc giải quyết.</p></div></div>
        <div className="panel tasks-panel"><div className="panel-head"><h3>Công việc của tôi</h3><span className="count-bubble">{DEMO?visibleDocs.filter(d=>d.assignee===actor.name||d.assignee.includes(actor.name.split(' ').at(-1)||'___')).length:workqueue.length}</span></div>
          {DEMO?visibleDocs.filter(d=>d.assignee===actor.name||d.assignee.includes(actor.name.split(' ').at(-1)||'___')).map(d=><button className="task-row" key={d.id} onClick={()=>openDoc(d)}><div className="task-icon"><FileClock size={20}/></div><div><strong>{d.title}</strong><small>{d.code} · Hạn {formatDate(d.dueAt)}</small></div><Status status={d.status}/><ChevronRight size={18}/></button>)
           :workqueue.map(t=><button className="task-row" key={String(field(t,'MaTacVu'))} onClick={()=>{const d=visibleDocs.find(v=>v.id===field(t,'MaVanBan'));if(d)openDoc(d);}}><div className="task-icon"><FileClock size={20}/></div><div><strong>{String(field(t,'TieuDe'))}</strong><small>{String(field(t,'MaNoiBo'))} · {String(field(t,'Buoc'))}</small></div><Status status="CHO_DUYET"/><ChevronRight size={18}/></button>)}
          {DEMO&&visibleDocs.filter(d=>d.assignee===actor.name||d.assignee.includes(actor.name.split(' ').at(-1)||'___')).length===0&&<EmptyView title="Không có việc đang chờ" message="Các công việc được giao sẽ hiện tại đây." icon={CheckCheck}/>}</div></>}

      {page==='giao-nhan'&&<><div className="page-intro compact"><div><div className="overline">CHUYỂN PHÁT LIÊN CƠ QUAN</div><h1>Giao nhận văn bản</h1><p>Biên nhận được theo dõi độc lập tại mỗi cơ quan nhận.</p></div></div>
        {!DEMO&&<LiveDeliveries token={token} direction="out" onUpdated={updateAfterOperation}/>}
        {DEMO&&<>
        <div className="mini-stats"><span><Network size={17}/>Tổng lượt: <strong>{totalTransfer}</strong></span><span><CheckCircle2 size={17}/>Đã nhận: <strong>{visibleDocs.flatMap(d=>d.transfers).filter(t=>t.status==='DA_TIEP_NHAN').length}</strong></span>
          <span><Clock3 size={17}/>Chờ nhận: <strong>{visibleDocs.flatMap(d=>d.transfers).filter(t=>t.status!=='DA_TIEP_NHAN').length}</strong></span></div>
        <div className="panel deliveries-panel"><div className="panel-head"><h3>Danh sách giao nhận</h3></div>
          {visibleDocs.filter(d=>d.transfers.length>0).map(d=><div className="delivery-group" key={d.id}><div className="delivery-group-head"><div><small>{d.number||d.code}</small><h4>{d.title}</h4></div>
            <button className="secondary small" onClick={()=>openDoc(d)}>Hành trình <ArrowUpRight size={16}/></button></div><TransferCards doc={d}/></div>)}
          {totalTransfer===0&&<EmptyView title="Chưa có lượt chuyển phát" message="Chỉ bản đã ký và phát hành mới được giao đến cơ quan nhận." icon={Network}/>}</div></>}</>}

      {page==='ho-so'&&<><div className="page-intro compact"><div><div className="overline">LƯU TRỮ ĐIỆN TỬ</div><h1>Hồ sơ lưu trữ</h1><p>Tập hợp văn bản và quản lý vị trí lưu hồ sơ giấy.</p></div></div>
        <EmptyView title="Chưa có hồ sơ" message="Chức năng tạo, liên kết và đóng hồ sơ sẽ được triển khai trong giai đoạn tiếp theo." icon={Archive}/></>}

      {page==='bao-cao'&&<><div className="page-intro compact"><div><div className="overline">THỐNG KÊ</div><h1>Báo cáo tổng hợp</h1><p>Chỉ số trong phạm vi cơ quan và quyền đang đăng nhập.</p></div></div>
        <div className="report-grid">{[
          ['Văn bản đang xử lý',visibleDocs.filter(d=>d.status!=='HOAN_THANH').length],
          ['Văn bản đã hoàn thành',visibleDocs.filter(d=>d.status==='HOAN_THANH').length],
          ['Văn bản khẩn',visibleDocs.filter(d=>d.urgency!=='THUONG').length],
          ['Lượt giao nhận',totalTransfer]
        ].map(([label,value])=><div className="panel report-item" key={label}><span>{label}</span><strong>{value}</strong><div className="report-bar"/></div>)}</div></>}

      {page==='quan-tri'&&<><div className="page-intro compact"><div><div className="overline">QUẢN TRỊ NỀN TẢNG</div><h1>Quản trị hệ thống</h1>
        <p>Quản lý cơ quan và tài khoản. Tài khoản quản trị không mặc nhiên xem nội dung văn bản.</p></div></div>
        <div className="admin-grid"><div className="panel admin-tile"><Building2 size={28}/><h3>Quản lý cơ quan</h3><p>Cơ quan A, B, C hoạt động độc lập với sổ và quyền riêng.</p><span>Chức năng quản trị chi tiết đang phát triển</span></div>
          <div className="panel admin-tile"><Users size={28}/><h3>Người dùng</h3><p>Quản lý tài khoản, vai trò quản trị, khóa tài khoản.</p><span>Không có quyền tự cấp nghiệp vụ</span></div>
          <div className="panel admin-tile"><ShieldCheck size={28}/><h3>Bảo vệ tài liệu</h3><p>Mọi API cần kiểm tra quyền và phạm vi dữ liệu.</p><span>Thiết kế mặc định từ chối khi thiếu quyền</span></div></div></>}
    </div>
    </main>
    {currentDoc&&<DocumentDetails key={currentDoc.id} doc={currentDoc} onClose={()=>setSelected(null)} onAction={action} allowed={actor}
      canDecide={DEMO?currentDoc.assignee===actor.name:workqueue.some(t=>String(field(t,'MaVanBan'))===currentDoc.id)}
      liveTimeline={remoteTimeline} token={token} onUpdated={updateAfterOperation}/>}
    {createOpen&&<NewDocument onClose={()=>setCreateOpen(false)} onCreate={create} live={!DEMO} recipients={agencyOptions}/>}
    {notice&&<div className="toast"><CheckCircle2 size={18}/>{notice}<button aria-label="Đóng thông báo" onClick={()=>setNotice('')}><X size={15}/></button></div>}
  </div>;
}
