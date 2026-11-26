/* v0.6.2: Thong nhat truong/pho phong; ten goi chuc vu rieng luu o NguoiDung.ChucVu.
   Khong xoa vai tro cu de giu lich su. Chay sau 010; co the chay lai an toan. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=10)
    THROW 51011,N'Can chay script 010 truoc 011.',1;

-- Quyen cu cua truong/pho van phong duoc hop nhat vao truong/pho phong.
INSERT dbo.VaiTroQuyen(MaCoQuan,MaVaiTro,MaQuyen)
SELECT oldPerm.MaCoQuan,newRole.MaVaiTro,oldPerm.MaQuyen
FROM dbo.VaiTroQuyen oldPerm
JOIN dbo.VaiTro oldRole ON oldRole.MaVaiTro=oldPerm.MaVaiTro
JOIN (VALUES ('TRUONG_VAN_PHONG','TRUONG_PHONG'),('PHO_VAN_PHONG','PHO_PHONG')) AS changeRole(Cu,Moi)
    ON changeRole.Cu=oldRole.MaDinhDanh
JOIN dbo.VaiTro newRole ON newRole.MaDinhDanh=changeRole.Moi
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.VaiTroQuyen currentPerm
    WHERE currentPerm.MaVaiTro=newRole.MaVaiTro AND currentPerm.MaQuyen=oldPerm.MaQuyen
      AND (currentPerm.MaCoQuan=oldPerm.MaCoQuan OR
           (currentPerm.MaCoQuan IS NULL AND oldPerm.MaCoQuan IS NULL))
);

-- Doi ma vai tro cho tai khoan cu; giu nguyen ChucVu (VD: Truong van phong).
UPDATE uv SET uv.MaVaiTro=newRole.MaVaiTro
FROM dbo.NguoiDungVaiTro uv
JOIN dbo.VaiTro oldRole ON oldRole.MaVaiTro=uv.MaVaiTro
JOIN (VALUES ('TRUONG_VAN_PHONG','TRUONG_PHONG'),('PHO_VAN_PHONG','PHO_PHONG')) AS changeRole(Cu,Moi)
    ON changeRole.Cu=oldRole.MaDinhDanh
JOIN dbo.VaiTro newRole ON newRole.MaDinhDanh=changeRole.Moi
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.NguoiDungVaiTro currentRole
    WHERE currentRole.MaNguoiDung=uv.MaNguoiDung AND currentRole.MaCoQuan=uv.MaCoQuan
      AND currentRole.MaVaiTro=newRole.MaVaiTro
);

-- Neu da co ca hai vai tro, bo ban gan cu trung lap.
DELETE uv FROM dbo.NguoiDungVaiTro uv
JOIN dbo.VaiTro oldRole ON oldRole.MaVaiTro=uv.MaVaiTro
WHERE oldRole.MaDinhDanh IN ('TRUONG_VAN_PHONG','PHO_VAN_PHONG');

IF NOT EXISTS (SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=11)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(11,N'Thong nhat chuc vu theo phong ban va ten goi chuc vu tuy chinh');

COMMIT TRANSACTION;
GO
