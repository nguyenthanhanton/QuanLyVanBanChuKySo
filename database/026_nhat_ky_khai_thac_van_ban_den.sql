/* 0.9 QA fix: record the incoming document ID in controlled archive access logs.
   Additive, idempotent, no existing audit rows are modified. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=25)
    THROW 51026,N'Can chay 025 truoc 026.',1;
GO
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.NhatKyKhaiThacHoSo','MaVanBanDen') IS NULL
    ALTER TABLE dbo.NhatKyKhaiThacHoSo
        ADD MaVanBanDen UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name='FK_NhatKyKhaiThacHoSo_VanBanDen'
                 AND parent_object_id=OBJECT_ID('dbo.NhatKyKhaiThacHoSo'))
    ALTER TABLE dbo.NhatKyKhaiThacHoSo
        ADD CONSTRAINT FK_NhatKyKhaiThacHoSo_VanBanDen
        FOREIGN KEY(MaVanBanDen) REFERENCES dbo.VanBanDen(MaVanBanDen);

IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=26)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
        VALUES(26,N'Ghi nhat ky khai thac van ban den trong ho so');
COMMIT TRANSACTION;
GO
