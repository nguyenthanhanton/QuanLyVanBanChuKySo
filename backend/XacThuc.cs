using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;

public sealed record YeuCauDangNhap(string TenDangNhap, string MatKhau);
public sealed record SuaHoTenCaNhan(string HoTen);
public sealed record DoiMatKhauCaNhan(string MatKhauHienTai,string MatKhauMoi);
public sealed class TaiKhoanDangNhap {
    public int MaNguoiDung { get; set; }
    public int? MaCoQuan { get; set; }
    public string TenDangNhap { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string MatKhauBam { get; set; } = "";
    public bool DangHoatDong { get; set; }
    public int SoLanSai { get; set; }
    public DateTime? KhoaDen { get; set; }
}

public static class MatKhau
{
    private const int SoVong = 210_000;
    public static string TaoBam(string matKhau)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(matKhau, salt, SoVong, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${SoVong}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool KiemTra(string matKhau, string? duLieu)
    {
        try {
            var phan = (duLieu ?? "").Split('$');
            if (phan.Length != 4 || phan[0] != "pbkdf2-sha256") return false;
            if (!int.TryParse(phan[1], out var soVong) || soVong < 100_000 || soVong > 2_000_000) return false;
            var salt = Convert.FromBase64String(phan[2]);
            var expected = Convert.FromBase64String(phan[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(matKhau, salt, soVong, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        } catch { return false; }
    }
}

public sealed class XacThuc(IConfiguration config)
{
    public string TaoToken(NguoiThucHien user)
    {
        var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, user.TenDangNhap),
            new Claim("uid", user.MaNguoiDung.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims,
            expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

public static class XacThucEndpoints
{
    public static void MapXacThuc(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", async (YeuCauDangNhap req, Database db, XacThuc auth, PhanQuyen permission) => {
            if (string.IsNullOrWhiteSpace(req.TenDangNhap) || string.IsNullOrWhiteSpace(req.MatKhau))
                return Results.BadRequest(new { message = "Cần nhập tên đăng nhập và mật khẩu." });
            using var cn = db.Open();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
            var account = await cn.QuerySingleOrDefaultAsync<TaiKhoanDangNhap>(@"
                SELECT n.MaNguoiDung, n.MaCoQuan, n.TenDangNhap, n.HoTen,
                       n.MatKhauBam, n.DangHoatDong, n.SoLanSai, n.KhoaDen
                FROM dbo.NguoiDung n WITH (UPDLOCK, ROWLOCK)
                LEFT JOIN dbo.CoQuan c ON c.MaCoQuan=n.MaCoQuan
                WHERE n.TenDangNhap=@TenDangNhap
                  AND (n.MaCoQuan IS NULL OR c.DangHoatDong=1)",
                new { req.TenDangNhap }, tx);
            if (account == null || !account.DangHoatDong) {
                tx.Commit();
                return Results.Unauthorized();
            }
            if (account.KhoaDen.HasValue && account.KhoaDen.Value > DateTime.UtcNow) {
                tx.Commit();
                return Results.Json(new { message = "Tài khoản tạm khóa sau 5 lần đăng nhập sai." }, statusCode: 423);
            }
            if (!MatKhau.KiemTra(req.MatKhau, account.MatKhauBam)) {
                await cn.ExecuteAsync(@"
                    UPDATE dbo.NguoiDung
                    SET SoLanSai=CASE WHEN KhoaDen <= SYSUTCDATETIME() THEN 1 ELSE SoLanSai+1 END,
                        KhoaDen=CASE WHEN (CASE WHEN KhoaDen <= SYSUTCDATETIME() THEN 1 ELSE SoLanSai+1 END)>=5
                            THEN DATEADD(MINUTE,5,SYSUTCDATETIME()) ELSE NULL END
                    WHERE MaNguoiDung=@id", new { id = account.MaNguoiDung }, tx);
                tx.Commit();
                return Results.Unauthorized();
            }
            await cn.ExecuteAsync("UPDATE dbo.NguoiDung SET SoLanSai=0,KhoaDen=NULL WHERE MaNguoiDung=@id",
                new { id = account.MaNguoiDung }, tx);
            tx.Commit();
            var user = new NguoiThucHien(account.MaNguoiDung, account.MaCoQuan, account.TenDangNhap, account.HoTen);
            return Results.Ok(new { accessToken = auth.TaoToken(user), user, capabilities = await permission.DanhSachQuyen(user) });
        });

        group.MapGet("/me", async (HttpContext http, PhanQuyen permissions) => {
            var actor = await permissions.NguoiHienTai(http);
            return actor is null ? Results.Unauthorized() : Results.Ok(new {
                actor.MaNguoiDung, actor.MaCoQuan, actor.TenDangNhap, actor.HoTen,
                capabilities = await permissions.DanhSachQuyen(actor)
            });
        }).RequireAuthorization();

        group.MapGet("/profile",async(HttpContext http,PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor is null)return Results.Unauthorized();
            using var cn=db.Open();
            var profile=await cn.QuerySingleOrDefaultAsync(@"
                SELECT n.MaNguoiDung,n.TenDangNhap,n.HoTen,n.ChucVu,
                       n.MaCoQuan,c.TenCoQuan,p.TenPhongBan,
                       (SELECT STRING_AGG(r.TenVaiTro,N', ')
                        FROM dbo.NguoiDungVaiTro ur JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro
                        WHERE ur.MaNguoiDung=n.MaNguoiDung AND ur.MaCoQuan=n.MaCoQuan) AS VaiTro
                FROM dbo.NguoiDung n
                LEFT JOIN dbo.CoQuan c ON c.MaCoQuan=n.MaCoQuan
                LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=n.MaPhongBan AND p.MaCoQuan=n.MaCoQuan
                WHERE n.MaNguoiDung=@user AND n.DangHoatDong=1",
                new{user=actor.MaNguoiDung});
            return profile is null?Results.NotFound():Results.Ok(profile);
        }).RequireAuthorization();

        group.MapPut("/profile/name",async(SuaHoTenCaNhan body,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor is null)return Results.Unauthorized();
            var name=body.HoTen?.Trim()??"";
            if(name.Length is < 3 or > 150)return Results.BadRequest(new{message="Họ tên phải có 3–150 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var changed=await cn.ExecuteAsync(@"
                UPDATE dbo.NguoiDung SET HoTen=@name WHERE MaNguoiDung=@user AND DangHoatDong=1",
                new{name,user=actor.MaNguoiDung},tx);
            if(changed!=1){tx.Rollback();return Results.NotFound();}
            await cn.ExecuteAsync(@"
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@agency,@user,'NGHIEP_VU','CAP_NHAT_HO_TEN',N'Người dùng tự cập nhật họ tên')",
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung},tx);
            tx.Commit();return Results.Ok(new{message="Đã cập nhật họ tên.",hoTen=name});
        }).RequireAuthorization();

        group.MapPut("/profile/password",async(DoiMatKhauCaNhan body,HttpContext http,
            PhanQuyen permissions,Database db)=>{
            var actor=await permissions.NguoiHienTai(http);
            if(actor is null)return Results.Unauthorized();
            if(string.IsNullOrEmpty(body.MatKhauHienTai) || string.IsNullOrEmpty(body.MatKhauMoi) ||
               body.MatKhauMoi.Length is < 12 or > 128)
                return Results.BadRequest(new{message="Cần nhập mật khẩu hiện tại và mật khẩu mới dài 12–128 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var oldHash=await cn.ExecuteScalarAsync<string?>(@"
                SELECT MatKhauBam FROM dbo.NguoiDung WITH(UPDLOCK,HOLDLOCK)
                WHERE MaNguoiDung=@user AND DangHoatDong=1",
                new{user=actor.MaNguoiDung},tx);
            if(oldHash is null){tx.Rollback();return Results.Unauthorized();}
            if(!MatKhau.KiemTra(body.MatKhauHienTai,oldHash)){
                tx.Rollback();return Results.BadRequest(new{message="Mật khẩu hiện tại không đúng."});
            }
            if(MatKhau.KiemTra(body.MatKhauMoi,oldHash)){
                tx.Rollback();return Results.BadRequest(new{message="Mật khẩu mới phải khác mật khẩu hiện tại."});
            }
            await cn.ExecuteAsync(@"
                UPDATE dbo.NguoiDung SET MatKhauBam=@hash,SoLanSai=0,KhoaDen=NULL
                WHERE MaNguoiDung=@user",
                new{hash=MatKhau.TaoBam(body.MatKhauMoi),user=actor.MaNguoiDung},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@agency,@user,'NGHIEP_VU','DOI_MAT_KHAU',N'Người dùng tự đổi mật khẩu')",
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung},tx);
            tx.Commit();return Results.Ok(new{message="Đã đổi mật khẩu. Vui lòng đăng nhập lại."});
        }).RequireAuthorization();

        group.MapGet("/capabilities", async (HttpContext http, PhanQuyen permissions) => {
            var actor = await permissions.NguoiHienTai(http);
            return actor is null ? Results.Unauthorized() : Results.Ok(await permissions.DanhSachQuyen(actor));
        }).RequireAuthorization();
    }
}
