/* DU LIEU MAU CHI DANH CHO LOCAL DEV / TEST, TUYET DOI KHONG CHAY PRODUCTION. */
USE QuanLyVanBanChuKySo;
GO
IF EXISTS(SELECT 1 FROM dbo.NguoiDung)
    THROW 51001, N'Da co tai khoan; tu choi seed de tranh ghi de du lieu.', 1;
GO
BEGIN TRANSACTION;

INSERT dbo.CoQuan(MaDinhDanh,TenCoQuan,DiaChi) VALUES
 ('CQ_A',N'Cơ quan A – Khối Tham mưu',N'Hà Nội'),
 ('CQ_B',N'Cơ quan B – Khối Hành chính',N'Hà Nội'),
 ('CQ_C',N'Cơ quan C – Khối Kỹ thuật',N'Bắc Ninh');

INSERT dbo.PhongBan(MaCoQuan,MaDinhDanh,TenPhongBan)
SELECT c.MaCoQuan,x.MaPhong,x.TenPhong FROM dbo.CoQuan c CROSS JOIN (VALUES
 ('VAN_THU',N'Văn thư'),('THAM_MUU',N'Phòng Tham mưu'),
 ('VAN_PHONG',N'Văn phòng'),('LANH_DAO',N'Ban lãnh đạo')
) x(MaPhong,TenPhong);

INSERT dbo.VaiTro(MaDinhDanh,TenVaiTro,LaVaiTroHeThong) VALUES
 ('SYSTEM_ADMIN',N'Quản trị toàn hệ thống',1),
 ('AGENCY_ADMIN',N'Quản trị cơ quan',0),
 ('VAN_THU',N'Văn thư',0),
 ('TRUONG_PHONG',N'Trưởng phòng',0),
 ('TRUONG_VAN_PHONG',N'Trưởng văn phòng',0),
 ('LANH_DAO',N'Lãnh đạo',0),
 ('CHUYEN_VIEN',N'Chuyên viên',0);

INSERT dbo.Quyen(MaDinhDanh,TenQuyen) VALUES
 ('documents.read',N'Đọc văn bản'),
 ('documents.manage',N'Soạn và sửa văn bản'),
 ('documents.submit',N'Trình duyệt văn bản'),
 ('workflow.decide',N'Phê duyệt/trả sửa'),
 ('workflow.queue',N'Công việc cần xử lý'),
 ('registers.manage',N'Quản lý sổ và cấp số'),
 ('signatures.manage-own',N'Quản lý chữ ký cá nhân'),
 ('signatures.manage-agency',N'Quản lý chữ ký cơ quan'),
 ('signing.prepare',N'Chuẩn bị PDF'),
 ('signing.sign',N'Ký số'),
 ('publications.issue',N'Phát hành'),
 ('publications.deliver',N'Giao nhận bản phát hành'),
 ('deliveries.accept',N'Tiếp nhận bản phát hành'),
 ('incoming.manage',N'Quản lý văn bản đến'),
 ('dossiers.manage',N'Quản lý hồ sơ'),
 ('dossiers.archive',N'Lưu trữ hồ sơ'),
 ('reports.read',N'Báo cáo'),
 ('audit.business.read',N'Nhật ký nghiệp vụ'),
 ('audit.technical.read',N'Nhật ký kỹ thuật'),
 ('users.manage',N'Quản lý tài khoản trong cơ quan'),
 ('agency.admins.manage',N'Quản trị tài khoản toàn hệ thống'),
 ('departments.manage',N'Quản lý phòng ban'),
 ('agencies.manage',N'Quản lý cơ quan'),
 ('settings.manage',N'Cấu hình cơ quan');

-- Quyen he thong KHONG co documents.read hoac quyen nghiep vu.
INSERT dbo.VaiTroQuyen(MaVaiTro,MaQuyen,MaCoQuan)
SELECT v.MaVaiTro,q.MaQuyen,NULL FROM dbo.VaiTro v CROSS JOIN dbo.Quyen q
WHERE v.MaDinhDanh='SYSTEM_ADMIN'
  AND q.MaDinhDanh IN ('agency.admins.manage','agencies.manage','audit.technical.read');

-- Moi co quan co bo cap quyen rieng. Khong tu gan quyen nghiep vu cho quan tri.
INSERT dbo.VaiTroQuyen(MaVaiTro,MaQuyen,MaCoQuan)
SELECT v.MaVaiTro,q.MaQuyen,c.MaCoQuan
FROM dbo.CoQuan c CROSS JOIN dbo.VaiTro v CROSS JOIN dbo.Quyen q
WHERE
 (v.MaDinhDanh='AGENCY_ADMIN' AND q.MaDinhDanh IN ('users.manage','departments.manage','settings.manage'))
 OR (v.MaDinhDanh='VAN_THU' AND q.MaDinhDanh IN
     ('documents.read','documents.manage','documents.submit','workflow.queue','registers.manage',
      'signatures.manage-agency','signing.prepare','signing.sign','publications.issue',
      'publications.deliver','deliveries.accept','incoming.manage','dossiers.manage',
      'dossiers.archive','reports.read'))
 OR (v.MaDinhDanh IN ('TRUONG_PHONG','TRUONG_VAN_PHONG','LANH_DAO') AND q.MaDinhDanh IN
     ('documents.read','workflow.queue','workflow.decide','signing.sign','signatures.manage-own'))
 OR (v.MaDinhDanh='CHUYEN_VIEN' AND q.MaDinhDanh IN
     ('documents.read','documents.manage','documents.submit','workflow.queue','dossiers.manage'));

-- Mat khau Password123! duoc BAM PBKDF2; seed CHI DEV, doi ngay neu dung mang that.
DECLARE @Bam VARCHAR(300)='pbkdf2-sha256$210000$/pX+2e+uTkwqjqzwRHnmlQ==$0Qdh0J1ZOw42fHABUuLIBUZKdfq5aJbB/jGAgxKWYF0=';
INSERT dbo.NguoiDung(MaCoQuan,MaPhongBan,TenDangNhap,MatKhauBam,HoTen,ChucVu)
VALUES (NULL,NULL,'quantrihethong',@Bam,N'Quản trị hệ thống',N'Quản trị kỹ thuật');

INSERT dbo.NguoiDung(MaCoQuan,MaPhongBan,TenDangNhap,MatKhauBam,HoTen,ChucVu)
SELECT c.MaCoQuan,p.MaPhongBan,x.TenDangNhap,@Bam,x.HoTen,x.ChucVu
FROM (VALUES
 ('CQ_A','VAN_PHONG','quantri_a',N'Quản trị cơ quan A',N'Quản trị'),
 ('CQ_A','VAN_THU','vanthu_a',N'Nguyễn Thị Minh',N'Văn thư'),
 ('CQ_A','THAM_MUU','truongphong_a',N'Trần Văn Hùng',N'Trưởng phòng'),
 ('CQ_A','VAN_PHONG','truongvanphong_a',N'Lê Thu Hà',N'Trưởng văn phòng'),
 ('CQ_A','LANH_DAO','lanhdao_a',N'Phạm Văn Nam',N'Lãnh đạo cơ quan'),
 ('CQ_A','THAM_MUU','chuyenvien_a',N'Đỗ Đức Anh',N'Chuyên viên'),
 ('CQ_B','VAN_PHONG','quantri_b',N'Quản trị cơ quan B',N'Quản trị'),
 ('CQ_B','VAN_THU','vanthu_b',N'Ngô Hải Yến',N'Văn thư'),
 ('CQ_B','LANH_DAO','lanhdao_b',N'Hoàng Quốc Bình',N'Lãnh đạo cơ quan'),
 ('CQ_B','THAM_MUU','chuyenvien_b',N'Vũ Thị Hương',N'Chuyên viên'),
 ('CQ_C','VAN_THU','vanthu_c',N'Phan Thanh Vân',N'Văn thư'),
 ('CQ_C','VAN_THU','vanthu2_c',N'Bùi Quang Khải',N'Văn thư'),
 ('CQ_C','LANH_DAO','lanhdao_c',N'Tạ Thị Lan',N'Lãnh đạo cơ quan'),
 ('CQ_C','THAM_MUU','chuyenvien_c',N'Đinh Hoàng Sơn',N'Chuyên viên')
) x(MaCoQuan,MaPhong,TenDangNhap,HoTen,ChucVu)
JOIN dbo.CoQuan c ON c.MaDinhDanh=x.MaCoQuan
JOIN dbo.PhongBan p ON p.MaCoQuan=c.MaCoQuan AND p.MaDinhDanh=x.MaPhong;

INSERT dbo.NguoiDungVaiTro(MaNguoiDung,MaVaiTro,MaCoQuan)
SELECT n.MaNguoiDung,v.MaVaiTro,n.MaCoQuan
FROM dbo.NguoiDung n JOIN dbo.VaiTro v ON v.MaDinhDanh=
 CASE
  WHEN n.TenDangNhap='quantrihethong' THEN 'SYSTEM_ADMIN'
  WHEN n.TenDangNhap LIKE 'quantri[_]%' THEN 'AGENCY_ADMIN'
  WHEN n.TenDangNhap LIKE 'vanthu%' THEN 'VAN_THU'
  WHEN n.TenDangNhap LIKE 'truongvanphong%' THEN 'TRUONG_VAN_PHONG'
  WHEN n.TenDangNhap LIKE 'truongphong%' THEN 'TRUONG_PHONG'
  WHEN n.TenDangNhap LIKE 'lanhdao%' THEN 'LANH_DAO'
  ELSE 'CHUYEN_VIEN'
 END;

INSERT dbo.CauHinhQuyTrinh(MaCoQuan,SoPhienBan,CoKiemTraVanPhong)
SELECT MaCoQuan,1,CASE WHEN MaDinhDanh='CQ_A' THEN 1 ELSE 0 END FROM dbo.CoQuan;

COMMIT TRANSACTION;
GO
