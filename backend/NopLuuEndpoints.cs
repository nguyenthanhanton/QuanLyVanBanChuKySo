using Dapper;

public sealed record QuyetDinhNopLuu(bool TiepNhan, string? LyDo);
public sealed class HoSoNopInfo { public int MaNguoiTao {get;set;} public bool DaDong {get;set;} }
public sealed class LuotNopInfo { public Guid MaHoSo {get;set;} public string TrangThai {get;set;}=""; }

public static class NopLuuEndpoints
{
    private static async Task<(NguoiThucHien? Actor,int Admin,bool VanThu,IResult? Error)> Quyen(
        HttpContext http, PhanQuyen permission, Database db)
    {
        var (actor,error)=await permission.YeuCau(http,"dossiers.manage");
        if(error!=null)return(null,0,false,error);
        if(actor!.MaCoQuan==null)return(null,0,false,Results.Forbid());
        var caps=await permission.DanhSachQuyen(actor);
        using var cn=db.Open();
        var isClerk=await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
            WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency AND r.MaDinhDanh='VAN_THU'",
            new{user=actor.MaNguoiDung,agency=actor.MaCoQuan})>0;
        return(actor,caps.Contains("users.manage")?1:0,isClerk,null);
    }

    public static void MapNopLuu(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/dossiers").RequireAuthorization();
        group.MapGet("/submissions/permissions",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(_,_,clerk,error)=await Quyen(http,permission,db);
            return error??Results.Ok(new{LaVanThu=clerk});
        });
        group.MapGet("/submissions",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,admin,clerk,error)=await Quyen(http,permission,db);if(error!=null)return error;
            using var cn=db.Open();
            var rows=await cn.QueryAsync(@"
                SELECT l.MaLuot,l.MaHoSo,l.TrangThai,l.ThoiDiemNop,l.ThoiDiemXuLy,l.LyDoTraLai,l.MaBienNhan,
                       h.MaHoSoNoiBo,h.TenHoSo,h.NamHoSo,h.DaDong,h.MaPhongBan,p.TenPhongBan,
                       u.HoTen AS NguoiNop,x.HoTen AS NguoiXuLy
                FROM dbo.LuotNopLuuHoSo l
                JOIN dbo.HoSoLuuTru h ON h.MaHoSo=l.MaHoSo AND h.MaCoQuan=l.MaCoQuan
                JOIN dbo.NguoiDung u ON u.MaNguoiDung=l.MaNguoiNop
                LEFT JOIN dbo.NguoiDung x ON x.MaNguoiDung=l.MaNguoiXuLy
                LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=h.MaPhongBan AND p.MaCoQuan=h.MaCoQuan
                WHERE l.MaCoQuan=@agency
                  AND (l.MaNguoiNop=@user OR h.MaNguoiTao=@user OR @admin=1 OR @clerk=1)
                ORDER BY l.ThoiDiemNop DESC,l.MaLuot DESC",
                new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin,clerk=clerk?1:0});
            return Results.Ok(rows);
        });

        group.MapPost("/{id:guid}/submit",async(Guid id,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,admin,_,error)=await Quyen(http,permission,db);if(error!=null)return error;
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            // A row lock serializes submission against edits, closure and clerk decisions.
            var h=await cn.QuerySingleOrDefaultAsync<HoSoNopInfo>(@"
                SELECT MaNguoiTao,DaDong FROM dbo.HoSoLuuTru WITH(UPDLOCK,HOLDLOCK)
                WHERE MaHoSo=@id AND MaCoQuan=@agency",
                new{id,agency=actor!.MaCoQuan},tx);
            if(h==null)return Results.NotFound();
            if(h.MaNguoiTao!=actor.MaNguoiDung&&admin==0)return Results.Forbid();
            if(!h.DaDong)return Results.Conflict(new{message="Cần đóng hồ sơ trước khi nộp lưu."});
            var previous=await cn.ExecuteScalarAsync<string?>(@"
                SELECT TOP 1 TrangThai FROM dbo.LuotNopLuuHoSo WHERE MaHoSo=@id AND MaCoQuan=@agency
                ORDER BY MaLuot DESC",new{id,agency=actor.MaCoQuan},tx);
            if(previous is "CHO_TIEP_NHAN" or "DA_TIEP_NHAN")
                return Results.Conflict(new{message="Hồ sơ đang chờ hoặc đã được tiếp nhận."});
            // Ho so khong duoc nop rong; chi chap nhan van ban thuong da phat hanh.
            var total=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBan hv
                JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                WHERE hv.MaHoSo=@id AND hv.MaCoQuan=@agency AND v.MaCoQuan=@agency
                  AND v.DoMat='THUONG' AND v.TrangThai IN ('DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH')",
                new{id,agency=actor.MaCoQuan},tx);
            total+=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBanDen hd
                JOIN dbo.VanBanDen d ON d.MaVanBanDen=hd.MaVanBanDen
                WHERE hd.MaHoSo=@id AND hd.MaCoQuan=@agency AND d.MaCoQuanNhan=@agency
                  AND d.SoDen IS NOT NULL AND d.TrangThai='HOAN_THANH'",
                new{id,agency=actor.MaCoQuan},tx);
            if(total==0)return Results.BadRequest(new{message="Hồ sơ phải có ít nhất một văn bản đi đã phát hành hoặc văn bản đến hoàn thành."});
            var luot=await cn.ExecuteScalarAsync<long>(@"INSERT dbo.LuotNopLuuHoSo
                (MaHoSo,MaCoQuan,MaNguoiNop) OUTPUT INSERTED.MaLuot
                VALUES(@id,@agency,@user)",new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung},tx);
            await cn.ExecuteAsync(@"INSERT dbo.LichSuHoSo(MaHoSo,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa)
                VALUES(@id,@agency,@user,'NOP_LUU',@note)",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,note=$"Nộp lưu hồ sơ, lượt {luot}; {total} văn bản."},tx);
            tx.Commit();return Results.Ok(new{MaLuot=luot,TrangThai="CHO_TIEP_NHAN"});
        });

        group.MapGet("/submissions/{luot:long}/documents",async(long luot,HttpContext http,
            PhanQuyen permission,Database db)=>{
            var(actor,admin,clerk,error)=await Quyen(http,permission,db);if(error!=null)return error;
            using var cn=db.Open();
            var id=await cn.ExecuteScalarAsync<Guid?>(@"
                SELECT l.MaHoSo FROM dbo.LuotNopLuuHoSo l
                JOIN dbo.HoSoLuuTru h ON h.MaHoSo=l.MaHoSo AND h.MaCoQuan=l.MaCoQuan
                WHERE l.MaLuot=@luot AND l.MaCoQuan=@agency
                  AND (l.MaNguoiNop=@user OR h.MaNguoiTao=@user OR @admin=1 OR @clerk=1)",
                new{luot,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin,clerk=clerk?1:0});
            if(id==null)return Results.NotFound();
            var rows=await cn.QueryAsync(@"
                SELECT v.MaVanBan,v.MaNoiBo,v.TieuDe,v.TrangThai,v.SoPhatHanh,v.KyHieu
                FROM dbo.HoSoLuuTruVanBan hv JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                WHERE hv.MaHoSo=@id AND hv.MaCoQuan=@agency AND v.MaCoQuan=@agency
                  AND v.DoMat='THUONG' " + VanBanEndpoints.PhamViVanBan + @"
                ORDER BY v.ThoiDiemTao DESC",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung});
            var incoming=await cn.QueryAsync(@"
                SELECT d.MaVanBanDen AS MaVanBan,CONCAT(N'Số đến ',d.SoDen,N'/',d.Nam) AS MaNoiBo,
                    d.TieuDe,d.TrangThai
                FROM dbo.HoSoLuuTruVanBanDen hd JOIN dbo.VanBanDen d ON d.MaVanBanDen=hd.MaVanBanDen
                WHERE hd.MaHoSo=@id AND hd.MaCoQuan=@agency AND d.MaCoQuanNhan=@agency",
                new{id,agency=actor.MaCoQuan});
            return Results.Ok(rows.Concat(incoming));
        });

        group.MapPost("/submissions/{luot:long}/decision",async(long luot,QuyetDinhNopLuu body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,_,clerk,error)=await Quyen(http,permission,db);if(error!=null)return error;
            if(!clerk)return Results.Forbid();
            var reason=body.LyDo?.Trim();
            if(!body.TiepNhan&&(string.IsNullOrWhiteSpace(reason)||reason.Length>1000))
                return Results.BadRequest(new{message="Trả lại hồ sơ phải có lý do từ 1–1.000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var submission=await cn.QuerySingleOrDefaultAsync<LuotNopInfo>(@"
                SELECT MaHoSo,TrangThai FROM dbo.LuotNopLuuHoSo WITH(UPDLOCK,HOLDLOCK)
                WHERE MaLuot=@luot AND MaCoQuan=@agency",
                new{luot,agency=actor!.MaCoQuan},tx);
            if(submission==null)return Results.NotFound();
            if(submission.TrangThai!="CHO_TIEP_NHAN")return Results.Conflict(new{message="Lượt nộp lưu đã được xử lý."});
            var receipt=body.TiepNhan?(Guid?)Guid.NewGuid():null;
            var status=body.TiepNhan?"DA_TIEP_NHAN":"TRA_LAI";
            await cn.ExecuteAsync(@"UPDATE dbo.LuotNopLuuHoSo
                SET TrangThai=@status,MaNguoiXuLy=@user,ThoiDiemXuLy=SYSUTCDATETIME(),
                    LyDoTraLai=@reason,MaBienNhan=@receipt WHERE MaLuot=@luot AND MaCoQuan=@agency",
                new{status,user=actor.MaNguoiDung,reason=body.TiepNhan?null:reason,receipt,luot,agency=actor.MaCoQuan},tx);
            await cn.ExecuteAsync(@"INSERT dbo.LichSuHoSo(MaHoSo,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa)
                VALUES(@id,@agency,@user,@action,@note)",new{
                    id=submission.MaHoSo,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    action=body.TiepNhan?"TIEP_NHAN":"TRA_LAI",
                    note=body.TiepNhan?$"Đã tiếp nhận lưu trữ; biên nhận {receipt}":$"Trả lại lượt {luot}: {reason}"},tx);
            tx.Commit();return Results.Ok(new{MaLuot=luot,TrangThai=status,MaBienNhan=receipt});
        });

        group.MapGet("/submissions/{luot:long}/receipt",async(long luot,HttpContext http,
            PhanQuyen permission,Database db)=>{
            var(actor,admin,clerk,error)=await Quyen(http,permission,db);if(error!=null)return error;
            using var cn=db.Open();
            var result=await cn.QuerySingleOrDefaultAsync(@"
                SELECT l.MaLuot,l.MaBienNhan,l.ThoiDiemNop,l.ThoiDiemXuLy,
                       h.MaHoSoNoiBo,h.TenHoSo,h.NamHoSo,h.SoNamBaoQuan,
                       n.HoTen AS NguoiGiao,v.HoTen AS NguoiNhan,c.TenCoQuan,
                       (SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBan hv WHERE hv.MaHoSo=h.MaHoSo)
                       +(SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBanDen hd WHERE hd.MaHoSo=h.MaHoSo) AS SoVanBan
                FROM dbo.LuotNopLuuHoSo l JOIN dbo.HoSoLuuTru h ON h.MaHoSo=l.MaHoSo
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=l.MaNguoiNop
                JOIN dbo.NguoiDung v ON v.MaNguoiDung=l.MaNguoiXuLy
                JOIN dbo.CoQuan c ON c.MaCoQuan=l.MaCoQuan
                WHERE l.MaLuot=@luot AND l.MaCoQuan=@agency AND l.TrangThai='DA_TIEP_NHAN'
                  AND (l.MaNguoiNop=@user OR h.MaNguoiTao=@user OR @clerk=1 OR @admin=1)",
                new{luot,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin,clerk=clerk?1:0});
            return result==null?Results.NotFound():Results.Ok(result);
        });
    }
}
