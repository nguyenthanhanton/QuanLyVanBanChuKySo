/* v0.6.4.2 — Ủy quyền một tác vụ ký/duyệt cụ thể, có hết hạn và thu hồi. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=14)
    THROW 51017,N'Can chay script 014 truoc 015.',1;

IF OBJECT_ID('dbo.UyQuyenKyTacVu','U') IS NULL
    CREATE TABLE dbo.UyQuyenKyTacVu(
      MaTacVu UNIQUEIDENTIFIER NOT NULL PRIMARY KEY REFERENCES dbo.TacVuXuLy(MaTacVu),
      MaVanBan UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.VanBan(MaVanBan),
      MaCoQuan INT NOT NULL REFERENCES dbo.CoQuan(MaCoQuan),
      MaNguoiGoc INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
      MaNguoiDuocUyQuyen INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
      MaNguoiCap INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
      LyDo NVARCHAR(500) NOT NULL,
      HieuLucTu DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
      HieuLucDen DATETIME2(3) NOT NULL,
      DaThuHoi BIT NOT NULL DEFAULT 0,
      ThoiDiemThuHoi DATETIME2(3) NULL,
      CONSTRAINT CK_UyQuyenKy_KhongTuUyQuyen CHECK(MaNguoiGoc<>MaNguoiDuocUyQuyen),
      CONSTRAINT CK_UyQuyenKy_ThoiHan CHECK(HieuLucDen>HieuLucTu)
    );

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=15)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(15,N'Uy quyen ky theo tac vu, co thoi han va nhat ky thu hoi');
COMMIT TRANSACTION;
GO
