# v0.5.7 — Trưởng/phó phòng, lãnh đạo cấp trưởng/phó

## Nâng cấp

1. Sao lưu SQL Server; chạy `database/009_chuc_vu_cap_pho.sql` **sau migration 008**. Script thêm 3 vai trò: `PHO_PHONG`, `PHO_VAN_PHONG`, `PHO_LANH_DAO` và phân quyền ký/phê duyệt tương ứng theo cơ quan.
2. Migration **không tự tạo người dùng hoặc tự cấp vai trò cho tài khoản**; quản trị cơ quan phải xác minh người có thẩm quyền trước khi gán. Không sửa trực tiếp vai trò của người khác nếu chưa được phê duyệt.
3. Chạy lại backend/frontend sau khi cập nhật GitHub.

## Kiểm tra vai trò đã có

```sql
USE QuanLyVanBanChuKySo;
SELECT MaDinhDanh,TenVaiTro FROM dbo.VaiTro
WHERE MaDinhDanh IN ('TRUONG_PHONG','PHO_PHONG','TRUONG_VAN_PHONG',
    'PHO_VAN_PHONG','LANH_DAO','PHO_LANH_DAO');
SELECT SoPhienBan,TenPhienBan FROM dbo.PhienBanLuocDo WHERE SoPhienBan=9;
```

## Gán vai trò (chỉ sau khi người có thẩm quyền xác nhận)

Ví dụ nếu đã có tài khoản `phophong_a` thuộc **đúng phòng** và muốn ủy quyền ký nháy, thay thông tin theo người thật trong dữ liệu:

```sql
DECLARE @TenDangNhap NVARCHAR(100) = N'phophong_a';
DECLARE @MaVaiTro VARCHAR(80) = 'PHO_PHONG';
SELECT n.MaNguoiDung,n.TenDangNhap,n.HoTen,n.MaCoQuan,p.TenPhongBan,
       v.MaVaiTro,v.MaDinhDanh
FROM dbo.NguoiDung n JOIN dbo.PhongBan p ON p.MaPhongBan=n.MaPhongBan
CROSS JOIN dbo.VaiTro v
WHERE n.TenDangNhap=@TenDangNhap AND v.MaDinhDanh=@MaVaiTro;
```

Sau khi quản trị viên kiểm tra đúng danh tính, cơ quan và phòng ban, có thể gán vai trò bằng thao tác có kiểm soát:

```sql
DECLARE @TenDangNhap NVARCHAR(100) = N'phophong_a';
DECLARE @MaVaiTro VARCHAR(80) = 'PHO_PHONG';
INSERT dbo.NguoiDungVaiTro(MaNguoiDung,MaVaiTro,MaCoQuan)
SELECT n.MaNguoiDung,v.MaVaiTro,n.MaCoQuan
FROM dbo.NguoiDung n CROSS JOIN dbo.VaiTro v
WHERE n.TenDangNhap=@TenDangNhap AND v.MaDinhDanh=@MaVaiTro
  AND n.MaCoQuan IS NOT NULL AND n.DangHoatDong=1
  AND NOT EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro nv
     WHERE nv.MaNguoiDung=n.MaNguoiDung AND nv.MaVaiTro=v.MaVaiTro
       AND nv.MaCoQuan=n.MaCoQuan);
```

*Lưu ý:* Script mẫu trên không tạo người dùng. Để chọn phó lãnh đạo, tài khoản phải được gán `PHO_LANH_DAO` của đúng cơ quan; phó văn phòng dùng `PHO_VAN_PHONG`.

## Kiểm thử

1. Người soạn phòng Tham mưu: danh sách ký nháy chỉ có `TRUONG_PHONG` và `PHO_PHONG` trong phòng Tham mưu.
2. Người soạn thuộc Văn thư/Văn phòng: danh sách chỉ có `TRUONG_VAN_PHONG`, `PHO_VAN_PHONG` trong **cùng cơ quan**.
3. Lãnh đạo ký chính thức: có cấp trưởng `LANH_DAO` và các cấp phó `PHO_LANH_DAO` cùng cơ quan.
4. Chọn cấp phó; người khác không được ký thay cấp phó đã chọn. Trưởng hoặc phó chưa được gán vai trò không xuất hiện trong danh sách.
5. Mọi chữ ký vẫn vào **cùng PDF** và quy trình 4 vị trí không thêm bước ký riêng cho cấp phó.

**Phạm vi hiện tại:** vai trò cấp trưởng/cấp phó được quản trị qua dữ liệu vai trò, chưa có màn hình quản lý bổ nhiệm/ủy quyền có thời hạn; quy tắc một lãnh đạo trưởng duy nhất mỗi cơ quan cần được quản trị và kiểm tra khi gán vai trò. Chỉ dùng thử nghiệm, chưa khẳng định chứng thư tự ký có hiệu lực pháp lý.
