using System.Security.Cryptography;
using System.Text.Json;
using Dapper;

public sealed record TaoChungThuKy(string LoaiChungThu,string TenChungThu,string MatKhau,
    bool TaoChungThuThuNghiem);
public sealed record ChuanBiPdf(Guid MaYeuCau);
public sealed record KyVanBan(string LoaiChuKy,string MatKhau,Guid MaYeuCau,
    string? HinhAnhKyBase64=null,int? TrangKy=null,float? TyLeX=null,float? TyLeY=null,
    float? TyLeRong=null,float? TyLeCao=null,Guid? MaChungThu=null);

public sealed class HoSoKy {
    public Guid MaVanBan {get;set;}
    public int MaCoQuan {get;set;}
    public int PhienBanHienTai {get;set;}
    public string TrangThai {get;set;}="";
    public string TieuDe {get;set;}="";
    public int? SoPhatHanh {get;set;}
    public string? KyHieu {get;set;}
    public int MaNguoiTao {get;set;}
    public byte SoBuocKy {get;set;}=5;
    public int? MaNguoiKyNhay {get;set;}
    public int? MaNguoiKyLanhDao {get;set;}
}
public sealed class ChungThuLuu {
    public Guid MaChungThu {get;set;}
    public int MaCoQuan {get;set;}
    public int? MaNguoiSoHuu {get;set;}
    public string DuongDanKhoaMaHoa {get;set;}="";
    public string MaKhoaMaHoa {get;set;}="";
    public string NguonChungThu {get;set;}="";
    public byte[] ChungThuCongKhai {get;set;}=[];
    public byte[]? HinhAnhChuKy {get;set;}
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
    private static byte[] DocPng(IFormFile? file)
    {
        if(file==null || file.Length is < 24 or > 5_242_880)
            throw new InvalidDataException("Cần ảnh chữ ký PNG tối đa 5 MB.");
        using var stream=new MemoryStream();file.CopyTo(stream);var bytes=stream.ToArray();
        if(!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
            throw new InvalidDataException("Ảnh chữ ký phải là PNG.");
        var w=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4));
        var h=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if(w is 0 or >4096 || h is 0 or >4096)throw new InvalidDataException("Ảnh vượt quá 4096 pixel mỗi chiều.");
        return bytes;
    }

    private static DichVuKyPdf.ViTriHienThi ViTriDaDangKy(KyVanBan request,ChungThuLuu cert,string label,bool agency=false)
    {
        if(request.MaChungThu!=cert.MaChungThu || cert.HinhAnhChuKy==null)
            throw new InvalidDataException("Hãy chọn chữ ký đã đăng ký có ảnh PNG trên trang Quản lý chữ ký số.");
        return ViTriKy(request with{HinhAnhKyBase64=Convert.ToBase64String(cert.HinhAnhChuKy)},label,agency)!;
    }
    // Cleanup must never replace the actual PDF signing error with an IOException.
    private static void XoaTepTam(string path)
    {
        try { if(File.Exists(path)) File.Delete(path); }
        catch(IOException) { /* PDF writer may still be releasing its handle. */ }
        catch(UnauthorizedAccessException) { /* Preserve the original exception. */ }
    }
    internal static DichVuKyPdf.ViTriHienThi? ViTriKy(KyVanBan request,string label,bool agency=false)
    {
        if(string.IsNullOrWhiteSpace(request.HinhAnhKyBase64) && !agency)return null;
        byte[]? image=null;
        if(!string.IsNullOrWhiteSpace(request.HinhAnhKyBase64)){
            if(request.HinhAnhKyBase64.Length>6_990_508)
                throw new InvalidDataException("Ảnh chữ ký vượt giới hạn 5 MB.");
            try {image=Convert.FromBase64String(request.HinhAnhKyBase64);}
            catch(FormatException){throw new InvalidDataException("Ảnh chữ ký không phải Base64 hợp lệ.");}
            if(image.Length>5_242_880 || image.Length<24 ||
               !image.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
                throw new InvalidDataException("Chỉ hỗ trợ ảnh chữ ký PNG tối đa 5 MB.");
            var width=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(16,4));
            var height=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(20,4));
            if(width is 0 or >4096 || height is 0 or >4096)
                throw new InvalidDataException("Ảnh chữ ký không được vượt 4096 pixel mỗi chiều.");
        }
        return new DichVuKyPdf.ViTriHienThi(image,request.TrangKy??1,
            request.TyLeX??0.60f,request.TyLeY??0.73f,request.TyLeRong??0.32f,
            request.TyLeCao??0.11f,label);
    }
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
            SELECT MaVanBan,MaCoQuan,MaNguoiTao,PhienBanHienTai,TrangThai,TieuDe,SoPhatHanh,KyHieu,
                   SoBuocKy,MaNguoiKyNhay,MaNguoiKyLanhDao
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
                       DangHoatDong,NgayHieuLuc,NgayHetHan,TenMauChuKy,
                       CAST(CASE WHEN HinhAnhChuKy IS NULL THEN 0 ELSE 1 END AS BIT) AS CoAnhChuKy
                FROM dbo.ChungThuKy WHERE MaCoQuan=@agency
                    AND (MaNguoiSoHuu=@user OR MaNguoiSoHuu IS NULL)
                ORDER BY ThoiDiemTao DESC",new{agency=user.MaCoQuan,user=user.MaNguoiDung});
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapGet("/api/v1/signing-credentials/{id:guid}/image",async(Guid id,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            var image=await cn.QuerySingleOrDefaultAsync<byte[]>(@"
                SELECT HinhAnhChuKy FROM dbo.ChungThuKy WHERE MaChungThu=@id AND MaCoQuan=@agency
                AND DangHoatDong=1 AND (MaNguoiSoHuu=@user OR
                  (MaNguoiSoHuu IS NULL AND EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro uv
                    JOIN dbo.VaiTro v ON v.MaVaiTro=uv.MaVaiTro
                    WHERE uv.MaCoQuan=@agency AND uv.MaNguoiDung=@user
                      AND v.MaDinhDanh IN ('LANH_DAO','VAN_THU'))))",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung});
            return image==null?Results.NotFound():Results.File(image,"image/png");
        }).RequireAuthorization();

        app.MapPost("/api/v1/signing-credentials/{id:guid}/image",async(Guid id,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            if(!http.Request.HasFormContentType)return Results.BadRequest(new{message="Cần multipart/form-data."});
            var form=await http.Request.ReadFormAsync();
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            byte[] image;
            try{image=DocPng(form.Files.GetFile("anhChuKy"));}
            catch(InvalidDataException ex){return Results.BadRequest(new{message=ex.Message});}
            using var cn=db.Open();
            var isLeader=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro v ON v.MaVaiTro=uv.MaVaiTro
                WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency AND v.MaDinhDanh='LANH_DAO'",
                new{user=actor.MaNguoiDung,agency=actor.MaCoQuan})>0;
            var count=await cn.ExecuteAsync(@"
                UPDATE dbo.ChungThuKy SET HinhAnhChuKy=@image,TenMauChuKy=@label
                WHERE MaChungThu=@id AND MaCoQuan=@agency AND
                  (MaNguoiSoHuu=@user OR (MaNguoiSoHuu IS NULL AND @isLeader=1))
                  AND DangHoatDong=1",new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    isLeader,image,label=form["tenMau"].ToString()[..Math.Min(180,form["tenMau"].ToString().Length)]});
            return count==0?Results.NotFound():Results.Ok(new{maChungThu=id});
        }).RequireAuthorization().DisableAntiforgery();

        app.MapPost("/api/v1/signing-credentials/{id:guid}/deactivate",async(Guid id,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            var isLeader=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro v ON v.MaVaiTro=uv.MaVaiTro
                WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency AND v.MaDinhDanh='LANH_DAO'",
                new{user=actor.MaNguoiDung,agency=actor.MaCoQuan})>0;
            var affected=await cn.ExecuteAsync(@"
                UPDATE dbo.ChungThuKy SET DangHoatDong=0
                WHERE MaChungThu=@id AND MaCoQuan=@agency AND DangHoatDong=1
                AND (MaNguoiSoHuu=@user OR (MaNguoiSoHuu IS NULL AND @isLeader=1))",
                new{id,user=actor.MaNguoiDung,agency=actor.MaCoQuan,isLeader});
            return affected==0?Results.NotFound():Results.Ok(new{maChungThu=id,daVoHieuHoa=true});
        }).RequireAuthorization();

        // Certificate import uses multipart; self-signed mode intentionally marked as TEST ONLY.
        app.MapPost("/api/v1/signing-credentials",async(HttpContext http,PhanQuyen permissions,
            Database db,DichVuKyPdf signer)=>{
            if(!http.Request.HasFormContentType)return Results.BadRequest(new{message="Cần multipart/form-data."});
            var form=await http.Request.ReadFormAsync();
            var type=form["loaiChungThu"].ToString();
            var test=form["thuNghiem"].ToString()=="true";
            var replaceTest=form["thayTheChungThuThuNghiem"].ToString()=="true";
            var name=form["tenChungThu"].ToString().Trim();
            var password=form["matKhau"].ToString();
            var file=form.Files.GetFile("tepPfx");
            byte[] stamp;
            try{stamp=DocPng(form.Files.GetFile("anhChuKy"));}
            catch(InvalidDataException ex){return Results.BadRequest(new{message=ex.Message});}
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
            if(type=="CO_QUAN" && !await CoVaiTro(cn,tx,actor,"LANH_DAO")){
                tx.Rollback();return Results.Forbid();
            }
            if(!await KiemTraMatKhau(cn,tx,actor,password)){
                tx.Rollback();return Results.BadRequest(new{message="Xác nhận mật khẩu tài khoản không đúng."});
            }
            int? owner=type=="CA_NHAN"?actor.MaNguoiDung:null;
            var active=await cn.QuerySingleOrDefaultAsync<ChungThuLuu>(@"
                SELECT TOP 1 MaChungThu,NguonChungThu FROM dbo.ChungThuKy WITH(UPDLOCK,HOLDLOCK)
                WHERE MaCoQuan=@agency AND LoaiChungThu=@type AND DangHoatDong=1
                  AND ((MaNguoiSoHuu=@owner) OR (MaNguoiSoHuu IS NULL AND @owner IS NULL))",
                new{agency=actor.MaCoQuan,type,owner},tx);
            var activeCount=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.ChungThuKy WITH(UPDLOCK,HOLDLOCK)
                WHERE MaCoQuan=@agency AND LoaiChungThu='CA_NHAN'
                  AND MaNguoiSoHuu=@owner AND DangHoatDong=1",
                new{agency=actor.MaCoQuan,owner},tx);
            if(type=="CA_NHAN" && activeCount>=3 && !replaceTest){
                tx.Rollback();return Results.Conflict(new{message="Tài khoản đã có 3 chữ ký cá nhân đang hoạt động."});
            }
            if(replaceTest && !test){tx.Rollback();return Results.BadRequest(new{message="Chỉ được thay thế bằng chứng thư tự ký thử nghiệm."});}
            if(type=="CO_QUAN" && active!=null && (!replaceTest || active.NguonChungThu!="TU_KY_THU_NGHIEM")){
                tx.Rollback();return Results.Conflict(new{message=active.NguonChungThu=="TU_KY_THU_NGHIEM"
                    ?"Đã có chứng thư thử nghiệm đang hoạt động. Chọn 'Thay thế chứng thư thử nghiệm cũ' để tạo mới."
                    :"Chứng thư PFX đang hoạt động không thể thay thế bằng chức năng thử nghiệm."});
            }
            if(replaceTest && active==null){
                tx.Rollback();return Results.BadRequest(new{message="Không có chứng thư thử nghiệm đang hoạt động để thay thế."});
            }
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
                if(replaceTest){
                    await cn.ExecuteAsync(@"
                        UPDATE dbo.ChungThuKy SET DangHoatDong=0
                        WHERE MaChungThu=@oldId AND MaCoQuan=@agency AND DangHoatDong=1",
                        new{oldId=active!.MaChungThu,agency=actor.MaCoQuan},tx);
                }
                await cn.ExecuteAsync(@"
                    INSERT dbo.ChungThuKy(MaChungThu,MaCoQuan,MaNguoiSoHuu,LoaiChungThu,NguonChungThu,TenChungThu,
                        SoSerial,ChungThuCongKhai,DuongDanKhoaMaHoa,MaKhoaMaHoa,NgayHieuLuc,NgayHetHan,TenMauChuKy,HinhAnhChuKy)
                    VALUES(@certId,@agency,@owner,@type,@source,@name,@serial,@publicCert,@path,@keyId,@start,@end,@label,@stamp)",
                    new{certId,agency=actor.MaCoQuan,owner,type,name,serial=cert.SoSerial,
                        publicCert=cert.ChungThuCongKhai,path=cert.DuongDanKhoaMaHoa,
                        keyId=signer.MaKhoa(),source=test?"TU_KY_THU_NGHIEM":"NHAP_PFX",
                        start=cert.NgayHieuLuc,end=cert.NgayHetHan,
                        label=form["tenMau"].ToString().Trim(),stamp},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                    VALUES(@agency,@user,'NGHIEP_VU',@action,@description)",
                    new{agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                        action=replaceTest?"THAY_CHUNG_THU_THU_NGHIEM":"TAO_CHUNG_THU",
                        description=replaceTest
                            ?$"Thay chứng thư thử nghiệm {active!.MaChungThu} bằng {certId}; giữ hồ sơ cũ."
                            :$"Tạo/nhập chứng thư {certId} ({type})."},tx);
                tx.Commit();
                return Results.Created("/api/v1/signing-credentials",new{
                    maChungThu=certId,loaiChungThu=type,noiBoThuNghiem=test,
                    daThayThe=replaceTest,maChungThuCu=replaceTest?active!.MaChungThu:(Guid?)null,
                    mucDoTinCay=test?"INTERNAL_SELF_SIGNED":"CHUA_THAM_DINH_CA",
                    ngayHetHan=cert.NgayHetHan});
            } catch {tx.Rollback();File.Delete(cert.DuongDanKhoaMaHoa);throw;}
        }).RequireAuthorization().DisableAntiforgery();

        // v0.5.2: sign the actual main draft PDF. Later workflow stages are implemented separately.
        app.MapPost("/api/v1/documents/{id:guid}/draft-signature",async(Guid id,KyVanBan request,
            HttpContext http,PhanQuyen permissions,Database db,DichVuKyPdf signer,IConfiguration config)=>{
            var(actor,error)=await permissions.YeuCau(http,"signing.sign");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(request.LoaiChuKy!="KY_NGUOI_SOAN" || request.MaYeuCau==Guid.Empty)
                return Results.BadRequest(new{message="Yêu cầu ký người soạn hoặc MaYeuCau không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var priorResult=await LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,"KY_NGUOI_SOAN");
            if(priorResult!=null){tx.Commit();return Results.Content(priorResult,"application/json");}
            var doc=await LayVanBan(cn,tx,id,actor.MaCoQuan.Value);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.MaNguoiTao!=actor.MaNguoiDung){tx.Rollback();return Results.Forbid();}
            if(doc.TrangThai is not ("NHAP" or "TRA_SUA")){
                tx.Rollback();return Results.Conflict(new{message="Chỉ ký khi văn bản còn ở bước soạn thảo."});
            }
            if(!await KiemTraMatKhau(cn,tx,actor,request.MatKhau)){
                tx.Rollback();return Results.BadRequest(new{message="Sai mật khẩu xác nhận ký."});
            }
            if(await LayBan(cn,tx,id,doc.PhienBanHienTai,1)!=null){
                tx.Rollback();return Results.Conflict(new{message="Phiên bản này đã được người soạn ký."});
            }
            if(await LayBan(cn,tx,id,doc.PhienBanHienTai,0)!=null){
                tx.Rollback();return Results.Conflict(new{message="Phiên bản này đã có bản chuẩn bị ký; cần kiểm tra quy trình."});
            }
            var source=await cn.QueryFirstOrDefaultAsync<NguonPdf>(@"
                SELECT TOP 1 f.DuongDanLuuTru,f.MaBamSha256 FROM dbo.TepDinhKem f
                JOIN dbo.PhienBanVanBan p ON p.MaPhienBan=f.MaPhienBan
                WHERE p.MaVanBan=@id AND p.SoPhienBan=@version
                    AND f.LoaiNoiDung='application/pdf' AND f.LaVanBanChinh=1
                ORDER BY f.MaTep",new{id,version=doc.PhienBanHienTai},tx);
            if(source==null || !File.Exists(source.DuongDanLuuTru) ||
                !string.Equals(await DichVuKyPdf.Bam(source.DuongDanLuuTru),source.MaBamSha256,StringComparison.OrdinalIgnoreCase)){
                tx.Rollback();return Results.Conflict(new{message="Không tìm thấy PDF chính nguyên vẹn của phiên bản hiện tại."});
            }
            var cert=await cn.QuerySingleOrDefaultAsync<ChungThuLuu>(@"
                SELECT TOP 1 MaChungThu,MaCoQuan,MaNguoiSoHuu,DuongDanKhoaMaHoa,MaKhoaMaHoa,
                    ChungThuCongKhai,NguonChungThu,HinhAnhChuKy FROM dbo.ChungThuKy
                WHERE MaCoQuan=@agency AND MaNguoiSoHuu=@user AND LoaiChungThu='CA_NHAN'
                    AND MaChungThu=@selected
                    AND DangHoatDong=1 AND NgayHieuLuc<=SYSUTCDATETIME()
                    AND NgayHetHan>SYSUTCDATETIME() ORDER BY ThoiDiemTao DESC",
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung,selected=request.MaChungThu},tx);
            if(cert==null){tx.Rollback();return Results.Conflict(new{message="Người soạn chưa có chứng thư cá nhân đang hoạt động."});}
            if(cert.MaKhoaMaHoa!=signer.MaKhoa()){
                tx.Rollback();return Results.Conflict(new{message="Không tìm thấy khóa giải mã chứng thư hiện tại."});
            }
            var baseline=Guid.NewGuid();var signedId=Guid.NewGuid();
            var baselinePath=TaoDuongDan(config,actor.MaCoQuan.Value,id,baseline);
            var signedPath=TaoDuongDan(config,actor.MaCoQuan.Value,id,signedId);
            var field=$"Ky_1_{id:N}_v{doc.PhienBanHienTai}";
            try {
                // Copy the unsigned PDF without inserting an issue number. No earlier revision is modified.
                File.Copy(source.DuongDanLuuTru,baselinePath);
                var baselineHash=await DichVuKyPdf.Bam(baselinePath);
                var result=signer.Ky(baselinePath,signedPath,field,cert.MaChungThu,
                    actor.MaCoQuan.Value,actor.MaNguoiDung,cert.DuongDanKhoaMaHoa,"KY_NGUOI_SOAN",
                    ViTriDaDangKy(request,cert,"Nguoi soan"));
                var checks=signer.XacMinh(signedPath);
                if(checks.Count!=1 || !checks[0].ToanVenMatMa || !checks[0].BaoPhuToanBoBanHienTai)
                    throw new CryptographicException("PDF người soạn ký không toàn vẹn hoặc chứa chữ ký không mong đợi.");
                var hash=await DichVuKyPdf.Bam(signedPath);
                await cn.ExecuteAsync(@"
                    INSERT dbo.BanPdfDaKy(MaBan,MaVanBan,MaCoQuan,SoPhienBan,ThuTuKy,DuongDanLuuTru,MaBamSha256)
                    VALUES(@baseline,@id,@agency,@version,0,@baselinePath,@baselineHash);
                    INSERT dbo.BanPdfDaKy(MaBan,MaVanBan,MaCoQuan,SoPhienBan,ThuTuKy,MaBanTruoc,DuongDanLuuTru,MaBamSha256,TenTruongKy)
                    VALUES(@signedId,@id,@agency,@version,1,@baseline,@signedPath,@hash,@field)",
                    new{baseline,id,agency=actor.MaCoQuan,version=doc.PhienBanHienTai,
                        baselinePath,baselineHash,signedId,signedPath,hash,field},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.ChuKySo(MaVanBan,MaCoQuan,MaNguoiKy,SoPhienBan,LoaiChuKy,DinhDangChuKy,
                        DuLieuChuKy,ChungThuCongKhai,MaBamTaiLieu,MaBamChuKyTruoc,KetQuaXacMinh,
                        MucDoTinCay,MaBanDaKy,MaChungThu,TenTruongKy)
                    VALUES(@id,@agency,@user,@version,'KY_NGUOI_SOAN','PADES_CMS',@cms,@publicCert,
                        @hash,@baselineHash,'HOP_LE_MAT_MA',@trust,@signedId,@certId,@field)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,version=doc.PhienBanHienTai,
                        cms=result.DuLieuCms,publicCert=cert.ChungThuCongKhai,hash,baselineHash,signedId,
                        certId=cert.MaChungThu,field,
                        trust=cert.NguonChungThu=="TU_KY_THU_NGHIEM"?"INTERNAL_SELF_SIGNED":"CHUA_THAM_DINH_CA"},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                    VALUES(@id,@agency,@user,'KY_NGUOI_SOAN',N'Người soạn đã ký PDF chính trước khi trình duyệt',@state,@state,@version)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,state=doc.TrangThai,version=doc.PhienBanHienTai},tx);
                var json=JsonSerializer.Serialize(new{maVanBan=id,loaiChuKy="KY_NGUOI_SOAN",maBan=signedId,
                    maBamSha256=hash,phienBan=doc.PhienBanHienTai,hopLeMatMa=true});
                await GhiBienNhan(cn,tx,actor,request.MaYeuCau,"KY_NGUOI_SOAN",json);
                tx.Commit();return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is CryptographicException or InvalidDataException){
                tx.Rollback();XoaTepTam(signedPath);
                XoaTepTam(baselinePath);
                return Results.BadRequest(new{message="Không ký được PDF: "+ex.Message});
            }catch{tx.Rollback();XoaTepTam(signedPath);
                XoaTepTam(baselinePath);throw;}
        }).RequireAuthorization();

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
            if(await LayBan(cn,tx,id,doc.PhienBanHienTai,0)!=null){
                tx.Rollback();return Results.Conflict(new{message="PDF đã ký từ bước người soạn. Chờ bước 0.5.4 xử lý cấp số mà không làm hỏng chữ ký trước."});
            }
            var source=await cn.QueryFirstOrDefaultAsync<NguonPdf>(@"
                SELECT TOP 1 f.DuongDanLuuTru,f.MaBamSha256 FROM dbo.TepDinhKem f
                JOIN dbo.PhienBanVanBan p ON p.MaPhienBan=f.MaPhienBan
                WHERE p.MaVanBan=@id AND p.SoPhienBan=@version AND f.LoaiNoiDung='application/pdf'
                  AND f.LaVanBanChinh=1
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
            var stages=new Dictionary<string,(int Order,string Role,string TaskStep)>{
                ["KY_NHAY"]=(2,"TRUONG_PHONG","KIEM_TRA_NOI_DUNG"),
                ["KY_THE_THUC"]=(3,"TRUONG_VAN_PHONG","KIEM_TRA_THE_THUC"),
                ["KY_LANH_DAO"]=(4,"LANH_DAO","PHE_DUYET_LANH_DAO")};
            if(!stages.TryGetValue(request.LoaiChuKy??"",out var stage) || request.MaYeuCau==Guid.Empty)
                return Results.BadRequest(new{message="Loại chữ ký hoặc MaYeuCau không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if(!await KiemTraMatKhau(cn,tx,actor,request.MatKhau)){
                tx.Rollback();return Results.BadRequest(new{message="Sai mật khẩu xác nhận ký."});
            }
            var doc=await LayVanBan(cn,tx,id,actor.MaCoQuan.Value);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.SoBuocKy is 3 or 4){
                if(request.LoaiChuKy=="KY_THE_THUC" ||
                   (doc.SoBuocKy==3 && request.LoaiChuKy=="KY_NHAY")){
                    tx.Rollback();return Results.Conflict(new{message="Quy trình này không có bước ký nháy/thể thức riêng."});
                }
                if(request.LoaiChuKy=="KY_LANH_DAO")
                    stage=(doc.SoBuocKy-1,"LANH_DAO","PHE_DUYET_LANH_DAO");
                var selected=request.LoaiChuKy=="KY_NHAY"?doc.MaNguoiKyNhay:doc.MaNguoiKyLanhDao;
                if(selected!=actor.MaNguoiDung){
                    var proxyTask=await cn.QueryFirstOrDefaultAsync<Guid?>(@"
                        SELECT TOP 1 t.MaTacVu FROM dbo.TacVuXuLy t
                        WHERE t.MaVanBan=@id AND t.MaCoQuan=@agency
                          AND t.MaNguoiDuocGiao=@selected AND t.Buoc=@step
                          AND t.PhienBanDuocDuyet=@version AND t.TrangThai='CHO_XU_LY'",
                        new{id,agency=actor.MaCoQuan,selected,step=stage.TaskStep,version=doc.PhienBanHienTai},tx);
                    if(proxyTask==null || !await UyQuyenKyEndpoints.DangHieuLuc(cn,tx,proxyTask.Value,
                        actor.MaCoQuan.Value,actor.MaNguoiDung,selected??0)){
                        tx.Rollback();return Results.Forbid();
                    }
                }
            }
            var command="KY_"+stage.Order;
            var old=await LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,command);
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            var authorizedRole=await CoVaiTro(cn,tx,actor,stage.Role);
            if(doc.SoBuocKy is 4 or 5 && request.LoaiChuKy=="KY_NHAY")
                authorizedRole=authorizedRole || await CoVaiTro(cn,tx,actor,"PHO_PHONG") ||
                    await CoVaiTro(cn,tx,actor,"TRUONG_VAN_PHONG") ||
                    await CoVaiTro(cn,tx,actor,"PHO_VAN_PHONG");
            if(doc.SoBuocKy is 3 or 4 or 5 && request.LoaiChuKy=="KY_LANH_DAO")
                authorizedRole=authorizedRole || await CoVaiTro(cn,tx,actor,"PHO_LANH_DAO");
            if(!authorizedRole){
                tx.Rollback();return Results.Forbid();
            }
            if(doc.TrangThai!="CHO_DUYET"){
                tx.Rollback();return Results.Conflict(new{message="Văn bản phải ở bước chờ duyệt để ký theo cấp."});
            }
            var assigned=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.TacVuXuLy t WITH(UPDLOCK,HOLDLOCK)
                WHERE t.MaVanBan=@id AND t.MaCoQuan=@agency
                  AND (t.MaNguoiDuocGiao=@user OR EXISTS(
                    SELECT 1 FROM dbo.UyQuyenKyTacVu q WHERE q.MaTacVu=t.MaTacVu
                      AND q.MaCoQuan=@agency AND q.MaNguoiGoc=t.MaNguoiDuocGiao
                      AND q.MaNguoiDuocUyQuyen=@user AND q.DaThuHoi=0
                      AND q.HieuLucTu<=SYSUTCDATETIME() AND q.HieuLucDen>SYSUTCDATETIME()))
                  AND t.Buoc=@taskStep AND t.PhienBanDuocDuyet=@version AND t.TrangThai='CHO_XU_LY'",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    taskStep=stage.TaskStep,version=doc.PhienBanHienTai},tx);
            if(assigned!=1){tx.Rollback();return Results.Forbid();}
            var previous=await LayBan(cn,tx,id,doc.PhienBanHienTai,stage.Order-1);
            if(previous==null){tx.Rollback();return Results.Conflict(new{message="Chưa đủ chữ ký theo thứ tự bắt buộc."});}
            var already=await LayBan(cn,tx,id,doc.PhienBanHienTai,stage.Order);
            if(already!=null){tx.Rollback();return Results.Conflict(new{message="Bước ký này đã hoàn thành."});}
            if(!File.Exists(previous.DuongDanLuuTru) ||
               !string.Equals(await DichVuKyPdf.Bam(previous.DuongDanLuuTru),previous.MaBamSha256,StringComparison.OrdinalIgnoreCase)){
                tx.Rollback();return Results.Conflict(new{message="Bản PDF trước ký không toàn vẹn."});
            }
            var priorSignatures=signer.XacMinh(previous.DuongDanLuuTru);
            if(priorSignatures.Count!=stage.Order-1 || priorSignatures.Any(s=>!s.ToanVenMatMa) ||
               !priorSignatures[^1].BaoPhuToanBoBanHienTai){
                tx.Rollback();return Results.Conflict(new{message="Chuỗi chữ ký các cấp trước không hợp lệ."});
            }
            int? owner=actor.MaNguoiDung;
            var cert=await cn.QuerySingleOrDefaultAsync<ChungThuLuu>(@"
                SELECT TOP 1 MaChungThu,MaCoQuan,MaNguoiSoHuu,DuongDanKhoaMaHoa,MaKhoaMaHoa,
                    ChungThuCongKhai,NguonChungThu,HinhAnhChuKy
                FROM dbo.ChungThuKy WHERE MaCoQuan=@agency AND DangHoatDong=1
                  AND MaChungThu=@selected
                  AND ((MaNguoiSoHuu=@owner) OR (MaNguoiSoHuu IS NULL AND @owner IS NULL))
                  AND NgayHieuLuc<=SYSUTCDATETIME() AND NgayHetHan>SYSUTCDATETIME()
                  AND LoaiChungThu=@type ORDER BY ThoiDiemTao DESC",
                new{agency=actor.MaCoQuan,owner,selected=request.MaChungThu,type=owner is null?"CO_QUAN":"CA_NHAN"},tx);
            if(cert==null){tx.Rollback();return Results.Conflict(new{message="Chưa cấu hình chứng thư hợp lệ cho người/cơ quan ký."});}
            if(cert.MaKhoaMaHoa!=signer.MaKhoa()){
                tx.Rollback();return Results.Conflict(new{message="Khóa mã hóa chứng thư đã luân chuyển; cần bộ giải mã cũ."});
            }
            var fileId=Guid.NewGuid();
            var path=TaoDuongDan(config,actor.MaCoQuan.Value,id,fileId);
            var field=$"Ky_{stage.Order}_{id:N}_v{doc.PhienBanHienTai}";
            try {
                var signed=signer.Ky(previous.DuongDanLuuTru,path,field,cert.MaChungThu,
                    actor.MaCoQuan.Value,owner,cert.DuongDanKhoaMaHoa,request.LoaiChuKy!,
                    ViTriDaDangKy(request,cert,request.LoaiChuKy switch{
                        "KY_NHAY"=>"Ky nhay","KY_THE_THUC"=>"Kiem tra the thuc",_=>"Lanh dao"}));
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
                const string next="CHO_DUYET";
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                    VALUES(@id,@agency,@user,@type,@message,'CHO_DUYET',@next,@version)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,type=request.LoaiChuKy,
                        message=$"{actor.HoTen} ký số PDF; bước {stage.Order}/{doc.SoBuocKy}, SHA-256 {hash[..16]}…",
                        next,version=doc.PhienBanHienTai},tx);
                var json=JsonSerializer.Serialize(new{maVanBan=id,loaiChuKy=request.LoaiChuKy,
                    trangThai=next,maBan=fileId,maBamSha256=hash,hopLeMatMa=true,
                    thamDinhCa=cert.NguonChungThu=="TU_KY_THU_NGHIEM"?"INTERNAL_SELF_SIGNED":"CHUA_THAM_DINH_CA"});
                await GhiBienNhan(cn,tx,actor,request.MaYeuCau,command,json);
                tx.Commit();return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is CryptographicException or InvalidDataException){
                tx.Rollback();XoaTepTam(path);
                return Results.BadRequest(new{message="Không ký được PDF: "+ex.Message});
            }catch{tx.Rollback();XoaTepTam(path);throw;}
        }).RequireAuthorization();

        // v0.5.4: the fifth (agency) signature appends directly to the stage-4 PDF.
        // Issue number stays in the immutable register; no PDF rewrite after signing stages 1-4.
        app.MapPost("/api/v1/documents/{id:guid}/agency-signature",async(Guid id,KyVanBan request,
            HttpContext http,PhanQuyen permissions,Database db,DichVuKyPdf signer,IConfiguration config)=>{
            var(actor,error)=await permissions.YeuCau(http,"signing.sign");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(request.MaYeuCau==Guid.Empty || request.LoaiChuKy!="KY_CO_QUAN")
                return Results.BadRequest(new{message="Loại chữ ký hoặc MaYeuCau không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if(!await CoVaiTro(cn,tx,actor,"VAN_THU")){
                tx.Rollback();return Results.Forbid();
            }
            if(!await KiemTraMatKhau(cn,tx,actor,request.MatKhau)){
                tx.Rollback();return Results.BadRequest(new{message="Sai mật khẩu xác nhận ký cơ quan."});
            }
            var doc=await LayVanBan(cn,tx,id,actor.MaCoQuan.Value);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.TrangThai!="DA_CAP_SO" || doc.SoPhatHanh is null || string.IsNullOrWhiteSpace(doc.KyHieu)){
                tx.Rollback();return Results.Conflict(new{message="Văn bản chưa được cấp số và ký hiệu."});
            }
            var agencyOrder=doc.SoBuocKy;
            var old=await LayBienNhan(cn,tx,actor.MaCoQuan.Value,request.MaYeuCau,"KY_"+agencyOrder);
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            if(await LayBan(cn,tx,id,doc.PhienBanHienTai,agencyOrder)!=null){
                tx.Rollback();return Results.Conflict(new{message="Chứng thư cơ quan đã ký phiên bản này."});
            }
            var prior=await LayBan(cn,tx,id,doc.PhienBanHienTai,agencyOrder-1);
            if(prior==null || !File.Exists(prior.DuongDanLuuTru) ||
               !string.Equals(await DichVuKyPdf.Bam(prior.DuongDanLuuTru),prior.MaBamSha256,StringComparison.OrdinalIgnoreCase)){
                tx.Rollback();return Results.Conflict(new{message="Không có PDF bốn chữ ký hợp lệ."});
            }
            var earlier=signer.XacMinh(prior.DuongDanLuuTru);
            if(earlier.Count!=agencyOrder-1 || earlier.Any(s=>!s.ToanVenMatMa) || !earlier[^1].BaoPhuToanBoBanHienTai){
                tx.Rollback();return Results.Conflict(new{message="Bốn chữ ký trước chưa toàn vẹn."});
            }
            var cert=await cn.QuerySingleOrDefaultAsync<ChungThuLuu>(@"
                SELECT TOP 1 MaChungThu,MaCoQuan,MaNguoiSoHuu,DuongDanKhoaMaHoa,MaKhoaMaHoa,
                    ChungThuCongKhai,NguonChungThu,HinhAnhChuKy FROM dbo.ChungThuKy
                WHERE MaCoQuan=@agency AND MaNguoiSoHuu IS NULL AND LoaiChungThu='CO_QUAN'
                    AND MaChungThu=@selected
                    AND DangHoatDong=1 AND NgayHieuLuc<=SYSUTCDATETIME()
                    AND NgayHetHan>SYSUTCDATETIME() ORDER BY ThoiDiemTao DESC",
                new{agency=actor.MaCoQuan,selected=request.MaChungThu},tx);
            if(cert==null){tx.Rollback();return Results.Conflict(new{message="Cơ quan chưa có chứng thư số đang hoạt động."});}
            if(cert.MaKhoaMaHoa!=signer.MaKhoa()){
                tx.Rollback();return Results.Conflict(new{message="Khóa mã hóa chứng thư không đúng bộ khóa hiện tại."});
            }
            var fileId=Guid.NewGuid();var path=TaoDuongDan(config,actor.MaCoQuan.Value,id,fileId);
            var field=$"Ky_{agencyOrder}_{id:N}_v{doc.PhienBanHienTai}";
            try{
                var signed=signer.Ky(prior.DuongDanLuuTru,path,field,cert.MaChungThu,
                    actor.MaCoQuan.Value,null,cert.DuongDanKhoaMaHoa,"KY_CO_QUAN",
                    ViTriDaDangKy(request,cert,$"So: {doc.SoPhatHanh}/{doc.KyHieu}",true));
                var checks=signer.XacMinh(path);
                if(checks.Count!=agencyOrder || checks.Any(s=>!s.ToanVenMatMa) || !checks[^1].BaoPhuToanBoBanHienTai)
                    throw new CryptographicException("Chuỗi chữ ký PDF không toàn vẹn.");
                var hash=await DichVuKyPdf.Bam(path);
                await cn.ExecuteAsync(@"
                    INSERT dbo.BanPdfDaKy(MaBan,MaVanBan,MaCoQuan,SoPhienBan,ThuTuKy,MaBanTruoc,
                        DuongDanLuuTru,MaBamSha256,TenTruongKy)
                    VALUES(@fileId,@id,@agency,@version,@order,@prior,@path,@hash,@field)",
                    new{fileId,id,agency=actor.MaCoQuan,version=doc.PhienBanHienTai,
                        order=agencyOrder,prior=prior.MaBan,path,hash,field},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.ChuKySo(MaVanBan,MaCoQuan,MaNguoiKy,SoPhienBan,LoaiChuKy,DinhDangChuKy,
                        DuLieuChuKy,ChungThuCongKhai,MaBamTaiLieu,MaBamChuKyTruoc,KetQuaXacMinh,
                        MucDoTinCay,MaBanDaKy,MaChungThu,TenTruongKy)
                    VALUES(@id,@agency,@user,@version,'KY_CO_QUAN','PADES_CMS',@cms,@publicCert,
                        @hash,@priorHash,'HOP_LE_MAT_MA',@trust,@fileId,@certId,@field)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,version=doc.PhienBanHienTai,
                        cms=signed.DuLieuCms,publicCert=cert.ChungThuCongKhai,hash,priorHash=prior.MaBamSha256,
                        fileId,certId=cert.MaChungThu,field,
                        trust=cert.NguonChungThu=="TU_KY_THU_NGHIEM"?"INTERNAL_SELF_SIGNED":"CHUA_THAM_DINH_CA"},tx);
                await cn.ExecuteAsync("UPDATE dbo.VanBan SET TrangThai='DA_KY',ThoiDiemCapNhat=SYSUTCDATETIME() WHERE MaVanBan=@id",new{id},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                    VALUES(@id,@agency,@user,'KY_CO_QUAN',@description,'DA_CAP_SO','DA_KY',@version)",
                    new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,version=doc.PhienBanHienTai,
                        description=$"Đã ký PDF bước {agencyOrder}/{agencyOrder}; số {doc.SoPhatHanh}/{doc.KyHieu}; SHA-256 {hash[..16]}…"},tx);
                var json=JsonSerializer.Serialize(new{maVanBan=id,trangThai="DA_KY",loaiChuKy="KY_CO_QUAN",
                    maBan=fileId,so=doc.SoPhatHanh,kyHieu=doc.KyHieu,maBamSha256=hash,hopLeMatMa=true});
                await GhiBienNhan(cn,tx,actor,request.MaYeuCau,"KY_"+agencyOrder,json);
                tx.Commit();return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is CryptographicException or InvalidDataException){
                tx.Rollback();XoaTepTam(path);
                return Results.BadRequest(new{message="Không ký được PDF cơ quan: "+ex.Message});
            }catch{tx.Rollback();XoaTepTam(path);throw;}
        }).RequireAuthorization();

        app.MapGet("/api/v1/documents/{id:guid}/signatures/verification",async(Guid id,
            HttpContext http,PhanQuyen permissions,Database db,DichVuKyPdf signer)=>{
            var(actor,error)=await permissions.YeuCau(http,"documents.read");
            if(error!=null)return error;
            // Dùng cùng SQL scope với GET /documents/{id}, ngay trong truy vấn tải.
            using var cn=db.Open();
            var pdf=await cn.QueryFirstOrDefaultAsync<BanPdf>(@"
                SELECT TOP 1 p.MaBan,p.DuongDanLuuTru,p.MaBamSha256
                FROM dbo.BanPdfDaKy p JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE p.MaVanBan=@id AND p.MaCoQuan=@agency AND v.MaCoQuan=@agency
                  AND p.SoPhienBan=v.PhienBanHienTai" + VanBanEndpoints.PhamViVanBan + @"
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
                WHERE p.MaVanBan=@id AND p.MaCoQuan=@agency AND v.MaCoQuan=@agency
                  AND p.SoPhienBan=v.PhienBanHienTai" + VanBanEndpoints.PhamViVanBan + @"
                ORDER BY p.ThuTuKy DESC",new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung});
            if(pdf==null || !File.Exists(pdf.DuongDanLuuTru))return Results.NotFound();
            if(await DichVuKyPdf.Bam(pdf.DuongDanLuuTru)!=pdf.MaBamSha256)
                return Results.Conflict(new{message="PDF không còn nguyên vẹn."});
            return Results.File(pdf.DuongDanLuuTru,"application/pdf",$"van-ban-{id:N}-da-ky.pdf");
        }).RequireAuthorization();
    }
}
