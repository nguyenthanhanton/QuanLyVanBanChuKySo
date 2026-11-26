import {useEffect,useState} from 'react';

type Tep={maTep:string;tenTep:string;loaiNoiDung:string;kichThuoc:number};
type XemTruoc={url:string;ten:string;loai:string;noiDung?:string};

// Chi doc tap tin dong bang tai thoi diem phat hanh; API kiem tra quyen va ma bam moi lan truy cap.
export default function TepDinhKemVanBanDen({url,token}:{url:string;token:string}){
  const [opened,setOpened]=useState(false);
  const [files,setFiles]=useState<Tep[]>([]);
  const [preview,setPreview]=useState<XemTruoc|null>(null);
  const [error,setError]=useState('');
  const [loading,setLoading]=useState(false);
  useEffect(()=>{setOpened(false);setFiles([]);setError('');setPreview(null);},[url]);
  useEffect(()=>()=>{if(preview?.url)URL.revokeObjectURL(preview.url);},[preview]);
  const request=async(path:string)=>{
    const response=await fetch(path,{headers:{Authorization:`Bearer ${token}`}});
    if(!response.ok){const body=await response.json().catch(()=>({}));
      throw new Error(body.message??`Không truy cập được tệp (${response.status}).`);}
    return response;
  };
  const toggle=async()=>{
    if(opened){setOpened(false);return;}
    setLoading(true);setError('');
    try{const response=await request(url);
      const items=await response.json() as Record<string,unknown>[];
      setFiles(items.map(x=>({maTep:String(x.maTep??x.MaTep),tenTep:String(x.tenTep??x.TenTep),
        loaiNoiDung:String(x.loaiNoiDung??x.LoaiNoiDung??''),kichThuoc:Number(x.kichThuoc??x.KichThuoc??0)})));
      setOpened(true);
    }catch(e){setError((e as Error).message);}finally{setLoading(false);}
  };
  const openFile=async(file:Tep,download:boolean)=>{
    setLoading(true);setError('');
    try{
      const path=`${url}/${file.maTep}`;
      const isPdf=file.loaiNoiDung==='application/pdf'||file.tenTep.toLowerCase().endsWith('.pdf');
      const isImage=/^image\/(png|jpeg|webp|gif)$/.test(file.loaiNoiDung);
      if(!download&&!isPdf&&!isImage){
        const response=await request(`${path}?preview=1`);
        setPreview({url:'',ten:file.tenTep,loai:'text',noiDung:await response.text()});return;
      }
      const blob=await(await request(path)).blob();
      const objectUrl=URL.createObjectURL(blob);
      if(download){const link=document.createElement('a');link.href=objectUrl;link.download=file.tenTep;
        document.body.append(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(objectUrl),60000);
      }else setPreview({url:objectUrl,ten:file.tenTep,loai:isPdf?'pdf':'image'});
    }catch(e){setError((e as Error).message);}finally{setLoading(false);}
  };
  return <div className="incoming-attachments">
    <button type="button" className="secondary small" disabled={loading} onClick={toggle}>
      {loading?'Đang tải...':opened?'Ẩn tệp đính kèm':'Xem tệp đính kèm'}</button>
    {opened&&<div className="incoming-attachments-list">
      {files.length===0?<p className="subtle">Văn bản này không có tệp đính kèm khi phát hành.</p>:
        files.map(file=><div key={file.maTep} className="incoming-attachment-row">
          <span title={file.tenTep}>{file.tenTep} ({Math.ceil(file.kichThuoc/1024)} KB)</span>
          <button type="button" className="secondary small" disabled={loading} onClick={()=>openFile(file,false)}>Xem</button>
          <button type="button" className="secondary small" disabled={loading} onClick={()=>openFile(file,true)}>Tải xuống</button>
        </div>)}
    </div>}
    {error&&<p className="form-error" role="alert">{error}</p>}
    {preview&&<div className="incoming-work-preview-overlay" role="dialog" aria-modal="true" aria-label={`Xem tệp ${preview.ten}`}>
      <div className="incoming-work-preview"><div className="incoming-work-preview-head">
        <strong>{preview.ten}</strong><button type="button" className="secondary small" onClick={()=>setPreview(null)}>Đóng</button></div>
        {preview.loai==='pdf'?<iframe src={preview.url} title={preview.ten}/>:
          preview.loai==='image'?<div className="incoming-attachment-image"><img src={preview.url} alt={preview.ten}/></div>:
            <pre className="incoming-attachment-text">{preview.noiDung}</pre>}
      </div>
    </div>}
  </div>;
}
