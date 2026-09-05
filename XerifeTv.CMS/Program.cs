using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using System.Globalization;
using XerifeTv.CMS.Shared.Database.MongoDB;
using XerifeTv.CMS.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Evita o uso de FileSystemWatcher (inotify) para o wwwroot: em ambientes como o Render,
// o limite de instâncias inotify do container estoura e derruba o app com 500 ao renderizar
// tags com asp-append-version. O wwwroot não muda em runtime, então polling é seguro aqui.
builder.Environment.WebRootFileProvider = new PhysicalFileProvider(builder.Environment.WebRootPath)
{
	UsePollingFileWatcher = true,
	UseActivePolling = false
};

var defaultCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

builder.Services.AddControllersWithViews();

var authApiAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAnyOrigin", builder =>
	{
		builder
			   .AllowAnyOrigin()
			   .AllowAnyMethod()
			   .AllowAnyHeader();
	});

	options.AddPolicy("AuthApi", policy =>
	{
		policy
			   .WithOrigins(authApiAllowedOrigins)
			   .AllowAnyMethod()
			   .AllowAnyHeader()
			   .AllowCredentials();
	});
});

builder.Services.Configure<DBSettings>(
	builder.Configuration.GetSection("MongoDBConfig"));

builder.Services.AddConfiguration(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.Urls.Add("http://*:80");
	app.UseHsts();
}

app.UseStatusCodePages(context =>
{
	var response = context.HttpContext.Response;
    var path = context.HttpContext.Request.Path;
    if (path.StartsWithSegments("/Api/Profile") || path.StartsWithSegments("/Api/Reviews"))
        return Task.CompletedTask;

	if (response.StatusCode == 401)
	{
		var originalUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
		response.Redirect($"/Users/RefreshSession?successRedirectUrl={originalUrl}");
	}

	if (response.StatusCode == 403)
		response.Redirect("/Users/UserUnauthorized");

	if (response.StatusCode == 404)
		response.Redirect("/");

	return Task.CompletedTask;
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowAnyOrigin");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger(options =>
{
	options.RouteTemplate = "openapi/{documentName}.json";
});

app.MapScalarApiReference("/Api", options =>
{
	options
		.WithTitle("Content API")
		.WithTheme(ScalarTheme.Moon)
		.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.Axios);
});

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();