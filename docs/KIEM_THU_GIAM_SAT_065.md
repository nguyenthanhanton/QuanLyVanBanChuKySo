# Kiểm thử phiên bản 0.6.5 — Giám sát (không điều khiển quy trình)

## Cập nhật

1. Dừng API và Vite, kéo nhánh `feat/v0.5-ky-so-nam-cap`.
2. Chạy `database/016_giam_sat_thong_bao.sql` trong SSMS, trên database `QuanLyVanBanChuKySo`.
3. Chạy `dotnet build` trong `backend`; khởi động lại từ thư mục dự án bằng `./start-backend.ps1` trong PowerShell.
4. Chạy `npm run dev` trong `web`.

## Kịch bản

1. Đăng nhập người xử lý, mở **Việc cần xử lý**. Xem hạn xử lý và mức cảnh báo trên mỗi tác vụ.
2. Nhấn biểu tượng chuông. Đối chiếu nhiệm vụ mới, trả sửa và ủy quyền; nhấn thông báo để đánh dấu đã đọc và mở văn bản. Đăng xuất, đăng nhập lại: trạng thái đã đọc vẫn được giữ.
3. Đăng nhập tài khoản có `reports.read`. Mở **Giám sát tiến độ**, lọc **Quá hạn**, **Sắp đến hạn**, **Đã hoàn thành**; đối chiếu người xử lý và thời gian.
4. Mở **Báo cáo thống kê**. Đối chiếu tổng, đang chờ, đã xử lý, quá hạn và thống kê theo phòng ban/cá nhân. Nhấn **Xuất Excel**; mở file `.xlsx` bằng Microsoft Excel.
5. Mở lại một văn bản đã ký và kiểm tra hash/chữ ký; báo cáo và thông báo tuyệt đối không được đổi trạng thái, thay người ký, đổi tệp hoặc sinh tác vụ mới.
6. Dùng tài khoản không có `reports.read`: API `/api/v1/monitor/reports` và `/excel` phải trả 403. Văn bản mật không được tiết lộ tiêu đề trong màn hình giám sát/báo cáo.

## Quy tắc thời hạn

- `QUA_HAN`: chưa xử lý và hạn trước thời điểm hiện tại.
- `SAP_DEN_HAN`: chưa xử lý và còn không quá 24 giờ.
- `TRONG_HAN`: chưa xử lý, còn hơn 24 giờ.
- `CHUA_DAT_HAN`: không có thời hạn.
- `HOAN_THANH`: tác vụ đã kết thúc.

Các trạng thái tính lúc đọc dữ liệu; không có job tự chuyển trạng thái. Việc đánh dấu đã đọc chỉ ghi vào `dbo.ThongBaoDaDoc`.
