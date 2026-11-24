# Các bước còn lại để hoàn thiện phần mềm

Tài liệu này là **danh mục công việc chưa hoàn thành**, không phải biên bản nghiệm thu. Bản mã nguồn 0.2 đã thêm API **ký PDF/CMS theo thứ tự, tạo chứng thư, phát hành, gửi, nhận, từ chối**, nhưng chưa chạy .NET và SQL Server trong môi trường xây dựng, chưa kiểm thử E2E.

## Bước 1 — Khóa nền tảng, kiểm tra trên Windows

- Build .NET 8, tạo DB và chạy seed trên **database mới**.
- Kiểm thử 401 vs 403; `quantrihethong` không đọc được văn bản; `quantri_a` không xem cơ quan B; người dùng không xem văn bản ngoài phạm vi.
- Kiểm thử 5 lần sai khóa 5 phút, đổi mật khẩu, phiên đăng nhập, refresh token HttpOnly và thu hồi phiên (hiện chưa triển khai phần sau).
- Kiểm tra giao diện live bằng API và toàn bộ luồng tạo bản PDF → trình duyệt → trả sửa → nộp lại → phê duyệt → cấp số.
- Bổ sung quản lý vai trò nghiệp vụ chỉ sau khi có quy trình lãnh đạo phê duyệt.

## Bước 2 — Chữ ký số PDF (đã viết mã lõi; phải kiểm thử/tích hợp CA)

- Đã viết: chứng thư cá nhân và cơ quan tách biệt; nhập PFX/P12 hoặc tự tạo **chứng thư thử nghiệm**; mã hóa PFX AES-256-GCM với khóa cấu hình bên ngoài DB/source.
- Đã viết: chuẩn bị bản PDF đã cấp số (chèn số/ký hiệu ở chân trang) trước mọi bước ký; khóa bằng hash SHA-256, mỗi bước có snapshot bất biến.
- Đã viết: ký **append-mode PDF** theo ba vai trò, yêu cầu mật khẩu tài khoản; kiểm tra tính toàn vẹn CMS và độ bao phủ của chữ ký cuối.
- **Chưa xong:** biên dịch/thử ký bằng .NET, nghiệm thu chỉnh sửa PDF làm chữ ký thất bại, thẩm định chuỗi CA / thu hồi, USB token/HSM/TSA.
- **Chưa xong:** mẫu chữ ký hiển thị, chọn trang/vị trí/kích thước, xem trước PDF trước khi xác nhận, quản lý vòng đời chứng thư và quy trình đổi khóa.
- Kiểm tra điều kiện giấy phép iText AGPL/commercial trước triển khai.

## Bước 3 — Phát hành/giao nhận (đã viết API lõi; chưa nghiệm thu)

- Đã viết `POST /api/v1/documents/{id}/publications`: chỉ phát hành khi ba chữ ký xác minh được về mật mã và bản PDF khớp hash; tạo các `LuotChuyenVanBan` trong một transaction.
- Đã viết `POST /api/v1/deliveries/{id}/deliver`: gửi có `MaYeuCau` ngăn lặp; `GET /api/v1/deliveries/inbox`, tiếp nhận hoặc từ chối kèm lý do.
- Đã viết hành trình ghi phát hành, gửi, tiếp nhận, từ chối và người thực hiện; sổ đến độc lập ở mỗi cơ quan nhận.
- **Chưa xong:** kiểm thử lỗi kết nối/crash/retry, hàng đợi gửi ngoài hệ thống/offline, số đến, hiển thị hạn/quá hạn trực tiếp trên bản đồ hành trình.
- **Không** cho A xem kết quả xử lý nội bộ của B/C. B/C chỉ xem bản phát hành, không thấy revision/dự thảo/nhận xét của A.

## Bước 4 — Nghiệp vụ hoàn chỉnh

- Văn bản đến từ bên ngoài hệ thống, đăng ký số đến, chỉ đạo và giao chuyên viên hai cấp.
- Kết quả xử lý, duyệt kết quả/trả lại, thời hạn, độ mật/phân quyền theo bộ phận.
- Hồ sơ lưu trữ: đóng chỉ khi hết nhiệm vụ, giữ vị trí giấy và bản gốc điện tử.
- Thu hồi/thay thế/đính chính: lãnh đạo quyết định, văn thư thực hiện, bên nhận xác nhận.
- Quản trị cơ quan/tài khoản/phòng ban/cấu hình quy trình phiên bản hóa và audit.
- Thông báo, báo cáo CSV an toàn, nhật ký kỹ thuật vs nghiệp vụ.

## Bước 5 — Nghiệm thu

- Chạy AC-01..AC-20 theo `07-cach-chay-va-nghiem-thu.md` từ bộ yêu cầu đầu vào.
- Unit/integration test SQL Server riêng; không chạy chung DB với E2E.
- Kiểm thử crash/retry, idempotency, 409 RowVersion, phân quyền bằng HTTP trực tiếp.
- Kiểm thử 3 chữ ký số PDF bằng công cụ xác minh độc lập; cảnh báo đúng hạn chế chứng thư tự ký.
- Backup/restore đồng bộ SQL + kho PDF + keyring, đo độ tin cậy và kiểm thử mất khóa.

**Không triển khai tự động lên production, không chuyển dữ liệu từ DB cũ, không lưu khóa riêng trong repository.**
