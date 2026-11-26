# Kiểm thử 0.6.3 — Quản lý cơ cấu phòng ban

## Cập nhật

1. Sao lưu SQL Server `QuanLyVanBanChuKySo` trước khi cập nhật.
2. Kéo nhánh `feat/v0.5-ky-so-nam-cap` từ GitHub.
3. Trong SSMS, chạy `database/012_co_cau_phong_ban.sql` rồi `database/013_van_thu_thuoc_ban_lanh_dao.sql` sau `011`.
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
9. **Văn thư:** khi chọn **Ban lãnh đạo**, chức vụ bao gồm **Văn thư**; ở các phòng khác không có lựa chọn này. Không thể tạo thêm Văn thư đang hoạt động nếu cơ quan đã có một người.
10. **Chuyển dữ liệu Văn thư:** tài khoản Văn thư hiện tại phải thuộc Ban lãnh đạo sau `013`; văn bản cũ giữ nguyên phòng soạn trong lịch sử.
11. **Khóa/mở tài khoản:** thử khóa Văn thư hiện tại, tạo Văn thư mới và thử mở lại Văn thư cũ: thao tác mở lại phải bị chặn khi cơ quan đã có một Văn thư đang hoạt động.

## Đảm bảo dữ liệu

- Không xóa vật lý phòng ban, người dùng, văn bản hay lịch sử.
- Không cho phòng ban trực thuộc phòng khác cơ quan.
- `LANH_DAO`, `VAN_PHONG` là phòng hệ thống được bảo vệ. `VAN_THU` chỉ còn là phòng lịch sử và không cho mở lại sau bước chuyển đổi `013`.
- Nếu đã có hơn một Văn thư hoạt động trong cùng cơ quan, script `013` dừng an toàn để quản trị viên xử lý; không tự xóa hoặc khóa tài khoản.
- Hiện chỉ quản lý quan hệ trực thuộc để hiển thị và quản trị; không tự động mở rộng phạm vi quyền đọc/ký cho phòng cấp trên.
