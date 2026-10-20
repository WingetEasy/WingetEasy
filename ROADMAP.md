# WingetEasy — Roadmap

> **Estado atual:** Issues #1–#25 implementadas. Issues #26–#45 pendentes + identidade visual.
> **Meta:** App totalmente funcional, publicado na Microsoft Store e disponível via `winget install`.

---

## Milestone 1 — Core sem UI ✅ (Concluído)

| # | Issue | Status |
|---|---|---|
| #1 | Configurar solution C# (App, Core, Data) | ✅ |
| #2 | Gerenciamento centralizado de NuGet | ✅ |
| #3 | GitHub Actions — CI básico | ✅ |
| #4 | Modelos de domínio (Records imutáveis) | ✅ |
| #5 | Interfaces dos serviços | ✅ |
| #6 | WingetService — CheckUpdatesAsync + parsing JSON | ✅ |
| #7 | WingetService — InstallUpdateAsync com progresso | ✅ |
| #8 | WingetService — GetInstalledPackages + IsWingetAvailable | ✅ |
| #9 | AppDbContext + EF Core + SQLite | ✅ |
| #10 | Entidades do banco + migration InitialSchema | ✅ |
| #11 | Repositórios (Settings, UpdateHistory, Package) | ✅ |
| #12 | AdminElevationHelper — elevação UAC | ✅ |
| #13 | SchedulerService — PeriodicTimer | ✅ |
| #14 | UpdateService — orquestração batch | ✅ |
| #15 | Serilog — logging estruturado | ✅ |
| #16 | Circuit Breaker para falhas consecutivas | ✅ |
| #17 | Testes unitários — WingetService | ✅ |
| #18 | Testes unitários — Repositórios | ✅ |

---

## Milestone 2 — UI Base WinUI 3 ✅ (Concluído)

| # | Issue | Status |
|---|---|---|
| #19 | Projeto WinUI 3 + DI Container (App.xaml.cs) | ✅ |
| #20 | TrayIcon — ícone e menu contextual | ✅ |
| #21 | MainWindow — NavigationView + Mica/Acrylic | ✅ |
| #22 | UpdatesViewModel (⚠️ parcial — usa dados mockados) | ⚠️ |
| #23 | UpdateFlyout — layout, posicionamento, animações | ✅ |
| #24 | PackageItemControl — controle por linha de pacote | ✅ |
| #25 | SettingsViewModel + SettingsPage completa | ✅ |

---

## Milestone 3 — Polimento e Robustez 🔄 (Em aberto)

### Prioridade crítica — desbloqueiam a feature central

| # | Issue | Descrição | Dependências |
|---|---|---|---|
| **#22-fix** | Conectar UpdatesViewModel aos serviços reais | Remover mocks de `CheckUpdatesAsync` e `UpdateSelectedAsync`; injetar `IWingetService` e `IUpdateService` | #6, #14 |
| **#26** | Feedback visual de progresso durante atualização | Estados visuais por pacote (Aguardando/Atualizando/Concluído/Falha), ProgressRing no lugar do checkbox, resumo ao final | #22-fix |
| **#27** | Toast Notifications nativas do Windows | `ToastNotificationService` real com botões interativos; capturar clique em "Ver agora" | #19, #6 |
| **#29** | Integração UAC com fluxo de atualização | ContentDialog antes do prompt UAC, tratar recusa graciosamente, processo elevado envia resultado via JSON | #12, #14, #22 |

### Novas páginas (placeholders hoje)

| # | Issue | Descrição | Dependências |
|---|---|---|---|
| **#30** | HistoryViewModel + HistoryPage | Timeline agrupada por dia, filtros (Hoje/7d/Mês/Tudo), busca, exportação CSV | #11, #21 |
| **#28** | Detecção e troca de tema em tempo real | `UISettings.ColorValuesChanged`, aplicar via `DispatcherQueue`, modo Sistema/Claro/Escuro | #25 |
| **#31** | OnboardingDialog — primeira execução | ContentDialog 4 passos via FlipView; detectar via banco se já concluído | #19, #25 |

---

## Milestone 4 — Funcionalidades Avançadas 📋 (Em andamento)

| # | Issue | Descrição | Dependências |
|---|---|---|---|
| **#32** | BackupService — criar snapshot | `winget export`, enriquecer com metadados WingetEasy, salvar no banco | #8, #11 |
| **#33** | BackupService — restaurar snapshot | Instalar por pacote com progresso, relatório de falhas, retry | #32, #14 |
| **#34** | BackupPage — UI completa | Cards de snapshot, criar/exportar/restaurar/deletar, overlay de progresso | #32, #33, #21 |
| **#35** | ProfilesService + ProfilesPage | Profiles curados pelo usuário, grid de cards, CRUD completo | #8, #11, #21 |
| **#39** | Filtros e categorias na UpdatesPage/Flyout | Chips por categoria (App/Runtime/Segurança), busca em tempo real, ordenação | #24, #22 |
| **#36** | Modo headless — argumentos CLI | `--check`, `--update-all`, `--backup`, `--restore`, `--minimized`, exit codes | #6, #7, #14, #32 |
| **#37** | Auto-update via Velopack | `VelopackApp.Build().Run()`, InfoBar discreta, download delta em background | #2, #19 |
| **#38** | Task Scheduler nativo do Windows | Migrar de `PeriodicTimer` para `Windows Task Scheduler`, `--check` via tarefa agendada | #13, #25 |

---

## Milestone 5 — Identidade Visual 🎨 (Pendente — aguardando design)

> **Nota:** O design visual completo do WingetEasy foi criado no Claude Design.
> Cole o conteúdo do arquivo `WingetEasy.html` aqui para incorporar as especificações.

### O que precisa ser implementado:
- [ ] Design tokens: cores de accent, tipografia, espaçamentos, corner radius
- [ ] Ícones do app em todas as resoluções (16/32/44/48/150/310px) com badge de notificação
- [ ] Ícone da tray com variante "updates disponíveis"
- [ ] Visual refinado do `PackageItemControl` (badges de categoria, estados visuais)
- [ ] Visual da `UpdateFlyout` (header, footer, animações)
- [ ] Tema claro e escuro consistentes com Fluent Design
- [ ] Splash screen
- [ ] Consistência visual entre todas as páginas

---

## Milestone 6 — Distribuição 🚀 (Pendente)

| # | Issue | Descrição | Dependências |
|---|---|---|---|
| **#40** | Empacotamento MSIX + manifesto | Capacidades `internetClient` + `runFullTrust`, assets em todas as resoluções | #19, #25 |
| **#41** | Assinatura de código | Self-signed para alfa, EV code signing para produção, `signtool` no CI | #40 |
| **#42** | GitHub Actions — workflow de release | Trigger em tag `v*.*.*`, build → test → publish → sign → release automático | #3, #40, #41 |
| **#43** | Manifesto YAML para `winget-pkgs` | `wingetcreate`, PR no `microsoft/winget-pkgs`, SHA256 do MSIX | #40, #41, #42 |
| **#44** | README profissional | Screenshots, badges CI/versão/licença, comando `winget install`, guia de contribuição | #42 |
| **#45** | GitHub Pages — Política de privacidade + suporte | `/privacy` e `/support` em PT e EN, URL no manifesto MSIX | #44 |

---

## Correções técnicas pendentes (fora das issues)

| Item | Descrição |
|---|---|
| `Directory.Build.props` | Atualizar `TargetFramework` de `net8.0` para `net10.0` |
| Migration `CheckLogs` | Corrigir tipo da coluna `DurationMs` de `bool` para `long` |
| Tray "Verificar agora" | Disparar verificação real (hoje só loga) |
| Salvar posição da janela | Implementar save/restore comentado em `MainWindow` |

---

## Ordem recomendada de execução

```
Semana 1:  #22-fix → #26 → #27 → #29  (UpdatesPage 100% funcional)
Semana 2:  #30 → #28 → #31             (Páginas restantes + UX)
Semana 3:  #32 → #33 → #34 → #35      (Backup e Profiles)
Semana 4:  #39 → #38 → #37            (Filtros, Scheduler, Auto-update)
Semana 5:  Identidade Visual            (Após receber o design)
Semana 6:  #36 → #40 → #41 → #42      (CLI + empacotamento)
Semana 7:  #43 → #44 → #45            (Distribuição pública)
```

---

## Métricas de conclusão

| Fase | Issues | Status |
|---|---|---|
| Milestone 1 — Core | #1–#18 | ✅ 18/18 |
| Milestone 2 — UI Base | #19–#25 | ⚠️ 6/7 (UpdatesViewModel parcial) |
| Milestone 3 — Polimento | #22-fix, #26–#31 | ❌ 0/7 |
| Milestone 4 — Avançado | #32–#39 | ❌ 0/8 |
| Milestone 5 — Visual | Design tokens + assets | ❌ 0/1 |
| Milestone 6 — Distribuição | #40–#45 | ❌ 0/6 |
| **Total** | | **24/45** (53%) |
