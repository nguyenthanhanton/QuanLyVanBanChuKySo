# QUẢN LÝ VĂN BẢN & CHỮ KÝ SỐ — BẢN NỀN TẢNG 0.4 (ĐANG KIỂM THỬ)

> **Nhánh phát triển 0.5.1:** Đã có [thiết kế năm vị trí ký số](docs/THIET_KE_KY_SO_05_CAP.md), [migration 007](database/007_nen_tang_ky_nam_cap.sql) và [hướng dẫn kiểm thử](docs/KIEM_THU_KY_05_CAP.md). Đây mới là nền tảng CSDL/quyền, **chưa có quy trình ký năm cấp trên backend/frontend**. Sau khi sao lưu SQL Server và áp dụng 006, chạy 007 trong SSMS rồi kiểm tra theo tài liệu. Không chạy 007 trước 006; không dùng dữ liệu thật để thử nghiệm.

Ứng dụng web quản lý văn bản đi/đến nhiều cơ quan, được dựng mới theo bộ tài liệu yêu cầu 01–08 cung cấp ngày 24/11/2026. Trọng tâm của bản đầu là **theo dõi hành trình văn bản và kiểm soát phạm vi dữ liệu**.

> **Quan trọng:** Đây là **mã nguồn bản nền tảng chưa nghiệm thu toàn bộ UC001–UC032**. Đã bổ sung mã backend để ký mật mã CMS trong PDF tuần tự và phát hành/gửi/nhận liên cơ quan, nhưng **chưa có điều kiện chạy thử .NET/SQL Server và kiểm thử chữ ký PDF tích hợp**. Giao diện mặc định vẫn là demo. Không sử dụng cho dữ liệu sản xuất hoặc khẳng định hiệu lực pháp lý khi chưa kiểm thử, thẩm định và tích hợp CA.

## 1. Cấu trúc dự án

```text
QuanLyVanBanChuKySo/
├── backend/                  API .NET 8 + Dapper + SQL Server
│   ├── Program.cs            Cấu hình API/JWT/CORS
│   ├── XacThuc.cs            Đăng nhập, PBKDF2, khóa sau 5 lần sai
│   ├── Database.cs           Kết nối và kiểm tra quyền ở API
│   ├── VanBanEndpoints.cs    Tạo/sửa văn bản, phiên bản, PDF, hành trình
│   ├── TepVanBanEndpoints.cs Thêm/bỏ/thay tệp ở bản nháp; xem phiên bản
│   ├── DichVuTepVanBan.cs   Kiểm tra và lưu Word/Excel/PDF, SHA-256
│   ├── QuyTrinhEndpoints.cs  Trình duyệt, phê duyệt, cấp số, tiếp nhận
│   ├── DichVuKyPdf.cs       Ký PDF/CMS, mã hóa PFX, xác minh từng chữ ký
│   ├── KySoEndpoints.cs     Quản lý chứng thư, chuẩn bị và ký 3 cấp
│   └── PhatHanhEndpoints.cs Phát hành, gửi, hộp nhận, từ chối, tải PDF
├── database/
│   ├── 001_khoi_tao.sql      Tạo DB và lược đồ từ số 0
│   ├── 002_du_lieu_mau.sql   3 cơ quan + 15 user dev/test
│   ├── 003_kiem_tra.sql      Các truy vấn kiểm tra chỉ đọc
│   ├── 004_ky_so_phat_hanh.sql  Chứng thư và bản PDF ký bất biến
│   └── 005_tep_dinh_kem_phat_hanh.sql  Tệp phát hành và khóa tên văn bản
│   └── 006_van_ban_chinh.sql  Phân biệt PDF chính với tệp đính kèm
├── web/                      React, TypeScript, Vite
│   ├── src/App.tsx           Giao diện và các thao tác
│   ├── src/demo.ts           Dữ liệu minh họa, KHÔNG phải chữ ký thật
│   └── src/styles.css        Phong cách giao diện đáp ứng màn hình nhỏ
└── docs/                     Thiết kế và danh mục còn thiếu
```

## 2. Quy ước tên cơ sở dữ liệu

### Nâng cấp 0.4 từ 0.3

Chạy **chỉ** `database/006_van_ban_chinh.sql` trên database đã áp dụng đến migration `005`. Không chạy lại `001`, `002`, `004`, `005` khi không cần. Script thêm `TepDinhKem.LaVanBanChinh` và đánh dấu PDF đầu tiên của mỗi phiên bản cũ làm văn bản chính; lịch sử/tệp cũ được giữ lại.

**Màn hình tạo văn bản có hai vùng độc lập:**

- **Văn bản chính (bắt buộc):** một tệp PDF dùng làm nguồn ký số, có thể thay bằng PDF khác khi đang nháp.
- **Tệp đính kèm (không bắt buộc):** Word, Excel, PDF khác; được gửi cùng phiên bản phát hành sang cơ quan nhận.

Tab **Phiên bản** chia hai nhóm; có nút xem trước. PDF xem nguyên trang trên trình duyệt; `.docx`/`.xlsx` xem phần chữ/bảng tính được trích xuất ở chế độ chỉ đọc, không bảo đảm giống bố cục Word/Excel. Định dạng cũ `.doc`/`.xls` cần bộ chuyển đổi bổ sung, hiện có nút tải về. Không dùng dịch vụ xem tệp bên ngoài để tránh lộ tài liệu cơ quan.

### Nâng cấp cơ sở dữ liệu đã chạy bản 0.2

**Nếu bạn đã chạy `001`, `002`, `004` và đã có văn bản thật:** chỉ chạy file mới `database/005_tep_dinh_kem_phat_hanh.sql` trong SSMS, không chạy lại `001` hay seed `002`. Đảm bảo tên database đang chọn là `QuanLyVanBanChuKySo` và chạy toàn bộ script có `GO`. Script tạo bảng `BanPhatHanhTep` và trigger `KhoaTieuDeVanBan`, không xóa các văn bản cũ.

Sau đó thay toàn bộ thư mục `backend` và `web` từ ZIP mới, giữ lại cấu hình cá nhân (`web/.env`, các khóa/biến môi trường, kho chứng thư). Khởi động lại `dotnet run` và `npm run dev`. Nếu chạy bản backend mới trước migration 005, thao tác phát hành/tải tệp sẽ báo không có bảng `dbo.BanPhatHanhTep`.

**Quy tắc tệp:** mỗi bản nháp có 1–20 tệp PDF, Word, Excel (25 MB/tệp, 100 MB tổng). Có thể thêm, bỏ, thay tệp bằng cách tạo phiên bản mới khi ở `NHAP` hoặc `TRA_SUA`; **tiêu đề không thể thay đổi**. Bước ký số vẫn cần một PDF. Bản phát hành đã gửi mang danh sách tệp chốt tại thời điểm phát hành, không chịu ảnh hưởng nếu bản nháp khác được sửa sau này.

- **Tên database:** `QuanLyVanBanChuKySo`.
- **Tên bảng:** tiếng Việt không dấu, PascalCase, ví dụ `CoQuan`, `NguoiDung`, `VanBan`, `PhienBanVanBan`, `LuotChuyenVanBan`, `LichSuVanBan`.
- **Tên cột:** tiếng Việt không dấu, PascalCase, ví dụ `MaVanBan`, `MaCoQuanGui`, `MaCoQuanNhan`, `ThoiDiemGui`, `TrangThai`, `LyDoTuChoi`.
- **Dữ liệu nội dung:** tiếng Việt **có dấu**, lưu bằng `NVARCHAR` / truyền Unicode JSON. Ví dụ: `N'Kế hoạch tổng kết công tác năm 2026'`.
- **Mã kỹ thuật/trạng thái:** không dấu (ví dụ `CHO_DUYET`, `DA_TIEP_NHAN`) để API ổn định.

Không áp dụng migration từ cơ sở dữ liệu tiếng Anh ở dự án cũ; đây là database mới độc lập.

## 3. Xem giao diện mô phỏng ngay

Cần Node.js 22 LTS hoặc phiên bản hỗ trợ Vite 5.

Trong PowerShell:

```powershell
cd .\QuanLyVanBanChuKySo\web
npm ci
Copy-Item .env.example .env
npm run dev
```

Mở `http://localhost:5173`. Mặc định `VITE_DEMO_MODE=true` nên **không gọi backend**.

- Tên đăng nhập `vanthu_a`, mật khẩu `Password123!`.
- Thử chuyển tài khoản demo ở góc phải sang `truongphong_a`, `truongvanphong_a`, `lanhdao_a`, `vanthu_b`, `vanthu_c`, hoặc `quantrihethong`.
- Chọn **Hành trình văn bản**, xem văn bản `VB-20261124-A001`: có phiên bản 1 → trả sửa → phiên bản 2 → duyệt → cấp số → ký (mô phỏng) → phát hành A → B/C; B đã tiếp nhận, C đang chờ.
- Bấm **Tạo văn bản mới** và thử trình duyệt. Thao tác trong chế độ mô phỏng được giữ bằng `localStorage` trên trình duyệt.

**Lưu ý:** chế độ mô phỏng không sử dụng SQL Server, không có kiểm soát bảo mật thực và không tạo chữ ký số. Chỉ dùng kiểm tra trải nghiệm giao diện.

## 4. Chạy API + SQL Server trên Windows

Cần .NET 8 SDK, Node.js, SQL Server local và `sqlcmd`.

### Bước 4.1. Tạo cơ sở dữ liệu mới

Tại thư mục dự án:

```powershell
sqlcmd -S . -E -C -i .\database\001_khoi_tao.sql
```

- **Mục đích:** tạo `QuanLyVanBanChuKySo` và các bảng chuẩn. Không xóa DB khác.
- **Đầu vào:** SQL Server local, Windows Authentication có quyền `CREATE DATABASE`.
- **Kết quả mong đợi:** bảng `dbo.PhienBanLuocDo` có phiên bản `1`, có các bảng `VanBan`, `LuotChuyenVanBan`, `LichSuVanBan`.
- **Lỗi thường gặp:** không kết nối SQL Server, thiếu quyền tạo DB; hoặc đã chạy migration 001 (script cố tình từ chối chạy lại).

### Bước 4.2. Seed dữ liệu DEV/TEST nếu đây là database mới, chưa có user

```powershell
sqlcmd -S . -E -C -i .\database\002_du_lieu_mau.sql
```

- Chỉ chạy trên DB trống dành cho development/test.
- Tạo 3 cơ quan A, B, C, vai trò/quyền, 15 tài khoản mẫu với mật khẩu `Password123!`.
- `quantrihethong` **không được cấp** `documents.read`.
- Script từ chối khi đã có tài khoản để tránh chạm dữ liệu hiện hữu.

### Bước 4.2b. Áp dụng migration ký số 004

```powershell
sqlcmd -S . -E -C -b -i .\database\004_ky_so_phat_hanh.sql
sqlcmd -S . -E -C -b -i .\database\005_tep_dinh_kem_phat_hanh.sql
sqlcmd -S . -E -C -b -i .\database\003_kiem_tra.sql
```

Migration thêm `ChungThuKy`, `BanPdfDaKy` và khóa ngoại tới `ChuKySo`. Tên database/bảng/cột đều tiếng Việt **không dấu**; tiêu đề, tên cơ quan, lý do và mô tả là Unicode có dấu.

### Bước 4.3. Đặt cấu hình an toàn cho API

Trong cùng cửa sổ PowerShell chạy backend:

```powershell
$env:ConnectionStrings__QuanLyVanBan = "Server=.;Database=QuanLyVanBanChuKySo;Trusted_Connection=True;TrustServerCertificate=True;"
$env:Jwt__Key = "REPLACE_THIS_WITH_A_RANDOM_SECRET_AT_LEAST_32_BYTES_LONG"
$env:Storage__RootPath = "D:\QuanLyVanBanStorage"
$buf = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buf)
$env:Security__EncryptionKeyBase64 = [Convert]::ToBase64String($buf)
$env:Security__ActiveKeyId = "khoa-noi-bo-001"
$env:Security__PfxPassword = "DOI_THANH_MAT_KHAU_MA_HOA_PFX_RAT_DAI"
$env:Signing__KeyStorePath = "D:\QuanLyVanBanSecrets\KhoChungThu"
$env:Signing__FontPath = "C:\Windows\Fonts\arial.ttf"
dotnet run --project .\backend\QuanLyVanBan.Api.csproj -- --urls http://127.0.0.1:5000
```

Thay chuỗi `Jwt__Key` và `Security__PfxPassword` bằng giá trị ngẫu nhiên mạnh **không đưa lên Git**. Cất **bền vững** `Security__EncryptionKeyBase64` trong secret manager / kho bí mật: nếu tạo khóa khác mỗi lần khởi động, hệ thống sẽ không giải mã được các PFX đã lưu. `Security__ActiveKeyId` là định danh phiên bản khóa, không tự thực hiện luân chuyển khóa. Tạo thư mục lưu trữ ở ổ đĩa riêng, giới hạn quyền file cho tài khoản API; không đặt file trong thư mục công khai của web.

**Bảo mật:** trong bản thử nghiệm, backend quản lý khóa bí mật PFX trên máy chủ và dùng AES-256-GCM để mã hóa khi lưu; chưa tích hợp USB token/HSM, ký từ xa của CA, OCSP/CRL, TSA và kiểm định pháp lý. Nếu yêu cầu chữ ký số theo quy định cơ quan, phải tích hợp nhà cung cấp phù hợp và quy trình phê duyệt thực tế.

**Giấy phép:** thư viện iText sử dụng giấy phép **AGPL hoặc thương mại**. Kiểm tra điều kiện phân phối, cung cấp mã nguồn và mua giấy phép phù hợp trước khi triển khai phần mềm đóng nguồn.

- `GET http://127.0.0.1:5000/api/v1/health` phải trả `{ "status": "ok", ... }`.
- `POST /api/v1/auth/login` với `vanthu_a` / `Password123!` sẽ cấp JWT 30 phút.
- Sau khi đăng nhập, `GET /api/v1/auth/capabilities` lấy quyền từ SQL Server.

### Bước 4.4. Nối React tới API

Trong `web/.env`, đặt:

```text
VITE_DEMO_MODE=false
```

Sau đó:

```powershell
cd .\web
npm ci
npm run dev
```

Vite chuyển các yêu cầu `/api` tới `http://127.0.0.1:5000` theo `vite.config.ts`.

### Bước 4.5. Thao tác quy trình thật ở giao diện

1. `chuyenvien_a` (hoặc tài khoản có quyền): tạo văn bản mới kèm **nhiều tệp PDF, Word, Excel** (.pdf, .doc, .docx, .xls, .xlsx), **đánh dấu chọn đồng thời Cơ quan B và C**. Backend lưu danh sách nhận dự kiến riêng từng cơ quan.
2. Trong trạng thái `NHAP`/`TRA_SUA`, mở văn bản → **Phiên bản** → đánh dấu bỏ tệp cũ, chọn tệp mới và **Lưu phiên bản mới**. Có thể sửa trích yếu; **tiêu đề đã tạo không được phép sửa** (API và trigger SQL đều chặn). Không xóa lịch sử các phiên bản cũ.
3. Người soạn **Trình duyệt** → `truongphong_a` duyệt → `truongvanphong_a` kiểm tra thể thức → `lanhdao_a` duyệt → `vanthu_a` **Cấp số**.
4. Trong chi tiết văn bản, tab **Chữ ký số**: các vai trò ký tạo/nhập chứng thư PFX/P12 riêng (chỉ chọn “thử nghiệm” nếu muốn tạo chứng thư tự ký), nhập mật khẩu tài khoản để xác nhận.
5. `vanthu_a` chọn **Chuẩn bị PDF**. Phiên bản đem ký phải có ít nhất **một tệp PDF**; Word/Excel vẫn là tệp kèm và không được ký trực tiếp. Backend chèn số/ký hiệu vào chân trang và đóng băng bản nội dung đã duyệt trước khi ký.
6. `truongphong_a` **Ký nháy**, `lanhdao_a` **Lãnh đạo ký**, `vanthu_a` **Cơ quan ký**. Tất cả đều là chữ ký CMS nhúng trong PDF theo append mode, yêu cầu password và quyền đúng vai trò.
7. `vanthu_a` **Phát hành**; vào **Giao nhận liên cơ quan** để **Gửi văn bản**. Hệ thống đóng băng danh sách **toàn bộ tệp đính kèm của phiên bản phát hành** và cung cấp cả bản PDF có 3 chữ ký đã xác minh.
8. `vanthu_b` đăng nhập → **Văn bản đến** → **Tệp đính kèm** để tải Word/Excel/PDF, **Tải PDF** để tải bản đã ký, rồi **Tiếp nhận** hoặc **Từ chối**. Bên gửi xem kết quả trong **Hành trình văn bản**.

Các thao tác phát hành và gửi là **hai hành động khác nhau**. Tạo lượt chuyển (`CHO_GUI`) không đồng nghĩa đã gửi (`DA_GUI`); bên nhận chỉ xem khi đã gửi.

Chứng thư tự ký nội bộ có thể được xác minh tính toàn vẹn bằng thuật toán mật mã nhưng **không được gọi là chứng thư tin cậy hay chữ ký có giá trị pháp lý**. Kết quả `HOP_LE_MAT_MA` khác với kết quả thẩm định chuỗi CA.

## 5. Kiểm thử đã thực hiện / chưa thực hiện

| Hạng mục | Trạng thái |
|---|---|
| `npm ci` | Thành công trong môi trường xây dựng |
| `npm run build` (TypeScript + Vite) | Thành công sau khi bổ sung UI ký, phát hành, nhận |
| Kiểm tra mật khẩu PBKDF2 seed bằng Node.js | Thành công |
| `dotnet build` / chạy API | **Chưa chạy** — môi trường xây dựng không có .NET 8 SDK |
| Apply SQL, kiểm tra transaction/constraint | **Chưa chạy** — môi trường xây dựng không có SQL Server |
| Xác minh chữ ký PAdES/CMS PDF và chống sửa sau ký | **Đã viết mã, chưa chạy** do thiếu .NET runtime |
| E2E đăng nhập → duyệt → ký 3 bước → phát hành → nhận | **Chưa chạy**, cần Windows/.NET/SQL Server |

Không được lấy kết quả test lịch sử của dự án cũ làm kết quả nghiệm thu dự án này.

## 6. Nguyên tắc bảo mật chính

1. API không tin trạng thái menu frontend, tự kiểm tra quyền từ DB và cơ quan theo tài khoản.
2. Quản trị hệ thống không mặc nhiên đọc tài liệu, và quản trị cơ quan không được tự nâng quyền nghiệp vụ.
3. Phạm vi đọc văn bản bị giới hạn theo người tạo/người được giao/trưởng phòng/văn thư/lãnh đạo; văn bản mật chưa được cho phép tạo ở bản đầu do chưa có cấu hình clearance.
4. Phiên bản mới là bản ghi bất biến, kiểm tra `PhienBanDong` khi sửa nháp (409 nếu xung đột).
5. Hành trình lưu ngày giờ có phần thời gian, không xóa lịch sử; bảng giao nhận giữ trạng thái từng cơ quan nhận.
6. Khóa JWT, kho PFX mã hóa và kho PDF nằm ngoài source và database. Không đưa mật khẩu demo, chứng thư hoặc ảnh chữ ký vào production.
7. Bản PDF ký không ghi đè lên bản trước; mỗi bước ký là một tệp và một bản ghi `BanPdfDaKy` có hash SHA-256. Trước khi phát hành và gửi lại kiểm tra mật mã.

**Hạn chế bảo mật bản đầu:** chưa có refresh token HttpOnly/JWT revocation, khóa phần cứng/HSM/USB token, kiểm tra OCSP/CRL/TSA, xác thực tin cậy CA, phân quyền độ mật chuyên sâu, quản trị vòng đời chứng thư (thu hồi/luân chuyển), các endpoint quản trị đầy đủ và kiểm thử bảo mật. Chưa sẵn sàng dùng với dữ liệu thật/nhạy cảm.

## 7. Hướng hoàn thiện

Xem [docs/KE_HOACH_TIEP_THEO.md](docs/KE_HOACH_TIEP_THEO.md). Ưu tiên hiện tại: **biên dịch .NET, chạy migration + E2E, thử sửa PDF sau ký, tích hợp CA/HSM và nghiệm thu chính sách bảo mật**. Không bật dùng sản xuất trước khi hoàn thành các mục này.
