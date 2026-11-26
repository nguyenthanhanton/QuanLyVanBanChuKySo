using System.Security.Cryptography;
using System.Text.Json;
using Dapper;

public sealed record PhatHanhVanBan(Guid MaYeuCau);
public sealed record GuiVanBan(Guid MaYeuCau);
public sealed record TuChoiVanBan(string LyDo,Guid MaYeuCau);

public sealed class ChuyenPhatNhan {
    public Guid MaVanBan {get;set;}
    public Guid MaPhatHanh {get;set;}
    public string TrangThai {get;set;}="";
    public string TieuDe {get;set;}="";
    public int MaCoQuanGui {get;set;}
    public int MaCoQuanNhan {get;set;}
}

public static class PhatHanhEndpoints
{
    private static async Task<IResult?> KiemTraKhoiKy(Database db,DichVuKyPdf signer,
        Guid id,int agency,Guid publication)
    {
        using var cn=db.Open();
        var pdf=await cn.QueryFirstOrDefaultAsync<BanPdf>(@"
            SELECT p.MaBan,p.DuongDanLuuTru,p.MaBamSha256
            FROM dbo.BanPdfDaKy p JOIN dbo.BanPhatHanh b
                ON b.MaVanBan=p.MaVanBan AND b.SoPhienBan=p.SoPhienBan
            WHERE b.MaPhatHanh=@publication AND p.MaVanBan=@id
                AND p.MaCoQuan=@agency AND p.ThuTuKy=3 AND p.MaBamSha256=b.MaBamBanCuoi",
            new{publication,id,agency});
        if(pdf==null || !File.Exists(pdf.DuongDanLuuTru))return Results.Conflict(new{message="Không có PDF phát hành."});
        var hash=await DichVuKyPdf.Bam(pdf.DuongDanLuuTru);
        if(hash!=pdf.MaBamSha256)return Results.Conflict(new{message="PDF phát hành bị thay đổi."});
        var signatures=signer.XacMinh(pdf.DuongDanLuuTru);
        if(signatures.Count!=3 || signatures.Any(s=>!s.ToanVenMatMa) ||
           !signatures[^1].BaoPhuToanBoBanHienTai)
            return Results.Conflict(new{message="Chữ ký PDF phát hành không hợp lệ về mật mã."});
        return null;
    }

    public static void MapPhatHanh(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/documents/{id:guid}/publications",async(Guid id,
            PhatHanhVanBan request,HttpContext http,PhanQuyen permissions,
            Database db,DichVuKyPdf signer)=>{
            var(actor,error)=await permissions.YeuCau(http,"publications.issue");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(request.MaYeuCau==Guid.Empty)return Results.BadRequest(new{message="MaYeuCau bắt buộc."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var old=await KySoEndpoints.LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,"PHAT_HANH");
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            var doc=await KySoEndpoints.LayVanBan(cn,tx,id,actor.MaCoQuan.Value);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.TrangThai!="DA_KY"){
                tx.Rollback();return Results.Conflict(new{message="Phải có đủ ba chữ ký hợp lệ trước khi phát hành."});
            }
            var pdf=await KySoEndpoints.LayBan(cn,tx,id,doc.PhienBanHienTai,3);
            if(pdf==null || !File.Exists(pdf.DuongDanLuuTru)){
                tx.Rollback();return Results.Conflict(new{message="Thiếu bản PDF đủ chữ ký."});
            }
            if(await DichVuKyPdf.Bam(pdf.DuongDanLuuTru)!=pdf.MaBamSha256){
                tx.Rollback();return Results.Conflict(new{message="PDF phát hành đã bị thay đổi."});
            }
            IReadOnlyList<DichVuKyPdf.TinhTrangChuKy> signatures;
            try {signatures=signer.XacMinh(pdf.DuongDanLuuTru);}
            catch(Exception ex)when(ex is CryptographicException or IOException or iText.Kernel.Exceptions.PdfException){
                tx.Rollback();return Results.Conflict(new{message="Không xác minh được PDF: "+ex.Message});
            }
            if(signatures.Count!=3 || signatures.Any(s=>!s.ToanVenMatMa) ||
                !signatures[^1].BaoPhuToanBoBanHienTai){
                tx.Rollback();return Results.Conflict(new{message="Ba chữ ký PDF chưa hợp lệ về mật mã."});
            }
            var recipients=(await cn.QueryAsync<int>(@"
                SELECT MaCoQuanNhan FROM dbo.NoiNhanDuKien WHERE MaVanBan=@id",new{id},tx)).ToArray();
            if(recipients.Length==0){tx.Rollback();return Results.Conflict(new{message="Chưa có cơ quan nhận dự kiến."});}
            var active=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.CoQuan WHERE MaCoQuan IN @recipients AND DangHoatDong=1",
                new{recipients},tx);
            if(active!=recipients.Length){tx.Rollback();return Results.Conflict(new{message="Có cơ quan nhận đã ngừng hoạt động."});}
            var attachments=(await cn.QueryAsync<DuongDanTep>(@"
                SELECT f.DuongDanLuuTru,f.TenTep,f.MaBamSha256
                FROM dbo.TepDinhKem f JOIN dbo.PhienBanVanBan v ON v.MaPhienBan=f.MaPhienBan
                WHERE v.MaVanBan=@id AND v.SoPhienBan=@version",
                new{id,version=doc.PhienBanHienTai},tx)).ToArray();
            foreach(var file in attachments){
                if(!File.Exists(file.DuongDanLuuTru) ||
                    await DichVuKyPdf.Bam(file.DuongDanLuuTru)!=file.MaBamSha256){
                    tx.Rollback();return Results.Conflict(new{message="Có tệp đính kèm không còn nguyên vẹn; không thể phát hành."});
                }
            }
            var publication=Guid.NewGuid();
            await cn.ExecuteAsync(@"
                INSERT dbo.BanPhatHanh(MaPhatHanh,MaVanBan,MaCoQuanPhatHanh,MaNguoiPhatHanh,
                    SoPhienBan,MaBamBanCuoi)
                VALUES(@publication,@id,@agency,@user,@version,@hash)",
                new{publication,id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    version=doc.PhienBanHienTai,hash=pdf.MaBamSha256},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.BanPhatHanhTep(MaPhatHanh,MaTep,MaBamSha256)
                SELECT @publication,f.MaTep,f.MaBamSha256 FROM dbo.TepDinhKem f
                JOIN dbo.PhienBanVanBan v ON v.MaPhienBan=f.MaPhienBan
                WHERE v.MaVanBan=@id AND v.SoPhienBan=@version",
                new{publication,id,version=doc.PhienBanHienTai},tx);
            foreach(var recipient in recipients){
                var delivery=Guid.NewGuid();
                await cn.ExecuteAsync(@"
                    INSERT dbo.LuotChuyenVanBan(MaLuotChuyen,MaPhatHanh,MaCoQuanGui,MaCoQuanNhan,
                        MaNguoiGui,TrangThai)
                    VALUES(@delivery,@publication,@agency,@recipient,@user,'CHO_GUI')",
                    new{delivery,publication,agency=actor.MaCoQuan,recipient,user=actor.MaNguoiDung},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,MaLuotChuyen,HanhDong,MoTa,PhamViHienThi)
                    VALUES(@id,@agency,@user,@delivery,'TAO_LUOT_CHUYEN',@message,'GIAO_NHAN')",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,delivery,
                        message=$"Đã tạo lượt chuyển tới cơ quan mã {recipient}; chờ gửi"},tx);
            }
            await cn.ExecuteAsync(@"
                UPDATE dbo.VanBan SET TrangThai='DA_PHAT_HANH',ThoiDiemCapNhat=SYSUTCDATETIME()
                WHERE MaVanBan=@id",new{id},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                VALUES(@id,@agency,@user,'PHAT_HANH',@message,'DA_KY','DA_PHAT_HANH',@version)",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,version=doc.PhienBanHienTai,
                    message=$"Phát hành bản PDF đã ký; {recipients.Length} cơ quan nhận; SHA-256 {pdf.MaBamSha256}"},tx);
            var json=JsonSerializer.Serialize(new{maVanBan=id,maPhatHanh=publication,
                trangThai="DA_PHAT_HANH",soCoQuanNhan=recipients.Length,maBamBanCuoi=pdf.MaBamSha256});
            await KySoEndpoints.GhiBienNhan(cn,tx,actor,request.MaYeuCau,"PHAT_HANH",json);
            tx.Commit();return Results.Content(json,"application/json");
        }).RequireAuthorization();

        app.MapPost("/api/v1/deliveries/{id:guid}/deliver",async(Guid id,GuiVanBan request,
            HttpContext http,PhanQuyen permissions,Database db,DichVuKyPdf signer)=>{
            var(actor,error)=await permissions.YeuCau(http,"publications.deliver");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(request.MaYeuCau==Guid.Empty)return Results.BadRequest();
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var old=await KySoEndpoints.LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,"GUI_VAN_BAN");
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            var delivery=await cn.QuerySingleOrDefaultAsync<ChuyenPhatNhan>(@"
                SELECT v.MaVanBan,l.MaPhatHanh,l.TrangThai,v.TieuDe,l.MaCoQuanGui,l.MaCoQuanNhan
                FROM dbo.LuotChuyenVanBan l WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE l.MaLuotChuyen=@id AND l.MaCoQuanGui=@agency",
                new{id,agency=actor.MaCoQuan},tx);
            if(delivery==null){tx.Rollback();return Results.NotFound();}
            if(delivery.TrangThai!="CHO_GUI"){
                tx.Rollback();return Results.Conflict(new{message="Lượt chuyển không còn ở trạng thái chờ gửi."});
            }
            // Integrity re-check immediately before the outgoing transition.
            var check=await KiemTraKhoiKy(db,signer,delivery.MaVanBan,actor.MaCoQuan.Value,delivery.MaPhatHanh);
            if(check!=null){tx.Rollback();return check;}
            await cn.ExecuteAsync(@"
                UPDATE dbo.LuotChuyenVanBan SET TrangThai='DA_GUI',ThoiDiemGui=SYSUTCDATETIME(),
                    SoLanGui=SoLanGui+1 WHERE MaLuotChuyen=@id",new{id},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,MaLuotChuyen,HanhDong,MoTa,PhamViHienThi)
                VALUES(@doc,@agency,@user,@id,'GUI_VAN_BAN',@message,'GIAO_NHAN')",
                new{doc=delivery.MaVanBan,agency=actor.MaCoQuan,user=actor.MaNguoiDung,id,
                    message=$"Đã gửi bản phát hành tới cơ quan mã {delivery.MaCoQuanNhan}"},tx);
            var json=JsonSerializer.Serialize(new{maLuotChuyen=id,trangThai="DA_GUI"});
            await KySoEndpoints.GhiBienNhan(cn,tx,actor,request.MaYeuCau,"GUI_VAN_BAN",json);
            tx.Commit();return Results.Content(json,"application/json");
        }).RequireAuthorization();

        app.MapGet("/api/v1/deliveries/inbox",async(HttpContext http,PhanQuyen permissions,Database db)=>{
            var(actor,error)=await permissions.YeuCau(http,"deliveries.accept");
            if(error!=null)return error;
            using var cn=db.Open();
            // No sender draft versions, internal comments or internal task identifiers.
            var rows=await cn.QueryAsync(@"
                SELECT l.MaLuotChuyen,l.TrangThai,l.ThoiDiemGui,l.ThoiDiemNhan,l.LyDoTuChoi,
                    v.TieuDe,v.TrichYeu,v.SoPhatHanh,v.KyHieu,v.NgayBanHanh,
                    c.TenCoQuan AS CoQuanGui,p.MaBamBanCuoi
                FROM dbo.LuotChuyenVanBan l
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                JOIN dbo.CoQuan c ON c.MaCoQuan=l.MaCoQuanGui
                WHERE l.MaCoQuanNhan=@agency AND l.TrangThai IN ('DA_GUI','DA_TIEP_NHAN','TU_CHOI')
                ORDER BY l.ThoiDiemGui DESC",new{agency=actor!.MaCoQuan});
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapPost("/api/v1/deliveries/{id:guid}/reject",async(Guid id,TuChoiVanBan request,
            HttpContext http,PhanQuyen permissions,Database db)=>{
            var(actor,error)=await permissions.YeuCau(http,"deliveries.accept");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(request.MaYeuCau==Guid.Empty || string.IsNullOrWhiteSpace(request.LyDo) ||
                request.LyDo.Trim().Length>1000)
                return Results.BadRequest(new{message="Phải có lý do từ chối tối đa 1000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var old=await KySoEndpoints.LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,"TU_CHOI_NHAN");
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            var delivery=await cn.QuerySingleOrDefaultAsync<ChuyenPhatNhan>(@"
                SELECT v.MaVanBan,l.MaPhatHanh,l.TrangThai,v.TieuDe,l.MaCoQuanGui,l.MaCoQuanNhan
                FROM dbo.LuotChuyenVanBan l WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE l.MaLuotChuyen=@id AND l.MaCoQuanNhan=@agency",
                new{id,agency=actor.MaCoQuan},tx);
            if(delivery==null){tx.Rollback();return Results.NotFound();}
            if(delivery.TrangThai!="DA_GUI"){
                tx.Rollback();return Results.Conflict(new{message="Chỉ từ chối văn bản đang chờ tiếp nhận."});
            }
            await cn.ExecuteAsync(@"
                UPDATE dbo.LuotChuyenVanBan SET TrangThai='TU_CHOI',LyDoTuChoi=@reason,
                    ThoiDiemNhan=SYSUTCDATETIME(),MaNguoiNhan=@user WHERE MaLuotChuyen=@id",
                new{id,reason=request.LyDo.Trim(),user=actor.MaNguoiDung},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,MaLuotChuyen,HanhDong,MoTa,PhamViHienThi)
                VALUES(@doc,@agency,@user,@id,'TU_CHOI_NHAN',@message,'GIAO_NHAN')",
                new{doc=delivery.MaVanBan,agency=actor.MaCoQuan,user=actor.MaNguoiDung,id,
                    message=$"Cơ quan nhận từ chối: {request.LyDo.Trim()}"},tx);
            var json=JsonSerializer.Serialize(new{maLuotChuyen=id,trangThai="TU_CHOI",lyDo=request.LyDo.Trim()});
            await KySoEndpoints.GhiBienNhan(cn,tx,actor,request.MaYeuCau,"TU_CHOI_NHAN",json);
            tx.Commit();return Results.Content(json,"application/json");
        }).RequireAuthorization();

        app.MapGet("/api/v1/deliveries/{id:guid}/attachments",async(Guid id,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            var caps=await permissions.DanhSachQuyen(actor);
            using var cn=db.Open();
            var delivery=await cn.QuerySingleOrDefaultAsync<ChuyenPhatNhan>(@"
                SELECT v.MaVanBan,l.MaPhatHanh,l.TrangThai,v.TieuDe,l.MaCoQuanGui,l.MaCoQuanNhan
                FROM dbo.LuotChuyenVanBan l
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan WHERE l.MaLuotChuyen=@id",new{id});
            if(delivery==null)return Results.NotFound();
            var sender=actor.MaCoQuan==delivery.MaCoQuanGui && caps.Contains("publications.deliver");
            var receiver=actor.MaCoQuan==delivery.MaCoQuanNhan && caps.Contains("deliveries.accept")
                && delivery.TrangThai!="CHO_GUI";
            if(!sender&&!receiver)return Results.NotFound();
            var list=await cn.QueryAsync(@"
                SELECT f.MaTep,f.TenTep,f.LoaiNoiDung,f.KichThuoc,s.MaBamSha256
                FROM dbo.BanPhatHanhTep s JOIN dbo.TepDinhKem f ON f.MaTep=s.MaTep
                WHERE s.MaPhatHanh=@publication ORDER BY f.TenTep",
                new{publication=delivery.MaPhatHanh});
            return Results.Ok(list);
        }).RequireAuthorization();

        app.MapGet("/api/v1/deliveries/{id:guid}/attachments/{fileId:guid}",async(Guid id,Guid fileId,
            HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            var caps=await permissions.DanhSachQuyen(actor);
            using var cn=db.Open();
            var detail=await cn.QuerySingleOrDefaultAsync<ChuyenPhatNhan>(@"
                SELECT v.MaVanBan,l.MaPhatHanh,l.TrangThai,v.TieuDe,l.MaCoQuanGui,l.MaCoQuanNhan
                FROM dbo.LuotChuyenVanBan l JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE l.MaLuotChuyen=@id",new{id});
            if(detail==null)return Results.NotFound();
            var sender=actor.MaCoQuan==detail.MaCoQuanGui && caps.Contains("publications.deliver");
            var receiver=actor.MaCoQuan==detail.MaCoQuanNhan && caps.Contains("deliveries.accept")
                && detail.TrangThai!="CHO_GUI";
            if(!sender&&!receiver)return Results.NotFound();
            var file=await cn.QuerySingleOrDefaultAsync<DuongDanTep>(@"
                SELECT f.DuongDanLuuTru,f.TenTep,f.LoaiNoiDung,s.MaBamSha256
                FROM dbo.BanPhatHanhTep s JOIN dbo.TepDinhKem f ON f.MaTep=s.MaTep
                WHERE s.MaPhatHanh=@publication AND s.MaTep=@fileId",
                new{publication=detail.MaPhatHanh,fileId});
            if(file==null)return Results.NotFound();
            if(!File.Exists(file.DuongDanLuuTru) ||
                await DichVuKyPdf.Bam(file.DuongDanLuuTru)!=file.MaBamSha256)
                return Results.Conflict(new{message="Tệp đính kèm phát hành không toàn vẹn."});
            http.Response.Headers["X-Content-Type-Options"]="nosniff";
            if(http.Request.Query.ContainsKey("preview")){
                try {var text=DichVuTepVanBan.XemTruocOffice(file.DuongDanLuuTru);
                    if(text==null)return Results.StatusCode(415);
                    return Results.Text(text,"text/plain; charset=utf-8");}
                catch(InvalidDataException){return Results.BadRequest(new{message="Tệp Office bị lỗi định dạng."});}
            }
            return Results.File(file.DuongDanLuuTru,file.LoaiNoiDung,file.TenTep);
        }).RequireAuthorization();

        app.MapGet("/api/v1/deliveries/{id:guid}/pdf",async(Guid id,HttpContext http,
            PhanQuyen permissions,Database db,DichVuKyPdf signer)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            var rights=await permissions.DanhSachQuyen(actor);
            using var cn=db.Open();
            var row=await cn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT p.MaVanBan,p.MaCoQuanPhatHanh,p.MaPhatHanh,l.MaCoQuanGui,l.MaCoQuanNhan,l.TrangThai,
                    s.DuongDanLuuTru,s.MaBamSha256
                FROM dbo.LuotChuyenVanBan l JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.BanPdfDaKy s ON s.MaVanBan=p.MaVanBan AND s.SoPhienBan=p.SoPhienBan
                    AND s.ThuTuKy=3 AND s.MaBamSha256=p.MaBamBanCuoi
                WHERE l.MaLuotChuyen=@id",new{id});
            if(row==null)return Results.NotFound();
            bool sending=actor.MaCoQuan==(int)row.MaCoQuanGui && rights.Contains("publications.deliver");
            bool receiving=actor.MaCoQuan==(int)row.MaCoQuanNhan && rights.Contains("deliveries.accept")
                && (string)row.TrangThai!="CHO_GUI";
            if(!sending && !receiving)return Results.NotFound();
            string path=row.DuongDanLuuTru;
            string hash=row.MaBamSha256;
            if(!File.Exists(path) || await DichVuKyPdf.Bam(path)!=hash)
                return Results.Conflict(new{message="PDF phát hành không còn toàn vẹn."});
            var verified=signer.XacMinh(path);
            if(verified.Count!=3 || verified.Any(s=>!s.ToanVenMatMa))
                return Results.Conflict(new{message="Không thể xác minh bản PDF phát hành."});
            return Results.File(path,"application/pdf",$"van-ban-phat-hanh-{id:N}.pdf");
        }).RequireAuthorization();
    }
}
