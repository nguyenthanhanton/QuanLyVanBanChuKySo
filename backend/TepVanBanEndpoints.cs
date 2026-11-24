using Dapper;

public sealed class VanBanBanThao {
    public Guid MaVanBan {get;set;}
    public string TieuDe {get;set;}="";
    public string? TrichYeu {get;set;}
    public string TrangThai {get;set;}="";
    public int PhienBanHienTai {get;set;}
    public byte[] PhienBanDong {get;set;}=[];
}

public static class TepVanBanEndpoints
{
    public static void MapTepVanBan(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/documents/{id:guid}/attachments",async(Guid id,HttpContext http,
            PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"documents.read");
            if(error!=null)return error;
            if(!await VanBanEndpoints.CoQuyenXem(db,actor!,id))return Results.NotFound();
            using var cn=db.Open();
            var files=await cn.QueryAsync(@"
                SELECT f.MaTep,f.TenTep,f.LoaiNoiDung,f.KichThuoc,f.MaBamSha256,p.SoPhienBan
                FROM dbo.TepDinhKem f
                JOIN dbo.PhienBanVanBan p ON p.MaPhienBan=f.MaPhienBan
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE p.MaVanBan=@id AND v.MaCoQuan=@agency AND p.SoPhienBan=v.PhienBanHienTai
                ORDER BY f.ThoiDiemTao,f.TenTep",new{id,agency=actor!.MaCoQuan});
            return Results.Ok(files);
        }).RequireAuthorization();

        // Draft update = new immutable revision. Omitted files are inherited,
        // removed file IDs are excluded, added files are stored under new IDs.
        app.MapPost("/api/v1/documents/{id:guid}/draft-files",async(Guid id,HttpContext http,
            PhanQuyen permission,Database db,IConfiguration config)=>{
            var(actor,error)=await permission.YeuCau(http,"documents.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(!http.Request.HasFormContentType)return Results.BadRequest(new{message="Cần multipart/form-data."});
            var form=await http.Request.ReadFormAsync();
            var files=form.Files.GetFiles("tepDinhKem").ToArray();
            var bad=await DichVuTepVanBan.KiemTraDanhSach(files);
            if(bad!=null)return Results.BadRequest(new{message=bad});
            var removeRaw=form["maTepLoaiBo"].ToString();
            var removeParts=removeRaw.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if(removeParts.Length>20 || removeParts.Any(x=>!Guid.TryParse(x,out _)))
                return Results.BadRequest(new{message="Danh sách mã tệp loại bỏ không hợp lệ."});
            var removed=removeParts.Select(Guid.Parse).Distinct().ToArray();
            var versionRaw=form["phienBanDong"].ToString();
            byte[] expected;
            try {expected=Convert.FromBase64String(versionRaw);}catch{return Results.BadRequest(new{message="Thiếu PhienBanDong hợp lệ."});}
            if(expected.Length!=8)return Results.BadRequest(new{message="PhienBanDong phải có 8 byte."});
            var notes=form["trichYeu"].ToString();
            if(notes.Length>10000)return Results.BadRequest(new{message="Trích yếu quá dài (tối đa 10.000 ký tự)."});
            var reason=form["lyDo"].ToString().Trim();
            if(reason.Length>1000)return Results.BadRequest(new{message="Lý do sửa tối đa 1000 ký tự."});
            var root=config["Storage:RootPath"];
            if(string.IsNullOrWhiteSpace(root))return Results.Problem("Chưa cấu hình Storage__RootPath.",statusCode:500);
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var doc=await cn.QuerySingleOrDefaultAsync<VanBanBanThao>(@"
                SELECT MaVanBan,TieuDe,TrichYeu,TrangThai,PhienBanHienTai,PhienBanDong
                FROM dbo.VanBan WITH(UPDLOCK,HOLDLOCK)
                WHERE MaVanBan=@id AND MaCoQuan=@agency AND MaNguoiTao=@user",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung},tx);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.TrangThai is not ("NHAP" or "TRA_SUA")){
                tx.Rollback();return Results.Conflict(new{message="Chỉ chỉnh sửa tệp khi văn bản ở trạng thái nháp hoặc trả sửa."});
            }
            if(!doc.PhienBanDong.SequenceEqual(expected)){
                tx.Rollback();return Results.Conflict(new{message="Phiên bản đã thay đổi. Tải lại trước khi sửa."});
            }
            var existing=(await cn.QueryAsync<Guid>(@"
                SELECT f.MaTep FROM dbo.TepDinhKem f
                JOIN dbo.PhienBanVanBan p ON p.MaPhienBan=f.MaPhienBan
                WHERE p.MaVanBan=@id AND p.SoPhienBan=@version",
                new{id,version=doc.PhienBanHienTai},tx)).ToArray();
            if(removed.Except(existing).Any()){
                tx.Rollback();return Results.BadRequest(new{message="Có tệp loại bỏ không thuộc phiên bản hiện tại."});
            }
            var kept=existing.Length-removed.Length;
            if(kept+files.Length is < 1 or > DichVuTepVanBan.GioiHanTep){
                tx.Rollback();return Results.BadRequest(new{message="Phiên bản phải có 1–20 tệp đính kèm."});
            }
            var folder=Path.Combine(root,actor.MaCoQuan.Value.ToString(),id.ToString("N"));
            var saved=new List<DichVuTepVanBan.TapTinDaLuu>();
            try {
                foreach(var f in files)saved.Add(await DichVuTepVanBan.Luu(f,folder));
                var nextVersion=doc.PhienBanHienTai+1;
                var versionId=Guid.NewGuid();
                await cn.ExecuteAsync(@"
                    INSERT dbo.PhienBanVanBan(MaPhienBan,MaVanBan,SoPhienBan,TieuDe,TrichYeu,MaNguoiTao,LyDoThayDoi)
                    VALUES(@versionId,@id,@nextVersion,@title,@notes,@user,@reason)",
                    new{versionId,id,nextVersion,title=doc.TieuDe,notes,user=actor.MaNguoiDung,
                        reason=string.IsNullOrWhiteSpace(reason)?"Cập nhật tệp đính kèm/trích yếu":reason},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.TepDinhKem(MaCoQuan,MaPhienBan,TenTep,LoaiNoiDung,KichThuoc,DuongDanLuuTru,MaBamSha256)
                    SELECT f.MaCoQuan,@versionId,f.TenTep,f.LoaiNoiDung,f.KichThuoc,f.DuongDanLuuTru,f.MaBamSha256
                    FROM dbo.TepDinhKem f JOIN dbo.PhienBanVanBan p ON p.MaPhienBan=f.MaPhienBan
                    WHERE p.MaVanBan=@id AND p.SoPhienBan=@previous
                      AND f.MaTep NOT IN @removed",
                    new{versionId,id,previous=doc.PhienBanHienTai,removed},tx);
                foreach(var item in saved)await cn.ExecuteAsync(@"
                    INSERT dbo.TepDinhKem(MaTep,MaCoQuan,MaPhienBan,TenTep,LoaiNoiDung,KichThuoc,DuongDanLuuTru,MaBamSha256)
                    VALUES(@fileId,@agency,@versionId,@filename,@mime,@length,@path,@sha)",
                    new{fileId=item.MaTep,agency=actor.MaCoQuan,versionId,filename=item.TenTep,
                        mime=item.LoaiNoiDung,length=item.KichThuoc,path=item.DuongDanLuuTru,sha=item.MaBamSha256},tx);
                var rows=await cn.ExecuteAsync(@"
                    UPDATE dbo.VanBan SET TrichYeu=@notes,PhienBanHienTai=@nextVersion,TrangThai='NHAP',
                        ThoiDiemCapNhat=SYSUTCDATETIME()
                    WHERE MaVanBan=@id AND MaCoQuan=@agency AND PhienBanDong=@expected",
                    new{notes,nextVersion,id,agency=actor.MaCoQuan,expected},tx);
                if(rows!=1){tx.Rollback();foreach(var item in saved)File.Delete(item.DuongDanLuuTru);
                    return Results.Conflict(new{message="Phiên bản bị thay đổi đồng thời."});}
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                    VALUES(@id,@agency,@user,'SUA_TEP_DINH_KEM',@description,@before,'NHAP',@nextVersion)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,before=doc.TrangThai,nextVersion,
                        description=$"Phiên bản {nextVersion}: thêm {files.Length}, bỏ {removed.Length} tệp; {reason}"},tx);
                tx.Commit();return Results.Ok(new{maVanBan=id,phienBan=nextVersion,
                    soTep=kept+files.Length,tieuDe=doc.TieuDe});
            }catch{
                tx.Rollback();
                foreach(var item in saved)if(File.Exists(item.DuongDanLuuTru))File.Delete(item.DuongDanLuuTru);
                throw;
            }
        }).RequireAuthorization().DisableAntiforgery();
    }
}
