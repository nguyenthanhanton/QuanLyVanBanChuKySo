# Kiểm thử theo chặng — phiên bản 0.5

## Chặng 0.5.1 (migration 007)

**Trước khi chạy:** sao lưu `QuanLyVanBanChuKySo`, bảo đảm migration 006 đã thành công. Chạy `database/007_nen_tang_ky_nam_cap.sql` bằng SSMS trên đúng database, sau đó chạy các truy vấn:

```sql
USE QuanLyVanBanChuKySo;
GO
SELECT SoPhienBan, TenPhienBan FROM dbo.PhienBanLuocDo WHERE SoPhienBan = 7;
SELECT name, definition FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.BanPdfDaKy') AND name = 'CK_BanPdfDaKy_ThuTu';
SELECT c.MaDinhDanh AS CoQuan, v.MaDinhDanh AS VaiTro, q.MaDinhDanh AS Quyen
FROM dbo.VaiTroQuyen vq
JOIN dbo.CoQuan c ON c.MaCoQuan = vq.MaCoQuan
JOIN dbo.VaiTro v ON v.MaVaiTro = vq.MaVaiTro
JOIN dbo.Quyen q ON q.MaQuyen = vq.MaQuyen
WHERE (v.MaDinhDanh = 'CHUYEN_VIEN' AND q.MaDinhDanh IN ('signing.sign','signatures.manage-own'))
   OR (v.MaDinhDanh = 'VAN_THU' AND q.MaDinhDanh = 'signatures.manage-own')
ORDER BY CoQuan, VaiTro, Quyen;
```

Kỳ vọng: một bản ghi version 7; CHECK cho phép `0..5`; quyền chuyên viên và văn thư theo từng cơ quan, không tạo bản ghi trùng. **Chạy lại 007 không thay đổi kết quả.** Backend 0.4 vẫn sử dụng luồng ba cấp, không có nút ký người soạn.

## Các chặng tiếp theo (chưa triển khai)

1. **0.5.2:** Đã có API ký PDF người soạn và chặn submit; chưa có kiểm thử .NET/SQL Server thực tế (xem mục dưới).
2. **0.5.3:** Đã viết mã ký 2–4 và chặn duyệt theo nhiệm vụ; cần kiểm thử .NET/SQL Server thực tế (xem bên dưới).
3. **0.5.4:** Đã có kiểm tra trước cấp số, ký cơ quan lần 5 và phát hành/giao nhận có xác minh 5 CMS; chưa có số và con dấu hiển thị trên trang PDF.
4. **0.5.5:** Đã viết mã ảnh PNG cho vùng ký mới, tọa độ trang PDF, xem PDF giao nhận; cần kiểm thử iText/.NET, độ hiển thị và trình xác minh độc lập.

Các chặng sau bắt buộc thử bằng .NET 8/SQL Server và trình đọc PDF độc lập. Không coi một bản ghi trong `ChuKySo` là bằng chứng chữ ký PDF hợp lệ nếu chưa xác minh CMS và toàn vẹn bản PDF.

## Chặng 0.5.2: ký PDF người soạn (đã viết mã, chờ kiểm thử tích hợp)

**Điều kiện:** migration 007 đã chạy; API khởi động với `Signing:RequireDrafterSignature=true` (mặc định trong `appsettings.json` trên nhánh 0.5); tài khoản người soạn có quyền `signing.sign`, `signatures.manage-own` và một chứng thư `CA_NHAN` hợp lệ. Chỉ dùng văn bản thử nghiệm trên môi trường dev.

1. Chạy lại backend và frontend sau `git pull` nhánh 0.5. Mở văn bản mới trạng thái `NHAP` có PDF chính và tab **Chữ ký số**.
2. Trước khi ký, thử nhấn `Trình duyệt`: nút phải bị vô hiệu hóa. Gửi POST trực tiếp `/api/v1/documents/{id}/submit` bằng JWT cũng phải nhận HTTP **409** (không được chỉ dựa vào giao diện).
3. Ở tab **Chữ ký số**, nhập mật khẩu tài khoản, tạo/nhập chứng thư cá nhân nếu chưa có, nhấn **Ký số PDF người soạn (1/5)**. API `POST /api/v1/documents/{id}/draft-signature` nhận `{ "loaiChuKy":"KY_NGUOI_SOAN", "matKhau":"...", "maYeuCau":"<UUID mới>" }`.
4. Xác minh: API verification trả một chữ ký `Ky_1_..._v...`, `ToanVenMatMa=true`, `BaoPhuToanBoBanHienTai=true`; tải PDF đã ký kiểm tra bằng phần mềm PDF độc lập.
5. Nhấn `Trình duyệt` → trạng thái `CHO_DUYET`; bảng `ChuKySo` có `KY_NGUOI_SOAN` với `SoPhienBan` hiện tại, bảng `BanPdfDaKy` có hai bản (0 và 1).
6. Đăng nhập bằng người **khác** có quyền ký, thử ký cùng văn bản → **403**; ký lại trên cùng phiên bản → **409**. Gửi lại cùng `MaYeuCau` phải trả cùng kết quả, không thêm chữ ký.
7. Thử sửa tệp trong bản nháp *sau khi ký* để tạo phiên bản mới → phải ký lại; không thể trình duyệt bằng chữ ký của phiên bản cũ.

**Ranh giới 0.5.2:** sau khi trình duyệt, các bước ký của trưởng phòng, trưởng văn phòng và lãnh đạo **chưa** được chuyển sang luồng 5 cấp. Trên nhánh 0.5 không dùng chức năng ký ba cấp cũ để hoàn thành phát hành với văn bản mới đã ký người soạn, vì thứ tự số bước đã đổi. Chưa triển khai bảo toàn chữ ký trong bước cấp số (0.5.4). Không merge vào main hoặc thử với dữ liệu thật trước khi đủ 0.5.3–0.5.5.

## Chặng 0.5.3: ba cấp ký trước phê duyệt (đã viết mã)

**Điều kiện:** đã hoàn thành kiểm thử người soạn ký bước 1, trình duyệt thành công; các tài khoản `truongphong_a`, `truongvanphong_a`, `lanhdao_a` có chứng thư cá nhân đang hoạt động. Hãy bảo toàn đúng `Security__EncryptionKeyBase64`, `Security__PfxPassword` và `Security__ActiveKeyId` đã dùng khi tạo chứng thư; đừng tạo khóa mới giữa lần ký.

1. Đăng nhập `truongphong_a`, mở văn bản trong **Văn bản đi**, xem tab **Chữ ký số**; nếu chưa có chứng thư cá nhân thì tạo chứng thư thử nghiệm. Nút **Phê duyệt** phải bị khóa; thử gọi API quyết định `DUYET` trực tiếp phải bị HTTP **409**.
2. Nhấn **2/5. Trưởng phòng ký nháy**, nhập mật khẩu tài khoản, xác minh có **hai** chữ ký mật mã nguyên vẹn. Nút Phê duyệt mở, nhấn duyệt; nhiệm vụ chuyển sang trưởng văn phòng.
3. Đăng nhập `truongvanphong_a`, tạo chứng thư cá nhân nếu cần. Phải thấy **3/5. Trưởng văn phòng ký thể thức**. Trước khi ký, duyệt API trả 409; sau khi ký xác minh **ba** chữ ký, duyệt chuyển lãnh đạo.
4. Đăng nhập `lanhdao_a`, chọn **4/5. Lãnh đạo ký phê duyệt**. Xác minh **bốn** chữ ký nguyên vẹn; nhấn duyệt, văn bản chuyển `CHO_CAP_SO`.
5. **Kiểm thử bảo mật:** các tài khoản không được giao nhiệm vụ không thể ký thay (**403**); ký 3 trước khi có ký 2 hoặc ký 4 trước khi có ký 3 bị chặn; ký hai lần cùng bước bị **409**; gửi lại cùng `MaYeuCau` cho kết quả cũ, không nhân bản.
6. **Kiểm thử trả sửa:** ở cấp 2/3/4, chọn **Trả sửa** (có lý do). Người soạn cập nhật PDF/tệp thành phiên bản mới, ký lại bước 1; các chữ ký của phiên bản cũ không đủ để duyệt phiên bản mới.
7. **Kiểm thử file:** tải PDF đã ký ở mỗi cấp, xác minh số chữ ký CMS bằng công cụ PDF độc lập, mỗi bản mới nối tiếp bản trước và hash SHA-256 lưu ở `BanPdfDaKy` không thay đổi.

**Còn thiếu ở 0.5.3:** hình ảnh chữ ký hiển thị trực tiếp trên PDF (ký CMS hiện vẫn vô hình); vị trí ký do người dùng chọn sẽ triển khai riêng. Chặng 0.5.4 phải bảo toàn bốn chữ ký sau cấp số rồi mới ký dấu cơ quan lần 5. Không thử nút chuẩn bị ký cũ sau cấp số và không phát hành bản chưa có ký cơ quan.

## Chặng 0.5.4: ký số cơ quan và giao nhận (chờ nghiệm thu)

**Điều kiện:** văn bản đang ở `CHO_CAP_SO` sau khi lãnh đạo ký 4/5 và duyệt. Tài khoản `vanthu_a` có quyền `registers.manage`, `signatures.manage-agency`, `signing.sign`, `publications.issue`, `publications.deliver`. Dùng chính bộ khóa AES/PFX đã tạo ở 0.5.2, không tạo mới.

1. Đăng nhập `vanthu_a`, mở văn bản, nhấn **Cấp số văn bản**, nhập ký hiệu (ví dụ `CV-CQA`). Hệ thống kiểm tra bốn chữ ký CMS/hashes rồi cập nhật `SoPhatHanh`, `KyHieu`, `NgayBanHanh`; trạng thái `DA_CAP_SO`. Nếu không đủ bốn chữ ký hợp lệ, API cấp số trả **409**.
2. Vào tab **Chữ ký số**, chọn loại chứng thư **Cơ quan (văn thư)**. Nhập tên, chọn tự ký thử nghiệm, mật khẩu tài khoản, nhấn **Lưu chứng thư**. Có thể tạo thêm chứng thư cơ quan dù đã có chứng thư cá nhân.
3. Nhập lại mật khẩu tài khoản, nhấn **5/5. Văn thư ký số cơ quan**. API `POST /api/v1/documents/{id}/agency-signature` nhận `{ "loaiChuKy":"KY_CO_QUAN", "matKhau":"...", "maYeuCau":"<UUID mới>" }`. Trạng thái chuyển thành `DA_KY`; API verification phải trả **5 chữ ký** đều `ToanVenMatMa=true`, chữ ký cuối `BaoPhuToanBoBanHienTai=true`.
4. Nhấn **Phát hành đến các cơ quan dự kiến**. Trạng thái `DA_PHAT_HANH` và mỗi bên nhận có một lượt `CHO_GUI`. Nếu chưa có chữ ký thứ 5, API phát hành trả 409.
5. Vào **Giao nhận liên cơ quan** (tài khoản bên gửi), nhấn **Gửi văn bản** với từng cơ quan. Đăng nhập `vanthu_b` hoặc `vanthu_c` để kiểm tra **Văn bản đến**; tải PDF ký 5 lần, xem phụ lục Word/Excel/PDF. Xác minh PDF cuối bằng trình đọc PDF độc lập, đối chiếu hash các tệp và lịch sử gửi/nhận.
6. **Kiểm thử âm:** ký cơ quan khi chưa cấp số, ký bằng chứng thư cá nhân thay cho chứng thư cơ quan, ký lại lần 5, sửa dữ liệu PDF trước gửi, hoặc xem tệp khi chưa gửi phải bị từ chối. Gửi lại cùng `MaYeuCau` không sinh lượt giao nhận trùng.

**Giới hạn phải biết trước khi nghiệm thu:** Để bảo toàn bốn chữ ký trước, phiên bản này **chỉ ghi số/ký hiệu/ngày phát hành trong database và lịch sử, không viết lại nội dung trang PDF đã ký**. File PDF cuối cùng gồm năm CMS theo kiểu cập nhật tăng dần; nếu PDF gốc không chứa số/ký hiệu, trang PDF phát hành vẫn chưa thể hiện số đó bằng chữ. Cần hoàn thiện *vùng hiển thị số và dấu* trong một revision ký hợp lệ và kiểm tra bằng Adobe Acrobat/ứng dụng độc lập trước khi dùng cho văn bản chính thức. Không coi 0.5.4 đã đạt yêu cầu thể thức văn bản pháp lý.

## Chặng 0.5.5: vùng ký hiển thị và kiểm thử độc lập (chờ nghiệm thu)

**Quy tắc:** dùng **văn bản thử nghiệm mới** để có đủ năm chữ ký kèm hình ảnh. Không được sửa các PDF đã ký của văn bản cũ để thêm ảnh. Mỗi người chuẩn bị PNG nền trong suốt riêng (văn thư chuẩn bị PNG con dấu thử nghiệm). Chỉ kiểm tra mẫu giả lập, không dùng ảnh chữ ký/con dấu thật trong môi trường demo.

1. Ở tab **Chữ ký số**, trước khi ký chọn ảnh `.png` tối đa 1 MB, kích thước mỗi chiều tối đa 4096 px. Chọn trang 1 và vị trí `trái=60%`, `trên=73%`, `rộng=32%`, `cao=11%` (thay theo bố cục tài liệu). Tỷ lệ từ mép trái/trên; không cho vùng vượt trang. Đối với các trang PDF có rotation khác 0, API từ chối vì chưa hỗ trợ chính xác tọa độ.
2. Ký lần 1 người soạn. Tải PDF và kiểm tra vùng hình ký hiển thị, chữ ký CMS có `ToanVenMatMa=true`. Sau đó trình duyệt.
3. Lần lượt dùng trưởng phòng, trưởng văn phòng, lãnh đạo: mỗi người chọn PNG riêng và vị trí khác không che nội dung, ký lần 2/3/4, xác minh số chữ ký tăng đúng thứ tự; thử duyệt khi chưa ký vẫn trả 409.
4. Văn thư cấp số, tạo/nhập chứng thư cơ quan và chọn PNG con dấu. Lần ký 5 phải có nhãn số/ký hiệu trong vùng chữ ký và hình dấu (tùy bố cục), số trong database phải khớp nội dung vùng ký. Xác minh cả năm chữ ký CMS và tải bản PDF cuối.
5. Mở PDF bằng **Adobe Acrobat Reader** hoặc một trình xác minh PDF độc lập: ghi nhận trạng thái xác minh từng chữ ký, việc trình đọc báo tài liệu có bản cập nhật sau chữ ký trước và chứng thư tự ký không tin cậy. Không đánh đồng `ToanVenMatMa=true` với trạng thái tin cậy của CA hay chữ ký có giá trị pháp lý.
6. Kiểm thử âm: ảnh JPEG/SVG, ảnh hơn 1 MB, trang ngoài phạm vi, vùng vượt mép, trang bị xoay, chữ ký sai tài khoản, tệp PDF bị sửa, xác minh các hash và các bản lịch sử bất biến.
7. Phát hành → gửi từ A → tiếp nhận ở B, mở **Xem PDF** trực tiếp trong **Văn bản đến**; tải và so sánh PDF, phụ lục, hành trình, ngày giờ và trạng thái từng cơ quan. Cơ quan chưa nhận không được đọc bản nháp.

**Giới hạn kỹ thuật hiện tại:** Các vị trí được nhập dưới dạng phần trăm, chưa có thao tác kéo-thả trực tiếp trên PDF; bản xem PDF sử dụng trình xem PDF nhúng của trình duyệt. Tính chính xác/khả năng hiển thị vùng ký iText phải được thử trên .NET 8 và từng mẫu PDF thực tế. Chức năng ảnh chỉ áp dụng cho **chữ ký mới**, không thêm ảnh vào chữ ký lịch sử. Chưa có kiểm định chữ ký pháp lý CA, thu hồi OCSP/CRL hay timestamp TSA.
