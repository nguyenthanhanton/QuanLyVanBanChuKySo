# Nghiệm thu phiên bản 0.7 — Hồ sơ lưu trữ điện tử

## Cài đặt

1. Trên nhánh `feat/v0.5-ky-so-nam-cap`, kéo cập nhật mới nhất từ GitHub.
2. **Sao lưu cơ sở dữ liệu** `QuanLyVanBanChuKySo` trước khi cập nhật.
3. Chạy toàn bộ `database/017_ho_so_luu_tru.sql` trong SSMS. Kiểm tra:

   ```sql
   USE QuanLyVanBanChuKySo;
   SELECT SoPhienBan,TenPhienBan FROM dbo.PhienBanLuocDo WHERE SoPhienBan=17;
   ```

4. Khởi động backend bằng `./start-backend.ps1` **ở thư mục gốc dự án** (trong PowerShell dùng `.\start-backend.ps1`), frontend trong `web` bằng `npm run dev`.

## Kiểm thử chức năng

1. Đăng nhập tài khoản có quyền `dossiers.manage`, mở **Hồ sơ lưu trữ**.
2. Tạo hồ sơ mã `HS-2026-001`, năm 2026, chọn phòng ban, loại hồ sơ, thời hạn bảo quản (để trống nghĩa là vĩnh viễn). Thử tạo mã trùng; phải bị từ chối.
3. Mở hồ sơ, tra cứu văn bản đi **đã phát hành**, đưa vào hồ sơ. Văn bản đang là bản nháp không được phép thêm; văn bản thuộc cơ quan khác không được phép thêm.
4. Chọn **Xem** để mở chi tiết văn bản; kiểm tra bản PDF đã ký vẫn được giữ nguyên. Gỡ văn bản khỏi hồ sơ; **văn bản gốc không bị xóa**.
5. Đóng hồ sơ; nút thêm/sửa/gỡ phải bị khóa. Mở lại và thử thêm văn bản.
6. Kiểm tra mục lịch sử đã ghi tạo, chỉnh sửa, thêm/gỡ, đóng/mở và người thực hiện/thời điểm.
7. Tạo hơn 15 hồ sơ và hơn 15 sự kiện, kiểm tra nút **Trước/Sau**.
8. Đăng nhập tài khoản cơ quan khác, thử truy cập GUID hồ sơ qua API: phải trả 404. Với tài khoản cùng cơ quan nhưng khác phòng và không phải người tạo/quản trị, phải không nhìn thấy hồ sơ đó. Khi người dùng có quyền xem hồ sơ nhưng không có quyền xem văn bản liên kết, danh sách văn bản không được tiết lộ.

## Lưu ý

- Đây là **lưu trữ liên kết**, không chuyển nơi lưu các PDF đã ký và không tự động xóa tệp khi hết hạn bảo quản.
- Hồ sơ đã đóng là trạng thái khóa chỉnh sửa, **không phải thao tác xóa**.
- Phiên bản 0.7 chỉ đưa **văn bản đi loại thường đã phát hành** vào hồ sơ. Việc lưu trữ văn bản đến, văn bản mật và chính sách tiêu hủy cần quy trình riêng.
- Nếu không thấy menu, kiểm tra tài khoản có quyền `dossiers.manage`; API luôn kiểm tra quyền bên máy chủ.
