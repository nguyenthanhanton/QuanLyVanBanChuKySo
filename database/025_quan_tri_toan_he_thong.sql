/* 0.9+ — Kich hoat khu vuc quan tri toan he thong.
   Khong tao lai tai khoan, khong sua mat khau, khong cap quyen doc van ban.
   Chay sau migration 024. Co the chay lai an toan. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=24)
    THROW 51025,N'Can ap dung migration 024 truoc 025.',1;

IF NOT EXISTS(SELECT 1 FROM dbo.VaiTro WHERE MaDinhDanh='SYSTEM_ADMIN')
    THROW 51026,N'Thieu vai tro SYSTEM_ADMIN. Kiem tra du lieu khoi tao.',1;

IF NOT EXISTS(SELECT 1 FROM dbo.VaiTro WHERE MaDinhDanh='AGENCY_ADMIN')
    THROW 51027,N'Thieu vai tro AGENCY_ADMIN. Kiem tra du lieu khoi tao.',1;

IF NOT EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro uv
    JOIN dbo.VaiTro vt ON vt.MaVaiTro=uv.MaVaiTro AND vt.MaDinhDanh='SYSTEM_ADMIN'
    JOIN dbo.NguoiDung n ON n.MaNguoiDung=uv.MaNguoiDung
    WHERE n.DangHoatDong=1 AND n.MaCoQuan IS NULL AND uv.MaCoQuan IS NULL)
    THROW 51028,N'Chua co quan tri he thong hoat dong. Hay kiem tra tai khoan seed an toan.',1;

INSERT dbo.VaiTroQuyen(MaCoQuan,MaVaiTro,MaQuyen)
SELECT NULL,vt.MaVaiTro,q.MaQuyen
FROM dbo.VaiTro vt CROSS JOIN dbo.Quyen q
WHERE vt.MaDinhDanh='SYSTEM_ADMIN'
  AND q.MaDinhDanh IN ('agencies.manage','agency.admins.manage','audit.technical.read')
  AND NOT EXISTS (SELECT 1 FROM dbo.VaiTroQuyen existing
    WHERE existing.MaCoQuan IS NULL AND existing.MaVaiTro=vt.MaVaiTro AND existing.MaQuyen=q.MaQuyen);

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=25)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(25,N'Quan tri toan he thong va cap quan tri co quan');

COMMIT TRANSACTION;
GO
