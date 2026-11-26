# Nghiệm thu 0.7.1 — Nộp lưu hồ sơ cho Văn thư cơ quan

## Cập nhật

1. Sao lưu database `QuanLyVanBanChuKySo`.
2. Kéo nhánh `feat/v0.5-ky-so-nam-cap` và chạy `database/019_nop_luu_ho_so.sql` trong SSMS (sau 018).
3. Kiểm tra: `SELECT * FROM dbo.PhienBanLuocDo WHERE SoPhienBan=19;`.
4. Chạy `dotnet build` trong `backend` và khởi động từ gốc bằng `.\start-backend.ps1` (PowerShell).
5. Chạy `npm run dev` trong `web` và tải lại trình duyệt.

## Kịch bản

1. Đăng nhập người có quyền `dossiers.manage`, tạo hồ sơ với ít nhất một văn bản đi loại `THUONG` **đã phát hành**. Hồ sơ phải đóng trước khi nộp.
2. Nhấn **Nộp lưu hồ sơ**. Hệ thống ghi `CHO_TIEP_NHAN`, thời gian và người nộp.
3. Khi đang chờ, thử mở lại hồ sơ: máy chủ phải từ chối thay đổi trạng thái, không cho sửa văn bản liên kết.
4. Đăng nhập `vanthu_a` (Cơ quan A) với vai trò `VAN_THU` và quyền `dossiers.manage`, mở **Hồ sơ lưu trữ → Hộp tiếp nhận**. Nhấn **Kiểm tra văn bản** để xem các văn bản đã phát hành và chọn **Trả lại** với lý do.
5. Người nộp xem lý do, mở lại hồ sơ, bổ sung, đóng lại và **Nộp lại**. Kiểm tra lịch sử có cả hai lượt.
6. Văn thư nhấn **Tiếp nhận**. Kiểm tra `DA_TIEP_NHAN`, mã biên nhận, người giao/nhận và thời gian; nhấn **Biên nhận → In / Lưu PDF**.
7. Thử xử lý cùng lượt lần nữa: API phải từ chối. Sau khi tiếp nhận, không thể mở hồ sơ để sửa hoặc nộp lại.
8. Đăng nhập tài khoản trưởng phòng/chuyên viên không có vai trò Văn thư: nút tiếp nhận không hiện; gọi trực tiếp API quyết định phải trả `403`.
9. Đăng nhập `vanthu_b`: không được xử lý lượt nộp lưu thuộc Cơ quan A (trả `404`).

## An toàn dữ liệu

- Không thay đổi nội dung PDF hay chữ ký. Lượt nộp lưu chỉ tham chiếu `HoSoLuuTru`.
- Không dùng biên nhận này làm chữ ký số CA; đây là biên nhận nghiệp vụ lưu trên database.
- Chỉ Văn thư cùng cơ quan (`VAN_THU`) và có quyền `dossiers.manage` có thể tiếp nhận/trả lại.
- Trả lại phải ghi lý do, hồ sơ đã tiếp nhận không được sửa.
