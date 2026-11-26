# Nghiệm thu 0.6.4.2 — Ủy quyền ký thay một tác vụ

## Chuẩn bị

1. Sao lưu database `QuanLyVanBanChuKySo`.
2. Kéo mã nguồn nhánh `feat/v0.5-ky-so-nam-cap`.
3. Chạy `database/015_uy_quyen_ky_theo_tac_vu.sql` trong SSMS, sau script `014`.
4. Chạy `dotnet build` trong `backend`, khởi động lại bằng `start-backend.ps1` từ thư mục gốc.
5. Chạy frontend `npm run dev`, đăng xuất/đăng nhập lại quản trị viên.

## Nguyên tắc

- Không thay đổi số vị trí chữ ký (văn bản phòng: 4; lãnh đạo tự soạn: 3).
- Chỉ hỗ trợ ủy quyền tác vụ `KIEM_TRA_NOI_DUNG` hoặc `PHE_DUYET_LANH_DAO` đang chờ. Không ủy quyền người soạn hoặc văn thư ký con dấu.
- Người được ủy quyền phải có chức vụ phù hợp, cùng phòng ban với người được giao, không phải người tạo và chưa ký phiên bản PDF này.
- Người được giao là trưởng phòng/lãnh đạo chính thì chỉ có thể ủy quyền cho cấp phó cùng phòng/Ban lãnh đạo; cấp phó có thể ủy quyền cho cấp trưởng hoặc cấp phó khác.
- Có hiệu lực từ lúc cấp đến hết hạn; chữ ký CMS lưu tên và chứng thư **người ký thực tế**, không giả danh người giao.
- Không được ghi đè một ủy quyền đã tạo cho cùng tác vụ. Việc đổi người được ủy quyền trong một tác vụ cần thiết kế nhật ký thay thế ở bản tiếp theo.
- Nếu đã ký PDF hợp lệ trước thời điểm hết hạn, người ký được hoàn tất phê duyệt sau khi hết hạn. Không thể thu hồi ủy quyền khi chữ ký đã được tạo.
- Ủy quyền được ghi vào `LichSuVanBan`, quyết định và chữ ký được ghi tên người thực hiện.

## Kịch bản

1. Tạo văn bản mới, người soạn ký PDF và trình Trưởng phòng/Phó phòng như bình thường.
2. Đăng nhập quản trị cơ quan, vào **Ủy quyền ký thay**. Chọn tác vụ ký nháy đang chờ.
3. Chọn một cấp phó đủ điều kiện, đặt thời hạn `2` giờ, nhập lý do (ít nhất 10 ký tự), nhấn **Cấp ủy quyền**.
4. Đăng nhập tài khoản được ủy quyền, mở **Việc cần xử lý**, mở văn bản, ký PDF bằng chứng thư của chính người này, rồi nhấn **Phê duyệt**.
5. Kiểm tra **Hành trình văn bản**: có sự kiện cấp ủy quyền, ký PDF và phê duyệt với đúng người ký thực tế.
6. Lặp lại với văn bản thử khác và ủy quyền lãnh đạo ký cho Phó lãnh đạo, rồi phát hành/giao nhận như cũ.
7. Thử chọn người khác phòng, người soạn, tài khoản không có vai trò thích hợp: phải bị chặn.
8. Với tác vụ chưa ký, nhấn **Thu hồi ủy quyền**, xác nhận người được ủy quyền không còn thấy tác vụ trong hàng đợi.
9. Thử hết hạn ủy quyền trước khi ký: người được ủy quyền không thể ký tiếp.

## Giới hạn

Backend/.NET và SQL Server phải được build/kiểm thử tại máy Windows của người dùng; môi trường triển khai hiện tại chỉ xác minh frontend.
