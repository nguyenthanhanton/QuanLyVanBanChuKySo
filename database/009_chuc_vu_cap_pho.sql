/* v0.5.7: Bo sung vai tro pho phong / pho van phong / pho lanh dao.
   Khong tao tai khoan pho mac dinh, khong tu dong cap quyen ky cho nguoi bat ky.
   Quan tri co quan phai gan vai tro cho dung nguoi; nguoi soan chon dich danh theo van ban.
   Chay sau 008. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=8)
    THROW 51009,N'Can chay migration 008 truoc 009.',1;

INSERT dbo.VaiTro(MaDinhDanh,TenVaiTro,LaVaiTroHeThong)
SELECT x.MaDinhDanh,x.TenVaiTro,0 FROM (VALUES
    ('PHO_PHONG',N'Phó trưởng phòng'),
    ('PHO_VAN_PHONG',N'Phó trưởng văn phòng'),
    ('PHO_LANH_DAO',N'Phó lãnh đạo cơ quan')
) AS x(MaDinhDanh,TenVaiTro)
WHERE NOT EXISTS(SELECT 1 FROM dbo.VaiTro v WHERE v.MaDinhDanh=x.MaDinhDanh);

-- Quyen xu ly va ky ca nhan giong vai tro truong tuong ung, theo tung co quan.
INSERT dbo.VaiTroQuyen(MaCoQuan,MaVaiTro,MaQuyen)
SELECT oldPerm.MaCoQuan,deputy.MaVaiTro,oldPerm.MaQuyen
FROM dbo.VaiTroQuyen oldPerm
JOIN dbo.VaiTro chief ON chief.MaVaiTro=oldPerm.MaVaiTro
JOIN (VALUES ('TRUONG_PHONG','PHO_PHONG'),
             ('TRUONG_VAN_PHONG','PHO_VAN_PHONG'),
             ('LANH_DAO','PHO_LANH_DAO')) AS pairs(Chief,Deputy)
  ON pairs.Chief=chief.MaDinhDanh
JOIN dbo.VaiTro deputy ON deputy.MaDinhDanh=pairs.Deputy
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.VaiTroQuyen currentPerm
    WHERE currentPerm.MaVaiTro=deputy.MaVaiTro
      AND currentPerm.MaQuyen=oldPerm.MaQuyen
      AND (currentPerm.MaCoQuan=oldPerm.MaCoQuan
           OR (currentPerm.MaCoQuan IS NULL AND oldPerm.MaCoQuan IS NULL))
);

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=9)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(9,N'Vai tro pho phong, pho van phong va pho lanh dao ky thay theo van ban');

COMMIT TRANSACTION;
GO
