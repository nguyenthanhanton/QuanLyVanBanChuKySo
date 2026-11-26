# Nghiệm thu bản gộp giai đoạn 0.7 (0.7.2–0.7.5)

## Triển khai

1. Sao lưu database `QuanLyVanBanChuKySo`; kéo nhánh `feat/v0.5-ky-so-nam-cap`.
2. Chạy `database/020_ho_so_luu_tru_nang_cao.sql` trong SSMS (yêu cầu 019 đã chạy).
3. Kiểm tra `SELECT * FROM dbo.PhienBanLuocDo WHERE SoPhienBan=20;`.
4. Chạy `dotnet build` trong thư mục `backend`, khởi động backend bằng `.\start-backend.ps1` từ thư mục gốc và chạy `npm run dev` trong `web`.

## Kiểm thử tra cứu và bảo quản

1. Tạo/tiếp nhận hồ sơ đã phát hành theo quy trình 0.7 và 0.7.1.
2. Mở **Hồ sơ lưu trữ → Lưu trữ và khai thác → Tra cứu & thống kê**. Kiểm tra lọc mã/tên, năm, phòng ban, loại, người lập/nhận, trạng thái hạn. Kiểm tra thống kê năm/phòng ban và xuất CSV.
3. Với hồ sơ `SoNamBaoQuan = NULL` xem nhãn `Vĩnh viễn`. Hồ sơ có thời hạn tính từ thời điểm **Văn thư tiếp nhận**, cảnh báo sắp hết hạn trong vòng 90 ngày. Không tự động xóa.
4. Người lập đề xuất đổi thời hạn tại tab **Bảo quản**; Văn thư cùng cơ quan duyệt/từ chối. Kiểm tra lịch sử và thời hạn sau quyết định.

## Kiểm thử khai thác và quyền

1. Người dùng có quyền `dossiers.manage` thuộc cùng cơ quan chọn hồ sơ đã tiếp nhận và gửi yêu cầu khai thác kèm lý do.
2. Văn thư xem tab **Khai thác**, duyệt thời hạn 1–72 giờ hoặc từ chối.
3. Người được cấp quyền chọn **Xem tài liệu được cấp**, mở bản PDF đã ký và tải tệp đính kèm. API kiểm tra mã băm SHA-256 trước khi trả tệp.
4. Văn thư thu hồi quyền. Thử gọi lại API tài liệu phải trả `403`; kiểm tra `NhatKyKhaiThacHoSo` ghi lịch sử xem/tải.
5. Thử tài khoản cơ quan B truy cập mã yêu cầu cơ quan A; phải từ chối. Tài khoản không có quyền hoặc hết thời gian không tải được.

## Kiểm tra toàn vẹn

1. Văn thư hoặc người lập hồ sơ nhấn **Kiểm tra toàn vẹn** trên hồ sơ đã tiếp nhận.
2. Hệ thống so sánh SHA-256 PDF đã ký với mã lưu, xác minh tính toàn vẹn mật mã của chữ ký; so sánh SHA-256 tệp đính kèm. Thiếu PDF ký được ghi vào số lượng cần kiểm tra.
3. Kiểm tra **Nhật ký truy cập → Xuất CSV**. Không ghi hoặc hiển thị đường dẫn vật lý lưu tệp cho người dùng.

## Giới hạn và an toàn

- Việc kiểm tra chữ ký CMS trên PDF chỉ đánh giá toàn vẹn mật mã, **không** công nhận chứng thư tự ký là chứng thư CA hợp pháp.
- Không tự động tiêu hủy hồ sơ quá hạn. Gia hạn phải được Văn thư phê duyệt.
- Phiên bản này giữ nguyên quy trình ký/phát hành, hồ sơ giấy và dữ liệu liên kết trước đó.
- Cần nghiệm thu .NET build và SQL trên máy Windows thật; môi trường dựng giao diện đã xác minh `npm run build`.
