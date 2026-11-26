/* v0.6.4.1 — Ghi đè quyền nghiệp vụ theo tài khoản, không đổi quy trình ký. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=13)
    THROW 51016,N'Can chay script 013 truoc 014.',1;

IF NOT EXISTS(SELECT 1 FROM dbo.Quyen WHERE MaDinhDanh='permissions.manage')
    INSERT dbo.Quyen(MaDinhDanh,TenQuyen)
    VALUES('permissions.manage',N'Phân quyền nghiệp vụ cho tài khoản trong cơ quan');

INSERT dbo.VaiTroQuyen(MaCoQuan,MaVaiTro,MaQuyen)
SELECT c.MaCoQuan,r.MaVaiTro,q.MaQuyen
FROM dbo.CoQuan c CROSS JOIN dbo.VaiTro r CROSS JOIN dbo.Quyen q
WHERE r.MaDinhDanh='AGENCY_ADMIN' AND q.MaDinhDanh='permissions.manage'
  AND NOT EXISTS(SELECT 1 FROM dbo.VaiTroQuyen existing
    WHERE existing.MaCoQuan=c.MaCoQuan AND existing.MaVaiTro=r.MaVaiTro AND existing.MaQuyen=q.MaQuyen);

IF OBJECT_ID('dbo.PhanQuyenNguoiDung','U') IS NULL
    CREATE TABLE dbo.PhanQuyenNguoiDung(
        MaCoQuan INT NOT NULL,
        MaNguoiDung INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaQuyen INT NOT NULL REFERENCES dbo.Quyen(MaQuyen),
        ChoPhep BIT NOT NULL,
        MaNguoiCap INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemCap DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PhanQuyenNguoiDung PRIMARY KEY(MaCoQuan,MaNguoiDung,MaQuyen),
        CONSTRAINT FK_PhanQuyenNguoiDung_CoQuan FOREIGN KEY(MaCoQuan) REFERENCES dbo.CoQuan(MaCoQuan)
    );

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=14)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(14,N'Phan quyen nghiep vu rieng cho tung tai khoan');

COMMIT TRANSACTION;
GO
