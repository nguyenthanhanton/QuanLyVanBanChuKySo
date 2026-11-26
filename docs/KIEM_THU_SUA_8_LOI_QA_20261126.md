# Nghiệm thu bản vá tám lỗi QA — 26/11/2026

Đối chiếu với `KET_QUA_KIEM_THU_TOAN_HE_THONG.md` và
`KET_QUA_KIEM_THU_CHI_TIET.csv` tại commit `d489f692`.

## Chuẩn bị

1. Sao lưu SQL Server và xác nhận đang dùng **database kiểm thử**, không phải sản xuất.
2. Chạy duy nhất `database/026_nhat_ky_khai_thac_van_ban_den.sql` trên DB đã có schema 025.
   Migration chỉ thêm cột nullable và khóa ngoại cho nhật ký khai thác hồ sơ đến;
   không chỉnh sửa hoặc xóa dữ liệu cũ. Chạy lại an toàn nếu đã áp dụng.
3. `cd backend && dotnet build`, sau đó khởi động backend bằng cấu hình bảo mật đã lưu
   (`start-backend.ps1` ở thư mục gốc trên Windows). Không tạo khóa AES/PFX mới.
4. `cd web && npm ci && npm run build && npm run dev`.
5. Dùng fixture QA hiện có. Không chạy lại toàn bộ harness ghi dữ liệu đã hoàn tất;
   chỉ chọn các ca cần tái kiểm. Không thử đọc PDF không thuộc phạm vi dữ liệu QA.

## Kiểm thử các lỗi đã sửa

| Bug | Thao tác | Kết quả mong đợi |
|---|---|---|
| QA-BUG-001 / TC-SEC-06 | Đăng nhập user cùng cơ quan nhưng khác phòng, không được giao văn bản. Thử GET `/api/v1/documents/{id}`, `/signed-pdf`, `/signatures/verification`. Sau đó thử người soạn và người được giao còn quyền. | Tài khoản không có quyền: cả ba 404, không nhận byte PDF/chữ ký. Người được phép: xem/tải được theo đúng phạm vi. |
| QA-BUG-002 / TC-OUT-05, TC-SEC-05 | Tải `%PDF-1.7` + nội dung giả hỏng vào văn bản chính, rồi thử một PDF hợp lệ. | PDF hỏng trả 400 với thông báo cấu trúc; không ghi thêm văn bản/phiên bản bị hỏng. PDF hợp lệ tạo được và ký bình thường. |
| QA-BUG-003 / TC-ARC-07 | Mở hồ sơ **đến** đã nộp/nhận lưu, có 1 PDF phát hành đủ chữ ký và 3 tệp phụ; nhấn Kiểm tra toàn vẹn. | `Tong=4`, `HopLe=4`, `CanKiemTra=0`, `ThieuPdfDaKy=0` khi đủ tệp và hash/CMS đều đúng. Làm hỏng **bản sao fixture** phải hiện lỗi; không sửa dữ liệu lưu trữ. |
| QA-BUG-004 / TC-ARC-05 | Xin/cấp quyền khai thác hồ sơ đến; mở Xem tài liệu được cấp; mở PDF và tải từng tệp phụ. Sau đó thu hồi quyền, thử lại. | Danh sách có dòng nguồn `DEN`; PDF là bản **đã ký theo snapshot phát hành**, ảnh/Word/Excel/PDF phụ tải đúng, ghi audit có MaVanBanDen. Sau thu hồi các đường xem/tải trả 403. |
| QA-BUG-005 / TC-ARC-02 | Tra cứu hồ sơ đến chứa 1 văn bản. | `SoVanBan=1`; hồ sơ hỗn hợp đi+đến đếm tổng hai nguồn. |
| QA-BUG-006 / TC-INF-07, TC-UI-05 | Dừng backend 5000 (giữ Vite chạy) rồi F5 trang với phiên còn hợp lệ; khởi động lại backend. | Proxy trả HTTP 502 + JSON thông báo không thể kết nối API; frontend hiển thị lỗi kết nối, không ép logout. Backend chạy lại thì tải dữ liệu được. |
| QA-BUG-007 / TC-MON-06 | Đăng nhập văn thư B có 3 văn bản đến `HOAN_THANH`, mở Văn bản đến. | Tổng 3, Đang xử lý 0, Hoàn thành 3. Các bản đang thực hiện vẫn tính vào Đang xử lý. |
| QA-BUG-008 / TC-OUT-02 | Tạo văn bản phiên bản 1, trả sửa và thay PDF để thành phiên bản 2, ký/phát hành/gửi/tiếp nhận. Mở Hành trình. | Các sự kiện `CAP_SO`, `TAO_LUOT_CHUYEN`, `GUI_VAN_BAN`, `TIEP_NHAN` ghi `SoPhienBan=2`; chữ ký/hash PDF vẫn đúng. Chỉ áp dụng cho lịch sử **mới**; lịch sử cũ không tự cập nhật. |

## Kiểm tra hồi quy bắt buộc

- Cùng cơ quan nhưng khác phòng: không được tải PDF; văn thư/lãnh đạo/người soạn/
  người được giao thực sự: vẫn được xem khi đúng quyền. Test cả ủy quyền ký còn hạn,
  đã thu hồi, và quyền khác cơ quan.
- Hồ sơ **đi**, **đến**, và **hỗn hợp**: danh sách, tải PDF, tải tệp phụ, SHA-256,
  kiểm tra chữ ký, nhật ký và thao tác sau thu hồi quyền.
- Quyền khai thác phải bị từ chối nếu chưa duyệt, đã hết hạn hoặc đã thu hồi;
  người khác không mượn được request ID của chủ quyền.
- Không sửa/chuyển dữ liệu lịch sử để “sửa số phiên bản”; cần kế hoạch riêng có backup
  và bằng chứng provenance nếu muốn chỉnh bản ghi cũ.

## Điều kiện kết luận

Chỉ ghi **ĐẠT 8 lỗi đã sửa** khi backend build thành công và chạy lại các case trên
trên SQL Server test theo đúng vai trò. Bản vá này **không** thay kết luận toàn hệ thống:
14 ca NOT_RUN và 4 ca BLOCKED trong báo cáo vẫn cần nghiệm thu riêng.
