
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Pentare.Treinamento.Plugin.Ocorrencia
{
    /// <summary>
    /// Plug-in responsável por distribuir automaticamente as ocorrências
    /// (Cases) entre equipes, conforme as regras cadastradas no Dataverse.
    ///
    /// A distribuição considera:
    /// - Categoria da ocorrência;
    /// - Prioridade da ocorrência;
    /// - Indicador VIP da conta associada ao cliente.
    ///
    /// Antes de atribuir a ocorrência, verifica se a equipe responsável
    /// possui a validação administrativa de permissão de leitura.
    /// </summary>
    public class RegraDistribuicao : IPlugin
    {
        /// <summary>
        /// Ponto de entrada da execução do plug-in.
        /// Recupera os serviços do Dataverse, valida o contexto da execução
        /// e inicia o processamento da regra de distribuição.
        /// </summary>
        /// <param name="serviceProvider">
        /// Provedor de serviços disponibilizado pelo ambiente do Dataverse.
        /// </param>
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext executionContext = (IPluginExecutionContext)serviceProvider.GetService(
                typeof(IPluginExecutionContext));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(
                typeof(IOrganizationServiceFactory));

            IOrganizationService service = serviceFactory.CreateOrganizationService(executionContext.UserId);

            try
            {
                tracingService.Trace("Iniciando RegraDistribuicao.");

                if (!string.Equals(executionContext.MessageName,"Create", StringComparison.OrdinalIgnoreCase))
                {
                    tracingService.Trace("Execução ignorada: evento diferente do Create.");
                    return;
                }

                if (executionContext.Stage != 20)
                {
                    tracingService.Trace("Execução ignorada: estágio diferente do PreOperation.");
                    return;
                }

                if (!executionContext.InputParameters.Contains("Target") || !(executionContext.InputParameters["Target"]
                        is Entity targetEntity))
                {
                    tracingService.Trace("Target ausente ou inválido.");
                    return;
                }

                if (!string.Equals(targetEntity.LogicalName, "incident", StringComparison.OrdinalIgnoreCase))
                {
                    tracingService.Trace("Execução ignorada: tabela diferente de incident.");
                    return;
                }

                tracingService.Trace("Target válido para a tabela incident.");

                OptionSetValue categoriaOcorrencia = targetEntity.GetAttributeValue<OptionSetValue>("ptr_categoriadeatendimento");
                OptionSetValue prioridadeOcorrencia = targetEntity.GetAttributeValue<OptionSetValue>("prioritycode");

                if (!targetEntity.Contains("customerid") || !(targetEntity["customerid"] is EntityReference clienteRef))
                {
                    tracingService.Trace("Cliente ausente. Mantendo o proprietário padrão.");
                    return;
                }

                EntityReference contaRef = ResolveContaCliente(service, tracingService, clienteRef);

                if (contaRef == null)
                {
                    tracingService.Trace("Não foi possível determinar a conta do cliente. Mantendo o proprietário padrão.");
                    return;
                }

                Entity conta = service.Retrieve("account", contaRef.Id, new ColumnSet("ptr_clientevip"));

                if (!conta.Contains("ptr_clientevip"))
                {
                    tracingService.Trace("Campo VIP ausente na conta. Mantendo o proprietário padrão.");
                    return;
                }

                bool vipCliente = conta.GetAttributeValue<bool>("ptr_clientevip");

                tracingService.Trace("Indicador VIP consultado com sucesso.");

                if (categoriaOcorrencia == null || prioridadeOcorrencia == null)
                {
                    tracingService.Trace("Categoria ou prioridade não informada. Mantendo o proprietário padrão.");
                    return;
                }

                ProcessarRegraDistribuicao(service, tracingService, targetEntity, categoriaOcorrencia, prioridadeOcorrencia,
                    vipCliente);

                tracingService.Trace("RegraDistribuicao finalizado.");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("Falha técnica em RegraDistribuicao. Tipo: {0}", ex.GetType().FullName);

                throw new InvalidPluginExecutionException(
                    "Não foi possível concluir a distribuição da ocorrência. " +
                    "Verifique a configuração das regras e as permissões " +
                    "da equipe responsável. Se o problema persistir, " +
                    "contate o administrador do Dynamics 365.");
            }
        }

        /// <summary>
        /// Consulta as regras de distribuição cadastradas e identifica
        /// a equipe responsável pela ocorrência.
        ///
        /// A combinação utilizada na pesquisa considera categoria,
        /// prioridade, indicador VIP e situação ativa da regra.
        /// </summary>
        /// <param name="service">Serviço de acesso ao Dataverse.</param>
        /// <param name="tracingService">Serviço de rastreamento do plug-in.</param>
        /// <param name="targetEntity">Ocorrência que está sendo criada.</param>
        /// <param name="categoriaOcorrencia">Categoria informada na ocorrência.</param>
        /// <param name="prioridadeOcorrencia">Prioridade informada na ocorrência.</param>
        /// <param name="vipCliente">Indica se o cliente é VIP.</param>
        private void ProcessarRegraDistribuicao(IOrganizationService service, ITracingService tracingService,
            Entity targetEntity, OptionSetValue categoriaOcorrencia, OptionSetValue prioridadeOcorrencia, bool vipCliente)
        {
            var query = new QueryExpression("ptr_regradistribuicao")
            {
                ColumnSet = new ColumnSet("ptr_equiperesponsavel", "ptr_categoria", "ptr_prioridade", "ptr_clientevip"),

                Criteria = new FilterExpression(LogicalOperator.And), TopCount = 2
            };

            query.Criteria.AddCondition("ptr_categoria", ConditionOperator.Equal, categoriaOcorrencia.Value);

            query.Criteria.AddCondition("ptr_prioridade", ConditionOperator.Equal, prioridadeOcorrencia.Value);

            query.Criteria.AddCondition("ptr_clientevip", ConditionOperator.Equal, vipCliente);

            query.Criteria.AddCondition("ptr_ativa", ConditionOperator.Equal, true);

            EntityCollection regras = service.RetrieveMultiple(query);

            if (regras.Entities.Count > 1)
            {
                tracingService.Trace("Distribuição bloqueada: foram encontradas múltiplas regras para a mesma combinação.");

                throw new InvalidPluginExecutionException(
                    "Existe mais de uma regra de distribuição ativa " +
                    "para a categoria, prioridade e condição VIP " +
                    "informadas. Solicite ao administrador que " +
                    "corrija as regras antes de criar a ocorrência.");
            }

            if (regras.Entities.Count == 0)
            {
                tracingService.Trace("Nenhuma regra correspondente encontrada. Mantendo o proprietário padrão.");
                return;
            }

            Entity regra = regras.Entities[0];

            if (!regra.Contains("ptr_equiperesponsavel") || !(regra["ptr_equiperesponsavel"] is EntityReference equipeRef) || 
                !string.Equals(equipeRef.LogicalName, "team", StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("A regra encontrada não possui uma referência válida para uma equipe.");

                throw new InvalidPluginExecutionException(
                    "A regra de distribuição encontrada não possui " +
                    "uma equipe responsável válida. Solicite ao " +
                    "administrador que corrija sua configuração.");
            }

            Entity equipe = service.Retrieve("team", equipeRef.Id, new ColumnSet("ptr_permissoesocorrencia"));

            bool possuiPermissao = equipe.GetAttributeValue<bool>("ptr_permissoesocorrencia");

            if (!possuiPermissao)
            {
                tracingService.Trace(
                    "Distribuição bloqueada: a equipe não possui permissão de leitura de ocorrências validada.");

                throw new InvalidPluginExecutionException(
                    "A equipe responsável não possui permissão de leitura " +
                    "de ocorrências validada. Solicite ao administrador " +
                    "que verifique suas permissões.");
            }

            targetEntity["ownerid"] = new EntityReference("team", equipeRef.Id);

            tracingService.Trace("Proprietário definido a partir da regra de distribuição.");
        }

        /// <summary>
        /// Identifica a conta associada ao cliente informado na ocorrência.
        ///
        /// Se o cliente for uma conta, retorna a própria referência.
        /// Se for um contato, consulta o campo parentcustomerid e retorna
        /// a conta associada, quando existente.
        /// </summary>
        /// <param name="service">Serviço de acesso ao Dataverse.</param>
        /// <param name="tracingService">Serviço de rastreamento do plug-in.</param>
        /// <param name="clienteRef">Referência do cliente da ocorrência.</param>
        /// <returns>
        /// Referência da conta associada ou null quando não for possível
        /// determinar uma conta válida.
        /// </returns>
        private EntityReference ResolveContaCliente(IOrganizationService service, ITracingService tracingService,
            EntityReference clienteRef)
        {
            if (string.Equals(clienteRef.LogicalName, "account", StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("O cliente informado é uma conta.");

                return clienteRef;
            }

            // Quando o cliente é um contato, verifica se ele possui
            // uma conta associada pelo campo parentcustomerid.
            if (string.Equals(clienteRef.LogicalName, "contact", StringComparison.OrdinalIgnoreCase))
            {
                tracingService.Trace("O cliente informado é um contato. Consultando a conta associada.");

                Entity contato = service.Retrieve("contact", clienteRef.Id, new ColumnSet("parentcustomerid"));

                // Verifica se existe uma referência válida para uma conta.
                if (contato.Contains("parentcustomerid") && contato["parentcustomerid"] is EntityReference parentRef && 
                    string.Equals(parentRef.LogicalName, "account", StringComparison.OrdinalIgnoreCase))
                {
                    tracingService.Trace("Conta associada ao contato encontrada.");

                    return parentRef;
                }

                tracingService.Trace("O contato não possui uma conta associada.");

                return null;
            }

            tracingService.Trace("Tipo de cliente não suportado para determinar VIP.");

            return null;
        }
    }
}
