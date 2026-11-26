# Database Version 1 — SQL Server

Nhánh lưu mốc cấu trúc và migration của cơ sở dữ liệu ứng dụng tại thời điểm Version 1: `backup/database-version-1`.

## Nguồn dữ liệu được công khai

- Các script SQL trong thư mục `database/`: từ `001_khoi_tao.sql` đến `026_nhat_ky_khai_thac_van_ban_den.sql`.
- `002_du_lieu_mau.sql` là dữ liệu **mẫu trong mã nguồn**; không phải bản xuất dữ liệu vận hành từ máy người dùng. Không dùng script này để ghi đè CSDL đã có dữ liệu.
- Cấu trúc hiện tại là chuỗi migration tăng dần; không có file `.bak`, tài khoản thực, mật khẩu hay nội dung hồ sơ được sao lưu trên nhánh.

## Khôi phục hoặc dựng môi trường thử nghiệm

1. Dùng SQL Server riêng cho kiểm thử, xác minh tên database và đường dẫn trong từng script trước khi chạy.
2. Với database mới, đối chiếu thứ tự và điều kiện tiên quyết của các script, chạy `001` rồi lần lượt các migration phù hợp đến `026` (không chạy dữ liệu mẫu trên database vận hành).
3. Với database đã tồn tại, chỉ chạy migration **chưa áp dụng** sau khi sao lưu và kiểm tra schema. Đặc biệt không tự động chạy lại toàn bộ bộ script.
4. Xác thực các bảng, cột, khóa ngoại, chỉ mục và thực hiện kiểm thử ứng dụng bằng dữ liệu thử nghiệm.

## Ranh giới an toàn

Nhánh GitHub công khai **chỉ chứa mã nguồn SQL và tài liệu**; không commit file `.bak`, `.bacpac`, `.mdf`, `.ldf`, dữ liệu trích xuất CSV, tập tin khóa/cấu hình bí mật. Bản sao lưu thực tế phải được giữ ở kho riêng và kiểm tra khôi phục độc lập.

Việc tạo nhánh này không thay đổi cơ sở dữ liệu đang chạy và không tạo hay đẩy Git tag `v1.0.0`.
