# Quản lý các phiên bản bằng Git (Windows + GitHub)

## Mục đích

Giữ nguyên database SQL Server và thư mục tệp đính kèm trên máy chủ; Git chỉ quản lý **mã nguồn** (`backend`, `web`, `database`, `docs`).

Không đưa lên GitHub: `web/.env`, khóa JWT, PFX/P12, mật khẩu, kho ký số, thư mục lưu file của người dùng, bản sao lưu database.

## Thiết lập một lần tại thư mục dự án trên máy

Mở PowerShell trong thư mục có `backend`, `web`, `database`, `README.md`:

```powershell
git --version
git status
```

Nếu dự án chưa có Git, dùng `git init -b main`. Nếu đã có Git, **không** chạy `git init` lần nữa và không xóa `.git`.

Kiểm tra tệp sắp được lưu:

```powershell
git status --short
git ls-files
```

Nếu cần lưu bản mới, thực hiện:

```powershell
git add backend web database docs README.md .gitignore
git diff --cached --stat
git commit -m "feat: quan ly van ban v0.3"
```

**Luôn xem `git diff --cached` trước khi commit** để bảo đảm không có mật khẩu hay bí mật.

## Kết nối GitHub

Tạo repository **trống** (không chọn Initialize with README) hoặc chọn chính xác repository hiện có muốn dùng. Sau đó:

```powershell
git remote -v
git remote add origin https://github.com/TEN_TAI_KHOAN/TEN_REPOSITORY.git
git push -u origin main
```

Chỉ sử dụng URL đúng của repository đã chọn. Nếu `origin` đã tồn tại, kiểm tra URL bằng `git remote -v` trước khi quyết định sửa.

## Mỗi phiên bản tiếp theo

Trên máy đang phát triển:

```powershell
git status
git pull --ff-only
git switch -c feat/xem-truoc-tep
# Lập trình và kiểm thử
git add backend web database docs README.md .gitignore
git diff --cached
git commit -m "feat: xem truoc tep v0.4"
git push -u origin feat/xem-truoc-tep
```

Gộp nhánh vào `main` qua pull request sau khi kiểm thử. Trên máy chạy phần mềm, `git pull --ff-only` để lấy code mới, sau đó chạy migration SQL **mới** (nếu có), restart backend/frontend. Không chạy lại các script khởi tạo database.

> GitHub không tự chạy SQL migration và không tự cập nhật ứng dụng đang chạy. `git pull` chỉ cập nhật file được Git quản lý.
