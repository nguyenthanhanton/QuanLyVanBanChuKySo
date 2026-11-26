# Nghiệm thu 0.5.6 — Chọn người ký theo phòng ban

## Chuẩn bị

1. Sao lưu SQL Server `QuanLyVanBanChuKySo`.
2. Chạy `database/008_nguoi_ky_duoc_chon.sql` trong SSMS sau migration 007. Xác nhận `SoPhienBan=8` tại `dbo.PhienBanLuocDo`.
3. Cập nhật backend và frontend, chạy `dotnet build` và `npm run build`.
4. Dùng văn bản thử nghiệm **mới**, PDF nguồn hợp lệ; không sử dụng hồ sơ đang ký dở để kiểm tra 4 cấp. Không tạo lại khóa mã hóa chứng thư.

## Các ca kiểm thử bắt buộc

| Ca | Thao tác | Mong đợi |
| --- | --- | --- |
| 1 | `chuyenvien_a` soạn, ký người soạn, nhấn **Trình duyệt** | Danh sách ký nháy chỉ có trưởng phòng thuộc `THAM_MUU` (ví dụ `truongphong_a`); lãnh đạo cùng cơ quan. |
| 2 | `vanthu_a` soạn, ký người soạn, nhấn **Trình duyệt** | Danh sách ký nháy là `TRUONG_VAN_PHONG` (ví dụ `truongvanphong_a`), không phải trưởng phòng `THAM_MUU`. |
| 3 | Chọn người và xác nhận | Tạo `TacVuXuLy` cho chính người ký nháy được chọn; văn bản chuyển `CHO_DUYET`, `VanBan.SoBuocKy=4`. |
| 4 | Đăng nhập người ký không được chọn và thử API ký | Bị từ chối 403, không tạo bản PDF. |
| 5 | Ký nháy bước 2 rồi duyệt | Nhiệm vụ chuyển thẳng đến lãnh đạo được chọn (không qua ký thể thức riêng). |
| 6 | Lãnh đạo được chọn ký bước 3 và duyệt | `CHO_CAP_SO`; văn thư cấp số và ký cơ quan bước 4. |
| 7 | Phát hành và gửi cơ quan B, B tiếp nhận | Bản PDF cuối có đúng 4 chữ ký toàn vẹn; hành trình cho biết chính xác từng người. |
| 8 | Thử gọi `/submit` với người ký của cơ quan khác, sai vai trò hoặc trưởng phòng khác đơn vị | API trả 400; không tạo nhiệm vụ, không đổi trạng thái. |
| 9 | Văn bản cũ đã `CHO_DUYET` theo 5 cấp | Tiếp tục quy trình 5 cấp đã lưu, không tự chuyển sang 4 cấp. |

**Giới hạn:** Chưa hỗ trợ phòng ban không có trưởng đơn vị được gán đúng vai trò; API chủ động từ chối trình duyệt và yêu cầu quản trị cấu hình lại. Chứng thư tự ký chỉ để thử nghiệm, không có nghĩa đã được CA pháp lý công nhận.
