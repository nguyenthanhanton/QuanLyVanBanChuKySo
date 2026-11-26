/* v0.6.5 - Luu trang thai da doc; khong can thiep trang thai van ban. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=15)
    THROW 51018,N'Can chay script 015 truoc 016.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.ThongBaoDaDoc','U') IS NULL
    CREATE TABLE dbo.ThongBaoDaDoc (
        MaNguoiDung INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaCoQuan INT NOT NULL REFERENCES dbo.CoQuan(MaCoQuan),
        LoaiThongBao VARCHAR(35) NOT NULL,
        MaThamChieu NVARCHAR(80) NOT NULL,
        ThoiDiemDoc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ThongBaoDaDoc PRIMARY KEY(MaNguoiDung,MaCoQuan,LoaiThongBao,MaThamChieu)
    );
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=16)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(16,N'Giam sat tien do va luu trang thai thong bao da doc');
COMMIT TRANSACTION;
GO
