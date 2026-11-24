using System.Security.Cryptography;
using System.Text.Json;
using Dapper;

public sealed record TaoChungThuKy(string LoaiChungThu,string TenChungThu,string MatKhau,
    bool TaoChungThuThuNghiem);
public sealed record ChuanBiPdf(Guid MaYeuCau);
public sealed record KyVanBan(string LoaiChuKy,string MatKhau,Guid MaYeuCau);

public sealed class HoSoKy {
    public Guid MaVanBan {get;set;}
    public int MaCoQuan {get;set;}
    public int PhienBanHienTai {get;set;}
    public string TrangThai {get;set;}="";
    public string TieuDe {get;set;}="";
    public int? SoPhatHanh {get;set;}
    public string? KyHieu {get;set;}
}
public sealed class ChungThuLuu {
    public Guid MaChungThu {get;set;}
    public int MaCoQuan {get;set;}
    public int? MaNguoiSoHuu {get;set;}
    public string DuongDanKhoaMaHoa {get;set;}="";
    public string MaKhoaMaHoa {get;set;}="";
    public string NguonChungThu {get;set;}="";
    public byte[] ChungThuCongKhai {get;set;}=[];
}
public sealed class BanPdf {
    public Guid MaBan {get;set;}
    public string DuongDanLuuTru {get;set;}="";
    public string MaBamSha256 {get;set;}="";
}
public sealed class NguonPdf {
    public string DuongDanLuuTru {get;set;}="";
    public string MaBamSha256 {get;set;}="";
}

public static class KySoEndpoints
{
    internal static string TaoDuongDan(IConfiguration config,int agency,Guid doc,Guid fileId)
    {
        var root=config["Storage:RootPath"];
        if(string.IsNullOrWhiteSpace(root))throw new InvalidOperationException("Chưa cấu hình Storage__RootPath.");
        var folder=Path.Combine(root,agency.ToString(),doc.ToString("N"),"ban_ky");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder,fileId.ToString("N")+".pdf");
    }

    internal static async Task<bool> CoVaiTro(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,NguoiThucHien actor,string role)
        => await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
            WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency AND r.MaDinhDanh=@role",
            new{user=actor.MaNguoiDung,agency=actor.MaCoQuan,role},tx)>0;

    internal static async Task<bool> KiemTraMatKhau(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,NguoiThucHien actor,string password)
    {
        var hash=await cn.QuerySingleAsync<string>("SELECT MatKhauBam FROM dbo.NguoiDung WHERE MaNguoiDung=@user",
            new{user=actor.MaNguoiDung},tx);
        return MatKhau.KiemTra(password,hash);
    }

    internal static async Task<HoSoKy?> LayVanBan(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,Guid id,int agency)
        => await cn.QuerySingleOrDefaultAsync<HoSoKy>(@"
            SELECT MaVanBan,MaCoQuan,PhienBanHienTai,TrangThai,TieuDe,SoPhatHanh,KyHieu
            FROM dbo.VanBan WITH(UPDLOCK,HOLDLOCK) WHERE MaVanBan=@id AND MaCoQuan=@agency",
            new{id,agency},tx);

    internal static async Task<BanPdf?> LayBan(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,Guid doc,int version,int stage)
        => await cn.QuerySingleOrDefaultAsync<BanPdf>(@"
            SELECT MaBan,DuongDanLuuTru,MaBamSha256 FROM dbo.BanPdfDaKy
            WHERE MaVanBan=@doc AND SoPhienBan=@version AND ThuTuKy=@stage",
            new{doc,version,stage},tx);

    internal static async Task<string?> LayBienNhan(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,int agency,Guid request,string command)
        => await cn.QueryFirstOrDefaultAsync<string>(@"
            SELECT KetQuaJson FROM dbo.BienNhanLenh WITH(UPDLOCK,HOLDLOCK)
            WHERE MaCoQuan=@agency AND MaYeuCau=@request AND LoaiLenh=@command",
            new{agency,request,command},tx);

    internal static async Task GhiBienNhan(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,NguoiThucHien actor,Guid request,string command,string json)
        => await cn.ExecuteAsync(@"
            INSERT dbo.BienNhanLenh(MaCoQuan,MaNguoiThucHien,MaYeuCau,LoaiLenh,KetQuaJson)
            VALUES(@agency,@user,@request,@command,@json)",
            new{agency=actor.MaCoQuan,user=actor.MaNguoiDung,request,command,json},tx);

    public static void MapKySo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/signing-credentials",async(HttpContext http,PhanQuyen permissions,Database db)=>{
            var user=await permissions.NguoiHienTai(http);
            if(user?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            var rows=await cn.QueryAsync(@"
                SELECT MaChungThu,LoaiChungThu,NguonChungThu,TenChungThu,SoSerial,MaNguoiSoHuu,
                       DangHoatDong,NgayHieuLuc,NgayHetHan
                FROM dbo.ChungThuKy WHERE MaCoQuan=@agency
                    AND (MaNguoiSoHuu=@user OR MaNguoiSoHuu IS NULL)
                ORDER BY ThoiDiemTao DESC",new{agency=user.MaCoQuan,user=user.MaNguoiDung});
            return Results.Ok(rows);
        }).RequireAuthorization();

        // Certificate import uses multipart; self-signed mode intentionally marked as TEST ONLY.
        app.MapPost("/api/v1/signing-credentials",async(HttpContext http,PhanQuyen permissions,
            Database db,DichVuKyPdf signer)=>{
            if(!http.Request.HasFormContentType)return Results.BadRequest(new{message="Cần multipart/form-data."});
            var form=await http.Request.ReadFormAsync();
            var type=form["loaiChungThu"].ToString();
            var test=form["thuNghiem"].ToString()=="true";
            var name=form["tenChungThu"].ToString().Trim();
            var password=form["matKhau"].ToString();
            var file=form.Files.GetFile("tepPfx");
            if(type is not ("CA_NHAN" or "CO_QUAN") || name.Length is < 3 or > 180)
                return Results.BadRequest(new{message="Loại hoặc tên chứng thư không hợp lệ."});
            if(!test && (file is null || file.Length is < 100 or > 1_048_576))
                return Results.BadRequest(new{message="Cần PFX/P12 hợp lệ, tối đa 1 MB."});
            var(actor,error)=await permissions.YeuCau(http,type=="CA_NHAN"?
                "signatures.manage-own":"signatures.manage-agency");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if(!await KiemTraMatKhau(cn,tx,actor,password)){
                tx.Rollback();return Results.BadRequest(new{message="Xác nhận mật khẩu tài khoản không đúng."});
            }
            int? owner=type=="CA_NHAN"?actor.MaNguoiDung:null;
            var active=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.ChungThuKy WITH(UPDLOCK,HOLDLOCK)
                WHERE MaCoQuan=@agency AND LoaiChungThu=@type AND DangHoatDong=1
                  AND ((MaNguoiSoHuu=@owner) OR (MaNguoiSoHuu IS NULL AND @owner IS NULL))",
                new{agency=actor.MaCoQuan,type,owner},tx);
            if(active>0){tx.Rollback();return Results.Conflict(new{message="Đã có chứng thư đang hoạt động. Cần quy trình thu hồi/thay thế riêng."});}
            var certId=Guid.NewGuid();
            DichVuKyPdf.ChungThuMoi cert;
            try {
                if(test)cert=signer.TaoChungThuNoiBo(certId,actor.MaCoQuan.Value,owner,name);
                else {
                    await using var stream=new MemoryStream();
                    await file!.CopyToAsync(stream);
                    cert=signer.NhapChungThu(certId,actor.MaCoQuan.Value,owner,stream.ToArray(),
                        form["matKhauPfx"].ToString());
                }
            } catch(Exception ex)when(ex is CryptographicException or InvalidDataException or InvalidOperationException){
                tx.Rollback();return Results.BadRequest(new{message="Không thể tạo/nhập chứng thư: "+ex.Message});
            }
            try {
                await cn.ExecuteAsync(@"
                    INSERT dbo.ChungThuKy(MaChungThu,MaCoQuan,MaNguoiSoHuu,LoaiChungThu,NguonChungThu,TenChungThu,
                        SoSerial,ChungThuCongKhai,DuongDanKhoaMaHoa,MaKhoaMaHoa,NgayHieuLuc,NgayHetHan)
                    VALUES(@certId,@agency,@owner,@type,@source,@name,@serial,@publicCert,@path,@keyId,@start,@end)",
                    new{certId,agency=actor.MaCoQuan,owner,type,name,serial=cert.SoSerial,
                        publicCert=cert.ChungThuCongKhai,path=cert.DuongDanKhoaMaHoa,
                        keyId=signer.MaKhoa(),source=test?"TU_KY_THU_NGHIEM":"NHAP_PFX",
                        start=cert.NgayHieuLuc,end=cert.NgayHetHan},tx);
                tx.Commit();
                return Results.Created("/api/v1/signing-credentials",new{
                    maChungThu=certId,loaiChungThu=type,noiBoThuNghiem=test,
                    mucDoTinCay=test?"INTERNAL_SELF_SIGNED":"CHUA_THAM_DINH_CA",
                    ngayHetHan=cert.NgayHetHan});
            } catch {tx.Rollback();File.Delete(cert.DuongDanKhoaMaHoa);throw;}
        }).RequireAuthorization().DisableAntiforgery();

        app.MapPost("/api/v1/documents/{id:guid}/prepared-pdf",async(Guid id,ChuanBiPdf request,
            HttpContext http,PhanQuyen permissions,Database db,DichVuKyPdf signer,IConfiguration config)=>{
            var(actor,error)=await permissions.YeuCau(http,"signing.prepare");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(request.MaYeuCau==Guid.Empty)return Results.BadRequest(new{message="MaYeuCau bắt buộc."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var old=await LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,"CHUAN_BI_KY");
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            var doc=await LayVanBan(cn,tx,id,actor.MaCoQuan.Value);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.TrangThai!="DA_CAP_SO" || doc.SoPhatHanh is null){
                tx.Rollback();return Results.Conflict(new{message="Phải cấp số và duyệt trước khi chuẩn bị ký."});
            }
            var source=await cn.QueryFirstOrDefaultAsync<NguonPdf>(@"
                SELECT TOP 1 f.DuongDanLuuTru,f.MaBamSha256 FROM dbo.TepDinhKem f
                JOIN dbo.PhienBanVanBan p ON p.MaPhienBan=f.MaPhienBan
                WHERE p.MaVanBan=@id AND p.SoPhienBan=@version AND f.LoaiNoiDung='application/pdf'
                ORDER BY f.MaTep",new{id,version=doc.PhienBanHienTai},tx);
            if(source==null || !File.Exists(source.DuongDanLuuTru)){
                tx.Rollback();return Results.Conflict(new{message="Không tìm thấy bản PDF được phê duyệt."});
            }
            if(await DichVuKyPdf.Bam(source.DuongDanLuuTru)!=source.MaBamSha256){
                tx.Rollback();return Results.Conflict(new{message="PDF bản dự thảo không còn nguyên vẹn."});
            }
            var fileId=Guid.NewGuid();var path=TaoDuongDan(config,actor.MaCoQuan.Value,id,fileId);
            try {
                signer.ChuanBi(source.DuongDanLuuTru,path,$"{doc.SoPhatHanh}/{doc.KyHieu}");
                var hash=await DichVuKyPdf.Bam(path);
                await cn.ExecuteAsync(@"
                    INSERT dbo.BanPdfDaKy(MaBan,MaVanBan,MaCoQuan,SoPhienBan,ThuTuKy,DuongDanLuuTru,MaBamSha256)
                    VALUES(@fileId,@id,@agency,@version,0,@path,@hash)",
                    new{fileId,id,agency=actor.MaCoQuan,version=doc.PhienBanHienTai,path,hash},tx);
                await cn.ExecuteAsync("UPDATE dbo.VanBan SET TrangThai='CHO_KY' WHERE MaVanBan=@id",new{id},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                    VALUES(@id,@agency,@user,'CHUAN_BI_KY',@description,'DA_CAP_SO','CHO_KY',@version)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,version=doc.PhienBanHienTai,
                        description=$"Đã chèn chân trang số {doc.SoPhatHanh}/{doc.KyHieu} và đóng băng PDF trước khi ký."},tx);
                var json=JsonSerializer.Serialize(new{maVanBan=id,trangThai="CHO_KY",maBan=fileId,maBamSha256=hash});
                await GhiBienNhan(cn,tx,actor,request.MaYeuCau,"CHUAN_BI_KY",json);
                tx.Commit();return Results.Content(json,"application/json");
            } catch {tx.Rollback();if(File.Exists(path))File.Delete(path);throw;}
        }).RequireAuthorization();

        app.MapPost("/api/v1/documents/{id:guid}/signatures",async(Guid id,KyVanBan request,
            HttpContext http,PhanQuyen permissions,Database db,DichVuKyPdf signer,IConfiguration config)=>{
            var(actor,error)=await permissions.YeuCau(http,"signing.sign");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            var stages=new Dictionary<string,(int Order,string Role)>{
                ["KY_NHAY"]=(1,"TRUONG_PHONG"),["KY_LANH_DAO"]=(2,"LANH_DAO"),
                ["KY_CO_QUAN"]=(3,"VAN_THU")};
            if(!stages.TryGetValue(request.LoaiChuKy??"",out var stage) || request.MaYeuCau==Guid.Empty)
                return Results.BadRequest(new{message="Loại chữ ký hoặc MaYeuCau không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var command="KY_"+stage.Order;
            var old=await LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,command);
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            if(!await CoVaiTro(cn,tx,actor,stage.Role)){
                tx.Rollback();return Results.Forbid();
            }
            if(!await KiemTraMatKhau(cn,tx,actor,request.MatKhau)){
                tx.Rollback();return Results.BadRequest(new{message="Sai mật khẩu xác nhận ký."});
            }
            var doc=await LayVanBan(cn,tx,id,actor.MaCoQuan.Value);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.TrangThai!="CHO_KY"){
                tx.Rollback();return Results.Conflict(new{message="Văn bản không ở bước ký."});
            }
            var previous=await LayBan(cn,tx,id,doc.PhienBanHienTai,stage.Order-1);
            if(previous==null){tx.Rollback();return Results.Conflict(new{message="Chưa đủ chữ ký theo thứ tự bắt buộc."});}
            var already=await LayBan(cn,tx,id,doc.PhienBanHienTai,stage.Order);
            if(already!=null){tx.Rollback();return Results.Conflict(new{message="Bước ký này đã hoàn thành."});}
            if(!File.Exists(previous.DuongDanLuuTru) ||
               !string.Equals(await DichVuKyPdf.Bam(previous.DuongDanLuuTru),previous.MaBamSha256,StringComparison.OrdinalIgnoreCase)){
                tx.Rollback();return Results.Conflict(new{message="Bản PDF trước ký không toàn vẹn."});
            }
            int? owner=stage.Order==3?null:actor.MaNguoiDung;
            var cert=await cn.QuerySingleOrDefaultAsync<ChungThuLuu>(@"
                SELECT TOP 1 MaChungThu,MaCoQuan,MaNguoiSoHuu,DuongDanKhoaMaHoa,MaKhoaMaHoa,
                    ChungThuCongKhai,NguonChungThu
                FROM dbo.ChungThuKy WHERE MaCoQuan=@agency AND DangHoatDong=1
                  AND ((MaNguoiSoHuu=@owner) OR (MaNguoiSoHuu IS NULL AND @owner IS NULL))
                  AND NgayHieuLuc<=SYSUTCDATETIME() AND NgayHetHan>SYSUTCDATETIME()
                  AND LoaiChungThu=@type ORDER BY ThoiDiemTao DESC",
                new{agency=actor.MaCoQuan,owner,type=owner is null?"CO_QUAN":"CA_NHAN"},tx);
            if(cert==null){tx.Rollback();return Results.Conflict(new{message="Chưa cấu hình chứng thư hợp lệ cho người/cơ quan ký."});}
            if(cert.MaKhoaMaHoa!=signer.MaKhoa()){
                tx.Rollback();return Results.Conflict(new{message="Khóa mã hóa chứng thư đã luân chuyển; cần bộ giải mã cũ."});
            }
            var fileId=Guid.NewGuid();
            var path=TaoDuongDan(config,actor.MaCoQuan.Value,id,fileId);
            var field=$"Ky_{stage.Order}_{id:N}";
            try {
                var signed=signer.Ky(previous.DuongDanLuuTru,path,field,cert.MaChungThu,
                    actor.MaCoQuan.Value,owner,cert.DuongDanKhoaMaHoa,request.LoaiChuKy);
                var verified=signer.XacMinh(path);
                if(verified.Count!=stage.Order || verified.Any(s=>!s.ToanVenMatMa) ||
                   !verified[^1].BaoPhuToanBoBanHienTai)
                    throw new CryptographicException("Dãy chữ ký PDF không toàn vẹn.");
                var hash=await DichVuKyPdf.Bam(path);
                await cn.ExecuteAsync(@"
                    INSERT dbo.BanPdfDaKy(MaBan,MaVanBan,MaCoQuan,SoPhienBan,ThuTuKy,MaBanTruoc,
                        DuongDanLuuTru,MaBamSha256,TenTruongKy)
                    VALUES(@fileId,@id,@agency,@version,@stage,@prior,@path,@hash,@field)",
                    new{fileId,id,agency=actor.MaCoQuan,version=doc.PhienBanHienTai,stage=stage.Order,
                        prior=previous.MaBan,path,hash,field},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.ChuKySo(MaVanBan,MaCoQuan,MaNguoiKy,SoPhienBan,LoaiChuKy,DinhDangChuKy,
                        DuLieuChuKy,ChungThuCongKhai,MaBamTaiLieu,MaBamChuKyTruoc,KetQuaXacMinh,
                        MucDoTinCay,MaBanDaKy,MaChungThu,TenTruongKy)
                    VALUES(@id,@agency,@user,@version,@type,'PADES_CMS',@cms,@publicCert,@hash,@priorHash,
                        'HOP_LE_MAT_MA',@trust,@fileId,@certId,@field)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,version=doc.PhienBanHienTai,
                        type=request.LoaiChuKy,cms=signed.DuLieuCms,publicCert=cert.ChungThuCongKhai,hash,
                        priorHash=previous.MaBamSha256,fileId,certId=cert.MaChungThu,field,
                        trust=cert.NguonChungThu=="TU_KY_THU_NGHIEM"?"INTERNAL_SELF_SIGNED":"CHUA_THAM_DINH_CA"},tx);
                var next=stage.Order==3?"DA_KY":"CHO_KY";
                await cn.ExecuteAsync("UPDATE dbo.VanBan SET TrangThai=@next WHERE MaVanBan=@id",new{id,next},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                    VALUES(@id,@agency,@user,@type,@message,'CHO_KY',@next,@version)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,type=request.LoaiChuKy,
                        message=$"{actor.HoTen} ký số PDF; bước {stage.Order}/3, SHA-256 {hash[..16]}…",
                        next,version=doc.PhienBanHienTai},tx);
                var json=JsonSerializer.Serialize(new{maVanBan=id,loaiChuKy=request.LoaiChuKy,
                    trangThai=next,maBan=fileId,maBamSha256=hash,hopLeMatMa=true,
                    thamDinhCa=cert.NguonChungThu=="TU_KY_THU_NGHIEM"?"INTERNAL_SELF_SIGNED":"CHUA_THAM_DINH_CA"});
                await GhiBienNhan(cn,tx,actor,request.MaYeuCau,command,json);
                tx.Commit();return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is CryptographicException or InvalidDataException){
                tx.Rollback();if(File.Exists(path))File.Delete(path);
                return Results.BadRequest(new{message="Không ký được PDF: "+ex.Message});
            }catch{tx.Rollback();if(File.Exists(path))File.Delete(path);throw;}
        }).RequireAuthorization();

        app.MapGet("/api/v1/documents/{id:guid}/signatures/verification",async(Guid id,
            HttpContext http,PhanQuyen permissions,Database db,DichVuKyPdf signer)=>{
            var(actor,error)=await permissions.YeuCau(http,"documents.read");
            if(error!=null)return error;
            using var cn=db.Open();
            var pdf=await cn.QueryFirstOrDefaultAsync<BanPdf>(@"
                SELECT TOP 1 p.MaBan,p.DuongDanLuuTru,p.MaBamSha256
                FROM dbo.BanPdfDaKy p JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE p.MaVanBan=@id AND p.MaCoQuan=@agency AND v.DoMat='THUONG'
                  AND (v.MaNguoiTao=@user OR EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro uv
                      JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
                      WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency
                        AND r.MaDinhDanh IN ('TRUONG_PHONG','LANH_DAO','VAN_THU')))
                ORDER BY p.ThuTuKy DESC",new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung});
            if(pdf==null || !File.Exists(pdf.DuongDanLuuTru))return Results.NotFound();
            var sha=await DichVuKyPdf.Bam(pdf.DuongDanLuuTru);
            if(sha!=pdf.MaBamSha256)return Results.Conflict(new{message="PDF lưu trữ đã bị thay đổi."});
            return Results.Ok(new{maBan=pdf.MaBan,maBamSha256=sha,
                chuKy=signer.XacMinh(pdf.DuongDanLuuTru),
                ghiChu="Kiểm tra tính toàn vẹn CMS/PDF, không chứng minh CA được pháp luật công nhận."});
        }).RequireAuthorization();

        app.MapGet("/api/v1/documents/{id:guid}/signed-pdf",async(Guid id,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var(actor,error)=await permissions.YeuCau(http,"documents.read");
            if(error!=null)return error;
            using var cn=db.Open();
            var pdf=await cn.QueryFirstOrDefaultAsync<BanPdf>(@"
                SELECT TOP 1 p.MaBan,p.DuongDanLuuTru,p.MaBamSha256 FROM dbo.BanPdfDaKy p
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE p.MaVanBan=@id AND v.MaCoQuan=@agency AND v.DoMat='THUONG'
                AND (v.MaNguoiTao=@user OR EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro uv
                   JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro WHERE uv.MaNguoiDung=@user
                   AND uv.MaCoQuan=@agency AND r.MaDinhDanh IN ('TRUONG_PHONG','LANH_DAO','VAN_THU')))
                ORDER BY p.ThuTuKy DESC",new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung});
            if(pdf==null || !File.Exists(pdf.DuongDanLuuTru))return Results.NotFound();
            if(await DichVuKyPdf.Bam(pdf.DuongDanLuuTru)!=pdf.MaBamSha256)
                return Results.Conflict(new{message="PDF không còn nguyên vẹn."});
            return Results.File(pdf.DuongDanLuuTru,"application/pdf",$"van-ban-{id:N}-da-ky.pdf");
        }).RequireAuthorization();
    }
}
