USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=23)
    THROW 51024,N'Can chay 023 truoc 024.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.HoSoLuuTruVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.HoSoLuuTruVanBanDen (
        MaHoSo UNIQUEIDENTIFIER NOT NULL,
        MaCoQuan INT NOT NULL,
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.VanBanDen(MaVanBanDen),
        MaNguoiThem INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemThem DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_HoSoLuuTruVanBanDen PRIMARY KEY(MaHoSo,MaVanBanDen),
        CONSTRAINT FK_HoSoLuuTruVanBanDen_HoSo FOREIGN KEY(MaHoSo,MaCoQuan)
            REFERENCES dbo.HoSoLuuTru(MaHoSo,MaCoQuan)
    );
    CREATE INDEX IX_HoSoLuuTruVanBanDen_VanBan ON dbo.HoSoLuuTruVanBanDen(MaVanBanDen);
END;
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=24)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(24,N'Cap so den va lien ket van ban den vao ho so luu tru');
COMMIT TRANSACTION;
GO
