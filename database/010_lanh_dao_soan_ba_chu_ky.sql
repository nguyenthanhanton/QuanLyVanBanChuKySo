/* v0.5.8 — lanh dao soan: ky nguoi soan -> lanh dao khac -> van thu co quan.
   Ho so da trinh truoc day van giu SoBuocKy 4/5.
   Vai tro LANH_DAO va PHO_LANH_DAO duoc them quyen soan/trinh van ban.
   Chay sau migration 009; idempotent. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=9)
    THROW 51010,N'Can chay migration 009 truoc 010.',1;

-- Khong chuyen doi van ban cu; chi mo rong gia tri cho phep.
IF EXISTS(SELECT 1 FROM sys.check_constraints
          WHERE parent_object_id=OBJECT_ID('dbo.VanBan') AND name='CK_VanBan_SoBuocKy')
    ALTER TABLE dbo.VanBan DROP CONSTRAINT CK_VanBan_SoBuocKy;

EXEC sys.sp_executesql N'ALTER TABLE dbo.VanBan WITH CHECK ADD CONSTRAINT CK_VanBan_SoBuocKy CHECK(SoBuocKy IN (3,4,5))';

INSERT dbo.VaiTroQuyen(MaCoQuan,MaVaiTro,MaQuyen)
SELECT c.MaCoQuan,v.MaVaiTro,q.MaQuyen
FROM dbo.CoQuan c CROSS JOIN dbo.VaiTro v CROSS JOIN dbo.Quyen q
WHERE v.MaDinhDanh IN ('LANH_DAO','PHO_LANH_DAO')
  AND q.MaDinhDanh IN ('documents.manage','documents.submit')
  AND NOT EXISTS (SELECT 1 FROM dbo.VaiTroQuyen existing
      WHERE existing.MaCoQuan=c.MaCoQuan AND existing.MaVaiTro=v.MaVaiTro
        AND existing.MaQuyen=q.MaQuyen);

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=10)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(10,N'Lanh dao soan van ban va trinh lanh dao khac ky truc tiep');

COMMIT TRANSACTION;
GO
