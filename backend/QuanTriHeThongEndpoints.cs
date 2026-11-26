using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

public sealed record TaoCoQuanHeThong(string MaDinhDanh,string TenCoQuan,string? DiaChi);
public sealed record SuaCoQuanHeThong(string TenCoQuan,string? DiaChi);
public sealed record TrangThaiCoQuanHeThong(bool DangHoatDong,string LyDo);
public sealed record TaoQuanTriCoQuan(int MaPhongBan,string TenDangNhap,string HoTen,string MatKhau);
public sealed record TrangThaiQuanTriCoQuan(bool DangHoatDong,string LyDo);

public static class QuanTriHeThongEndpoints
{
    private static async Task<(NguoiThucHien? Actor,IResult? Error)> KiemTra(
        HttpContext http,PhanQuyen permission,Database db,string quyen)
    {
        var (actor,error)=await permission.YeuCau(http,quyen);
        if(error!=null)return (null,error);
        if(actor!.MaCoQuan!=null)return (null,Results.Forbid());
        using var cn=db.Open();
        var laQuanTri=await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM dbo.NguoiDungVaiTro nvt
            JOIN dbo.VaiTro vt ON vt.MaVaiTro=nvt.MaVaiTro
            WHERE nvt.MaNguoiDung=@user AND nvt.MaCoQuan IS NULL AND vt.MaDinhDanh='SYSTEM_ADMIN'",
            new{user=actor.MaNguoiDung});
        return laQuanTri==0?(null,Results.Forbid()):(actor,null);
    }

    private static bool MaHopLe(string? value)=>value!=null && Regex.IsMatch(value,"^[A-Z0-9_-]{2,40}$");

    public static void MapQuanTriHeThong(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/system").RequireAuthorization();

        group.MapGet("/agencies",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var (_,error)=await KiemTra(http,permission,db,"agencies.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"
                SELECT c.MaCoQuan,c.MaDinhDanh,c.TenCoQuan,c.DiaChi,c.DangHoatDong,c.ThoiDiemTao,
                    (SELECT COUNT(1) FROM dbo.NguoiDung n WHERE n.MaCoQuan=c.MaCoQuan) AS SoTaiKhoan,
                    (SELECT COUNT(DISTINCT n.MaNguoiDung) FROM dbo.NguoiDung n
                     JOIN dbo.NguoiDungVaiTro uv ON uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=c.MaCoQuan
                     JOIN dbo.VaiTro vt ON vt.MaVaiTro=uv.MaVaiTro AND vt.MaDinhDanh='AGENCY_ADMIN'
                     WHERE n.MaCoQuan=c.MaCoQuan AND n.DangHoatDong=1) AS SoQuanTri
                FROM dbo.CoQuan c ORDER BY c.MaCoQuan DESC"));
        });

        group.MapPost("/agencies",async(TaoCoQuanHeThong body,HttpContext http,PhanQuyen permission,Database db)=>{
            var (actor,error)=await KiemTra(http,permission,db,"agencies.manage");
            if(error!=null)return error;
            var code=(body.MaDinhDanh??"").Trim().ToUpperInvariant();
            var name=(body.TenCoQuan??"").Trim();
            var address=body.DiaChi?.Trim();
            if(!MaHopLe(code)||name.Length is < 3 or > 200 || address?.Length>400)
                return Results.BadRequest(new{message="Mã cơ quan gồm 2–40 chữ in hoa, số, _ hoặc -; tên cơ quan 3–200 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if(await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.CoQuan WITH(UPDLOCK,HOLDLOCK) WHERE MaDinhDanh=@code",new{code},tx)>0){
                tx.Rollback();return Results.Conflict(new{message="Mã cơ quan đã tồn tại."});
            }
            // Copy only role-permission template, never document or user data from another agency.
            var template=await cn.ExecuteScalarAsync<int?>(@"
                SELECT TOP 1 MaCoQuan FROM dbo.VaiTroQuyen
                WHERE MaCoQuan IS NOT NULL GROUP BY MaCoQuan ORDER BY COUNT(*) DESC,MaCoQuan",transaction:tx);
            if(template==null){tx.Rollback();return Results.Conflict(new{message="Chưa có mẫu phân quyền cơ quan. Cần khởi tạo dữ liệu vai trò trước."});}
            var id=await cn.ExecuteScalarAsync<int>(@"
                INSERT dbo.CoQuan(MaDinhDanh,TenCoQuan,DiaChi)
                OUTPUT INSERTED.MaCoQuan VALUES(@code,@name,@address)",new{code,name,address},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.PhongBan(MaCoQuan,MaDinhDanh,TenPhongBan,ThuTuHienThi)
                VALUES(@id,'LANH_DAO',N'Ban lãnh đạo',10),(@id,'VAN_PHONG',N'Văn phòng',20);
                INSERT dbo.CauHinhQuyTrinh(MaCoQuan,SoPhienBan,CoKiemTraVanPhong)
                VALUES(@id,1,0);
                INSERT dbo.VaiTroQuyen(MaCoQuan,MaVaiTro,MaQuyen)
                SELECT @id,MaVaiTro,MaQuyen FROM dbo.VaiTroQuyen WHERE MaCoQuan=@template;
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@id,@user,'KY_THUAT','TAO_CO_QUAN',@message)",
                new{id,template,user=actor!.MaNguoiDung,message=$"Tạo cơ quan {code}: {name}"},tx);
            tx.Commit();return Results.Ok(new{maCoQuan=id,maDinhDanh=code,message="Đã tạo cơ quan. Hãy cấp tài khoản quản trị cơ quan."});
        });

        group.MapPut("/agencies/{id:int}",async(int id,SuaCoQuanHeThong body,HttpContext http,PhanQuyen permission,Database db)=>{
            var (actor,error)=await KiemTra(http,permission,db,"agencies.manage");
            if(error!=null)return error;
            var name=(body.TenCoQuan??"").Trim();
            if(name.Length is < 3 or > 200 || body.DiaChi?.Length>400)
                return Results.BadRequest(new{message="Tên cơ quan 3–200 ký tự, địa chỉ tối đa 400 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            if(await cn.ExecuteAsync("UPDATE dbo.CoQuan SET TenCoQuan=@name,DiaChi=@address WHERE MaCoQuan=@id",
                new{id,name,address=body.DiaChi?.Trim()},tx)==0){tx.Rollback();return Results.NotFound();}
            await cn.ExecuteAsync(@"INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@id,@user,'KY_THUAT','SUA_CO_QUAN',@message)",
                new{id,user=actor!.MaNguoiDung,message=$"Cập nhật cơ quan {id}: {name}"},tx);
            tx.Commit();return Results.Ok(new{message="Đã cập nhật cơ quan."});
        });

        group.MapPut("/agencies/{id:int}/status",async(int id,TrangThaiCoQuanHeThong body,HttpContext http,PhanQuyen permission,Database db)=>{
            var (actor,error)=await KiemTra(http,permission,db,"agencies.manage");
            if(error!=null)return error;
            if(string.IsNullOrWhiteSpace(body.LyDo)||body.LyDo.Trim().Length<5||body.LyDo.Length>300)
                return Results.BadRequest(new{message="Cần nêu lý do (5–300 ký tự)."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            if(await cn.ExecuteAsync("UPDATE dbo.CoQuan SET DangHoatDong=@active WHERE MaCoQuan=@id",
                new{id,active=body.DangHoatDong},tx)==0){tx.Rollback();return Results.NotFound();}
            await cn.ExecuteAsync(@"INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@id,@user,'KY_THUAT','TRANG_THAI_CO_QUAN',@message)",
                new{id,user=actor!.MaNguoiDung,message=$"{(body.DangHoatDong?"Mở lại":"Ngừng")} cơ quan: {body.LyDo.Trim()}"},tx);
            tx.Commit();return Results.Ok(new{message="Đã cập nhật trạng thái cơ quan."});
        });

        group.MapGet("/agencies/{id:int}/departments",async(int id,HttpContext http,PhanQuyen permission,Database db)=>{
            var (_,error)=await KiemTra(http,permission,db,"agency.admins.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"
                SELECT MaPhongBan,TenPhongBan FROM dbo.PhongBan
                WHERE MaCoQuan=@id AND DangHoatDong=1 ORDER BY ThuTuHienThi,TenPhongBan",new{id}));
        });

        group.MapGet("/agencies/{id:int}/admins",async(int id,HttpContext http,PhanQuyen permission,Database db)=>{
            var (_,error)=await KiemTra(http,permission,db,"agency.admins.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            if(await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.CoQuan WHERE MaCoQuan=@id",new{id})==0)return Results.NotFound();
            return Results.Ok(await cn.QueryAsync(@"
                SELECT DISTINCT n.MaNguoiDung,n.TenDangNhap,n.HoTen,n.DangHoatDong,n.MaPhongBan
                FROM dbo.NguoiDung n JOIN dbo.NguoiDungVaiTro uv
                  ON uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=@id
                JOIN dbo.VaiTro vt ON vt.MaVaiTro=uv.MaVaiTro AND vt.MaDinhDanh='AGENCY_ADMIN'
                WHERE n.MaCoQuan=@id ORDER BY n.HoTen",new{id}));
        });

        group.MapPost("/agencies/{id:int}/admins",async(int id,TaoQuanTriCoQuan body,HttpContext http,PhanQuyen permission,Database db)=>{
            var (actor,error)=await KiemTra(http,permission,db,"agency.admins.manage");
            if(error!=null)return error;
            var username=(body.TenDangNhap??"").Trim().ToLowerInvariant();
            var name=(body.HoTen??"").Trim();
            if(!Regex.IsMatch(username,"^[a-z0-9_.-]{4,100}$")||name.Length is < 3 or > 150||
                string.IsNullOrWhiteSpace(body.MatKhau)||body.MatKhau.Length<12)
                return Results.BadRequest(new{message="Tên đăng nhập 4–100 ký tự, họ tên 3–150 ký tự, mật khẩu tối thiểu 12 ký tự."});
            // Hash before opening the transaction to avoid a long-held serializable lock.
            var hash=MatKhau.TaoBam(body.MatKhau);
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if(await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(1) FROM dbo.PhongBan p JOIN dbo.CoQuan c ON c.MaCoQuan=p.MaCoQuan
                WHERE p.MaPhongBan=@department AND p.MaCoQuan=@id AND p.DangHoatDong=1 AND c.DangHoatDong=1",
                new{department=body.MaPhongBan,id},tx)==0){tx.Rollback();return Results.BadRequest(new{message="Phòng ban không hợp lệ hoặc cơ quan đang ngừng hoạt động."});}
            if(await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.NguoiDung WITH(UPDLOCK,HOLDLOCK) WHERE TenDangNhap=@username",new{username},tx)>0){
                tx.Rollback();return Results.Conflict(new{message="Tên đăng nhập đã tồn tại."});
            }
            var role=await cn.ExecuteScalarAsync<int?>("SELECT MaVaiTro FROM dbo.VaiTro WHERE MaDinhDanh='AGENCY_ADMIN'",transaction:tx);
            if(role==null){tx.Rollback();return Results.Conflict(new{message="Thiếu vai trò quản trị cơ quan."});}
            var user=await cn.ExecuteScalarAsync<int>(@"
                INSERT dbo.NguoiDung(MaCoQuan,MaPhongBan,TenDangNhap,MatKhauBam,HoTen,ChucVu)
                OUTPUT INSERTED.MaNguoiDung VALUES(@id,@department,@username,@hash,@name,N'Quản trị cơ quan')",
                new{id,department=body.MaPhongBan,username,hash,name},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.NguoiDungVaiTro(MaNguoiDung,MaVaiTro,MaCoQuan) VALUES(@user,@role,@id);
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@id,@actor,'KY_THUAT','CAP_QUAN_TRI_CO_QUAN',@message)",
                new{user,role,id,actor=actor!.MaNguoiDung,message=$"Cấp quản trị {username} cho cơ quan {id}"},tx);
            tx.Commit();return Results.Ok(new{maNguoiDung=user,message="Đã cấp quản trị viên cơ quan."});
        });

        group.MapPut("/agencies/{id:int}/admins/{user:int}/status",async(int id,int user,
            TrangThaiQuanTriCoQuan body,HttpContext http,PhanQuyen permission,Database db)=>{
            var (actor,error)=await KiemTra(http,permission,db,"agency.admins.manage");
            if(error!=null)return error;
            if(string.IsNullOrWhiteSpace(body.LyDo)||body.LyDo.Trim().Length<5||body.LyDo.Length>300)
                return Results.BadRequest(new{message="Cần nêu lý do (5–300 ký tự)."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var changed=await cn.ExecuteAsync(@"
                UPDATE n SET DangHoatDong=@active FROM dbo.NguoiDung n
                WHERE n.MaNguoiDung=@user AND n.MaCoQuan=@id
                  AND EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro uv
                      JOIN dbo.VaiTro vt ON vt.MaVaiTro=uv.MaVaiTro AND vt.MaDinhDanh='AGENCY_ADMIN'
                      WHERE uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=@id)",
                new{id,user,active=body.DangHoatDong},tx);
            if(changed==0){tx.Rollback();return Results.NotFound();}
            await cn.ExecuteAsync(@"INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@id,@actor,'KY_THUAT','TRANG_THAI_QUAN_TRI',@message)",
                new{id,actor=actor!.MaNguoiDung,message=$"{(body.DangHoatDong?"Mở lại":"Khóa")} quản trị {user}: {body.LyDo.Trim()}"},tx);
            tx.Commit();return Results.Ok(new{message="Đã cập nhật tài khoản quản trị."});
        });

        group.MapGet("/audit",async(int? page,HttpContext http,PhanQuyen permission,Database db)=>{
            var (_,error)=await KiemTra(http,permission,db,"audit.technical.read");
            if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"
                SELECT nk.MaNhatKy,nk.MaCoQuan,nk.HanhDong,nk.MoTa,nk.ThoiDiem,n.HoTen AS NguoiThucHien
                FROM dbo.NhatKyHeThong nk LEFT JOIN dbo.NguoiDung n ON n.MaNguoiDung=nk.MaNguoiDung
                WHERE nk.LoaiNhatKy='KY_THUAT' AND nk.HanhDong IN
                    ('TAO_CO_QUAN','SUA_CO_QUAN','TRANG_THAI_CO_QUAN','CAP_QUAN_TRI_CO_QUAN','TRANG_THAI_QUAN_TRI')
                ORDER BY nk.MaNhatKy DESC OFFSET @skip ROWS FETCH NEXT 20 ROWS ONLY",
                new{skip=(Math.Clamp(page??1,1,10000)-1)*20}));
        });
    }
}
