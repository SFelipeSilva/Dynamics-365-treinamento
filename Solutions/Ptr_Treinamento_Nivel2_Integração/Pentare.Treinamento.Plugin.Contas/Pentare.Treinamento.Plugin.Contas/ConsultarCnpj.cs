using Microsoft.Xrm.Sdk;
using System;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Pentare.Treinamento.Plugin.Contas
{
    /// <summary>
    /// Plugin responsável por executar a Custom API ptr_ConsultarCnpj.
    /// Realiza a consulta de informações cadastrais de uma empresa na BrasilAPI
    /// e disponibiliza os dados retornados por meio dos parâmetros de saída da Custom API.
    /// </summary>
    public class ConsultarCnpj : IPlugin
    {
        /// <summary>
        /// Método principal de execução do plugin.
        /// Obtém o contexto da execução, valida a mensagem recebida e inicia
        /// o processo de consulta dos dados cadastrais do CNPJ.
        /// </summary>
        /// <param name="serviceProvider">
        /// Provedor de serviços disponibilizado pelo Dataverse,
        /// utilizado para obter o contexto da execução e o serviço de rastreamento.
        /// </param>
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext executionContext = (IPluginExecutionContext)serviceProvider.GetService(
                typeof(IPluginExecutionContext));

            try
            {
                tracingService.Trace("Iniciando execução da Custom API ConsultarCnpj.");

                if (!string.Equals(executionContext.MessageName, "ptr_ConsultarCnpj", StringComparison.OrdinalIgnoreCase))
                {
                    tracingService.Trace("Mensagem de execução diferente da esperada: {0}", executionContext.MessageName);
                    return;
                }

                string cnpjInformado = executionContext.InputParameters.Contains("ptr_CNPJ")
                        ? executionContext.InputParameters["ptr_CNPJ"] as string : null;

                tracingService.Trace("Parâmetro de entrada recebido.");

                ConsultaDados(cnpjInformado, executionContext, tracingService).GetAwaiter().GetResult();

                tracingService.Trace("Execução da Custom API finalizada.");
            }
            catch (Exception ex)
            {
                tracingService.Trace("Falha inesperada na Custom API. Tipo: {0}. Mensagem: {1}", ex.GetType().FullName, ex.Message);

                throw new InvalidPluginExecutionException("Ocorreu um erro inesperado ao consultar o CNPJ. Tente novamente.", ex);
            }
        }

        /// <summary>
        /// Normaliza o CNPJ informado, removendo caracteres que não sejam
        /// letras ou números e convertendo os caracteres para maiúsculo.
        /// </summary>
        /// <param name="cnpjInformado">CNPJ recebido como parâmetro de entrada.</param>
        /// <returns>
        /// CNPJ normalizado, contendo somente caracteres alfanuméricos em maiúsculo.
        /// </returns>
        private static string NormalizarCnpj(string cnpjInformado)
        {
            string cnpjNormalizado = Regex.Replace(cnpjInformado ?? "", @"[^0-9a-zA-Z]", "").ToUpperInvariant();

            return cnpjNormalizado;
        }

        /// <summary>
        /// Consulta a BrasilAPI utilizando o CNPJ normalizado e processa
        /// os dados cadastrais retornados pelo serviço.
        /// Trata os principais códigos de resposta HTTP e define os parâmetros
        /// de saída da Custom API conforme o resultado da consulta.
        /// </summary>
        /// <param name="cnpjInformado">CNPJ recebido da Custom API.</param>
        /// <param name="context">Contexto de execução utilizado para definir os parâmetros de saída.</param>
        /// <param name="tracing">Serviço de rastreamento utilizado para registrar informações da consulta.</param>
        /// <returns>
        /// Uma tarefa que representa a execução assíncrona da consulta à BrasilAPI.
        /// </returns>
        private async Task ConsultaDados(string cnpjInformado, IPluginExecutionContext context, ITracingService tracing)
        {
            string cnpj = NormalizarCnpj(cnpjInformado);

            string url = $"https://brasilapi.com.br/api/cnpj/v1/{cnpj}";

            tracing.Trace("Iniciando consulta ao serviço BrasilAPI.");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Dataverse-Integration/1.0");
                    httpClient.Timeout = TimeSpan.FromSeconds(15);
                    httpClient.DefaultRequestHeaders.ConnectionClose = true;

                    using (HttpResponseMessage response = await httpClient.GetAsync(url).ConfigureAwait(false))
                    {
                        int codigoStatus = (int)response.StatusCode;

                        tracing.Trace("BrasilAPI retornou HTTP {0}.", codigoStatus);

                        switch (codigoStatus)
                        {
                            case 400:
                                DefinirSaida(context, false, cnpj, "", 0, "Requisição inválida. Verifique o CNPJ informado.");
                                return;

                            case 404:
                                DefinirSaida(context, false, cnpj, "", 0, "CNPJ não encontrado na base da BrasilAPI.");
                                return;

                            case 429:
                                DefinirSaida(context, false, cnpj, "", 0, "Limite de consultas atingido. Tente novamente mais tarde.");
                                return;

                            case 503:
                                DefinirSaida(context, false, cnpj, "", 0, "Serviço BrasilAPI temporariamente indisponível. Tente novamente.");
                                return;
                        }

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            tracing.Trace("Erro HTTP não previsto: {0}", codigoStatus);

                            DefinirSaida(context, false, cnpj, "", 0, "Falha na consulta do CNPJ. Código HTTP: " + codigoStatus);
                            return;
                        }

                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            var serializer = new DataContractJsonSerializer(typeof(DadosBrasilApi));

                            DadosBrasilApi dados = (DadosBrasilApi)serializer.ReadObject(stream);

                            if (dados == null)
                            {
                                DefinirSaida(context, false, cnpj, "", 0, "A BrasilAPI retornou uma resposta vazia.");
                                return;
                            }

                            DefinirSaida(context, true, dados.Cnpj ?? cnpj, dados.RazaoSocial ?? "", dados.SituacaoCadastral, 
                                "CNPJ consultado com sucesso.");

                            tracing.Trace("Consulta ao BrasilAPI concluída com sucesso.");
                        }
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                tracing.Trace("Timeout na consulta à BrasilAPI. Detalhes: {0}", ex.Message);

                DefinirSaida(context, false, cnpj, "", 0, "Tempo limite excedido ao consultar o CNPJ. Tente novamente.");
            }
            catch (HttpRequestException ex)
            {
                tracing.Trace(
                    "Falha de comunicação com BrasilAPI. Detalhes: {0}", ex.Message);

                DefinirSaida(context, false, cnpj, "", 0, "Não foi possível estabelecer comunicação com a BrasilAPI.");
            }
            catch (Exception ex)
            {
                tracing.Trace("Erro inesperado ao processar a consulta. Tipo: {0}. Mensagem: {1}", ex.GetType().FullName, ex.Message);

                DefinirSaida(context, false, cnpj, "", 0, "Ocorreu um erro ao processar os dados do CNPJ.");
            }
        }

        /// <summary>
        /// Define os parâmetros de saída da Custom API com o resultado da consulta.
        /// </summary>
        /// <param name="context">Contexto de execução da Custom API.</param>
        /// <param name="sucesso">Indica se a consulta foi concluída com sucesso.</param>
        /// <param name="cnpj">CNPJ normalizado ou retornado pela BrasilAPI.</param>
        /// <param name="razaoSocial">Razão social da empresa consultada.</param>
        /// <param name="situacaoCadastral">Código numérico correspondente à situação cadastral da empresa.</param>
        /// <param name="mensagemCnpj">Mensagem descritiva sobre o resultado da consulta.</param>
        private static void DefinirSaida(IPluginExecutionContext context, bool sucesso, string cnpj, string razaoSocial,
            int situacaoCadastral, string mensagemCnpj)
        {
            context.OutputParameters["ptr_SucessoConsulta"] = sucesso;
            context.OutputParameters["ptr_Cnpj"] = cnpj;
            context.OutputParameters["ptr_RazaoSocial"] = razaoSocial;
            context.OutputParameters["ptr_SituacaoCadastral"] = situacaoCadastral;
            context.OutputParameters["ptr_MensagemCnpj"] = mensagemCnpj;
        }

        /// <summary>
        /// Representa a estrutura JSON retornada pelo serviço BrasilAPI.
        /// Os atributos DataMember associam as propriedades C# aos nomes
        /// correspondentes presentes na resposta JSON.
        /// </summary>

        [DataContract]
        private class DadosBrasilApi
        {
            [DataMember(Name = "cnpj")]
            public string Cnpj { get; set; }

            [DataMember(Name = "razao_social")]
            public string RazaoSocial { get; set; }

            [DataMember(Name = "situacao_cadastral")]
            public int SituacaoCadastral { get; set; }
        }
    }
}
