# 🎮 Gerenciamento de Jogos
### DesafioAPI + DesafioConsole — .NET Core · Entity Framework Core · SQLite

---

## 1. Visão Geral do Projeto

Este projeto é composto por dois programas que trabalham em conjunto para gerenciar uma coleção de jogos, permitindo cadastrar, listar, alugar e devolver jogos.

| DesafioAPI — O Servidor | DesafioConsole — O Cliente |
|---|---|
| Fica rodando em segundo plano | Usado diretamente pelo usuário no terminal |
| Guarda os jogos em banco de dados SQLite | Não acessa o banco diretamente |
| Recebe e responde requisições HTTP | Conversa com a API via HTTP |
| Como um garçom: recebe pedido e entrega | Como o cliente: faz pedidos ao garçom |

> 💡 Para tudo funcionar, os dois projetos precisam estar rodando ao mesmo tempo!

---

## 2. Como os Dois Projetos se Comunicam

A comunicação entre o Console e a API acontece via **HTTP**, o mesmo protocolo que o navegador usa para acessar sites. O Console envia requisições e a API responde com dados em formato **JSON**.

| Método HTTP | O que significa | Exemplo no projeto |
|---|---|---|
| GET | Buscar / ler informações | Listar todos os jogos |
| POST | Criar algo novo ou executar ação | Adicionar jogo, Alugar, Devolver |
| PUT | Atualizar algo existente | Editar o nome de um jogo |
| DELETE | Remover algo | Excluir um jogo do sistema |

---

## 3. Projeto DesafioAPI — O Servidor

A API foi construída em camadas, onde cada camada tem uma responsabilidade bem definida.

### 3.1 Ordem de Criação dos Arquivos

> Siga sempre esta ordem — cada etapa depende da anterior:

| # | Arquivo | Por que nessa ordem? |
|---|---|---|
| 1 | `Dominio/Jogo.cs` | É a base de tudo. Sem definir o que é um Jogo, nada mais funciona. |
| 2 | `DataAccess/Mapeamentos/JogoConfiguration.cs` | Mapeia o Jogo para o banco. Depende do Jogo.cs existir. |
| 3 | `DataAccess/Contexto.cs` | Usa o Jogo e o Configuration. Precisa dos dois prontos. |
| 4 | `DataAccess/Repositorios/JogoRepositorio.cs` | Usa o Contexto. Precisa dele pronto. |
| 5 | `appsettings.json` | Define onde o banco será criado. Precisa estar pronto antes de rodar. |
| 6 | `Program.cs` da API | Liga tudo: registra serviços e define os endpoints. |
| 7 | Migrations (terminal) | Cria a tabela no banco. Só funciona com tudo acima pronto. |
| 8 | `Services/JogoService.cs` (Console) | Consome a API. A API precisa existir primeiro. |
| 9 | `Program.cs` do Console | O menu final. Depende do JogoService estar pronto. |

---

### 3.2 `Dominio/Jogo.cs` — A Entidade Principal

É o coração do projeto. Define o que é um "Jogo" para o sistema.

> 💡 Pense na classe Jogo como uma ficha cadastral. Cada jogo no banco é uma ficha preenchida com esses três campos.

| Propriedade | Tipo | Para que serve |
|---|---|---|
| `Id` | `int` | Identificador único. Gerado automaticamente pelo banco. |
| `Nome` | `string?` | Nome do jogo, ex: "FIFA 2024". O `?` indica que pode ser nulo. |
| `Alugado` | `bool` | Indica se o jogo está alugado (`true`) ou disponível (`false`). |

```csharp
namespace DesafioAPI.Dominio
{
    public class Jogo
    {
        public int Id { get; set; }        // Chave primária — gerada pelo banco
        public string? Nome { get; set; }  // Nome do jogo — pode ser nulo
        public bool Alugado { get; set; }  // Status de aluguel — padrão: false
    }
}
```

> 💡 `get; set;` significa que a propriedade pode ser lida e alterada. É o padrão para propriedades de entidades no C#.

---

### 3.3 `DataAccess/Mapeamentos/JogoConfiguration.cs` — Mapeamento do Banco

Define como a entidade `Jogo` deve ser transformada em uma tabela no banco de dados.

> 💡 É como a planta de uma casa: antes de construir, você define exatamente como cada cômodo (coluna) será.

**Imports necessários:**
```csharp
using DesafioAPI.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
```

**O que cada linha de configuração faz:**
```csharp
builder.ToTable("Jogos");
// Define que a tabela no banco se chamará 'Jogos'

builder.HasKey(j => j.Id);
// Define Id como chave primária — o banco irá gerar o valor automaticamente

builder.Property(j => j.Nome).IsRequired().HasMaxLength(100);
// Nome é obrigatório e tem no máximo 100 caracteres

builder.Property(j => j.Alugado).IsRequired();
// Alugado é obrigatório — o banco não aceita valor nulo
```

> 💡 `IEntityTypeConfiguration<Jogo>` é uma interface que obriga você a implementar o método `Configure`. É um contrato que o EF Core usa para aplicar as configurações.

---

### 3.4 `DataAccess/Contexto.cs` — A Ponte com o Banco

Esta classe é a conexão entre o código C# e o banco de dados SQLite.

> 💡 Pense no Contexto como o gerente do banco de dados: ele sabe onde tudo está guardado e como acessar cada informação.

```csharp
public class AppDbContext : DbContext
// Herda de DbContext — classe base do Entity Framework

public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
// Construtor que recebe as configurações (string de conexão, tipo de banco)
// O 'base(options)' repassa isso para a classe pai (DbContext)

public DbSet<Jogo> Jogos { get; set; }
// Representa a tabela 'Jogos' dentro do código
// Quando você escreve _context.Jogos, está acessando essa tabela

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    // Aplica AUTOMATICAMENTE todas as classes de configuração da pasta Mapeamentos
    // O JogoConfiguration.cs é aplicado aqui sem precisar chamar manualmente
}
```

---

### 3.5 `DataAccess/Repositorios/JogoRepositorio.cs` — Operações no Banco

É quem de fato executa as operações no banco. O `Program.cs` nunca fala diretamente com o banco — ele sempre passa pelo repositório. Esse padrão se chama **Repository Pattern**.

> 💡 O sufixo `Async` significa operação assíncrona: o programa não para e espera o banco responder — ele continua funcionando enquanto aguarda, o que melhora a performance.

| Método | O que faz | Código resumido |
|---|---|---|
| `ObterTodosAsync()` | Busca todos os jogos cadastrados | `_context.Jogos.ToListAsync()` |
| `ObterPorIdAsync(id)` | Busca um jogo específico pelo Id | `_context.Jogos.FindAsync(id)` |
| `AdicionarAsync(jogo)` | Insere um novo jogo no banco | `AddAsync(jogo)` + `SaveChangesAsync()` |
| `AtualizarAsync(jogo)` | Salva as alterações de um jogo existente | `Update(jogo)` + `SaveChangesAsync()` |
| `RemoverAsync(jogo)` | Remove um jogo do banco | `Remove(jogo)` + `SaveChangesAsync()` |

**Imports necessários:**
```csharp
using DesafioAPI.DataAccess.Contexto;
using DesafioAPI.Dominio;
using Microsoft.EntityFrameworkCore;
```

---

### 3.6 `appsettings.json` — Configurações da API

Arquivo de configuração da API. Guarda informações que podem variar dependendo do ambiente, como a string de conexão com o banco.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:\\Alunos\\luiz\\DesafioAPI\\jogos.db"
  }
}
```

> 💡 Use **caminho absoluto** para evitar o erro clássico: o banco sendo criado em `bin\Debug\net10.0` enquanto a API procura em outro lugar.

---

### 3.7 `Program.cs` da API — Os Endpoints

É o arquivo principal da API. Faz duas coisas: configura os serviços e define os endpoints.

**Bloco 1 — Configuração dos Serviços:**
```csharp
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
// Ativa o Swagger — documentação e tela de testes da API

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
// Registra o banco usando a string do appsettings.json

builder.Services.AddScoped<JogoRepositorio>();
// Registra o repositório para injeção de dependência automática
```

> 💡 `AddScoped` significa que o .NET cria uma instância nova do `JogoRepositorio` para cada requisição HTTP. É destruída ao final da requisição.

**Bloco 2 — Os Endpoints (Rotas):**

| Método | Rota | O que faz | Retorna |
|---|---|---|---|
| GET | `/jogos` | Lista todos os jogos | 200 OK + lista JSON |
| GET | `/jogos/{id}` | Busca um jogo pelo Id | 200 OK ou 404 Not Found |
| POST | `/jogos` | Adiciona um novo jogo | 201 Created |
| PUT | `/jogos/{id}` | Atualiza os dados de um jogo | 200 OK ou 404 Not Found |
| DELETE | `/jogos/{id}` | Remove um jogo | 204 No Content |
| POST | `/jogos/{id}/alugar` | Marca o jogo como alugado | 200 OK ou 400 Bad Request |
| POST | `/jogos/{id}/devolver` | Marca o jogo como disponível | 200 OK ou 400 Bad Request |

> 💡 Os endpoints `/alugar` e `/devolver` têm regra de negócio: se tentar alugar um jogo já alugado, retorna erro **400** com a mensagem _"O jogo já está alugado"_.

**Injeção de Dependência na prática:**
```csharp
app.MapGet("/jogos", async (JogoRepositorio repositorio) =>
{
    // O .NET injeta o 'repositorio' automaticamente
    // Você não precisa escrever: var repositorio = new JogoRepositorio(...)
    var jogos = await repositorio.ObterTodosAsync();
    return Results.Ok(jogos);
});
```

---

## 4. Projeto DesafioConsole — O Cliente

O Console não tem banco de dados nem Entity Framework. Sua única responsabilidade é comunicar-se com a API via HTTP e exibir os resultados no terminal.

### 4.1 `Services/JogoService.cs` — A Ponte com a API

```csharp
private readonly HttpClient _httpClient;

public JogoService()
{
    var handler = new HttpClientHandler();
    // Aceita certificados locais de desenvolvimento (resolve erro SSL)
    handler.ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

    _httpClient = new HttpClient(handler);
    _httpClient.BaseAddress = new Uri("http://localhost:5017");
    // URL base da API — todos os métodos complementam com a rota específica
}
```

| Método | Tipo HTTP | Rota chamada | O que faz |
|---|---|---|---|
| `ListarJogosAsync()` | GET | `/jogos` | Busca e exibe todos os jogos |
| `AdicionarJogoAsync(nome)` | POST | `/jogos` | Envia um novo jogo para a API cadastrar |
| `AlugarJogoAsync(id)` | POST | `/jogos/{id}/alugar` | Avisa a API para marcar o jogo como alugado |
| `DevolverJogoAsync(id)` | POST | `/jogos/{id}/devolver` | Avisa a API para marcar o jogo como disponível |

> 💡 O `HttpClient` funciona como um navegador dentro do código: abre uma URL, recebe a resposta JSON e devolve os dados para o programa usar.

**Por que existe a classe `Jogo` dentro do Console?**

O Console não tem acesso direto ao projeto da API, então ele precisa de sua própria definição da classe `Jogo` para conseguir desserializar (converter) o JSON recebido. É uma cópia simplificada com as mesmas propriedades: `Id`, `Nome` e `Alugado`.

### 4.2 `Program.cs` do Console — O Menu

```csharp
var jogoService = new JogoService();

while (true)  // Loop infinito — o programa só termina quando o usuário escolher Sair
{
    Console.WriteLine("\n=== MENU ===");
    Console.WriteLine("1. Listar Jogos");
    Console.WriteLine("2. Adicionar Jogo");
    Console.WriteLine("3. Alugar Jogo");
    Console.WriteLine("4. Devolver Jogo");
    Console.WriteLine("5. Sair");

    var opcao = Console.ReadLine();  // Lê o que o usuário digitou

    switch (opcao)
    {
        case "1": await jogoService.ListarJogosAsync(); break;
        case "2":
            Console.Write("Nome do jogo: ");
            var nome = Console.ReadLine();
            await jogoService.AdicionarJogoAsync(nome); break;
        case "3":
            Console.Write("ID do jogo: ");
            var idAlugar = int.Parse(Console.ReadLine());
            await jogoService.AlugarJogoAsync(idAlugar); break;
        case "4":
            Console.Write("ID do jogo: ");
            var idDevolver = int.Parse(Console.ReadLine());
            await jogoService.DevolverJogoAsync(idDevolver); break;
        case "5": return;  // Encerra o programa
    }
}
```

> 💡 O `await` antes de cada chamada é necessário porque os métodos são assíncronos. Sem ele, o programa não esperaria a resposta da API antes de continuar.

---

## 5. Fluxo Completo de uma Operação

Exemplo: o usuário aluga um jogo.

| Passo | Onde acontece | O que ocorre |
|---|---|---|
| 1 | Console — `Program.cs` | Usuário escolhe "3 - Alugar" e digita o Id do jogo |
| 2 | Console — `JogoService.cs` | Envia: `POST http://localhost:5017/jogos/3/alugar` |
| 3 | API — `Program.cs` | O endpoint `/jogos/{id}/alugar` recebe a requisição |
| 4 | API — `JogoRepositorio.cs` | Repositório busca o jogo no banco pelo Id |
| 5 | API — `Program.cs` | Verifica se já está alugado → se sim, retorna erro 400 |
| 6 | API — `JogoRepositorio.cs` | Define `Alugado = true` e salva no banco |
| 7 | API — `Program.cs` | Retorna resposta HTTP 200 OK para o Console |
| 8 | Console — `JogoService.cs` | Exibe "Jogo alugado com sucesso!" |

---

## 6. Erros Encontrados e Como Foram Resolvidos

Documentar erros é uma prática muito valorizada no mercado — demonstra raciocínio crítico e capacidade de depuração.

| # | Erro | Causa | Solução |
|---|---|---|---|
| 1 | Namespaces incorretos no `JogoRepositorio.cs` | Usava `DataAccess.Dominio` e `DataAccess.Context` que não existiam. | Corrigir para: `DesafioAPI.Dominio`, `DataAccess.Contexto`, `Microsoft.EntityFrameworkCore` |
| 2 | Falta de `using` no `Contexto.cs` | `DbContext`, `DbSet` e `ModelBuilder` usados sem importar o namespace do EF. | Adicionar `using Microsoft.EntityFrameworkCore;` e `using DesafioAPI.Dominio;` |
| 3 | Pacotes do EF Core não instalados | Pacotes do Entity Framework e SQLite ausentes no projeto. | `dotnet add package Microsoft.EntityFrameworkCore` + Sqlite + Design |
| 4 | Swagger não instalado | Pacote `Swashbuckle.AspNetCore` ausente. | `dotnet add package Swashbuckle.AspNetCore` |
| 5 | `ConnectionStrings` ausente no `appsettings.json` | API não sabia onde criar o banco de dados. | Adicionar seção `ConnectionStrings` com `Data Source` no `appsettings.json` |
| 6 | Migration pendente | `dotnet ef database update` não havia sido executado com sucesso. | Executar: `dotnet ef migrations add CriacaoInicial` e `dotnet ef database update` |
| 7 | Banco criado em pasta errada | Caminho relativo criava o `.db` em `bin/Debug/net10.0`. | Usar caminho absoluto no `appsettings.json` |
| 8 | Erro SSL na aplicação Console | `HttpClient` não confiava no certificado local de desenvolvimento. | Usar HTTP na URL base **ou** configurar `DangerousAcceptAnyServerCertificateValidator` |

---

## 7. Glossário de Conceitos

| Conceito | O que é |
|---|---|
| **API (Web API)** | Programa que fica rodando e responde a requisições HTTP de outros programas. |
| **Entity Framework Core** | Biblioteca que permite manipular banco de dados usando C# ao invés de SQL. |
| **DbContext** | Classe central do EF Core — representa a sessão com o banco de dados. |
| **DbSet\<T\>** | Representa uma tabela do banco dentro do código C#. |
| **Migration** | Mecanismo do EF Core para criar e atualizar tabelas no banco via código. |
| **Repository Pattern** | Padrão que isola o acesso ao banco em uma classe dedicada (repositório). |
| **Injeção de Dependência** | Mecanismo onde o .NET cria e entrega automaticamente os objetos que você precisa. |
| **HttpClient** | Classe do .NET para fazer requisições HTTP (GET, POST, PUT, DELETE). |
| **JSON** | Formato de dados usado para trocar informações entre a API e o Console. |
| **Async/Await** | Modelo de programação assíncrona que evita travar o programa enquanto espera respostas. |
| **Swagger** | Ferramenta que gera documentação e interface visual para testar os endpoints da API. |
| **SQLite** | Banco de dados leve que funciona como um único arquivo `.db` no computador. |
| **Endpoint** | URL específica da API que aceita um determinado tipo de requisição. |
| **CRUD** | Acrônimo de Create, Read, Update, Delete — as quatro operações básicas de dados. |

---

*Projeto desenvolvido como parte do Desafio de Gerenciamento de Jogos — .NET Core + EF Core + SQLite*
