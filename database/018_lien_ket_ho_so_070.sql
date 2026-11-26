/* v0.7 hotfix - dbo.HoSoVanBan da co tu 001 va thuoc dbo.HoSo cu.
   Tao bang lien ket RIENG cho dbo.HoSoLuuTru. Khong doi/xoa du lieu cu. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=17)
    THROW 51020,N'Can chay script 017 truoc 018.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.HoSoLuuTruVanBan','U') IS NULL
BEGIN
    CREATE TABLE dbo.HoSoLuuTruVanBan (
        MaHoSo UNIQUEIDENTIFIER NOT NULL,
        MaCoQuan INT NOT NULL,
        MaVanBan UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.VanBan(MaVanBan),
        MaNguoiThem INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemThem DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_HoSoLuuTruVanBan PRIMARY KEY(MaHoSo,MaVanBan),
        CONSTRAINT FK_HoSoLuuTruVanBan_HoSo FOREIGN KEY(MaHoSo,MaCoQuan)
            REFERENCES dbo.HoSoLuuTru(MaHoSo,MaCoQuan)
    );
    CREATE INDEX IX_HoSoLuuTruVanBan_VanBan ON dbo.HoSoLuuTruVanBan(MaVanBan);
END;
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=18)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(18,N'Tach lien ket ho so luu tru moi khoi HoSoVanBan cu');
COMMIT TRANSACTION;
GO
