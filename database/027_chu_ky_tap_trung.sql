-- Version 2.1: registration of visible signature stamps, without modifying signed documents.
IF COL_LENGTH('dbo.ChungThuKy','TenMauChuKy') IS NULL
    ALTER TABLE dbo.ChungThuKy ADD TenMauChuKy NVARCHAR(180) NULL;
IF COL_LENGTH('dbo.ChungThuKy','HinhAnhChuKy') IS NULL
    ALTER TABLE dbo.ChungThuKy ADD HinhAnhChuKy VARBINARY(MAX) NULL;
GO
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ChungThuKy') AND name='UX_ChungThuKy_DangSuDung')
    DROP INDEX UX_ChungThuKy_DangSuDung ON dbo.ChungThuKy;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ChungThuKy') AND name='UX_ChungThuKy_CoQuan_DangSuDung')
    CREATE UNIQUE INDEX UX_ChungThuKy_CoQuan_DangSuDung ON dbo.ChungThuKy(MaCoQuan)
      WHERE DangHoatDong=1 AND LoaiChungThu='CO_QUAN';
GO
-- Agency certificate administration moves from clerk to chief leader.
DELETE vq FROM dbo.VaiTroQuyen vq
JOIN dbo.VaiTro v ON v.MaVaiTro=vq.MaVaiTro AND v.MaDinhDanh='VAN_THU'
JOIN dbo.Quyen q ON q.MaQuyen=vq.MaQuyen AND q.MaDinhDanh='signatures.manage-agency';
INSERT dbo.VaiTroQuyen(MaVaiTro,MaQuyen,MaCoQuan)
SELECT v.MaVaiTro,q.MaQuyen,c.MaCoQuan
FROM dbo.CoQuan c CROSS JOIN dbo.VaiTro v CROSS JOIN dbo.Quyen q
WHERE v.MaDinhDanh='LANH_DAO' AND q.MaDinhDanh='signatures.manage-agency'
AND NOT EXISTS(SELECT 1 FROM dbo.VaiTroQuyen existing
  WHERE existing.MaVaiTro=v.MaVaiTro AND existing.MaQuyen=q.MaQuyen AND existing.MaCoQuan=c.MaCoQuan);
-- Existing credentials remain valid, but need a registered PNG before signing in v2.1.
-- 3 active personal credentials is enforced atomically by the API transaction;
-- agency has one active credential, registered by the chief agency leader.
