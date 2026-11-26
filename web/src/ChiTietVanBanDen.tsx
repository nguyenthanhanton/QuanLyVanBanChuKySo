import {useEffect,useState} from 'react';
import {FileText,Inbox,X} from 'lucide-react';
import type {VanBan} from './types';
import TepDinhKemVanBanDen from './TepDinhKemVanBanDen';

export default function ChiTietVanBanDen({doc,token,onClose,onProcess}:{
  doc:VanBan;token:string;onClose:()=>void;onProcess:(id:string)=>void}){
  const [pdfUrl,setPdfUrl]=useState('');
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  useEffect(()=>{
    let active=true;let objectUrl='';
    setLoading(true);setError('');setPdfUrl('');
    (async()=>{
      try{
        const response=await fetch(`/api/v1/incoming-work/${doc.id}/pdf`,{
          headers:{Authorization:`Bearer ${token}`}});
        if(!response.ok){const problem=await response.json().catch(()=>({}));
          throw new Error(problem.message??(response.status===404?'Không tìm thấy PDF đã phát hành của văn bản đến.':
            response.status===403?'Bạn không có quyền xem PDF văn bản đến.':`Máy chủ trả lỗi ${response.status}.`));}
        const blob=await response.blob();
        objectUrl=URL.createObjectURL(blob);
        if(active)setPdfUrl(objectUrl);
        else URL.revokeObjectURL(objectUrl);
      }catch(e){if(active)setError((e as Error).message);}
      finally{if(active)setLoading(false);}
    })();
    return()=>{active=false;if(objectUrl)URL.revokeObjectURL(objectUrl);};
  },[doc.id,token]);
  return <div className="incoming-work-preview-overlay" role="dialog" aria-modal="true" aria-label={`Chi tiết văn bản đến ${doc.title}`}>
    <div className="incoming-detail-modal">
      <div className="incoming-detail-head">
        <div><small>CHI TIẾT VĂN BẢN ĐẾN · {doc.code}</small><h2>{doc.title}</h2></div>
        <button type="button" className="secondary small" onClick={onClose} aria-label="Đóng chi tiết"><X size={16}/> Đóng</button>
      </div>
      <div className="incoming-detail-content">
        <div className="incoming-detail-section">
          <h3><FileText size={17}/> Văn bản chính đã ký số</h3>
          {loading&&<p className="subtle">Đang tải PDF đã ký...</p>}
          {error&&<p className="form-error" role="alert">{error}</p>}
          {pdfUrl&&<iframe src={pdfUrl} title={`PDF đã ký — ${doc.title}`}/>}
        </div>
        <div className="incoming-detail-side">
          <h3><Inbox size={17}/> Thông tin văn bản</h3>
          <p><strong>Số đến:</strong> {doc.code.replace(/^DEN-/, '')}</p>
          <p><strong>Tiêu đề:</strong> {doc.title}</p>
          <div className="incoming-detail-attachments"><h3>Tệp đính kèm khi phát hành</h3>
            <TepDinhKemVanBanDen url={`/api/v1/incoming-work/${doc.id}/attachments`} token={token}/>
          </div>
          <button type="button" className="primary" onClick={()=>onProcess(doc.id)}>Đi đến điều hành xử lý</button>
        </div>
      </div>
    </div>
  </div>;
}
