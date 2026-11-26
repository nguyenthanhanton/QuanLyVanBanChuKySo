# Kiểm thử 0.6.1 — Quản trị người dùng trong từng cơ quan

## Phạm vi

- Đăng nhập tài khoản `quantri_a` có quyền `users.manage` (không phải `quantrihethong`).
- Menu **Quản trị tài khoản** cho phép xem người dùng, tạo tài khoản, đổi phòng ban/vai trò, khóa/mở khóa.
- Chức vụ hiển thị bằng tiếng Việt có dấu; tên bảng/cột database vẫn không dấu.
- Mật khẩu mới do quản trị viên nhập (tối thiểu 12 ký tự) và được lưu dưới dạng PBKDF2-SHA256 với salt ngẫu nhiên; không trả lại mật khẩu qua API, không commit mật khẩu lên GitHub.
- Chỉ quản trị viên cùng cơ quan được thao tác, không được tự sửa/khóa chính mình hay thay đổi tài khoản quản trị khác.
- Chỉ gán vai trò nghiệp vụ, không có chức năng tự cấp `AGENCY_ADMIN`/`SYSTEM_ADMIN`.
- Ngăn tạo cấp trưởng thứ hai đang hoạt động trong cùng đơn vị/cơ quan. Có thể tạo nhiều cấp phó.

## Kiểm tra

1. `git pull`, chạy `dotnet build`, `npm run build`. Không cần script migration mới nếu đã chạy 009/010 trước đó.
2. Đăng nhập `quantri_a`: thấy menu **Quản trị tài khoản**, nhưng không tự động được đọc văn bản nghiệp vụ.
3. Tạo `phophong_a` thuộc **THAM_MUU**, vai trò **Phó trưởng phòng**, mật khẩu tự chọn >=12 ký tự (không dùng mật khẩu demo chung). Danh sách xuất hiện đúng người và vai trò.
4. Thử tạo trùng `phophong_a`: API trả `409`.
5. Thử gán một tài khoản phòng Tham mưu thành **Trưởng phòng** khi đã có `truongphong_a` đang hoạt động: API từ chối `409`.
6. Khóa `phophong_a`: đăng nhập bằng tài khoản đó phải thất bại. Mở khóa rồi đăng nhập lại.
7. Đăng nhập `quantri_b`: không thấy người dùng cơ quan A; ID của người dùng A không thể sửa từ token cơ quan B.
8. `quantri_a` thử tự khóa hoặc thay đổi quyền của chính mình: API trả `403`.
9. Tạo tài khoản `pholanhdao_a` với vai trò **Phó lãnh đạo cơ quan**; kiểm tra khi `lanhdao_a` soạn văn bản, tên người này xuất hiện trong danh sách lãnh đạo ký tiếp.

## Giới hạn và phần sau

- 0.6.1 **chưa** có giao diện đổi mật khẩu riêng, đặt lại mật khẩu, quản trị cơ quan/phòng ban đầy đủ hoặc ủy quyền ký thay có thời hạn. Các việc này để 0.6.2+.
- Đổi vai trò/phòng ban tài khoản đang tham gia duyệt văn bản có thể khiến nhiệm vụ cũ không còn đủ điều kiện ký; nên hoàn tất hoặc điều phối lại các việc đang xử lý trước khi đổi phân công.
- Chỉ dùng HTTPS khi triển khai thực tế; không sử dụng mật khẩu thử nghiệm chung.
