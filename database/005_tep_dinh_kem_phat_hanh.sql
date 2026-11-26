/*
  Migration 005: Dong bang danh sach tep kem theo ban phat hanh.
  Them moi, KHONG xoa du lieu cu. Chay sau 004.
*/
USE QuanLyVanBanChuKySo;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=5)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    CREATE TABLE dbo.BanPhatHanhTep (
        MaPhatHanh UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.BanPhatHanh(MaPhatHanh),
        MaTep UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.TepDinhKem(MaTep),
        MaBamSha256 CHAR(64) NOT NULL,
        PRIMARY KEY(MaPhatHanh,MaTep)
    );

    -- Snapshot old publications, if any, from their immutable released version.
    INSERT dbo.BanPhatHanhTep(MaPhatHanh,MaTep,MaBamSha256)
    SELECT p.MaPhatHanh,f.MaTep,f.MaBamSha256
    FROM dbo.BanPhatHanh p
    JOIN dbo.PhienBanVanBan v ON v.MaVanBan=p.MaVanBan AND v.SoPhienBan=p.SoPhienBan
    JOIN dbo.TepDinhKem f ON f.MaPhienBan=v.MaPhienBan;

    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(5,N'Đóng băng tệp đính kèm khi phát hành');
    COMMIT TRANSACTION;
END;
GO

-- Prevent changing a document title even outside the HTTP API.
CREATE OR ALTER TRIGGER dbo.KhoaTieuDeVanBan
ON dbo.VanBan AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(TieuDe) AND EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.MaVanBan=i.MaVanBan
        WHERE i.TieuDe<>d.TieuDe
    )
        THROW 51005, N'Tiêu đề văn bản đã tạo không được thay đổi.', 1;
END;
GO
