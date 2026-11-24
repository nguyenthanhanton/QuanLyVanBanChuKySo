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

1. **0.5.2:** tài khoản tác giả có chứng thư cá nhân ký trực tiếp PDF nháp; submit trước ký trả 409; người khác không ký thay.
2. **0.5.3:** ba cấp duyệt được giao ký lần lượt 2, 3, 4; không ký thì nút duyệt/API không cho chuyển; từ chối cho phép trả sửa.
3. **0.5.4:** cấp số không phá bốn chữ ký; chứng thư cơ quan ký lần 5, chỉ phát hành sau xác minh năm chữ ký.
4. **0.5.5:** giao diện hiển thị đủ năm vị trí, chữ ký hợp lệ và lịch sử theo người/thời gian, thử liên cơ quan A → B/C.

Các chặng sau bắt buộc thử bằng .NET 8/SQL Server và trình đọc PDF độc lập. Không coi một bản ghi trong `ChuKySo` là bằng chứng chữ ký PDF hợp lệ nếu chưa xác minh CMS và toàn vẹn bản PDF.
