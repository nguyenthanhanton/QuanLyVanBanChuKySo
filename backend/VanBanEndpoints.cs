using System.Security.Cryptography;
using Dapper;

public sealed record SuaVanBan(string TieuDe, string? TrichYeu, string PhienBanDong, string? LyDo);
public sealed class BanGhiVanBan { public string TrangThai { get; set; } = ""; public int PhienBanHienTai { get; set; } public string TieuDe {get;set;}=""; }
public sealed class CauHinhBanGhi { public int? MaPhongBan { get; set; } public Guid? MaCauHinh { get; set; } }
public sealed class DuongDanTep { public string DuongDanLuuTru { get; set; } = ""; public string TenTep { get; set; } = ""; public string LoaiNoiDung {get;set;}=""; public string MaBamSha256 {get;set;}=""; }

public static class VanBanEndpoints
{
    // Bo loc pham vi su dung cho moi duong doc; khong dua vao an menu de bao mat.
    // Tai lieu MAT chi xem khi la nguoi tao hoac nguoi duoc giao dich danh.
    private const string PhamViVanBan = @"
      AND (
        v.MaNguoiTao=@user
        OR EXISTS(SELECT 1 FROM dbo.TacVuXuLy t
                  WHERE t.MaVanBan=v.MaVanBan AND t.MaNguoiDuocGiao=@user)
        OR (v.DoMat='THUONG' AND EXISTS(
              SELECT 1 FROM dbo.NguoiDungVaiTro uv
              JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
              WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency
                AND r.MaDinhDanh IN ('VAN_THU','LANH_DAO')))
        OR (v.DoMat='THUONG' AND v.MaPhongBan=(SELECT MaPhongBan FROM dbo.NguoiDung WHERE MaNguoiDung=@user)
            AND EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro uv
              JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
              WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency AND r.MaDinhDanh='TRUONG_PHONG'))
      )";

    internal static async Task<bool> CoQuyenXem(Database db, NguoiThucHien actor, Guid id)
    {
        using var cn=db.Open();
        var count=await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM dbo.VanBan v
            WHERE v.MaVanBan=@id AND v.MaCoQuan=@agency" + PhamViVanBan,
            new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung});
        return count>0;
    }

    public static void MapVanBan(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/agencies/recipients",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var actor=await permission.NguoiHienTai(http);
            if(actor==null)return Results.Unauthorized();
            using var cn=db.Open();
            var list=await cn.QueryAsync(@"
                SELECT MaCoQuan,MaDinhDanh,TenCoQuan FROM dbo.CoQuan
                WHERE DangHoatDong=1 AND MaCoQuan<>@agency ORDER BY TenCoQuan",
                new{agency=actor.MaCoQuan});
            return Results.Ok(list);
        }).RequireAuthorization();

        var route = app.MapGroup("/api/v1/documents").RequireAuthorization();

        route.MapGet("/", async (HttpContext http, PhanQuyen permission, Database db,
            string? timKiem, string? trangThai, int? trang) => {
            var (actor, error) = await permission.YeuCau(http, "documents.read");
            if (error != null) return error;
            if (actor!.MaCoQuan is null) return Results.Forbid();
            var soTrang = Math.Clamp(trang ?? 1, 1, 100_000);
            using var cn = db.Open();
            var rows = await cn.QueryAsync(@"
                SELECT v.MaVanBan, v.MaNoiBo, v.TieuDe, v.TrichYeu, v.TrangThai,
                       v.DoMat, v.DoKhan, v.LoaiVanBan, v.PhienBanHienTai, v.SoPhatHanh,
                       v.KyHieu, v.NgayBanHanh, v.ThoiDiemTao, v.ThoiDiemCapNhat,
                       n.HoTen AS NguoiTao, v.PhienBanDong
                FROM dbo.VanBan v JOIN dbo.NguoiDung n ON n.MaNguoiDung=v.MaNguoiTao
                WHERE v.MaCoQuan=@agency
                  AND (@timKiem IS NULL OR v.TieuDe LIKE '%' + @timKiem + '%' OR v.MaNoiBo LIKE '%' + @timKiem + '%')
                  AND (@trangThai IS NULL OR v.TrangThai=@trangThai)
                " + PhamViVanBan + @"
                ORDER BY v.ThoiDiemTao DESC
                OFFSET @offset ROWS FETCH NEXT 20 ROWS ONLY",
                new { agency=actor.MaCoQuan,user=actor.MaNguoiDung,timKiem, trangThai, offset=(soTrang-1)*20 });
            return Results.Ok(rows.Select(row => (object)row));
        });

        route.MapGet("/{id:guid}", async (Guid id, HttpContext http, PhanQuyen permission, Database db) => {
            var (actor, error) = await permission.YeuCau(http, "documents.read");
            if (error != null) return error;
            if(!await CoQuyenXem(db,actor!,id))return Results.NotFound();
            using var cn = db.Open();
            var doc = await cn.QuerySingleOrDefaultAsync(@"
                SELECT v.MaVanBan, v.MaNoiBo, v.TieuDe, v.TrichYeu, v.TrangThai,
                       v.DoMat, v.DoKhan, v.LoaiVanBan, v.PhienBanHienTai, v.SoPhatHanh,
                       v.KyHieu, v.NgayBanHanh, v.ThoiDiemTao, v.PhienBanDong,
                       n.HoTen AS NguoiTao, c.TenCoQuan
                FROM dbo.VanBan v
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=v.MaNguoiTao
                JOIN dbo.CoQuan c ON c.MaCoQuan=v.MaCoQuan
                WHERE v.MaVanBan=@id AND v.MaCoQuan=@agency", new { id, agency=actor!.MaCoQuan });
            return doc == null ? Results.NotFound() : Results.Ok(doc);
        });

        route.MapPost("/", async (HttpContext http, PhanQuyen permission, Database db, IConfiguration config) => {
            var (actor, error) = await permission.YeuCau(http, "documents.manage");
            if (error != null) return error;
            if (actor!.MaCoQuan is null) return Results.Forbid();
            if (!http.Request.HasFormContentType)
                return Results.BadRequest(new { message="Yêu cầu multipart/form-data, tệp đính kèm và các trường văn bản." });

            var form = await http.Request.ReadFormAsync();
            var title = form["tieuDe"].ToString().Trim();
            var notes = form["trichYeu"].ToString().Trim();
            var degree = form["doMat"].ToString();
            var urgency = form["doKhan"].ToString();
            var recipientRaw = form["maCoQuanNhan"].ToString();
            var mainFile=form.Files.GetFile("tepVanBanChinh") ?? form.Files.GetFile("tepPdf");
            var attachments=form.Files.GetFiles("tepDinhKem").ToArray();
            var files=(mainFile==null?Array.Empty<IFormFile>():new[]{mainFile}).Concat(attachments).ToArray();
            if (title.Length is < 3 or > 300) return Results.BadRequest(new { message="Tiêu đề phải có 3–300 ký tự." });
            if (!new[] {"THUONG", "MAT", "TOI_MAT", "TUYET_MAT"}.Contains(degree))
                return Results.BadRequest(new { message="Độ mật không hợp lệ." });
            if (degree != "THUONG")
                return Results.Json(new { message="Chưa cấu hình thẩm quyền tạo văn bản mật trong bản nền tảng." },statusCode:403);
            if (!new[] {"THUONG", "KHAN", "THUONG_KHAN", "HOA_TOC"}.Contains(urgency))
                return Results.BadRequest(new { message="Độ khẩn không hợp lệ." });
            var recipientValues=recipientRaw.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
            if(recipientValues.Length>20 || recipientValues.Any(x=>!int.TryParse(x,out var n)||n<=0))
                return Results.BadRequest(new{message="Có tối đa 20 mã cơ quan nhận hợp lệ."});
            var recipients=recipientValues.Select(int.Parse).Distinct().ToArray();
            if(mainFile==null)return Results.BadRequest(new{message="Phải chọn văn bản chính dạng PDF để ký số."});
            if(!Path.GetExtension(mainFile.FileName).Equals(".pdf",StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new{message="Văn bản chính phải là PDF; Word/Excel đặt ở mục tệp đính kèm."});
            var fileError=await DichVuTepVanBan.KiemTraDanhSach(files);
            if(fileError!=null)return Results.BadRequest(new{message=fileError});

            var root = config["Storage:RootPath"];
            if (string.IsNullOrWhiteSpace(root))
                return Results.Problem("Chưa cấu hình Storage__RootPath.", statusCode:500);
            var id = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var folder = Path.Combine(root, actor.MaCoQuan.Value.ToString(), id.ToString("N"));
            var saved = new List<DichVuTepVanBan.TapTinDaLuu>();
            try {
                foreach(var file in files)saved.Add(await DichVuTepVanBan.Luu(file,folder));
                using var cn = db.Open();
                await cn.OpenAsync();
                using var tx = cn.BeginTransaction();
                if(recipients.Length>0){
                    var exists=await cn.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(1) FROM dbo.CoQuan
                        WHERE MaCoQuan IN @recipients AND MaCoQuan<>@agency AND DangHoatDong=1",
                        new{recipients,agency=actor.MaCoQuan},tx);
                    if(exists!=recipients.Length){
                        tx.Rollback();
                        foreach(var item in saved)if(File.Exists(item.DuongDanLuuTru))File.Delete(item.DuongDanLuuTru);
                        return Results.BadRequest(new{message="Có cơ quan nhận không tồn tại/đã ngừng hoặc trùng cơ quan gửi."});
                    }
                }
                var owner=await cn.QuerySingleAsync<CauHinhBanGhi>(@"
                    SELECT TOP 1 n.MaPhongBan,c.MaCauHinh
                    FROM dbo.NguoiDung n
                    LEFT JOIN dbo.CauHinhQuyTrinh c ON c.MaCoQuan=n.MaCoQuan AND c.DangSuDung=1
                    WHERE n.MaNguoiDung=@user ORDER BY c.SoPhienBan DESC",
                    new{user=actor.MaNguoiDung},tx);
                var code = "VB-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + id.ToString("N")[..8].ToUpperInvariant();
                await cn.ExecuteAsync(@"
                    INSERT dbo.VanBan (MaVanBan,MaCoQuan,MaPhongBan,MaCauHinh,MaNguoiTao,MaNoiBo,LoaiVanBan,TieuDe,TrichYeu,DoMat,DoKhan)
                    VALUES (@id,@agency,@department,@workflow,@user,@code,'DI',@title,@notes,@degree,@urgency)",
                    new { id, agency=actor.MaCoQuan,department=owner.MaPhongBan,workflow=owner.MaCauHinh,
                          user=actor.MaNguoiDung, code, title, notes, degree, urgency }, tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.PhienBanVanBan (MaPhienBan,MaVanBan,SoPhienBan,TieuDe,TrichYeu,MaNguoiTao, LyDoThayDoi)
                    VALUES (@versionId,@id,1,@title,@notes,@user,N'Tạo bản dự thảo')",
                    new { versionId,id,title,notes,user=actor.MaNguoiDung }, tx);
                foreach(var recipient in recipients)await cn.ExecuteAsync(@"
                    INSERT dbo.NoiNhanDuKien(MaVanBan,MaCoQuanNhan)VALUES(@id,@recipient)",new{id,recipient},tx);
                foreach(var item in saved)await cn.ExecuteAsync(@"
                    INSERT dbo.TepDinhKem (MaTep,MaCoQuan,MaPhienBan,TenTep,LoaiNoiDung,KichThuoc,DuongDanLuuTru,MaBamSha256,LaVanBanChinh)
                    VALUES (@fileId,@agency,@versionId,@filename,@mime,@length,@finalPath,@sha,@isMain)",
                    new { fileId=item.MaTep,agency=actor.MaCoQuan,versionId,filename=item.TenTep,
                        mime=item.LoaiNoiDung,length=item.KichThuoc,finalPath=item.DuongDanLuuTru,sha=item.MaBamSha256,
                        isMain=item.MaTep==saved[0].MaTep }, tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan (MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiSau,SoPhienBan)
                    VALUES (@id,@agency,@user,'TAO_MOI',N'Tạo văn bản dự thảo','NHAP',1)",
                    new { id,agency=actor.MaCoQuan,user=actor.MaNguoiDung },tx);
                tx.Commit();
                return Results.Created($"/api/v1/documents/{id}",new { maVanBan=id,maNoiBo=code, phienBan=1 });
            } catch {
                foreach(var item in saved)if(File.Exists(item.DuongDanLuuTru))File.Delete(item.DuongDanLuuTru);
                throw;
            }
        });

        route.MapGet("/{id:guid}/revisions", async (Guid id, HttpContext http, PhanQuyen permission, Database db) => {
            var (actor, error) = await permission.YeuCau(http,"documents.read");
            if(error != null) return error;
            if(!await CoQuyenXem(db,actor!,id))return Results.NotFound();
            using var cn=db.Open();
            var revisions=await cn.QueryAsync(@"
                SELECT p.MaPhienBan,p.SoPhienBan,p.TieuDe,p.TrichYeu,p.LyDoThayDoi,p.ThoiDiemTao,
                       n.HoTen AS NguoiTao,t.MaTep,t.TenTep,t.MaBamSha256,t.LaVanBanChinh
                FROM dbo.PhienBanVanBan p JOIN dbo.NguoiDung n ON n.MaNguoiDung=p.MaNguoiTao
                LEFT JOIN dbo.TepDinhKem t ON t.MaPhienBan=p.MaPhienBan
                WHERE p.MaVanBan=@id ORDER BY p.SoPhienBan DESC", new {id});
            return Results.Ok(revisions);
        });

        route.MapPut("/{id:guid}", async (Guid id, SuaVanBan req, HttpContext http, PhanQuyen permission, Database db) => {
            var (actor, error) = await permission.YeuCau(http, "documents.manage");
            if (error != null) return error;
            if (req.TieuDe.Trim().Length is < 3 or > 300) return Results.BadRequest();
            byte[] expected;
            try { expected=Convert.FromBase64String(req.PhienBanDong); } catch {return Results.BadRequest(new {message="PhienBanDong không hợp lệ."});}
            using var cn=db.Open();
            await cn.OpenAsync();
            using var tx=cn.BeginTransaction();
            var doc=await cn.QuerySingleOrDefaultAsync<BanGhiVanBan>(@"
                SELECT PhienBanHienTai,TrangThai,TieuDe FROM dbo.VanBan WITH (UPDLOCK)
                WHERE MaVanBan=@id AND MaCoQuan=@agency AND MaNguoiTao=@user",
                new {id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung},tx);
            if (doc==null) {tx.Rollback();return Results.NotFound();}
            if (doc.TrangThai!="NHAP" && doc.TrangThai!="TRA_SUA") {tx.Rollback();return Results.Conflict(new {message="Chỉ sửa được văn bản nháp hoặc bị trả sửa."});}
            if(!string.Equals(req.TieuDe.Trim(),doc.TieuDe,StringComparison.Ordinal)){
                tx.Rollback();return Results.Conflict(new{message="Tiêu đề văn bản đã tạo không được thay đổi."});
            }
            var modified=await cn.ExecuteAsync(@"
                UPDATE dbo.VanBan SET TrichYeu=@notes,PhienBanHienTai=PhienBanHienTai+1,
                    TrangThai='NHAP',ThoiDiemCapNhat=SYSUTCDATETIME()
                WHERE MaVanBan=@id AND MaCoQuan=@agency AND PhienBanDong=@expected",
                new {id,agency=actor.MaCoQuan,notes=req.TrichYeu,expected},tx);
            if(modified==0){tx.Rollback();return Results.Conflict(new {message="Có người đã sửa văn bản. Hãy tải lại."});}
            var ver=doc.PhienBanHienTai+1;
            var versionId=Guid.NewGuid();
            await cn.ExecuteAsync(@"
                INSERT dbo.PhienBanVanBan (MaPhienBan,MaVanBan,SoPhienBan,TieuDe,TrichYeu,MaNguoiTao,LyDoThayDoi)
                VALUES (@versionId,@id,@ver,@title,@notes,@user,@reason)",
                new {versionId,id,ver,title=doc.TieuDe,notes=req.TrichYeu,user=actor.MaNguoiDung,reason=req.LyDo},tx);
            // Snapshot file duoc ke thua nhung khong ghi de ban truoc.
            await cn.ExecuteAsync(@"
                INSERT dbo.TepDinhKem (MaCoQuan,MaPhienBan,TenTep,LoaiNoiDung,KichThuoc,DuongDanLuuTru,MaBamSha256,LaVanBanChinh)
                SELECT MaCoQuan,@versionId,TenTep,LoaiNoiDung,KichThuoc,DuongDanLuuTru,MaBamSha256,LaVanBanChinh
                FROM dbo.TepDinhKem WHERE MaPhienBan=(SELECT MaPhienBan FROM dbo.PhienBanVanBan WHERE MaVanBan=@id AND SoPhienBan=@prev)",
                new{versionId,id,prev=doc.PhienBanHienTai},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiSau,SoPhienBan)
                VALUES(@id,@agency,@user,'SUA_BAN_THAO',@message,'NHAP',@ver)",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,ver,message=req.LyDo??"Lưu phiên bản mới"},tx);
            tx.Commit();
            return Results.Ok(new{maVanBan=id,phienBan=ver});
        });

        route.MapGet("/{id:guid}/files/{fileId:guid}", async (Guid id,Guid fileId,HttpContext http,
            PhanQuyen permission,Database db) => {
            var(actor,error)=await permission.YeuCau(http,"documents.read");
            if(error!=null)return error;
            if(!await CoQuyenXem(db,actor!,id))return Results.NotFound();
            using var cn=db.Open();
            var file=await cn.QuerySingleOrDefaultAsync<DuongDanTep>(@"
                SELECT TOP 1 t.DuongDanLuuTru,t.TenTep,t.LoaiNoiDung,t.MaBamSha256 FROM dbo.TepDinhKem t
                JOIN dbo.PhienBanVanBan p ON p.MaPhienBan=t.MaPhienBan
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE t.MaTep=@fileId AND v.MaVanBan=@id AND v.MaCoQuan=@agency",new{id,fileId,agency=actor!.MaCoQuan});
            if(file==null || !File.Exists(file.DuongDanLuuTru))return Results.NotFound();
            if(await DichVuKyPdf.Bam(file.DuongDanLuuTru)!=file.MaBamSha256)
                return Results.Conflict(new{message="Tệp đính kèm không còn toàn vẹn."});
            http.Response.Headers["X-Content-Type-Options"]="nosniff";
            if(http.Request.Query.ContainsKey("preview")){
                try {var text=DichVuTepVanBan.XemTruocOffice(file.DuongDanLuuTru);
                    if(text==null)return Results.StatusCode(415);
                    return Results.Text(text,"text/plain; charset=utf-8");}
                catch(InvalidDataException){return Results.BadRequest(new{message="Tệp Office bị lỗi định dạng."});}
            }
            return Results.File(file.DuongDanLuuTru,file.LoaiNoiDung,file.TenTep);
        });

        route.MapGet("/{id:guid}/timeline", async (Guid id,HttpContext http,PhanQuyen permission,Database db) => {
            var(actor,error)=await permission.YeuCau(http,"documents.read");
            if(error!=null)return error;
            if(!await CoQuyenXem(db,actor!,id))return Results.NotFound();
            using var cn=db.Open();
            var doc=await cn.QuerySingleOrDefaultAsync(@"
                SELECT v.MaVanBan,v.TieuDe,v.TrangThai,v.MaNoiBo,v.PhienBanHienTai,v.DoKhan,v.DoMat,
                       c.TenCoQuan AS CoQuanSoHuu
                FROM dbo.VanBan v JOIN dbo.CoQuan c ON v.MaCoQuan=c.MaCoQuan
                WHERE v.MaVanBan=@id AND v.MaCoQuan=@agency",new{id,agency=actor!.MaCoQuan});
            if(doc==null)return Results.NotFound();
            var events=await cn.QueryAsync(@"
                SELECT l.MaLichSu,l.HanhDong,l.MoTa,l.TrangThaiTruoc,l.TrangThaiSau,l.SoPhienBan,
                       l.ThoiDiem,n.HoTen AS NguoiThucHien,c.TenCoQuan
                FROM dbo.LichSuVanBan l
                LEFT JOIN dbo.NguoiDung n ON n.MaNguoiDung=l.MaNguoiThucHien
                JOIN dbo.CoQuan c ON c.MaCoQuan=l.MaCoQuan
                WHERE l.MaVanBan=@id AND
                  (l.MaCoQuan=@agency OR (l.PhamViHienThi='GIAO_NHAN' AND EXISTS(
                    SELECT 1 FROM dbo.LuotChuyenVanBan ch
                    WHERE ch.MaLuotChuyen=l.MaLuotChuyen AND ch.MaCoQuanGui=@agency)))
                ORDER BY l.ThoiDiem,l.MaLichSu",new{id,agency=actor.MaCoQuan});
            var tasks=await cn.QueryAsync(@"
                SELECT t.Buoc,t.TrangThai,t.HanXuLy,t.ThoiDiemTao,t.ThoiDiemHoanThanh,
                       n.HoTen AS NguoiPhuTrach
                FROM dbo.TacVuXuLy t JOIN dbo.NguoiDung n ON n.MaNguoiDung=t.MaNguoiDuocGiao
                WHERE t.MaVanBan=@id AND t.MaCoQuan=@agency ORDER BY t.ThoiDiemTao",new{id,agency=actor.MaCoQuan});
            // Ben gui chi thay trang thai giao/nhan, khong thay viec noi bo cua co quan nhan.
            var deliveries=await cn.QueryAsync(@"
                SELECT l.MaLuotChuyen,c.TenCoQuan AS CoQuanNhan,l.TrangThai,l.ThoiDiemGui,
                       l.ThoiDiemNhan,l.LyDoTuChoi,l.SoLanGui
                FROM dbo.LuotChuyenVanBan l
                JOIN dbo.CoQuan c ON c.MaCoQuan=l.MaCoQuanNhan
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                WHERE p.MaVanBan=@id AND l.MaCoQuanGui=@agency ORDER BY c.TenCoQuan",
                new{id,agency=actor.MaCoQuan});
            var planned=await cn.QueryAsync(@"
                SELECT c.MaCoQuan,c.TenCoQuan FROM dbo.NoiNhanDuKien d
                JOIN dbo.CoQuan c ON c.MaCoQuan=d.MaCoQuanNhan
                WHERE d.MaVanBan=@id ORDER BY c.TenCoQuan",new{id});
            return Results.Ok(new { document=doc,events,tasks,deliveries,planned });
        });
    }
}
