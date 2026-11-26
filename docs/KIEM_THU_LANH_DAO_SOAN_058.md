# Kiểm thử lãnh đạo soạn — quy trình 3 chữ ký

## Nâng cấp

1. Sao lưu SQL Server, chạy `database/010_lanh_dao_soan_ba_chu_ky.sql` **sau 009**.
2. Xác nhận `SoPhienBan=10` trong `dbo.PhienBanLuocDo`; không chỉnh sửa các văn bản 4/5 cấp đã có.
3. Cập nhật GitHub, `dotnet build`, khởi động lại backend và frontend. Đăng nhập lại để tải quyền mới.

## Quy tắc

| Người soạn | Người được chọn ký tiếp | Bỏ qua |
| --- | --- | --- |
| Lãnh đạo cấp trưởng `LANH_DAO` | Một người có `PHO_LANH_DAO` cùng cơ quan | Trưởng/phó phòng |
| Lãnh đạo cấp phó `PHO_LANH_DAO` | Lãnh đạo cấp trưởng hoặc cấp phó khác cùng cơ quan | Trưởng/phó phòng |
| Người soạn thuộc phòng ban | Trưởng/phó phòng phù hợp, sau đó lãnh đạo | Không bỏ bước ký nháy |

Người soạn không thể chọn chính mình. Danh sách và lệnh `/submit` đều áp dụng quy tắc. Nếu chưa có lãnh đạo khác được bổ nhiệm đúng vai trò, hệ thống không cho trình duyệt.

## Kịch bản

1. Đăng nhập `lanhdao_a`, tạo văn bản và ký người soạn (1/3).
2. Bấm **Trình duyệt**: chỉ hiện các lãnh đạo cấp phó cùng cơ quan (phải được quản trị gán vai trò `PHO_LANH_DAO` từ trước). Không thấy danh sách trưởng phòng.
3. Chọn cấp phó, trình duyệt: hệ thống tạo trực tiếp công việc `PHE_DUYET_LANH_DAO`.
4. Đăng nhập cấp phó được chọn, ký PDF lãnh đạo (2/3), phê duyệt; văn bản chuyển `CHO_CAP_SO`.
5. Văn thư cấp số, ký số cơ quan (3/3), phát hành và gửi. Kiểm tra cả 3 chữ ký CMS trên cùng PDF.
6. Thử giả mạo mã trưởng phòng hoặc chính người soạn trong lệnh `/submit`: phải bị từ chối.
7. Thử văn bản do cấp phó soạn: có thể chọn cấp trưởng hoặc cấp phó **khác**, không thấy chính mình.
8. Văn bản do `vanthu_a` soạn vẫn dùng quy trình 4 chữ ký, không thay đổi.

**Chú ý:** Chưa có tài khoản cấp phó thử nghiệm được tạo tự động. Không thêm tài khoản hoặc cấp quyền nếu chưa được quản trị cơ quan phê duyệt. Chứng thư tự ký chỉ để thử nghiệm.
