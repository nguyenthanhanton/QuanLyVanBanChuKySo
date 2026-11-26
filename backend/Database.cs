using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

public sealed class Database(IConfiguration config)
{
    private readonly string _chuoiKetNoi = config.GetConnectionString("QuanLyVanBan")!;
    public SqlConnection Open() => new(_chuoiKetNoi);
}

public sealed record NguoiThucHien(int MaNguoiDung, int? MaCoQuan, string TenDangNhap, string HoTen);

public sealed class PhanQuyen(Database db)
{
    public async Task<NguoiThucHien?> NguoiHienTai(HttpContext http)
    {
        if (!http.User.Identity?.IsAuthenticated ?? true) return null;
        if (!int.TryParse(http.User.FindFirst("uid")?.Value, out var id)) return null;
        using var cn = db.Open();
        return await cn.QuerySingleOrDefaultAsync<NguoiThucHien>(@"
            SELECT n.MaNguoiDung, n.MaCoQuan, n.TenDangNhap, n.HoTen
            FROM dbo.NguoiDung n LEFT JOIN dbo.CoQuan c ON n.MaCoQuan=c.MaCoQuan
            WHERE n.MaNguoiDung=@id AND n.DangHoatDong=1
              AND (n.MaCoQuan IS NULL OR c.DangHoatDong=1)", new { id });
    }

    public async Task<string[]> DanhSachQuyen(NguoiThucHien user)
    {
        using var cn = db.Open();
        var quyen = await cn.QueryAsync<string>(@"
            WITH QuyenVaiTro AS (
                SELECT DISTINCT q.MaQuyen,q.MaDinhDanh
                FROM dbo.NguoiDungVaiTro nvt
                JOIN dbo.VaiTroQuyen vtq ON vtq.MaVaiTro=nvt.MaVaiTro
                    AND ((vtq.MaCoQuan=nvt.MaCoQuan) OR (vtq.MaCoQuan IS NULL AND nvt.MaCoQuan IS NULL))
                JOIN dbo.Quyen q ON q.MaQuyen=vtq.MaQuyen
                WHERE nvt.MaNguoiDung=@MaNguoiDung
                  AND ((nvt.MaCoQuan=@MaCoQuan) OR (nvt.MaCoQuan IS NULL AND @MaCoQuan IS NULL))
            )
            SELECT DISTINCT q.MaDinhDanh FROM dbo.Quyen q
            LEFT JOIN QuyenVaiTro inherited ON inherited.MaQuyen=q.MaQuyen
            LEFT JOIN dbo.PhanQuyenNguoiDung pq
              ON pq.MaQuyen=q.MaQuyen AND pq.MaNguoiDung=@MaNguoiDung
                 AND pq.MaCoQuan=@MaCoQuan
            WHERE (pq.ChoPhep=1 OR (pq.MaQuyen IS NULL AND inherited.MaQuyen IS NOT NULL))",
            user);
        return quyen.ToArray();
    }

    // Cau hinh UI khong quyet dinh quyen. Moi API goi ham nay truoc khi truy van du lieu.
    public async Task<(NguoiThucHien? Actor, IResult? Error)> YeuCau(HttpContext http, string quyen)
    {
        if (!http.User.Identity?.IsAuthenticated ?? true) return (null, Results.Unauthorized());
        var actor = await NguoiHienTai(http);
        if (actor == null) return (null, Results.Unauthorized());
        var capabilities = await DanhSachQuyen(actor);
        if (!capabilities.Contains(quyen, StringComparer.Ordinal))
            return (null, Results.StatusCode(StatusCodes.Status403Forbidden));
        return (actor, null);
    }
}
