# Kiểm thử 0.6.3 — Quản lý cơ cấu phòng ban

## Cập nhật

1. Sao lưu SQL Server `QuanLyVanBanChuKySo` trước khi cập nhật.
2. Kéo nhánh `feat/v0.5-ky-so-nam-cap` từ GitHub.
3. Trong SSMS, chạy `database/012_co_cau_phong_ban.sql` sau `011`.
4. Khởi động lại API bằng `start-backend.ps1` tại thư mục gốc, rồi vào `web` chạy `npm run dev`.
5. Đăng nhập tài khoản quản trị cơ quan và mở mục **Quản lý tài khoản**.

## Tình huống cần kiểm tra

1. **Tạo:** tạo mã `KE_TOAN`, tên `Phòng Kế toán`, thứ tự `30`, trực thuộc `Ban lãnh đạo`. Kiểm tra phòng xuất hiện khi tạo tài khoản mới.
2. **Sắp xếp:** đổi thứ tự Kế toán thành `15`, nhấn **Lưu**. Danh sách người dùng phải sắp theo thứ tự đã lưu, trong mỗi phòng từ trưởng đến phó rồi nhân viên.
3. **Đổi tên:** đổi `Phòng Kế toán` thành `Phòng Tài chính - Kế toán`; kiểm tra tài khoản trong phòng vẫn giữ nguyên mã phòng, không bị mất.
4. **Trực thuộc:** chuyển Kế toán trực thuộc `Văn phòng`. Không cho chuyển Ban lãnh đạo xuống dưới phòng khác; không cho tạo vòng lặp giữa các phòng.
5. **Ngừng hoạt động:** phòng đang có người hoặc phòng trực thuộc phải bị chặn ngừng. Phòng không có người/con được ngừng hoạt động và không xuất hiện trong danh sách phân công mới.
6. **Mở lại:** mở lại phòng đã ngừng hoạt động, kiểm tra phòng xuất hiện trở lại.
7. **Phân quyền:** người không có `departments.manage` không được gọi các API quản trị phòng ban.
8. **Luồng ký:** tạo và trình duyệt văn bản trong một phòng chuyên môn, kiểm tra chỉ người ký được phép trong đúng phòng được chọn.

## Đảm bảo dữ liệu

- Không xóa vật lý phòng ban, người dùng, văn bản hay lịch sử.
- Không cho phòng ban trực thuộc phòng khác cơ quan.
- Các mã phòng hệ thống `LANH_DAO`, `VAN_PHONG`, `VAN_THU` được bảo vệ khỏi ngừng hoạt động vì quy trình ký đang dùng mã này.
- Hiện chỉ quản lý quan hệ trực thuộc để hiển thị và quản trị; không tự động mở rộng phạm vi quyền đọc/ký cho phòng cấp trên.
