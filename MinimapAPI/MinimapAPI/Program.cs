using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MinimapAPI.Data;
using MinimapAPI.Models;
using MiniValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<MinimalContextDb>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "Hello World =]").ExcludeFromDescription();

app.MapGet("/Api/Fornecedor", async (MinimalContextDb context) => await context
   .Fornecedores
   .ToListAsync())
   .WithName("GetFornecedor")
   .WithTags("Fornecedor");

app.MapGet("/Api/Fornecedor/{id}", async (Guid id, MinimalContextDb context) => await context
   .Fornecedores
   .FindAsync(id) is Fornecedor fornecedor 
                   ? Results.Ok(fornecedor) 
                   : Results.NotFound())
   .Produces<Fornecedor>(StatusCodes.Status200OK)
   .WithName("GetFornecedorId")
   .WithTags("Fornecedor");

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

app.MapPut("/Api/Fornecedor/{id}", [Authorize] async (Guid id, MinimalContextDb context,Fornecedor fornecedor) =>
{
    var fornecedorBanco = await context.Fornecedores.FindAsync(id);
    if (fornecedorBanco == null) return Results.NotFound();

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

app.MapDelete("/Api/Fornecedor/{id}", [Authorize] async (Guid id, MinimalContextDb context) =>
{
    var fornecedor = await context.Fornecedores.FindAsync(id);
    if (fornecedor == null) return Results.NotFound();

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

app.Run();