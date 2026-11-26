# Kiểm thử chức năng Quản trị toàn hệ thống

## Mục tiêu

- Tài khoản `SYSTEM_ADMIN` quản lý danh mục cơ quan, cấp và khóa quản trị viên cơ quan.
- Quản trị viên cơ quan chỉ quản lý người dùng/phòng ban của cơ quan mình.
- Không tài khoản quản trị nào mặc nhiên được đọc nội dung văn bản nghiệp vụ.

## Chuẩn bị

1. Sao lưu database trước khi chạy migration hoặc thực hiện thử nghiệm ghi dữ liệu.
2. Chạy `database/025_quan_tri_toan_he_thong.sql` **sau** `024`; kiểm tra phiên bản 25 trong `dbo.PhienBanLuocDo`.
3. Không chạy lại `001` và `002` trên database đang sử dụng.
4. Khởi động backend bằng `start-backend.ps1` từ thư mục gốc repository và frontend bằng `npm run dev` trong `web`.
5. Dùng tài khoản hệ thống đang hoạt động (`quantrihethong` nếu còn tồn tại), không dùng mật khẩu seed ở môi trường thật; đổi mật khẩu theo quy trình an toàn.

## Kịch bản chính

| Mã | Người thực hiện | Thao tác | Kết quả phải có |
| --- | --- | --- | --- |
| SYS-01 | `SYSTEM_ADMIN` | Đăng nhập, mở **Quản trị hệ thống** | Hiện danh sách cơ quan; không hiện màn hình văn bản nghiệp vụ |
| SYS-02 | `SYSTEM_ADMIN` | Tạo mã `CQ_TEST_025`, tên và địa chỉ | Tạo cơ quan, Ban lãnh đạo, Văn phòng và cấu hình quy trình |
| SYS-03 | `SYSTEM_ADMIN` | Tạo lại mã `CQ_TEST_025` | HTTP 409, không tạo bản ghi trùng |
| SYS-04 | `SYSTEM_ADMIN` | Chọn `CQ_TEST_025`, cấp quản trị viên mới | Tài khoản đăng nhập được và chỉ quản lý cơ quan thử nghiệm |
| SYS-05 | Quản trị cơ quan thử nghiệm | Tạo tài khoản nhân viên/phòng ban | Thành công trong cùng cơ quan |
| SYS-06 | Quản trị cơ quan A | Gọi API `/api/v1/system/agencies` | HTTP 403 |
| SYS-07 | Quản trị cơ quan thử nghiệm | Thử truy cập API người dùng cơ quan A bằng ID | Không thấy và không sửa được dữ liệu cơ quan A |
| SYS-08 | `SYSTEM_ADMIN` | Khóa quản trị viên thử nghiệm, nhập lý do | Tài khoản không đăng nhập được; có nhật ký |
| SYS-09 | `SYSTEM_ADMIN` | Mở lại tài khoản, nhập lý do | Đăng nhập được; có nhật ký |
| SYS-10 | `SYSTEM_ADMIN` | Ngừng hoạt động cơ quan thử nghiệm, nhập lý do | Mọi tài khoản thuộc cơ quan đó bị từ chối đăng nhập; không xóa dữ liệu |
| SYS-11 | `SYSTEM_ADMIN` | Mở lại cơ quan thử nghiệm | Tài khoản hoạt động đăng nhập bình thường |
| SYS-12 | `SYSTEM_ADMIN` | Kiểm tra **Nhật ký quản trị** | Có thao tác tạo/sửa/khóa/mở, người thực hiện, thời gian |
| SYS-13 | `SYSTEM_ADMIN` | Thử đọc API `/api/v1/documents` | HTTP 403, không có quyền nội dung văn bản |

## Kỳ vọng kỹ thuật

- Backend `dotnet build`: không lỗi; frontend `npm run build`: không lỗi.
- API quan trọng: `GET/POST /api/v1/system/agencies`, `PUT /api/v1/system/agencies/{id}`, `PUT /api/v1/system/agencies/{id}/status`, `GET/POST /api/v1/system/agencies/{id}/admins`, `PUT /api/v1/system/agencies/{id}/admins/{user}/status`, `GET /api/v1/system/audit`.
- Mật khẩu chỉ lưu dạng băm PBKDF2, tuyệt đối không ghi vào nhật ký hoặc ảnh chụp nghiệm thu.
- Không tự động xóa cơ quan, tài khoản hoặc dữ liệu đã có trong quá trình kiểm thử.
- Trạng thái test phải báo `ĐẠT`, `KHÔNG ĐẠT` hoặc `BỊ CHẶN` với mã HTTP và bằng chứng; tuyệt đối không suy đoán.

## Báo cáo Codex cần trả lại

Tạo `docs/KET_QUA_KIEM_THU_QUAN_TRI_HE_THONG_025.md` gồm môi trường, migration đã chạy, số lượng kịch bản đạt/không đạt/bị chặn, các lỗi cụ thể (bước tái hiện, nguyên nhân, ảnh/log nếu có), thay đổi mã nguồn đã thực hiện và rủi ro tồn đọng. Chỉ sửa lỗi nếu người dùng cho phép; không tự sửa dữ liệu thật.
