import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { GlobalWorkerOptions, getDocument, type PDFDocumentProxy } from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';

GlobalWorkerOptions.workerSrc = workerUrl;

export type SignaturePosition = { page:number; x:number; y:number; width:number; height:number };
type Props = {
  sourcePath:string; token:string; image:File;
  position:SignaturePosition; onConfirm:(position:SignaturePosition)=>void; onClose:()=>void;
};
const clamp = (value:number,min:number,max:number) => Math.max(min,Math.min(max,value));

export default function SignaturePlacement({sourcePath,token,image,position,onConfirm,onClose}:Props) {
  const canvasRef=useRef<HTMLCanvasElement>(null);
  const surfaceRef=useRef<HTMLDivElement>(null);
  const dragRef=useRef<{mode:'move'|'resize';atX:number;atY:number;start:SignaturePosition}|null>(null);
  const [pdf,setPdf]=useState<PDFDocumentProxy|null>(null);
  const [page,setPage]=useState(position.page);
  const [placement,setPlacement]=useState(position);
  const [imageUrl,setImageUrl]=useState('');
  const [error,setError]=useState('');
  const [loading,setLoading]=useState(true);
  const [size,setSize]=useState({width:0,height:0});

  useEffect(()=>{
    const url=URL.createObjectURL(image);setImageUrl(url);
    return ()=>URL.revokeObjectURL(url);
  },[image]);

  useEffect(()=>{
    let alive=true;let document:PDFDocumentProxy|null=null;
    setLoading(true);setError('');
    fetch('/api/v1'+sourcePath,{headers:{Authorization:`Bearer ${token}`}}).then(async response=>{
      if(!response.ok)throw new Error(`Không tải được PDF để đặt chữ ký (${response.status}).`);
      const data=new Uint8Array(await response.arrayBuffer());
      document=await getDocument({data}).promise;
      if(alive){setPdf(document);setPage(p=>clamp(p,1,document!.numPages));}
      else await document.destroy();
    }).catch(err=>{if(alive)setError((err as Error).message);}).finally(()=>{if(alive)setLoading(false);});
    return ()=>{alive=false;if(document)void document.destroy();};
  },[sourcePath,token]);

  useEffect(()=>{
    if(!pdf)return;
    let active=true;let task:ReturnType<Awaited<ReturnType<PDFDocumentProxy['getPage']>>['render']>|null=null;
    (async()=>{
      const pdfPage=await pdf.getPage(page);
      if(pdfPage.rotate!==0)throw new Error('PDF có trang bị xoay; chưa thể đặt chữ ký lên trang này.');
      const viewport=pdfPage.getViewport({scale:1});
      const scale=Math.min(1.6,760/viewport.width);
      const fit=pdfPage.getViewport({scale});
      const canvas=canvasRef.current;
      if(!active||!canvas)return;
      canvas.width=Math.ceil(fit.width);canvas.height=Math.ceil(fit.height);
      setSize({width:fit.width,height:fit.height});
      const context=canvas.getContext('2d');
      if(!context)throw new Error('Trình duyệt không hỗ trợ Canvas.');
      task=pdfPage.render({canvasContext:context,viewport:fit});
      await task.promise;
    })().catch(err=>{if(active&&err?.name!=='RenderingCancelledException')setError((err as Error).message);});
    return ()=>{active=false;task?.cancel();};
  },[pdf,page]);

  const begin=(event:ReactPointerEvent<HTMLDivElement>,mode:'move'|'resize')=>{
    event.preventDefault();event.stopPropagation();
    dragRef.current={mode,atX:event.clientX,atY:event.clientY,start:placement};
    event.currentTarget.setPointerCapture(event.pointerId);
  };
  const move=(event:ReactPointerEvent<HTMLDivElement>)=>{
    const drag=dragRef.current,rect=surfaceRef.current?.getBoundingClientRect();
    if(!drag||!rect)return;
    const dx=(event.clientX-drag.atX)/rect.width*100;
    const dy=(event.clientY-drag.atY)/rect.height*100;
    if(drag.mode==='move')setPlacement({...drag.start,
      x:clamp(drag.start.x+dx,0,100-drag.start.width),
      y:clamp(drag.start.y+dy,0,100-drag.start.height)});
    else setPlacement({...drag.start,
      width:clamp(drag.start.width+dx,5,100-drag.start.x),
      height:clamp(drag.start.height+dy,3,100-drag.start.y)});
  };

  return <div className="preview-backdrop" onClick={onClose}>
    <div className="preview-window signature-placement-window" onClick={event=>event.stopPropagation()}>
      <div className="preview-header"><strong>Đặt chữ ký trực tiếp trên PDF</strong>
        <button className="icon-button" onClick={onClose} aria-label="Đóng">✕</button></div>
      <p className="placement-hint">Kéo ảnh tới nơi muốn ký; kéo góc dưới bên phải để đổi kích thước. Đây là bản xem trước, <strong>chưa ký số</strong>.</p>
      {error&&<div className="form-error">{error}</div>}
      {loading&&<p className="subtle">Đang mở PDF...</p>}
      <div className="placement-scroll">
        <div className="placement-page" ref={surfaceRef} style={{width:size.width||undefined,height:size.height||undefined}}>
          <canvas ref={canvasRef} className="placement-canvas" />
          {pdf&&imageUrl&&size.width>0&&<div className="placement-stamp"
            style={{left:`${placement.x}%`,top:`${placement.y}%`,width:`${placement.width}%`,height:`${placement.height}%`}}
            onPointerDown={event=>begin(event,'move')} onPointerMove={move}
            onPointerUp={()=>{dragRef.current=null;}} onPointerCancel={()=>{dragRef.current=null;}}>
            <img alt="Hình chữ ký sẽ đặt trên PDF" src={imageUrl} draggable={false}/>
            <div className="placement-resize" title="Kéo để đổi kích thước"
              onPointerDown={event=>begin(event,'resize')} onPointerMove={move}
              onPointerUp={()=>{dragRef.current=null;}} onPointerCancel={()=>{dragRef.current=null;}} />
          </div>}
        </div>
      </div>
      <div className="placement-footer">
        <div className="placement-pages"><button className="secondary small" disabled={page<=1} onClick={()=>setPage(p=>p-1)}>Trang trước</button>
          <span>Trang {page}/{pdf?.numPages??'…'}</span><button className="secondary small" disabled={!pdf||page>=pdf.numPages} onClick={()=>setPage(p=>p+1)}>Trang sau</button></div>
        <button className="primary small" disabled={!pdf||!!error||loading||!size.width}
          onClick={()=>onConfirm({...placement,page})}>Xác nhận vị trí</button>
      </div>
    </div>
  </div>;
}
