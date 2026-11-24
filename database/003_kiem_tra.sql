/* TRUY VAN CHI DOC: doi chieu nhanh database sau khi apply + seed. */
USE QuanLyVanBanChuKySo;
GO
SELECT DB_NAME() AS TenCoSoDuLieu;
SELECT COUNT(*) AS SoCoQuan FROM dbo.CoQuan;
SELECT COUNT(*) AS SoPhongBan FROM dbo.PhongBan;
SELECT COUNT(*) AS SoNguoiDung FROM dbo.NguoiDung;
SELECT COUNT(*) AS SoVanBan FROM dbo.VanBan;
SELECT COUNT(*) AS SoLuotChuyen FROM dbo.LuotChuyenVanBan;
SELECT COUNT(*) AS SoSuKienHanhTrinh FROM dbo.LichSuVanBan;
SELECT COUNT(*) AS SoChungThu FROM dbo.ChungThuKy;
SELECT COUNT(*) AS SoBanPdfDaKy FROM dbo.BanPdfDaKy;
SELECT SoPhienBan,TenPhienBan,ThoiDiemApDung FROM dbo.PhienBanLuocDo ORDER BY SoPhienBan;

-- Du kien bang 0: khong co van ban phat hanh nao thieu buoc ky thu 3.
SELECT COUNT(*) AS SoBanPhatHanhThieuDuBaChuKy
FROM dbo.BanPhatHanh p
WHERE NOT EXISTS(
    SELECT 1 FROM dbo.BanPdfDaKy s
    WHERE s.MaVanBan=p.MaVanBan AND s.SoPhienBan=p.SoPhienBan
      AND s.ThuTuKy=3 AND s.MaBamSha256=p.MaBamBanCuoi);

-- Ket qua PHAI la 0: Quan tri he thong khong duoc nhan quyen doc van ban.
SELECT COUNT(*) AS ViPhamQuyenQuanTri FROM dbo.NguoiDungVaiTro uv
JOIN dbo.NguoiDung n ON n.MaNguoiDung=uv.MaNguoiDung
JOIN dbo.VaiTroQuyen vq ON vq.MaVaiTro=uv.MaVaiTro AND
    (vq.MaCoQuan=uv.MaCoQuan OR (vq.MaCoQuan IS NULL AND uv.MaCoQuan IS NULL))
JOIN dbo.Quyen q ON q.MaQuyen=vq.MaQuyen
WHERE n.TenDangNhap='quantrihethong' AND q.MaDinhDanh='documents.read';

-- Xac nhan ten cot/tieu de ho tro Unicode.
SELECT c.MaDinhDanh,c.TenCoQuan,COUNT(n.MaNguoiDung) AS SoTaiKhoan
FROM dbo.CoQuan c LEFT JOIN dbo.NguoiDung n ON n.MaCoQuan=c.MaCoQuan
GROUP BY c.MaDinhDanh,c.TenCoQuan ORDER BY c.MaDinhDanh;
GO
