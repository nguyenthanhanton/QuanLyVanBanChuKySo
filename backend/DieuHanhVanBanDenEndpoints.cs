using Dapper;

public sealed record GiaoXuLyVanBanDen(int MaPhongChuTri,int[] MaPhongPhoiHop,string? ChiDao,DateTime? HanXuLy);
public sealed record BaoCaoVanBanDen(string NoiDung);
public sealed record KetThucVanBanDen(string KetQuaTongHop);

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
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency AND TrangThai='CHO_DANG_KY'",
                new{id,agency=actor.MaCoQuan});
            return changed==1?Results.Ok(new{message="Đã trình lãnh đạo"}):Results.Conflict(new{message="Văn bản không ở trạng thái chờ trình hoặc không thuộc cơ quan."});
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

        group.MapPost("/{id:guid}/report",async(Guid id,BaoCaoVanBanDen body,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            if(string.IsNullOrWhiteSpace(body.NoiDung)||body.NoiDung.Length>4000)
                return Results.BadRequest(new{message="Nội dung báo cáo phải có từ 1 đến 4000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var department=await cn.QuerySingleOrDefaultAsync<int?>(@"
                SELECT n.MaPhongBan FROM dbo.NguoiDung n
                JOIN dbo.PhanCongVanBanDen p WITH(UPDLOCK,HOLDLOCK) ON p.MaVanBanDen=@id
                WHERE n.MaNguoiDung=@user AND n.MaCoQuan=p.MaCoQuan AND p.MaCoQuan=@agency
                  AND p.TrangThai='DANG_XU_LY' AND
                    (p.MaPhongChuTri=n.MaPhongBan OR EXISTS(
                        SELECT 1 FROM dbo.PhoiHopVanBanDen h
                        WHERE h.MaVanBanDen=@id AND h.MaPhongBan=n.MaPhongBan))",
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
                  AND EXISTS(SELECT 1 FROM dbo.KetQuaPhongVanBanDen k
                             WHERE k.MaVanBanDen=p.MaVanBanDen AND k.MaPhongBan=p.MaPhongChuTri)",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,content=body.KetQuaTongHop.Trim()},tx);
            if(done==0){tx.Rollback();return Results.Conflict(new{message="Phòng chủ trì chưa báo cáo hoặc văn bản đã kết thúc."});}
            await cn.ExecuteAsync(@"UPDATE dbo.VanBanDen SET TrangThai='HOAN_THANH'
                WHERE MaVanBanDen=@id AND MaCoQuanNhan=@agency",new{id,agency=actor.MaCoQuan},tx);
            tx.Commit();return Results.Ok(new{message="Đã kết thúc xử lý văn bản đến."});
        });
    }
}
