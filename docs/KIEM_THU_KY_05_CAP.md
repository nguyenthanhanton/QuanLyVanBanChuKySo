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
2. **0.5.3:** ba cấp duyệt được giao ký lần lượt 2, 3, 4; không ký thì nút duyệt/API không cho chuyển; từ chối cho phép trả sửa.
3. **0.5.4:** cấp số không phá bốn chữ ký; chứng thư cơ quan ký lần 5, chỉ phát hành sau xác minh năm chữ ký.
4. **0.5.5:** giao diện hiển thị đủ năm vị trí, chữ ký hợp lệ và lịch sử theo người/thời gian, thử liên cơ quan A → B/C.

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
