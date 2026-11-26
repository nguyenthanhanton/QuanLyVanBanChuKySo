# Nghiệm thu đổi tên cơ quan và giữ phiên khi F5

## 1. Đổi tên cơ quan

1. Đăng nhập tài khoản `AGENCY_ADMIN` của cơ quan A.
2. Mở **Quản trị tài khoản > Thông tin cơ quan**. Mã cơ quan và địa chỉ chỉ đọc; tên cơ quan có thể sửa.
3. Đổi tên cơ quan A thành một tên hợp lệ, nhấn **Lưu tên cơ quan**.
4. Kiểm tra tên trên menu bên trái thay đổi ngay; tải lại trình duyệt vẫn hiển thị tên mới.
5. Đăng nhập bằng tài khoản cơ quan B: tên cơ quan B không đổi.
6. Đăng nhập tài khoản chuyên viên A: không có chức năng chỉnh sửa tên cơ quan.
7. Kiểm tra `dbo.NhatKyHeThong` có hành động `DOI_TEN_CO_QUAN` với cơ quan/người thao tác đúng.
8. Thử gửi `PUT /api/v1/admin/agency/name` bằng tài khoản không có quyền: phải trả HTTP 403.
9. Đổi lại tên cơ quan cũ nếu đang nghiệm thu trên database thật.

Không cần migration mới: dùng cột `dbo.CoQuan.TenCoQuan` và nhật ký hiện có.

## 2. Giữ đăng nhập sau khi tải lại trang

1. Đăng nhập tài khoản bất kỳ; mở **Việc cần xử lý** hoặc **Quản trị tài khoản**.
2. Nhấn F5: phải giữ đăng nhập và trở lại mục đang xem.
3. Tiếp tục nhấn Ctrl+R: kết quả tương tự.
4. Đăng xuất rồi nhấn F5: phải trở về trang đăng nhập.
5. Chờ JWT hết hạn (hiện mặc định 30 phút) rồi tải lại: phải yêu cầu đăng nhập, không sử dụng token hết hạn.
6. Thử tắt API khi tải lại: ứng dụng thông báo lỗi kết nối, không tự động xóa phiên vì lỗi mạng.
7. Kiểm tra tài khoản bị khóa hoặc cơ quan bị ngừng hoạt động: khi tải lại phải yêu cầu đăng nhập lại.

Phiên lưu tạm trong `sessionStorage` của tab trình duyệt, không lưu mật khẩu và không dùng `localStorage` cho token. Đóng tab sẽ kết thúc lưu phiên tạm trong tab đó. Các API luôn kiểm tra quyền trên backend.

## Kiểm tra mã nguồn

- Frontend: `npm run build`.
- Backend: `dotnet build`.
- Kiểm tra trình duyệt với `VITE_DEMO_MODE=false` và API hoạt động.
- Báo cáo từng tình huống `ĐẠT` / `KHÔNG ĐẠT` / `BỊ CHẶN`, kèm bước tái hiện và log lỗi khi có.
