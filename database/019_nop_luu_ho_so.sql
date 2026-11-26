/* 0.7.1: Nop luu, tiep nhan / tra lai, phieu bien nhan. Khong thay doi PDF va bang 001-018. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=18)
    THROW 51021,N'Can chay 018 truoc 019.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.LuotNopLuuHoSo','U') IS NULL
BEGIN
    CREATE TABLE dbo.LuotNopLuuHoSo (
        MaLuot BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MaHoSo UNIQUEIDENTIFIER NOT NULL,
        MaCoQuan INT NOT NULL,
        MaNguoiNop INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemNop DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        TrangThai VARCHAR(20) NOT NULL DEFAULT 'CHO_TIEP_NHAN',
        MaNguoiXuLy INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemXuLy DATETIME2(3) NULL,
        LyDoTraLai NVARCHAR(1000) NULL,
        MaBienNhan UNIQUEIDENTIFIER NULL,
        CONSTRAINT FK_LuotNopLuu_HoSo FOREIGN KEY(MaHoSo,MaCoQuan)
            REFERENCES dbo.HoSoLuuTru(MaHoSo,MaCoQuan),
        CONSTRAINT CK_LuotNopLuu_TrangThai CHECK(TrangThai IN ('CHO_TIEP_NHAN','TRA_LAI','DA_TIEP_NHAN'))
    );
    CREATE UNIQUE INDEX UX_LuotNopLuu_MotLuotDangCho
        ON dbo.LuotNopLuuHoSo(MaHoSo) WHERE TrangThai='CHO_TIEP_NHAN';
    CREATE UNIQUE INDEX UX_LuotNopLuu_DaTiepNhan
        ON dbo.LuotNopLuuHoSo(MaHoSo) WHERE TrangThai='DA_TIEP_NHAN';
    CREATE UNIQUE INDEX UX_LuotNopLuu_BienNhan
        ON dbo.LuotNopLuuHoSo(MaBienNhan) WHERE MaBienNhan IS NOT NULL;
    CREATE INDEX IX_LuotNopLuu_CoQuan ON dbo.LuotNopLuuHoSo(MaCoQuan,TrangThai,ThoiDiemNop DESC);
END;
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=19)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(19,N'Nop luu ho so, van thu tiep nhan va phieu bien nhan dien tu');
COMMIT TRANSACTION;
GO
