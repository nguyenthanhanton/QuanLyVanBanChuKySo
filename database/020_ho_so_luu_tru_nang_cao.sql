/* 0.7 consolidated: retention review and controlled archive access. No deletion. */
USE QuanLyVanBanChuKySo;
GO
SET XACT_ABORT ON;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=19)
    THROW 51022,N'Can chay 019 truoc 020.',1;
GO
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.DeXuatBaoQuanHoSo','U') IS NULL
BEGIN
    CREATE TABLE dbo.DeXuatBaoQuanHoSo (
        MaDeXuat BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MaHoSo UNIQUEIDENTIFIER NOT NULL,
        MaCoQuan INT NOT NULL,
        MaNguoiDeXuat INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        SoNamDeXuat INT NULL,
        LyDo NVARCHAR(1000) NOT NULL,
        TrangThai VARCHAR(20) NOT NULL DEFAULT 'CHO_DUYET',
        MaNguoiDuyet INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemTao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ThoiDiemDuyet DATETIME2(3) NULL,
        GhiChuDuyet NVARCHAR(1000) NULL,
        CONSTRAINT FK_DeXuatBaoQuan_HoSo FOREIGN KEY(MaHoSo,MaCoQuan)
            REFERENCES dbo.HoSoLuuTru(MaHoSo,MaCoQuan),
        CONSTRAINT CK_DeXuatBaoQuan_ThoiHan CHECK(SoNamDeXuat IS NULL OR SoNamDeXuat BETWEEN 1 AND 200),
        CONSTRAINT CK_DeXuatBaoQuan_TrangThai CHECK(TrangThai IN ('CHO_DUYET','DA_DUYET','TU_CHOI'))
    );
    CREATE UNIQUE INDEX UX_DeXuatBaoQuan_Cho ON dbo.DeXuatBaoQuanHoSo(MaHoSo)
        WHERE TrangThai='CHO_DUYET';
END;
IF OBJECT_ID('dbo.YeuCauKhaiThacHoSo','U') IS NULL
BEGIN
    CREATE TABLE dbo.YeuCauKhaiThacHoSo (
        MaYeuCau BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MaHoSo UNIQUEIDENTIFIER NOT NULL,
        MaCoQuan INT NOT NULL,
        MaNguoiYeuCau INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        LyDo NVARCHAR(1000) NOT NULL,
        TrangThai VARCHAR(20) NOT NULL DEFAULT 'CHO_DUYET',
        MaNguoiDuyet INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        ThoiDiemTao DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ThoiDiemDuyet DATETIME2(3) NULL,
        HieuLucDen DATETIME2(3) NULL,
        GhiChuDuyet NVARCHAR(1000) NULL,
        CONSTRAINT FK_YeuCauKhaiThac_HoSo FOREIGN KEY(MaHoSo,MaCoQuan)
            REFERENCES dbo.HoSoLuuTru(MaHoSo,MaCoQuan),
        CONSTRAINT CK_YeuCauKhaiThac_TrangThai CHECK(TrangThai IN ('CHO_DUYET','DA_DUYET','TU_CHOI','THU_HOI'))
    );
    CREATE INDEX IX_YeuCauKhaiThac_CoQuan ON dbo.YeuCauKhaiThacHoSo(MaCoQuan,TrangThai,ThoiDiemTao DESC);
END;
IF OBJECT_ID('dbo.NhatKyKhaiThacHoSo','U') IS NULL
BEGIN
    CREATE TABLE dbo.NhatKyKhaiThacHoSo (
        MaNhatKy BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MaYeuCau BIGINT NOT NULL REFERENCES dbo.YeuCauKhaiThacHoSo(MaYeuCau),
        MaCoQuan INT NOT NULL,
        MaNguoiDung INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
        MaVanBan UNIQUEIDENTIFIER NULL REFERENCES dbo.VanBan(MaVanBan),
        HanhDong VARCHAR(30) NOT NULL,
        ThoiDiem DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_NhatKyKhaiThac_HoSo ON dbo.NhatKyKhaiThacHoSo(MaYeuCau,ThoiDiem DESC);
END;
IF NOT EXISTS(SELECT 1 FROM dbo.PhienBanLuocDo WHERE SoPhienBan=20)
    INSERT dbo.PhienBanLuocDo(SoPhienBan,TenPhienBan)
    VALUES(20,N'Tra cuu, thoi han bao quan, khai thac va kiem toan ho so');
COMMIT TRANSACTION;
GO
