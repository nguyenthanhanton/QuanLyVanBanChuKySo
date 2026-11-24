/*
  v0.5.1: Mo rong so ban PDF ky tuan tu den 5 va quyen nguoi soan.
  KHONG kich hoat quy trinh 5 cap; API 0.4 van ky 3 cap nhu cu.
  Chay sau 006, tren ban sao luu SQL Server; co the chay lai an toan.
*/
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan = 7)
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan = 6)
            THROW 50007, N'Can chay migration 006 truoc migration 007.', 1;

        IF NOT EXISTS (
            SELECT 1 FROM sys.check_constraints
            WHERE name = 'CK_BanPdfDaKy_ThuTu'
              AND parent_object_id = OBJECT_ID('dbo.BanPdfDaKy')
        )
            THROW 50008, N'Khong tim thay rang buoc CK_BanPdfDaKy_ThuTu; dung de kiem tra luoc do.', 1;

        ALTER TABLE dbo.BanPdfDaKy DROP CONSTRAINT CK_BanPdfDaKy_ThuTu;
        ALTER TABLE dbo.BanPdfDaKy WITH CHECK ADD CONSTRAINT CK_BanPdfDaKy_ThuTu
            CHECK (ThuTuKy BETWEEN 0 AND 5);

        -- Quyen ky cua nguoi soan: cap theo tung co quan, khong cap quyen he thong.
        -- VAN_THU co the la nguoi soan, can quan ly chung thu ca nhan cua chinh minh.
        INSERT INTO dbo.VaiTroQuyen (MaVaiTro, MaQuyen, MaCoQuan)
        SELECT v.MaVaiTro, q.MaQuyen, c.MaCoQuan
        FROM dbo.CoQuan AS c
        CROSS JOIN dbo.VaiTro AS v
        CROSS JOIN dbo.Quyen AS q
        WHERE (
            (v.MaDinhDanh = 'CHUYEN_VIEN' AND q.MaDinhDanh IN ('signing.sign', 'signatures.manage-own'))
            OR (v.MaDinhDanh = 'VAN_THU' AND q.MaDinhDanh = 'signatures.manage-own')
        )
        AND NOT EXISTS (
            SELECT 1 FROM dbo.VaiTroQuyen AS vq
            WHERE vq.MaVaiTro = v.MaVaiTro
              AND vq.MaQuyen = q.MaQuyen
              AND vq.MaCoQuan = c.MaCoQuan
        );

        INSERT INTO dbo.PhienBanLuocDo (SoPhienBan, TenPhienBan)
        VALUES (7, N'Nen tang ky PDF nam vi tri - chua kich hoat quy trinh');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
