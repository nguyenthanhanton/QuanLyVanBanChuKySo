using Dapper;
using System.Security.Cryptography;

public sealed record GiaoXuLyVanBanDen(int MaPhongChuTri,int[] MaPhongPhoiHop,string? ChiDao,DateTime? HanXuLy);
public sealed record BaoCaoVanBanDen(string NoiDung);
public sealed record KetThucVanBanDen(string KetQuaTongHop);
public sealed record GiaoCaNhanVanBanDen(int MaPhongBan,int MaNguoiChuTri,int[] MaNguoiPhoiHop,string? GhiChu,string? LyDoGiaoLai);
public sealed class TepMinhChungRow
{
    public string DuongDanLuuTru {get;set;}="";
    public string LoaiNoiDung {get;set;}="";
    public string TenTep {get;set;}="";
    public string MaBamSha256 {get;set;}="";
}
public sealed class VanBanDenSoRow {public int Nam {get;set;} public int? SoDen {get;set;}}

public static class DieuHanhVanBanDenEndpoints
{
    private static async Task<bool> CoVaiTro(System.Data.IDbConnection cn,int user,int agency,params string[] roles)
        => await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.NguoiDungVaiTro ur JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro
            WHERE ur.MaNguoiDung=@user AND ur.MaCoQuan=@agency AND r.MaDinhDanh IN @roles",
            new{user,agency,roles})>0;

    public static void MapDieuHanhVanBanDen(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/incoming-work").RequireAuthorization();
        group.MapGet("",async(HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            var leader=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"LANH_DAO","PHO_LANH_DAO");
            var clerk=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"VAN_THU");
            var rows=await cn.QueryAsync(@"
                SELECT d.MaVanBanDen,d.TieuDe,d.SoDen,d.TrangThai,p.MaPhongChuTri,
                       ct.TenPhongBan AS TenPhongChuTri,p.HanXuLy,p.ChiDao,p.KetQuaTongHop,
                       p.TrangThai AS TrangThaiXuLy,p.ThoiDiemGiao,
                       (SELECT STRING_AGG(ph.TenPhongBan,N', ') FROM dbo.PhoiHopVanBanDen h
                        JOIN dbo.PhongBan ph ON ph.MaPhongBan=h.MaPhongBan
                        WHERE h.MaVanBanDen=d.MaVanBanDen) AS TenPhongPhoiHop,
                       (SELECT COUNT(*) FROM dbo.KetQuaPhongVanBanDen k
                        WHERE k.MaVanBanDen=d.MaVanBanDen) AS SoBaoCao,
                       (SELECT COUNT(*) FROM dbo.PhoiHopVanBanDen h
                        WHERE h.MaVanBanDen=d.MaVanBanDen)+CASE WHEN p.MaVanBanDen IS NULL THEN 0 ELSE 1 END AS SoPhong
                FROM dbo.VanBanDen d
                LEFT JOIN dbo.PhanCongVanBanDen p ON p.MaVanBanDen=d.MaVanBanDen
                LEFT JOIN dbo.PhongBan ct ON ct.MaPhongBan=p.MaPhongChuTri
                LEFT JOIN dbo.NguoiDung n ON n.MaNguoiDung=@user
                WHERE d.MaCoQuanNhan=@agency
                  AND (@leader=1 OR @clerk=1 OR (p.MaVanBanDen IS NOT NULL AND
                     (p.MaPhongChuTri=n.MaPhongBan OR EXISTS(
                         SELECT 1 FROM dbo.PhoiHopVanBanDen h
                         WHERE h.MaVanBanDen=d.MaVanBanDen AND h.MaPhongBan=n.MaPhongBan))))
                ORDER BY d.ThoiDiemTiepNhan DESC",
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung,leader,clerk});
            var departments=await cn.QueryAsync(@"
                SELECT MaPhongBan,TenPhongBan FROM dbo.PhongBan
                WHERE MaCoQuan=@agency AND DangHoatDong=1 AND MaDinhDanh<>'LANH_DAO'
                ORDER BY ThuTuHienThi,TenPhongBan",new{agency=actor.MaCoQuan});
            return Results.Ok(new{rows,departments,leader,clerk});
        });

        group.MapPost("/{id:guid}/submit",async(Guid id,HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();await cn.OpenAsync();
            if(!await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"VAN_THU"))return Results.Forbid();
            var changed=await cn.ExecuteAsync(@"
                UPDATE dbo.VanBanDen SET TrangThai='CHO_LANH_DAO'
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency AND TrangThai='CHO_DANG_KY'
                  AND SoDen IS NOT NULL",
                new{id,agency=actor.MaCoQuan});
            return changed==1?Results.Ok(new{message="Đã trình lãnh đạo"}):Results.Conflict(new{message="Phải cấp số đến trước khi trình lãnh đạo (hoặc văn bản đã được trình)."});
        });

        group.MapPost("/{id:guid}/register",async(Guid id,HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();await cn.OpenAsync();
            if(!await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"VAN_THU"))return Results.Forbid();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var row=await cn.QuerySingleOrDefaultAsync<VanBanDenSoRow>(@"
                SELECT Nam,SoDen FROM dbo.VanBanDen WITH(UPDLOCK,HOLDLOCK)
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency",
                new{id,agency=actor.MaCoQuan},tx);
            if(row is null){tx.Rollback();return Results.NotFound();}
            if(row.SoDen is not null){tx.Commit();return Results.Ok(new{soDen=row.SoDen,nam=row.Nam,message="Văn bản đã có số đến."});}
            await cn.ExecuteAsync(@"
                IF NOT EXISTS(SELECT 1 FROM dbo.SoVanBan WITH(UPDLOCK,HOLDLOCK)
                    WHERE MaCoQuan=@agency AND Nam=@year AND LoaiSo='DEN')
                    INSERT dbo.SoVanBan(MaCoQuan,Nam,LoaiSo,TenSo,SoDaCap)
                    VALUES(@agency,@year,'DEN',N'Sổ văn bản đến',0)",
                new{agency=actor.MaCoQuan,year=row.Nam},tx);
            await cn.ExecuteAsync(@"
                UPDATE s SET SoDaCap=CASE WHEN SoDaCap<m.SoLonNhat THEN m.SoLonNhat ELSE SoDaCap END
                FROM dbo.SoVanBan s CROSS APPLY (
                    SELECT ISNULL(MAX(SoDen),0) AS SoLonNhat FROM dbo.VanBanDen
                    WHERE MaCoQuanNhan=@agency AND Nam=@year) m
                WHERE s.MaCoQuan=@agency AND s.Nam=@year AND s.LoaiSo='DEN'",
                new{agency=actor.MaCoQuan,year=row.Nam},tx);
            var number=await cn.ExecuteScalarAsync<int>(@"
                UPDATE dbo.SoVanBan WITH(UPDLOCK) SET SoDaCap=SoDaCap+1
                OUTPUT INSERTED.SoDaCap
                WHERE MaCoQuan=@agency AND Nam=@year AND LoaiSo='DEN'",
                new{agency=actor.MaCoQuan,year=row.Nam},tx);
            await cn.ExecuteAsync(@"
                UPDATE dbo.VanBanDen SET SoDen=@number
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency AND SoDen IS NULL",
                new{id,agency=actor.MaCoQuan,number},tx);
            tx.Commit();return Results.Ok(new{soDen=number,nam=row.Nam,message=$"Đã đăng ký số đến {number}/{row.Nam}."});
        });

        group.MapPost("/{id:guid}/assign",async(Guid id,GiaoXuLyVanBanDen body,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            if(body.MaPhongChuTri<=0 || body.MaPhongPhoiHop is null || body.MaPhongPhoiHop.Length>30 ||
               body.MaPhongPhoiHop.Distinct().Count()!=body.MaPhongPhoiHop.Length ||
               body.MaPhongPhoiHop.Contains(body.MaPhongChuTri) ||
               body.ChiDao?.Length>2000 || (body.HanXuLy is not null && body.HanXuLy<=DateTime.UtcNow))
                return Results.BadRequest(new{message="Phòng chủ trì, danh sách phối hợp hoặc hạn xử lý không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            if(!await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"LANH_DAO","PHO_LANH_DAO"))return Results.Forbid();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var status=await cn.QuerySingleOrDefaultAsync<string>(@"
                SELECT TrangThai FROM dbo.VanBanDen WITH(UPDLOCK,HOLDLOCK)
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency",
                new{id,agency=actor.MaCoQuan},tx);
            if(status!="CHO_LANH_DAO"){
                tx.Rollback();return Results.Conflict(new{message="Văn bản chưa được văn thư trình hoặc đã được giao."});
            }
            var ids=body.MaPhongPhoiHop.Append(body.MaPhongChuTri).ToArray();
            var valid=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.PhongBan
                WHERE MaPhongBan IN @ids AND MaCoQuan=@agency AND DangHoatDong=1 AND MaDinhDanh<>'LANH_DAO'",
                new{ids,agency=actor.MaCoQuan},tx);
            if(valid!=ids.Length){tx.Rollback();return Results.BadRequest(new{message="Có phòng ban không hoạt động hoặc thuộc cơ quan khác."});}
            await cn.ExecuteAsync(@"
                INSERT dbo.PhanCongVanBanDen(MaVanBanDen,MaCoQuan,MaPhongChuTri,MaNguoiGiao,HanXuLy,ChiDao)
                VALUES(@id,@agency,@lead,@user,@deadline,@note)",
                new{id,agency=actor.MaCoQuan,lead=body.MaPhongChuTri,user=actor.MaNguoiDung,
                    deadline=body.HanXuLy,note=body.ChiDao?.Trim()},tx);
            foreach(var department in body.MaPhongPhoiHop)
                await cn.ExecuteAsync(@"
                    INSERT dbo.PhoiHopVanBanDen(MaVanBanDen,MaPhongBan) VALUES(@id,@department)",
                    new{id,department},tx);
            await cn.ExecuteAsync(@"
                UPDATE dbo.VanBanDen SET TrangThai='DANG_XU_LY'
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency",
                new{id,agency=actor.MaCoQuan},tx);
            tx.Commit();return Results.Ok(new{message="Đã giao phòng chủ trì và các phòng phối hợp."});
        });

        group.MapGet("/{id:guid}/people",async(Guid id,HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            var leader=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"LANH_DAO","PHO_LANH_DAO");
            var clerk=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"VAN_THU");
            var manager=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"TRUONG_PHONG","PHO_PHONG");
            var allowed=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.PhanCongVanBanDen p
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=@user
                WHERE p.MaVanBanDen=@id AND p.MaCoQuan=@agency AND
                    (@leader=1 OR @clerk=1 OR p.MaPhongChuTri=n.MaPhongBan OR
                        EXISTS(SELECT 1 FROM dbo.PhoiHopVanBanDen h WHERE h.MaVanBanDen=@id AND h.MaPhongBan=n.MaPhongBan))",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,leader,clerk});
            if(allowed==0)return Results.NotFound();
            var rooms=await cn.QueryAsync(@"
                SELECT p.MaPhongChuTri AS MaPhongBan,ph.TenPhongBan,CAST(1 AS BIT) AS LaPhongChuTri
                FROM dbo.PhanCongVanBanDen p JOIN dbo.PhongBan ph ON ph.MaPhongBan=p.MaPhongChuTri
                WHERE p.MaVanBanDen=@id AND p.MaCoQuan=@agency
                UNION ALL
                SELECT h.MaPhongBan,ph.TenPhongBan,CAST(0 AS BIT)
                FROM dbo.PhoiHopVanBanDen h JOIN dbo.PhongBan ph ON ph.MaPhongBan=h.MaPhongBan
                JOIN dbo.PhanCongVanBanDen p ON p.MaVanBanDen=h.MaVanBanDen
                WHERE p.MaVanBanDen=@id AND p.MaCoQuan=@agency",
                new{id,agency=actor.MaCoQuan});
            var employees=await cn.QueryAsync(@"
                SELECT n.MaNguoiDung,n.MaPhongBan,n.HoTen,n.ChucVu AS TenChucVu
                FROM dbo.NguoiDung n
                WHERE n.MaCoQuan=@agency AND n.DangHoatDong=1
                  AND (EXISTS(SELECT 1 FROM dbo.PhanCongVanBanDen p
                        WHERE p.MaVanBanDen=@id AND p.MaCoQuan=@agency AND p.MaPhongChuTri=n.MaPhongBan)
                       OR EXISTS(SELECT 1 FROM dbo.PhoiHopVanBanDen h
                         JOIN dbo.PhanCongVanBanDen p ON p.MaVanBanDen=h.MaVanBanDen
                         WHERE h.MaVanBanDen=@id AND p.MaCoQuan=@agency AND h.MaPhongBan=n.MaPhongBan))
                  AND (@leader=1 OR (@manager=1 AND n.MaPhongBan=(SELECT MaPhongBan FROM dbo.NguoiDung WHERE MaNguoiDung=@user)))
                ORDER BY n.HoTen",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,leader,manager});
            var assigned=await cn.QueryAsync(@"
                SELECT a.MaPhongBan,a.MaNguoiChuTri,n.HoTen AS TenNguoiChuTri,n.ChucVu AS ChucVuChuTri,a.GhiChu,
                  (SELECT STRING_AGG(CONCAT(helper.HoTen,CASE WHEN NULLIF(helper.ChucVu,N'') IS NULL
                       THEN N'' ELSE CONCAT(N' (',helper.ChucVu,N')') END),N', ') FROM dbo.NguoiPhoiHopVanBanDen x
                   JOIN dbo.NguoiDung helper ON helper.MaNguoiDung=x.MaNguoiDung
                   WHERE x.MaVanBanDen=a.MaVanBanDen AND x.MaPhongBan=a.MaPhongBan) AS TenNguoiPhoiHop
                FROM dbo.PhanCongCaNhanVanBanDen a JOIN dbo.NguoiDung n ON n.MaNguoiDung=a.MaNguoiChuTri
                WHERE a.MaVanBanDen=@id",new{id});
            var actorDepartment=await cn.ExecuteScalarAsync<int?>(@"SELECT MaPhongBan FROM dbo.NguoiDung WHERE MaNguoiDung=@user",
                new{user=actor.MaNguoiDung});
            var changes=await cn.QueryAsync(@"
                SELECT l.MaLichSu,l.MaPhongBan,oldUser.HoTen AS NguoiCu,newUser.HoTen AS NguoiMoi,
                    editor.HoTen AS NguoiDieuChinh,l.LyDo,l.ThoiDiem
                FROM dbo.LichSuGiaoLaiCaNhanVanBanDen l
                JOIN dbo.NguoiDung oldUser ON oldUser.MaNguoiDung=l.MaNguoiChuTriCu
                JOIN dbo.NguoiDung newUser ON newUser.MaNguoiDung=l.MaNguoiChuTriMoi
                JOIN dbo.NguoiDung editor ON editor.MaNguoiDung=l.MaNguoiThayDoi
                WHERE l.MaVanBanDen=@id ORDER BY l.ThoiDiem DESC",new{id});
            var canReport=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.PhanCongCaNhanVanBanDen a
                JOIN dbo.PhanCongVanBanDen p ON p.MaVanBanDen=a.MaVanBanDen
                WHERE a.MaVanBanDen=@id AND a.MaNguoiChuTri=@user AND p.MaCoQuan=@agency
                  AND p.TrangThai='DANG_XU_LY'",
                new{id,user=actor.MaNguoiDung,agency=actor.MaCoQuan})>0;
            return Results.Ok(new{rooms,employees,assigned,changes,actorDepartment,canAssign=manager,canReport});
        });

        group.MapPost("/{id:guid}/people",async(Guid id,GiaoCaNhanVanBanDen body,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            if(body.MaPhongBan<=0 || body.MaNguoiChuTri<=0 || body.MaNguoiPhoiHop is null ||
               body.MaNguoiPhoiHop.Length>50 || body.MaNguoiPhoiHop.Contains(body.MaNguoiChuTri) ||
               body.MaNguoiPhoiHop.Distinct().Count()!=body.MaNguoiPhoiHop.Length || body.GhiChu?.Length>2000 ||
               body.LyDoGiaoLai?.Length>1000)
                return Results.BadRequest(new{message="Cá nhân chủ trì hoặc phối hợp không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            if(!await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"TRUONG_PHONG","PHO_PHONG"))return Results.Forbid();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var eligible=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.PhanCongVanBanDen p WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.NguoiDung manager ON manager.MaNguoiDung=@user
                WHERE p.MaVanBanDen=@id AND p.MaCoQuan=@agency AND p.TrangThai='DANG_XU_LY'
                  AND manager.MaCoQuan=@agency AND manager.MaPhongBan=@room
                  AND (p.MaPhongChuTri=@room OR EXISTS(SELECT 1 FROM dbo.PhoiHopVanBanDen h
                      WHERE h.MaVanBanDen=@id AND h.MaPhongBan=@room))",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,room=body.MaPhongBan},tx);
            if(eligible==0){tx.Rollback();return Results.Forbid();}
            var hasReport=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.KetQuaPhongVanBanDen
                WHERE MaVanBanDen=@id AND MaPhongBan=@room",
                new{id,room=body.MaPhongBan},tx);
            if(hasReport>0){tx.Rollback();return Results.Conflict(new{message="Phòng đã báo cáo; không thể đổi người chịu trách nhiệm."});}
            var previous=await cn.ExecuteScalarAsync<int?>(@"
                SELECT MaNguoiChuTri FROM dbo.PhanCongCaNhanVanBanDen WITH(UPDLOCK,HOLDLOCK)
                WHERE MaVanBanDen=@id AND MaPhongBan=@room",
                new{id,room=body.MaPhongBan},tx);
            if(previous is not null && string.IsNullOrWhiteSpace(body.LyDoGiaoLai)){
                tx.Rollback();return Results.Conflict(new{message="Muốn giao lại phải ghi rõ lý do điều chỉnh phân công."});
            }
            var ids=body.MaNguoiPhoiHop.Append(body.MaNguoiChuTri).ToArray();
            var valid=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.NguoiDung
                WHERE MaNguoiDung IN @ids AND MaCoQuan=@agency AND MaPhongBan=@room AND DangHoatDong=1",
                new{ids,agency=actor.MaCoQuan,room=body.MaPhongBan},tx);
            if(valid!=ids.Length){tx.Rollback();return Results.BadRequest(new{message="Người được giao phải đang hoạt động và thuộc đúng phòng."});}
            await cn.ExecuteAsync(@"
                DELETE FROM dbo.NguoiPhoiHopVanBanDen WHERE MaVanBanDen=@id AND MaPhongBan=@room;
                UPDATE dbo.PhanCongCaNhanVanBanDen
                  SET MaNguoiChuTri=@principal,MaNguoiGiao=@user,GhiChu=@note,ThoiDiemGiao=SYSUTCDATETIME()
                  WHERE MaVanBanDen=@id AND MaPhongBan=@room;
                IF @@ROWCOUNT=0 INSERT dbo.PhanCongCaNhanVanBanDen
                    (MaVanBanDen,MaPhongBan,MaNguoiChuTri,MaNguoiGiao,GhiChu)
                    VALUES(@id,@room,@principal,@user,@note)",
                new{id,room=body.MaPhongBan,principal=body.MaNguoiChuTri,
                    user=actor.MaNguoiDung,note=body.GhiChu?.Trim()},tx);
            foreach(var helper in body.MaNguoiPhoiHop)
                await cn.ExecuteAsync(@"
                    INSERT dbo.NguoiPhoiHopVanBanDen(MaVanBanDen,MaPhongBan,MaNguoiDung)
                    VALUES(@id,@room,@helper)",new{id,room=body.MaPhongBan,helper},tx);
            if(previous is not null)
                await cn.ExecuteAsync(@"
                    INSERT dbo.LichSuGiaoLaiCaNhanVanBanDen
                        (MaVanBanDen,MaPhongBan,MaNguoiChuTriCu,MaNguoiChuTriMoi,MaNguoiThayDoi,LyDo)
                    VALUES(@id,@room,@previous,@principal,@user,@reason)",
                    new{id,room=body.MaPhongBan,previous,principal=body.MaNguoiChuTri,
                        user=actor.MaNguoiDung,reason=body.LyDoGiaoLai!.Trim()},tx);
            tx.Commit();return Results.Ok(new{message="Đã giao cá nhân chịu trách nhiệm thực hiện."});
        });

        group.MapPost("/{id:guid}/report",async(Guid id,BaoCaoVanBanDen body,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            if(string.IsNullOrWhiteSpace(body.NoiDung)||body.NoiDung.Length>4000)
                return Results.BadRequest(new{message="Nội dung báo cáo phải có từ 1 đến 4000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var department=await cn.QuerySingleOrDefaultAsync<int?>(@"
                SELECT a.MaPhongBan FROM dbo.PhanCongCaNhanVanBanDen a
                JOIN dbo.PhanCongVanBanDen p WITH(UPDLOCK,HOLDLOCK) ON p.MaVanBanDen=a.MaVanBanDen
                WHERE a.MaVanBanDen=@id AND a.MaNguoiChuTri=@user AND p.MaCoQuan=@agency
                  AND p.TrangThai='DANG_XU_LY'",
                new{id,user=actor.MaNguoiDung,agency=actor.MaCoQuan},tx);
            if(department is null){tx.Rollback();return Results.Forbid();}
            await cn.ExecuteAsync(@"
                UPDATE dbo.KetQuaPhongVanBanDen SET NoiDung=@content,MaNguoiBaoCao=@user,
                    ThoiDiemBaoCao=SYSUTCDATETIME()
                WHERE MaVanBanDen=@id AND MaPhongBan=@department;
                IF @@ROWCOUNT=0 INSERT dbo.KetQuaPhongVanBanDen
                    (MaVanBanDen,MaPhongBan,MaNguoiBaoCao,NoiDung)
                    VALUES(@id,@department,@user,@content)",
                new{id,department,user=actor.MaNguoiDung,content=body.NoiDung.Trim()},tx);
            tx.Commit();return Results.Ok(new{message="Đã lưu kết quả của phòng."});
        });

        group.MapGet("/{id:guid}/reports",async(Guid id,HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            var leader=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"LANH_DAO","PHO_LANH_DAO");
            var clerk=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"VAN_THU");
            var access=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.PhanCongVanBanDen p
                LEFT JOIN dbo.NguoiDung n ON n.MaNguoiDung=@user
                WHERE p.MaVanBanDen=@id AND p.MaCoQuan=@agency AND
                  (@leader=1 OR @clerk=1 OR p.MaPhongChuTri=n.MaPhongBan OR EXISTS(
                    SELECT 1 FROM dbo.PhoiHopVanBanDen h
                    WHERE h.MaVanBanDen=p.MaVanBanDen AND h.MaPhongBan=n.MaPhongBan))",
                new{id,user=actor.MaNguoiDung,agency=actor.MaCoQuan,leader,clerk});
            if(access==0)return Results.NotFound();
            var reports=await cn.QueryAsync(@"
                SELECT k.MaPhongBan,ph.TenPhongBan,n.HoTen AS NguoiBaoCao,k.NoiDung,k.ThoiDiemBaoCao
                FROM dbo.KetQuaPhongVanBanDen k JOIN dbo.PhongBan ph ON ph.MaPhongBan=k.MaPhongBan
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=k.MaNguoiBaoCao
                WHERE k.MaVanBanDen=@id ORDER BY ph.ThuTuHienThi",new{id});
            return Results.Ok(reports);
        });

        group.MapGet("/{id:guid}/evidence",async(Guid id,HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            if(!await CoQuyenXemXuLy(cn,actor,id))return Results.NotFound();
            var files=await cn.QueryAsync(@"
                SELECT m.MaMinhChung,m.MaPhongBan,m.TenTep,m.LoaiNoiDung,m.KichThuoc,
                    m.ThoiDiemTaiLen,n.HoTen AS NguoiTaiLen
                FROM dbo.MinhChungBaoCaoVanBanDen m
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=m.MaNguoiTaiLen
                WHERE m.MaVanBanDen=@id ORDER BY m.ThoiDiemTaiLen",new{id});
            return Results.Ok(files);
        });

        // Xem ban PDF phat hanh bat bien cua van ban den, ke ca truoc khi lanh dao giao phong.
        group.MapGet("/{id:guid}/pdf",async(Guid id,HttpContext http,PhanQuyen permissions,
            Database db,DichVuKyPdf signer)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            if(!await CoQuyenXemXuLy(cn,actor,id))return Results.NotFound();
            var pdf=await cn.QueryFirstOrDefaultAsync<TepMinhChungRow>(@"
                SELECT s.DuongDanLuuTru,s.MaBamSha256
                FROM dbo.VanBanDen d
                JOIN dbo.LuotChuyenVanBan l ON l.MaLuotChuyen=d.MaLuotChuyen
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                JOIN dbo.BanPdfDaKy s ON s.MaVanBan=p.MaVanBan AND s.SoPhienBan=p.SoPhienBan
                    AND s.ThuTuKy=v.SoBuocKy AND s.MaBamSha256=p.MaBamBanCuoi
                WHERE d.MaVanBanDen=@id AND d.MaCoQuanNhan=@agency
                  AND l.MaCoQuanNhan=@agency AND l.TrangThai='DA_TIEP_NHAN'",
                new{id,agency=actor.MaCoQuan});
            if(pdf is null)return Results.NotFound(new{message="Văn bản đến chưa có PDF đã phát hành để xem."});
            if(!File.Exists(pdf.DuongDanLuuTru) ||
               await DichVuKyPdf.Bam(pdf.DuongDanLuuTru)!=pdf.MaBamSha256)
                return Results.Conflict(new{message="PDF văn bản đến không còn nguyên vẹn."});
            try{
                var signatures=signer.XacMinh(pdf.DuongDanLuuTru);
                if(signatures.Count==0 || signatures.Any(s=>!s.ToanVenMatMa) ||
                    !signatures[^1].BaoPhuToanBoBanHienTai)
                    return Results.Conflict(new{message="PDF không vượt qua kiểm tra toàn vẹn chữ ký."});
            }catch(Exception){return Results.Conflict(new{message="Không thể kiểm tra chữ ký PDF."});}
            return Results.File(pdf.DuongDanLuuTru,"application/pdf");
        });

        group.MapGet("/{id:guid}/attachments",async(Guid id,HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            if(!await CoQuyenXemXuLy(cn,actor,id))return Results.NotFound();
            var files=await cn.QueryAsync(@"
                SELECT f.MaTep,f.TenTep,f.LoaiNoiDung,f.KichThuoc
                FROM dbo.VanBanDen d
                JOIN dbo.LuotChuyenVanBan l ON l.MaLuotChuyen=d.MaLuotChuyen
                JOIN dbo.BanPhatHanhTep p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.TepDinhKem f ON f.MaTep=p.MaTep
                WHERE d.MaVanBanDen=@id AND d.MaCoQuanNhan=@agency
                  AND l.MaCoQuanNhan=@agency AND l.TrangThai='DA_TIEP_NHAN'
                ORDER BY f.TenTep",new{id,agency=actor.MaCoQuan});
            return Results.Ok(files);
        });

        group.MapGet("/{id:guid}/attachments/{fileId:guid}",async(Guid id,Guid fileId,
            HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            if(!await CoQuyenXemXuLy(cn,actor,id))return Results.NotFound();
            var file=await cn.QueryFirstOrDefaultAsync<TepMinhChungRow>(@"
                SELECT f.DuongDanLuuTru,f.TenTep,f.LoaiNoiDung,p.MaBamSha256
                FROM dbo.VanBanDen d
                JOIN dbo.LuotChuyenVanBan l ON l.MaLuotChuyen=d.MaLuotChuyen
                JOIN dbo.BanPhatHanhTep p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.TepDinhKem f ON f.MaTep=p.MaTep
                WHERE d.MaVanBanDen=@id AND d.MaCoQuanNhan=@agency
                  AND l.MaCoQuanNhan=@agency AND l.TrangThai='DA_TIEP_NHAN' AND p.MaTep=@fileId",
                new{id,agency=actor.MaCoQuan,fileId});
            if(file is null)return Results.NotFound();
            if(!File.Exists(file.DuongDanLuuTru) ||
               await DichVuKyPdf.Bam(file.DuongDanLuuTru)!=file.MaBamSha256)
                return Results.Conflict(new{message="Tệp đính kèm phát hành không còn nguyên vẹn."});
            http.Response.Headers["X-Content-Type-Options"]="nosniff";
            if(http.Request.Query.ContainsKey("preview")){
                try{var preview=DichVuTepVanBan.XemTruocOffice(file.DuongDanLuuTru);
                    return preview is null?Results.StatusCode(415):Results.Text(preview,"text/plain; charset=utf-8");}
                catch(InvalidDataException){return Results.BadRequest(new{message="Tệp Office không đọc được."});}
            }
            return Results.File(file.DuongDanLuuTru,file.LoaiNoiDung,file.TenTep);
        });

        group.MapPost("/{id:guid}/evidence",async(Guid id,HttpContext http,PhanQuyen permissions,
            Database db,IConfiguration config)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            if(!http.Request.HasFormContentType)return Results.BadRequest(new{message="Cần gửi multipart/form-data."});
            var form=await http.Request.ReadFormAsync();
            var file=form.Files.GetFile("tepMinhChung");
            if(file is null || file.Length<1 || file.Length>5*1024*1024)
                return Results.BadRequest(new{message="Chọn một ảnh PNG/JPG/WEBP tối đa 5 MB."});
            var root=config["Storage:RootPath"];
            if(string.IsNullOrWhiteSpace(root))return Results.Problem("Chưa cấu hình Storage__RootPath.",statusCode:500);
            // Xac dinh dinh dang theo magic bytes, khong tin ContentType hay duoi ten tep.
            byte[] bytes;
            using(var stream=new MemoryStream()){
                await file.CopyToAsync(stream);
                bytes=stream.ToArray();
            }
            var png=bytes.Length>=8 && bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
            var jpeg=bytes.Length>=4 && bytes[0]==255 && bytes[1]==216 && bytes[^2]==255 && bytes[^1]==217;
            var webp=bytes.Length>=12 && System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF" &&
                System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WEBP";
            if(!png&&!jpeg&&!webp)return Results.BadRequest(new{message="Tệp phải là ảnh PNG, JPEG hoặc WEBP hợp lệ."});
            var extension=png?".png":jpeg?".jpg":".webp";
            var mime=png?"image/png":jpeg?"image/jpeg":"image/webp";
            using var cn=db.Open();await cn.OpenAsync();
            var room=await cn.ExecuteScalarAsync<int?>(@"
                SELECT a.MaPhongBan FROM dbo.PhanCongCaNhanVanBanDen a
                JOIN dbo.PhanCongVanBanDen p ON p.MaVanBanDen=a.MaVanBanDen
                JOIN dbo.KetQuaPhongVanBanDen k ON k.MaVanBanDen=a.MaVanBanDen AND k.MaPhongBan=a.MaPhongBan
                WHERE a.MaVanBanDen=@id AND a.MaNguoiChuTri=@user AND p.MaCoQuan=@agency
                  AND p.TrangThai='DANG_XU_LY'",
                new{id,user=actor.MaNguoiDung,agency=actor.MaCoQuan});
            if(room is null)return Results.Forbid();
            var count=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.MinhChungBaoCaoVanBanDen
                WHERE MaVanBanDen=@id AND MaPhongBan=@room",new{id,room});
            if(count>=10)return Results.Conflict(new{message="Mỗi phòng chỉ được tải tối đa 10 ảnh minh chứng."});
            var fileId=Guid.NewGuid();
            var directory=Path.Combine(root,actor.MaCoQuan.Value.ToString(),"incoming-evidence",id.ToString("N"));
            Directory.CreateDirectory(directory);
            var path=Path.Combine(directory,fileId.ToString("N")+extension);
            await File.WriteAllBytesAsync(path,bytes);
            var hash=Convert.ToHexString(SHA256.HashData(bytes));
            try{
                await cn.ExecuteAsync(@"
                    INSERT dbo.MinhChungBaoCaoVanBanDen
                      (MaMinhChung,MaVanBanDen,MaPhongBan,MaNguoiTaiLen,TenTep,LoaiNoiDung,KichThuoc,DuongDanLuuTru,MaBamSha256)
                    VALUES(@fileId,@id,@room,@user,@name,@mime,@size,@path,@hash)",
                    new{fileId,id,room,user=actor.MaNguoiDung,
                        name=Path.GetFileName(file.FileName).Length>255?Path.GetFileName(file.FileName)[..255]:Path.GetFileName(file.FileName),
                        mime,size=bytes.Length,path,hash});
            }catch{File.Delete(path);throw;}
            return Results.Ok(new{message="Đã lưu ảnh minh chứng.",maMinhChung=fileId});
        }).DisableAntiforgery();

        group.MapGet("/{id:guid}/evidence/{fileId:guid}",async(Guid id,Guid fileId,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            if(!await CoQuyenXemXuLy(cn,actor,id))return Results.NotFound();
            var evidence=await cn.QueryFirstOrDefaultAsync<TepMinhChungRow>(@"
                SELECT DuongDanLuuTru,LoaiNoiDung,TenTep,MaBamSha256
                FROM dbo.MinhChungBaoCaoVanBanDen WHERE MaMinhChung=@fileId AND MaVanBanDen=@id",
                new{id,fileId});
            if(evidence is null || !File.Exists(evidence.DuongDanLuuTru))return Results.NotFound();
            using var stream=File.OpenRead(evidence.DuongDanLuuTru);
            var bytes=new byte[stream.Length];
            await stream.ReadExactlyAsync(bytes);
            if(Convert.ToHexString(SHA256.HashData(bytes))!=evidence.MaBamSha256)
                return Results.Conflict(new{message="Ảnh minh chứng không còn nguyên vẹn."});
            return Results.File(bytes,evidence.LoaiNoiDung,evidence.TenTep);
        });

        group.MapPost("/{id:guid}/complete",async(Guid id,KetThucVanBanDen body,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            if(string.IsNullOrWhiteSpace(body.KetQuaTongHop)||body.KetQuaTongHop.Length>4000)
                return Results.BadRequest(new{message="Cần nhập kết quả tổng hợp tối đa 4000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            if(!await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"LANH_DAO","PHO_LANH_DAO"))return Results.Forbid();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var done=await cn.ExecuteAsync(@"
                UPDATE p SET TrangThai='HOAN_THANH',KetQuaTongHop=@content,
                    MaNguoiKetThuc=@user,ThoiDiemKetThuc=SYSUTCDATETIME()
                FROM dbo.PhanCongVanBanDen p
                WHERE p.MaVanBanDen=@id AND p.MaCoQuan=@agency AND p.TrangThai='DANG_XU_LY'
                  AND EXISTS(SELECT 1 FROM dbo.VanBanDen d
                             WHERE d.MaVanBanDen=p.MaVanBanDen AND d.SoDen IS NOT NULL)
                  AND EXISTS(SELECT 1 FROM dbo.KetQuaPhongVanBanDen k
                             WHERE k.MaVanBanDen=p.MaVanBanDen AND k.MaPhongBan=p.MaPhongChuTri)",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,content=body.KetQuaTongHop.Trim()},tx);
            if(done==0){tx.Rollback();return Results.Conflict(new{message="Chưa cấp số đến, phòng chủ trì chưa báo cáo hoặc văn bản đã kết thúc."});}
            await cn.ExecuteAsync(@"UPDATE dbo.VanBanDen SET TrangThai='HOAN_THANH'
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency",new{id,agency=actor.MaCoQuan},tx);
            tx.Commit();return Results.Ok(new{message="Đã kết thúc xử lý văn bản đến."});
        });
    }

    private static async Task<bool> CoQuyenXemXuLy(System.Data.IDbConnection cn,NguoiThucHien actor,Guid id)
    {
        var leader=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan!.Value,"LANH_DAO","PHO_LANH_DAO");
        var clerk=await CoVaiTro(cn,actor.MaNguoiDung,actor.MaCoQuan.Value,"VAN_THU");
        return await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.VanBanDen d
            LEFT JOIN dbo.PhanCongVanBanDen p ON p.MaVanBanDen=d.MaVanBanDen
            JOIN dbo.NguoiDung n ON n.MaNguoiDung=@user
            WHERE d.MaVanBanDen=@id AND d.MaCoQuanNhan=@agency AND
                (@leader=1 OR @clerk=1 OR p.MaPhongChuTri=n.MaPhongBan OR EXISTS(
                   SELECT 1 FROM dbo.PhoiHopVanBanDen h WHERE h.MaVanBanDen=@id AND h.MaPhongBan=n.MaPhongBan))",
            new{id,user=actor.MaNguoiDung,agency=actor.MaCoQuan,leader,clerk})>0;
    }
}
