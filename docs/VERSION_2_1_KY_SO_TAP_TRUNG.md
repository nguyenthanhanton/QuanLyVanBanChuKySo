# Version 2.1 — Quản lý chữ ký số tập trung

Nhánh: `feat/version-2.1-chu-ky-tap-trung` (tách từ Version 1).

## Nghiệp vụ

1. Cá nhân đăng ký tối đa **ba chứng thư cá nhân đang hoạt động**, mỗi chứng thư có ảnh PNG và tên mẫu; ảnh có thể được cập nhật khi chưa vô hiệu hóa.
2. Lãnh đạo **cấp trưởng** (`LANH_DAO`) đăng ký và quản lý **một chứng thư cơ quan đang hoạt động**. Văn thư (`VAN_THU`) được **dùng** chứng thư cơ quan ở bước ký phát hành, không được tạo hay thay đổi chứng thư cơ quan.
3. Khi ký, người dùng chọn một cấu hình đã đăng ký (cá nhân ở các bước 1–4; cơ quan ở bước cuối), đặt vị trí trên PDF rồi nhập mật khẩu và xác nhận. Client **không gửi ảnh Base64** theo mỗi lệnh ký; backend đọc mẫu PNG đã đăng ký cùng chứng thư theo `MaChungThu` và ký PDF theo luồng nguyên vẹn đã có.
4. Việc vô hiệu hóa chứng thư không xóa chứng thư, khóa hoặc tài liệu đã ký trước đó. Không thay số bước/quy trình phê duyệt của Version 1.

## Cài đặt

1. Sao lưu database, kiểm tra đã áp dụng migration `026`.
2. Chạy `database/027_chu_ky_tap_trung.sql` trên **database thử nghiệm**; script thêm `TenMauChuKy`, `HinhAnhChuKy`, bỏ unique index giới hạn một chứng thư cá nhân, duy trì unique một chứng thư cơ quan và chuyển quyền quản lý chứng thư cơ quan từ Văn thư sang Lãnh đạo trưởng.
3. Dừng API đang chạy trước khi `dotnet build` để tránh Windows khóa `QuanLyVanBan.Api.exe`; sau đó khởi động bằng `start-backend.ps1` hiện có.
4. Tại `web`: chạy `npm ci`, `npm run build`, `npm run dev`. Đăng xuất/đăng nhập lại để token và danh sách quyền cập nhật theo migration.
5. Vào **Quản lý chữ ký số** để đăng ký các ảnh/chứng thư *trước khi ký*. Chứng thư cũ giữ nguyên nhưng cần bấm **Đổi ảnh PNG** một lần để dùng theo luồng v2.1.

## Kiểm thử nghiệm thu

1. Người dùng cá nhân tạo 3 mẫu hoạt động, thử tạo mẫu thứ 4: trả `409`; vô hiệu hóa 1 mẫu rồi đăng ký mới: thành công.
2. Văn thư không được đăng ký hoặc vô hiệu hóa chứng thư cơ quan; Lãnh đạo trưởng làm được. Phó lãnh đạo không được quản lý chứng thư cơ quan.
3. Ở bước soạn, chọn mẫu cá nhân A, mở PDF, kéo đặt, xác nhận, ký và tải PDF đã ký. Kiểm tra ảnh và xác minh CMS.
4. Ở bước trưởng phòng / lãnh đạo: chọn mẫu cá nhân thuộc tài khoản hiện tại; thử dùng ID mẫu tài khoản khác phải từ chối.
5. Ở bước cuối: Văn thư chọn mẫu cơ quan và ký; thử dùng chứng thư cá nhân ở bước cơ quan phải từ chối.
6. Thử gửi request ký không có `MaChungThu` hoặc với chứng thư đã vô hiệu hóa: phải bị từ chối.
7. Kiểm tra lịch sử ký và PDF Version 1 không thay đổi; ảnh vừa cập nhật chỉ áp dụng cho lần ký mới.
8. Kiểm tra giới hạn ảnh PNG 5 MB, mật khẩu tài khoản sai, hết hạn chứng thư và PDF trang xoay.

**Không sử dụng chứng thư tự ký thử nghiệm như chứng thư CA hợp pháp.** Không đưa khóa, mật khẩu, `.bak` hay PFX vào GitHub.
