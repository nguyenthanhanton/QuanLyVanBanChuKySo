# Nghiệm thu 0.6.4.1 — Phân quyền theo tài khoản

## Cập nhật

1. Sao lưu database `QuanLyVanBanChuKySo`.
2. Kéo nhánh GitHub `feat/v0.5-ky-so-nam-cap`.
3. Trong SSMS chạy `database/014_phan_quyen_nguoi_dung.sql` sau script `013`.
4. Build backend (`dotnet build`) và chạy lại API bằng `start-backend.ps1` từ thư mục dự án.
5. Chạy frontend `npm run dev`; đăng xuất/đăng nhập lại tài khoản quản trị cơ quan để tải quyền `permissions.manage` mới.

## Ý nghĩa ba lựa chọn

- **Theo chức vụ:** sử dụng quyền của vai trò trong bảng `VaiTroQuyen`.
- **Cho phép:** cấp thêm quyền riêng cho tài khoản đang chỉnh.
- **Từ chối:** chặn quyền riêng dù vai trò đang có quyền đó.
- Không thay đổi mã chức vụ, phòng ban, người ký được chỉ định, tài liệu hay lịch sử chữ ký.

## Kiểm thử

1. Vào **Quản trị tài khoản**, mở **Phân quyền** của một chuyên viên đang hoạt động.
2. Quyền `reports.read` mặc định (nếu không có) phải hiển thị `Theo chức vụ`.
3. Đổi `reports.read` sang **Cho phép**; đăng nhập tài khoản chuyên viên, xác nhận có mục báo cáo.
4. Đổi sang **Từ chối**, kiểm tra không thể gọi API báo cáo nếu API đã có kiểm tra quyền này.
5. Đổi về **Theo chức vụ**; quyền trở lại theo phân công.
6. Tài khoản quản trị cơ quan và chính người quản trị đang đăng nhập không được sửa quyền riêng qua màn hình.
7. Thử gọi API cấp quyền quản trị nhạy cảm như `users.manage` phải bị từ chối.
8. Với người đang có tác vụ ký được giao, xác nhận không thể tự ý ký bằng tài khoản chưa được chỉ định, dù có cấp `signing.sign`.

## API

- `GET /api/v1/admin/users/{id}/permissions` — danh sách quyền nghiệp vụ, quyền từ vai trò và ghi đè.
- `PUT /api/v1/admin/users/{id}/permissions` — `{"maDinhDanh":"reports.read","choPhep":true|false|null}`.

## Giới hạn của bản này

- Chỉ áp dụng cho quyền nghiệp vụ kiểm tra qua `PhanQuyen.YeuCau`.
- Việc sửa quyền không tự thay đổi các tác vụ đã giao hoặc quyền ký được gán đích danh.
- Chưa thực hiện chuyển tác vụ hoặc ủy quyền ký thay có thời hạn. Đó là bước **0.6.4.2**, cần kiểm thử riêng cùng chữ ký PDF.
