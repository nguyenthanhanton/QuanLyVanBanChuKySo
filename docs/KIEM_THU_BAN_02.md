# Kịch bản kiểm thử tích hợp cho bản 0.2

Tài liệu hướng dẫn **cần thực thi** trên Windows/.NET 8/SQL Server. Các trường hợp bên dưới chưa được xác nhận bằng kết quả chạy trong môi trường xây dựng hiện tại.

## A. Thiết lập an toàn

1. Tạo **SQL Server database thử nghiệm mới** bằng `001`, chạy `002`, `004`, `005`, `006`, sau cùng chạy truy vấn `003` (trong `README.md`). Nếu đang ở bản 0.3 đã chạy `005`, **chỉ chạy 006**. Không chạy seed `002` trên hệ thống có tài khoản thật.
2. Cấu hình `Storage__RootPath`, `Signing__KeyStorePath`, `Signing__FontPath`, `Security__EncryptionKeyBase64`, `Security__PfxPassword`, `Security__ActiveKeyId`, JWT qua biến môi trường. Khóa mã hóa cần sao lưu độc lập.
3. Khởi động API bằng `dotnet run`; kiểm tra `GET /api/v1/health` và mã HTTP 200.
4. Khởi động React với `VITE_DEMO_MODE=false`, đăng nhập từng tài khoản ở các cửa sổ riêng.

## B. Luồng A → B

| # | Người dùng | Thao tác | Kết quả phải thấy |
|---|---|---|---|
| 1 | `chuyenvien_a` | Chọn PDF ở **Văn bản chính**, Word/Excel ở **Tệp đính kèm**, chọn cơ quan B | `VanBan.TrangThai=NHAP`, có ba tệp, đúng một PDF chính, một nơi nhận |
| 1a | Người tạo | Mở tab **Phiên bản**, bỏ Excel cũ, thêm Excel mới, sửa trích yếu | Có phiên bản 2, tệp cũ ở phiên bản 1, tiêu đề giữ nguyên |
| 2 | `chuyenvien_a` | Trình duyệt | `CHO_DUYET`, tạo công việc cho trưởng phòng |
| 3 | `truongphong_a` | Duyệt nội dung | Chuyển công việc kiểm tra thể thức |
| 4 | `truongvanphong_a` | Duyệt thể thức | Chuyển lãnh đạo duyệt |
| 5 | `lanhdao_a` | Duyệt | `CHO_CAP_SO` |
| 6 | `vanthu_a` | Cấp số / ký hiệu | `DA_CAP_SO`, sổ văn bản đi tăng 1 |
| 7 | Vai trò có thẩm quyền | Nhập PFX/P12 hoặc tạo chứng thư **thử nghiệm** | `ChungThuKy` có cá nhân và cơ quan tách biệt |
| 8 | `vanthu_a` | Chuẩn bị PDF | `CHO_KY`, `BanPdfDaKy.ThuTuKy=0`, PDF có chân trang số/ký hiệu |
| 9 | `truongphong_a` | Ký nháy | `ThuTuKy=1`, `ChuKySo` ghi chứng thư và CMS |
| 10 | `lanhdao_a` | Ký lãnh đạo | `ThuTuKy=2`, chữ ký 1 còn kiểm tra được |
| 11 | `vanthu_a` | Ký cơ quan | `DA_KY`, `ThuTuKy=3`, cả 3 chữ ký kiểm tra toàn vẹn |
| 12 | `vanthu_a` | Phát hành | `DA_PHAT_HANH`, `BanPhatHanh` được tạo, lượt chuyển `CHO_GUI` |
| 13 | `vanthu_a` | Gửi | `DA_GUI`, ghi thời điểm gửi; B mới có thể tải PDF |
| 14 | `vanthu_b` | Mở văn bản đến, tải **tất cả tệp PDF/Word/Excel** và PDF đã ký, Tiếp nhận | Đọc được bản tệp chốt lúc phát hành; `DA_TIEP_NHAN`, tạo `VanBanDen` trong phạm vi B |
| 15 | `vanthu_a` | Mở Hành trình văn bản | Thấy nơi nhận B, giờ gửi, trạng thái tiếp nhận; **không** thấy việc nội bộ của B |

## C. Trường hợp bắt buộc từ chối

- Đăng nhập `quantrihethong`: gọi trực tiếp `GET /api/v1/documents` phải nhận 403; không được tự xem file.
- `chuyenvien_a` yêu cầu ký cơ quan: 403.
- `vanthu_b` yêu cầu xem file PDF của văn bản A **trước khi A gửi**: 404.
- Cố phát hành chỉ sau 1–2 chữ ký: 409.
- Cố ký bước 2 trước bước 1: 409.
- Sử dụng cùng `MaYeuCau` hai lần ở API ký/gửi/phát hành: không tạo hai file hoặc hai bản phát hành.
- Chỉnh một byte trong file PDF đã ký: kiểm tra hash trước tải/gửi phải từ chối; chức năng xác minh PDF độc lập phải báo chữ ký không còn hợp lệ. Không sửa tài liệu sản xuất để thử.
- Cố sửa văn bản sau khi cấp số/ký/phát hành: 409; không được thay thế bản phát hành.
- Cố đổi `VanBan.TieuDe` bằng API hoặc SQL `UPDATE`: bị từ chối, kể cả đang nháp.
- Cố tải bản chính là Word/Excel: HTTP 400, vì văn bản chính chỉ được phép PDF.
- Thay PDF chính khi chưa bỏ PDF chính cũ: HTTP 400; bỏ PDF chính mà không chọn PDF khác: HTTP 400.
- Mở xem trước PDF trên browser; `.docx`/`.xlsx` xem nội dung chữ/bảng qua API có kiểm tra quyền; `.doc`/`.xls` hiện cần tải về.
- Chỉ còn tệp Word/Excel không có PDF: tạo/sửa bản nháp vẫn được, nhưng **chuẩn bị PDF để ký** phải từ chối.
- Tải tệp của cơ quan A từ cơ quan B trước khi gửi: 404; danh sách tệp bản nháp không được lộ.
- Sửa/bỏ tệp ở bản nháp phải tạo phiên bản mới, không làm hỏng bản cũ; thử gửi và tải tệp đính kèm kiểm tra SHA-256.
- B từ chối một lượt đang `DA_GUI`: A thấy lý do và trạng thái `TU_CHOI`; B không có `VanBanDen` đã tiếp nhận.
- Lặp thao tác Tiếp nhận: không tạo thêm `VanBanDen`.
- Dùng cùng PFX cho người khác hoặc khóa mã hóa sai: không được ký, không được lộ PFX qua API.

## D. Điểm dừng trước sản xuất

- Build/test API và migration trên SQL Server thật, thêm integration tests tự động.
- Đối chiếu PDF bằng trình xác minh chữ ký độc lập, không chỉ trả về kết quả của iText.
- Thẩm định chứng thư CA, chính sách CRL/OCSP/TSA, lưu khóa trên USB token/HSM hoặc ký từ xa nếu nghiệp vụ yêu cầu.
- Kiểm thử backup/restore đồng bộ SQL, PDF và kho PFX mã hóa; giới hạn file-access ở hệ điều hành.
- Kiểm tra quyền người dùng, dữ liệu mật, audit, khả năng khôi phục và hiệu lực pháp lý trước sử dụng thực tế.
