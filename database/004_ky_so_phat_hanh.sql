/*
  Migration 004: Chung thu ky va cac lan PDF ky noi tiep.
  Them moi, KHONG xoa hay ghi de bang da co. Chay sau 001 (va 002 neu demo).
*/
USE QuanLyVanBanChuKySo;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=4)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    CREATE TABLE dbo.ChungThuKy (
        MaChungThu UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        MaCoQuan INT NOT NULL REFERENCES dbo.CoQuan(MaCoQuan),
        MaNguoiSoHuu INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        LoaiChungThu VARCHAR(20) NOT NULL,
        NguonChungThu VARCHAR(30) NOT NULL,
        TenChungThu NVARCHAR(180) NOT NULL,
        SoSerial VARCHAR(100) NOT NULL,
        ChungThuCongKhai VARBINARY(MAX) NOT NULL,
        DuongDanKhoaMaHoa NVARCHAR(500) NOT NULL,
        MaKhoaMaHoa VARCHAR(50) NOT NULL,
        DangHoatDong BIT NOT NULL DEFAULT 1,
        NgayHieuLuc DATETIME2(3) NOT NULL,
        NgayHetHan DATETIME2(3) NOT NULL,
        ThoiDiemTao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_ChungThuKy_Loai CHECK(LoaiChungThu IN ('CA_NHAN','CO_QUAN')),
        CONSTRAINT CK_ChungThuKy_Nguon CHECK(NguonChungThu IN ('TU_KY_THU_NGHIEM','NHAP_PFX')),
        CONSTRAINT CK_ChungThuKy_SoHuu CHECK(
            (LoaiChungThu='CA_NHAN' AND MaNguoiSoHuu IS NOT NULL)
            OR (LoaiChungThu='CO_QUAN' AND MaNguoiSoHuu IS NULL)),
        CONSTRAINT UQ_ChungThuKy_Serial UNIQUE(SoSerial)
    );

    CREATE UNIQUE INDEX UX_ChungThuKy_DangSuDung
      ON dbo.ChungThuKy(MaCoQuan,LoaiChungThu,MaNguoiSoHuu)
      WHERE DangHoatDong=1;

    CREATE TABLE dbo.BanPdfDaKy (
        MaBan UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        MaVanBan UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.VanBan(MaVanBan),
        MaCoQuan INT NOT NULL REFERENCES dbo.CoQuan(MaCoQuan),
        SoPhienBan INT NOT NULL,
        ThuTuKy INT NOT NULL,
        MaBanTruoc UNIQUEIDENTIFIER NULL REFERENCES dbo.BanPdfDaKy(MaBan),
        DuongDanLuuTru NVARCHAR(500) NOT NULL,
        MaBamSha256 CHAR(64) NOT NULL,
        TenTruongKy VARCHAR(100) NULL,
        ThoiDiemTao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_BanPdfDaKy UNIQUE(MaVanBan,SoPhienBan,ThuTuKy),
        CONSTRAINT CK_BanPdfDaKy_ThuTu CHECK(ThuTuKy BETWEEN 0 AND 3)
    );

    ALTER TABLE dbo.ChuKySo ADD
        MaBanDaKy UNIQUEIDENTIFIER NULL REFERENCES dbo.BanPdfDaKy(MaBan),
        MaChungThu UNIQUEIDENTIFIER NULL REFERENCES dbo.ChungThuKy(MaChungThu),
        TenTruongKy VARCHAR(100) NULL;

    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(4,N'Ký PDF tuần tự, chứng thư tự ký và bản PDF đầu ra');

    COMMIT TRANSACTION;
END;
GO
