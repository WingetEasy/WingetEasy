## O que esse PR faz?

<!-- Descreva o que foi implementado em 2-3 frases. -->


## Issue relacionada

Closes #<!-- número da issue -->

---

## Tipo de mudança

- [ ] Nova funcionalidade (feature)
- [ ] Correção de bug (fix)
- [ ] Refatoração sem mudança de comportamento
- [ ] Documentação
- [ ] Outro: ___

---

## Como testar

<!-- Passos exatos para testar o que foi implementado. Seja específico — quem revisar vai seguir esses passos. -->

1. 
2. 
3. 

**Resultado esperado:**

---

## Checklist obrigatório

### Código
- [ ] Os testes existentes continuam passando (`dotnet test`)
- [ ] Novos testes foram adicionados para a lógica implementada
- [ ] Nenhum warning novo foi introduzido no build
- [ ] Nenhum uso de `.Result` ou `.Wait()` em Tasks
- [ ] Nenhum uso de `async void` fora de event handlers de UI

### Arquitetura
- [ ] Nenhuma lógica de negócio em ViewModels — apenas em Services
- [ ] Nenhum acesso direto ao `AppDbContext` fora de Repositories
- [ ] Nenhum `new ServiceName()` — sempre injetado via DI
- [ ] View não acessa Service diretamente — sempre via ViewModel

### Qualidade
- [ ] Todos os membros públicos novos têm XML documentation (`///`)
- [ ] Nenhuma string visível ao usuário hardcoded no C# — usa arquivo de resource
- [ ] Nomes de classes, métodos e arquivos seguem as convenções do CONTRIBUTING.md

### Se mexeu no banco de dados
- [ ] Uma nova migration foi gerada (`dotnet ef migrations add NomeDaMigration`)
- [ ] A migration foi revisada manualmente antes do commit
- [ ] A migration aplica sem erros em banco vazio e em banco existente

---

## Screenshots (se tiver mudança visual)

<!-- Cole screenshots do antes e depois, ou do resultado final. Inclua tema claro e escuro se aplicável. -->

| Antes | Depois |
|---|---|
| | |

---

## Notas para o revisor

<!-- Qualquer contexto adicional, decisões de design, trade-offs ou pontos de atenção para quem for revisar. -->
