using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.ESS;
using HCMS_Api.Components.HCMS.Common;
using HCMS_Api.Components.HCMS.Common.Dapper;
using HCMS_Api.Components.HCMS.Common.DataAccess; 
using HCMS_Api.Components.HCMS.Common.Models;
using HCMS_Api.Components.HCMS.Common.Security;
using HCMS_Api.Components.HCMS.ESS;
using HCMS_Api.Components.HCMS.HR;
using HCMS_Api.Components.HCMS.Payroll;
using HCMS_Api.Models;
using HCMS_Api.Services;
using HCMS_Api.Services.Authorization;
using HCMS_Api.Services.DMS.Divisions;
using HCMS_Api.Services.EmployeeAuthority;
using HCMS_Api.Services.HodService;
using HCMS_Api.Services.LookupService;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Text;
var builder = WebApplication.CreateBuilder(args);

// Configuring SeriLog for logging 

// Correctly configure the log path
var logPath = Path.Combine(AppContext.BaseDirectory, builder.Configuration.GetSection("CorsSettings:LogPath").Value);

// Ensure directory exists
var logDirectory = Path.GetDirectoryName(logPath);
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

if (string.IsNullOrEmpty(logPath))
{
    throw new ArgumentNullException("CorsSettings:LogPath", "Log path cannot be null or empty.");
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day
     )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

string redisConnectionString = builder.Configuration.GetConnectionString("RedisConnectionString");


// Configure Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

// Add Antiforgery
builder.Services.AddAntiforgery();
//builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

/*
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });
*/

builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
        options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
    });


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
 
builder.Services.AddSwaggerGen(c =>
{
    //c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());


    // ✅ FIX: Prevent schema name collisions (Division vs Division)
    c.CustomSchemaIds(type => type.FullName);

    // ✅ API Version shown in Swagger
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DMS API",
        Version = "v1",
        Description = "Document Management System APIs"
    });

    c.AddSecurityDefinition("ApiVersion", new OpenApiSecurityScheme
    {
        Name = "x-api-version",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API Version (e.g. 1.0, 2.0)"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiVersion"
                }
            },
            Array.Empty<string>()
        }
    });

    //// ✅ JWT Bearer Authentication
    //var securityScheme = new OpenApiSecurityScheme
    //{
    //    Name = "Authorization",
    //    Description = "Enter JWT token in this format: Bearer {your token}",
    //    In = ParameterLocation.Header,
    //    Type = SecuritySchemeType.Http,
    //    Scheme = "bearer",
    //    BearerFormat = "JWT",
    //    Reference = new OpenApiReference
    //    {
    //        Type = ReferenceType.SecurityScheme,
    //        Id = JwtBearerDefaults.AuthenticationScheme
    //    }
    //};

    //c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);

    //c.AddSecurityRequirement(new OpenApiSecurityRequirement
    //{
    //    {
    //        securityScheme,
    //        Array.Empty<string>()
    //    }
    //});
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;

    options.ApiVersionReader = new HeaderApiVersionReader("x-api-version");
});


builder.Services.AddHttpContextAccessor();

// Add configuration for connection string
builder.Services.AddSingleton(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<SessionHelper>();
builder.Services.AddScoped<IHodService, HodService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IAuthorization, Authorization>();
builder.Services.AddScoped<IEmployeeAuthorityService, EmployeeAuthorityService>();
builder.Services.AddScoped<Common>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<Utilities>();
builder.Services.AddScoped<DMSUtilities>();
builder.Services.AddScoped<ClientContextService>();
builder.Services.AddScoped<LeaveComponent>();
builder.Services.AddScoped<ValidateAntiForgeryTokenFilter>();
builder.Services.AddScoped<EmployeeDashboardComponent>();
builder.Services.AddScoped<EmployeeInformation>();
builder.Services.AddScoped<DataServices>();
builder.Services.AddScoped<LoginComponent>();
builder.Services.AddScoped<UserPermission>();
builder.Services.AddScoped<EmployeeJobInformationComponent>();
builder.Services.AddScoped<EmployeePersonalInformationComponent>();
builder.Services.AddScoped<MedicalReimbursementComponent>();
builder.Services.AddScoped<EmployeeExitClearanceComponent>();
builder.Services.AddScoped<IDapperDataService, DapperDataService>();
builder.Services.AddScoped<PFSlipViaEmailComponent>();
builder.Services.AddScoped<PerformanceJournalPolicyComponent>();
builder.Services.AddScoped<PerformanceJournalComponent>();
builder.Services.AddScoped<AttendanceSheetComponent>();

#region DMS Services

builder.Services.AddScoped<DMSCommon>();
builder.Services.AddScoped<DMSDataServices>();
builder.Services.AddScoped<IDMSDapperDataService, DMSDapperDataService>();
builder.Services.AddScoped<DivisionComponent>();
builder.Services.AddScoped<DepartmentComponent>();
builder.Services.AddScoped<SubDepartmentComponent>();
builder.Services.AddScoped<DocumentTypeComponent>();
builder.Services.AddScoped<BusinessDomainComponent>();
builder.Services.AddScoped<CabinetStructureTabsConfigComponent>();
builder.Services.AddScoped<AttributeMandatoryScopeComponent>();
builder.Services.AddScoped<AuditLogComponent>();
builder.Services.AddScoped<DistributionListComponent>();
builder.Services.AddScoped<DocumentApprovalComponent>();
builder.Services.AddScoped<DocumentAttributeComponent>();
builder.Services.AddScoped<DocumentComponent>();
builder.Services.AddScoped<DocumentRequestComponent>();
builder.Services.AddScoped<DocumentTrainingComponent>();
builder.Services.AddScoped<DocumentVersionComponent>();
builder.Services.AddScoped<ESignatureComponent>();
builder.Services.AddScoped<NotificationComponent>();
builder.Services.AddScoped<RequestApprovalComponent>();
builder.Services.AddScoped<ResponsibilityTransferComponent>();
builder.Services.AddScoped<RoleComponent>();
builder.Services.AddScoped<TemplateComponent>();
builder.Services.AddScoped<TrainingPolicyComponent>();
builder.Services.AddScoped<TransferScopePolicyComponent>();
builder.Services.AddScoped<TransferWorkflowPolicyComponent>();
builder.Services.AddScoped<UserComponent>();
builder.Services.AddScoped<UserRoleComponent>();
builder.Services.AddScoped<WorkflowPolicyComponent>();
builder.Services.AddScoped<WorkflowStepComponent>(); 
builder.Services.AddScoped<DesignationComponent>(); 
builder.Services.AddScoped<IDivisionService, DivisionService>();

#endregion DMS Service


var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins(allowedOrigins) // Add the allowed origin(s)
              .AllowAnyMethod() // Allow all HTTP methods (GET, POST, etc.)
              .AllowAnyHeader().AllowCredentials(); // ? this line is required; // Allow all headers

    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });
// In-memory IDistributedCache for Session (no extra NuGet needed)
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".HCMS.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // If your Angular app is on a different origin and you need cross-site cookies, uncomment:
    // options.Cookie.SameSite = SameSiteMode.None;
    // options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddAuthorization();
var app = builder.Build();
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads"); // example outside wwwroot
Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles();
app.UseRouting();
HCMS_Api.Common.ServiceLocator.Initialize(app.Services);

// Use CORS middleware
app.UseCors("AllowSpecificOrigin");

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

//Collapse all API groups
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DMS API v1");

        // ✅ Collapse all endpoints by default
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);

        // Optional but recommended
        c.DefaultModelsExpandDepth(-1); // Hides schema section

        // Optional but recommended
        c.DisplayRequestDuration();
    });
}


app.UseHttpsRedirection();

app.UseAuthentication();
app.UseSession();
app.UseAuthorization();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});

app.MapControllers();

app.Run();
