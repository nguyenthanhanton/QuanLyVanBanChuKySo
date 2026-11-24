/* Ban 0.4: phan biet tep PDF chinh de ky va cac tep dinh kem. */
USE QuanLyVanBanChuKySo;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=6)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    ALTER TABLE dbo.TepDinhKem ADD LaVanBanChinh BIT NOT NULL
        CONSTRAINT DF_TepDinhKem_LaVanBanChinh DEFAULT (0);

    -- Du lieu tu ban 0.3 chua phan biet tep chinh: chon mot PDF
    -- theo thu tu on dinh trong moi phien ban de giu kha nang ky so.
    ;WITH PdfCu AS (
        SELECT MaTep, ROW_NUMBER() OVER (
            PARTITION BY MaPhienBan ORDER BY ThoiDiemTao, MaTep) AS SoThuTu
        FROM dbo.TepDinhKem WHERE LoaiNoiDung='application/pdf'
    )
    UPDATE f SET LaVanBanChinh=1
    FROM dbo.TepDinhKem f JOIN PdfCu p ON p.MaTep=f.MaTep
    WHERE p.SoThuTu=1;

    CREATE UNIQUE INDEX UX_TepDinhKem_VanBanChinh
        ON dbo.TepDinhKem(MaPhienBan) WHERE LaVanBanChinh=1;
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(6,N'Phân biệt văn bản chính PDF và các tệp đính kèm');
    COMMIT TRANSACTION;
END;
GO
