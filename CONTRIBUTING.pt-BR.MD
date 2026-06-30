# Contribuindo com o WingetEasy

**🌍 Leia em outros idiomas:** [English](CONTRIBUTING.md)

Obrigado por querer contribuir! Este documento explica tudo que você precisa saber para começar a trabalhar no projeto sem precisar analisar o código inteiro.

---

## Índice

- [Pré-requisitos](#pré-requisitos)
- [Configurando o ambiente em 5 minutos](#configurando-o-ambiente-em-5-minutos)
- [Entendendo a estrutura do projeto](#entendendo-a-estrutura-do-projeto)
- [Como encontrar o que trabalhar](#como-encontrar-o-que-trabalhar)
- [Convenções de código](#convenções-de-código)
- [Abrindo um Pull Request](#abrindo-um-pull-request)
- [O que bloqueia um PR](#o-que-bloqueia-um-pr)
- [Dúvidas](#dúvidas)

---

## Pré-requisitos

| Ferramenta | Versão mínima | Onde baixar |
|---|---|---|
| Visual Studio 2022 Community | 17.8+ | [visualstudio.microsoft.com](https://visualstudio.microsoft.com/vs/community/) |
| Windows | 10 21H1 (build 19043) | — |
| Git | Qualquer | [git-scm.com](https://git-scm.com) |

**Workloads obrigatórios no Visual Studio Installer:**
- ✅ Desenvolvimento de área de trabalho com .NET
- ✅ Desenvolvimento de SDK de Aplicativos do Windows

Mais nada. Não instale outros workloads — só aumenta o tamanho da instalação.

---

## Configurando o ambiente em 5 minutos

```bash
# 1. Fork o repositório no GitHub, depois clone o seu fork
git clone https://github.com/SEU-USUARIO/WingetEasy.git
cd WingetEasy

# 2. Abra a solution no Visual Studio
start WingetEasy.slnx
```

No Visual Studio:
1. Aguarde o restore automático dos pacotes NuGet (barra de progresso no rodapé)
2. Selecione o projeto `WingetEasy.App` como projeto de inicialização (clique direito → Definir como Projeto de Inicialização)
3. Pressione **F5**

Se o app abrir e o ícone aparecer na bandeja do sistema, o ambiente está correto. ✅

**Se falhar:** verifique se os dois workloads do Visual Studio estão instalados e se o Windows está na versão 10 21H1 ou superior.

---

## Entendendo a estrutura do projeto

```
WingetEasy/
│
├── src/
│   ├── WingetEasy.App/          → Tudo que o usuário VÊ
│   │   ├── Views/               → Páginas e janelas (.xaml)
│   │   ├── ViewModels/          → Lógica de apresentação (sem UI, sem banco)
│   │   └── Controls/            → Controles XAML customizados reutilizáveis
│   │
│   ├── WingetEasy.Core/         → Lógica de NEGÓCIO (sem UI, sem banco)
│   │   ├── Interfaces/          → Contratos dos serviços (I-prefixo)
│   │   ├── Models/              → Records imutáveis de domínio
│   │   └── Services/            → Implementações dos serviços
│   │
│   └── WingetEasy.Data/         → Banco de DADOS
│       ├── Entities/            → Entidades EF Core (tabelas)
│       ├── Repositories/        → Acesso ao banco (nunca use DbContext direto)
│       └── Migrations/          → Geradas automaticamente pelo EF Core
│
└── tests/
    └── WingetEasy.Core.Tests/   → Testes unitários (espelham a estrutura do Core)
```

### Regra de ouro: onde cada coisa fica

| Quero mexer em... | Abro o projeto... | Pasta... |
|---|---|---|
| Visual, layout, animações | `WingetEasy.App` | `Views/` |
| Lógica de uma tela | `WingetEasy.App` | `ViewModels/` |
| Comunicação com o Winget | `WingetEasy.Core` | `Services/WingetService.cs` |
| Agendamento de verificações | `WingetEasy.Core` | `Services/SchedulerService.cs` |
| Banco de dados, queries | `WingetEasy.Data` | `Repositories/` |
| Contratos / interfaces | `WingetEasy.Core` | `Interfaces/` |
| Testes unitários | `WingetEasy.Core.Tests` | espelha `Core/` |

### Fluxo de dados (para entender quem chama quem)

```
View (XAML)
  ↕ Data Binding
ViewModel
  ↕ Interface injetada (DI)
Service (Core)
  ↕ Interface injetada (DI)
Repository (Data) → SQLite
  +
WingetService → winget.exe (subprocess)
```

**Nunca** pule um nível: View não acessa Service, ViewModel não acessa banco diretamente.

---

## Como encontrar o que trabalhar

### Issues para quem está começando

Filtre as issues com a label **`good first issue`** — são tarefas bem delimitadas, com código de referência, que não exigem conhecer o projeto inteiro.

### Lendo uma issue

Cada issue do projeto segue este formato:

1. **Descrição** — o que precisa ser feito e por quê
2. **Requer antes** — issues que precisam estar concluídas primeiro (nunca ignore isso)
3. **Tarefas** — lista exata do que implementar
4. **Referência de código** — trecho mostrando o padrão esperado
5. **Critérios de aceite** — o que precisa funcionar para o PR ser aprovado

Se a issue diz "Requer antes: #4, #5", verifique se essas issues já estão fechadas antes de começar.

### Antes de começar

Comente na issue que você vai trabalhar nela. Isso evita que duas pessoas façam a mesma coisa.

---

## Convenções de código

O `.editorconfig` na raiz aplica a maioria das regras automaticamente no Visual Studio. O que não é automático:

### Nomenclatura

| Tipo | Convenção | Exemplo |
|---|---|---|
| Interface | Prefixo `I` | `IWingetService` |
| ViewModel | Sufixo `ViewModel` | `UpdatesViewModel` |
| Página / Janela | Sufixo `Page` ou `Window` | `SettingsPage`, `UpdateFlyout` |
| Repositório | Sufixo `Repository` | `UpdateHistoryRepository` |
| Teste | Sufixo `Tests`, espelha a classe | `WingetServiceTests` |
| Campo privado | Prefixo `_` camelCase | `_wingetService` |
| Constante | PascalCase | `MaxRetryCount` |

### XML Documentation (obrigatório em membros públicos)

Todo método, propriedade e interface pública precisa de XML doc. O Visual Studio gera a estrutura automaticamente ao digitar `///` na linha anterior.

```csharp
/// <summary>
/// Verifica se há pacotes com atualização disponível via Winget CLI.
/// Resultado é cacheado por 30 minutos — chamadas repetidas não executam subprocess.
/// </summary>
/// <param name="ct">Token de cancelamento. Timeout interno de 5 minutos já aplicado.</param>
/// <returns>Lista vazia se não há atualizações ou se o winget não está instalado.</returns>
Task<IEnumerable<WingetPackage>> CheckUpdatesAsync(CancellationToken ct = default);
```

### Async/Await

```csharp
// ✅ Correto
public async Task<Result> FazerAlgoAsync(CancellationToken ct = default) { }

// ❌ Nunca — async void engole exceptions silenciosamente
public async void FazerAlgo() { }

// ❌ Nunca — bloqueia a thread e pode causar deadlock
var result = service.FazerAlgoAsync().Result;
```

### Strings na UI

Nenhuma string visível ao usuário pode ficar hardcoded no código C#. Use os arquivos de resource:

```csharp
// ❌ Errado
StatusMessage = "Verificando atualizações...";

// ✅ Correto
StatusMessage = Resources.CheckingUpdates;
```

### Injeção de dependência

```csharp
// ❌ Nunca instancie serviços diretamente
var service = new WingetService();

// ✅ Sempre via construtor
public UpdatesViewModel(IWingetService wingetService)
{
    _wingetService = wingetService;
}
```

---

## Abrindo um Pull Request

### Fluxo

```bash
# 1. Crie uma branch a partir da main
git checkout -b feature/issue-6-check-updates-async

# 2. Faça suas alterações e commit
git add .
git commit -m "feat: implementar CheckUpdatesAsync com cache de 30 min (#6)"

# 3. Push para o seu fork
git push origin feature/issue-6-check-updates-async

# 4. Abra o PR no GitHub apontando para main do repositório original
```

### Convenção de nome de branch

| Tipo | Formato | Exemplo |
|---|---|---|
| Nova funcionalidade | `feature/issue-{N}-{descricao}` | `feature/issue-6-check-updates-async` |
| Correção de bug | `fix/issue-{N}-{descricao}` | `fix/issue-23-flyout-dpi` |
| Documentação | `docs/{descricao}` | `docs/contributing-update` |
| Refatoração | `refactor/{descricao}` | `refactor/winget-service-cache` |

### Convenção de mensagem de commit

```
tipo: descrição curta em minúsculas (#número-da-issue)

Tipos: feat, fix, refactor, test, docs, chore
```

### Checklist antes de abrir o PR

- [ ] Os testes unitários passam: `dotnet test`
- [ ] Nenhum warning novo introduzido no build
- [ ] Membros públicos novos têm XML documentation (`///`)
- [ ] Nenhuma string visível ao usuário hardcoded no C#
- [ ] Nenhum acesso direto ao `AppDbContext` fora de um Repository
- [ ] A issue referenciada está mencionada no PR com `Closes #N`

---

## O que bloqueia um PR

PRs com qualquer um dos itens abaixo serão rejeitados até corrigir:

| Problema | Por quê |
|---|---|
| `async void` fora de event handler | Engole exceptions — bug silencioso garantido |
| `.Result` ou `.Wait()` em Task | Pode causar deadlock na UI thread |
| `new WingetService()` em ViewModel | Quebra testabilidade e viola a arquitetura |
| `AppDbContext` injetado em Service ou ViewModel | Deve ser acessado apenas via Repository |
| String de UI hardcoded no C# | Impossibilita tradução futura |
| Membro público sem XML doc | Outros contribuidores não saberão usar |
| Teste sem assert | Teste que nunca falha não protege nada |

---

## Dúvidas

Abra uma **Discussion** no GitHub (aba Discussions) — não uma issue. Issues são para bugs e tarefas. Discussions são para perguntas, ideias e conversas.

Se a dúvida for sobre uma issue específica, comente diretamente na issue.
