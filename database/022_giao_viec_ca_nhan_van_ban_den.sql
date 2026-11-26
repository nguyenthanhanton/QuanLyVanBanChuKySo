/* v0.8.1: Truong/pho phong phan cong ca nhan chu tri va phoi hop. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=21)
    THROW 51022,N'Can chay 021 truoc 022.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.PhanCongCaNhanVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.PhanCongCaNhanVanBanDen (
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.PhanCongVanBanDen(MaVanBanDen),
        MaPhongBan INT NOT NULL REFERENCES dbo.PhongBan(MaPhongBan),
        MaNguoiChuTri INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaNguoiGiao INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        GhiChu NVARCHAR(2000) NULL,
        ThoiDiemGiao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PhanCongCaNhanDen PRIMARY KEY(MaVanBanDen,MaPhongBan)
    );
END;
IF OBJECT_ID('dbo.NguoiPhoiHopVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.NguoiPhoiHopVanBanDen (
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL,
        MaPhongBan INT NOT NULL,
        MaNguoiDung INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        PRIMARY KEY(MaVanBanDen,MaPhongBan,MaNguoiDung),
        CONSTRAINT FK_NguoiPhoiHopDen_PhanCong FOREIGN KEY(MaVanBanDen,MaPhongBan)
            REFERENCES dbo.PhanCongCaNhanVanBanDen(MaVanBanDen,MaPhongBan)
    );
END;
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=22)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(22,N'Giao viec van ban den den ca nhan trong tung phong');
COMMIT TRANSACTION;
GO
