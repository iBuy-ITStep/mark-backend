using System.Reflection;
using Swashbuckle.AspNetCore;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MarkBackend.Data;
using MarkBackend.Helpers;
using MarkBackend.Interfaces;
using MarkBackend.Models;
using MarkBackend.Repositories;
using System.Text;

namespace MarkBackend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Add API Controllers (No views)
            builder.Services.AddControllers();

            // 2. Load Configuration securely
            var _confString = builder.Configuration;

            // 3. Swagger / OpenAPI Setup with JWT Authorization Support
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "MiniMart API", Version = "v1" });

                // Adds the "Authorize" button in Swagger UI
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer'[space] and then your valid token.\r\n\r\nExample: \"Bearer eyJhbGciOi...\""
                });

                /*
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
                */
                options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer"),
                        new List<string>()
                    }
                });

                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });

            // 4. Database Context setup
            builder.Services.AddDbContext<ApplicationContext>(options =>
                options.UseSqlServer(_confString.GetConnectionString("DefaultConnection")));

            // 5. Identity Setup
            builder.Services.Configure<DataProtectionTokenProviderOptions>(opts => opts.TokenLifespan = TimeSpan.FromMinutes(15));
            builder.Services.AddIdentity<User, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationContext>()
            .AddDefaultTokenProviders();

            // 6. JWT Authentication Setup (Replacing standard Cookie auth)
            var jwtSecret = _confString.GetValue<string>("JwtSecret") ?? "SuperSecretDefaultKeyThatNeedsToBeLongEnough123!";
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            builder.Services.AddAuthentication(options =>
            {
                // Tell ASP.NET to use JWT Bearer tokens by default, not cookies
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false, // Set to true in prod if strictly validation of domains is needed
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // 7. Dependency Injection Registration
            builder.Services.AddScoped<IProductRepository, ProductRepository>();
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<IBrandRepository, BrandRepository>();
            builder.Services.AddScoped<ICartRepository, CartRepository>();
            builder.Services.AddScoped<EmailHelper>();
            builder.Services.AddScoped<IImageRepository, ImageRepository>();

            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(x =>
            {
                x.MultipartBodyLengthLimit = 8 * 1024 * 1024; // 8 MB ceiling for form uploads
            });

            var app = builder.Build();

            // 8. Middleware Pipeline Setup
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Allows the API to serve files directly from /wwwroot/ (e.g. your product images)
            app.UseStaticFiles();

            app.UseRouting();

            // MUST be in this order
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

            // Maps to all [ApiController] classes
            app.MapControllers();

            await app.RunAsync();
        }
    }
}