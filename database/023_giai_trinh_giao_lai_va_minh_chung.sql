USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=22)
    THROW 51023,N'Can chay 022 truoc 023.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.LichSuGiaoLaiCaNhanVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.LichSuGiaoLaiCaNhanVanBanDen (
        MaLichSu BIGINT IDENTITY(1,1) PRIMARY KEY,
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.VanBanDen(MaVanBanDen),
        MaPhongBan INT NOT NULL REFERENCES dbo.PhongBan(MaPhongBan),
        MaNguoiChuTriCu INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaNguoiChuTriMoi INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaNguoiThayDoi INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        LyDo NVARCHAR(1000) NOT NULL,
        ThoiDiem DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.MinhChungBaoCaoVanBanDen','U') IS NULL
BEGIN
    CREATE TABLE dbo.MinhChungBaoCaoVanBanDen (
        MaMinhChung UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        MaVanBanDen UNIQUEIDENTIFIER NOT NULL,
        MaPhongBan INT NOT NULL,
        MaNguoiTaiLen INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        TenTep NVARCHAR(255) NOT NULL,
        LoaiNoiDung VARCHAR(40) NOT NULL,
        KichThuoc BIGINT NOT NULL,
        DuongDanLuuTru NVARCHAR(1000) NOT NULL,
        MaBamSha256 CHAR(64) NOT NULL,
        ThoiDiemTaiLen DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_MinhChung_BaoCao FOREIGN KEY(MaVanBanDen,MaPhongBan)
            REFERENCES dbo.KetQuaPhongVanBanDen(MaVanBanDen,MaPhongBan),
        CONSTRAINT CK_MinhChung_KichThuoc CHECK(KichThuoc BETWEEN 1 AND 5242880)
    );
    CREATE INDEX IX_MinhChung_VanBan ON dbo.MinhChungBaoCaoVanBanDen(MaVanBanDen,MaPhongBan);
END;
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=23)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(23,N'Giai trinh giao lai va anh minh chung bao cao van ban den');
COMMIT TRANSACTION;
GO
