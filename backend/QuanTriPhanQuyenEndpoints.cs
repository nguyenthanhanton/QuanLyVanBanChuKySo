using Dapper;
using Microsoft.Data.SqlClient;

public sealed record CapQuyenNguoiDung(string MaDinhDanh,bool? ChoPhep);

public static class QuanTriPhanQuyenEndpoints
{
    private static readonly string[] QuyenBaoVe = [
        "permissions.manage","users.manage","departments.manage","settings.manage",
        "agency.admins.manage","agencies.manage","audit.technical.read"
    ];

    private static async Task<bool> DuocQuanLy(SqlConnection cn,System.Data.IDbTransaction? tx,int agency,int user)
    {
        return await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.NguoiDung n
            WHERE n.MaNguoiDung=@user AND n.MaCoQuan=@agency
              AND NOT EXISTS(SELECT 1 FROM dbo.NguoiDungVaiTro uv
                JOIN dbo.VaiTro v ON v.MaVaiTro=uv.MaVaiTro
                WHERE uv.MaNguoiDung=n.MaNguoiDung AND uv.MaCoQuan=@agency
                  AND v.MaDinhDanh IN ('AGENCY_ADMIN','SYSTEM_ADMIN'))",
            new{agency,user},tx)>0;
    }

    public static void MapQuanTriPhanQuyen(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin").RequireAuthorization();

        group.MapGet("/users/{id:int}/permissions",async(int id,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"permissions.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            using var cn=db.Open();
            if(!await DuocQuanLy(cn,null,actor.MaCoQuan.Value,id))return Results.NotFound();
            var rows=await cn.QueryAsync(@"
                SELECT q.MaDinhDanh,q.TenQuyen,ov.ChoPhep,
                  CAST(CASE WHEN EXISTS(
                    SELECT 1 FROM dbo.NguoiDungVaiTro uv
                    JOIN dbo.VaiTroQuyen vq ON vq.MaVaiTro=uv.MaVaiTro AND vq.MaCoQuan=uv.MaCoQuan
                    WHERE uv.MaNguoiDung=@id AND uv.MaCoQuan=@agency AND vq.MaQuyen=q.MaQuyen
                  ) THEN 1 ELSE 0 END AS BIT) AS TuVaiTro
                FROM dbo.Quyen q
                LEFT JOIN dbo.PhanQuyenNguoiDung ov
                    ON ov.MaQuyen=q.MaQuyen AND ov.MaNguoiDung=@id AND ov.MaCoQuan=@agency
                WHERE q.MaDinhDanh NOT IN @protected
                ORDER BY q.TenQuyen",
                new{id,agency=actor.MaCoQuan,@protected=QuyenBaoVe});
            return Results.Ok(rows);
        });

        group.MapPut("/users/{id:int}/permissions",async(int id,CapQuyenNguoiDung body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"permissions.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null || actor.MaNguoiDung==id)return Results.Forbid();
            if(string.IsNullOrWhiteSpace(body.MaDinhDanh) || QuyenBaoVe.Contains(body.MaDinhDanh))
                return Results.BadRequest(new{message="Không thể thay đổi quyền quản trị được bảo vệ."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if(!await DuocQuanLy(cn,tx,actor.MaCoQuan.Value,id)){
                tx.Rollback();return Results.NotFound();
            }
            var q=await cn.ExecuteScalarAsync<int?>("SELECT MaQuyen FROM dbo.Quyen WHERE MaDinhDanh=@code",
                new{code=body.MaDinhDanh},tx);
            if(q==null){tx.Rollback();return Results.BadRequest(new{message="Không tìm thấy quyền nghiệp vụ."});}
            if(body.ChoPhep==null){
                await cn.ExecuteAsync(@"DELETE FROM dbo.PhanQuyenNguoiDung
                    WHERE MaCoQuan=@agency AND MaNguoiDung=@id AND MaQuyen=@q",
                    new{agency=actor.MaCoQuan,id,q},tx);
            }else{
                await cn.ExecuteAsync(@"
                    UPDATE dbo.PhanQuyenNguoiDung WITH(UPDLOCK,HOLDLOCK)
                    SET ChoPhep=@allowed,MaNguoiCap=@admin,ThoiDiemCap=SYSUTCDATETIME()
                    WHERE MaCoQuan=@agency AND MaNguoiDung=@id AND MaQuyen=@q;
                    IF @@ROWCOUNT=0
                      INSERT dbo.PhanQuyenNguoiDung(MaCoQuan,MaNguoiDung,MaQuyen,ChoPhep,MaNguoiCap)
                      VALUES(@agency,@id,@q,@allowed,@admin);",
                    new{agency=actor.MaCoQuan,id,q,allowed=body.ChoPhep.Value,admin=actor.MaNguoiDung},tx);
            }
            await cn.ExecuteAsync(@"
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@agency,@admin,'NGHIEP_VU','DOI_QUYEN_NGUOI_DUNG',@description)",
                new{agency=actor.MaCoQuan,admin=actor.MaNguoiDung,
                    description=$"Tài khoản {id}: {body.MaDinhDanh} -> {(body.ChoPhep==null?"mặc định":body.ChoPhep.Value?"cho phép":"từ chối")}"},tx);
            tx.Commit();return Results.Ok(new{maNguoiDung=id,maDinhDanh=body.MaDinhDanh,choPhep=body.ChoPhep});
        });
    }
}
