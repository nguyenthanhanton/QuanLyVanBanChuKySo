/* v0.5.6.1: Lua chon nguoi ky dung phong ban va lanh dao.
   Van ban cu giu so buoc 5; van ban moi sau khi chon nguoi ky co so buoc 4.
   Chi chay sau migration 007. Script co the chay lai an toan. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=8)
BEGIN
    BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=7)
        THROW 51008,N'Can chay migration 007 truoc 008.',1;
    IF COL_LENGTH('dbo.VanBan','MaNguoiKyNhay') IS NULL
        ALTER TABLE dbo.VanBan ADD MaNguoiKyNhay INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung);
    IF COL_LENGTH('dbo.VanBan','MaNguoiKyLanhDao') IS NULL
        ALTER TABLE dbo.VanBan ADD MaNguoiKyLanhDao INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung);
    IF COL_LENGTH('dbo.VanBan','SoBuocKy') IS NULL
        ALTER TABLE dbo.VanBan ADD SoBuocKy TINYINT NOT NULL
            CONSTRAINT DF_VanBan_SoBuocKy DEFAULT 5;
    -- SQL Server compiles CHECK expressions before ALTER ADD becomes visible in the same batch.
    IF NOT EXISTS(SELECT 1 FROM sys.check_constraints
                  WHERE parent_object_id=OBJECT_ID('dbo.VanBan') AND name='CK_VanBan_SoBuocKy')
        EXEC sys.sp_executesql N'ALTER TABLE dbo.VanBan WITH CHECK ADD CONSTRAINT CK_VanBan_SoBuocKy CHECK(SoBuocKy IN (4,5))';
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(8,N'Chon nguoi ky theo phong ban - quy trinh 4 vi tri cho van ban moi');
    COMMIT TRANSACTION;
END;
GO
