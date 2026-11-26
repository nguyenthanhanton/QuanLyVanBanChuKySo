using Dapper;

public static class ViecCaNhanEndpoints
{
    public static void MapViecCaNhan(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/my-work",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var actor=await permission.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            // Nhung muc nay khong phai TacVuXuLy: loc tai SQL theo tai khoan va co quan.
            var jobs=await cn.QueryAsync(@"
                SELECT v.MaVanBan AS MaThamChieu,v.MaNoiBo,v.TieuDe,
                       CAST('BAN_NHAP' AS VARCHAR(30)) AS Loai,
                       CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ChuKySo s
                           WHERE s.MaVanBan=v.MaVanBan AND s.SoPhienBan=v.PhienBanHienTai
                             AND s.LoaiChuKy='KY_NGUOI_SOAN' AND s.MaNguoiKy=@user)
                         THEN N'Đã ký người soạn — chờ trình duyệt'
                         ELSE N'Chưa ký PDF người soạn' END AS NVARCHAR(160)) AS CongViec,
                       CAST(NULL AS DATETIME2) AS HanXuLy,v.ThoiDiemCapNhat AS ThoiDiem
                FROM dbo.VanBan v
                WHERE v.MaCoQuan=@agency AND v.MaNguoiTao=@user
                  AND v.TrangThai IN ('NHAP','TRA_SUA')
                UNION ALL
                SELECT d.MaVanBanDen,d.TieuDe,d.TieuDe,'VAN_BAN_DEN',
                       CASE WHEN d.SoDen IS NULL THEN N'Cấp số đến'
                            ELSE N'Trình lãnh đạo' END,NULL,d.ThoiDiemTiepNhan
                FROM dbo.VanBanDen d
                WHERE d.MaCoQuanNhan=@agency AND d.TrangThai='CHO_DANG_KY'
                  AND EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro ur
                    JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro
                    WHERE ur.MaNguoiDung=@user AND ur.MaCoQuan=@agency AND r.MaDinhDanh='VAN_THU')
                UNION ALL
                SELECT d.MaVanBanDen,d.TieuDe,d.TieuDe,'VAN_BAN_DEN',
                       N'Lãnh đạo giao phòng chủ trì/phối hợp',NULL,d.ThoiDiemTiepNhan
                FROM dbo.VanBanDen d
                WHERE d.MaCoQuanNhan=@agency AND d.TrangThai='CHO_LANH_DAO'
                  AND EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro ur
                    JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro
                    WHERE ur.MaNguoiDung=@user AND ur.MaCoQuan=@agency
                      AND r.MaDinhDanh IN ('LANH_DAO','PHO_LANH_DAO'))
                UNION ALL
                SELECT d.MaVanBanDen,d.TieuDe,d.TieuDe,'VAN_BAN_DEN',
                       N'Phân công cá nhân xử lý',p.HanXuLy,p.ThoiDiemGiao
                FROM dbo.PhanCongVanBanDen p
                JOIN dbo.VanBanDen d ON d.MaVanBanDen=p.MaVanBanDen
                CROSS APPLY(SELECT p.MaPhongChuTri AS MaPhongBan UNION
                    SELECT h.MaPhongBan FROM dbo.PhoiHopVanBanDen h WHERE h.MaVanBanDen=p.MaVanBanDen) room
                JOIN dbo.NguoiDung u ON u.MaNguoiDung=@user AND u.MaPhongBan=room.MaPhongBan
                WHERE p.MaCoQuan=@agency AND p.TrangThai='DANG_XU_LY'
                  AND NOT EXISTS(SELECT 1 FROM dbo.PhanCongCaNhanVanBanDen a
                    WHERE a.MaVanBanDen=p.MaVanBanDen AND a.MaPhongBan=room.MaPhongBan)
                  AND NOT EXISTS(SELECT 1 FROM dbo.KetQuaPhongVanBanDen k
                    WHERE k.MaVanBanDen=p.MaVanBanDen AND k.MaPhongBan=room.MaPhongBan)
                  AND EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro ur
                    JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro
                    WHERE ur.MaNguoiDung=@user AND ur.MaCoQuan=@agency
                      AND r.MaDinhDanh IN ('TRUONG_PHONG','PHO_PHONG'))
                UNION ALL
                SELECT d.MaVanBanDen,d.TieuDe,d.TieuDe,'VAN_BAN_DEN',
                       CASE WHEN a.MaNguoiChuTri=@user THEN N'Chủ trì thực hiện và gửi báo cáo'
                            ELSE N'Phối hợp xử lý văn bản đến' END,
                       p.HanXuLy,a.ThoiDiemGiao
                FROM dbo.PhanCongCaNhanVanBanDen a
                JOIN dbo.PhanCongVanBanDen p ON p.MaVanBanDen=a.MaVanBanDen
                JOIN dbo.VanBanDen d ON d.MaVanBanDen=a.MaVanBanDen
                WHERE p.MaCoQuan=@agency AND p.TrangThai='DANG_XU_LY'
                  AND NOT EXISTS(SELECT 1 FROM dbo.KetQuaPhongVanBanDen k
                    WHERE k.MaVanBanDen=a.MaVanBanDen AND k.MaPhongBan=a.MaPhongBan)
                  AND (a.MaNguoiChuTri=@user OR EXISTS(SELECT 1 FROM dbo.NguoiPhoiHopVanBanDen h
                    WHERE h.MaVanBanDen=a.MaVanBanDen AND h.MaPhongBan=a.MaPhongBan AND h.MaNguoiDung=@user))
                UNION ALL
                SELECT l.MaLuotChuyen,v.MaNoiBo,v.TieuDe,'TIEP_NHAN',
                       N'Tiếp nhận văn bản liên cơ quan',NULL,l.ThoiDiemGui
                FROM dbo.LuotChuyenVanBan l
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE l.MaCoQuanNhan=@agency AND l.TrangThai='DA_GUI'
                  AND EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro ur
                    JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro
                    WHERE ur.MaNguoiDung=@user AND ur.MaCoQuan=@agency AND r.MaDinhDanh='VAN_THU')
                ORDER BY ThoiDiem DESC",
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung});
            return Results.Ok(jobs);
        }).RequireAuthorization();
    }
}
