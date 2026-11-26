/* v0.6.3 bổ sung: mỗi cơ quan tối đa một văn thư đang hoạt động,
   bố trí tại Ban lãnh đạo, giữ nguyên văn bản và hành trình đã tạo. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=12)
    THROW 51013,N'Can chay script 012 truoc 013.',1;

-- Không tự ý chọn hoặc khóa tài khoản nếu một cơ quan đang có nhiều văn thư.
IF EXISTS(
    SELECT n.MaCoQuan FROM dbo.NguoiDung n
    JOIN dbo.NguoiDungVaiTro uv ON uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=n.MaCoQuan
    JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro AND r.MaDinhDanh='VAN_THU'
    WHERE n.DangHoatDong=1
    GROUP BY n.MaCoQuan HAVING COUNT(DISTINCT n.MaNguoiDung)>1
)
    THROW 51014,N'Co quan dang co hon mot Van thu hoat dong. Hay kiem tra va khoa/phan cong lai truoc khi chay 013.',1;

-- Phải có Ban lãnh đạo tương ứng để chuyển tài khoản.
IF EXISTS(
    SELECT 1 FROM dbo.NguoiDung n
    JOIN dbo.NguoiDungVaiTro uv ON uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=n.MaCoQuan
    JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro AND r.MaDinhDanh='VAN_THU'
    WHERE NOT EXISTS(SELECT 1 FROM dbo.PhongBan p
        WHERE p.MaCoQuan=n.MaCoQuan AND p.MaDinhDanh='LANH_DAO' AND p.DangHoatDong=1)
)
    THROW 51015,N'Thieu Ban lanh dao dang hoat dong tai mot co quan co Van thu.',1;

UPDATE n SET n.MaPhongBan=p.MaPhongBan
FROM dbo.NguoiDung n
JOIN dbo.NguoiDungVaiTro uv ON uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=n.MaCoQuan
JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro AND r.MaDinhDanh='VAN_THU'
JOIN dbo.PhongBan p ON p.MaCoQuan=n.MaCoQuan AND p.MaDinhDanh='LANH_DAO'
WHERE n.MaPhongBan<>p.MaPhongBan;

-- Phòng Văn thư cũ không còn chọn khi tạo tài khoản mới.
-- Không xóa phòng để giữ các tham chiếu trong văn bản cũ.
UPDATE p SET DangHoatDong=0 FROM dbo.PhongBan p
WHERE p.MaDinhDanh='VAN_THU'
  AND NOT EXISTS(SELECT 1 FROM dbo.NguoiDung n WHERE n.MaPhongBan=p.MaPhongBan);

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=13)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(13,N'Mot van thu dang hoat dong trong Ban lanh dao moi co quan');

COMMIT TRANSACTION;
GO
