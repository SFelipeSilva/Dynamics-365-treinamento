# RegraDistribuicao --- Dynamics 365 / Dataverse

## 1. Identificação

  Item                     Descrição
  ------------------------ -----------------------------------------
  Projeto                  `RegraDistribuicao`
  Plataforma               Microsoft Dynamics 365 / Dataverse
  Linguagem                C#
  Classe                   `RegraDistribuicao`
  Namespace                `Pentare.Treinamento.Plugin.Ocorrencia`
  Interface                `Microsoft.Xrm.Sdk.IPlugin`
  Tabela processada        Ocorrência (`incident`)
  Tabela de configuração   `ptr_regradistribuicao`
  Tabela de equipes        Equipe (`team`)

## 2. Objetivo

O plug-in distribui automaticamente novas ocorrências entre equipes, de
acordo com regras cadastradas no Dataverse.

A seleção da regra considera:

-   Categoria da ocorrência;
-   Prioridade da ocorrência;
-   Indicador VIP da conta associada ao cliente;
-   Situação ativa da regra.

Quando encontra exatamente uma regra correspondente, o plug-in valida a
equipe responsável e define essa equipe como proprietária (`ownerid`) da
ocorrência.

## 3. Registro do passo do plug-in

Configuração esperada do passo:

  Propriedade       Configuração
  ----------------- -----------------------------------------------------------
  Mensagem          `Create`
  Tabela primária   `incident`
  Estágio           PreOperation (20)
  Modo              Síncrono
  Classe            `Pentare.Treinamento.Plugin.Ocorrencia.RegraDistribuicao`

O estágio PreOperation permite alterar `ownerid` diretamente no
`Target`, antes de a ocorrência ser gravada.

O código também verifica a mensagem `Create`, o estágio 20 e o nome
lógico `incident`. Se algum desses critérios não corresponder, a
execução é encerrada sem processar a distribuição.

## 4. Tabelas e colunas utilizadas

### 4.1. Ocorrência (`incident`)

  ------------------------------------------------------------------------------
  Campo                   Nome lógico                    Utilização
  ----------------------- ------------------------------ -----------------------
  Cliente                 `customerid`                   Identifica a conta ou o
                                                         contato associado à
                                                         ocorrência.

  Categoria               `ptr_categoriadeatendimento`   Critério para localizar
                                                         a regra.

  Prioridade              `prioritycode`                 Critério para localizar
                                                         a regra.

  Proprietário            `ownerid`                      Recebe a equipe
                                                         responsável quando a
                                                         distribuição é
                                                         concluída.
  ------------------------------------------------------------------------------

### 4.2. Conta (`account`)

  -----------------------------------------------------------------------
  Campo                   Nome lógico             Utilização
  ----------------------- ----------------------- -----------------------
  Cliente VIP             `ptr_clientevip`        Indica se a conta é VIP
                                                  e participa da seleção
                                                  da regra.

  -----------------------------------------------------------------------

O campo `customerid` é um lookup polimórfico. O plug-in aceita:

-   **Conta (`account`):** utiliza diretamente a conta informada.
-   **Contato (`contact`):** consulta `parentcustomerid` e utiliza a
    conta associada, se o lookup apontar para uma conta.

Se o contato não possuir uma conta principal, ou se o tipo de cliente
não for suportado, o plug-in mantém o proprietário padrão e encerra o
processamento.

### 4.3. Regra de distribuição (`ptr_regradistribuicao`)

  -------------------------------------------------------------------------
  Campo                   Nome lógico               Utilização
  ----------------------- ------------------------- -----------------------
  Categoria               `ptr_categoria`           Categoria atendida pela
                                                    regra.

  Prioridade              `ptr_prioridade`          Prioridade atendida
                                                    pela regra.

  Cliente VIP             `ptr_clientevip`          Condição VIP exigida
                                                    pela regra.

  Ativa                   `ptr_ativa`               Indica se a regra pode
                                                    ser utilizada.

  Equipe responsável      `ptr_equiperesponsavel`   Lookup para a equipe
                                                    (`team`) que receberá a
                                                    ocorrência.
  -------------------------------------------------------------------------

### 4.4. Equipe (`team`)

  ----------------------------------------------------------------------------
  Campo                   Nome lógico                  Utilização
  ----------------------- ---------------------------- -----------------------
  Permissões de           `ptr_permissoesocorrencia`   Indicador
  ocorrência validadas                                 administrativo que
                                                       precisa estar como Sim
                                                       para permitir a
                                                       atribuição pelo
                                                       plug-in.

  ----------------------------------------------------------------------------

**Importante:** `ptr_permissoesocorrencia` pertence à tabela `team`.
O plug-in recupera a equipe indicada por `ptr_equiperesponsavel` e 
consulta esse campo diretamente no registro da equipe.

Esse indicador representa uma validação administrativa prévia. Ele não
verifica em tempo real todas as permissões efetivas do Dataverse e não
concede permissões. As funções de segurança da equipe devem ser
configuradas separadamente. Se as funções ou permissões forem alteradas,
o administrador deve reavaliar o indicador.

## 5. Lógica de execução

1.  Recupera `ITracingService`, `IPluginExecutionContext`,
    `IOrganizationServiceFactory` e cria `IOrganizationService`
    utilizando `executionContext.UserId`.
2.  Confirma que a mensagem é `Create`, o estágio é PreOperation (20) e
    o `Target` é uma entidade `incident`.
3.  Lê a categoria (`ptr_categoriadeatendimento`) e a prioridade
    (`prioritycode`).
4.  Recupera o cliente (`customerid`).
5.  Determina a conta associada: utiliza a própria conta ou consulta
    `parentcustomerid` quando o cliente é um contato.
6.  Consulta `ptr_clientevip` na conta.
7.  Pesquisa registros ativos em `ptr_regradistribuicao` que
    correspondam à categoria, prioridade e condição VIP.
8.  Limita a consulta a dois resultados (`TopCount = 2`), pois dois
    registros já são suficientes para identificar duplicidade.
9.  Se encontrar exatamente uma regra, valida se `ptr_equiperesponsavel`
    contém uma referência válida para `team`.
10. Recupera a equipe e consulta `ptr_permissoesocorrencia`.
11. Se o indicador estiver como Sim, define `ownerid` no `Target` com a
    referência da equipe.

## 6. Consulta das regras

A consulta é realizada na tabela `ptr_regradistribuicao`, com condições
combinadas por `AND`:

  -----------------------------------------------------------------------
  Coluna da regra                     Valor comparado
  ----------------------------------- -----------------------------------
  `ptr_categoria`                     Valor de
                                      `ptr_categoriadeatendimento` da
                                      ocorrência

  `ptr_prioridade`                    Valor de `prioritycode` da
                                      ocorrência

  `ptr_clientevip`                    Valor de `ptr_clientevip` da conta

  `ptr_ativa`                         `true`
  -----------------------------------------------------------------------

Somente regras que atendam a todos os critérios são consideradas.

A consulta utiliza `TopCount = 2` para distinguir três situações:
nenhuma regra, uma regra ou mais de uma regra.

## 7. Comportamentos e tratamento de situações

  -----------------------------------------------------------------------
  Situação                            Comportamento
  ----------------------------------- -----------------------------------
  Mensagem diferente de `Create`      Execução encerrada.

  Estágio diferente de PreOperation   Execução encerrada.

  `Target` ausente ou inválido        Execução encerrada.

  Tabela diferente de `incident`      Execução encerrada.

  Cliente ausente                     Mantém o proprietário padrão.

  Contato sem conta associada         Mantém o proprietário padrão.

  Tipo de cliente não suportado       Mantém o proprietário padrão.

  Campo VIP ausente na conta          Mantém o proprietário padrão.

  Categoria ou prioridade ausente     Mantém o proprietário padrão.

  Nenhuma regra correspondente        Mantém o proprietário padrão.

  Mais de uma regra correspondente    Bloqueia a criação com mensagem de
                                      duplicidade.

  Regra sem equipe válida             Bloqueia a criação com mensagem de
                                      configuração.

  `ptr_permissoesocorrencia` igual a  Bloqueia a criação com mensagem de
  Não                                 validação de permissões.

  Equipe validada                     Define a equipe como proprietária
                                      da ocorrência.

  Falha técnica inesperada            Registra detalhes no rastreamento e
                                      apresenta mensagem genérica ao
                                      usuário.
  -----------------------------------------------------------------------

Quando as condições necessárias não são atendidas, o código não define
`ownerid`; portanto, o proprietário padrão da operação é mantido.

As exceções `InvalidPluginExecutionException` lançadas pelas validações
de negócio são relançadas pelo método `Execute`, preservando a mensagem
específica. Outras exceções são registradas no `ITracingService` e
convertidas em uma mensagem genérica.

## 8. Configuração e manutenção das regras

Antes de ativar uma regra:

1.  Confirme que a equipe indicada em `ptr_equiperesponsavel` existe.
2.  Confirme que a equipe é uma equipe proprietária (`Owner Team`)
    adequada para ser proprietária de ocorrências.
3.  Verifique as funções de segurança atribuídas à equipe e o privilégio
    de leitura da tabela `incident`, no nível de acesso adequado ao
    cenário.
4.  Após a validação administrativa, marque `ptr_permissoesocorrencia`
    como Sim no registro da equipe.
5.  Se as funções ou permissões da equipe forem alteradas, reavalie o
    indicador e mantenha-o como Não enquanto a validação estiver
    pendente.
6.  Evite regras ativas duplicadas para a mesma combinação de
    Categoria + Prioridade + Cliente VIP.
7.  Confira os valores numéricos das colunas Choice no ambiente de
    destino.

A marcação de `ptr_permissoesocorrencia` como Sim não configura nem
concede privilégios. Ela registra apenas que a validação administrativa
foi realizada.

## 9. Rastreamento e diagnóstico

O plug-in utiliza `ITracingService` para registrar etapas relevantes,
incluindo:

-   Início e validação do contexto;
-   Valores de categoria e prioridade;
-   Tipo de cliente identificado;
-   Consulta do indicador VIP;
-   Resultado da consulta de regras;
-   Validação da equipe;
-   Definição do proprietário;
-   Tipo e detalhes de falhas técnicas.

Para investigar uma falha:

1.  Habilite o rastreamento de plug-ins no ambiente.
2.  Reproduza a criação da ocorrência.
3.  Consulte os registros em **Plugin Trace Log**.
4.  Verifique a mensagem, o tipo da exceção e o stack trace.

A mensagem apresentada ao usuário em caso de falha técnica é genérica.
Os detalhes técnicos devem ser consultados no rastreamento
administrativo.

## 10. Documentação XML do código C

O código pode utilizar comentários XML, como `/// <summary>`,
`/// <param>` e `/// <returns>`, para documentar classes e métodos, além
de auxiliar o IntelliSense no Visual Studio.

Para gerar o arquivo XML durante a compilação:

1.  Clique com o botão direito no projeto no Gerenciador de Soluções.
2.  Abra **Propriedades (Properties)**.
3.  Acesse **Build (Compilar) \> Output (Saída)**.
4.  Habilite **Generate a file containing API documentation**.
5.  Salve e compile o projeto (`Ctrl + Shift + B`).

Alternativamente, adicione ao arquivo `.csproj`:

``` xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

A compilação gera um arquivo `.xml` com o nome do assembly, normalmente
ao lado da DLL na pasta de saída. Esse XML é documentação complementar e
não substitui a DLL que deve ser registrada no Dataverse.

## 11. Cenários de teste

  -----------------------------------------------------------------------
  Cenário                             Resultado esperado
  ----------------------------------- -----------------------------------
  Conta VIP com uma regra ativa       Ocorrência atribuída à equipe da
  correspondente e equipe validada    regra.

  Conta não VIP com uma regra         Ocorrência atribuída à equipe da
  correspondente e equipe validada    regra.

  Cliente é contato vinculado a uma   Indicador VIP obtido da conta
  conta                               principal e regra avaliada.

  Contato sem conta principal         Proprietário padrão mantido.

  Cliente ausente                     Proprietário padrão mantido.

  Categoria ou prioridade ausente     Proprietário padrão mantido.

  Nenhuma regra correspondente        Proprietário padrão mantido.

  Duas ou mais regras correspondentes Criação bloqueada por duplicidade.

  Regra sem equipe responsável válida Criação bloqueada por configuração
                                      inválida.

  Equipe com                          Criação bloqueada pela validação
  `ptr_permissoesocorrencia` igual a  administrativa.
  Não                                 

  Equipe com                          Atribuição realizada, sujeita à
  `ptr_permissoesocorrencia` igual a  validação de segurança do
  Sim                                 Dataverse.

  Falha técnica durante consulta ao   Mensagem genérica ao usuário e
  Dataverse                           detalhes no Plugin Trace Log.
  -----------------------------------------------------------------------

## 12. Dependências e implantação

O projeto utiliza:

-   `Microsoft.Xrm.Sdk`;
-   `Microsoft.Xrm.Sdk.Query`;
-   `System`.

Para implantar ou atualizar:

1.  Confirme os nomes lógicos das colunas personalizadas no ambiente.
2.  Confirme que as equipes e regras necessárias existem.
3.  Confira os valores das opções Choice utilizados nas regras.
4.  Valide as funções de segurança das equipes e o indicador
    `ptr_permissoesocorrencia`.
5.  Compile o projeto.
6.  Registre ou atualize a DLL no ambiente do Dataverse.
7.  Confira o registro do passo para `Create`, tabela `incident`,
    estágio PreOperation e modo síncrono.
8.  Execute os cenários de teste em ambiente de desenvolvimento ou
    homologação antes de transportar a solução.

## 13. Observações técnicas

-   A consulta de regras é feita na tabela personalizada
    `ptr_regradistribuicao`.
-   O lookup `ptr_equiperesponsavel` referencia a tabela padrão `team`.
-   O indicador `ptr_permissoesocorrencia` é recuperado da tabela
    `team`.
-   A identidade usada para criar `IOrganizationService` é
    `executionContext.UserId`. Essa identidade precisa ter privilégios
    para realizar as consultas necessárias.
-   O plug-in não implementa uma verificação completa e dinâmica das
    permissões efetivas da equipe; utiliza o indicador administrativo
    previamente mantido.
-   Os nomes lógicos e valores Choice devem ser conferidos em cada
    ambiente, especialmente ao transportar a solução.

## 14. Resumo

O plug-in `RegraDistribuicao` automatiza a atribuição de novas
ocorrências a equipes com base em regras configuráveis no Dataverse. A
seleção considera categoria, prioridade e condição VIP do cliente. Antes
da atribuição, valida a referência da equipe e o indicador
administrativo `ptr_permissoesocorrencia`, armazenado na própria tabela
`team`.

A solução também trata regras duplicadas, configurações inválidas e
falhas técnicas, utilizando `ITracingService` para apoiar o diagnóstico.
