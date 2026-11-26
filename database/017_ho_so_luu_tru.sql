/* v0.7 - Ho so luu tru dien tu, lien ket va nhat ky. Khong sua van ban da ky. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=16)
    THROW 51019,N'Can chay script 016 truoc 017.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.HoSoLuuTru','U') IS NULL
BEGIN
    CREATE TABLE dbo.HoSoLuuTru (
        MaHoSo UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        MaCoQuan INT NOT NULL REFERENCES dbo.CoQuan(MaCoQuan),
        MaPhongBan INT NULL,
        MaNguoiTao INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaHoSoNoiBo VARCHAR(60) NOT NULL,
        TenHoSo NVARCHAR(250) NOT NULL,
        LoaiHoSo NVARCHAR(100) NOT NULL,
        NamHoSo INT NOT NULL,
        SoNamBaoQuan INT NULL,
        GhiChu NVARCHAR(1000) NULL,
        DaDong BIT NOT NULL DEFAULT 0,
        ThoiDiemTao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ThoiDiemCapNhat DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_HoSoLuuTru_Ma UNIQUE(MaCoQuan,MaHoSoNoiBo),
        CONSTRAINT UQ_HoSoLuuTru_PhamVi UNIQUE(MaHoSo,MaCoQuan),
        CONSTRAINT FK_HoSoLuuTru_Phong FOREIGN KEY(MaCoQuan,MaPhongBan)
            REFERENCES dbo.PhongBan(MaCoQuan,MaPhongBan),
        CONSTRAINT CK_HoSoLuuTru_Nam CHECK(NamHoSo BETWEEN 1900 AND 2200),
        CONSTRAINT CK_HoSoLuuTru_BaoQuan CHECK(SoNamBaoQuan IS NULL OR SoNamBaoQuan BETWEEN 1 AND 200)
    );
END;
IF OBJECT_ID('dbo.HoSoVanBan','U') IS NULL
BEGIN
    CREATE TABLE dbo.HoSoVanBan (
        MaHoSo UNIQUEIDENTIFIER NOT NULL,
        MaCoQuan INT NOT NULL,
        MaVanBan UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.VanBan(MaVanBan),
        MaNguoiThem INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemThem DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_HoSoVanBan PRIMARY KEY(MaHoSo,MaVanBan),
        CONSTRAINT FK_HoSoVanBan_HoSo FOREIGN KEY(MaHoSo,MaCoQuan)
            REFERENCES dbo.HoSoLuuTru(MaHoSo,MaCoQuan)
    );
    CREATE INDEX IX_HoSoVanBan_VanBan ON dbo.HoSoVanBan(MaVanBan);
END;
IF OBJECT_ID('dbo.LichSuHoSo','U') IS NULL
BEGIN
    CREATE TABLE dbo.LichSuHoSo (
        MaLichSu BIGINT IDENTITY(1,1) PRIMARY KEY,
        MaHoSo UNIQUEIDENTIFIER NOT NULL,
        MaCoQuan INT NOT NULL,
        MaVanBan UNIQUEIDENTIFIER NULL,
        MaNguoiThucHien INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        HanhDong VARCHAR(30) NOT NULL,
        MoTa NVARCHAR(500) NOT NULL,
        ThoiDiem DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_LichSuHoSo_HoSo FOREIGN KEY(MaHoSo,MaCoQuan)
            REFERENCES dbo.HoSoLuuTru(MaHoSo,MaCoQuan)
    );
    CREATE INDEX IX_LichSuHoSo_ThoiDiem ON dbo.LichSuHoSo(MaHoSo,ThoiDiem DESC);
END;
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=17)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(17,N'Ho so luu tru dien tu - lien ket van ban, bao quan va nhat ky');
COMMIT TRANSACTION;
GO
