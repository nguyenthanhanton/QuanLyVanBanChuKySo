using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
// Max 100 MiB of document attachments plus multipart overhead per request.
builder.WebHost.ConfigureKestrel(k=>k.Limits.MaxRequestBodySize=110L*1024*1024);
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Phai cau hinh Jwt__Key >= 32 byte qua bien moi truong.");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("QuanLyVanBan")))
    throw new InvalidOperationException("Phai cau hinh ConnectionStrings__QuanLyVanBan.");

builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<PhanQuyen>();
builder.Services.AddSingleton<XacThuc>();
builder.Services.AddSingleton<DichVuKyPdf>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(15)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("WebLocal", policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
          .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors("WebLocal");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok", now = DateTimeOffset.UtcNow }));
app.MapXacThuc();
app.MapVanBan();
app.MapTepVanBan();
app.MapQuyTrinh();
app.MapKySo();
app.MapPhatHanh();
app.MapQuanTriNguoiDung();
app.MapQuanTriPhongBan();
app.MapQuanTriPhanQuyen();
app.Run();
