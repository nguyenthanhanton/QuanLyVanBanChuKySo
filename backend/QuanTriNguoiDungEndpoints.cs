using Dapper;
using Microsoft.Data.SqlClient;

public sealed record TaoTaiKhoan(string TenDangNhap,string HoTen,string MatKhau,int MaPhongBan,string MaVaiTro,string? TenChucVu);
public sealed record PhanCongTaiKhoan(int MaPhongBan,string MaVaiTro,string? TenChucVu);
public sealed record DoiTrangThaiTaiKhoan(bool DangHoatDong);

public static class QuanTriNguoiDungEndpoints
{
    private sealed class PhongBanChucVu
    {
        public int MaPhongBan {get;set;}
        public string MaDinhDanh {get;set;}="";
    }
    private static readonly string[] VaiTroNghiepVu = [
        "VAN_THU","CHUYEN_VIEN","TRUONG_PHONG","PHO_PHONG","LANH_DAO","PHO_LANH_DAO"
    ];

    private static bool HopLeChucVu(string role,string department) =>
        role is "LANH_DAO" or "PHO_LANH_DAO" or "VAN_THU"
            ? department=="LANH_DAO" : department!="LANH_DAO";

    private static string? LayTenChucVu(string? label) => string.IsNullOrWhiteSpace(label)?null:label.Trim();

    // One chief agency leader; one head per advisory department; one office head per agency.
    private static async Task<bool> DaCoCapTruong(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,int agency,int department,string role,int? excluding=null)
    {
        if(role is not ("LANH_DAO" or "TRUONG_PHONG"))return false;
        var count=await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(DISTINCT n.MaNguoiDung)
            FROM dbo.NguoiDung n WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.NguoiDungVaiTro uv ON uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=n.MaCoQuan
            JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
            WHERE n.MaCoQuan=@agency AND n.DangHoatDong=1 AND r.MaDinhDanh=@role
              AND (@excluding IS NULL OR n.MaNguoiDung<>@excluding)
              AND (@role<>'TRUONG_PHONG' OR n.MaPhongBan=@department)",
            new{agency,department,role,excluding},tx);
        return count>0;
    }

    private static async Task<bool> DaCoVanThu(SqlConnection cn,SqlTransaction tx,int agency,int? excluding=null)
        => await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(DISTINCT n.MaNguoiDung) FROM dbo.NguoiDung n WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.NguoiDungVaiTro uv ON uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=n.MaCoQuan
            JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
            WHERE n.MaCoQuan=@agency AND n.DangHoatDong=1 AND r.MaDinhDanh='VAN_THU'
              AND (@excluding IS NULL OR n.MaNguoiDung<>@excluding)",new{agency,excluding},tx)>0;

    public static void MapQuanTriNguoiDung(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin").RequireAuthorization();

        group.MapGet("/options",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"users.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            var departments=await cn.QueryAsync(@"
                SELECT MaPhongBan,TenPhongBan,MaDinhDanh,ThuTuHienThi,MaPhongBanCha FROM dbo.PhongBan
                WHERE MaCoQuan=@agency AND DangHoatDong=1 ORDER BY ThuTuHienThi,TenPhongBan",
                new{agency=actor!.MaCoQuan});
            var roles=await cn.QueryAsync(@"
                SELECT MaDinhDanh,TenVaiTro FROM dbo.VaiTro
                WHERE MaDinhDanh IN @roles ORDER BY TenVaiTro",
                new{roles=VaiTroNghiepVu});
            return Results.Ok(new{phongBan=departments,vaiTro=roles});
        });

        group.MapGet("/users",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"users.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            var users=await cn.QueryAsync(@"
                SELECT n.MaNguoiDung,n.TenDangNhap,n.HoTen,n.ChucVu,n.MaPhongBan,
                       p.TenPhongBan,n.DangHoatDong,
                       (SELECT TOP 1 r.MaDinhDanh FROM dbo.NguoiDungVaiTro uv
                        JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
                        WHERE uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=@agency
                        ORDER BY r.MaVaiTro) AS MaVaiTro
                FROM dbo.NguoiDung n LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=n.MaPhongBan
                WHERE n.MaCoQuan=@agency ORDER BY n.HoTen",
                new{agency=actor!.MaCoQuan});
            return Results.Ok(users);
        });

        group.MapPost("/users",async(TaoTaiKhoan body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"users.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            var username=body.TenDangNhap?.Trim().ToLowerInvariant()??"";
            var fullname=body.HoTen?.Trim()??"";
            if(username.Length is < 4 or > 100 || !System.Text.RegularExpressions.Regex.IsMatch(username,"^[a-z0-9_.-]+$"))
                return Results.BadRequest(new{message="Tên đăng nhập cần 4–100 ký tự không dấu (a-z, 0-9, _, ., -)."});
            if(fullname.Length is < 3 or > 150 || string.IsNullOrWhiteSpace(body.MatKhau) || body.MatKhau.Length<12)
                return Results.BadRequest(new{message="Họ tên cần 3–150 ký tự; mật khẩu mới phải có ít nhất 12 ký tự."});
            if(!VaiTroNghiepVu.Contains(body.MaVaiTro))return Results.BadRequest(new{message="Chức vụ không hợp lệ."});
            var title=LayTenChucVu(body.TenChucVu);
            if(title?.Length>150)return Results.BadRequest(new{message="Tên gọi chức vụ tối đa 150 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var role=await cn.ExecuteScalarAsync<int?>("SELECT MaVaiTro FROM dbo.VaiTro WHERE MaDinhDanh=@name",
                new{name=body.MaVaiTro},tx);
            var department=await cn.QuerySingleOrDefaultAsync<PhongBanChucVu>(@"
                SELECT MaPhongBan,MaDinhDanh FROM dbo.PhongBan WHERE MaCoQuan=@agency
                    AND MaPhongBan=@department AND DangHoatDong=1",
                new{agency=actor.MaCoQuan,department=body.MaPhongBan},tx);
            if(role==null || department==null){tx.Rollback();return Results.BadRequest(new{message="Phòng ban hoặc chức vụ chưa được cấu hình."});}
            if(!HopLeChucVu(body.MaVaiTro,department.MaDinhDanh)){
                tx.Rollback();return Results.BadRequest(new{message="Chức vụ lãnh đạo cơ quan chỉ thuộc Ban lãnh đạo; các phòng khác sử dụng chức vụ phòng ban."});
            }
            if(await DaCoCapTruong(cn,tx,actor.MaCoQuan.Value,body.MaPhongBan,body.MaVaiTro)){
                tx.Rollback();return Results.Conflict(new{message="Đơn vị đã có cấp trưởng đang hoạt động. Hãy chọn cấp phó hoặc điều chỉnh bổ nhiệm hiện tại trước."});
            }
            if(body.MaVaiTro=="VAN_THU" && await DaCoVanThu(cn,tx,actor.MaCoQuan.Value)){
                tx.Rollback();return Results.Conflict(new{message="Cơ quan đã có một Văn thư đang hoạt động."});
            }
            var roleName=await cn.ExecuteScalarAsync<string>("SELECT TenVaiTro FROM dbo.VaiTro WHERE MaVaiTro=@role",new{role},tx);
            try{
                var userId=await cn.ExecuteScalarAsync<int>(@"
                    INSERT dbo.NguoiDung(MaCoQuan,MaPhongBan,TenDangNhap,MatKhauBam,HoTen,ChucVu)
                    VALUES(@agency,@department,@username,@hash,@fullname,@position);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new{agency=actor.MaCoQuan,department=body.MaPhongBan,username,
                        hash=MatKhau.TaoBam(body.MatKhau),fullname,position=title??roleName},tx);
                await cn.ExecuteAsync(@"
                    INSERT dbo.NguoiDungVaiTro(MaNguoiDung,MaVaiTro,MaCoQuan)
                    VALUES(@userId,@role,@agency);
                    INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                    VALUES(@agency,@actor,'NGHIEP_VU','TAO_TAI_KHOAN',@description)",
                    new{userId,role,agency=actor.MaCoQuan,actor=actor.MaNguoiDung,
                        description=$"Tạo tài khoản {username} ({userId}), vai trò {body.MaVaiTro}."},tx);
                tx.Commit();return Results.Created($"/api/v1/admin/users/{userId}",new{maNguoiDung=userId,tenDangNhap=username});
            }catch(SqlException ex)when(ex.Number is 2601 or 2627){
                tx.Rollback();return Results.Conflict(new{message="Tên đăng nhập đã tồn tại."});
            }
        });

        group.MapPut("/users/{id:int}/assignment",async(int id,PhanCongTaiKhoan body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"users.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null || actor.MaNguoiDung==id)return Results.Forbid();
            if(!VaiTroNghiepVu.Contains(body.MaVaiTro))return Results.BadRequest(new{message="Chức vụ không hợp lệ."});
            var title=LayTenChucVu(body.TenChucVu);
            if(title?.Length>150)return Results.BadRequest(new{message="Tên gọi chức vụ tối đa 150 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var userId=await cn.ExecuteScalarAsync<int?>(@"
                SELECT MaNguoiDung FROM dbo.NguoiDung WITH(UPDLOCK,HOLDLOCK)
                WHERE MaNguoiDung=@id AND MaCoQuan=@agency",
                new{id,agency=actor.MaCoQuan},tx);
            if(userId==null){tx.Rollback();return Results.NotFound();}
            var adminRole=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro v ON v.MaVaiTro=uv.MaVaiTro
                WHERE uv.MaNguoiDung=@id AND uv.MaCoQuan=@agency AND v.MaDinhDanh='AGENCY_ADMIN'",
                new{id,agency=actor.MaCoQuan},tx);
            if(adminRole>0){tx.Rollback();return Results.Forbid();}
            var department=await cn.QuerySingleOrDefaultAsync<PhongBanChucVu>(@"
                SELECT MaPhongBan,MaDinhDanh FROM dbo.PhongBan WHERE MaPhongBan=@department
                  AND MaCoQuan=@agency AND DangHoatDong=1",
                new{department=body.MaPhongBan,agency=actor.MaCoQuan},tx);
            var role=await cn.ExecuteScalarAsync<int?>("SELECT MaVaiTro FROM dbo.VaiTro WHERE MaDinhDanh=@role",
                new{role=body.MaVaiTro},tx);
            if(department==null || role==null){tx.Rollback();return Results.BadRequest(new{message="Phòng ban hoặc vai trò không hợp lệ."});}
            if(!HopLeChucVu(body.MaVaiTro,department.MaDinhDanh)){
                tx.Rollback();return Results.BadRequest(new{message="Chức vụ không phù hợp với phòng ban được chọn."});
            }
            if(await DaCoCapTruong(cn,tx,actor.MaCoQuan.Value,body.MaPhongBan,body.MaVaiTro,id)){
                tx.Rollback();return Results.Conflict(new{message="Đơn vị đã có cấp trưởng đang hoạt động."});
            }
            if(body.MaVaiTro=="VAN_THU" && await DaCoVanThu(cn,tx,actor.MaCoQuan.Value,id)){
                tx.Rollback();return Results.Conflict(new{message="Cơ quan đã có một Văn thư đang hoạt động."});
            }
            var roleName=await cn.ExecuteScalarAsync<string>("SELECT TenVaiTro FROM dbo.VaiTro WHERE MaVaiTro=@role",new{role},tx);
            await cn.ExecuteAsync(@"
                DELETE FROM dbo.NguoiDungVaiTro WHERE MaNguoiDung=@id AND MaCoQuan=@agency;
                UPDATE dbo.NguoiDung SET MaPhongBan=@department,ChucVu=@position WHERE MaNguoiDung=@id AND MaCoQuan=@agency;
                INSERT dbo.NguoiDungVaiTro(MaNguoiDung,MaVaiTro,MaCoQuan) VALUES(@id,@role,@agency);
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@agency,@actor,'NGHIEP_VU','PHAN_CONG_VAI_TRO',@description)",
                new{id,agency=actor.MaCoQuan,department=body.MaPhongBan,role,position=title??roleName,
                    actor=actor.MaNguoiDung,description=$"Gán {body.MaVaiTro} cho tài khoản {id}, phòng {body.MaPhongBan}."},tx);
            tx.Commit();return Results.Ok(new{maNguoiDung=id,maPhongBan=body.MaPhongBan,maVaiTro=body.MaVaiTro});
        });

        group.MapPut("/users/{id:int}/status",async(int id,DoiTrangThaiTaiKhoan body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"users.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null || actor.MaNguoiDung==id)return Results.Forbid();
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var adminRole=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro v ON v.MaVaiTro=uv.MaVaiTro
                WHERE uv.MaNguoiDung=@id AND uv.MaCoQuan=@agency AND v.MaDinhDanh='AGENCY_ADMIN'",
                new{id,agency=actor.MaCoQuan},tx);
            if(adminRole>0){tx.Rollback();return Results.Forbid();}
            if(body.DangHoatDong && await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
                WHERE uv.MaNguoiDung=@id AND uv.MaCoQuan=@agency AND r.MaDinhDanh='VAN_THU'",
                new{id,agency=actor.MaCoQuan},tx)>0 &&
                await DaCoVanThu(cn,tx,actor.MaCoQuan.Value,id)){
                tx.Rollback();return Results.Conflict(new{message="Không thể mở tài khoản: cơ quan đã có một Văn thư hoạt động."});
            }
            var changed=await cn.ExecuteAsync(@"
                UPDATE dbo.NguoiDung SET DangHoatDong=@enabled
                WHERE MaNguoiDung=@id AND MaCoQuan=@agency",
                new{id,agency=actor.MaCoQuan,enabled=body.DangHoatDong},tx);
            if(changed==0){tx.Rollback();return Results.NotFound();}
            await cn.ExecuteAsync(@"
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@agency,@actor,'NGHIEP_VU',@action,@description)",
                new{agency=actor.MaCoQuan,actor=actor.MaNguoiDung,
                    action=body.DangHoatDong?"MO_TAI_KHOAN":"KHOA_TAI_KHOAN",
                    description=$"Đổi trạng thái tài khoản {id}: {(body.DangHoatDong?"hoạt động":"khóa")}."},tx);
            tx.Commit();return Results.Ok(new{maNguoiDung=id,dangHoatDong=body.DangHoatDong});
        });
    }
}
