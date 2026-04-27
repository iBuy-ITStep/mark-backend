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

            // 1.5. CORS Configuration
            var corsOrigins = _confString.GetValue<string>("CorsOrigins") ?? "*";
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecified", policy =>
                {
                    if (corsOrigins == "*")
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    }
                    else
                    {
                        policy.WithOrigins(corsOrigins.Split(";"))
                              .AllowAnyHeader()
                              .AllowAnyMethod()
                              .AllowCredentials();
                    }
                });
            });

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

            // Ensure database is created (only for non-test environments)
            if (!app.Environment.IsEnvironment("Testing"))
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
                context.Database.EnsureCreated();
            }

            // 8. Middleware Pipeline Setup
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Allows the API to serve files directly from /wwwroot/ (e.g. product images)
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors("AllowSpecified");

            // MUST be in this order
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

            // Maps to all [ApiController] classes
            app.MapControllers();

            // Seed roles and superadmin (only for non-test environments)
            if (!app.Environment.IsEnvironment("Testing"))
            {
                await SeedAsync(app);
            }

            await app.RunAsync();
        }

        /// <summary>
        /// Seeds initial roles and a SUPERADMIN account into the application's identity store if they do not already exist.
        /// </summary>
        /// <param name="app">The web application instance whose service provider is used to access identity management services.</param>
        /// <returns>A task that represents the asynchronous seeding operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the super administrator account cannot be created due to identity errors.</exception>
        private static async Task SeedAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // === Seed roles ===
            string[] roles = ["SuperAdmin", "Admin", "Seller", "User"];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // === Seed superadmin ===
            const string superAdminEmail = "msadm@markbackend.internal";
            const string superAdminPassword = "bv10cBvUp9QYa9l_V8";

            var existing = await userManager.FindByEmailAsync(superAdminEmail);
            if (existing == null)
            {
                var superAdmin = new User
                {
                    UserName = superAdminEmail,
                    Email = superAdminEmail,
                    // Skip email confirmation — this is infrastructure
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(superAdmin, superAdminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
                }
                else
                {
                    // Surface seeding errors clearly at startup
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"SuperAdmin seeding failed: {errors}");
                }
            }
        }
    }
}

// Make Program class accessible to test project
public partial class Program { }