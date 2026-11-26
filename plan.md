# PLAN — Kế hoạch kiểm thử và quy trình nghiệp vụ toàn hệ thống Văn thư số

> **Dành cho Codex agent kiểm thử độc lập.** Đọc hết tài liệu trước khi chạy. Đây là **bản giao việc kiểm thử, không phải xác nhận hệ thống đã ĐẠT**.
>
> Ngày lập: 26/11/2026 · Repo: `nguyenthanhanton/QuanLyVanBanChuKySo` · Nhánh: `feat/v0.5-ky-so-nam-cap` · Phạm vi: toàn bộ tính năng hiện có đến phần quản trị hệ thống / cập nhật thông tin cơ quan / giữ phiên F5.

## A. Lệnh giao việc cho Codex (copy nguyên mục này vào phiên agent)

Bạn đóng vai QA lead kiêm kiểm thử tích hợp của hệ thống quản lý văn bản và chữ ký số. Hãy:

1. Đọc `plan.md`, `README.md`, `backend/Program.cs`, `backend/*Endpoints.cs`, `database/001`–`025`, `web/src/` và các tài liệu `docs/KIEM_THU_*.md` có liên quan; đối chiếu tài liệu với **mã nguồn thực tế**.
2. Kiểm tra điều kiện môi trường và backup. **Không chạy migration, seed, tạo/sửa/khóa tài khoản, ký số, phát hành, nộp lưu hoặc ghi database thật trước khi xác nhận đây là môi trường test đã được chủ sở hữu cho phép.** Nếu chưa xác nhận, chỉ thực hiện kiểm tra tĩnh, build và trả về `BỊ CHẶN` cho ca cần ghi dữ liệu.
3. Thực thi theo thứ tự các cổng kiểm thử ở mục D, từng ca trong mục E. Có thể dùng UI, API hoặc SQL **chỉ đọc** để xác minh; không lấy phản hồi HTTP 200 thay cho kiểm tra kết quả thực tế.
4. Với lỗi, ghi rõ tiền điều kiện, tài khoản/vai trò, mã trường hợp, bước tái hiện, HTTP status/response đã lược bỏ dữ liệu nhạy cảm, log liên quan, kết quả kỳ vọng và thực tế. Không tự vá mã nguồn hoặc cấu trúc dữ liệu; đề xuất bản vá riêng khi người dùng cho phép.
5. Tạo báo cáo `docs/KET_QUA_KIEM_THU_TOAN_HE_THONG.md` và bảng `docs/KET_QUA_KIEM_THU_CHI_TIET.csv` theo mẫu mục G. Báo cáo có tóm tắt, mức độ rủi ro, danh sách blocker, bằng chứng và đường dẫn tái hiện; thông báo trong chat cho chủ dự án số lượng ĐẠT/KHÔNG ĐẠT/BỊ CHẶN/CHƯA CHẠY và 5 lỗi ưu tiên nhất.
6. Nếu thiếu quyền truy cập, tài khoản, khóa giải mã chứng thư, dữ liệu PDF hay SQL Server, **dừng đúng nhóm ca bị chặn và nêu đầu vào cần cung cấp**. Không bịa kết quả, không thử đoán mật khẩu, không tự cấp thêm quyền.

## B. Mục tiêu nghiệm thu, giới hạn, nguyên tắc an toàn

### B1. Các cổng chất lượng

- **G0 — Môi trường:** repo đúng nhánh, config bí mật được cấu hình đúng, DB backup riêng, API và giao diện kết nối được.
- **G1 — Build/database:** backend build thành công, frontend build thành công, migration đúng thứ tự đến `025`; không có sai cấu trúc cột.
- **G2 — Quyền:** hệ thống/agency admin, lãnh đạo, trưởng/phó phòng, chuyên viên, văn thư, người nhận liên cơ quan được phân tách đúng.
- **G3 — Văn bản đi:** PDF chính duy nhất được ký tăng dần; 4 chữ ký cho quy trình phòng ban thông thường, 3 chữ ký khi lãnh đạo tự soạn; dữ liệu không bị mất.
- **G4 — Văn bản đến:** gửi → tiếp nhận → cấp số đến → lãnh đạo giao phòng → trưởng phòng giao cá nhân → báo cáo → kết luận/hoàn thành.
- **G5 — Hồ sơ:** đưa văn bản hợp lệ vào hồ sơ, đóng, nộp, văn thư tiếp nhận, khai thác/bảo quản, kiểm tra toàn vẹn.
- **G6 — Trải nghiệm và an toàn:** lỗi có thông điệp đúng, thông báo điều hướng đúng, phân trang >15 bản, phiên F5, tải file và từ chối truy cập trái quyền.

### B2. Vùng cấm và phạm vi thao tác

- Chỉ chạy thử nghiệp vụ **trên database/kho tệp test đã được cấp phép**; tuyệt đối không xóa/reset database, không chạy lại `001`/`002` trên DB có dữ liệu, không chạy `git reset --hard`/`git push --force`.
- Các khóa `Jwt__Key`, `Security__EncryptionKeyBase64`, `Security__ActiveKeyId`, `Security__PfxPassword`, `Signing__KeyStorePath` và kho PFX là bí mật. **Không ghi giá trị vào chat, báo cáo, log, ảnh hoặc commit.** Chỉ báo `đã cấu hình/chưa cấu hình`.
- Không tạo lại AES key hay đổi `ActiveKeyId` để chữa lỗi ký; thay khóa có thể làm chứng thư đang lưu không giải mã được. Dùng chứng thư tự ký **chỉ trong test**, không kết luận có giá trị pháp lý/CA tin cậy.
- Không xóa chứng thư, hồ sơ, PDF đã ký, nhật ký nghiệp vụ hay văn bản thật. Không gửi văn bản thử đến cơ quan đang dùng dữ liệu thật. Test thay đổi trạng thái/khóa tài khoản cần **tài khoản test**, có phương án khôi phục hợp lệ.
- Dùng tiền tố dữ liệu `QA_E2E_YYYYMMDD_*` và liệt kê `MaCoQuan`, `MaVanBan`, `MaVanBanDen`, `MaHoSo`, `MaTacVu` được tạo để đối chiếu; không làm trùng với dữ liệu cũ.
- Chỉ đọc SQL khi chưa có xác nhận bằng văn bản về test DB. Việc test security dùng tài khoản được cung cấp, không dò mật khẩu hay nâng quyền.

## C. Mô tả quy trình nghiệp vụ chuẩn

### C1. Mô hình tổ chức & quyền quản trị

| Vai trò | Chức năng chính | Giới hạn |
|---|---|---|
| `SYSTEM_ADMIN` | Tạo/sửa/ngừng cơ quan; tạo/khóa quản trị cơ quan; nhật ký kỹ thuật | Không mặc nhiên xem văn bản nghiệp vụ |
| `AGENCY_ADMIN` | Tạo/khóa nhân viên; quản lý phòng, chức vụ, quyền riêng; đổi **tên và địa chỉ** cơ quan mình | Không quản trị cơ quan khác, không tự cấp quyền hệ thống |
| `LANH_DAO` / `PHO_LANH_DAO` | Ký chính thức; chỉ đạo văn bản đến; có thể soạn văn bản | Lãnh đạo soạn văn bản không qua trưởng phòng ký nháy |
| `TRUONG_PHONG` / `PHO_PHONG` | Ký nháy/kiểm tra khi được chọn; giao việc đến cá nhân thuộc phòng | Phó phòng/trưởng phòng soạn theo quy tắc chọn người ký khác mình |
| `CHUYEN_VIEN` | Soạn, ký người soạn; xử lý việc được giao; báo cáo | Không tự duyệt/phát hành nếu không được giao quyền hợp lệ |
| `VAN_THU` | Ký số cơ quan, cấp số/phát hành; nhận/cấp số đến; nộp lưu và tiếp nhận hồ sơ theo quy định | Mỗi cơ quan tối đa **một Văn thư đang hoạt động**, thuộc Ban lãnh đạo |

- Ban lãnh đạo có trưởng và các cấp phó. Phòng thông thường có trưởng và các cấp phó; tên chức danh hiển thị (VD: `Trưởng văn phòng`, `Phó viện trưởng`) là trường đặt tên riêng, không thay thế mã vai trò chuẩn.
- **Trực thuộc phòng ban chỉ là quan hệ tổ chức**, không tự động thêm tầng ký. Thứ tự hiển thị quản trị: Ban lãnh đạo trước, các phòng sau; trong phòng xếp người theo chức vụ cao → thấp.
- Khi `SYSTEM_ADMIN` tạo cơ quan: cần có Ban lãnh đạo, Văn phòng, cấu hình quy trình và quyền mẫu; tiếp theo tạo `AGENCY_ADMIN`. Admin cơ quan mới tạo phòng/nhân viên theo tổ chức thực tế.

### C2. Văn bản đi — từ dự thảo đến nơi nhận

1. **Người soạn** chọn người nhận, nhập tiêu đề/trích yếu, tải **PDF chính duy nhất** và tệp đính kèm Word/Excel/PDF (nếu có). Trước khi trình duyệt được thay tệp, tạo phiên bản mới nếu nội dung thay đổi; không sửa nội dung của phiên bản đã ký.
2. **Ký người soạn:** mở trực tiếp PDF, tải ảnh chữ ký PNG tối đa 5 MB, kéo/thả, chỉnh vị trí và kích thước, xác nhận rồi ký số bằng chứng thư của mình. Kiểm tra ảnh hiển thị đúng trên **chính PDF đó**, không tồn tại hai bản chính (một có ảnh, một không).
3. **Trình duyệt:** chọn đúng người ký nháy và lãnh đạo ký chính thức; danh sách lọc theo phòng ban, chức vụ, người soạn, vai trò trưởng/cấp phó. Văn thư trực thuộc Ban lãnh đạo được chọn một trưởng phòng đủ điều kiện trong cơ quan cho bước ký nháy. Khi người soạn là lãnh đạo/cấp phó lãnh đạo, **bỏ bước trưởng phòng**, chọn lãnh đạo phù hợp khác người soạn.
4. **Trưởng/phó phòng được chọn:** mở đúng tác vụ, kiểm tra, ký trên PDF hiện có rồi phê duyệt/chuyển bước hoặc trả sửa có lý do. Quy trình thông thường **4 vị trí chữ ký**: người soạn → trưởng/phó phòng → lãnh đạo → văn thư ký cơ quan.
5. **Lãnh đạo được chọn:** ký chính thức trên cùng PDF, chuyển văn thư; đường đi lãnh đạo tự soạn thông thường **3 vị trí**: người soạn (lãnh đạo) → lãnh đạo khác → văn thư cơ quan. Không áp dụng máy móc bước kiểm tra phòng.
6. **Văn thư:** cấp số/ký hiệu, ký số cơ quan/con dấu trên cùng PDF, phát hành, gửi đến cơ quan nhận đã chọn. Không phát hành khi thiếu chữ ký hợp lệ/tác vụ chưa hoàn tất.
7. **Văn thư cơ quan nhận:** xem bản PDF cuối đã ký và tệp đính kèm, chọn tiếp nhận hoặc từ chối có lý do; hệ thống lưu biên nhận, thời điểm, người nhận, lịch sử và mã băm.
8. Tại mọi bước: **Hành trình văn bản**, phiên bản, xác minh chữ ký và PDF đã ký phản ánh đúng người/bước/thời điểm; văn bản cũ theo quy trình 5 bước không tự bị đổi trạng thái/số bước đã lưu.

### C3. Ký thay theo tác vụ

- Quản trị cơ quan chỉ ủy quyền cho **tác vụ đang chờ cụ thể**, có lý do và thời hạn; người ký thay đủ điều kiện về chức vụ/phòng ban, không phải người soạn hoặc đã ký cùng phiên bản.
- Trưởng được ủy quyền cho cấp phó cùng phòng/ban; cấp phó có thể ký thay đúng quy tắc ủy quyền áp dụng. Chỉ ủy quyền bước kiểm tra nội dung hoặc ký lãnh đạo; không ủy quyền chữ ký người soạn và con dấu văn thư.
- PDF lưu chứng thư và tên **người thực sự ký**; sự kiện ủy quyền, thu hồi, hết hạn có nhật ký. Không tự làm thêm một bước chữ ký; không thay đổi tổng số bước ký.

### C4. Văn bản đến — điều hành xử lý một phòng chủ trì, nhiều phòng phối hợp

1. Văn thư nhận bản phát hành, có thể mở **PDF đã ký và mọi tệp đính kèm**.
2. Văn thư **cấp số đến** (theo cơ quan/năm/sổ) và **trình lãnh đạo**. Trạng thái `Chưa cấp số` chỉ có nghĩa chưa hoàn thành đăng ký số đến, không có nghĩa chưa tiếp nhận từ bên gửi.
3. Lãnh đạo đọc PDF đã ký, chọn **một phòng chủ trì bắt buộc**, có thể chọn nhiều phòng phối hợp, hạn xử lý và ý kiến chỉ đạo.
4. Trưởng/phó phòng của **mỗi phòng được giao** phân công **cá nhân chủ trì cụ thể** và các cá nhân phối hợp tùy chọn. Hiển thị họ tên **kèm chức vụ**. Sau khi giao, muốn đổi người phải có **lý do giao lại** và lưu lịch sử, không âm thầm sửa người được giao.
5. Cá nhân được giao xem văn bản, thực hiện nhiệm vụ và nộp báo cáo; có thể đính kèm ảnh minh chứng PNG/JPG/WEBP (tối đa 5 MB/ảnh). Các bên được phép xem/tải minh chứng theo quyền.
6. Cấp có thẩm quyền theo dõi báo cáo các phòng; mục kết thúc là **Kết luận cấp trên**; quyết định hoàn thành phải có đủ điều kiện nghiệp vụ và lịch sử.
7. Văn bản đến **đã hoàn thành** có thể được đưa vào **hồ sơ lưu trữ** cùng cơ quan, mở được PDF đã ký và tài liệu đính kèm; không nhầm ID văn bản đến với ID văn bản đi.

### C5. Hồ sơ lưu trữ và khai thác

1. Người có quyền trong cơ quan tạo hồ sơ (mã, năm, loại, phòng ban, thời hạn/vĩnh viễn); quyền sửa ưu tiên người tạo hoặc admin được phép, quyền xem theo phạm vi hồ sơ và quyền xem từng văn bản.
2. Chọn văn bản đi **đã phát hành** hoặc văn bản đến **đã cấp số và hoàn thành** rồi thêm vào hồ sơ đúng cơ quan; cấm thêm văn bản chưa hợp lệ hoặc không có quyền xem.
3. Đóng hồ sơ để ngăn sửa danh mục; người được phép **nộp lưu**. Văn thư cơ quan nhận hồ sơ, kiểm tra danh mục, **tiếp nhận** hoặc **trả lại** với lý do. Kiểm tra **biên nhận điện tử** và lịch sử nộp lưu.
4. Sau khi tiếp nhận: tra cứu/lọc, thống kê, xuất CSV, đề xuất đổi thời hạn bảo quản, văn thư duyệt/từ chối; kiểm tra cảnh báo sắp hết hạn và hồ sơ vĩnh viễn. **Không tự động tiêu hủy** hồ sơ quá hạn.
5. Người đủ điều kiện **xin khai thác** có lý do; văn thư xét duyệt và cấp quyền có thời hạn. Trong thời gian được cấp, xem PDF đã ký/tải tệp có kiểm tra mã băm; khi hết hạn/thu hồi phải không còn tải được. Ghi nhật ký truy cập, xuất CSV và kiểm tra toàn vẹn.

### C6. Tiện ích chung

- **Việc cần xử lý:** tổng hợp bản nháp tự tạo chưa ký/chưa trình, tác vụ ký/duyệt, việc văn bản đến được lãnh đạo/phòng giao, việc văn thư chờ tiếp nhận/cấp số; nhấn phải mở đúng loại hồ sơ/văn bản.
- **Thông báo:** mỗi sự kiện là một thẻ riêng dễ đọc, hiển thị người/cơ quan, thời gian; nhấn mở đúng chi tiết **văn bản đến** hoặc văn bản đi, không trả lỗi 404 do gọi sai API.
- **Giám sát:** chỉ xem tiến độ, hạn, người xử lý, trạng thái; không tự động thay bước. **Thống kê** và xuất tệp đúng số liệu, đúng quyền.
- **Danh sách:** các phần dài **trên 15 kết quả** phải phân trang, tổng số/điều hướng hợp lệ; lọc/tìm kiếm không làm mất dữ liệu.
- **Tài khoản:** người dùng xem thông tin của mình, chỉ được sửa tên và mật khẩu; các trường chức vụ, phòng ban, cơ quan chỉ xem. `AGENCY_ADMIN` được sửa tên **và địa chỉ** của cơ quan mình; mã cơ quan không đổi.
- **Phiên:** F5/Ctrl+R trong cùng tab giữ đăng nhập và mục đang xem khi JWT còn hạn; đăng xuất xóa phiên; phiên hết hạn/khóa tài khoản phải yêu cầu đăng nhập lại.

## D. Chuẩn bị và lệnh kiểm tra môi trường

### D1. Điều kiện

| Thành phần | Kỳ vọng | Cách kiểm tra |
|---|---|---|
| OS thao tác thật | Windows 10/11 hoặc Windows có SQL Server | Ghi OS vào báo cáo |
| API | .NET 8, ASP.NET Core | `dotnet --info` và `dotnet build` trong `backend` |
| Web | Node/npm, React + Vite | `npm ci`, `npm run build` trong `web` |
| CSDL | SQL Server DB `QuanLyVanBanChuKySo` | SSMS đọc `dbo.PhienBanLuocDo` |
| Web live | `VITE_DEMO_MODE=false` | Kiểm tra `web/.env` **chỉ xác nhận chế độ**, không chụp bí mật |
| API health | `http://127.0.0.1:5000/api/v1/health` | JSON `status: ok` |
| Git | Nhánh `feat/v0.5-ky-so-nam-cap` | `git status`, `git rev-parse HEAD` |
| File/kho chứng thư | Đường dẫn có quyền truy cập đúng | Chỉ kiểm tra tồn tại/khả năng dùng; không tiết lộ giá trị khóa |

Lệnh tham khảo (PowerShell tại **thư mục gốc repo**, không chạy nếu đang có thay đổi chưa commit cần giữ):

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
dotnet build .\backend\QuanLyVanBan.Api.csproj
npm --prefix .\web run build
Invoke-RestMethod http://127.0.0.1:5000/api/v1/health
```

Nếu API chưa chạy, dùng `start-backend.ps1` ở **thư mục gốc repo** (không phải `backend`), với secret config cá nhân đang lưu an toàn. Không tự ghi secret vào `.env` commit được.

SQL **chỉ đọc**, chạy đúng DB đã được xác nhận:

```sql
SELECT TOP 30 SoPhienBan, TenPhienBan, ThoiDiemApDung
FROM dbo.PhienBanLuocDo ORDER BY SoPhienBan DESC;

SELECT MaDinhDanh, TenCoQuan, DangHoatDong
FROM dbo.CoQuan ORDER BY MaCoQuan;

SELECT r.MaDinhDanh, COUNT(*) AS SoTaiKhoan
FROM dbo.NguoiDungVaiTro ur
JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro
GROUP BY r.MaDinhDanh ORDER BY r.MaDinhDanh;
```

- Nếu chưa có migration 1–25 đầy đủ: xác định migration còn thiếu; **xin phép và sao lưu** trước khi áp dụng theo thứ tự, tuyệt đối không dùng seed 002 cho database hiện hữu.
- Nếu không có .NET SDK/SQL Server/API/role test: vẫn được kiểm tra tĩnh, frontend build, kiểm tra code bảo mật; các ca không thể thực thi là `BỊ CHẶN`.

### D2. Bộ tài khoản và tệp thử nghiệm (không chứa mật khẩu)

| Nhóm | Tài khoản seed tham khảo / điều kiện | Phải xác minh trước khi dùng |
|---|---|---|
| Toàn hệ thống | `quantrihethong` | `SYSTEM_ADMIN`, `MaCoQuan IS NULL`, đang hoạt động |
| Quản trị A/B | `quantri_a`, `quantri_b` | Vai trò `AGENCY_ADMIN`, đúng cơ quan |
| Văn thư A/B | `vanthu_a`, `vanthu_b` | `VAN_THU` đang hoạt động, Ban lãnh đạo |
| Lãnh đạo A/B | `lanhdao_a`, `lanhdao_b` | Vai trò `LANH_DAO` đúng cơ quan |
| Trưởng phòng A | `truongphong_a`, `truongvanphong_a` | `TRUONG_PHONG`; tên chức vụ thực tế có thể khác |
| Chuyên viên A/B | `chuyenvien_a`, `chuyenvien_b` | Còn hoạt động và đã phân phòng |
| Cấp phó | Tài khoản `PHO_PHONG` và `PHO_LANH_DAO` được cấp riêng trên DB test | Không tự giả định seed có sẵn |

Chuẩn bị trên môi trường test được cấp phép:

- PDF **hợp lệ** 2–3 trang, nội dung thử; ảnh chữ ký PNG trong suốt dưới 5 MB; chứng thư tự ký thử nghiệm có khả năng giải mã; Word/Excel/PDF tệp đính kèm; ảnh chứng minh JPG/PNG/WEBP dưới 5 MB.
- PDF lỗi cấu trúc/không thể ký, tệp vượt kích thước, ảnh sai định dạng, tên tài khoản trùng, mã cơ quan trùng dùng làm dữ liệu âm tính; tránh file nhạy cảm.
- Cơ quan A và B là hai tenant test độc lập, mỗi tenant có quyền quản trị/người dùng. Dùng cơ quan thứ ba chỉ nếu đã được tạo trong DB test; không dùng cơ quan thật để gửi thử.
- Tối thiểu 16 bản ghi test cùng loại nhằm kiểm tra phân trang; **không tạo 16 bản ghi vào DB thật**.

## E. Danh sách ca kiểm thử tích hợp bắt buộc

**Nguyên tắc thực thi:** Đi từ E01 → E13; mã `TC-*` là định danh cố định. Mỗi ca cần cả API/UI khi liên quan; chụp bằng chứng trước–sau và so khớp DB/nhật ký ở mức **chỉ đọc**. Nếu ca trước bị chặn, đánh dấu ca phụ thuộc `BỊ CHẶN` (không tự đánh `ĐẠT`).

### E01. Hạ tầng, xác thực, phiên (`TC-INF`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-INF-01 | Kiểm tra Git HEAD, cấu hình không chứa secret, build .NET, build web | Cả hai build thành công, không có secret bị commit |
| TC-INF-02 | Kiểm tra DB schema 1–25, bảng/cột liên quan | Phiên bản/migration khớp code, không lỗi `Invalid column name` |
| TC-INF-03 | GET health và đăng nhập tài khoản hợp lệ | Health 200, JWT + danh sách quyền đúng |
| TC-INF-04 | Sai mật khẩu, JWT thiếu/hết hạn/giả | 401/423 theo hợp đồng; không lộ hash mật khẩu |
| TC-INF-05 | Đăng nhập, vào **Việc cần xử lý**, nhấn F5/Ctrl+R | Giữ phiên và đúng trang khi còn hạn |
| TC-INF-06 | Đăng xuất rồi F5; khóa tài khoản rồi F5; token hết hạn | Yêu cầu đăng nhập lại, không truy cập API bằng token hết hạn |
| TC-INF-07 | Mất kết nối backend tạm thời khi F5 | Nêu lỗi kết nối, không xóa phiên chỉ vì sự cố mạng |

### E02. Quản trị hệ thống và quản trị cơ quan (`TC-ADM`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-ADM-01 | `SYSTEM_ADMIN` đăng nhập, mở Quản trị hệ thống | Chỉ menu quản trị hệ thống; danh sách cơ quan đúng |
| TC-ADM-02 | Tạo `QA_E2E_CQ_*`, kiểm tra SQL sau tạo | Cơ quan, Ban lãnh đạo, Văn phòng, quy trình, quyền mẫu được tạo |
| TC-ADM-03 | Tạo trùng mã cơ quan, mã không hợp lệ | Trả lỗi có ý nghĩa, không tạo trùng, không 500 |
| TC-ADM-04 | Cấp `AGENCY_ADMIN` tại cơ quan test, đăng nhập mới | Chỉ có quyền cơ quan vừa cấp, không thấy cơ quan khác |
| TC-ADM-05 | Khóa/mở quản trị cơ quan; ngừng/mở cơ quan với lý do | Trạng thái và audit thay đổi, user bị chặn đăng nhập khi khóa |
| TC-ADM-06 | `AGENCY_ADMIN` đổi tên, địa chỉ cơ quan mình rồi F5 | Cập nhật cả hai; mã bất biến; tất cả panel bằng chiều rộng |
| TC-ADM-07 | Admin cơ quan A gọi `/api/v1/system/agencies` | 403, không liệt kê tất cả cơ quan |
| TC-ADM-08 | `SYSTEM_ADMIN` gọi API đọc văn bản nghiệp vụ | 403, không cấp ngầm `documents.read` |
| TC-ADM-09 | Tạo phòng (thuộc và trực thuộc), chỉnh thứ tự, ngừng/mở phòng | Hiển thị đúng cây tổ chức; chặn vòng lặp và trực thuộc khác cơ quan |
| TC-ADM-10 | Tạo nhân viên, gán phòng, chức vụ, tên gọi chức vụ; cấp phó | Chỉ vai trò phù hợp phòng; lãnh đạo/văn thư thuộc Ban lãnh đạo |
| TC-ADM-11 | Kiểm tra 1 Văn thư đang hoạt động/cơ quan, 1 trưởng phù hợp/phòng | Không cho thêm trưởng/văn thư hoạt động trùng nếu chính sách cấm |
| TC-ADM-12 | Phân quyền cho chuyên viên: Theo vai trò / Cho phép / Từ chối | Nút/API đúng quyền; không cấp quyền hệ thống được bảo vệ |
| TC-ADM-13 | Tạo và quản lý người dùng từ UI trên màn hình hẹp | Ô nhập, bảng danh sách không tràn/mất nút; sắp phòng/chức vụ đúng |

### E03. Hồ sơ người dùng (`TC-ACC`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-ACC-01 | Xem thông tin cá nhân | Tên, login, cơ quan, phòng, chức vụ đúng |
| TC-ACC-02 | Sửa họ tên | Thành công; sidebar/profile đổi; không tự sửa chức vụ/phòng |
| TC-ACC-03 | Đổi mật khẩu: sai mật khẩu hiện tại, mới quá ngắn, mật khẩu hợp lệ | Các ca sai bị từ chối; sau đổi phải đăng nhập theo mật khẩu mới |
| TC-ACC-04 | Thử chỉnh chức vụ, cơ quan bằng API tự phục vụ | Không tồn tại endpoint tự chỉnh hoặc bị từ chối; chỉ admin được phân công |

### E04. Văn bản đi và quản lý tệp (`TC-OUT`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-OUT-01 | Người soạn tạo bản nháp PDF chính + Word/Excel/PDF phụ | DB/tệp ghi đúng tenant; một PDF chính, danh sách phụ chính xác |
| TC-OUT-02 | Sửa/thay PDF chính, thêm/bỏ tệp khi còn nháp | Phiên bản/hash cập nhật và lịch sử giữ được |
| TC-OUT-03 | Người khác, khác cơ quan hoặc sai trạng thái thử sửa | Từ chối 403/404/409 hợp lý; tệp/bản ký không bị đổi |
| TC-OUT-04 | Mở PDF chính, kéo ảnh PNG chữ ký, xác nhận vị trí | Xem trước vị trí chính xác trên đúng trang, kích thước đúng |
| TC-OUT-05 | Ảnh sai định dạng, PNG >5 MB, PDF lỗi | Báo lỗi dễ hiểu, không tạo bản PDF/nhật ký ký thất bại thành công |
| TC-OUT-06 | Ký người soạn trước khi trình, xem PDF đã ký | Có chữ ký thực, ảnh ký hiển thị; kiểm tra SHA/chứng thư |
| TC-OUT-07 | Ký lại cùng tác vụ hoặc nhấn gửi lặp | Không ký trùng/không sinh lặp sự kiện hoặc số ký |

### E05. Chọn người ký, phê duyệt, ủy quyền (`TC-SIGN`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-SIGN-01 | Chuyên viên trình trưởng/phó phòng cùng phòng + lãnh đạo | Đúng tập ứng viên, người soạn không tự ký nháy |
| TC-SIGN-02 | Văn thư soạn, chọn trưởng phòng bất kỳ đủ điều kiện trong cơ quan | Không hiện danh sách rỗng khi có người hợp lệ |
| TC-SIGN-03 | Trưởng phòng soạn | Chỉ cấp phó phù hợp; không được tự ký nháy |
| TC-SIGN-04 | Phó phòng soạn | Có trưởng phòng và các phó khác hợp lệ; không có chính người soạn |
| TC-SIGN-05 | Lãnh đạo/phó lãnh đạo soạn | Không có bước ký trưởng phòng; chọn lãnh đạo khác phù hợp |
| TC-SIGN-06 | Kiểm tra kết quả ký cả các bước + số chữ ký | Thường 4, lãnh đạo tự soạn 3; mỗi bước thêm vào **cùng PDF chính** |
| TC-SIGN-07 | Người không được chọn/khác phòng/khác cơ quan cố ký/duyệt | 403/404/400; trạng thái và PDF giữ nguyên |
| TC-SIGN-08 | Trả sửa có lý do; cập nhật PDF và trình lại | Lịch sử/phiên bản/số lần ký đúng, không sửa PDF đã ký cũ |
| TC-SIGN-09 | Quản trị ủy quyền tác vụ ký nháy/ lãnh đạo với lý do/thời hạn | Người hợp lệ thấy việc; người khác không ký được; danh tính ký đúng |
| TC-SIGN-10 | Thu hồi/hết hạn ủy quyền trước ký, chọn ứng viên khác phòng | Không còn ký được, nhật ký lưu; không thêm bậc ký |
| TC-SIGN-11 | Xác minh chữ ký PDF / tải PDF đã ký | Kết quả mật mã hợp lệ; phân biệt chứng thư tự ký với CA công nhận |
| TC-SIGN-12 | Đọc một văn bản cũ có `SoBuocKy=5` | Không tự động chuyển thành 4/3 bước, giữ lịch sử cũ |

### E06. Phát hành, giao nhận (`TC-DEL`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-DEL-01 | Văn thư cấp số/ký hiệu, ký số cơ quan, phát hành | Chặn nếu chưa đủ ký; PDF cuối có con dấu và số đúng |
| TC-DEL-02 | Gửi bản phát hành tới cơ quan B | Cơ quan nhận thấy bản đã ký + tất cả tệp phụ |
| TC-DEL-03 | B tiếp nhận, đối chiếu biên nhận | Trạng thái và giờ gửi/nhận chính xác, người nhận đúng |
| TC-DEL-04 | B từ chối có lý do (một văn bản khác) | Hành trình ghi nhận, không chuyển nhầm thành đã tiếp nhận |
| TC-DEL-05 | Gửi/tiếp nhận lặp hoặc khác tenant gọi ID | Không trùng biên nhận; không lộ bản nháp/nội bộ bên gửi |
| TC-DEL-06 | Tải PDF đã ký + phụ từ màn hình giao nhận | Đúng bản phát hành, không gọi nhầm API văn bản đi của cơ quan nhận |

### E07. Văn bản đến và xử lý nhiệm vụ (`TC-IN`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-IN-01 | Văn thư B tiếp nhận và cấp số đến | Số đến duy nhất đúng năm/cơ quan; trạng thái cập nhật |
| TC-IN-02 | Trình lãnh đạo cơ quan B | Lãnh đạo thấy việc; chuyên viên không tự chỉ đạo |
| TC-IN-03 | Chọn 1 phòng chủ trì và >=1 phòng phối hợp, hạn/ý kiến | Lưu đúng phòng/hạn, không gửi sai cơ quan |
| TC-IN-04 | Trưởng mỗi phòng giao **cá nhân chủ trì** và phối hợp | Người được giao thấy việc, tên hiển thị **kèm chức vụ** |
| TC-IN-05 | Thử giao lại không có lý do, sau đó có lý do | Lần đầu bị chặn; lần sau ghi lịch sử trước–sau |
| TC-IN-06 | Cá nhân báo cáo kết quả, tải minh chứng ảnh | Báo cáo và ảnh PNG/JPG/WEBP hiển thị/tải được đúng quyền |
| TC-IN-07 | Ảnh sai định dạng/vượt 5 MB, người không được giao thử báo cáo | Phản hồi rõ, không ghi tệp/sự kiện bất hợp lệ |
| TC-IN-08 | Lãnh đạo xem báo cáo các phòng, kết luận và hoàn thành | Trạng thái `HOAN_THANH`, người/hạn/kết luận và lịch sử đủ |
| TC-IN-09 | Mở chi tiết từ Văn bản đến/Công việc/Thông báo | Mở đúng **chi tiết văn bản đến**, PDF đã ký + file phụ, không 404 sai ID |

### E08. Hồ sơ lưu trữ nền tảng (`TC-DOS`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-DOS-01 | Tạo hồ sơ năm/loại/phòng/thời hạn, xem và sửa | Chủ hồ sơ hoặc admin đúng quyền quản lý; dữ liệu lưu đúng |
| TC-DOS-02 | Thêm văn bản đi đã phát hành, văn bản đến đã hoàn thành | Danh sách đủ 2 loại; mở PDF ký + tệp đính kèm đúng nguồn |
| TC-DOS-03 | Thêm cùng văn bản hai lần, khác cơ quan, văn bản nháp | Ngăn trùng/sai quyền/sai trạng thái; không có 500 |
| TC-DOS-04 | Đóng hồ sơ rồi thử thay đổi danh sách | Chặn sửa hồ sơ đã đóng; dữ liệu và lịch sử không đổi |
| TC-DOS-05 | Nộp lưu, văn thư cơ quan tiếp nhận, mở biên nhận | Đúng người nhận/trạng thái/thời gian; không nộp rỗng |
| TC-DOS-06 | Văn thư trả lại có lý do (hồ sơ test khác) | Người nộp thấy lý do; lịch sử đầy đủ |
| TC-DOS-07 | Nộp trùng hồ sơ đang chờ/đã nhận | Không tạo lượt nộp trùng hợp lệ |

### E09. Lưu trữ nâng cao (`TC-ARC`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-ARC-01 | Tra cứu theo mã, tên, năm, phòng, người lập/nhận, trạng thái | Bộ lọc chính xác, không hiển thị tenant khác |
| TC-ARC-02 | Xem thống kê và xuất CSV | Tổng số khớp dữ liệu; file Unicode mở đúng |
| TC-ARC-03 | Đề xuất thời hạn mới, Văn thư duyệt/từ chối | Chỉ hồ sơ đã nhận; lịch sử và thời hạn sau duyệt đúng |
| TC-ARC-04 | Kiểm tra hồ sơ vĩnh viễn/hết hạn/sắp hết hạn | Không tự xóa; thời hạn tính từ ngày văn thư tiếp nhận |
| TC-ARC-05 | Xin quyền khai thác, văn thư duyệt có hạn | Người xin thấy PDF/file trong thời hạn quyền |
| TC-ARC-06 | Thu hồi/hết hạn quyền khai thác, thử mở lại | Từ chối; không trả đường dẫn vật lý hoặc nội dung file |
| TC-ARC-07 | Kiểm tra SHA-256/tính toàn vẹn và nhật ký truy cập | Băm đúng, ghi người xem/tải, xuất CSV được |

### E10. Hàng đợi, thông báo, giám sát, thống kê (`TC-MON`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-MON-01 | Bản nháp tự tạo, chưa ký/chưa trình | Hiện tại **Việc cần xử lý**, click mở đúng bản nháp |
| TC-MON-02 | Nhiệm vụ ký, duyệt, văn bản đến giao cá nhân, chờ tiếp nhận | Đúng người nhận, việc đã hoàn thành không còn chờ |
| TC-MON-03 | Thông báo nhận/gửi/phát hành/giao việc | Mỗi thông báo có khung riêng, thời gian rõ, nút mở đúng đối tượng |
| TC-MON-04 | Đánh dấu đã đọc/nhấn thông báo | Trạng thái đã đọc cập nhật, số chưa đọc chính xác |
| TC-MON-05 | Giám sát quá hạn/sắp hạn/đúng hạn/đã hoàn thành | Số liệu thời gian và người xử lý đúng, không tự duyệt chuyển bước |
| TC-MON-06 | Thống kê tổng hợp và xuất Excel/CSV nếu có | Số lượng khớp DB phạm vi được phép, định dạng file đúng |
| TC-MON-07 | Tạo >15 bản ghi test ở từng danh sách dài | Trang 1 có 15 kết quả, có điều hướng/đếm tổng, không trùng/bỏ sót |

### E11. An toàn API, tính toàn vẹn và điều kiện lỗi (`TC-SEC`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-SEC-01 | B/A hoán đổi documentId, dossierId, deliveryId, fileId, taskId | Không đọc/sửa/tải dữ liệu trái tenant; 403/404 phù hợp |
| TC-SEC-02 | JWT thiếu/hết hạn, sai quyền, tài khoản khóa, cơ quan ngừng | Bị từ chối theo quyền; token cũ không vượt qua server checks |
| TC-SEC-03 | Thử cấp quyền `SYSTEM_ADMIN`, sửa role được bảo vệ bằng API nghiệp vụ | Bị chặn; không leo thang đặc quyền |
| TC-SEC-04 | Kiểm thử trùng thao tác/ký/phát hành/giao nhận/nộp lưu | Idempotency/409 hợp lý; không nhân bản số hiệu hoặc dữ liệu |
| TC-SEC-05 | File giả định dạng, MIME sai, nội dung PDF lỗi, kích thước vượt giới hạn | Lỗi rõ, không crash hoặc đọc đường dẫn tuỳ ý; không lưu file độc |
| TC-SEC-06 | Thử xem/tải file thuộc hồ sơ chưa được cấp quyền/hết hạn | 403/404; có nhật ký từ chối hoặc kiểm tra quyền |
| TC-SEC-07 | Kiểm tra log, API response, HTML bundle, Git diff | Không lộ bí mật kết nối, PFX, AES, JWT key, mật khẩu hoặc đường dẫn nội bộ nhạy cảm |
| TC-SEC-08 | Ký PDF, đối chiếu mã băm trước/sau, xác minh chữ ký | Không tuyên bố chứng thư tự ký là CA hợp pháp; phát hiện can thiệp file |

### E12. Giao diện/responsive (`TC-UI`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-UI-01 | Desktop 1920×1080, 1366×768, tablet 768×1024, mobile 390×844 | Không mất nút/lỗi cuộn ngang ngoài ý muốn; modal có thể thao tác |
| TC-UI-02 | Tất cả bảng/quản trị >15 bản ghi | Phân trang/tìm/lọc vẫn xem được bản ghi thứ 16 và cuối |
| TC-UI-03 | Thông tin cơ quan vs tạo tài khoản vs người dùng trong cơ quan | Các panel rộng bằng nhau, 3 ô thông tin cơ quan cân đối |
| TC-UI-04 | Xem trước PDF, kéo ảnh chữ ký, danh sách tệp/ảnh minh chứng | PDF/ảnh hiển thị được, giữ đúng tỷ lệ, không lệch vị trí ký |
| TC-UI-05 | 401/403/404/409/500, mất kết nối | Hiển thị đúng nguyên nhân (không gom mọi lỗi thành 'Không thể kết nối máy chủ') |

### E13. Hồi quy và khôi phục (`TC-REG`)

| Mã | Hành động | Kết quả kỳ vọng |
|---|---|---|
| TC-REG-01 | Mở dữ liệu cũ từ phiên bản 0.5, 0.6, 0.7, 0.9 | Giữ hành trình, chữ ký PDF, hồ sơ, biên nhận và dữ liệu cũ |
| TC-REG-02 | Restart backend, F5 frontend, đổi tài khoản cùng tab | Dữ liệu không mất; quyền/phiên cập nhật đúng |
| TC-REG-03 | Ngừng cơ quan test rồi mở lại | Dữ liệu còn nguyên, hoạt động trở lại theo quyền |
| TC-REG-04 | Chạy lại migration 025 trên **bản sao DB test** (nếu được phép) | Không mất dữ liệu; quyền không nhân bản; không sửa khóa |

## F. Nguyên tắc đánh giá và bằng chứng

**Trạng thái duy nhất của mỗi case:**

- `PASS (ĐẠT)`: đã thực thi, kết quả thực tế khớp kỳ vọng, có bằng chứng.
- `FAIL (KHÔNG ĐẠT)`: chạy được và có sai lệch xác định.
- `BLOCKED (BỊ CHẶN)`: thiếu môi trường/quyền/chứng thư/PDF/tiền điều kiện; phải chỉ ra blocker.
- `NOT_RUN (CHƯA CHẠY)`: chưa thực hiện vì thứ tự hoặc thời gian; không cộng vào PASS.

**Không đánh dấu PASS chỉ dựa trên** `dotnet build`, `npm run build`, mã nguồn có endpoint, HTTP 200, hoặc lời nghiệm thu cũ. Bước độc lập cần evidence độc lập.

Thu thập ít nhất:

- Thời gian bắt đầu/kết thúc (múi giờ Asia/Ho_Chi_Minh), Git commit SHA, migration max.
- Ảnh UI không lộ thông tin nhạy cảm, tên tài khoản vai trò test, HTTP method/status, trường phản hồi an toàn, log máy chủ nếu có.
- Danh sách ID dữ liệu test, người tạo/được giao, trạng thái trước–sau, xác minh SHA-256/chữ ký (ghi nhận `valid/invalid/not-testable` và giới hạn tin cậy CA).
- Bảng đối chiếu số liệu từ DB **chỉ đọc** trước/sau (khi được phép); không lưu bản full dump chứa PII.
- Nhãn mức độ lỗi `Critical`, `High`, `Medium`, `Low` theo tác động: lộ dữ liệu/nhảy quyền = Critical; ký/phát hành/lưu sai = High; thông báo, phân trang, UX = Medium/Low tùy mức.

## G. Mẫu báo cáo bắt buộc

### G1. `docs/KET_QUA_KIEM_THU_TOAN_HE_THONG.md`

```markdown
# Báo cáo kiểm thử toàn hệ thống — YYYY-MM-DD

## 1. Tóm tắt
- Git branch/commit: ...
- Môi trường: ...
- API/Frontend/SQL migration: ...
- Tổng ca: ...; PASS ...; FAIL ...; BLOCKED ...; NOT_RUN ...
- Kết luận: SẴN SÀNG / CHƯA SẴN SÀNG / CHƯA ĐỦ BẰNG CHỨNG.

## 2. Tổng hợp theo nhóm E01–E13
| Nhóm | Tổng | PASS | FAIL | BLOCKED | NOT_RUN | Ghi chú |
|---|---:|---:|---:|---:|---:|---|

## 3. Các lỗi ưu tiên
| Mã bug | Mức | Test case | Bước tái hiện | Mong đợi | Thực tế | Bằng chứng |
|---|---|---|---|---|---|---|

## 4. Blocker và điều kiện cần người dùng cung cấp
## 5. Kiểm tra cách ly cơ quan, ký PDF, dữ liệu và bảo mật
## 6. Đề xuất sửa lỗi theo mức ưu tiên (không tự thay code)
## 7. Danh sách ID dữ liệu kiểm thử và đề nghị dọn dẹp thủ công nếu cần
## 8. Phụ lục: lệnh đã chạy, tệp bằng chứng, môi trường
```

### G2. `docs/KET_QUA_KIEM_THU_CHI_TIET.csv`

Cột chuẩn CSV UTF-8 BOM tùy công cụ: `TestId,Module,Role,Precondition,Steps,Expected,Actual,Status,Severity,HTTPStatus,Evidence,ExecutedAt,Notes`. Một dòng mỗi TC; trường `Status` chỉ một trong `PASS,FAIL,BLOCKED,NOT_RUN`.

### G3. Thông báo trực tiếp cho chủ dự án

Trả lời chat tiếng Việt theo mẫu:

1. **Kết quả:** Tổng X; PASS A / FAIL B / BLOCKED C / NOT_RUN D.
2. **Đã chạy thực tế:** môi trường, vai trò, build, bao nhiêu luồng E2E hoàn chỉnh.
3. **5 lỗi nghiêm trọng nhất**, mỗi lỗi có cách tái hiện và bằng chứng.
4. **Chưa thể xác nhận:** các ca bị chặn/chưa chạy và nguyên nhân.
5. **Đề nghị bước kế tiếp:** sửa gì, ai cấp quyền/dữ liệu gì, thứ tự chạy lại. Gắn liên kết báo cáo trong repository/workspace nếu đã tạo.

## H. Tài liệu đối chiếu theo phân hệ

- `database/001_khoi_tao.sql` → `database/025_quan_tri_toan_he_thong.sql`: cấu trúc, từng migration, mẫu quyền; **không tự chạy seed lại**.
- `backend/Program.cs`, `Database.cs`, `XacThuc.cs`: API, JWT/tenant/permissions, profile.
- `backend/QuanTriHeThongEndpoints.cs`, `QuanTriThongTinCoQuanEndpoints.cs`, `QuanTriNguoiDungEndpoints.cs`, `QuanTriPhongBanEndpoints.cs`, `QuanTriPhanQuyenEndpoints.cs`: quản trị.
- `backend/VanBanEndpoints.cs`, `TepVanBanEndpoints.cs`, `KySoEndpoints.cs`, `DichVuKyPdf.cs`, `QuyTrinhEndpoints.cs`, `UyQuyenKyEndpoints.cs`, `PhatHanhEndpoints.cs`: văn bản đi và ký.
- `backend/DieuHanhVanBanDenEndpoints.cs`, `ViecCaNhanEndpoints.cs`: tiếp nhận và điều hành văn bản đến, cá nhân.
- `backend/HoSoEndpoints.cs`, `NopLuuEndpoints.cs`, `LuuTruNangCaoEndpoints.cs`, `GiamSatEndpoints.cs`: lưu trữ, khai thác, giám sát/thông báo.
- `web/src/App.tsx`, `DieuHanhVanBanDenPanel.tsx`, `ChiTietVanBanDen.tsx`, `HoSoPage.tsx`, `LuuTruNangCaoPanel.tsx`, `QuanTriHeThong.tsx`, `ThongTinCoQuan.tsx`: giao diện nghiệp vụ.
- Các hướng dẫn kiểm thử chi tiết trong `docs/KIEM_THU_*.md`: dùng để bổ sung bước thao tác; **ưu tiên mã nguồn đang chạy** nếu tài liệu cũ mô tả 5 bước/4 bước khác phiên bản.

---

**Tiêu chí kết thúc:** chỉ tuyên bố toàn hệ thống ĐẠT khi tất cả ca bắt buộc đã chạy, không có FAIL Critical/High, không có BLOCKED làm hỏng luồng chính, và đã xác minh quyền liên cơ quan + ký PDF + nộp lưu bằng bằng chứng. Mọi kết luận trước thời điểm đó chỉ là **kết quả một phần**.
