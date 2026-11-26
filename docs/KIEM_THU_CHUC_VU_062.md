# Kiểm thử phiên bản 0.6.2 — Chức vụ theo phòng ban

## Cập nhật

1. Cập nhật mã nguồn ở nhánh `feat/v0.5-ky-so-nam-cap`.
2. Sao lưu cơ sở dữ liệu trước khi chạy script.
3. Chạy `database/011_chuc_vu_theo_phong_ban.sql` sau script `010` trong SQL Server Management Studio.
4. Dừng API, chạy `dotnet build` trong `backend`, sau đó chạy `start-backend.ps1` từ thư mục gốc dự án.
5. Chạy `npm run dev` trong `web`, làm mới trình duyệt bằng Ctrl+Shift+R.

## Quy tắc

- Phòng `LANH_DAO` (Ban lãnh đạo) chỉ nhận `LANH_DAO` và `PHO_LANH_DAO`.
- Các phòng khác không được gán hai chức vụ lãnh đạo cơ quan; người đứng đầu dùng `TRUONG_PHONG`, cấp phó dùng `PHO_PHONG`.
- `VAN_PHONG` không còn chức vụ kỹ thuật `TRUONG_VAN_PHONG`/`PHO_VAN_PHONG`. Các tài khoản cũ được chuyển sang `TRUONG_PHONG`/`PHO_PHONG` nhưng vẫn giữ tên chức vụ hiển thị trong `NguoiDung.ChucVu`.
- Ô “Tên gọi chức vụ tại cơ quan” là nhãn tự do, ví dụ “Trưởng văn phòng”, “Phó viện trưởng”. Nhãn này không cấp thêm quyền ký; quyền được kiểm tra bằng mã chức vụ kỹ thuật, phòng ban và người được chọn ký.
- Chỉ có một trưởng phòng đang hoạt động trong một phòng và một lãnh đạo cơ quan chính đang hoạt động trong cơ quan.

## Kịch bản

1. Đăng nhập quản trị cơ quan, mở **Quản lý tài khoản**.
2. Chọn **Ban lãnh đạo**: chỉ thấy “Lãnh đạo cơ quan”, “Phó lãnh đạo cơ quan”; nhập tên gọi “Phó viện trưởng”.
3. Chọn **Văn phòng**: thấy “Trưởng phòng”, “Phó trưởng phòng”, không còn “Trưởng văn phòng”; nhập tên gọi “Trưởng văn phòng”.
4. Đổi phòng của tài khoản trong danh sách, xác nhận danh sách chức vụ đổi theo phòng; sửa tên gọi chức vụ rồi lưu.
5. Tạo văn bản bởi người thuộc Văn phòng và trình duyệt: chỉ chọn được trưởng/phó phòng cùng Văn phòng (với Văn thư, người duyệt phòng ở Văn phòng).
6. Đăng nhập lãnh đạo cơ quan, xác nhận quy trình lãnh đạo soạn vẫn bỏ qua ký trưởng phòng.

**Lưu ý:** Script không xóa tài khoản hay nhật ký lịch sử và không thay đổi tên chức vụ đã nhập.
