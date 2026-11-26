using Dapper;

public sealed record DoiTenCoQuan(string TenCoQuan);

public static class QuanTriThongTinCoQuanEndpoints
{
    public static void MapQuanTriThongTinCoQuan(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin/agency").RequireAuthorization();

        group.MapGet("",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"settings.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            using var cn=db.Open();
            var row=await cn.QuerySingleOrDefaultAsync(@"
                SELECT MaCoQuan,MaDinhDanh,TenCoQuan,DiaChi,DangHoatDong
                FROM dbo.CoQuan WHERE MaCoQuan=@agency AND DangHoatDong=1",
                new{agency=actor.MaCoQuan});
            return row is null?Results.NotFound():Results.Ok(row);
        });

        group.MapPut("/name",async(DoiTenCoQuan body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"settings.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            var name=body.TenCoQuan?.Trim()??"";
            if(name.Length is < 3 or > 200)
                return Results.BadRequest(new{message="Tên cơ quan phải có từ 3 đến 200 ký tự."});

            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var oldName=await cn.QuerySingleOrDefaultAsync<string>(@"
                SELECT TenCoQuan FROM dbo.CoQuan WITH(UPDLOCK,HOLDLOCK)
                WHERE MaCoQuan=@id AND DangHoatDong=1",
                new{id=actor.MaCoQuan},tx);
            if(oldName is null){tx.Rollback();return Results.NotFound();}

            // No agency id in the request: the tenant is always derived from the logged-in user.
            var isAdmin=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.NguoiDungVaiTro ur
                JOIN dbo.VaiTro r ON r.MaVaiTro=ur.MaVaiTro AND r.MaDinhDanh='AGENCY_ADMIN'
                WHERE ur.MaNguoiDung=@user AND ur.MaCoQuan=@id",
                new{user=actor.MaNguoiDung,id=actor.MaCoQuan},tx);
            if(isAdmin==0){tx.Rollback();return Results.Forbid();}
            if(oldName==name){tx.Commit();return Results.Ok(new{tenCoQuan=name,message="Tên cơ quan không thay đổi."});}
            await cn.ExecuteAsync("UPDATE dbo.CoQuan SET TenCoQuan=@name WHERE MaCoQuan=@id",
                new{name,id=actor.MaCoQuan},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
                VALUES(@id,@user,'KY_THUAT','DOI_TEN_CO_QUAN',@description)",
                new{id=actor.MaCoQuan,user=actor.MaNguoiDung,
                    description=$"Đổi tên cơ quan từ '{oldName}' thành '{name}'"},tx);
            tx.Commit();return Results.Ok(new{tenCoQuan=name,message="Đã cập nhật tên cơ quan."});
        });
    }
}
