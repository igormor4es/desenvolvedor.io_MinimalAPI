using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MinimalAPI.Data;
using MinimalAPI.Models;
using MiniValidation;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);

#region Configure Services

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("MinimalAPI")));

builder.Services.AddDbContext<MinimalContextDb>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ExcluirFornecedor", policy => policy.RequireClaim("ExcluirFornecedor"));
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Minimal API",
        Description = "Developed by Eduardo Pires - Owner @ desenvolvedor.io - Implemented by Igor de Moraes",
        Contact = new OpenApiContact { Name = "Eduardo Pires", Email = "contato@eduardopires.net.br" },
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o Token JWT desta maneira: Bearer {seu token}",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

var app = builder.Build();

#endregion

#region Configure Pipeline

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();

MapActions(app);

app.Run();

#endregion

#region Actions

void MapActions(WebApplication app)
{
    #region JWT

    app.MapPost("/Api/Registro", [AllowAnonymous] async (SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, IOptions<AppJwtSettings> appJwtSettings, RegisterUser registerUser) =>
    {
        if (registerUser == null)
            return Results.BadRequest("Usu?rio n?o informado.");

        if (!MiniValidator.TryValidate(registerUser, out var errors))
            return Results.ValidationProblem(errors);

        var user = new IdentityUser
        {
            UserName = registerUser.Email,
            Email = registerUser.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, registerUser.Password);

        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var jwt = new JwtBuilder().WithUserManager(userManager)
                                  .WithJwtSettings(appJwtSettings.Value)
                                  .WithEmail(user.Email)
                                  .WithJwtClaims()
                                  .WithUserClaims()
                                  .WithUserRoles()
                                  .BuildUserResponse();

        return Results.Ok(jwt);

    }).ProducesValidationProblem()
      .Produces(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status400BadRequest)
      .WithName("RegistroUsuario")
      .WithTags("Usuario");

    app.MapPost("/Api/Login", [AllowAnonymous] async (SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, IOptions<AppJwtSettings> appJwtSettings, LoginUser loginUser) =>
    {
        if (loginUser == null)
            return Results.BadRequest("Usu?rio n?o informado.");

        if (!MiniValidator.TryValidate(loginUser, out var errors))
            return Results.ValidationProblem(errors);

        var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, false, true);

        if (result.IsLockedOut)
            return Results.BadRequest("Usu?rio bloqueado.");

        if (!result.Succeeded)
            return Results.BadRequest("Usu?rio ou senha inv?lidos!");

        var jwt = new JwtBuilder()
                      .WithUserManager(userManager)
                      .WithJwtSettings(appJwtSettings.Value)
                      .WithEmail(loginUser.Email)
                      .WithJwtClaims()
                      .WithUserClaims()
                      .WithUserRoles()
                      .BuildUserResponse();

        return Results.Ok(jwt);

    }).ProducesValidationProblem()
      .Produces(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status400BadRequest)
      .WithName("LoginUsuario")
      .WithTags("Usuario");

    #endregion

    #region GET

    app.MapGet("/", [AllowAnonymous] () => "Hello World =]").ExcludeFromDescription();

    app.MapGet("/Api/Fornecedor", [AllowAnonymous] async (MinimalContextDb context) => await context
       .Fornecedores
       .ToListAsync())
       .WithName("GetFornecedor")
       .WithTags("Fornecedor");

    app.MapGet("/Api/Fornecedor/{id}", [Authorize] async (Guid id, MinimalContextDb context) => await context
       .Fornecedores
       .FindAsync(id) is Fornecedor fornecedor
                       ? Results.Ok(fornecedor)
                       : Results.NotFound())
       .Produces<Fornecedor>(StatusCodes.Status200OK)
       .WithName("GetFornecedorId")
       .WithTags("Fornecedor");

    #endregion

    #region POST

    app.MapPost("/Api/Fornecedor/", [Authorize] async (MinimalContextDb context, Fornecedor fornecedor) =>
    {
        if (!MiniValidator.TryValidate(fornecedor, out var errors))
            return Results.ValidationProblem(errors);

        context.Fornecedores.Add(fornecedor);
        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.CreatedAtRoute("GetFornecedorId", new { id = fornecedor.Id }, fornecedor)
            : Results.BadRequest("Houve um problema ao salvar o registro!");

    }).ProducesValidationProblem()
    .Produces<Fornecedor>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PostFornecedor")
    .WithTags("Fornecedor");

    #endregion

    #region PUT

    app.MapPut("/Api/Fornecedor/{id}", [Authorize] async (Guid id, MinimalContextDb context, Fornecedor fornecedor) =>
    {
        var fornecedorBanco = await context.Fornecedores
                                           .AsNoTracking<Fornecedor>()
                                           .FirstOrDefaultAsync(p => p.Id == id);

        if (fornecedorBanco == null)
            return Results.NotFound();

        if (!MiniValidator.TryValidate(fornecedor, out var errors))
            return Results.ValidationProblem(errors);

        context.Fornecedores.Update(fornecedor);

        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("Houve um problema ao editar o registro!");

    }).ProducesValidationProblem()
      .Produces(StatusCodes.Status204NoContent)
      .Produces(StatusCodes.Status400BadRequest)
      .WithName("PutFornecedor")
      .WithTags("Fornecedor");

    #endregion

    #region DELETE

    app.MapDelete("/Api/Fornecedor/{id}", [Authorize] async (Guid id, MinimalContextDb context) =>
    {
        var fornecedor = await context.Fornecedores.FindAsync(id);

        if (fornecedor == null)
            return Results.NotFound();

        context.Fornecedores.Remove(fornecedor);

        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("Houve um problema ao apagar o registro!");

    }).Produces(StatusCodes.Status400BadRequest)
      .Produces(StatusCodes.Status204NoContent)
      .Produces(StatusCodes.Status404NotFound)
      .RequireAuthorization("ExcluirFornecedor")
      .WithName("DeleteFornecedor")
      .WithTags("Fornecedor");

    #endregion
}

#endregion