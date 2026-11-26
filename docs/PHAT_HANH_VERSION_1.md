# Version 1 (v1.0.0) — Ghi nhận nghiệm thu

Ngày ghi nhận: 26/11/2026.

## Phạm vi

Version 1 bao gồm nền tảng quản lý văn bản đi/đến, quy trình ký số, phát hành và tiếp nhận, giao việc, hồ sơ lưu trữ, quản trị đa cơ quan cùng bản vá tám phát hiện QA tại PR #3.

## Kết quả do người dùng nghiệm thu thủ công

Người dùng xác nhận **ĐẠT 8/8 bước** trong luồng nghiệm thu tương tác:

1. Đăng nhập và giữ phiên sau F5 — ĐẠT.
2. Thông báo văn bản đến/quyền Văn thư — ĐẠT.
3. Việc cần xử lý và điều hướng — ĐẠT.
4. Phân quyền dữ liệu giữa cơ quan — ĐẠT.
5. Xem PDF đã ký và tệp đính kèm — ĐẠT.
6. Hồ sơ lưu trữ và quyền khai thác — ĐẠT.
7. Xử lý lỗi 403, 404, mất và khôi phục kết nối — ĐẠT.
8. Giao diện và hồi quy nghiệp vụ tổng thể — ĐẠT.

Đây là xác nhận nghiệm thu do người dùng báo cáo, không phải bằng chứng test tự động riêng của từng QA-BUG-001…008. Các tình huống chi tiết trong `docs/KIEM_THU_SUA_8_LOI_QA_20261126.md` và ca NOT_RUN/BLOCKED từ báo cáo QA gốc vẫn phải theo dõi độc lập nếu chưa có log chứng minh.

## Kiểm thử xây dựng

GitHub Actions `Build backend and web` run #45799086553: **success** cho commit sửa lỗi `2d9acba` (trước khi bổ sung tài liệu nghiệm thu). Người dùng cũng báo `dotnet build` thành công trên Windows và khởi chạy backend/frontend.

## Dữ liệu và vận hành

- Migration bổ sung: `database/026_nhat_ky_khai_thac_van_ban_den.sql` (chạy trên DB test sau khi sao lưu, nếu chưa áp dụng).
- Việc hợp nhất code không tự động chạy SQL migration, không thay đổi dữ liệu SQL Server, chứng thư PFX hoặc khóa AES.
- Không đưa `start-backend.ps1`, bí mật, nội dung `D:\QuanLyVanBanSecrets` lên GitHub.
- Mốc phát hành dự kiến: `v1.0.0`; Git tag chỉ được coi là tồn tại khi xác nhận đã tạo trên remote.
