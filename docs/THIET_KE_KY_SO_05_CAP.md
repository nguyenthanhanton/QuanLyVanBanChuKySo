# Phiên bản 0.5 — Quy trình năm vị trí ký PDF

**Trạng thái:** thiết kế kỹ thuật đã chốt yêu cầu; chưa bật luồng ký mới trên API sản xuất/thử nghiệm.

## 1. Năm vị trí, bắt buộc đúng người, đúng thứ tự

| ThuTuKy | LoaiChuKy | Vai trò / người ký | Điều kiện bắt buộc | Hành động mở khóa |
|---|---|---|---|---|
| 1 | `KY_NGUOI_SOAN` | Người tạo văn bản (`VanBan.MaNguoiTao`), được quyền `signing.sign` | `NHAP`, đúng phiên bản, có chứng thư cá nhân | Cho phép `Trình duyệt` |
| 2 | `KY_NHAY` | Người được giao bước `KIEM_TRA_NOI_DUNG` có vai trò `TRUONG_PHONG` | `CHO_DUYET`, đúng `MaTacVu` và phiên bản | Cho phép duyệt/chuyển bước trưởng văn phòng |
| 3 | `KY_THE_THUC` | Người được giao bước `KIEM_TRA_THE_THUC` có vai trò `TRUONG_VAN_PHONG` | `CHO_DUYET`, đúng nhiệm vụ | Cho phép duyệt/chuyển lãnh đạo |
| 4 | `KY_LANH_DAO` | Người được giao bước `PHE_DUYET_LANH_DAO` có vai trò `LANH_DAO` | `CHO_DUYET`, đúng nhiệm vụ | Cho phép phê duyệt/cấp số |
| 5 | `KY_CO_QUAN` | Văn thư, chứng thư loại `CO_QUAN` | `DA_CAP_SO`, số/ký hiệu được xác lập, 4 chữ ký hợp lệ | Cho phép phát hành |

Mỗi bước yêu cầu chứng thư hợp lệ tại thời điểm ký, mật khẩu tài khoản, đúng `MaNguoiDung`, hash PDF đầu vào khớp `MaBamSha256`, và khóa yêu cầu `MaYeuCau` để tránh ký trùng.

## 2. Vòng đời tài liệu và bản ký

1. Tạo nháp: một PDF chính và 0–19 tệp kèm; tên văn bản đã tạo không sửa.
2. Chuẩn bị PDF từ PDF chính **trước lần ký thứ nhất**, không chèn số chính thức tại bước này; lưu `BanPdfDaKy.ThuTuKy=0` trên phiên bản hiện tại.
3. Người soạn ký (1), sau đó `Trình duyệt`. API submit kiểm tra chữ ký 1 đúng phiên bản.
4. Trưởng phòng ký nháy (2) rồi duyệt; trưởng văn phòng ký (3) rồi duyệt; lãnh đạo ký (4) rồi duyệt.
5. Văn thư cấp số sau lãnh đạo. Ghi số vào database. Nếu bắt buộc có số trên PDF, **phải bổ sung số/ký hiệu qua cập nhật PDF tăng dần (incremental update)**, không sửa trực tiếp các byte đã ký. Chỉ thực hiện sau khi kiểm thử độc lập rằng cả 4 chữ ký trước vẫn được xác minh đúng revision và không mất dấu.
6. Văn thư ký cơ quan (5) vào bản PDF cuối cùng; ký lần cuối phải bao phủ toàn bộ bản phát hành. Chỉ sau đó phát hành/gửi PDF đã ký và các tệp kèm.
7. Mỗi bước giữ bản PDF bất biến, dấu hash và chuỗi `MaBanTruoc`; tệp đã ký không được ghi đè.

**Điểm chốt kỹ thuật trước khi bật:** số/ký hiệu cuối trang của văn bản không được sửa byte PDF đã ký. Nếu phép cập nhật tăng dần làm trình xác minh báo tài liệu bị sửa không được phép, phải điều chỉnh quy trình cấp số (chẳng hạn giữ/chốt số trước chữ ký đầu tiên) và được người dùng duyệt lại.

## 3. Trả sửa, phiên bản và nhất quán

- Trả sửa kết thúc nhiệm vụ hiện tại; chỉ người tạo được sửa bản nháp.
- Lưu sửa đổi tạo `PhienBanVanBan` mới và sao lưu danh sách tệp của phiên bản đó; không chỉnh bản cũ.
- Không kế thừa chữ ký từ phiên bản trước. Phiên bản mới bắt đầu lại từ bước 0 và ký người soạn lần nữa.
- Tất cả API ký/duyệt/submit phải kiểm tra trạng thái, phiên bản, nhiệm vụ người xử lý và trình tự chữ ký **trong transaction**.
- Tệp phiên bản cũ và mọi chữ ký cũ còn nguyên để truy vết, không được sử dụng để vượt qua kiểm tra phiên bản mới.
- Phát hành xác minh 5 CMS, thứ tự ký, hash bản cuối, quyền cơ quan, số/ký hiệu và snapshot tệp kèm.
- Nếu một cấp từ chối trước khi ký, được từ chối; không có cách duyệt chuyển tiếp nếu chưa ký.

## 4. Phạm vi triển khai theo chặng

| Chặng | Nội dung | Điều kiện nghiệm thu |
|---|---|---|
| **0.5.1** | Lược đồ cho 5 lần ký; quyền ký của chuyên viên; thiết kế và ca kiểm thử | Migration 007 chạy an toàn, không đổi hành vi 0.4 |
| **0.5.2** | Chuẩn bị PDF lúc nháp, ký người soạn, khóa submit đến khi ký xong | Không thể submit nếu thiếu chữ ký 1 |
| **0.5.3** | Ký nháy/trưởng văn phòng/lãnh đạo theo nhiệm vụ được giao, khóa quyết định duyệt | Không thể duyệt khi thiếu chữ ký đúng cấp |
| **0.5.4** | Cấp số không phá chữ ký, ký cơ quan lần 5, phát hành có kiểm chứng | Test 5 chữ ký trên PDF và công cụ xác minh độc lập |
| **0.5.5** | Giao diện thao tác theo vai trò, lịch sử ký/luân chuyển, E2E A→B,C | Không lộ dữ liệu nội bộ, không vượt quyền |

## 5. Kiểm thử phải có

1. `chuyenvien_a` đúng tài liệu ký 1, `vanthu_a` chỉ được ký 1 nếu **chính là người tạo**; người khác 403/409.
2. `POST /documents/{id}/submit` trước chữ ký 1 → 409; sau chữ ký 1 → `CHO_DUYET`.
3. Trưởng phòng không có nhiệm vụ → từ chối ký 2; ký 2 xong mới quyết định `DUYET`.
4. Bỏ qua bước 3 hoặc 4 → 409; văn thư ký 5 khi chưa có số → 409.
5. Dùng lại `MaYeuCau`, ký hai lần cùng bước → không sinh thêm tệp/chuỗi ký.
6. Trả sửa ở mỗi cấp, lưu phiên bản mới: 5 bước ký của phiên bản cũ không hợp lệ để duyệt bản mới.
7. Đối chiếu 5 chữ ký bằng thư viện khác, cả hash từng bản ký và dấu thay đổi PDF sau cấp số.
8. Cơ quan B/C trước phát hành không xem được bản nháp; sau gửi chỉ nhận PDF 5 chữ ký và các phụ lục của phiên bản phát hành.

## 6. Giới hạn hiện tại

Kết quả `HOP_LE_MAT_MA` chỉ xác nhận tính toàn vẹn mật mã trong phần mềm; không tự khẳng định chứng thư số có độ tin cậy pháp lý. Quy trình mới chỉ được bật sau kiểm thử thực tế .NET 8, SQL Server và công cụ PDF độc lập.
