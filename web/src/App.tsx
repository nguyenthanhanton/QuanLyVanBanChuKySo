import { Fragment, useEffect, useMemo, useState, type FormEvent } from 'react';
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
import SignaturePlacement, { type SignaturePosition } from './SignaturePlacement';
import HoSoPage from './HoSoPage';
import DieuHanhVanBanDenPanel from './DieuHanhVanBanDenPanel';
import ChiTietVanBanDen from './ChiTietVanBanDen';

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
    { id:'giam-sat',label:'Giám sát tiến độ',icon:Clock3,permission:'reports.read' },
  ] },
  { group: 'QUẢN TRỊ', items: [
    { id:'quan-tri',label:'Quản trị tài khoản',icon:Settings2,permission:'users.manage' },
    { id:'co-cau',label:'Cơ cấu phòng ban',icon:Building2,permission:'departments.manage' },
    { id:'uy-quyen',label:'Ủy quyền ký thay',icon:ShieldCheck,permission:'permissions.manage' },
  ] },
];

const names: Record<Trang, string> = {
  'tong-quan':'Tổng quan', 'van-ban-di':'Quản lý văn bản đi', 'van-ban-den':'Quản lý văn bản đến',
  'cong-viec':'Việc cần xử lý', 'hanh-trinh':'Hành trình văn bản', 'giao-nhan':'Theo dõi giao nhận',
  'ho-so':'Hồ sơ lưu trữ', 'bao-cao':'Báo cáo thống kê', 'giam-sat':'Giám sát tiến độ', 'quan-tri':'Quản trị tài khoản',
  'co-cau':'Quản lý cơ cấu phòng ban', 'uy-quyen':'Ủy quyền ký thay'
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
  if(permission==='incoming.manage' && (user?.capabilities.includes('workflow.decide') ||
      user?.capabilities.includes('workflow.queue')))return true;
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
  let res:Response;
  try{
    res=await fetch('/api/v1'+path,{...init,headers:{ ...(init?.body instanceof FormData ? {} : {'Content-Type':'application/json'}),
      Authorization:`Bearer ${token}`,...init?.headers }});
  }catch{
    throw new Error('Không thể kết nối máy chủ. Hãy kiểm tra backend và kết nối mạng.');
  }
  if(!res.ok) {
    const problem=await res.json().catch(()=>({}));
    const fallback:Record<number,string>={
      400:'Yêu cầu không hợp lệ.',401:'Phiên đăng nhập không hợp lệ hoặc đã hết hạn.',
      403:'Bạn không có quyền truy cập chức năng này.',404:'Không tìm thấy văn bản hoặc tài nguyên được yêu cầu.',
      409:'Dữ liệu đã thay đổi hoặc có xung đột.',500:'Máy chủ gặp lỗi xử lý yêu cầu.'};
    throw new Error(typeof problem.message==='string'&&problem.message.trim()?problem.message:
      fallback[res.status]??`Máy chủ trả lỗi ${res.status}.`);
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
    createdAt:s('ThoiDiemTao'),version:Number(field(d,'PhienBanHienTai') ?? 1),
    signingSteps:Number(field(d,'SoBuocKy')??5),to:[],events:[],transfers:[]
  };
}

type MonitorTask={MaTacVu:string;MaVanBan:string;MaNoiBo:string;TieuDe:string;
  Buoc:string;TrangThai:string;HanXuLy:string|null;ThoiDiemTao:string;ThoiDiemHoanThanh:string|null;
  NguoiPhuTrach:string;TenPhongBan:string|null;NguoiKyThay:string|null;TinhTrangHan:string;SoPhutXuLy:number};
type MonitorReport={NguoiXuLy:string;PhongBan:string;Tong:number;DangCho:number;DaXuLy:number;
  QuaHan:number;DungHan:number;PhutTrungBinh:number|null};
type MonitorNotification={MaThamChieu:string;LoaiThongBao:string;MaVanBan:string;MaNoiBo:string;
  TieuDe:string;ThoiDiem:string;MucDo:string;DaDoc:boolean};
const deadlineLabels:Record<string,string>={HOAN_THANH:'Đã hoàn thành',QUA_HAN:'Quá hạn',
  SAP_DEN_HAN:'Sắp đến hạn',TRONG_HAN:'Trong hạn',CHUA_DAT_HAN:'Chưa đặt hạn'};
const PAGE_SIZE=15;
function pageRows<T>(rows:T[],page:number):T[]{
  const safe=Math.min(Math.max(page,1),Math.max(1,Math.ceil(rows.length/PAGE_SIZE)));
  return rows.slice((safe-1)*PAGE_SIZE,safe*PAGE_SIZE);
}
function Pagination({total,page,onPage}:{total:number;page:number;onPage:(page:number)=>void}){
  const pages=Math.max(1,Math.ceil(total/PAGE_SIZE));
  if(total<=PAGE_SIZE)return null;
  const current=Math.min(Math.max(page,1),pages);
  return <div className="pagination-controls"><span>{(current-1)*PAGE_SIZE+1}–{Math.min(current*PAGE_SIZE,total)} / {total} kết quả</span>
    <button className="secondary small" disabled={current===1} onClick={()=>onPage(current-1)}>Trước</button>
    <span>Trang {current}/{pages}</span>
    <button className="secondary small" disabled={current===pages} onClick={()=>onPage(current+1)}>Sau</button></div>;
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
  const [timelinePage,setTimelinePage]=useState(1);
  const events=[...doc.events].reverse();
  return <div className="timeline">
    {pageRows(events,timelinePage).map((event,i)=><div className="timeline-entry" key={event.id}>
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
    <Pagination total={events.length} page={timelinePage} onPage={setTimelinePage}/>
  </div>;
}

function TransferCards({doc}:{doc:VanBan}) {
  const [transferPage,setTransferPage]=useState(1);
  if(doc.transfers.length===0) return <div className="empty-card"><Network size={26}/><strong>Chưa phát hành liên cơ quan</strong><span>Khi văn bản được phát hành, nơi nhận và biên nhận sẽ xuất hiện ở đây.</span></div>;
  return <div className="transfer-cards">{pageRows(doc.transfers,transferPage).map(t=><div className="transfer-card" key={t.id}>
    <div className="transfer-card-top"><div className="agency-icon"><Building2 size={18}/></div><div className="transfer-name"><strong>{t.agency}</strong><span>{t.contact}</span></div><Status status={t.status} small/></div>
    <div className="transfer-meta"><span>Thời gian gửi</span><strong>{formatDate(t.sentAt)}</strong></div>
    <div className="transfer-meta"><span>Tiếp nhận</span><strong>{formatDate(t.receivedAt)}</strong></div>
    {t.note && <p className="transfer-note">{t.note}</p>}
  </div>)}<Pagination total={doc.transfers.length} page={transferPage} onPage={setTransferPage}/></div>;
}

function SignaturePanel({doc,actor,token,onUpdated,taskStep}:{doc:VanBan;actor:Actor|null;token:string;onUpdated:()=>Promise<void>;taskStep?:string}) {
  const total=doc.signingSteps??5;
  const [accountPassword,setAccountPassword]=useState('');
  const [pfxPassword,setPfxPassword]=useState('');
  const [certName,setCertName]=useState('');
  const [certType,setCertType]=useState('CA_NHAN');
  const [testCert,setTestCert]=useState(false);
  const [replaceTestCert,setReplaceTestCert]=useState(false);
  const [pfxFile,setPfxFile]=useState<File>();
  const [signatureImage,setSignatureImage]=useState<File>();
  const [signaturePage,setSignaturePage]=useState(1);
  const [signatureX,setSignatureX]=useState(60);
  const [signatureY,setSignatureY]=useState(73);
  const [signatureWidth,setSignatureWidth]=useState(32);
  const [signatureHeight,setSignatureHeight]=useState(11);
  const [signedPreview,setSignedPreview]=useState(false);
  const [placementOpen,setPlacementOpen]=useState(false);
  const [placementConfirmed,setPlacementConfirmed]=useState(false);
  const [mainPdfPath,setMainPdfPath]=useState('');
  const [placementError,setPlacementError]=useState('');
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState('');
  const [verify,setVerify]=useState<Record<string,unknown>|null>(null);
  const [credentials,setCredentials]=useState<Record<string,unknown>[]>([]);
  useEffect(()=>{
    if(DEMO||!token)return;
    requestApi('/signing-credentials',token).then((x:Record<string,unknown>[])=>setCredentials(x)).catch(()=>{});
    requestApi(`/documents/${doc.id}/signatures/verification`,token).then((x:Record<string,unknown>)=>setVerify(x)).catch(()=>{});
  },[doc.id,token]);
  useEffect(()=>{
    if(DEMO||!token)return;
    let alive=true;
    setMainPdfPath('');setPlacementError('');setPlacementConfirmed(false);
    requestApi(`/documents/${doc.id}/attachments`,token).then((files:Record<string,unknown>[])=>{
      const main=files.find(file=>Boolean(field(file,'LaVanBanChinh')));
      if(!main)throw new Error('Văn bản chưa có PDF chính.');
      if(alive)setMainPdfPath(`/documents/${doc.id}/files/${String(field(main,'MaTep'))}`);
    }).catch(err=>{if(alive)setPlacementError((err as Error).message);});
    return ()=>{alive=false;};
  },[doc.id,doc.version,token]);
  const operation=async(kind:'prepare'|'sign'|'draft-sign'|'agency-sign'|'publish'|'verify'|'download',signature?:string)=>{
    setBusy(true);setMessage('');
    try {
      if(kind==='download'){
        await downloadApi(`/documents/${doc.id}/signed-pdf`,token,`van-ban-${doc.code}.pdf`);
      } else if(kind==='verify'){
        setVerify(await requestApi(`/documents/${doc.id}/signatures/verification`,token));
      } else {
        if(kind==='sign'||kind==='draft-sign'||kind==='agency-sign'){
          if(!signatureImage||!placementConfirmed)throw new Error('Hãy chọn ảnh PNG, kéo đặt chữ ký trên PDF và xác nhận vị trí trước khi ký.');
          if(!accountPassword)throw new Error('Cần nhập mật khẩu để xác nhận ký.');
        }
        const endpoint=kind==='prepare'?`/documents/${doc.id}/prepared-pdf`:
          kind==='sign'?`/documents/${doc.id}/signatures`:
          kind==='draft-sign'?`/documents/${doc.id}/draft-signature`:
          kind==='agency-sign'?`/documents/${doc.id}/agency-signature`:`/documents/${doc.id}/publications`;
        let imageBase64:string|undefined;
        if(signatureImage&&(kind==='sign'||kind==='draft-sign'||kind==='agency-sign')){
          if(signatureImage.size>5*1024*1024||signatureImage.type!=='image/png')
            throw new Error('Ảnh ký phải là PNG tối đa 5 MB.');
          const url=await new Promise<string>((resolve,reject)=>{
            const reader=new FileReader();reader.onload=()=>resolve(String(reader.result));
            reader.onerror=()=>reject(new Error('Không đọc được ảnh chữ ký.'));
            reader.readAsDataURL(signatureImage);
          });
          imageBase64=url.split(',')[1];
        }
        const payload=kind==='sign'||kind==='draft-sign'||kind==='agency-sign'?{
          loaiChuKy:kind==='draft-sign'?'KY_NGUOI_SOAN':kind==='agency-sign'?'KY_CO_QUAN':signature,
          matKhau:accountPassword,maYeuCau:crypto.randomUUID(),
          hinhAnhKyBase64:imageBase64,trangKy:signaturePage,
          tyLeX:signatureX/100,tyLeY:signatureY/100,
          tyLeRong:signatureWidth/100,tyLeCao:signatureHeight/100}:
          {maYeuCau:crypto.randomUUID()};
        await requestApi(endpoint,token,{method:'POST',body:JSON.stringify(payload)});
        setAccountPassword('');
        setPlacementConfirmed(false);
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
      if(replaceTestCert&&!testCert)throw new Error('Chỉ được thay chứng thư tự ký thử nghiệm.');
      const form=new FormData();form.set('loaiChungThu',certType);form.set('tenChungThu',certName);
      form.set('matKhau',accountPassword);form.set('matKhauPfx',pfxPassword);
      form.set('thayTheChungThuThuNghiem',String(replaceTestCert));
      form.set('thuNghiem',String(testCert));if(pfxFile)form.set('tepPfx',pfxFile);
      const result=await requestApi('/signing-credentials',token,{method:'POST',body:form});
      setMessage((result.daThayThe?'Đã lưu chứng thư thử nghiệm mới; chứng thư cũ vẫn được giữ để tra cứu.':
        result.noiBoThuNghiem?'Đã tạo chứng thư thử nghiệm nội bộ.':'Đã nhập chứng thư;')+
        ' Hệ thống chưa thẩm định chuỗi CA/thu hồi.');
      setAccountPassword('');setPfxPassword('');setPfxFile(undefined);setReplaceTestCert(false);
      setCredentials(await requestApi('/signing-credentials',token));
    }catch(err){setMessage((err as Error).message);}finally{setBusy(false);}
  };
  const activeCreds=credentials.filter(c=>field(c,'DangHoatDong')===true);
  const signatures=(field(verify??{},'ChuKy') as Record<string,unknown>[]|undefined)??[];
  return <section className="drawer-section signature-live">
    <div className="signature-header"><div className="signature-icon"><ShieldCheck size={30}/></div>
      <h3>Ký số PDF và xác minh</h3><p>Chữ ký CMS bảo đảm toàn vẹn; ảnh PNG chỉ là phần hiển thị trong vùng chữ ký số.</p></div>
    <div className="form-section"><h4>Chứng thư đang sử dụng</h4>
      {activeCreds.map(c=><div className="version-row" key={String(field(c,'MaChungThu'))}>
        <ShieldCheck size={18}/><div><strong>{String(field(c,'TenChungThu'))}</strong><span>{String(field(c,'LoaiChungThu'))} · {field(c,'NguonChungThu')==='TU_KY_THU_NGHIEM'?'Thử nghiệm nội bộ':'PFX nhập'} · Hết hạn {formatDate(String(field(c,'NgayHetHan')))}</span></div>
      </div>)}
      <div className="form-grid"><label>Loại chứng thư<select value={certType} onChange={e=>setCertType(e.target.value)}>
        <option value="CA_NHAN">Cá nhân</option><option value="CO_QUAN">Cơ quan (văn thư)</option></select></label>
        <label>Tên chứng thư<input value={certName} onChange={e=>setCertName(e.target.value)} placeholder="Tên cá nhân / cơ quan"/></label></div>
      <label className="form-check"><input type="checkbox" checked={testCert} onChange={e=>setTestCert(e.target.checked)}/>
        Tạo chứng thư tự ký để thử nghiệm (không được coi là chữ ký số được CA công nhận)</label>
      {testCert&&activeCreds.some(c=>String(field(c,'LoaiChungThu'))===certType)&&
        <label className="form-check"><input type="checkbox" checked={replaceTestCert} onChange={e=>setReplaceTestCert(e.target.checked)}/>
          Thay thế chứng thư thử nghiệm cũ (không xóa chứng thư cũ hoặc PDF đã ký)</label>}
      {!testCert&&<><label>Nhập chứng thư PFX/P12<input type="file" accept=".pfx,.p12" onChange={e=>setPfxFile(e.target.files?.[0])}/></label>
        <label>Mật khẩu tệp PFX<input type="password" value={pfxPassword} onChange={e=>setPfxPassword(e.target.value)}/></label></>}
      <label>Mật khẩu tài khoản để ký / nhập chứng thư<input type="password" value={accountPassword} onChange={e=>setAccountPassword(e.target.value)}/></label>
      <button className="secondary small" disabled={busy||certName.trim().length<3||
        !isAllowed(actor,certType==='CA_NHAN'?'signatures.manage-own':'signatures.manage-agency')}
        onClick={createCredential}>Lưu chứng thư</button></div>
    <div className="form-section"><h4>Các bước ký và phát hành</h4>
      {['NHAP','TRA_SUA','CHO_DUYET','DA_CAP_SO'].includes(doc.status)&&<div className="form-section">
        <h4>Xem PDF và kéo thả hình chữ ký trước khi ký</h4>
        <p className="fine-note">1. Chọn ảnh PNG nền trong suốt tối đa 5 MB. 2. Nhấn “Mở PDF để đặt chữ ký”, kéo ảnh vào vị trí và chỉnh kích thước. 3. Xác nhận vị trí rồi mới nhấn ký số. Không hỗ trợ trang PDF xoay.</p>
        <label>Ảnh chữ ký / con dấu (PNG)
          <input type="file" accept="image/png,.png" onChange={e=>{setSignatureImage(e.target.files?.[0]);setPlacementConfirmed(false);}}/>
        </label>
        {placementError&&<div className="form-error">{placementError}</div>}
        <button className="primary small" disabled={busy||!signatureImage||!mainPdfPath}
          onClick={()=>{setPlacementConfirmed(false);setPlacementOpen(true);}}>Mở PDF để đặt chữ ký</button>
        {placementConfirmed?<p className="success-note">✓ Đã xác nhận vị trí: trang {signaturePage}, trái {signatureX.toFixed(1)}%, trên {signatureY.toFixed(1)}%. Có thể nhấn ký số.</p>:
          <p className="fine-note">Chưa xác nhận vị trí ký. Hãy đặt ảnh trước khi ký.</p>}
        {placementOpen&&signatureImage&&<SignaturePlacement
          sourcePath={signatures.length>0?`/documents/${doc.id}/signed-pdf`:mainPdfPath}
          token={token} image={signatureImage}
          position={{page:signaturePage,x:signatureX,y:signatureY,width:signatureWidth,height:signatureHeight}}
          onClose={()=>setPlacementOpen(false)} onConfirm={(value:SignaturePosition)=>{
            setSignaturePage(value.page);setSignatureX(value.x);setSignatureY(value.y);
            setSignatureWidth(value.width);setSignatureHeight(value.height);
            setPlacementConfirmed(true);setPlacementOpen(false);
          }}/>}
        <button className="secondary small" disabled={busy||!mainPdfPath} onClick={()=>setSignedPreview(true)}>Xem văn bản chính</button>
        {signedPreview&&<FilePreview path={signatures.length>0?`/documents/${doc.id}/signed-pdf`:mainPdfPath} filename={`van-ban-${doc.code}.pdf`}
          token={token} onClose={()=>setSignedPreview(false)}/>}
      </div>}
      {(doc.status==='NHAP'||doc.status==='TRA_SUA')&&isAllowed(actor,'signing.sign')&&<>
        <p className="fine-note">Bước 1: Người tạo ký PDF chính trước khi trình duyệt. Khi chọn người ký, hệ thống tự xác định 3 vị trí nếu lãnh đạo soạn (không qua trưởng phòng), hoặc 4 vị trí với người soạn thuộc phòng ban. Nếu sửa tệp, cần ký lại.</p>
        <button className="primary small" disabled={busy||signatures.length>0}
          onClick={()=>operation('draft-sign')}>{signatures.length>0?'Đã ký PDF người soạn':'Ký số PDF người soạn (1)'}</button>
      </>}
      {doc.status==='CHO_DUYET'&&isAllowed(actor,'signing.sign')&&taskStep&&<div className="signature-step-actions">
        {taskStep==='KIEM_TRA_NOI_DUNG'&&<button className="primary small" disabled={busy||signatures.length!==1}
          onClick={()=>operation('sign','KY_NHAY')}>2/{total}. Trưởng đơn vị được chọn ký nháy</button>}
        {taskStep==='KIEM_TRA_THE_THUC'&&<button className="primary small" disabled={busy||signatures.length!==2}
          onClick={()=>operation('sign','KY_THE_THUC')}>3/5. Trưởng văn phòng ký thể thức</button>}
        {taskStep==='PHE_DUYET_LANH_DAO'&&<button className="primary small" disabled={busy||signatures.length!==total-2}
          onClick={()=>operation('sign','KY_LANH_DAO')}>{total-1}/{total}. Lãnh đạo được chọn ký phê duyệt</button>}
        <p className="fine-note">Chỉ người đang được giao xử lý mới được ký. Ký xong quay lại Hành trình để duyệt chuyển bước.</p>
      </div>}
      {doc.status==='DA_CAP_SO'&&<>
        <p className="fine-note">Bước {total}/{total}: Văn thư ký số cơ quan trên PDF đã được lãnh đạo duyệt.</p>
        {isAllowed(actor,'signing.sign')&&isAllowed(actor,'signatures.manage-agency')&&
          <button className="primary small" disabled={busy||signatures.length!==total-1}
            onClick={()=>operation('agency-sign')}>{total}/{total}. Văn thư ký số cơ quan</button>}
      </>}
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
  const [deliveryListPage,setDeliveryListPage]=useState(1);
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
    {pageRows(rows,deliveryListPage).map(row=>{
      const id=String(field(row,'MaLuotChuyen'));
      const status=String(field(row,'TrangThai')) as ChuyenPhat['status'];
      return <div className="delivery-live-row" key={id}>
        <div className="delivery-live-main"><div className="eyebrow">{String(field(row,direction==='out'?'CoQuanNhan':'CoQuanGui'))}</div>
          <strong>{String(field(row,'TieuDe'))}</strong><small>{String(field(row,'MaNoiBo')??field(row,'KyHieu')??'Bản đã phát hành')}
            {' · Gửi: '}{formatDate(String(field(row,'ThoiDiemGui')??''))}</small></div>
        <Status status={status} small/>
        <div className="delivery-live-actions">
          <button className="secondary small" onClick={()=>setPreview({path:`/deliveries/${id}/pdf`,name:`van-ban-${id}.pdf`})}>
            Xem PDF</button>
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
    <Pagination total={rows.length} page={deliveryListPage} onPage={setDeliveryListPage}/>
    {preview&&<FilePreview path={preview.path} filename={preview.name} token={token} onClose={()=>setPreview(null)}/>}
  </div>;
}

function DraftAttachmentEditor({doc,actor,token,onUpdated}:{doc:VanBan;actor:Actor|null;token:string;onUpdated:()=>Promise<void>}) {
  const [attachments,setAttachments]=useState<Record<string,unknown>[]>([]);
  const [mainSigned,setMainSigned]=useState(false);
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
    // One logical main document: after signing, preview/download the newest signed PDF.
    // A 404 means this draft has not yet been signed (or the user cannot access its signed copy).
    const hasSigned=await requestApi(`/documents/${doc.id}/signatures/verification`,token)
      .then((result:Record<string,unknown>)=>{
        const entries=field(result,'ChuKy') as Record<string,unknown>[]|undefined;
        return Array.isArray(entries)&&entries.length>0;
      }).catch(()=>false);
    setMainSigned(hasSigned);
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
      <h4>{isMain?'Văn bản chính — một PDF, cập nhật sau mỗi lần ký':'Tệp đính kèm — Word / Excel / PDF'}</h4>
      <div className="attachment-list">
      {attachments.filter(file=>Boolean(field(file,'LaVanBanChinh'))===isMain).map(file=>{const id=String(field(file,'MaTep'));const filename=String(field(file,'TenTep'));
        const currentPath=isMain&&mainSigned?`/documents/${doc.id}/signed-pdf`:`/documents/${doc.id}/files/${id}`;
        return <div className="attachment-entry" key={id}><FileText size={17}/><span className={removed.includes(id)?'marked-removal':''}>{filename}{Boolean(field(file,'LaVanBanChinh'))?' · Văn bản chính':''}</span>
          {isMain&&mainSigned&&<span className="success-note">Đã cập nhật chữ ký số</span>}
          <button className="secondary small" onClick={()=>setPreview({path:currentPath,name:filename})}>{isMain?'Xem văn bản chính':'Xem trước'}</button>
          <button className="secondary small" onClick={()=>downloadApi(currentPath,token,filename).catch(err=>setError((err as Error).message))}>Tải về</button>
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

type SigningChoice={maNguoiKyNhay:number;maNguoiKyLanhDao:number};
type SignerOption={MaNguoiDung:number;HoTen:string;ChucVu:string;TenPhongBan:string;VaiTroKy:string};

type AdminUser={MaNguoiDung:number;TenDangNhap:string;HoTen:string;TenPhongBan:string;
  MaPhongBan:number;MaVaiTro:string;ChucVu:string;DangHoatDong:boolean};
type AdminOption={MaDinhDanh?:string;TenVaiTro?:string;MaPhongBan?:number;TenPhongBan?:string;ThuTuHienThi?:number};
type SigningDelegationTask={MaTacVu:string;MaVanBan:string;Buoc:string;TieuDe:string;
  NguoiDuocGiao:string;MaNguoiDuocGiao:number;MaNguoiDuocUyQuyen:number|null;
  HieuLucDen:string|null;DaThuHoi:boolean};
type SigningCandidate={MaNguoiDung:number;HoTen:string};
type SigningDelegationHistory={MaTacVu:string;MaNoiBo:string;TieuDe:string;Buoc:string;
  NguoiGoc:string;NguoiDuocUyQuyen:string;NguoiCap:string;LyDo:string;
  HieuLucTu:string;HieuLucDen:string;ThoiDiemThuHoi:string|null;ThoiDiemKy:string|null;
  TrangThaiUyQuyen:string};
function SigningDelegationAdministration({token}:{token:string}){
  const [tasks,setTasks]=useState<SigningDelegationTask[]>([]);
  const [task,setTask]=useState('');const [candidates,setCandidates]=useState<SigningCandidate[]>([]);
  const [delegateId,setDelegateId]=useState(0);const [hours,setHours]=useState(48);
  const [reason,setReason]=useState('');const [busy,setBusy]=useState(false);
  const [history,setHistory]=useState<SigningDelegationHistory[]>([]);
  const [historyFilter,setHistoryFilter]=useState('ALL');
  const [historyPage,setHistoryPage]=useState(1);
  const [error,setError]=useState('');const [message,setMessage]=useState('');
  const load=async()=>{
    const [raw,historyRaw]=await Promise.all([
      requestApi('/admin/signing-delegations/tasks',token),
      requestApi('/admin/signing-delegations/history',token)
    ]) as [Record<string,unknown>[],Record<string,unknown>[]];
    const rows=raw.map(x=>({MaTacVu:String(field(x,'MaTacVu')),MaVanBan:String(field(x,'MaVanBan')),
      Buoc:String(field(x,'Buoc')),TieuDe:String(field(x,'TieuDe')),
      NguoiDuocGiao:String(field(x,'NguoiDuocGiao')),MaNguoiDuocGiao:Number(field(x,'MaNguoiDuocGiao')),
      MaNguoiDuocUyQuyen:field(x,'MaNguoiDuocUyQuyen')==null?null:Number(field(x,'MaNguoiDuocUyQuyen')),
      HieuLucDen:field(x,'HieuLucDen')==null?null:String(field(x,'HieuLucDen')),
      DaThuHoi:Boolean(field(x,'DaThuHoi'))}));
    setTasks(rows);setTask(current=>rows.some(t=>t.MaTacVu===current)?current:rows[0]?.MaTacVu??'');
    setHistory(historyRaw.map(x=>({
      MaTacVu:String(field(x,'MaTacVu')),MaNoiBo:String(field(x,'MaNoiBo')),
      TieuDe:String(field(x,'TieuDe')),Buoc:String(field(x,'Buoc')),
      NguoiGoc:String(field(x,'NguoiGoc')),NguoiDuocUyQuyen:String(field(x,'NguoiDuocUyQuyen')),
      NguoiCap:String(field(x,'NguoiCap')),LyDo:String(field(x,'LyDo')),
      HieuLucTu:String(field(x,'HieuLucTu')),HieuLucDen:String(field(x,'HieuLucDen')),
      ThoiDiemThuHoi:field(x,'ThoiDiemThuHoi')==null?null:String(field(x,'ThoiDiemThuHoi')),
      ThoiDiemKy:field(x,'ThoiDiemKy')==null?null:String(field(x,'ThoiDiemKy')),
      TrangThaiUyQuyen:String(field(x,'TrangThaiUyQuyen'))
    })));
  };
  useEffect(()=>{load().catch(e=>setError((e as Error).message));},[token]);
  useEffect(()=>{
    if(!task){setCandidates([]);setDelegateId(0);return;}
    let active=true;
    requestApi(`/admin/signing-delegations/tasks/${task}/candidates`,token).then((raw:Record<string,unknown>[])=>{
      if(!active)return;
      const list=raw.map(x=>({MaNguoiDung:Number(field(x,'MaNguoiDung')),HoTen:String(field(x,'HoTen'))}));
      setCandidates(list);setDelegateId(current=>list.some(x=>x.MaNguoiDung===current)?current:list[0]?.MaNguoiDung??0);
    }).catch(e=>{if(active)setError((e as Error).message);});
    return()=>{active=false;};
  },[token,task]);
  const current=tasks.find(t=>t.MaTacVu===task);
  const active=!!current?.MaNguoiDuocUyQuyen&&!current.DaThuHoi&&!!current.HieuLucDen&&
    new Date(current.HieuLucDen).getTime()>Date.now();
  const filteredHistory=history.filter(h=>historyFilter==='ALL'||h.TrangThaiUyQuyen===historyFilter);
  const submit=async(action:'grant'|'revoke')=>{
    if(!current)return;
    setBusy(true);setError('');setMessage('');
    try{
      if(action==='grant')await requestApi('/admin/signing-delegations',token,{method:'POST',body:JSON.stringify({
        maTacVu:task,maNguoiDuocUyQuyen:delegateId,soGioHieuLuc:hours,lyDo:reason})});
      else await requestApi(`/admin/signing-delegations/${task}/revoke`,token,{method:'POST'});
      await load();setMessage(action==='grant'?'Đã cấp ủy quyền cho tác vụ ký.':'Đã thu hồi ủy quyền.');
    }catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  return <section className="panel admin-signing-delegation">
    <div className="panel-head"><div><h3>Ủy quyền ký thay có thời hạn</h3>
      <p>Chỉ ủy quyền một tác vụ đang chờ ký, cho người đủ chức vụ cùng phòng; chữ ký PDF mang tên người ký thực tế.</p>
    </div><button className="secondary small" disabled={busy} onClick={()=>load().catch(e=>setError((e as Error).message))}>
      <RefreshCw size={15}/> Tải lại</button></div>
    <div className="admin-delegation-form">
      <label>Tác vụ cần ủy quyền<select value={task} onChange={e=>setTask(e.target.value)} disabled={busy}>
        {tasks.length===0&&<option value="">Không có tác vụ đang chờ</option>}
        {tasks.map(t=><option key={t.MaTacVu} value={t.MaTacVu}>{t.TieuDe} — {t.Buoc==='KIEM_TRA_NOI_DUNG'?'Ký nháy':'Lãnh đạo ký'}</option>)}
      </select></label>
      {current&&<div className="admin-delegation-summary">
        <strong>{current.TieuDe}</strong><span>Người được giao ban đầu: {current.NguoiDuocGiao}</span>
        {current.MaNguoiDuocUyQuyen!=null&&<span>Đã ủy quyền cho mã người dùng {current.MaNguoiDuocUyQuyen} — {current.DaThuHoi?'Đã thu hồi':active?'Đang hiệu lực':'Hết hạn'}</span>}
      </div>}
      {current&&!current.MaNguoiDuocUyQuyen&&<>
        <label>Người được ủy quyền<select value={delegateId} disabled={busy} onChange={e=>setDelegateId(Number(e.target.value))}>
          {candidates.length===0&&<option value={0}>Không có người đủ điều kiện</option>}
          {candidates.map(x=><option key={x.MaNguoiDung} value={x.MaNguoiDung}>{x.HoTen}</option>)}
        </select></label>
        <label>Thời hạn (giờ)<input type="number" min={1} max={720} value={hours} disabled={busy} onChange={e=>setHours(Number(e.target.value))}/></label>
        <label className="admin-delegation-reason">Lý do ủy quyền<textarea rows={3} maxLength={500} minLength={10} value={reason}
          onChange={e=>setReason(e.target.value)} placeholder="Ví dụ: Trưởng phòng đang đi công tác, ủy quyền Phó trưởng phòng xử lý tác vụ này."/></label>
        <button className="primary small" disabled={busy||!delegateId||hours<1||hours>720||reason.trim().length<10}
          onClick={()=>submit('grant')}><ShieldCheck size={15}/> Cấp ủy quyền</button>
      </>}
      {current&&current.MaNguoiDuocUyQuyen!=null&&!current.DaThuHoi&&<button className="secondary small" disabled={busy} onClick={()=>submit('revoke')}>Thu hồi ủy quyền</button>}
    </div>
    <div className="admin-delegation-history">
      <div className="panel-head"><div><h3>Lịch sử ủy quyền ký thay</h3>
        <p>Lưu người giao, người ký thay, thời hạn và lý do; bao gồm cả tác vụ đã hoàn thành.</p></div>
        <label>Trạng thái <select value={historyFilter} onChange={e=>{setHistoryFilter(e.target.value);setHistoryPage(1);}}>
          <option value="ALL">Tất cả</option><option value="DANG_HIEU_LUC">Đang hiệu lực</option>
          <option value="DA_KY">Đã ký thay</option><option value="HET_HAN">Hết hạn</option>
          <option value="DA_THU_HOI">Đã thu hồi</option><option value="DA_KET_THUC">Tác vụ kết thúc</option>
        </select></label></div>
      {pageRows(filteredHistory,historyPage).map(h=><div className="admin-delegation-history-row" key={h.MaTacVu}>
        <div className="admin-delegation-history-head"><strong>{h.TieuDe} · {h.MaNoiBo}</strong>
          <span>{({'DANG_HIEU_LUC':'Đang hiệu lực','DA_KY':'Đã ký thay','HET_HAN':'Hết hạn',
            'DA_THU_HOI':'Đã thu hồi','DA_KET_THUC':'Tác vụ đã kết thúc'} as Record<string,string>)[h.TrangThaiUyQuyen]??h.TrangThaiUyQuyen}</span></div>
        <p>{h.Buoc==='KIEM_TRA_NOI_DUNG'?'Ký nháy':'Ký lãnh đạo'}: <strong>{h.NguoiGoc}</strong> ủy quyền cho <strong>{h.NguoiDuocUyQuyen}</strong></p>
        <p>Người cấp: {h.NguoiCap} · Từ {formatDate(h.HieuLucTu)} đến {formatDate(h.HieuLucDen)}</p>
        <p>Lý do: {h.LyDo}</p>
        {h.ThoiDiemKy&&<p>Thực tế ký: {formatDate(h.ThoiDiemKy)}</p>}
        {h.ThoiDiemThuHoi&&<p>Thu hồi: {formatDate(h.ThoiDiemThuHoi)}</p>}
      </div>)}
      {history.length===0&&<p className="fine-note">Chưa có lịch sử ủy quyền trong cơ quan.</p>}
      <Pagination total={filteredHistory.length} page={historyPage} onPage={setHistoryPage}/>
    </div>
    {error&&<div className="form-error">{error}</div>}{message&&<p className="success-note">{message}</p>}
  </section>;
}
type PermissionRow={MaDinhDanh:string;TenQuyen:string;ChoPhep:boolean|null;TuVaiTro:boolean};
function UserPermissionSettings({token,user,onClose}:{token:string;user:AdminUser;onClose:()=>void}){
  const [permissions,setPermissions]=useState<PermissionRow[]>([]);
  const [busy,setBusy]=useState('');const [error,setError]=useState('');const [message,setMessage]=useState('');
  const load=async()=>{
    const result=await requestApi(`/admin/users/${user.MaNguoiDung}/permissions`,token) as Record<string,unknown>[];
    setPermissions(result.map(x=>({MaDinhDanh:String(field(x,'MaDinhDanh')),
      TenQuyen:String(field(x,'TenQuyen')),ChoPhep:field(x,'ChoPhep')==null?null:Boolean(field(x,'ChoPhep')),
      TuVaiTro:Boolean(field(x,'TuVaiTro'))})));
  };
  useEffect(()=>{load().catch(e=>setError((e as Error).message));},[token,user.MaNguoiDung]);
  const save=async(code:string,allow:boolean|null)=>{
    setBusy(code);setError('');setMessage('');
    try{await requestApi(`/admin/users/${user.MaNguoiDung}/permissions`,token,{method:'PUT',
      body:JSON.stringify({maDinhDanh:code,choPhep:allow})});await load();setMessage('Đã cập nhật quyền. Áp dụng ngay cho các yêu cầu API tiếp theo.');}
    catch(e){setError((e as Error).message);}finally{setBusy('');}
  };
  return <section className="admin-permission-settings"><div className="panel-head"><div>
    <h3>Phân quyền — {user.HoTen}</h3><p>Chọn kế thừa từ chức vụ, cho phép hoặc từ chối từng quyền nghiệp vụ.</p>
    </div><button className="secondary small" onClick={onClose}><X size={16}/> Đóng</button></div>
    <div className="admin-permission-grid">{permissions.map(p=><div className="admin-permission-item" key={p.MaDinhDanh}>
      <div><strong>{p.TenQuyen}</strong><small>{p.MaDinhDanh} · Chức vụ: {p.TuVaiTro?'có quyền':'không có quyền'}</small></div>
      <select aria-label={`Quyền ${p.TenQuyen}`} disabled={!!busy} value={p.ChoPhep===null?'inherit':p.ChoPhep?'allow':'deny'}
        onChange={e=>save(p.MaDinhDanh,e.target.value==='inherit'?null:e.target.value==='allow')}>
        <option value="inherit">Theo chức vụ</option><option value="allow">Cho phép</option><option value="deny">Từ chối</option>
      </select></div>)}</div>
    {error&&<div className="form-error">{error}</div>}{message&&<p className="success-note">{message}</p>}
  </section>;
}
type ManagedDepartment={MaPhongBan:number;MaDinhDanh:string;TenPhongBan:string;ThuTuHienThi:number;
  MaPhongBanCha:number|null;DangHoatDong:boolean;SoNguoiDung:number;SoPhongBanCon:number};
function DepartmentAdministration({token}:{token:string}){
  const [items,setItems]=useState<ManagedDepartment[]>([]);
  const [departmentPage,setDepartmentPage]=useState(1);
  const [draft,setDraft]=useState<Record<number,{name:string;order:number;parent:number}>>({});
  const [code,setCode]=useState('');const [name,setName]=useState('');const [order,setOrder]=useState(50);
  const [parent,setParent]=useState(0);const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');const [message,setMessage]=useState('');
  const load=async()=>{
    const rows=await requestApi('/admin/departments',token) as Record<string,unknown>[];
    const list=rows.map(x=>({MaPhongBan:Number(field(x,'MaPhongBan')),
      MaDinhDanh:String(field(x,'MaDinhDanh')),TenPhongBan:String(field(x,'TenPhongBan')),
      ThuTuHienThi:Number(field(x,'ThuTuHienThi')),
      MaPhongBanCha:field(x,'MaPhongBanCha')==null?null:Number(field(x,'MaPhongBanCha')),
      DangHoatDong:Boolean(field(x,'DangHoatDong')),SoNguoiDung:Number(field(x,'SoNguoiDung')),
      SoPhongBanCon:Number(field(x,'SoPhongBanCon'))}));
    setItems(list);setDraft(Object.fromEntries(list.map(d=>[d.MaPhongBan,
      {name:d.TenPhongBan,order:d.ThuTuHienThi,parent:d.MaPhongBanCha??0}])));
  };
  useEffect(()=>{load().catch(e=>setError((e as Error).message));},[token]);
  const act=async(fn:()=>Promise<unknown>,success:string)=>{
    setBusy(true);setError('');setMessage('');
    try{await fn();await load();setMessage(success);}
    catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  return <section className="panel admin-departments"><div className="panel-head"><div>
    <h3>Quản lý cơ cấu phòng ban</h3>
    <p>Thứ tự hiển thị: số nhỏ xếp trước. Trực thuộc: chọn đơn vị quản lý cấp trên; không tự cấp quyền ký hoặc duyệt văn bản.</p>
    </div><button className="secondary small" disabled={busy} onClick={()=>load().catch(e=>setError((e as Error).message))}><RefreshCw size={15}/> Tải lại</button></div>
    <h4 className="admin-department-section-title">Thêm phòng ban mới</h4>
    <form className="admin-department-create" onSubmit={e=>{e.preventDefault();act(async()=>{
      await requestApi('/admin/departments',token,{method:'POST',body:JSON.stringify({
        maDinhDanh:code,tenPhongBan:name,thuTuHienThi:order,maPhongBanCha:parent||null})});
      setCode('');setName('');setOrder(50);setParent(0);
    },'Đã thêm phòng ban.');}}>
      <label>Mã phòng ban (không dấu)<input required value={code} maxLength={40} placeholder="VD: KE_TOAN"
        onChange={e=>setCode(e.target.value.toUpperCase())}/></label>
      <label>Tên phòng ban<input required value={name} maxLength={200} placeholder="VD: Phòng Kế toán"
        onChange={e=>setName(e.target.value)}/></label>
      <label>Thứ tự hiển thị<input type="number" value={order} min={0} max={9999} onChange={e=>setOrder(Number(e.target.value))}/></label>
      <label>Phòng cấp trên (trực thuộc)<select value={parent} onChange={e=>setParent(Number(e.target.value))}>
        <option value={0}>Trực thuộc cơ quan</option>
        {items.filter(d=>d.DangHoatDong).map(d=><option key={d.MaPhongBan} value={d.MaPhongBan}>{d.TenPhongBan}</option>)}
      </select></label>
      <button className="primary small" type="submit" disabled={busy||!code||!name}><Plus size={15}/> Thêm phòng</button>
    </form>
    <h4 className="admin-department-section-title">Danh sách phòng ban <span>({items.length})</span></h4>
    <div className="admin-department-list">{pageRows(items,departmentPage).map(d=>{const edit=draft[d.MaPhongBan]??
      {name:d.TenPhongBan,order:d.ThuTuHienThi,parent:d.MaPhongBanCha??0};
      return <div className="admin-department-row" key={d.MaPhongBan}>
        <div className="admin-department-description"><strong>{d.TenPhongBan}</strong>
          <small>{d.MaDinhDanh} · {d.SoNguoiDung} người · {d.SoPhongBanCon} phòng trực thuộc</small>
          <span className={d.DangHoatDong?'success-note':'form-error'}>{d.DangHoatDong?'Đang hoạt động':'Ngừng hoạt động'}</span></div>
        <div className="admin-department-fields"><label>Tên phòng<input disabled={busy||!d.DangHoatDong} value={edit.name}
          onChange={e=>setDraft(s=>({...s,[d.MaPhongBan]:{...edit,name:e.target.value}}))}/></label>
        <label>Thứ tự hiển thị<input disabled={busy||!d.DangHoatDong} type="number" min={0} max={9999} value={edit.order}
          onChange={e=>setDraft(s=>({...s,[d.MaPhongBan]:{...edit,order:Number(e.target.value)}}))}/></label>
        <label>Phòng cấp trên (trực thuộc)<select disabled={busy||!d.DangHoatDong||d.MaDinhDanh==='LANH_DAO'} value={edit.parent}
          onChange={e=>setDraft(s=>({...s,[d.MaPhongBan]:{...edit,parent:Number(e.target.value)}}))}>
          <option value={0}>Trực thuộc cơ quan</option>
          {items.filter(p=>p.DangHoatDong&&p.MaPhongBan!==d.MaPhongBan).map(p=><option key={p.MaPhongBan} value={p.MaPhongBan}>{p.TenPhongBan}</option>)}
        </select></label></div>
        <div className="admin-department-actions"><button className="secondary small" disabled={busy||!d.DangHoatDong}
          onClick={()=>act(()=>requestApi(`/admin/departments/${d.MaPhongBan}`,token,{method:'PUT',body:JSON.stringify({
            tenPhongBan:edit.name,thuTuHienThi:edit.order,maPhongBanCha:edit.parent||null})}),'Đã lưu phòng ban.')}>Lưu</button>
          <button className="secondary small" disabled={busy||d.MaDinhDanh==='VAN_THU'||['LANH_DAO','VAN_PHONG'].includes(d.MaDinhDanh)&&d.DangHoatDong}
            onClick={()=>act(()=>requestApi(`/admin/departments/${d.MaPhongBan}/status`,token,{method:'PUT',body:JSON.stringify({
              dangHoatDong:!d.DangHoatDong})}),d.DangHoatDong?'Đã ngừng hoạt động phòng.':'Đã mở lại phòng.')}>{d.DangHoatDong?'Ngừng':'Mở lại'}</button>
        </div>
      </div>;})}</div>
    <Pagination total={items.length} page={departmentPage} onPage={setDepartmentPage}/>
    {error&&<div className="form-error">{error}</div>}{message&&<p className="success-note">{message}</p>}
  </section>;
}
function UserAdministration({token,username,canManagePermissions}:{token:string;username:string;canManagePermissions:boolean}){
  const [users,setUsers]=useState<AdminUser[]>([]);
  const [userPage,setUserPage]=useState(1);
  const [departments,setDepartments]=useState<AdminOption[]>([]);
  const [roles,setRoles]=useState<AdminOption[]>([]);
  const [name,setName]=useState('');const [login,setLogin]=useState('');
  const [password,setPassword]=useState('');const [department,setDepartment]=useState(0);
  const [role,setRole]=useState('CHUYEN_VIEN');const [positionName,setPositionName]=useState('');
  const [edits,setEdits]=useState<Record<number,{department:number;role:string;positionName:string}>>({});
  const [busy,setBusy]=useState(false);const [error,setError]=useState('');const [message,setMessage]=useState('');
  const [permissionUser,setPermissionUser]=useState<number|null>(null);
  const load=async()=>{
    const [options,accounts]=await Promise.all([
      requestApi('/admin/options',token),requestApi('/admin/users',token)
    ]);
    const ds=(field(options,'phongBan') as Record<string,unknown>[]).map(x=>({
      MaPhongBan:Number(field(x,'MaPhongBan')),TenPhongBan:String(field(x,'TenPhongBan')),
      MaDinhDanh:String(field(x,'MaDinhDanh')),ThuTuHienThi:Number(field(x,'ThuTuHienThi'))}));
    const rs=(field(options,'vaiTro') as Record<string,unknown>[]).map(x=>({
      MaDinhDanh:String(field(x,'MaDinhDanh')),TenVaiTro:String(field(x,'TenVaiTro'))}));
    const us=(accounts as Record<string,unknown>[]).map(x=>({
      MaNguoiDung:Number(field(x,'MaNguoiDung')),TenDangNhap:String(field(x,'TenDangNhap')),
      HoTen:String(field(x,'HoTen')),TenPhongBan:String(field(x,'TenPhongBan')??''),
      MaPhongBan:Number(field(x,'MaPhongBan')??0),MaVaiTro:String(field(x,'MaVaiTro')??''),
      ChucVu:String(field(x,'ChucVu')??''),
      DangHoatDong:Boolean(field(x,'DangHoatDong'))}));
    setDepartments(ds);setRoles(rs);setUsers(us);
    setDepartment(current=>{
      const id=current||ds[0]?.MaPhongBan||0;
      const leader=ds.find(d=>d.MaPhongBan===id)?.MaDinhDanh==='LANH_DAO';
      setRole(currentRole=>leader?(['PHO_LANH_DAO','VAN_THU'].includes(currentRole)?currentRole:'LANH_DAO'):
        ['LANH_DAO','PHO_LANH_DAO','VAN_THU'].includes(currentRole)?'CHUYEN_VIEN':currentRole);
      return id;
    });
    setEdits(Object.fromEntries(us.map(u=>[u.MaNguoiDung,{department:u.MaPhongBan,role:u.MaVaiTro,positionName:u.ChucVu}])));
  };
  useEffect(()=>{load().catch(e=>setError((e as Error).message));},[token]);
  const action=async(fn:()=>Promise<unknown>,success:string)=>{
    setBusy(true);setError('');setMessage('');
    try{await fn();await load();setMessage(success);}catch(e){setError((e as Error).message);}finally{setBusy(false);}
  };
  const validRoles=(departmentId:number)=>{
    const leadership=departments.find(d=>d.MaPhongBan===departmentId)?.MaDinhDanh==='LANH_DAO';
    return roles.filter(r=>leadership?['LANH_DAO','PHO_LANH_DAO','VAN_THU'].includes(r.MaDinhDanh??''):
      !['LANH_DAO','PHO_LANH_DAO','VAN_THU'].includes(r.MaDinhDanh??''));
  };
  const changeDepartment=(id:number,currentRole:string)=>{
    const available=validRoles(id);return available.some(r=>r.MaDinhDanh===currentRole)?currentRole:available[0]?.MaDinhDanh??'';
  };
  const roleLabel=(r:string)=>roles.find(item=>item.MaDinhDanh===r)?.TenVaiTro??r;
  const departmentRank=(id:number)=>{
    return departments.find(d=>d.MaPhongBan===id)?.ThuTuHienThi??9999;
  };
  const positionRank=(roleCode:string)=>({LANH_DAO:0,TRUONG_PHONG:0,TRUONG_VAN_PHONG:0,
    PHO_LANH_DAO:1,PHO_PHONG:1,PHO_VAN_PHONG:1,CHUYEN_VIEN:2,VAN_THU:2,
    AGENCY_ADMIN:3} as Record<string,number>)[roleCode]??4;
  const orderedUsers=[...users].sort((a,b)=>departmentRank(a.MaPhongBan)-departmentRank(b.MaPhongBan)||
    (a.TenPhongBan||'').localeCompare(b.TenPhongBan||'','vi')||
    positionRank(a.MaVaiTro)-positionRank(b.MaVaiTro)||a.HoTen.localeCompare(b.HoTen,'vi'));
  return <div className="user-admin">
    <section className="panel admin-create"><h3>Thêm tài khoản vào cơ quan</h3>
      <p className="subtle">Chỉ quản trị viên cơ quan được tạo tài khoản. Mật khẩu không được đưa vào GitHub hay nhật ký.</p>
      <form onSubmit={e=>{e.preventDefault();action(async()=>{
        await requestApi('/admin/users',token,{method:'POST',body:JSON.stringify({
          tenDangNhap:login,hoTen:name,matKhau:password,maPhongBan:department,maVaiTro:role,tenChucVu:positionName})});
        setPassword('');setLogin('');setName('');setPositionName('');
      },'Đã tạo tài khoản. Hãy hướng dẫn người dùng đổi mật khẩu sau lần đăng nhập đầu.');}}>
        <div className="form-grid admin-create-grid">
          <label>Tên đăng nhập<input value={login} onChange={e=>setLogin(e.target.value)} required placeholder="vd: phophong_a"/></label>
          <label>Họ và tên<input value={name} onChange={e=>setName(e.target.value)} required placeholder="Nhập họ tên người dùng"/></label>
          <label>Mật khẩu ban đầu (ít nhất 12 ký tự)<input type="password" value={password} onChange={e=>setPassword(e.target.value)} minLength={12} required autoComplete="new-password"/></label>
          <label>Phòng ban<select value={department} onChange={e=>{const id=Number(e.target.value);setDepartment(id);setRole(changeDepartment(id,role));}}>
            {departments.map(d=><option key={d.MaPhongBan} value={d.MaPhongBan}>{d.TenPhongBan}</option>)}
          </select></label>
          <label>Chức vụ<select value={role} onChange={e=>setRole(e.target.value)}>
            {validRoles(department).map(r=><option key={r.MaDinhDanh} value={r.MaDinhDanh}>{r.TenVaiTro}</option>)}
          </select></label>
          <label>Tên gọi chức vụ tại cơ quan (tùy chọn)<input maxLength={150} value={positionName} onChange={e=>setPositionName(e.target.value)} placeholder="VD: Trưởng văn phòng, Phó viện trưởng"/></label>
        </div>
        <button className="primary" type="submit" disabled={busy||!department||!role||password.length<12}><Plus size={16}/> Tạo tài khoản</button>
      </form></section>
    <section className="panel admin-users-list"><div className="panel-head"><div><h3>Người dùng trong cơ quan</h3><p>{users.length} tài khoản</p></div>
      <button className="secondary small" disabled={busy} onClick={()=>load().catch(e=>setError((e as Error).message))}><RefreshCw size={15}/> Tải lại</button></div>
      {permissionUser!==null&&users.find(u=>u.MaNguoiDung===permissionUser)&&<UserPermissionSettings
        key={permissionUser} token={token} user={users.find(u=>u.MaNguoiDung===permissionUser)!}
        onClose={()=>setPermissionUser(null)}/>}
      {pageRows(orderedUsers,userPage).map((u,index)=>{const edit=edits[u.MaNguoiDung]??{department:u.MaPhongBan,role:u.MaVaiTro,positionName:u.ChucVu};
        const globalIndex=(Math.min(userPage,Math.max(1,Math.ceil(orderedUsers.length/PAGE_SIZE)))-1)*PAGE_SIZE+index;
        const own=u.TenDangNhap===username;const protectedUser=u.MaVaiTro==='AGENCY_ADMIN'||own;
        const groupStart=index===0||orderedUsers[globalIndex-1].MaPhongBan!==u.MaPhongBan;
        const count=groupStart?orderedUsers.filter(item=>item.MaPhongBan===u.MaPhongBan).length:0;
        return <Fragment key={u.MaNguoiDung}>
        {groupStart&&<div className="admin-department-header"><strong>{u.TenPhongBan||'Chưa phân phòng ban'}</strong><span>{count} tài khoản</span></div>}
        <div className="admin-user-row">
          <div><strong>{u.HoTen}</strong><small>{u.TenDangNhap} · {u.TenPhongBan}</small><small>{u.ChucVu||roleLabel(u.MaVaiTro)}</small>
            <span className={u.DangHoatDong?'success-note':'form-error'}>{u.DangHoatDong?'Đang hoạt động':'Đã khóa'}</span></div>
          <label>Phòng ban<select disabled={busy||protectedUser} value={edit.department} onChange={e=>{const id=Number(e.target.value);setEdits(s=>({...s,[u.MaNguoiDung]:{...edit,department:id,role:changeDepartment(id,edit.role)}}));}}>
            {departments.map(d=><option key={d.MaPhongBan} value={d.MaPhongBan}>{d.TenPhongBan}</option>)}
          </select></label>
          <label>Chức vụ<select disabled={busy||protectedUser} value={edit.role} onChange={e=>setEdits(s=>({...s,[u.MaNguoiDung]:{...edit,role:e.target.value}}))}>
            {protectedUser&&<option value={u.MaVaiTro}>{u.MaVaiTro}</option>}
            {!protectedUser&&!validRoles(edit.department).some(r=>r.MaDinhDanh===edit.role)&&<option value={edit.role}>{roleLabel(edit.role)} (cũ)</option>}
            {validRoles(edit.department).map(r=><option key={r.MaDinhDanh} value={r.MaDinhDanh}>{r.TenVaiTro}</option>)}
          </select></label>
          <label>Tên gọi chức vụ<input disabled={busy||protectedUser} maxLength={150} value={edit.positionName}
            onChange={e=>setEdits(s=>({...s,[u.MaNguoiDung]:{...edit,positionName:e.target.value}}))} placeholder="VD: Trưởng văn phòng"/></label>
          <div className="admin-user-actions"><button className="secondary small" disabled={busy||protectedUser||!edit.department||!edit.role}
            onClick={()=>action(()=>requestApi(`/admin/users/${u.MaNguoiDung}/assignment`,token,{method:'PUT',body:JSON.stringify({maPhongBan:edit.department,maVaiTro:edit.role,tenChucVu:edit.positionName})}),'Đã cập nhật phân công.')}>Lưu phân công</button>
            {canManagePermissions&&!protectedUser&&<button className="secondary small" disabled={busy}
              onClick={()=>setPermissionUser(u.MaNguoiDung)}><ShieldCheck size={14}/> Phân quyền</button>}
            <button className="secondary small" disabled={busy||protectedUser}
              onClick={()=>action(()=>requestApi(`/admin/users/${u.MaNguoiDung}/status`,token,{method:'PUT',body:JSON.stringify({dangHoatDong:!u.DangHoatDong})}),u.DangHoatDong?'Đã khóa tài khoản.':'Đã mở tài khoản.')}>{u.DangHoatDong?'Khóa':'Mở khóa'}</button></div>
        </div></Fragment>;})}
      <Pagination total={orderedUsers.length} page={userPage} onPage={setUserPage}/>
    </section>
    {error&&<div className="form-error">{error}</div>}{message&&<p className="success-note">{message}</p>}
  </div>;
}

function SignerPicker({doc,token,onClose,onSubmit}:{doc:VanBan;token:string;onClose:()=>void;
  onSubmit:(choice:SigningChoice)=>void}){
  const [leaderDraft,setLeaderDraft]=useState(false);
  const [clerkDraft,setClerkDraft]=useState(false);
  const [reviewers,setReviewers]=useState<SignerOption[]>([]);
  const [leaders,setLeaders]=useState<SignerOption[]>([]);
  const [reviewer,setReviewer]=useState(0);
  const [leader,setLeader]=useState(0);
  const [role,setRole]=useState('');
  const [error,setError]=useState('');
  const [loading,setLoading]=useState(true);
  useEffect(()=>{
    let active=true;
    requestApi(`/documents/${doc.id}/eligible-signers`,token).then((data:Record<string,unknown>)=>{
      if(!active)return;
      const first=(field(data,'nguoiKyNhay')??[]) as Record<string,unknown>[];
      const second=(field(data,'nguoiKyLanhDao')??[]) as Record<string,unknown>[];
      const convert=(items:Record<string,unknown>[]):SignerOption[]=>items.map(x=>({
        MaNguoiDung:Number(field(x,'MaNguoiDung')),HoTen:String(field(x,'HoTen')),
        ChucVu:String(field(x,'ChucVu')??''),TenPhongBan:String(field(x,'TenPhongBan')??''),
        VaiTroKy:String(field(x,'VaiTroKy')??'')
      }));
      const a=convert(first),b=convert(second);
      setReviewers(a);setLeaders(b);setReviewer(a[0]?.MaNguoiDung??0);setLeader(b[0]?.MaNguoiDung??0);
      setRole(String(field(data,'vaiTroKyNhay')??''));
      setLeaderDraft(Boolean(field(data,'boQuaKyNhay')));
      setClerkDraft(Boolean(field(data,'vanThuSoan')));
    }).catch(e=>{if(active)setError((e as Error).message);}).finally(()=>{if(active)setLoading(false);});
    return ()=>{active=false;};
  },[doc.id,token]);
  return <div className="preview-backdrop" onClick={onClose}><div className="signer-picker" onClick={e=>e.stopPropagation()}>
    <h3>Chọn người được phép ký văn bản</h3>
    <p className="fine-note">{leaderDraft?'Lãnh đạo soạn: chọn lãnh đạo khác ký duyệt ở bước 2, sau đó văn thư ký cơ quan ở bước 3. Không trình qua trưởng phòng.':
      clerkDraft?'Văn thư soạn: chọn một Trưởng phòng bất kỳ trong cơ quan ký nháy ở bước 2; tiếp theo lãnh đạo ký duyệt và văn thư ký số cơ quan.':
      'Người soạn đã ký bước 1. Chọn trưởng/phó trưởng đơn vị ký nháy (bước 2), lãnh đạo ký duyệt (bước 3), văn thư ký cơ quan (bước 4).'}</p>
    {loading?<p>Đang tải danh sách người ký...</p>:<>
      {!leaderDraft&&<label>{clerkDraft?'Chọn Trưởng phòng ký nháy (toàn cơ quan)':role==='TRUONG_VAN_PHONG'?'Trưởng văn phòng ký nháy':
        role==='PHO_VAN_PHONG'?'Phó trưởng văn phòng ký nháy':
        role==='PHO_PHONG'?'Phó trưởng phòng ký nháy':'Trưởng phòng / người được ủy quyền ký nháy'}
        <select value={reviewer} onChange={e=>setReviewer(Number(e.target.value))}>
          {reviewers.map(p=><option key={p.MaNguoiDung} value={p.MaNguoiDung}>{p.HoTen} — {p.VaiTroKy} — {p.TenPhongBan}</option>)}
        </select></label>}
      <label>Lãnh đạo trưởng / cấp phó ký chính thức
        <select value={leader} onChange={e=>setLeader(Number(e.target.value))}>
          {leaders.map(p=><option key={p.MaNguoiDung} value={p.MaNguoiDung}>{p.HoTen} — {p.VaiTroKy}</option>)}
        </select></label>
      {((!leaderDraft&&!reviewers.length)||!leaders.length)&&<div className="form-error">{clerkDraft&&!reviewers.length?
        'Không tìm thấy Trưởng phòng đang hoạt động trong cơ quan. Hãy kiểm tra chức vụ và phòng ban của các tài khoản trong Quản trị tài khoản.':
        'Chưa có người ký phù hợp và khác người soạn. Cần bổ nhiệm người ký đúng vai trò; lãnh đạo cấp trưởng soạn thì chỉ chọn các cấp phó, lãnh đạo cấp phó soạn có thể chọn cấp trưởng hoặc cấp phó khác.'}</div>}
    </>}
    {error&&<div className="form-error">{error}</div>}
    <div className="signature-step-actions"><button className="secondary" onClick={onClose}>Hủy</button>
      <button className="primary" disabled={loading||!!error||(!leaderDraft&&!reviewer)||!leader||(!leaderDraft&&reviewer===leader)}
        onClick={()=>onSubmit({maNguoiKyNhay:leaderDraft?0:reviewer,maNguoiKyLanhDao:leader})}>Xác nhận và trình duyệt</button></div>
  </div></div>;
}

function DocumentDetails({doc,onClose,onAction,allowed,canDecide,taskStep,liveTimeline,token,onUpdated}:
  {doc:VanBan,onClose:()=>void,onAction:(action:'submit'|'approve'|'revise'|'register',choice?:SigningChoice)=>void,allowed:Actor|null,canDecide:boolean,
    taskStep?:string;liveTimeline?:{ events:SuKien[];transfers:ChuyenPhat[] };token:string;onUpdated:()=>Promise<void>}) {
  const [tab,setTab]=useState<'journey'|'info'|'versions'|'signatures'>('journey');
  const [showSignerPicker,setShowSignerPicker]=useState(false);
  const [draftSigned,setDraftSigned]=useState(false);
  const [reviewSigned,setReviewSigned]=useState(false);
  const [checkingDraftSignature,setCheckingDraftSignature]=useState(false);
  const loadDraftSignature=async()=>{
    if(DEMO || !token || (doc.status!=='NHAP'&&doc.status!=='TRA_SUA'))return;
    setCheckingDraftSignature(true);
    try {
      const result=await requestApi(`/documents/${doc.id}/signatures/verification`,token) as Record<string,unknown>;
      const signatures=(field(result,'ChuKy') as Record<string,unknown>[]|undefined)??[];
      setDraftSigned(signatures.length===1&&Boolean(field(signatures[0],'ToanVenMatMa'))&&
        Boolean(field(signatures[0],'BaoPhuToanBoBanHienTai'))&&
        String(field(signatures[0],'TenTruongKy')).startsWith('Ky_1_'));
    }catch{setDraftSigned(false);}finally{setCheckingDraftSignature(false);}
  };
  const reviewOrder=taskStep==='KIEM_TRA_NOI_DUNG'?2:taskStep==='KIEM_TRA_THE_THUC'?3:
    taskStep==='PHE_DUYET_LANH_DAO'?(doc.signingSteps??5)-1:0;
  const loadReviewSignature=async()=>{
    if(DEMO||!token||doc.status!=='CHO_DUYET'||!reviewOrder)return;
    try{
      const result=await requestApi(`/documents/${doc.id}/signatures/verification`,token) as Record<string,unknown>;
      const signatures=(field(result,'ChuKy') as Record<string,unknown>[]|undefined)??[];
      setReviewSigned(signatures.length===reviewOrder&&signatures.every(s=>Boolean(field(s,'ToanVenMatMa')))&&
        Boolean(field(signatures[reviewOrder-1],'BaoPhuToanBoBanHienTai')));
    }catch{setReviewSigned(false);}
  };
  useEffect(()=>{setDraftSigned(false);void loadDraftSignature();},[doc.id,doc.version,doc.status,token]);
  useEffect(()=>{setReviewSigned(false);void loadReviewSignature();},[doc.id,doc.version,doc.status,taskStep,token]);
  const refreshAfterSignature=async()=>{await onUpdated();await loadDraftSignature();await loadReviewSignature();};
  const viewDoc=liveTimeline?{...doc,events:liveTimeline.events,transfers:liveTimeline.transfers}:doc;
  return <div className="drawer-backdrop" onClick={onClose}>
    <div className="drawer" role="dialog" aria-modal="true" aria-label="Chi tiết văn bản" onClick={e=>e.stopPropagation()}>
      <div className="drawer-head"><div><div className="eyebrow">CHI TIẾT VĂN BẢN</div><div className="drawer-code">{doc.code}</div></div>
        <button className="icon-button" aria-label="Đóng" onClick={onClose}><X size={20}/></button></div>
      <div className="drawer-body"><h2>{doc.title}</h2><div className="drawer-pills"><Status status={doc.status}/>
        {doc.urgency!=='THUONG'&&<span className="priority-pill">{doc.urgency==='HOA_TOC'?'Hỏa tốc':'Khẩn'}</span>}
        <span className="neutral-pill">Phiên bản {doc.version}</span></div>
      <div className="drawer-actions">
        {(doc.status==='NHAP'||doc.status==='TRA_SUA') && isAllowed(allowed,'documents.submit') && <>
          <button className="primary small" disabled={!DEMO&&(!draftSigned||checkingDraftSignature)} onClick={()=>DEMO?onAction('submit'):setShowSignerPicker(true)}>
            Trình duyệt<ArrowRight size={15}/></button>
          {!DEMO&&!draftSigned&&<span className="fine-note">Cần ký PDF người soạn tại tab Chữ ký số trước.</span>}
        </>}
        {doc.status==='CHO_DUYET' && canDecide && isAllowed(allowed,'workflow.decide') && <>
          <button className="primary small" disabled={!DEMO&&!reviewSigned} onClick={()=>onAction('approve')}>Phê duyệt<Check size={15}/></button>
          <button className="secondary small" onClick={()=>onAction('revise')}>Trả sửa</button>
          {!DEMO&&!reviewSigned&&<span className="fine-note">Cần ký số PDF ở tab Chữ ký số trước khi duyệt.</span>}
        </>}
        {doc.status==='CHO_CAP_SO' && isAllowed(allowed,'registers.manage') && <button className="primary small" onClick={()=>onAction('register')}>Cấp số văn bản <ArrowRight size={15}/></button>}
      </div>
      {showSignerPicker&&<SignerPicker doc={doc} token={token} onClose={()=>setShowSignerPicker(false)}
        onSubmit={choice=>{setShowSignerPicker(false);onAction('submit',choice);}}/>}
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
      {tab==='signatures' && !DEMO && <SignaturePanel doc={doc} actor={allowed} token={token} taskStep={taskStep} onUpdated={refreshAfterSignature}/>}
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
  const [monitorTasks,setMonitorTasks]=useState<MonitorTask[]>([]);
  const [monitorReports,setMonitorReports]=useState<MonitorReport[]>([]);
  const [notifications,setNotifications]=useState<MonitorNotification[]>([]);
  const [notificationOpen,setNotificationOpen]=useState(false);
  const [incomingFocus,setIncomingFocus]=useState('');
  const [monitorFilter,setMonitorFilter]=useState('ALL');
  const [documentPage,setDocumentPage]=useState(1);
  const [taskPage,setTaskPage]=useState(1);
  const [journeyPage,setJourneyPage]=useState(1);
  const [deliveryPage,setDeliveryPage]=useState(1);
  const [monitorPage,setMonitorPage]=useState(1);
  const [reportPage,setReportPage]=useState(1);
  const [notificationPage,setNotificationPage]=useState(1);
  useEffect(()=>{setDocumentPage(1);setTaskPage(1);setJourneyPage(1);setDeliveryPage(1);
    setMonitorPage(1);setReportPage(1);},[page,query,filter]);
  useEffect(()=>{if(DEMO||!token)return;
    let active=true;
    const poll=()=>{
      requestApi('/monitor/notifications',token).then((rows:MonitorNotification[])=>{if(active)setNotifications(rows);}).catch(()=>{});
      requestApi('/monitor/tasks',token).then((rows:MonitorTask[])=>{if(active)setMonitorTasks(rows);}).catch(()=>{});
    };
    poll();const interval=window.setInterval(poll,60000);
    window.addEventListener('focus',poll);
    return()=>{active=false;window.clearInterval(interval);window.removeEventListener('focus',poll);};
  },[token]);
  const [agencyOptions,setAgencyOptions]=useState<{value:string;label:string}[]>(DEMO?[{value:'Cơ quan B',label:'Cơ quan B'},{value:'Cơ quan C',label:'Cơ quan C'}]:[]);
  const [error,setError]=useState('');

  useEffect(()=>{if(DEMO)localStorage.setItem(storedDocsKey,JSON.stringify(docs));},[docs]);

  const canNavigate=(permission:string,id:Trang)=>isAllowed(actor,permission)||
    (id==='van-ban-den'&&(isAllowed(actor,'workflow.queue')||isAllowed(actor,'documents.read')));
  const menuItems=useMemo(()=>navigation.flatMap(g=>g.items).filter(i=>canNavigate(i.permission,i.id)),[actor]);
  const visibleDocs=useMemo(()=>getAccessibleDocs(docs,actor),[docs,actor]);
  const filtered=useMemo(()=>visibleDocs.filter(d=>{
    if(page==='van-ban-di'&&d.type!=='DI')return false;
    if(page==='van-ban-den'&&d.type!=='DEN')return false;
    if(!DEMO&&page==='hanh-trinh'&&d.type!=='DI')return false;
    if(filter!=='ALL'&&d.status!==filter)return false;
    const term=query.toLocaleLowerCase('vi-VN').trim();
    return !term || `${d.code} ${d.title} ${d.number||''}`.toLocaleLowerCase('vi-VN').includes(term);
  }).sort((a,b)=>new Date(b.createdAt).getTime()-new Date(a.createdAt).getTime()),[visibleDocs,page,filter,query]);
  const monitoredRows=monitorTasks.filter(t=>monitorFilter==='ALL'||t.TinhTrangHan===monitorFilter);
  const currentDoc=visibleDocs.find(d=>d.id===selected);
  const fetchTimeline=async(d:VanBan)=>{
    if(d.type==='DEN')return {events:[] as SuKien[],transfers:[] as ChuyenPhat[]};
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
    if(d.type==='DEN')return;
    if(!DEMO&&token){
      try{setRemoteTimeline(await fetchTimeline(d));}catch(err){setError((err as Error).message);}
    }
  };
  const openWorkqueueDocument=async(task:Record<string,unknown>)=>{
    const id=String(field(task,'MaVanBan')??'');
    const existing=visibleDocs.find(d=>d.id===id);
    if(existing){await openDoc(existing);return;}
    if(!token||!id)return;
    try{
      const response=await requestApi(`/documents/${id}`,token) as Record<string,unknown>;
      const document=adaptLiveDocument(response);
      setDocs(previous=>previous.some(d=>d.id===id)?previous:[...previous,document]);
      await openDoc(document);
    }catch(err){setError(`Không thể mở văn bản của tác vụ: ${(err as Error).message}`);}
  };
  const pickJourney=async(d:VanBan)=>{
    setJourneyPick(d.id);setJourneyTimeline(undefined);
    if(!DEMO&&token){
      try{setJourneyTimeline(await fetchTimeline(d));}catch(err){setError((err as Error).message);}
    }
  };

  const refresh=async(authToken:string,capabilities=actor?.capabilities??[])=>{
    // Backend phân trang 20 bản/lượt; nạp tiếp để phân trang 15 bản trên giao diện không bỏ sót bản cũ.
    const fetchDocuments=async()=>{
      const all:Record<string,unknown>[]=[];
      for(let pageNumber=1;pageNumber<=100;pageNumber++){
        const batch=await requestApi(`/documents?trang=${pageNumber}`,authToken) as Record<string,unknown>[];
        all.push(...batch);
        if(batch.length<20)return all;
      }
      throw new Error('Danh sách vượt giới hạn tải 2.000 văn bản. Cần bổ sung phân trang máy chủ cho dữ liệu lớn.');
    };
    const [documents,tasks,incoming]=await Promise.all([
      fetchDocuments(),
      requestApi('/workqueue',authToken).catch(()=>[]),
      capabilities.includes('incoming.manage')
        ?requestApi('/incoming',authToken).catch(()=>[]):Promise.resolve([])
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
    const [progress,messages]=await Promise.all([
      requestApi('/monitor/tasks',authToken).catch(()=>[]),
      requestApi('/monitor/notifications',authToken).catch(()=>[])
    ]);
    setMonitorTasks(progress as MonitorTask[]);setNotifications(messages as MonitorNotification[]);
  };
  const fetchReports=async()=>{
    if(DEMO||!token||!isAllowed(actor,'reports.read'))return;
    try{setMonitorReports(await requestApi('/monitor/reports',token) as MonitorReport[]);}
    catch(err){setError((err as Error).message);}
  };
  useEffect(()=>{if(page==='bao-cao'||page==='giam-sat')void fetchReports();},[page,token]);
  const openMonitoredDocument=async(id:string)=>{
    const existing=visibleDocs.find(d=>d.id===id);
    if(existing){await openDoc(existing);return;}
    try{const data=await requestApi(`/documents/${id}`,token) as Record<string,unknown>;
      const document=adaptLiveDocument(data);
      setDocs(previous=>previous.some(d=>d.id===id)?previous:[...previous,document]);await openDoc(document);
    }catch{setError('Không có quyền mở chi tiết văn bản này; chỉ được xem số liệu giám sát.');}
  };
  const markNotification=async(n:MonitorNotification)=>{
    try{await requestApi('/monitor/notifications/read',token,{method:'POST',body:JSON.stringify({
      loaiThongBao:n.LoaiThongBao,maThamChieu:n.MaThamChieu})});
      setNotifications(old=>old.map(x=>x.MaThamChieu===n.MaThamChieu&&x.LoaiThongBao===n.LoaiThongBao?{...x,DaDoc:true}:x));
      setNotificationOpen(false);
      if(n.LoaiThongBao.startsWith('DEN_')){
        setIncomingFocus(n.MaVanBan);setPage('van-ban-den');setSelected(null);return;
      }
      await openMonitoredDocument(n.MaVanBan);
    }catch(err){setError((err as Error).message);}
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
    if(caps.includes('documents.read'))await refresh(body.accessToken,caps);
  };

  const logout=()=>{setActor(null);setToken('');setSelected(null);setQuery('');setProfileMenu(false);setError('');};
  const go=(next:Trang)=>{if(!menuItems.find(i=>i.id===next))return;setPage(next);setSelected(null);setIncomingFocus('');setFilter('ALL');setQuery('');setMenuOpen(false);};
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
  const action=async(kind:'submit'|'approve'|'revise'|'register',choice?:SigningChoice)=>{
    if(!actor||!currentDoc)return;
    const reason=kind==='revise'?window.prompt('Nhập lý do trả sửa (bắt buộc):',''):null;
    if(kind==='revise'&&(!reason || !reason.trim()))return;
    const symbol=kind==='register'?window.prompt('Nhập ký hiệu văn bản (ví dụ KH-CQA):','KH-CQA'):null;
    if(kind==='register'&&(!symbol || !symbol.trim()))return;
    if(!DEMO){
      try{
        if(kind==='submit')await requestApi(`/documents/${currentDoc.id}/submit`,token,{method:'POST',body:JSON.stringify({maYeuCau:crypto.randomUUID(),...choice})});
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
      <tbody>{pageRows(filtered,documentPage).map(d=><tr key={d.id} onClick={()=>openDoc(d)} tabIndex={0} onKeyDown={e=>{if(e.key==='Enter')openDoc(d);}}>
        <td><div className="doc-cell"><div className={`file-icon ${d.type==='DEN'?'incoming':''}`}><FileText size={21}/></div>
          <div className="doc-title"><strong>{d.title}</strong><span>{d.number||d.code}{d.urgency!=='THUONG'&&<b className="urgent-label">Khẩn</b>}</span></div></div></td>
        <td className="muted-cell">{formatDate(d.createdAt).slice(0,10)}</td><td><Status status={d.status} small/></td>
        <td><div className="assignee"><span className="tiny-avatar">{initials(d.assignee)}</span><span>{d.assignee}</span></div></td>
        <td><button className="row-open" aria-label={`Xem ${d.title}`}><ChevronRight size={18}/></button></td></tr>)}</tbody></table>
      {filtered.length===0&&<EmptyView icon={Files} title="Chưa có văn bản" message="Không tìm thấy văn bản phù hợp với bộ lọc hiện tại."/>}</div>
    <div className="table-footer"><span>Tổng {filtered.length} kết quả</span><span>Chọn một văn bản để xem hành trình <ArrowUpRight size={14}/></span></div>
    <Pagination total={filtered.length} page={documentPage} onPage={setDocumentPage}/>
  </div>;

  return <div className="app-shell">
    <aside className={`sidebar ${menuOpen?'show':''}`}>
      <div className="brand"><div className="brand-symbol"><Files size={25}/></div><div><strong>VĂN THƯ SỐ</strong><span>QUẢN LÝ VĂN BẢN</span></div></div>
      <div className="agency-card"><div className="agency-icon"><Building2 size={19}/></div><div><span>Đơn vị đang làm việc</span><strong>{actor.agency}</strong></div><ChevronDown size={15}/></div>
      <nav>{navigation.map(group=>{
        const items=group.items.filter(i=>canNavigate(i.permission,i.id));
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
        <button className="icon-button notification-button" title="Thông báo" aria-label={`Thông báo, ${notifications.filter(n=>!n.DaDoc).length} chưa đọc`}
          onClick={()=>{setNotificationOpen(x=>!x);setNotificationPage(1);
            if(!DEMO&&token)void requestApi('/monitor/notifications',token).then(setNotifications).catch(()=>{});}}>
          <Bell size={20}/>{notifications.some(n=>!n.DaDoc)&&<span className="notification-count">{Math.min(99,notifications.filter(n=>!n.DaDoc).length)}</span>}</button>
        {notificationOpen&&<div className="notification-popup"><div className="notification-heading">
          <h3>Thông báo của tôi</h3><span>{notifications.filter(n=>!n.DaDoc).length} chưa đọc</span></div>
          <div className="notification-list">{pageRows(notifications,notificationPage).map(n=>{
            const incoming=n.LoaiThongBao.startsWith('DEN_');
            const overdue=n.MucDo==='QUA_HAN';
            const soon=n.MucDo==='SAP_DEN_HAN';
            const Icon=overdue?CircleAlert:soon?Clock3:incoming?Inbox:n.LoaiThongBao==='SU_KIEN'?FileCheck2:FileClock;
            return <button key={`${n.LoaiThongBao}-${n.MaThamChieu}`}
              className={`notification-item ${n.DaDoc?'read':'unread'} ${overdue?'overdue':soon?'soon':''}`}
              onClick={()=>void markNotification(n)}>
              <span className="notification-item-icon"><Icon size={19}/></span>
              <span className="notification-item-body"><span className="notification-item-top"><strong>{n.TieuDe}</strong>
                {!n.DaDoc&&<span className="notification-unread-indicator" title="Chưa đọc"/>}</span>
                <small className="notification-item-code">{n.MaNoiBo}</small>
                <span className="notification-item-bottom"><small>{formatDate(n.ThoiDiem)}</small>
                  {(overdue||soon)&&<span className={`notification-severity ${overdue?'danger':'warning'}`}>
                    {overdue?'Quá hạn':'Sắp đến hạn'}</span>}</span>
              </span><ChevronRight className="notification-item-chevron" size={16}/>
            </button>;
          })}</div>
          {notifications.length===0&&<p>Chưa có thông báo.</p>}
          <Pagination total={notifications.length} page={notificationPage} onPage={setNotificationPage}/>
        </div>}
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
        {!DEMO&&page==='van-ban-den'?<>
          <DieuHanhVanBanDenPanel token={token} onUpdated={updateAfterOperation} focusId={incomingFocus}/>
          {actor?.capabilities.includes('incoming.manage')&&<LiveDeliveries token={token} direction="in" onUpdated={updateAfterOperation}/>}
        </>:
          documentList(page==='van-ban-di'?'Danh sách văn bản đi':'Danh sách văn bản đến')}
      </>}

      {page==='hanh-trinh'&&<><div className="page-intro compact"><div><div className="overline">MINH BẠCH QUY TRÌNH</div><h1>Hành trình văn bản</h1>
        <p>Xem từng bước chuyển, người đang giữ việc và trạng thái tiếp nhận của từng cơ quan.</p></div></div>
        <div className="journey-workspace"><div className="panel journey-list"><div className="panel-head"><div><h3>Chọn văn bản</h3><p>{filtered.length} văn bản</p></div></div>
          {pageRows(filtered,journeyPage).map(d=><button className={`journey-list-item ${journeyPick===d.id?'chosen':''}`} key={d.id} onClick={()=>pickJourney(d)}>
            <div className="journey-list-top"><small>{d.number||d.code}</small><Status status={d.status} small/></div>
            <strong>{d.title}</strong><div><span><CalendarDays size={14}/>{formatDate(d.createdAt).slice(0,10)}</span><span>{d.to.length} nơi nhận</span></div>
          </button>)}{filtered.length===0&&<EmptyView title="Chưa có dữ liệu" message="Hãy tạo một văn bản mới để bắt đầu." icon={Route}/>}
          <Pagination total={filtered.length} page={journeyPage} onPage={setJourneyPage}/></div>
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
          {DEMO?pageRows(visibleDocs.filter(d=>d.assignee===actor.name||d.assignee.includes(actor.name.split(' ').at(-1)||'___')),taskPage).map(d=><button className="task-row" key={d.id} onClick={()=>openDoc(d)}><div className="task-icon"><FileClock size={20}/></div><div><strong>{d.title}</strong><small>{d.code} · Hạn {formatDate(d.dueAt)}</small></div><Status status={d.status}/><ChevronRight size={18}/></button>)
           :pageRows(workqueue,taskPage).map(t=>{const progress=monitorTasks.find(m=>m.MaTacVu===String(field(t,'MaTacVu')));
             return <button className="task-row" key={String(field(t,'MaTacVu'))} onClick={()=>void openWorkqueueDocument(t)}><div className="task-icon"><FileClock size={20}/></div><div><strong>{String(field(t,'TieuDe'))}</strong>
               <small>{String(field(t,'MaNoiBo'))} · {String(field(t,'Buoc'))} · Hạn: {formatDate(progress?.HanXuLy??undefined)}</small></div>
               <span className={`deadline-pill ${progress?.TinhTrangHan??''}`}>{deadlineLabels[progress?.TinhTrangHan??'']??'Đang xử lý'}</span><ChevronRight size={18}/></button>;})}
          {DEMO&&visibleDocs.filter(d=>d.assignee===actor.name||d.assignee.includes(actor.name.split(' ').at(-1)||'___')).length===0&&<EmptyView title="Không có việc đang chờ" message="Các công việc được giao sẽ hiện tại đây." icon={CheckCheck}/>}
          <Pagination total={DEMO?visibleDocs.filter(d=>d.assignee===actor.name||d.assignee.includes(actor.name.split(' ').at(-1)||'___')).length:workqueue.length} page={taskPage} onPage={setTaskPage}/></div></>}

      {page==='giao-nhan'&&<><div className="page-intro compact"><div><div className="overline">CHUYỂN PHÁT LIÊN CƠ QUAN</div><h1>Giao nhận văn bản</h1><p>Biên nhận được theo dõi độc lập tại mỗi cơ quan nhận.</p></div></div>
        {!DEMO&&<LiveDeliveries token={token} direction="out" onUpdated={updateAfterOperation}/>}
        {DEMO&&<>
        <div className="mini-stats"><span><Network size={17}/>Tổng lượt: <strong>{totalTransfer}</strong></span><span><CheckCircle2 size={17}/>Đã nhận: <strong>{visibleDocs.flatMap(d=>d.transfers).filter(t=>t.status==='DA_TIEP_NHAN').length}</strong></span>
          <span><Clock3 size={17}/>Chờ nhận: <strong>{visibleDocs.flatMap(d=>d.transfers).filter(t=>t.status!=='DA_TIEP_NHAN').length}</strong></span></div>
        <div className="panel deliveries-panel"><div className="panel-head"><h3>Danh sách giao nhận</h3></div>
          {pageRows(visibleDocs.filter(d=>d.transfers.length>0),deliveryPage).map(d=><div className="delivery-group" key={d.id}><div className="delivery-group-head"><div><small>{d.number||d.code}</small><h4>{d.title}</h4></div>
            <button className="secondary small" onClick={()=>openDoc(d)}>Hành trình <ArrowUpRight size={16}/></button></div><TransferCards doc={d}/></div>)}
          {totalTransfer===0&&<EmptyView title="Chưa có lượt chuyển phát" message="Chỉ bản đã ký và phát hành mới được giao đến cơ quan nhận." icon={Network}/>}
          <Pagination total={visibleDocs.filter(d=>d.transfers.length>0).length} page={deliveryPage} onPage={setDeliveryPage}/></div></>}</>}

      {page==='ho-so'&&(DEMO?<><div className="page-intro compact"><div><div className="overline">LƯU TRỮ ĐIỆN TỬ · 0.7</div><h1>Hồ sơ lưu trữ</h1></div></div>
        <EmptyView title="Chế độ minh họa" message="Đăng nhập vào API thật để tạo và quản lý hồ sơ lưu trữ." icon={Archive}/></>
        :<HoSoPage token={token} documents={visibleDocs} onOpen={openDoc}/>)}

      {page==='bao-cao'&&<><div className="page-intro compact"><div><div className="overline">THỐNG KÊ · 0.6.5</div><h1>Báo cáo hiệu suất</h1>
        <p>Chỉ tổng hợp tác vụ văn bản thường thuộc cơ quan; không tự động thay đổi tác vụ.</p></div>
        {!DEMO&&<button className="primary" onClick={()=>downloadApi('/monitor/reports/excel',token,'bao-cao-giam-sat-065.xlsx').catch(e=>setError((e as Error).message))}>Xuất Excel</button>}</div>
        <div className="report-grid">{[
          ['Tổng tác vụ',monitorReports.reduce((s,r)=>s+r.Tong,0)],
          ['Đang chờ',monitorReports.reduce((s,r)=>s+r.DangCho,0)],
          ['Đã xử lý',monitorReports.reduce((s,r)=>s+r.DaXuLy,0)],
          ['Quá hạn',monitorReports.reduce((s,r)=>s+r.QuaHan,0)]
        ].map(([label,value])=><div className="panel report-item" key={label}><span>{label}</span><strong>{value}</strong><div className="report-bar"/></div>)}</div>
        <div className="panel monitor-panel"><div className="panel-head"><h3>Hiệu suất theo người và phòng ban</h3></div>
          <div className="monitor-table-wrap"><table><thead><tr><th>Phòng ban</th><th>Người xử lý</th><th>Tổng</th><th>Đã xử lý</th><th>Quá hạn</th><th>Đúng hạn</th><th>Trung bình</th></tr></thead>
            <tbody>{pageRows(monitorReports,reportPage).map((r,i)=><tr key={`${r.NguoiXuLy}-${i}`}><td>{r.PhongBan}</td><td>{r.NguoiXuLy}</td><td>{r.Tong}</td><td>{r.DaXuLy}</td><td>{r.QuaHan}</td>
              <td>{r.DungHan}</td><td>{r.PhutTrungBinh==null?'—':`${(r.PhutTrungBinh/60).toFixed(1)} giờ`}</td></tr>)}</tbody></table></div>
          {monitorReports.length===0&&<p className="fine-note">Chưa có tác vụ để thống kê.</p>}
          <Pagination total={monitorReports.length} page={reportPage} onPage={setReportPage}/>
        </div></>}

      {page==='giam-sat'&&<><div className="page-intro compact"><div><div className="overline">GIÁM SÁT · 0.6.5</div><h1>Theo dõi tiến độ</h1>
          <p>Chỉ xem bước xử lý, người phụ trách, thời gian chờ và hạn; không tự động chuyển bước.</p></div></div>
        <div className="panel monitor-panel"><div className="panel-head"><h3>Tác vụ trong phạm vi giám sát</h3>
          <label>Lọc hạn xử lý <select value={monitorFilter} onChange={e=>{setMonitorFilter(e.target.value);setMonitorPage(1);}}>
            <option value="ALL">Tất cả</option>{Object.entries(deadlineLabels).map(([value,label])=><option key={value} value={value}>{label}</option>)}
          </select></label></div>
          <div className="monitor-table-wrap"><table><thead><tr><th>Văn bản</th><th>Bước</th><th>Người xử lý</th><th>Giao lúc</th><th>Hạn</th><th>Đã chờ</th><th>Tình trạng</th></tr></thead>
            <tbody>{pageRows(monitoredRows,monitorPage).map(t=><tr key={t.MaTacVu}>
              <td><button className="monitor-link" onClick={()=>void openMonitoredDocument(t.MaVanBan)}>{t.TieuDe}<small>{t.MaNoiBo}</small></button></td>
              <td>{t.Buoc}</td><td>{t.NguoiPhuTrach}<small>{t.TenPhongBan??'Chưa phân phòng'}</small>{t.NguoiKyThay&&<small>Ký thay: {t.NguoiKyThay}</small>}</td>
              <td>{formatDate(t.ThoiDiemTao)}</td><td>{formatDate(t.HanXuLy??undefined)}</td>
              <td>{Math.max(0,t.SoPhutXuLy/60).toFixed(1)} giờ</td><td><span className={`deadline-pill ${t.TinhTrangHan}`}>{deadlineLabels[t.TinhTrangHan]??t.TinhTrangHan}</span></td>
            </tr>)}</tbody></table></div>
          {monitorTasks.length===0&&<p className="fine-note">Chưa có tác vụ để giám sát.</p>}
          <Pagination total={monitoredRows.length} page={monitorPage} onPage={setMonitorPage}/>
        </div></>}

      {page==='quan-tri'&&<><div className="page-intro compact"><div><div className="overline">QUẢN TRỊ CƠ QUAN · 0.6.1</div><h1>Quản trị tài khoản</h1>
        <p>Tạo tài khoản, phân công phòng ban và chức vụ. Quản trị viên không mặc nhiên được xem văn bản nghiệp vụ.</p></div></div>
        {!DEMO?<UserAdministration token={token} username={actor.username} canManagePermissions={isAllowed(actor,'permissions.manage')}/>:
          <div className="panel">Chức năng quản trị tài khoản chỉ sử dụng khi kết nối API thật.</div>}</>}

      {page==='co-cau'&&<><div className="page-intro compact"><div><div className="overline">QUẢN TRỊ CƠ QUAN · 0.6.3</div>
        <h1>Cơ cấu phòng ban</h1><p>Thêm, sửa, sắp xếp thứ bậc và quản lý các phòng trực thuộc cơ quan.</p></div></div>
        {!DEMO?<DepartmentAdministration token={token}/>:
          <div className="panel">Chức năng quản lý cơ cấu phòng ban chỉ sử dụng khi kết nối API thật.</div>}</>}

      {page==='uy-quyen'&&<><div className="page-intro compact"><div><div className="overline">QUẢN TRỊ CƠ QUAN · 0.6.4.2</div>
        <h1>Ủy quyền ký thay</h1><p>Giao quyền ký một tác vụ cụ thể có thời hạn, không thay đổi số bước ký văn bản.</p></div></div>
        {!DEMO?<SigningDelegationAdministration token={token}/>:
          <div className="panel">Chức năng ủy quyền ký chỉ sử dụng khi kết nối API thật.</div>}</>}
    </div>
    </main>
    {currentDoc&&currentDoc.type==='DEN'&&!DEMO&&<ChiTietVanBanDen key={currentDoc.id} doc={currentDoc} token={token}
      onClose={()=>setSelected(null)} onProcess={id=>{setSelected(null);setIncomingFocus(id);setPage('van-ban-den');}}/>}
    {currentDoc&&(currentDoc.type!=='DEN'||DEMO)&&<DocumentDetails key={currentDoc.id} doc={currentDoc} onClose={()=>setSelected(null)} onAction={action} allowed={actor}
      canDecide={DEMO?currentDoc.assignee===actor.name:workqueue.some(t=>String(field(t,'MaVanBan'))===currentDoc.id)}
      taskStep={String(field(workqueue.find(t=>String(field(t,'MaVanBan'))===currentDoc.id)??{},'Buoc')??'')}
      liveTimeline={remoteTimeline} token={token} onUpdated={updateAfterOperation}/>}
    {createOpen&&<NewDocument onClose={()=>setCreateOpen(false)} onCreate={create} live={!DEMO} recipients={agencyOptions}/>}
    {notice&&<div className="toast"><CheckCircle2 size={18}/>{notice}<button aria-label="Đóng thông báo" onClick={()=>setNotice('')}><X size={15}/></button></div>}
  </div>;
}
