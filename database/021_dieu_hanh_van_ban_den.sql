/* v0.8.0: mot phong chu tri va nhieu phong phoi hop cho van ban den. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=20)
    THROW 51021,N'Can chay script 020 truoc 021.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.PhanCongVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.PhanCongVanBanDen (
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL PRIMARY KEY REFERENCES dbo.VanBanDen(MaVanBanDen),
        MaCoQuan INT NOT NULL REFERENCES dbo.CoQuan(MaCoQuan),
        MaPhongChuTri INT NOT NULL,
        MaNguoiGiao INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        HanXuLy DATETIME2(3) NULL,
        ChiDao NVARCHAR(2000) NULL,
        TrangThai VARCHAR(25) NOT NULL DEFAULT 'DANG_XU_LY',
        KetQuaTongHop NVARCHAR(4000) NULL,
        MaNguoiKetThuc INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemGiao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ThoiDiemKetThuc DATETIME2(3) NULL,
        CONSTRAINT FK_PhanCongDen_Phong FOREIGN KEY(MaCoQuan,MaPhongChuTri)
            REFERENCES dbo.PhongBan(MaCoQuan,MaPhongBan),
        CONSTRAINT CK_PhanCongDen_TrangThai CHECK(TrangThai IN ('DANG_XU_LY','HOAN_THANH'))
    );
END;
IF OBJECT_ID('dbo.PhoiHopVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.PhoiHopVanBanDen (
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.PhanCongVanBanDen(MaVanBanDen),
        MaPhongBan INT NOT NULL REFERENCES dbo.PhongBan(MaPhongBan),
        PRIMARY KEY(MaVanBanDen,MaPhongBan)
    );
END;
IF OBJECT_ID('dbo.KetQuaPhongVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.KetQuaPhongVanBanDen (
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.PhanCongVanBanDen(MaVanBanDen),
        MaPhongBan INT NOT NULL REFERENCES dbo.PhongBan(MaPhongBan),
        MaNguoiBaoCao INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        NoiDung NVARCHAR(4000) NOT NULL,
        ThoiDiemBaoCao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        PRIMARY KEY(MaVanBanDen,MaPhongBan)
    );
END;
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=21)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(21,N'Dieu hanh van ban den: mot phong chu tri, nhieu phong phoi hop');
COMMIT TRANSACTION;
GO
