using Dapper;
using Microsoft.Data.SqlClient;

public sealed record TaoHoSo(string MaHoSoNoiBo, string TenHoSo, string LoaiHoSo, int NamHoSo,
    int? MaPhongBan, int? SoNamBaoQuan, string? GhiChu);
public sealed record SuaHoSo(string TenHoSo, string LoaiHoSo, int NamHoSo,
    int? MaPhongBan, int? SoNamBaoQuan, string? GhiChu);
public sealed record LienKetVanBan(Guid MaVanBan);
public sealed record DongMoHoSo(bool DaDong);

public static class HoSoEndpoints
{
    // Quan tri hoac nguoi tao duoc thay doi ho so; nguoi cung phong duoc xem ho so.
    // Cac van ban trong ho so van phai vuot qua kiem tra quyen xem van ban rieng.
    private const string DuocXem = @"(h.MaNguoiTao=@user OR h.MaPhongBan=(SELECT MaPhongBan FROM dbo.NguoiDung
        WHERE MaNguoiDung=@user) OR @admin=1)";
    private const string DuocSua = "(h.MaNguoiTao=@user OR @admin=1)";

    private static async Task<(NguoiThucHien? actor, int admin, IResult? error)> Quyen(
        HttpContext http, PhanQuyen permission)
    {
        var (actor,error)=await permission.YeuCau(http,"dossiers.manage");
        if(error!=null)return(null,0,error);
        if(actor!.MaCoQuan is null)return(null,0,Results.Forbid());
        var caps=await permission.DanhSachQuyen(actor);
        return(actor,caps.Contains("users.manage")?1:0,null);
    }

    private static string? LoiDuLieu(string name,string category,int year,int? retention,string? notes)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>250)return "Tên hồ sơ phải có 1–250 ký tự.";
        if(string.IsNullOrWhiteSpace(category)||category.Trim().Length>100)return "Loại hồ sơ phải có 1–100 ký tự.";
        if(year<1900||year>2200)return "Năm hồ sơ không hợp lệ.";
        if(retention is <1 or >200)return "Số năm bảo quản phải từ 1 đến 200 hoặc để trống (vĩnh viễn).";
        if(notes?.Length>1000)return "Ghi chú tối đa 1.000 ký tự.";
        return null;
    }

    public static void MapHoSo(this IEndpointRouteBuilder app)
    {
        var routes=app.MapGroup("/api/v1/dossiers").RequireAuthorization();
        routes.MapGet("/departments",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,_,error)=await Quyen(http,permission);if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"SELECT MaPhongBan,TenPhongBan FROM dbo.PhongBan
                WHERE MaCoQuan=@agency AND DangHoatDong=1 ORDER BY TenPhongBan",new{agency=actor!.MaCoQuan}));
        });

        routes.MapGet("/",async(HttpContext http,PhanQuyen permission,Database db,
            string? timKiem,int? nam,int? maPhongBan,bool? daDong)=>{
            var(actor,admin,error)=await Quyen(http,permission);if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"SELECT h.MaHoSo,h.MaHoSoNoiBo,h.TenHoSo,h.LoaiHoSo,h.NamHoSo,
                h.MaPhongBan,p.TenPhongBan,h.SoNamBaoQuan,h.GhiChu,h.DaDong,h.ThoiDiemTao,
                h.ThoiDiemCapNhat,n.HoTen AS NguoiTao,
                (SELECT COUNT(*) FROM dbo.HoSoVanBan hv JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                 WHERE hv.MaHoSo=h.MaHoSo AND v.MaCoQuan=@agency AND v.DoMat='THUONG' " + VanBanEndpoints.PhamViVanBan + @") AS SoVanBan
                FROM dbo.HoSoLuuTru h LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=h.MaPhongBan
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=h.MaNguoiTao
                WHERE h.MaCoQuan=@agency AND " + DuocXem + @"
                  AND (@timKiem IS NULL OR h.TenHoSo LIKE '%'+@timKiem+'%' OR h.MaHoSoNoiBo LIKE '%'+@timKiem+'%')
                  AND (@nam IS NULL OR h.NamHoSo=@nam)
                  AND (@maPhongBan IS NULL OR h.MaPhongBan=@maPhongBan)
                  AND (@daDong IS NULL OR h.DaDong=@daDong)
                ORDER BY h.NamHoSo DESC,h.ThoiDiemTao DESC",
                new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin,timKiem,nam,maPhongBan,daDong}));
        });

        routes.MapPost("/",async(TaoHoSo body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,_,error)=await Quyen(http,permission);if(error!=null)return error;
            var validation=LoiDuLieu(body.TenHoSo,body.LoaiHoSo,body.NamHoSo,body.SoNamBaoQuan,body.GhiChu);
            if(validation!=null)return Results.BadRequest(new{message=validation});
            if(string.IsNullOrWhiteSpace(body.MaHoSoNoiBo)||body.MaHoSoNoiBo.Trim().Length>60)
                return Results.BadRequest(new{message="Mã hồ sơ phải có 1–60 ký tự."});
            using var cn=db.Open();
            await cn.OpenAsync();using var tx=cn.BeginTransaction();
            if(body.MaPhongBan!=null && await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.PhongBan
                WHERE MaPhongBan=@id AND MaCoQuan=@agency AND DangHoatDong=1",
                new{id=body.MaPhongBan,agency=actor!.MaCoQuan},tx)!=1)
                return Results.BadRequest(new{message="Phòng ban không thuộc cơ quan hoặc đã ngừng hoạt động."});
            try{
                var id=Guid.NewGuid();
                await cn.ExecuteAsync(@"INSERT dbo.HoSoLuuTru(MaHoSo,MaCoQuan,MaPhongBan,MaNguoiTao,
                    MaHoSoNoiBo,TenHoSo,LoaiHoSo,NamHoSo,SoNamBaoQuan,GhiChu)
                    VALUES(@id,@agency,@MaPhongBan,@user,@MaHoSoNoiBo,@TenHoSo,@LoaiHoSo,@NamHoSo,@SoNamBaoQuan,@GhiChu)",
                    new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,body.MaPhongBan,
                        MaHoSoNoiBo=body.MaHoSoNoiBo.Trim(),TenHoSo=body.TenHoSo.Trim(),
                        LoaiHoSo=body.LoaiHoSo.Trim(),body.NamHoSo,body.SoNamBaoQuan,body.GhiChu},tx);
                await Ghi(cn,tx,id,actor,"TAO",null,"Tạo hồ sơ lưu trữ.");
                tx.Commit();return Results.Ok(new{MaHoSo=id});
            }catch(SqlException ex) when(ex.Number is 2601 or 2627){
                return Results.Conflict(new{message="Mã hồ sơ đã tồn tại trong cơ quan."});
            }
        });

        routes.MapGet("/{id:guid}",async(Guid id,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,admin,error)=await Quyen(http,permission);if(error!=null)return error;
            using var cn=db.Open();
            var dossier=await cn.QuerySingleOrDefaultAsync(@"SELECT h.*,p.TenPhongBan,n.HoTen AS NguoiTao
                FROM dbo.HoSoLuuTru h LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=h.MaPhongBan
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=h.MaNguoiTao
                WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency AND "+DuocXem,
                new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin});
            if(dossier==null)return Results.NotFound();
            var docs=await cn.QueryAsync(@"SELECT v.MaVanBan,v.MaNoiBo,v.TieuDe,v.SoPhatHanh,v.KyHieu,
                v.NgayBanHanh,v.TrangThai,hv.ThoiDiemThem,n.HoTen AS NguoiThem
                FROM dbo.HoSoVanBan hv JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=hv.MaNguoiThem
                WHERE hv.MaHoSo=@id AND hv.MaCoQuan=@agency AND v.MaCoQuan=@agency
                  AND v.DoMat='THUONG' " + VanBanEndpoints.PhamViVanBan + @"
                ORDER BY hv.ThoiDiemThem DESC",new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,admin});
            var history=await cn.QueryAsync(@"SELECT l.MaLichSu,l.MaVanBan,l.HanhDong,l.MoTa,l.ThoiDiem,
                n.HoTen AS NguoiThucHien FROM dbo.LichSuHoSo l
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=l.MaNguoiThucHien
                WHERE l.MaHoSo=@id AND l.MaCoQuan=@agency ORDER BY l.MaLichSu DESC",
                new{id,agency=actor.MaCoQuan});
            // Lich su co the chua ma van ban mat; an tham chieu neu khong co quyen xem.
            var visibleIds=(await cn.QueryAsync<Guid>(@"SELECT v.MaVanBan FROM dbo.VanBan v
                WHERE v.MaCoQuan=@agency AND v.DoMat='THUONG' " + VanBanEndpoints.PhamViVanBan,
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung})).ToHashSet();
            return Results.Ok(new{HoSo=dossier,VanBan=docs,LichSu=history.Select(h=>new{
                h.MaLichSu,MaVanBan=h.MaVanBan!=null && visibleIds.Contains((Guid)h.MaVanBan)?h.MaVanBan:null,
                h.HanhDong,MoTa=h.MaVanBan==null||visibleIds.Contains((Guid)h.MaVanBan)?h.MoTa:"Thao tác trên văn bản được giới hạn quyền xem.",
                h.ThoiDiem,h.NguoiThucHien})});
        });

        routes.MapPut("/{id:guid}",async(Guid id,SuaHoSo body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,admin,error)=await Quyen(http,permission);if(error!=null)return error;
            var validation=LoiDuLieu(body.TenHoSo,body.LoaiHoSo,body.NamHoSo,body.SoNamBaoQuan,body.GhiChu);
            if(validation!=null)return Results.BadRequest(new{message=validation});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            if(body.MaPhongBan!=null&&await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.PhongBan
                WHERE MaPhongBan=@MaPhongBan AND MaCoQuan=@agency AND DangHoatDong=1",
                new{body.MaPhongBan,agency=actor!.MaCoQuan},tx)!=1)
                return Results.BadRequest(new{message="Phòng ban không hợp lệ."});
            var changed=await cn.ExecuteAsync(@"UPDATE h SET TenHoSo=@TenHoSo,LoaiHoSo=@LoaiHoSo,
                NamHoSo=@NamHoSo,MaPhongBan=@MaPhongBan,SoNamBaoQuan=@SoNamBaoQuan,
                GhiChu=@GhiChu,ThoiDiemCapNhat=SYSUTCDATETIME() FROM dbo.HoSoLuuTru h
                WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency AND h.DaDong=0 AND "+DuocSua,
                new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin,
                    TenHoSo=body.TenHoSo.Trim(),LoaiHoSo=body.LoaiHoSo.Trim(),body.NamHoSo,
                    body.MaPhongBan,body.SoNamBaoQuan,body.GhiChu},tx);
            if(changed!=1)return Results.Conflict(new{message="Hồ sơ đã đóng hoặc bạn không được phép sửa."});
            await Ghi(cn,tx,id,actor,"SUA",null,"Cập nhật thông tin và thời hạn bảo quản.");
            tx.Commit();return Results.Ok(new{message="Đã cập nhật hồ sơ."});
        });

        routes.MapPost("/{id:guid}/documents",async(Guid id,LienKetVanBan body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,admin,error)=await Quyen(http,permission);if(error!=null)return error;
            // Khong duoc dua van ban khong the xem vao ho so, ke ca qua API truc tiep.
            if(!await VanBanEndpoints.CoQuyenXem(db,actor!,body.MaVanBan))return Results.NotFound();
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var allowed=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTru h WITH (UPDLOCK,HOLDLOCK)
                WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency AND h.DaDong=0 AND "+DuocSua,
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,admin},tx);
            if(allowed!=1)return Results.Conflict(new{message="Hồ sơ đã đóng hoặc không thuộc quyền chỉnh sửa."});
            var published=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.VanBan
                WHERE MaVanBan=@MaVanBan AND MaCoQuan=@agency AND DoMat='THUONG'
                  AND TrangThai IN ('DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH')",
                new{body.MaVanBan,agency=actor.MaCoQuan},tx);
            if(published!=1)return Results.BadRequest(new{message="Chỉ được lưu văn bản thường đã phát hành trong cùng cơ quan."});
            var inserted=await cn.ExecuteAsync(@"INSERT dbo.HoSoVanBan(MaHoSo,MaCoQuan,MaVanBan,MaNguoiThem)
                SELECT @id,@agency,@MaVanBan,@user WHERE NOT EXISTS(
                    SELECT 1 FROM dbo.HoSoVanBan WITH(UPDLOCK,HOLDLOCK)
                    WHERE MaHoSo=@id AND MaVanBan=@MaVanBan)",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,body.MaVanBan},tx);
            if(inserted==0)return Results.Conflict(new{message="Văn bản đã có trong hồ sơ."});
            await Ghi(cn,tx,id,actor,"THEM_VAN_BAN",body.MaVanBan,"Đưa văn bản đã phát hành vào hồ sơ.");
            await cn.ExecuteAsync("UPDATE dbo.HoSoLuuTru SET ThoiDiemCapNhat=SYSUTCDATETIME() WHERE MaHoSo=@id",new{id},tx);
            tx.Commit();return Results.Ok(new{message="Đã liên kết văn bản."});
        });

        routes.MapDelete("/{id:guid}/documents/{documentId:guid}",async(Guid id,Guid documentId,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,admin,error)=await Quyen(http,permission);if(error!=null)return error;
            if(!await VanBanEndpoints.CoQuyenXem(db,actor!,documentId))return Results.NotFound();
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var allowed=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTru h WITH(UPDLOCK,HOLDLOCK)
                WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency AND h.DaDong=0 AND "+DuocSua,
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,admin},tx);
            if(allowed!=1)return Results.Conflict(new{message="Hồ sơ đã đóng hoặc không thuộc quyền chỉnh sửa."});
            var count=await cn.ExecuteAsync(@"DELETE FROM dbo.HoSoVanBan
                WHERE MaHoSo=@id AND MaCoQuan=@agency AND MaVanBan=@documentId",
                new{id,agency=actor.MaCoQuan,documentId},tx);
            if(count==0)return Results.NotFound();
            await Ghi(cn,tx,id,actor,"BO_VAN_BAN",documentId,"Gỡ liên kết văn bản; bản gốc vẫn được giữ nguyên.");
            await cn.ExecuteAsync("UPDATE dbo.HoSoLuuTru SET ThoiDiemCapNhat=SYSUTCDATETIME() WHERE MaHoSo=@id",new{id},tx);
            tx.Commit();return Results.Ok(new{message="Đã gỡ liên kết; không xóa văn bản gốc."});
        });

        routes.MapPost("/{id:guid}/close",async(Guid id,DongMoHoSo body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,admin,error)=await Quyen(http,permission);if(error!=null)return error;
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var count=await cn.ExecuteAsync(@"UPDATE h SET DaDong=@DaDong,ThoiDiemCapNhat=SYSUTCDATETIME()
                FROM dbo.HoSoLuuTru h WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency
                  AND h.DaDong<>@DaDong AND "+DuocSua,
                new{id,body.DaDong,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin},tx);
            if(count!=1)return Results.Conflict(new{message="Không có quyền hoặc hồ sơ đã ở trạng thái này."});
            await Ghi(cn,tx,id,actor,body.DaDong?"DONG":"MO_LAI",null,
                body.DaDong?"Đóng hồ sơ lưu trữ.":"Mở lại hồ sơ lưu trữ.");
            tx.Commit();return Results.Ok(new{message=body.DaDong?"Đã đóng hồ sơ.":"Đã mở lại hồ sơ."});
        });
    }

    private static Task<int> Ghi(SqlConnection cn,SqlTransaction tx,Guid id,NguoiThucHien actor,
        string action,Guid? document,string message)=>cn.ExecuteAsync(@"INSERT dbo.LichSuHoSo
        (MaHoSo,MaCoQuan,MaVanBan,MaNguoiThucHien,HanhDong,MoTa)
        VALUES(@id,@agency,@document,@user,@action,@message)",new{id,agency=actor.MaCoQuan,
            document,user=actor.MaNguoiDung,action,message},tx);
}
