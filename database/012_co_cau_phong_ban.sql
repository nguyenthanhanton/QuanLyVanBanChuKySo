/* v0.6.3 — Thứ tự và quan hệ trực thuộc giữa các phòng ban.
   Chạy sau 011; giữ nguyên mã phòng, tài khoản, văn bản và lịch sử. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=11)
    THROW 51012,N'Can chay script 011 truoc 012.',1;

IF COL_LENGTH('dbo.PhongBan','ThuTuHienThi') IS NULL
    ALTER TABLE dbo.PhongBan ADD ThuTuHienThi INT NOT NULL
        CONSTRAINT DF_PhongBan_ThuTuHienThi DEFAULT(100);

IF COL_LENGTH('dbo.PhongBan','MaPhongBanCha') IS NULL
    ALTER TABLE dbo.PhongBan ADD MaPhongBanCha INT NULL;

-- Chỉ phòng cùng cơ quan được làm phòng cấp trên.
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_PhongBan_ChaCungCoQuan')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.PhongBan WITH CHECK ADD CONSTRAINT FK_PhongBan_ChaCungCoQuan
      FOREIGN KEY(MaCoQuan,MaPhongBanCha) REFERENCES dbo.PhongBan(MaCoQuan,MaPhongBan)';

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_PhongBan_KhongTuTrucThuoc')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.PhongBan ADD CONSTRAINT CK_PhongBan_KhongTuTrucThuoc
      CHECK(MaPhongBanCha IS NULL OR MaPhongBanCha<>MaPhongBan)';

EXEC sys.sp_executesql N'UPDATE dbo.PhongBan SET ThuTuHienThi=CASE MaDinhDanh
    WHEN ''LANH_DAO'' THEN 10 WHEN ''VAN_PHONG'' THEN 20 WHEN ''VAN_THU'' THEN 90 ELSE 50 END
WHERE ThuTuHienThi=100';

IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=12)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(12,N'Quan ly phong ban, thu tu hien thi va co cau truc thuoc');

COMMIT TRANSACTION;
GO
