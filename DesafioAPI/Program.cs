using DesafioAPI.Dominio;
using DesafioAPI.DataAccess.Mapeamentos;
using DesafioAPI.DataAccess.Repositorios;
using Microsoft.EntityFrameworkCore;
using DesafioAPI.DataAccess;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<Contexto>(options => 
options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<JogoRepositorio>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/jogos", async (JogoRepositorio repositorio) =>
{
    var jogos = await repositorio.ObterTodosAsync();
    return Results.Ok(jogos);
});

app.MapGet("/jogos/{id}", async (int id, JogoRepositorio repositorio) =>
{
    var jogo = await repositorio.ObterPorIdAsync(id);
    return jogo != null ? Results.Ok(jogo) : Results.NotFound();
});

app.MapPost("/jogos", async (Jogo jogo, JogoRepositorio repositorio) =>
{
    await repositorio.AdicionarAsync(jogo);
    return Results.Created($"/jogos/{jogo.Id}", jogo);
});

app.MapPut("/jogos/{id}", async (int id, Jogo jogo, JogoRepositorio repositorio) =>
{
    var jogoExistente = await repositorio.ObterPorIdAsync(id);
    if (jogoExistente == null)
    {
        return Results.NotFound();
    }

    jogoExistente.Nome = jogo.Nome;
    jogoExistente.Alugado = jogo.Alugado;

    await repositorio.AtualizarAsync(jogoExistente);
    return Results.NoContent();
});

app.MapDelete("/jogos/{id}", async (int id, JogoRepositorio repositorio) =>
{
    var jogo = await repositorio.ObterPorIdAsync(id);
    if (jogo == null)
    {
        return Results.NotFound();
    }

    await repositorio.RemoverAsync(jogo);
    return Results.NoContent();
});

app.MapPost("/jogos/{id}/alugar", async (int id, JogoRepositorio repositorio) =>
{
    var jogo = await repositorio.ObterPorIdAsync(id);
    if (jogo == null)
    {
        return Results.NotFound();
    }

    if (jogo.Alugado)
    {
        return Results.BadRequest("Jogo já está alugado.");
    }

    jogo.Alugado = true;
    await repositorio.AtualizarAsync(jogo);
    return Results.NoContent();
});

app.MapPost("/jogos/{id}/devolver", async (int id, JogoRepositorio repositorio) =>
{
    var jogo = await repositorio.ObterPorIdAsync(id);
    if (jogo == null)
    {
        return Results.NotFound();
    }

    if (!jogo.Alugado)
    {
        return Results.BadRequest("Jogo não está alugado.");
    }

    jogo.Alugado = false;
    await repositorio.AtualizarAsync(jogo);
    return Results.NoContent();
});

app.Run();


